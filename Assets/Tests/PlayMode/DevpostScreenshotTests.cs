using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// BOB-055: Devpost screenshots from a normal match in the real scene, in English. Unlike RegressionTests, the rules
    /// (items, 30 s timer, random draw), the scene seed (0 = random) and the bot levels stay at their defaults, and the
    /// dispenser is never filled by the test. The only nudges: a Plastic Fork when the human was not dealt a Fork or
    /// Hammer, and HP 1 after the item shot so the knockout comes quickly. Files go to Logs/ci/screens/devpost-*.png;
    /// the runner copies them to Docs/Autopilot/screens/. Layout audits stay in RegressionTests.
    /// </summary>
    public class DevpostScreenshotTests : IPrebuildSetup
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Screens = "Logs/ci/screens";
        const int Bots = 4;
        CandyTableDirector director;
        VRTableInput input;
        Scene scene;
        float oldTimeScale;
        bool oldRunInBackground;
        int? oldLanguage;

        GameEngine Engine => Read<GameEngine>(director, "engine");
        TableSelectable Candy => Read<TableSelectable>(director, "candySelectable");
        bool HumanReady => Candy != null && Candy.interactable && Read<BotDecision>(director, "humanChoice") == null;
        PlayerState Human => Engine.Players.Single(p => !p.IsBot);
        RoomSetup Room => Read<RoomSetup>(director, "room");
        IEnumerable<object> Props => Read<IList>(director, "props").Cast<object>();

        public void Setup()
        {
#if UNITY_EDITOR
            // Same builder output as the APK build and the CI Budget Audit, also when this fixture runs alone.
            var builder = Type.GetType("BiteOrBetray.EditorTools.GameplayPerformanceBuilder, BiteOrBetray.Editor", true);
            builder.GetMethod("PrepareScenes", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
#endif
        }

        [UnitySetUp]
        public IEnumerator LoadTable()
        {
            oldTimeScale = Time.timeScale;
            oldRunInBackground = Application.runInBackground;
            Time.timeScale = 1;
            Application.runInBackground = true;
            // Devpost captures are always English; the laptop's choice is restored in TearDown.
            oldLanguage = PlayerPrefs.HasKey(GameSettings.LanguageKey) ? PlayerPrefs.GetInt(GameSettings.LanguageKey) : (int?)null;
            PlayerPrefs.SetInt(GameSettings.LanguageKey, (int)Language.English);
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "The director must come from the gameplay scene.");
            yield return Until(() => Read<bool>(director, "ready"), "menu finished Start");
            // Not rules: keep the results screen instead of looping, and write no telemetry CSV.
            director.loop = false;
            director.recordTelemetry = false;
            var defaults = new RuleConfig();
            Assert.AreEqual((defaults.ItemsPerRound, defaults.TurnTimeLimitSeconds, defaults.RandomDraw),
                (director.rules.ItemsPerRound, director.rules.TurnTimeLimitSeconds, director.rules.RandomDraw),
                "The scene must use the default rules for the Devpost captures.");
            // Same as RegressionTests: device polling off, actions go through TableSelectable, focus stays on.
            input = director.GetComponent<VRTableInput>();
            input.enabled = false;
            Invoke(input, "CreateVisuals");
            input.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            input.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Invoke(input, "OnInputFocusAcquired");
            yield return Until(() => input.HasFocus, "initial focus");
        }

        [UnityTearDown]
        public IEnumerator UnloadTable()
        {
            if (director != null) director.ReturnToMenu();
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-055 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = oldTimeScale;
            Application.runInBackground = oldRunInBackground;
            if (oldLanguage.HasValue) PlayerPrefs.SetInt(GameSettings.LanguageKey, oldLanguage.Value);
            else PlayerPrefs.DeleteKey(GameSettings.LanguageKey);
            PlayerPrefs.Save();
            Localization.Language = Language.English;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Normal_match_devpost_screens_menu_setup_onboarding_turn_item_spectator_results()
        {
            Assert.AreEqual("<b>Play vs Bots</b>", Button("Play vs Bots").GetComponent<TextMesh>().text, "Devpost captures must be English.");
            yield return Screenshot("devpost-01-menu");
            Select(Button("Play vs Bots"));
            while (Room.BotCount > Bots) Select(Button("Fewer bots"));
            while (Room.BotCount < Bots) Select(Button("More bots"));
            yield return Screenshot("devpost-02-bot-setup");

            Select(Button("Start"));
            yield return Until(() => Read<int>(director, "onboardingPage") == 0 && Button("Skip").interactable, "first onboarding page");
            yield return Screenshot("devpost-03-onboarding");
            Select(Button("Skip"));
            yield return Until(() => Engine != null, "match after Skip");

            // Random seed: bots may knock the human out before their first turn, then start a fresh match.
            for (int attempt = 1; ; attempt++)
            {
                yield return FastForward(() => HumanReady || Human.Eliminated, "first human turn");
                if (HumanReady) break;
                Assert.Less(attempt, 3, "The human was knocked out before their first turn in three matches.");
                var previous = Engine;
                director.ReturnToMenu();
                yield return null;
                Select(Button("Play vs Bots"));
                Select(Button("Start"));
                yield return Until(() => Engine != null && Engine != previous, "next match");
            }
            Assert.AreEqual(Bots + 1, Engine.Players.Count);
            Assert.IsNotEmpty(Human.Items, "Default rules deal items on the first round.");
            yield return new WaitForSeconds(1.5f); // d20 and round popups fade, the waiter's bubble stays
            yield return Screenshot("devpost-04-your-turn");
            yield return Screenshot("devpost-cover", 1920, 1280);

            // Item moment: a dealt Fork or Hammer if there is one, otherwise one Plastic Fork from the test.
            var item = new[] { ItemType.PlasticFork, ItemType.HammerCrush }.Cast<ItemType?>().FirstOrDefault(t => Human.Items.Contains(t.Value));
            if (item == null)
            {
                item = ItemType.PlasticFork;
                Engine.DebugGiveItem(Human, item.Value);
                Invoke(director, "RefreshProps");
            }
            TestContext.WriteLine("devpost-05-item: " + item.Value);
            int used = Engine.Stats.ItemsUsed[(int)item.Value];
            Select(FindProp(Human, item.Value));
            yield return Until(() => Engine.Stats.ItemsUsed[(int)item.Value] == used + 1, "use " + item.Value);
            // ItemEffect: the item reaches the candy after ~1.1 s, then the GOOD/BAD popup stays for 1.8 s.
            yield return new WaitForSeconds(1.6f);
            yield return Screenshot("devpost-05-item");

            // Knockout: the human keeps eating from the normal dispenser until a bad candy (or a bot's Give) lands.
            Engine.DebugSetHp(Human, 1);
            yield return FastForward(() => director.SpectatorInputLocked || Engine.IsOver, "human knocked out", true);
            Assert.IsTrue(Human.Eliminated, "The human won at HP 1; no spectator capture in this run.");
            var avatar = director.artRoot.Find("Seats/Seat_0").GetComponentInChildren<SeatedAvatar>(true);
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            yield return Until(() => renderers.All(r => r.sharedMaterials.All(m => m.name.EndsWith(" Spectator"))), "transparent spectator avatar");
            yield return new WaitForSeconds(1);
            yield return Screenshot("devpost-06-spectator");

            var result = director.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == "Hasil Akhir");
            yield return FastForward(() => result.gameObject.activeInHierarchy, "results screen");
            Assert.IsTrue(Engine.IsOver && Engine.Winner != null && Engine.Winner.IsBot);
            StringAssert.Contains(Strings.Winner.Text, result.text);
            yield return new WaitForSeconds(2); // last action popups fade
            yield return Screenshot("devpost-07-results");
        }

        // ------------------------------------------------------------------ match

        /// <summary>Bot turns at 20x; returns at 1x so the captures and the human's 30 s timer run in real time.</summary>
        IEnumerator FastForward(Func<bool> done, string message, bool humanEats = false)
        {
            float deadline = Time.realtimeSinceStartup + 180;
            Time.timeScale = 20;
            try
            {
                while (!done())
                {
                    Assert.Less(Time.realtimeSinceStartup, deadline, message + ": " + Engine?.Phase + ", turn " + Engine?.TurnNumber);
                    if (humanEats && HumanReady) Select(Candy);
                    yield return null;
                }
            }
            finally { Time.timeScale = 1; }
        }

        TableSelectable FindProp(PlayerState owner, ItemType type)
        {
            var prop = Props.FirstOrDefault(p => !Read<bool>(p, "pocket") && Read<ItemType>(p, "type") == type
                && Read<PlayerState>(Read<object>(p, "seat"), "player") == owner);
            Assert.IsNotNull(prop, $"Item {type} of {owner.Name} is not on the mat.");
            return Read<TableSelectable>(prop, "selectable");
        }

        // ------------------------------------------------------------------ screenshot

        /// <summary>Render the player's seat camera to a RenderTexture (not ScreenCapture) and save a PNG.</summary>
        IEnumerator Screenshot(string name, int width = 1280, int height = 720)
        {
            // In the menu the desktop camera has not moved to the seat yet; PlayGame does the same when a match starts.
            if (Engine == null) Invoke(director, "PlaceDesktopCamera");
            var camera = Camera.main;
            Assert.IsNotNull(camera, "Player seat camera (MainCamera) is missing.");
            var seat = director.artRoot.Find("Seats/Seat_0/HeadAnchor");
            Assert.Less(Vector3.Distance(seat.position, camera.transform.position), .15f, name + ": camera is not at the player's seat.");
            float oldAspect = camera.aspect;
            camera.aspect = width / (float)height;
            yield return null; // LateUpdate lays out the labels for the capture's real aspect ratio.
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
            }
            int colors = texture.GetPixels32().Where((_, i) => i % 97 == 0).Select(p => (p.r, p.g, p.b)).Distinct().Count();
            Directory.CreateDirectory(Screens);
            File.WriteAllBytes(Path.Combine(Screens, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
            Assert.Greater(colors, 32, $"Screenshot {name} is empty/uniform ({colors} colors).");
            TestContext.WriteLine($"{name}: {width}x{height}");
        }

        // ------------------------------------------------------------------ util

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Tombol " + label);

        static void Select(TableSelectable target)
        {
            Assert.IsNotNull(target);
            Assert.IsTrue(target.gameObject.activeInHierarchy && target.isActiveAndEnabled && target.interactable, target.name + " is not interactable");
            target.Select();
        }

        static IEnumerator Until(Func<bool> condition, string message, float seconds = 30)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, message);
                yield return null;
            }
        }

        static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    }
}
