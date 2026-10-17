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
    /// <summary>Angka Editor tanpa headset; loop ukur hanya menjumlahkan angka, tanpa format/log/alokasi fixture per frame.</summary>
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

        public void Setup()
        {
            // Editor assembly tidak direferensikan oleh asmdef PlayMode yang juga dapat di-build ke player.
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
            report.AppendLine("GC = ProfilerRecorder Memory/GC Allocated In Frame (byte, seluruh frame termasuk Editor/Test Runner).");
            report.AppendLine("Draw/SetPass = UnityStats, seluruh render Editor mono, BUKAN draw/mata XR atau bukti 72 FPS Quest.");
            report.AppendLine("Timer input mati, tempo=4, timeScale=1, 5 bot; isi dispenser/inventory dikontrol fixture. Animasi/komponen gameplay tetap aktif.");
            Directory.CreateDirectory("Logs/ci");
            if (File.Exists("Logs/ci/perf-prepare.txt")) report.Append(File.ReadAllText("Logs/ci/perf-prepare.txt"));
            SaveReport();
            int mobile = Array.IndexOf(QualitySettings.names, "Mobile");
            Assert.GreaterOrEqual(mobile, 0);
            QualitySettings.SetQualityLevel(mobile, true);
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director);
            yield return Until(() => Read<bool>(director, "ready"), "menu siap");
            Assert.IsNotNull(director.artRoot.Find("Performance Geometry"), "Prebuild wajib menyiapkan scene yang juga diaudit/dibuild.");
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
            QualitySettings.SetQualityLevel(oldQuality, true);
            Time.timeScale = oldTimeScale;
            Application.runInBackground = oldRunInBackground;
        }

        [UnityTest]
        public IEnumerator Gameplay_idle_gc_render_stats_and_automated_match_are_recorded()
        {
            Button("Main vs Bot").Select();
            var room = Read<RoomSetup>(director, "room");
            while (room.BotCount < room.MaxBots) Button("Tambah bot").Select();
            var seats = room.BuildSeats(director.humanName, director.botNames);
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
            Button("Mulai").Select();
            yield return Until(() => Read<GameEngine>(director, "engine") != null && HumanReady(), "giliran manusia pertama");
            var engine = Read<GameEngine>(director, "engine");
            var human = engine.Players.Single(p => !p.IsBot);
            var victim = engine.Players.First(p => p.IsBot);
            engine.DebugSetPool(Enumerable.Range(0, 64).Select(_ => new Candy(CandyColor.Yellow, true)));
            var telemetry = Read<TelemetryRecorder>(director, "telemetry");
            Assert.IsNotNull(telemetry);
            // Tunggu popup d20/ronde dan JIT; jangan ukur pembuatan coroutine/recorder sebagai idle.
            float warmupUntil = Time.realtimeSinceStartup + 3;
            for (int i = 0; i < 120 || Time.realtimeSinceStartup < warmupUntil; i++) yield return null;
            var camera = Camera.main;
            Assert.IsNotNull(camera);
            report.AppendLine(FormattableString.Invariant($"camera={camera.name}, FOV={camera.fieldOfView:0.0}, pixel={camera.pixelWidth}x{camera.pixelHeight}; quality=Mobile"));
            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                Assert.IsTrue(gc.Valid, "Counter GC tidak tersedia, jangan laporkan nol palsu.");
                yield return null;
                yield return null;
                var idleOn = new Samples();
                float idleUntil = Time.realtimeSinceStartup + 3;
                for (int i = 0; i < IdleFrames || Time.realtimeSinceStartup < idleUntil; i++) { yield return null; idleOn.Add(gc); }
                Assert.IsTrue(HumanReady(), "Window idle harus tetap menunggu input manusia.");
                report.AppendLine(idleOn.Line("idle_telemetry_on"));
                SaveReport();

                Write(director, "telemetry", null);
                // Jalur perbandingan tetap game/karakter/seed yang sama; tidak menonaktifkan komponen gameplay.
                for (int i = 0; i < 60; i++) yield return null;
                var idleOff = new Samples();
                idleUntil = Time.realtimeSinceStartup + 3;
                for (int i = 0; i < IdleFrames || Time.realtimeSinceStartup < idleUntil; i++) { yield return null; idleOff.Add(gc); }
                Write(director, "telemetry", telemetry);
                report.AppendLine(idleOff.Line("idle_telemetry_off"));
                report.AppendLine(FormattableString.Invariant($"idle_gc_delta_on_minus_off_B_per_frame={idleOn.GcAverage - idleOff.GcAverage:0.00}"));
                report.AppendLine($"idle_gc_near_zero_1KiB={idleOn.GcAverage <= 1024}; angka mentah, tanpa mengurangi overhead Editor.");
                SaveReport();
                Assert.Greater(idleOn.GcSamples, 0);
                Assert.Greater(idleOff.GcSamples, 0);
                Assert.Greater(idleOn.DrawPeak, 0, "UnityStats tidak mencatat render; jalankan tanpa -nographics.");

                engine.DebugGiveItem(human, ItemType.PlasticFork);
                engine.DebugGiveItem(victim, ItemType.HammerCrush);
                Invoke(director, "RefreshProps");
                var unchanged = Prop(victim);
                var unchangedId = unchanged.GetEntityId();
                using (var refresh = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BoB.RefreshProps", 64))
                {
                    Prop(human).Select();
                    yield return Until(() => engine.Stats.ItemsUsed[(int)ItemType.PlasticFork] == 1 && HumanReady(), "aksi item selesai");
                    Assert.AreSame(unchanged, Prop(victim), "Item pemain lain tidak boleh dibuat ulang.");
                    Candy().Select();
                    yield return Until(() => engine.Stats.EatSelf == 1 && HumanReady(), "aksi Eat selesai");
                    Assert.AreSame(unchanged, Prop(victim), "Eat tidak boleh membuat ulang inventory peserta lain.");
                    report.AppendLine($"props_unchanged_instance_id={unchangedId}; tetap setelah PlasticFork dan Eat.");
                    if (refresh.Valid && refresh.Count > 0)
                    {
                        double sum = 0; long peak = 0;
                        for (int i = 0; i < refresh.Count; i++)
                        { long value = refresh.GetSample(i).Value; sum += value; peak = Math.Max(peak, value); }
                        report.AppendLine(FormattableString.Invariant($"BoB.RefreshProps samples={refresh.Count}, avg_ns={sum / refresh.Count:0.00}, peak_ns={peak}; sesudah optimasi, tanpa capture sebelum."));
                    }
                    else report.AppendLine("BoB.RefreshProps=n/a; marker tidak tersedia, identitas prop diuji terpisah.");
                }
                // Akhiri lewat callback manusia dan coroutine bot asli; pool bad membuat hasil deterministik/cepat.
                engine.DebugSetPool(Enumerable.Range(0, 128).Select(_ => new Candy(CandyColor.Blue, false)));
                var match = new Samples();
                var restartButton = Button("Main lagi");
                float deadline = Time.realtimeSinceStartup + 120;
                bool timedOut = false;
                while (!restartButton.interactable)
                {
                    if (Time.realtimeSinceStartup > deadline) { timedOut = true; break; }
                    if (HumanReady()) Candy().Select();
                    yield return null;
                    match.Add(gc);
                }
                report.AppendLine(match.Line("automated_match"));
                SaveReport();
                Assert.IsFalse(timedOut, "Pertandingan otomatis macet.");
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

        struct Samples
        {
            public int GcSamples, DrawPeak;
            int frames, setPassPeak;
            long gcSum, gcPeak, drawSum, setPassSum;
            double frameSum, framePeak;
            public double GcAverage => GcSamples > 0 ? (double)gcSum / GcSamples : double.NaN;

            public void Add(ProfilerRecorder gc)
            {
                frames++;
                if (gc.Count > 0) { long value = gc.LastValue; GcSamples++; gcSum += value; gcPeak = Math.Max(gcPeak, value); }
                int draws = UnityStats.drawCalls, passes = UnityStats.setPassCalls;
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

        void SaveReport()
        {
            if (report != null) File.WriteAllText("Logs/ci/perf.txt", report + $"status={(complete ? "complete" : "incomplete (lihat hasil tes)")}\n");
        }

        TableSelectable Candy() => (TableSelectable)CandyField.GetValue(director);
        bool HumanReady() => Candy() != null && Candy().interactable && ChoiceField.GetValue(director) == null;
        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true).Single(t => t.name == "Tombol " + label);
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
