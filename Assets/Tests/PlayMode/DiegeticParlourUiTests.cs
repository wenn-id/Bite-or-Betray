using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BiteOrBetray.Art3D;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    public class DiegeticParlourUiTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        CandyTableDirector director;
        VRTableInput input;
        Scene scene;
        float oldTimeScale;
        bool oldRunInBackground;

        GameEngine Engine => Read<GameEngine>(director, "engine");
        TableSelectable Candy => Read<TableSelectable>(director, "candySelectable");
        bool HumanReady => Candy != null && Candy.interactable && Read<BotDecision>(director, "humanChoice") == null;
        PlayerState Human => Engine.Players.Single(p => !p.IsBot);

        [UnitySetUp]
        public IEnumerator LoadParlour()
        {
            oldTimeScale = Time.timeScale;
            oldRunInBackground = Application.runInBackground;
            Time.timeScale = 1f;
            Application.runInBackground = true;
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director);
            yield return Until(() => Read<bool>(director, "ready"), "director ready");
            director.loop = false;
            director.recordTelemetry = false;
            director.tempo = 4f;
            director.thinkSeconds = Vector2.zero;
            director.rules.TurnTimeLimitSeconds = 0;
            director.rules.RandomDraw = false;
            input = director.GetComponent<VRTableInput>();
            input.enabled = false;
            Invoke(input, "CreateVisuals");
            input.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            input.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Invoke(input, "OnInputFocusAcquired");
            yield return Until(() => input.HasFocus, "focus");
            Time.timeScale = 10f;
        }

        [UnityTearDown]
        public IEnumerator UnloadParlour()
        {
            if (director != null) director.ReturnToMenu();
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("DiegeticParlourCleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = oldTimeScale;
            Application.runInBackground = oldRunInBackground;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Bundled_parlour_font_diegetic_props_and_quiet_table_work_through_gameplay()
        {
            var font = Read<Font>(director, "font");
            Assert.IsNotNull(font);
            StringAssert.Contains("BoBParlourFont", font.name);

            var preview = Camera.main.GetComponent<ArtPreviewControls>();
            if (preview != null) { preview.tableOverview = false; preview.enabled = false; }
            yield return null;
            var lectern = Read<GameObject>(director, "menuLectern");
            var easel = Read<GameObject>(director, "chalkboardEasel");
            Assert.IsNotNull(lectern);
            Assert.IsNotNull(easel);
            Assert.IsTrue(lectern.activeSelf, "Lectern must be visible on seated main menu.");
            Assert.AreEqual(ShadowCastingMode.Off, lectern.GetComponent<MeshRenderer>().shadowCastingMode);
            Assert.AreEqual(1, lectern.GetComponent<MeshFilter>().sharedMesh.subMeshCount);
            Assert.AreEqual(ShadowCastingMode.Off, easel.GetComponent<MeshRenderer>().shadowCastingMode);
            Assert.AreEqual(1, easel.GetComponent<MeshFilter>().sharedMesh.subMeshCount);

            var room = Read<RoomSetup>(director, "room");
            Button("Play vs Bots").Select();
            var seats = room.BuildSeats(director.HumanName, director.botNames);
            for (int s = 100; s < 900; s++)
            {
                var g = new GameEngine(director.rules.Clone(), s) { KeepLog = false };
                g.Start(seats);
                if (!g.Current.IsBot) { director.seed = s - Read<int>(director, "gameIndex"); break; }
            }
            bool onboardingDone = Read<bool>(director, "onboardingDone");
            Button("Start").Select();
            if (!onboardingDone)
            {
                yield return Until(() => Button("Skip").interactable, "onboarding skip ready");
                Button("Skip").Select();
            }
            yield return Until(() => Engine != null && HumanReady, "human turn ready");
            yield return null;

            Assert.IsFalse(lectern.activeSelf, "Lectern folds away during active gameplay.");
            Assert.IsTrue(easel.activeSelf, "Parlour chalkboard easel stands active during match.");

            var humanSeat = Read<object>(director, "human");
            var strip = Read<ParlourDiegeticProps.SugarHeartStrip>(humanSeat, "heartStrip");
            Assert.IsNotNull(strip);
            Assert.IsTrue(strip.gameObject.activeSelf);
            Assert.AreEqual(1, strip.gameObject.GetComponent<MeshFilter>().sharedMesh.subMeshCount);
            Assert.IsTrue(strip.TurnLit, "Turn star medallion lights up on active player.");

            Engine.DebugSetHp(Human, 2);
            Engine.DebugGiveItem(Human, ItemType.Cupcake);
            Invoke(director, "RefreshProps");
            var cupcakeBtn = Read<IList>(director, "props").Cast<object>()
                .Select(p => (prop: p, btn: Read<TableSelectable>(p, "selectable")))
                .First(x => Read<ItemType>(x.prop, "type") == ItemType.Cupcake && x.btn.interactable).btn;
            cupcakeBtn.Select();
            yield return Until(() => Human.HasCupcake && HumanReady, "cupcake active");
            yield return null;
            Assert.AreEqual(2, strip.ActiveHearts);
            Assert.IsTrue(strip.CupcakeLit, "Cupcake shield tints rim and top heart ice-blue.");

            // Candy-targeting items (e.g., PlasticFork) must aim at and touch the active candy in CandyTray, never the Dispenser
            Time.timeScale = 1f;
            director.tempo = 1f;
            Engine.DebugGiveItem(Human, ItemType.PlasticFork);
            Invoke(director, "RefreshProps");
            var forkBtn = Read<IList>(director, "props").Cast<object>()
                .Select(p => (prop: p, btn: Read<TableSelectable>(p, "selectable")))
                .First(x => Read<ItemType>(x.prop, "type") == ItemType.PlasticFork && x.btn.interactable).btn;
            Vector3 activeCandyPos = Candy.transform.position;
            Vector3 dispenserPos = Read<Vector3>(director, "tableCenter");
            forkBtn.Select();
            Transform liveFork = null;
            yield return Until(() =>
            {
                liveFork = director.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t != null && t.parent == director.transform && t.name.StartsWith("PlasticFork"));
                if (liveFork == null) return false;
                var renderers = liveFork.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return false;
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                return Vector3.Distance(b.ClosestPoint(activeCandyPos), activeCandyPos) < 0.030f;
            }, "fork prongs touch active candy in CandyTray");
            Assert.IsNotNull(liveFork);
            Vector3 toCandy = (activeCandyPos - liveFork.position).normalized;
            Assert.Greater(Vector3.Dot(liveFork.forward, toCandy), 0.85f,
                "PlasticFork prongs must point into the active candy in CandyTray, not toward the Dispenser.");
            Assert.Less(Vector3.Distance(liveFork.position, activeCandyPos),
                Vector3.Distance(liveFork.position, dispenserPos) * 0.55f,
                "PlasticFork must stay at CandyTray rather than drifting over the Dispenser.");
            Time.timeScale = 10f;
            director.tempo = 4f;
            yield return Until(() => HumanReady, "fork animation complete");

            var narration = director.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == "Narration");
            Write(director, "captionUntil", Time.unscaledTime - 1f);
            Write(director, "decisionThink", 0f);
            Write(director, "turnHintShown", false);
            narration.text = "";
            yield return null;

            Engine.DebugSetPool(new[] { new Candy(CandyColor.Blue, true), new Candy(CandyColor.Yellow, true), new Candy(CandyColor.Red, false) });
            int eatsBefore = Engine.Stats.EatSelf;
            Candy.Select();
            yield return Until(() => Engine.Stats.EatSelf == eatsBefore + 1 && HumanReady, "eat candy");
            Assert.IsEmpty(narration.text, "Routine eating uses 3D animations and ticket popups without narration spam.");

            Write(director, "decisionThink", 6.1f);
            yield return null;
            StringAssert.Contains(Strings.YourTurn.Text, narration.text, "Round 1 gentle idle hint triggers after 6s.");
        }

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(b => b.name == "Button " + label);

        static IEnumerator Until(Func<bool> condition, string msg, float timeout = 30f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, end, msg);
                yield return null;
            }
        }

        static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
        static void Write(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    }
}
