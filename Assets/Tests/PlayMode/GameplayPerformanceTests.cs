#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BiteOrBetray.Core;
using BiteOrBetray.DebugTools;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>Editor numbers without a headset; the measurement loop only sums numbers, with no per-frame formatting/logging/fixture allocation.</summary>
    public class GameplayPerformanceTests : IPrebuildSetup
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const int IdleFrames = 240;
        static readonly FieldInfo CandyField = typeof(CandyTableDirector).GetField("candySelectable", Fields);
        static readonly FieldInfo ChoiceField = typeof(CandyTableDirector).GetField("humanChoice", Fields);
        CandyTableDirector director;
        Scene scene;
        float oldTimeScale;
        int oldQuality;
        bool oldRunInBackground;
        StringBuilder report;
        bool complete;
        RenderTexture batchTarget;

        public void Setup()
        {
            // The Editor assembly is not referenced by the PlayMode asmdef, which can also be built into a player.
            var builder = Type.GetType("BiteOrBetray.EditorTools.GameplayPerformanceBuilder, BiteOrBetray.Editor", true);
            builder.GetMethod("PrepareScenes", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        [UnitySetUp]
        public IEnumerator LoadTable()
        {
            oldTimeScale = Time.timeScale;
            oldQuality = QualitySettings.GetQualityLevel();
            oldRunInBackground = Application.runInBackground;
            Time.timeScale = 1;
            Application.runInBackground = true;
            report = new StringBuilder();
            complete = false;
            report.AppendLine($"BOB-022 | Editor {Application.unityVersion} | {SystemInfo.graphicsDeviceName} | {DateTime.UtcNow:O}");
            report.AppendLine("GC = ProfilerRecorder Memory/GC Allocated In Frame (bytes, whole frame including Editor/Test Runner).");
            report.AppendLine("Draw/SetPass = max(ProfilerRecorder Render Draw Calls/SetPass Calls Count, UnityStats), whole mono Editor render, NOT XR draws/eye or proof of 72 FPS on Quest.");
            if (Application.isBatchMode)
                report.AppendLine("Editor -batchmode does not draw the Game view: the main camera is rendered manually to a RenderTexture every measured frame (GC/frame includes that render).");
            report.AppendLine("Input timer off, tempo=4, timeScale=1, 5 bots; dispenser/inventory contents controlled by the fixture. Animation/gameplay components stay active.");
            Directory.CreateDirectory("Logs/ci");
            if (File.Exists("Logs/ci/perf-prepare.txt")) report.Append(File.ReadAllText("Logs/ci/perf-prepare.txt"));
            SaveReport();
            int mobile = Array.IndexOf(QualitySettings.names, "Mobile");
            Assert.GreaterOrEqual(mobile, 0);
            QualitySettings.SetQualityLevel(mobile, true);
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director);
            yield return Until(() => Read<bool>(director, "ready"), "menu ready");
            Assert.IsNotNull(director.artRoot.Find("Performance Geometry"), "Prebuild must prepare the scene that is also audited/built.");
            director.loop = false;
            director.showLog = false;
            director.recordTelemetry = true;
            director.tempo = 4;
            director.thinkSeconds = Vector2.zero;
            director.rules.TurnTimeLimitSeconds = 0;
            director.rules.ItemsPerRound = 0;
            director.rules.RandomDraw = false;
            director.GetComponent<VRTableInput>().logInput = false;
            Assert.IsFalse(director.GetComponent<VRTableInput>().IsXRActive);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SaveReport();
            if (director != null) director.ReturnToMenu();
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-022 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (batchTarget != null) RenderTexture.ReleaseTemporary(batchTarget);
            batchTarget = null;
            QualitySettings.SetQualityLevel(oldQuality, true);
            Time.timeScale = oldTimeScale;
            Application.runInBackground = oldRunInBackground;
        }

        [UnityTest]
        public IEnumerator Gameplay_idle_gc_render_stats_and_automated_match_are_recorded()
        {
            // BOB-047: the optimization only replaces SDK visuals; tracking sources and avatar geometry remain.
            var tracking = director.artRoot.GetComponentInChildren<LocalAvatarTracking>(true);
            Assert.IsNotNull(tracking);
            Assert.IsTrue(tracking.isActiveAndEnabled && tracking.avatar.isActiveAndEnabled);
            Assert.AreSame(director.GetComponent<VRTableInput>(), tracking.tableInput);
            var rig = tracking.headTarget.root;
            var sdkRenderers = rig.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(sdkRenderers);
            CollectionAssert.AreEquivalent(sdkRenderers, tracking.controllerRenderers, "Includes dynamic hands: LocalAvatarTracking must hide them while XR is active.");
            Assert.IsTrue(sdkRenderers.All(r => !r.enabled), "SDK renderers are saved disabled, without deactivating the rig/sensors.");
            Assert.IsTrue(rig.GetComponentsInChildren<OVRHand>(true).Length >= 2, "Both hand sensors remain.");
            Assert.IsNotNull(rig.Find("TrackingSpace/LeftHandAnchor/LeftControllerAnchor"));
            Assert.IsNotNull(rig.Find("TrackingSpace/RightHandAnchor/RightControllerAnchor"));
            Assert.IsTrue(director.artRoot.GetComponentsInChildren<Renderer>(true)
                .All(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off));
            foreach (var skin in tracking.avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!AssetDatabase.GetAssetPath(skin.sharedMesh).StartsWith("Assets/BiteOrBetray/PerformanceMeshes/Skin_", StringComparison.Ordinal)) continue;
                var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(skin);
                Assert.IsNotNull(original, "Prefab source mesh is still available.");
                var source = original.sharedMesh;
                var mesh = skin.sharedMesh;
                Assert.AreEqual(source.vertexCount, mesh.vertexCount);
                Assert.AreEqual(source.blendShapeCount, mesh.blendShapeCount);
                CollectionAssert.AreEqual(source.bindposes, mesh.bindposes);
                CollectionAssert.AreEqual(source.boneWeights, mesh.boneWeights);
                CollectionAssert.AreEqual(source.vertices, mesh.vertices);
                CollectionAssert.AreEquivalent(Enumerable.Range(0, source.subMeshCount).SelectMany(source.GetIndices).ToArray(),
                    Enumerable.Range(0, mesh.subMeshCount).SelectMany(mesh.GetIndices).ToArray(), "No indices may be lost when material slots are merged.");
            }
            Button("Play vs Bots").Select();
            var room = Read<RoomSetup>(director, "room");
            while (room.BotCount < room.MaxBots) Button("More bots").Select();
            var seats = room.BuildSeats(director.HumanName, director.botNames);
            director.seed = 0;
            for (int seed = 100; seed <= 1100; seed++)
            {
                var probe = new GameEngine(director.rules.Clone(), seed) { KeepLog = false };
                probe.Start(seats);
                if (probe.Current.IsBot) continue;
                director.seed = seed;
                break;
            }
            Assert.AreNotEqual(0, director.seed);
            Write(director, "onboardingDone", true);
            Button("Start").Select();
            yield return Until(() => Read<GameEngine>(director, "engine") != null && HumanReady(), "first human turn");
            var engine = Read<GameEngine>(director, "engine");
            var human = engine.Players.Single(p => !p.IsBot);
            var victim = engine.Players.First(p => p.IsBot);
            engine.DebugSetPool(Enumerable.Range(0, 64).Select(_ => new Candy(CandyColor.Yellow, true)));
            var telemetry = Read<TelemetryRecorder>(director, "telemetry");
            Assert.IsNotNull(telemetry);
            // Wait for the d20/round popups and JIT; do not measure coroutine/recorder creation as idle.
            float warmupUntil = Time.realtimeSinceStartup + 3;
            for (int i = 0; i < 120 || Time.realtimeSinceStartup < warmupUntil; i++) yield return null;
            var camera = Camera.main;
            Assert.IsNotNull(camera);
            report.AppendLine(FormattableString.Invariant($"camera={camera.name}, FOV={camera.fieldOfView:0.0}, pixel={camera.pixelWidth}x{camera.pixelHeight}; quality=Mobile"));
            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            using (var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var passes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
            {
                Assert.IsTrue(gc.Valid, "GC counter unavailable; do not report a false zero.");
                var render = new RenderCounters(draws, passes);
                RenderBatchFrame(camera);
                yield return null;
                RenderBatchFrame(camera);
                yield return null;
                var idleOn = new Samples();
                float idleUntil = Time.realtimeSinceStartup + 3;
                for (int i = 0; i < IdleFrames || Time.realtimeSinceStartup < idleUntil; i++) { RenderBatchFrame(camera); yield return null; idleOn.Add(gc, render); }
                Assert.IsTrue(HumanReady(), "The idle window must still be waiting for human input.");
                report.AppendLine(idleOn.Line("idle_telemetry_on"));
                SaveReport();

                Write(director, "telemetry", null);
                // The comparison path keeps the same game/characters/seed; it does not disable gameplay components.
                for (int i = 0; i < 60; i++) { RenderBatchFrame(camera); yield return null; }
                var idleOff = new Samples();
                idleUntil = Time.realtimeSinceStartup + 3;
                for (int i = 0; i < IdleFrames || Time.realtimeSinceStartup < idleUntil; i++) { RenderBatchFrame(camera); yield return null; idleOff.Add(gc, render); }
                Write(director, "telemetry", telemetry);
                report.AppendLine(idleOff.Line("idle_telemetry_off"));
                report.AppendLine(FormattableString.Invariant($"idle_gc_delta_on_minus_off_B_per_frame={idleOn.GcAverage - idleOff.GcAverage:0.00}"));
                report.AppendLine($"idle_gc_near_zero_1KiB={idleOn.GcAverage <= 1024}; raw numbers, Editor overhead not subtracted.");
                SaveReport();
                Assert.Greater(idleOn.GcSamples, 0);
                Assert.Greater(idleOff.GcSamples, 0);
                Assert.Greater(idleOn.DrawPeak, 0, "Render counters/UnityStats recorded no rendering; run without -nographics.");

                engine.DebugGiveItem(human, ItemType.PlasticFork);
                engine.DebugGiveItem(victim, ItemType.HammerCrush);
                Invoke(director, "RefreshProps");
                var unchanged = Prop(victim);
                var unchangedId = unchanged.GetEntityId();
                using (var refresh = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BoB.RefreshProps", 64))
                {
                    Prop(human).Select();
                    yield return Until(() => engine.Stats.ItemsUsed[(int)ItemType.PlasticFork] == 1 && HumanReady(), "item action finished");
                    Assert.AreSame(unchanged, Prop(victim), "Other players' items must not be recreated.");
                    Candy().Select();
                    yield return Until(() => engine.Stats.EatSelf == 1 && HumanReady(), "Eat action finished");
                    Assert.AreSame(unchanged, Prop(victim), "Eat must not recreate other participants' inventories.");
                    report.AppendLine($"props_unchanged_instance_id={unchangedId}; unchanged after PlasticFork and Eat.");
                    if (refresh.Valid && refresh.Count > 0)
                    {
                        double sum = 0; long peak = 0;
                        for (int i = 0; i < refresh.Count; i++)
                        { long value = refresh.GetSample(i).Value; sum += value; peak = Math.Max(peak, value); }
                        report.AppendLine(FormattableString.Invariant($"BoB.RefreshProps samples={refresh.Count}, avg_ns={sum / refresh.Count:0.00}, peak_ns={peak}; after optimization, no before capture."));
                    }
                    else report.AppendLine("BoB.RefreshProps=n/a; marker unavailable, prop identity tested separately.");
                }
                // Finish through the real human callback and bot coroutines; an all-bad pool makes the result deterministic/fast.
                engine.DebugSetPool(Enumerable.Range(0, 128).Select(_ => new Candy(CandyColor.Blue, false)));
                var match = new Samples();
                var restartButton = Button("Play again");
                float deadline = Time.realtimeSinceStartup + 120;
                bool timedOut = false;
                while (!restartButton.interactable)
                {
                    if (Time.realtimeSinceStartup > deadline) { timedOut = true; break; }
                    if (HumanReady()) Candy().Select();
                    RenderBatchFrame(camera);
                    yield return null;
                    match.Add(gc, render);
                }
                report.AppendLine(match.Line("automated_match"));
                SaveReport();
                Assert.IsFalse(timedOut, "Automated match got stuck.");
                Assert.IsTrue(engine.IsOver);
                Assert.IsNotNull(engine.Winner);
                Assert.Greater(match.GcSamples, 0);
            }
            File.Copy(telemetry.FilePath, "Logs/ci/perf-telemetry.csv", true);
            var lines = File.ReadAllLines(telemetry.FilePath);
            Assert.AreEqual(TelemetryRecorder.Header, lines[0]);
            Assert.AreEqual(1, lines.Skip(1).Count(l => l.Split(',')[8] == "GAME_OVER"));
            Assert.IsTrue(lines.Skip(1).Any(l => l.Split(',')[7] == "0" && l.Split(',')[8] != "GAME_OVER"));
            Assert.IsTrue(lines.Skip(1).Any(l => l.Split(',')[7] == "1" && l.Split(',')[8] != "GAME_OVER"));
            report.AppendLine($"match_over=true, turns={engine.TurnNumber}, telemetry_rows={lines.Length - 1}; CSV=Logs/ci/perf-telemetry.csv");
            report.AppendLine(lines[0]);
            report.AppendLine(lines[1]);
            report.AppendLine(lines[lines.Length - 1]);
            complete = true;
            SaveReport();
        }

        /// <summary>Render counters of the previous frame; Editor UnityStats can stay 0 when the Game view is not drawn.</summary>
        readonly struct RenderCounters
        {
            readonly ProfilerRecorder draws, passes;
            public RenderCounters(ProfilerRecorder draws, ProfilerRecorder passes) { this.draws = draws; this.passes = passes; }
            public int Draws => Math.Max(UnityStats.drawCalls, Last(draws));
            public int Passes => Math.Max(UnityStats.setPassCalls, Last(passes));
            static int Last(ProfilerRecorder recorder) => recorder.Valid && recorder.Count > 0 ? (int)recorder.LastValue : 0;
        }

        struct Samples
        {
            public int GcSamples, DrawPeak;
            int frames, setPassPeak;
            long gcSum, gcPeak, drawSum, setPassSum;
            double frameSum, framePeak;
            public double GcAverage => GcSamples > 0 ? (double)gcSum / GcSamples : double.NaN;

            public void Add(ProfilerRecorder gc, RenderCounters render)
            {
                frames++;
                if (gc.Count > 0) { long value = gc.LastValue; GcSamples++; gcSum += value; gcPeak = Math.Max(gcPeak, value); }
                int draws = render.Draws, passes = render.Passes;
                drawSum += draws; setPassSum += passes;
                DrawPeak = Math.Max(DrawPeak, draws); setPassPeak = Math.Max(setPassPeak, passes);
                double ms = Time.unscaledDeltaTime * 1000;
                frameSum += ms; framePeak = Math.Max(framePeak, ms);
            }

            public string Line(string name) => string.Format(CultureInfo.InvariantCulture,
                "{0}: frames={1}, gc_samples={2}, gc_avg_B={3:0.00}, gc_peak_B={4}, draw_avg={5:0.00}, draw_peak={6}, setpass_avg={7:0.00}, setpass_peak={8}, frame_avg_ms={9:0.000}, frame_peak_ms={10:0.000}",
                name, frames, GcSamples, GcAverage, gcPeak, frames > 0 ? (double)drawSum / frames : double.NaN, DrawPeak,
                frames > 0 ? (double)setPassSum / frames : double.NaN, setPassPeak, frames > 0 ? frameSum / frames : double.NaN, framePeak);
        }

        /// <summary>
        /// Editor -batchmode (CI runner) never draws the Game view, so measured frames would have no rendering at all.
        /// The main camera is rendered to a RenderTexture like the BOB-023 screenshots; a windowed Editor still uses the Game view.
        /// </summary>
        void RenderBatchFrame(Camera camera)
        {
            if (!Application.isBatchMode) return;
            if (batchTarget == null) batchTarget = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 24);
            var oldTarget = camera.targetTexture;
            camera.targetTexture = batchTarget;
            camera.Render();
            camera.targetTexture = oldTarget;
        }

        void SaveReport()
        {
            if (report != null) File.WriteAllText("Logs/ci/perf.txt", report + $"status={(complete ? "complete" : "incomplete (see test results)")}\n");
        }

        TableSelectable Candy() => (TableSelectable)CandyField.GetValue(director);
        bool HumanReady() => Candy() != null && Candy().interactable && ChoiceField.GetValue(director) == null;
        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true).Single(t => t.name == "Button " + label);
        TableSelectable Prop(PlayerState player) => Read<IList>(director, "props").Cast<object>()
            .Where(p => Read<PlayerState>(Read<object>(p, "seat"), "player") == player).Select(p => Read<TableSelectable>(p, "selectable")).Single();

        static IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!condition()) { Assert.Less(Time.realtimeSinceStartup, deadline, message); yield return null; }
        }

        static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
        static void Write(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        static void Invoke(object target, string method) => target.GetType().GetMethod(method, Fields).Invoke(target, null);
    }
}
#endif
