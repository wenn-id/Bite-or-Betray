using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BiteOrBetray.Art3D;
using BiteOrBetray.Core;
using BiteOrBetray.DebugTools;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// Regression replacing the Quest 3 test (BOB-023). One test per scenario so the "Failed tests" section in CI.md names
    /// the scenario directly. Same fixture as OfflineMatchTests: tests only prepare device samples, dispenser contents,
    /// HP and inventory; actions still go through TableSelectable/VRTableInput and director coroutines.
    /// Screenshots from the player seat camera (Camera.Render to RenderTexture) are saved to Logs/ci/screens/; the runner
    /// copies them to Docs/Autopilot/screens/ for owner approval.
    /// </summary>
    public class RegressionTests : IPrebuildSetup
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const string Screens = "Logs/ci/screens";
        CandyTableDirector director;
        VRTableInput input;
        GameplayAudio audio;
        Scene scene;
        float oldTimeScale, oldVolume;
        bool oldRunInBackground;
        int? oldLanguage;
        // Cues that were ever assigned to the SFX pool; the clip stays assigned after it finishes, so per-frame polling is enough.
        readonly HashSet<GameplaySound> heard = new HashSet<GameplaySound>();
        readonly Dictionary<Transform, Bounds> headBounds = new Dictionary<Transform, Bounds>();

        GameEngine Engine => Read<GameEngine>(director, "engine");
        TableSelectable Candy => Read<TableSelectable>(director, "candySelectable");
        bool HumanReady => Candy != null && Candy.interactable && Read<BotDecision>(director, "humanChoice") == null;
        PlayerState Human => Engine.Players.Single(p => !p.IsBot);
        RoomSetup Room => Read<RoomSetup>(director, "room");
        IEnumerable<object> Props => Read<IList>(director, "props").Cast<object>();
        IEnumerable<TableSelectable> BotTargets => Read<IDictionary>(director, "botTargets").Values.Cast<TableSelectable>();

        public void Setup()
        {
#if UNITY_EDITOR
            // Filtered tests must also load the same builder output as the APK build and the CI Budget Audit.
            var builder = Type.GetType("BiteOrBetray.EditorTools.GameplayPerformanceBuilder, BiteOrBetray.Editor", true);
            builder.GetMethod("PrepareScenes", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
#endif
        }

        [UnitySetUp]
        public IEnumerator LoadTable()
        {
            oldTimeScale = Time.timeScale;
            oldVolume = AudioListener.volume;
            oldRunInBackground = Application.runInBackground;
            Time.timeScale = 1;
            Application.runInBackground = true;
            heard.Clear();
            headBounds.Clear();
            // Screenshots and texts checked by this suite are always English (the default language); the laptop's choice is restored in TearDown.
            oldLanguage = PlayerPrefs.HasKey(GameSettings.LanguageKey) ? PlayerPrefs.GetInt(GameSettings.LanguageKey) : (int?)null;
            PlayerPrefs.SetInt(GameSettings.LanguageKey, (int)Language.English);
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "Director must come from the gameplay scene.");
            // Until calls PollAudio, so audio is fetched before waiting on anything.
            audio = director.GetComponent<GameplayAudio>();
            Assert.IsNotNull(audio, "GameplayAudio must be attached to the director.");
            yield return Until(() => Read<bool>(director, "ready"), "menu finished Start");
            Assert.IsTrue(Button("Play vs Bots").interactable);
            Assert.IsNull(Engine, "Scene must open at the menu, with no match yet.");
            director.loop = false;
            director.recordTelemetry = false;
            director.tempo = 4;
            director.thinkSeconds = Vector2.zero;
            director.rules.ItemsPerRound = 0;
            director.rules.TurnTimeLimitSeconds = 0;
            director.rules.RandomDraw = false;
            input = director.GetComponent<VRTableInput>();
            Assert.IsNotNull(input);
            Assert.IsFalse(input.IsXRActive, "This suite runs in the Editor without an XR device.");
            // Device polling is replaced by test samples; the real input component/coroutines are still used.
            input.enabled = false;
            Invoke(input, "CreateVisuals");
            input.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            input.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Invoke(input, "OnInputFocusAcquired");
            yield return Until(() => input.HasFocus, "initial fixture focus");
            foreach (var bot in ActiveBots()) HeadBounds(bot);
            Time.timeScale = 20;
        }

        [UnityTearDown]
        public IEnumerator UnloadTable()
        {
            if (director != null) director.ReturnToMenu();
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-023 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = oldTimeScale;
            AudioListener.volume = oldVolume;
            Application.runInBackground = oldRunInBackground;
            if (oldLanguage.HasValue) PlayerPrefs.SetInt(GameSettings.LanguageKey, oldLanguage.Value);
            else PlayerPrefs.DeleteKey(GameSettings.LanguageKey);
            PlayerPrefs.Save();
            Localization.Language = Language.English;
            LogAssert.NoUnexpectedReceived();
        }

        // ------------------------------------------------------------------ scenarios

        [UnityTest]
        public IEnumerator Seated_framing_1_to_5_bots_clear_player_mat_and_waiter_announcement()
        {
            Time.timeScale = 1; // Vertex measurement must not skip animation peaks because of the fixture's sped-up time.
            Invoke(director, "PlaceDesktopCamera");
            var camera = Camera.main;
            float aspect = camera.aspect;
            camera.aspect = 16f / 9;
            try
            {
                yield return null;
                yield return Screenshot("BOB-050-menu-5-bots");
                AssertSeatFraming(camera);
                AssertNoCornerLamps(camera);
                float fov = camera.fieldOfView;
                try
                {
                    // The mask must actually draw the lamps: a wide FOV from the same seat contains both bulbs.
                    camera.fieldOfView = 100;
                    Assert.IsNotEmpty(LampPixels(camera), "Room_Glow mask draws nothing from the player seat (FOV 100).");
                    // Negative control: the old framing must actually be detected by the lamp mask.
                    camera.fieldOfView = 65;
                    var lamps = LampPixels(camera);
                    Assert.IsNotEmpty(CutLamps(lamps), "Negative control FOV 65 not detected: " + LampReport(lamps, CutLamps(lamps)));
                    Assert.Throws<AssertionException>(() => AssertNoCornerLamps(camera));
                }
                finally { camera.fieldOfView = fov; }
                var misplaced = ActiveBots()[0].transform;
                Vector3 position = misplaced.position;
                try
                {
                    misplaced.position = Read<Transform>(Read<object>(director, "human"), "mat").position - Vector3.up * .5f;
                    var error = Assert.Throws<AssertionException>(() => AssertSeatFraming(camera));
                    StringAssert.Contains("enters the player mat volume", error.Message);
                }
                finally { misplaced.position = position; }
                for (int count = 1; count <= 5; count++)
                {
                    yield return StartMatch(count);
                    Assert.AreEqual(count, ActiveBots().Length);
                    // The real announcement plays Wave and fills the waiter card; save evidence while it is still shown.
                    director.StartCoroutine((IEnumerator)Invoke(director, "AnnounceRound"));
                    yield return new WaitForSeconds(.3f);
                    Assert.IsNotEmpty(Read<TextMesh>(director, "speech").text);
                    yield return Screenshot("BOB-050-" + count + "-bots-announcement");
                    AssertSeatFraming(camera, count + " bots, announcement");
                    // All slots are tested together over the full range of motion; the 1-4 bot subsets use the same positions.
                    if (count == RoomSetup.SlotCount)
                    {
                        foreach (CharacterReaction reaction in Enum.GetValues(typeof(CharacterReaction)))
                        {
                            foreach (var bot in ActiveBots()) bot.Play(reaction, camera.transform);
                            for (int sample = 0; sample < 10; sample++)
                            {
                                yield return new WaitForSeconds(.2f);
                                AssertSeatFraming(camera, $"{count} bots, reaction {reaction} sample {sample + 1}");
                            }
                        }
                    }
                    director.ReturnToMenu();
                    yield return null;
                }
            }
            finally { camera.aspect = aspect; }
        }

        CharacterAnimator[] ActiveBots() => director.artRoot.Find("Seats").GetComponentsInChildren<CharacterAnimator>()
            .Where(c => c.name.StartsWith("Bot_")).ToArray();

        void AssertSeatFraming(Camera camera, string when = "menu")
        {
            var mat = Read<object>(director, "human");
            var playerMat = Read<Transform>(mat, "mat");
            // Work volume 42 x 38 cm, from the mat surface up to 35 cm above it.
            var workArea = new Bounds(playerMat.position + Vector3.up * .175f, new Vector3(.42f, .35f, .38f));
            var bots = ActiveBots();
            Assert.IsNotEmpty(bots);
            foreach (var bot in bots)
            {
                var renderers = bot.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !r.forceRenderingOff).ToArray();
                Assert.IsNotEmpty(renderers, bot.name + " has no active renderer.");
                foreach (var renderer in renderers)
                {
                    Bounds bounds = PosedBounds(renderer);
                    Assert.IsFalse(bounds.Intersects(workArea), bot.name + "/" + renderer.name + " enters the player mat volume.");
                    AssertInFrame(camera, renderer, bot.name + "/" + renderer.name + " (" + when + ")");
                }
            }
            var waiter = Read<CharacterAnimator>(director, "waiter");
            Assert.IsNotNull(waiter);
            var waiterRenderers = waiter.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !r.forceRenderingOff).ToArray();
            Assert.IsNotEmpty(waiterRenderers, "Waiter has no active renderer.");
            foreach (var renderer in waiterRenderers)
                AssertInFrame(camera, renderer, "Waiter/" + renderer.name + " (" + when + ")");

            var dispenser = director.artRoot.Find("Dispenser").GetComponentsInChildren<Renderer>()
                .Where(r => r.enabled && !r.forceRenderingOff).Select(r => (r.name, bounds: PosedBounds(r))).ToArray();
            Assert.IsNotEmpty(dispenser);
            // Bot faces and the waiter's face/chest: ray/bounds without the optional dispenser colliders.
            foreach (var character in bots.Append(waiter))
            {
                Assert.IsNotNull(character.Head);
                Vector3 head = character.Head.position + Vector3.up * .1f;
                foreach (Vector3 offset in new[] { Vector3.zero, Vector3.left * .06f, Vector3.right * .06f,
                    Vector3.up * .06f, Vector3.down * (character == waiter ? .28f : .06f) })
                {
                    Vector3 toFace = head + offset - camera.transform.position;
                    var ray = new Ray(camera.transform.position, toFace.normalized);
                    // The message includes the ray height relative to the box top so framing fixes can be measured from CI.md.
                    string blocked = null;
                    foreach (var (part, bounds) in dispenser)
                        if (bounds.IntersectRay(ray, out float distance) && distance < toFace.magnitude)
                            blocked = $"{part} top y={bounds.max.y:F3}, ray enters y={ray.GetPoint(distance).y:F3}, face y={(head + offset).y:F3}";
                    Assert.IsNull(blocked, character.name + " hidden by the dispenser from the seat camera (offset " + offset + ", " + when + "): " + blocked);
                }
            }
        }

        static Bounds PosedBounds(Renderer renderer)
        {
            // Skin culling bounds are deliberately loose for tracking (3 m cube); measure the drawn posed mesh.
            Bounds local = renderer.localBounds;
            if (renderer is SkinnedMeshRenderer skin)
            {
                var mesh = new Mesh();
                try { skin.BakeMesh(mesh); mesh.RecalculateBounds(); local = mesh.bounds; }
                finally { Object.Destroy(mesh); }
            }
            var result = new Bounds(renderer.transform.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(renderer.transform.TransformPoint(local.center +
                Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }

        static void AssertInFrame(Camera camera, Renderer renderer, string label)
        {
            Assert.AreNotEqual(0, camera.cullingMask & (1 << renderer.gameObject.layer), label + " not drawn by the camera.");
            Mesh mesh = null;
            try
            {
                Vector3[] vertices;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    // World AABB corners can leave the frame while a tilted mesh stays whole; project the real posed vertices.
                    mesh = new Mesh();
                    skin.BakeMesh(mesh);
                    vertices = mesh.vertices;
                }
                else
                {
                    var bounds = renderer.localBounds;
                    vertices = Enumerable.Range(0, 8).Select(i => bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))).ToArray();
                }
                Assert.IsNotEmpty(vertices, label + " has no vertices.");
                Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 screen = camera.WorldToViewportPoint(renderer.transform.TransformPoint(vertex));
                    min = Vector3.Min(min, screen);
                    max = Vector3.Max(max, screen);
                }
                Assert.Greater(min.z, camera.nearClipPlane, label + " passes behind the camera.");
                Assert.GreaterOrEqual(min.x, .01f, label + " clipped at frame left: " + min);
                Assert.LessOrEqual(max.x, .99f, label + " clipped at frame right: " + max);
                Assert.GreaterOrEqual(min.y, .01f, label + " clipped at frame bottom: " + min);
                Assert.LessOrEqual(max.y, .99f, label + " clipped at frame top: " + max);
            }
            finally { if (mesh != null) Object.Destroy(mesh); }
        }

        const int LampWidth = 320, LampHeight = 180;
        bool lampMaskFlipped;

        void AssertNoCornerLamps(Camera camera)
        {
            var lamps = LampPixels(camera);
            var cut = CutLamps(lamps);
            Assert.IsEmpty(cut, "White lamp bulbs still clipped at the frame's top edge/corners: " + LampReport(lamps, cut));
        }

        // Lamp fragments in the top corner zones, or touching the top/left/right edge of the frame's upper half (clipped bulb).
        static Vector2Int[] CutLamps(IEnumerable<Vector2Int> lamps) => lamps.Where(p =>
            p.y >= LampHeight - 2 || p.y >= LampHeight / 2 && (p.x < 2 || p.x >= LampWidth - 2) ||
            p.y >= LampHeight * 3 / 4 && (p.x < LampWidth / 25 || p.x >= LampWidth * 24 / 25)).ToArray();

        string LampReport(Vector2Int[] lamps, Vector2Int[] cut) => lamps.Length == 0 ? "0 lamp px in frame" :
            $"{cut.Length}/{lamps.Length} lamp px at the edge, box x={lamps.Min(p => p.x)}-{lamps.Max(p => p.x)} " +
            $"y={lamps.Min(p => p.y)}-{lamps.Max(p => p.y)} of {LampWidth}x{LampHeight} (y=0 bottom" +
            (lampMaskFlipped ? ", readback Y flipped by marker)" : ")");

        Vector2Int[] LampPixels(Camera camera)
        {
            // Room_Glow contains both lamp bulbs in one mesh. Render the source mask even when the furniture is already merged.
            var lamp = director.artRoot.Find("Room").GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Room_Glow");
            var mesh = lamp.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh);
            // RenderTexture readback orientation depends on the graphics API (D3D flips Y): calibrate with a cube above the frame center.
            Vector3 top = camera.ViewportToWorldPoint(new Vector3(.5f, .85f, camera.nearClipPlane + 1));
            var marker = MaskPixels(camera, Resources.GetBuiltinResource<Mesh>("Cube.fbx"),
                Matrix4x4.TRS(top, camera.transform.rotation, Vector3.one * .05f));
            Assert.IsNotEmpty(marker, "Lamp mask calibration marker not drawn.");
            lampMaskFlipped = marker.Average(p => p.y) < LampHeight / 2f;
            return MaskPixels(camera, mesh, lamp.transform.localToWorldMatrix)
                .Select(p => lampMaskFlipped ? new Vector2Int(p.x, LampHeight - 1 - p.y) : p).ToArray();
        }

        // Mask pixels (raw readback coordinates) of a mesh drawn white with the camera matrices on a black background.
        static Vector2Int[] MaskPixels(Camera camera, Mesh mesh, Matrix4x4 matrix)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", Color.white);
            var target = RenderTexture.GetTemporary(LampWidth, LampHeight, 24);
            var pixels = new Texture2D(LampWidth, LampHeight, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var commands = new CommandBuffer();
            try
            {
#if UNITY_EDITOR
                // Shader variants not yet compiled draw the cyan placeholder (r=0) and the mask ends up empty.
                UnityEditor.ShaderUtil.SetAsyncCompilation(commands, false);
#endif
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.black);
                commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(camera.projectionMatrix, true));
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    commands.DrawMesh(mesh, matrix, material, sub, 0);
#if UNITY_EDITOR
                UnityEditor.ShaderUtil.RestoreAsyncCompilation(commands);
#endif
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, LampWidth, LampHeight), 0, 0);
                pixels.Apply();
                var result = new List<Vector2Int>();
                for (int y = 0; y < LampHeight; y++)
                    for (int x = 0; x < LampWidth; x++)
                        if (pixels.GetPixel(x, y).r >= .1f) result.Add(new Vector2Int(x, y));
                return result.ToArray();
            }
            finally
            {
                RenderTexture.active = previous;
                commands.Release();
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixels);
                Object.Destroy(material);
            }
        }

        [UnityTest]
        public IEnumerator Menu_bot_setup_all_onboarding_pages_and_gameplay_with_screens()
        {
            Assert.IsTrue(Text("Menu Title").gameObject.activeSelf);
            Assert.AreEqual("<b>Play vs Bots</b>", Label(Button("Play vs Bots")), "Menu screenshot must be English.");
            yield return Screenshot("01-menu");
            Select(Button("Play vs Bots"));
            SetBotCount(5);
            Assert.AreEqual($"<b>{Strings.BotCount.Format(5)}</b>", Text("Bot Count").text);
            Assert.IsTrue(Button("Start").interactable);
            yield return Screenshot("02-bot-setup");

            // Onboarding uses the default rules (items + timer) so all pages are shown; the game itself still has no timer.
            var defaults = new RuleConfig();
            director.rules.ItemsPerRound = defaults.ItemsPerRound;
            director.rules.TurnTimeLimitSeconds = defaults.TurnTimeLimitSeconds;
            var pages = OnboardingGuide.Pages(director.rules, false);
            Assert.AreEqual(6, pages.Count, "How to play, grab, eat, Give, Items, Item list.");
            Select(Button("Start"));
            for (int page = 0; page < pages.Count; page++)
            {
                int expected = page;
                yield return Until(() => Read<int>(director, "onboardingPage") == expected, "onboarding page " + (page + 1));
                var text = Text("How to Play");
                Assert.IsTrue(text.gameObject.activeSelf);
                Assert.AreEqual($"<b>{pages[page].Title}</b>\n<size=36>{string.Join("\n", pages[page].Lines)}</size>", text.text);
                Assert.IsNull(Engine, "Game has not started during onboarding.");
                bool last = page == pages.Count - 1;
                Assert.AreEqual(last ? "<b>Start playing</b>" : "<b>Next</b>", Button("Next").GetComponent<TextMesh>().text);
                Assert.AreEqual(!last, Button("Skip").gameObject.activeSelf);
                if (page == 0) yield return Screenshot("03-onboarding");
                if (last)
                {
                    director.rules.ItemsPerRound = 0;
                    director.rules.TurnTimeLimitSeconds = 0;
                    director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
                }
                Select(Button("Next"));
            }
            yield return Until(() => Engine != null, "game after Start playing");
            Assert.IsFalse(Text("How to Play").gameObject.activeSelf);
            SetPool(true);
            yield return Until(() => HumanReady, "first human turn");
            Assert.AreSame(Human, Engine.Current);
            foreach (var type in new[] { ItemType.HotMilk, ItemType.PlasticFork, ItemType.CloverCoin })
                Engine.DebugGiveItem(Human, type);
            RefreshProps();
            yield return new WaitForSeconds(5); // d20/round popups are gone
            yield return Screenshot("04-gameplay");

            // Onboarding only once per session: back to menu, then Start begins the game directly.
            director.ReturnToMenu();
            yield return null;
            Select(Button("Play vs Bots"));
            director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
            Select(Button("Start"));
            yield return Until(() => Engine != null, "second game without onboarding");
            Assert.AreEqual(-1, Read<int>(director, "onboardingPage"));
            Assert.IsFalse(Text("How to Play").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator UI_labels_do_not_overlap_in_menu_gameplay_spectator_and_results()
        {
            Camera.main.aspect = 16f / 9;
            yield return Screenshot("BOB-049-menu");
            Assert.IsFalse(director.GetComponentsInChildren<TextMesh>().Any(t => t.text.Contains("Bite or Betray")),
                "The room sign already shows the game title.");
            var button = Button("Play vs Bots");
            // Negative control: reject two deliberately overlapping label bounds.
            Button("Settings").transform.position = button.transform.position;
            var overlap = Assert.Throws<AssertionException>(() => AssertLabelLayout(Camera.main, "overlap control"));
            StringAssert.Contains("overlaps", overlap.Message);
            yield return null; // Restore positions from runtime anchors.
            var head = HeadBounds(ActiveBots().Single(b => b.name == "Bot_Peach"));
            button.transform.localScale *= .1f;
            button.transform.position = head.center;
            var headOverlap = Assert.Throws<AssertionException>(() => AssertLabelLayout(Camera.main, "head control"));
            StringAssert.Contains("covers bot head", headOverlap.Message);
            yield return null;
            var dispenser = director.artRoot.Find("Dispenser").GetComponentsInChildren<Renderer>()
                .Single(r => r.name == "CandyGlobe");
            button.transform.localScale *= .1f;
            button.transform.position = PosedBounds(dispenser).center;
            var dispenserOverlap = Assert.Throws<AssertionException>(() => AssertLabelLayout(Camera.main, "dispenser control"));
            StringAssert.Contains("dispenser geometry", dispenserOverlap.Message);
            yield return null;
            var text = button.GetComponent<TextMesh>();
            Color normal = text.color;
            button.SetHover(true);
            yield return null;
            Assert.AreNotEqual(normal, text.color, "Hover must be visible without enlarging the button.");
            button.SetHover(false);
            button.interactable = false;
            yield return null;
            Assert.AreNotEqual(normal, text.color, "Disabled must have a distinct appearance.");
            var block = new MaterialPropertyBlock();
            text.transform.Find("Card").GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.AreEqual(3, block.GetFloat("_State"));
            button.interactable = true;

            yield return StartMatch(5, false);
            // Simultaneous effects near the dispenser must remain clear of it and each other.
            var origin = Candy.transform.position + Vector3.up * .09f;
            Invoke(director, "Popup", origin, "BAD", Color.red, 4f);
            Invoke(director, "Popup", origin, "KO", Color.red, 4f);
            yield return Screenshot("BOB-049-gameplay");
            Engine.DebugSetHp(Human, 1);
            director.thinkSeconds = new Vector2(4, 4);
            Select(Candy);
            yield return Until(() => director.SpectatorInputLocked, "KO for spectator label audit");
            yield return Screenshot("BOB-049-spectator");
            director.thinkSeconds = Vector2.zero;
            Invoke(director, "Popup", origin, "Last action", Color.red, 3600f);
            Assert.IsTrue(director.GetComponentsInChildren<TextMesh>().Any(t => t.name == "Popup" && t.text == "Last action"));
            yield return FinishMatch();
            AssertResults();
            AssertResultsPresentation();
            Invoke(director, "Popup", origin, "Late action", Color.red, 4f);
            AssertResultsPresentation();
            yield return Screenshot("BOB-049-results");
            director.ReturnToMenu();
            yield return null;
            Assert.IsTrue(Text("Table Info").gameObject.activeSelf, "The board must be restored after results.");
            yield return StartMatch(1);
            Assert.IsNotEmpty(Text("Table Info").text);
            Assert.IsTrue(Text("Table Info").gameObject.activeInHierarchy);
            Assert.IsFalse(Text("Final Results").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Language_english_by_default_switch_in_settings_applies_now_and_persists()
        {
            Assert.AreEqual(Language.English, director.Settings.Language);
            Assert.AreEqual(Language.English, Localization.Language);
            Assert.AreEqual("<b>Create Room</b>", Label(Button("Create Room")).Split('\n')[0]);
            StringAssert.Contains(Strings.MenuHintDesktop.English, Caption);

            Select(Button("Settings"));
            var language = Button("Language");
            Assert.IsTrue(language.interactable, "The language button is in Settings.");
            Assert.AreEqual("<b>Language: English</b>", Label(language));
            Assert.AreEqual("<b>Settings</b>", Text("Menu Title").text);
            Select(language);

            // Applies immediately without restart: Settings, main menu, Bot setup, narration and the human seat name.
            Assert.AreEqual(Language.Indonesian, director.Settings.Language);
            Assert.AreEqual(Language.Indonesian, Localization.Language);
            Assert.AreEqual("<b>Bahasa: Bahasa Indonesia</b>", Label(language));
            Assert.AreEqual("<b>Kembali</b>", Label(Button("Back")));
            StringAssert.StartsWith("<b>Tangan dominan: ", Label(Button("Dominant hand")));
            StringAssert.Contains(Strings.MenuHintDesktop.Indonesian, Caption);
            Assert.AreEqual((int)Language.Indonesian, PlayerPrefs.GetInt(GameSettings.LanguageKey, -1), "Language saved in PlayerPrefs.");
            Select(Button("Back"));
            Assert.AreEqual("<b>Main vs Bot</b>", Label(Button("Play vs Bots")));
            Select(Button("Play vs Bots"));
            StringAssert.Contains("Atur meja", Caption);
            Assert.AreEqual($"<b>{Room.BotCount} bot</b>", Text("Bot Count").text);
            Assert.AreEqual("<b>Mulai</b>", Label(Button("Start")));
            Assert.AreEqual("Kamu", director.HumanName);
            Select(Button("Back to menu"));

            // New session: the scene is reloaded (via an empty scene like TearDown), the language is read again from PlayerPrefs.
            SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-048 New session"));
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            audio = director.GetComponent<GameplayAudio>();
            yield return Until(() => Read<bool>(director, "ready"), "new session menu");
            Assert.AreEqual(Language.Indonesian, director.Settings.Language);
            Assert.AreEqual("<b>Main vs Bot</b>", Label(Button("Play vs Bots")));
            StringAssert.Contains(Strings.MenuHintDesktop.Indonesian, Caption);

            // Back to English via the same button, also immediate and persisted.
            Select(Button("Settings"));
            Select(Button("Language"));
            Assert.AreEqual(Language.English, Localization.Language);
            Assert.AreEqual("<b>Language: English</b>", Label(Button("Language")));
            Assert.AreEqual((int)Language.English, PlayerPrefs.GetInt(GameSettings.LanguageKey, -1));
            Select(Button("Back"));
            Assert.AreEqual("<b>Play vs Bots</b>", Label(Button("Play vs Bots")));
            Assert.AreEqual("You", director.HumanName);
        }

        [UnityTest]
        public IEnumerator Eight_items_follow_game_engine_rules()
        {
            yield return StartMatch(2);
            var victim = Engine.Players.First(p => p.IsBot);
            int max = Engine.Config.MaxHp;

            // Plastic Fork: the same active candy is revealed to all players.
            var candy = Engine.ActiveCandy;
            yield return UseItem(ItemType.PlasticFork);
            Assert.AreSame(candy, Engine.ActiveCandy);
            Assert.IsTrue(candy.Revealed);
            yield return Until(() => Text("Candy Label").text.Contains("GOOD"), "revealed candy label");

            // Magic Gloves: the old candy goes back into the dispenser, another candy comes out hidden. A candy already pierced by Fork
            // stays revealed in the dispenser (owner decision, RulesTests.GlovesAfterFork_ForkedCandyStaysRevealed).
            int pool = Engine.CandiesInDispenser;
            yield return UseItem(ItemType.MagicGloves);
            Assert.AreNotSame(candy, Engine.ActiveCandy);
            Assert.AreSame(candy, Engine.DebugPool.Last(), "RandomDraw=false: the old candy goes to the back.");
            Assert.IsTrue(candy.Revealed, "The Forked candy stays revealed after Gloves.");
            Assert.IsFalse(Engine.ActiveCandy.Revealed, "The new candy from Gloves is not revealed.");
            Assert.AreEqual(pool, Engine.CandiesInDispenser);

            // Hammer Crush: the active candy is crushed and revealed, good left decreases, a new candy comes out.
            candy = Engine.ActiveCandy;
            int good = Engine.GoodLeft;
            yield return UseItem(ItemType.HammerCrush);
            Assert.IsTrue(candy.Revealed);
            Assert.AreNotSame(candy, Engine.ActiveCandy);
            Assert.AreEqual(good - 1, Engine.GoodLeft);
            Assert.AreEqual(pool - 1, Engine.CandiesInDispenser);

            // Hot Milk: HP +1, not above max HP.
            Engine.DebugSetHp(Human, max - 2);
            yield return UseItem(ItemType.HotMilk);
            Assert.AreEqual(max - 1, Human.Hp);
            Engine.DebugSetHp(Human, max);
            yield return UseItem(ItemType.HotMilk);
            Assert.AreEqual(max, Human.Hp, "Hot Milk at max HP is wasted.");

            // Cupcake: the next bad candy's effect is blocked once, the turn still continues.
            yield return UseItem(ItemType.Cupcake);
            Assert.IsTrue(Human.HasCupcake);
            Engine.DebugSetPool(Candies(CandyColor.Blue, false, 1).Concat(Candies(CandyColor.Yellow, true, 63)));
            int blocks = Engine.Stats.CupcakeBlocks, eats = Engine.Stats.EatSelf;
            Select(Candy);
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "eat bad with cupcake");
            Assert.AreEqual(max, Human.Hp);
            Assert.IsFalse(Human.HasCupcake);
            Assert.AreEqual(blocks + 1, Engine.Stats.CupcakeBlocks);
            Assert.AreSame(Human, Engine.Current, "Cupcake: turn continues despite a bad candy.");
            yield return Until(() => HumanReady, "turn continues after cupcake");

            // Clover Coin: attaches to the active candy and is consumed when the candy is eaten.
            yield return UseItem(ItemType.CloverCoin);
            Assert.IsTrue(Engine.CloverOnActive);
            int triggers = Engine.Stats.CloverTriggers;
            eats = Engine.Stats.EatSelf;
            Select(Candy);
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "eat clover candy");
            Assert.AreEqual(triggers + 1, Engine.Stats.CloverTriggers);
            Assert.IsFalse(Engine.CloverOnActive);
            yield return Until(() => HumanReady, "turn continues after clover");

            // Little Pocket: 3 random options without Pocket; the pick is used immediately.
            Engine.DebugGiveItem(victim, ItemType.HotMilk);
            yield return UseItem(ItemType.LittlePocket);
            Assert.AreEqual(GamePhase.AwaitingPocketChoice, Engine.Phase);
            Assert.AreEqual(3, Engine.PocketOptions.Count);
            CollectionAssert.DoesNotContain(Engine.PocketOptions, ItemType.LittlePocket);
            yield return Until(() => Props.Any(p => Read<bool>(p, "pocket") && PropButton(p).interactable), "Pocket options");
            var options = Props.Where(p => Read<bool>(p, "pocket")).ToArray();
            Assert.AreEqual(3, options.Length);
            var option = options.FirstOrDefault(p => Read<ItemType>(p, "type") != ItemType.GrapplingHook) ?? options[0];
            var chosen = Read<ItemType>(option, "type");
            int chosenUsed = Engine.Stats.ItemsUsed[(int)chosen];
            Select(PropButton(option));
            if (chosen == ItemType.GrapplingHook) Select(FindProp(victim, ItemType.HotMilk));
            yield return Until(() => Engine.Phase == GamePhase.AwaitingAction && HumanReady, "Pocket pick used");
            Assert.AreEqual(chosenUsed + 1, Engine.Stats.ItemsUsed[(int)chosen], "Pocket pick " + chosen);
            Assert.IsEmpty(Engine.PocketOptions);

            // Grappling Hook: take 1 opponent item and use it immediately.
            if (!victim.Items.Contains(ItemType.HotMilk)) Engine.DebugGiveItem(victim, ItemType.HotMilk);
            Engine.DebugSetHp(Human, max - 1);
            Engine.DebugGiveItem(Human, ItemType.GrapplingHook);
            RefreshProps();
            int victimItems = victim.Items.Count, milk = Engine.Stats.ItemsUsed[(int)ItemType.HotMilk];
            int hooks = Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook];
            Select(FindProp(Human, ItemType.GrapplingHook));
            Select(FindProp(victim, ItemType.HotMilk));
            yield return Until(() => Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook] == hooks + 1, "human Grappling");
            yield return Until(() => HumanReady, "Grappling animation finished");
            Assert.AreEqual(victimItems - 1, victim.Items.Count);
            Assert.AreEqual(milk + 1, Engine.Stats.ItemsUsed[(int)ItemType.HotMilk], "Stolen item is used immediately.");
            Assert.AreEqual(max, Human.Hp);
            foreach (var type in ItemInfo.All) Assert.Greater(Engine.Stats.ItemsUsed[(int)type], 0, ItemInfo.Name(type));
            Assert.IsTrue(heard.Contains(GameplaySound.UseItem), "Item cue not played.");
        }

        [UnityTest]
        public IEnumerator Three_candy_colors_and_clover_damage_cap_follow_rules()
        {
            Assert.AreEqual(3, CandyRules.MaxDamagePerCandy);
            yield return StartMatch(1);
            var bot = Engine.Players.Single(p => p.IsBot);
            int max = Engine.Config.MaxHp;
            var cases = new[]
            {
                (CandyColor.Yellow, true, false, 3), (CandyColor.Blue, true, false, 3), (CandyColor.Red, true, false, 3),
                (CandyColor.Blue, true, true, 3),
                (CandyColor.Yellow, false, false, max), (CandyColor.Blue, false, false, max), (CandyColor.Red, false, false, max),
                (CandyColor.Yellow, false, true, max), (CandyColor.Blue, false, true, max), (CandyColor.Red, false, true, max)
            };
            for (int i = 0; i < cases.Length; i++)
            {
                var (color, good, clover, start) = cases[i];
                string label = $"{CandyRules.ColorName(color)} {(good ? "good" : "bad")}{(clover ? " + Clover" : "")}";
                yield return Until(() => HumanReady, "human turn: " + label);
                Engine.DebugSetHp(Human, start);
                Engine.DebugSetHp(bot, max);
                // Yellow bad filler: bots can only eat/feed -1, then the turn returns to the human.
                Engine.DebugSetPool(Candies(color, good, 1).Concat(Candies(CandyColor.Yellow, false, 16)));
                if (clover) yield return UseItem(ItemType.CloverCoin);
                int mult = clover ? 2 : 1;
                int expected = good ? Math.Min(max, start + CandyRules.GoodHeal(color) * mult)
                    : Math.Max(0, start - Math.Min(CandyRules.BadDamage(color) * mult, CandyRules.MaxDamagePerCandy));
                int eats = Engine.Stats.EatSelf, skipped = Engine.Stats.TurnsSkipped, logs = Engine.LogLines.Count;
                // Alternate the VR path (grab to mouth) and click/select candy.
                if (i % 2 == 0)
                {
                    Grab(Hand(0));
                    EatAtMouth();
                }
                else Select(Candy);
                yield return Until(() => Engine.Stats.EatSelf == eats + 1, "eat " + label);
                Assert.AreEqual(expected, Human.Hp, label);
                Assert.AreEqual(!good && CandyRules.BadParalyzes(color), Human.Paralyzed, "paralyzed " + label);
                Assert.AreEqual(good, Engine.Current == Human, "turn continues only after good: " + label);
                if (clover && !good && color == CandyColor.Blue)
                {
                    Assert.AreEqual(max - 3, Human.Hp, "Blue bad x2 = 4, capped at 3.");
                    Assert.IsTrue(Engine.LogLines.Skip(logs).Any(l => l.StartsWith($"Damage capped at {CandyRules.MaxDamagePerCandy} per candy")),
                        "Missing damage cap log: " + string.Join(" | ", Engine.LogLines.Skip(logs)));
                }
                if (Human.Paralyzed)
                {
                    yield return Until(() => Engine.Stats.TurnsSkipped > skipped, "paralyzed turn skipped: " + label);
                    Assert.IsFalse(Human.Paralyzed);
                }
            }
            Assert.IsTrue(heard.Contains(GameplaySound.EatGood) && heard.Contains(GameplaySound.EatBad) && heard.Contains(GameplaySound.Damage));
        }

        [UnityTest]
        public IEnumerator New_round_refills_dispenser_and_deals_items()
        {
            director.rules.ItemsPerRound = 2;
            director.rules.DealItemsOnFirstRound = false;
            yield return StartMatch(2);
            Assert.IsTrue(Engine.Players.All(p => p.Items.Count == 0), "Round 1 without items (DealItemsOnFirstRound=false).");
            int round = Engine.RoundNumber, rounds = Engine.Stats.Rounds.Count, eats = Engine.Stats.EatSelf;
            Engine.DebugSetPool(Candies(CandyColor.Yellow, true, 1));
            Assert.AreEqual(0, Engine.CandiesInDispenser);
            Select(Candy);
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "eat last candy of the round");
            var cfg = Engine.Config;
            int alive = Engine.AlivePlayers.Count();
            int total = Mathf.Clamp(alive * cfg.CandiesPerAlivePlayer, Math.Max(2, cfg.MinCandies), Math.Max(2, cfg.MaxCandies));
            var announced = Engine.Announcement;
            Assert.AreEqual(round + 1, Engine.RoundNumber);
            Assert.AreEqual(rounds + 1, Engine.Stats.Rounds.Count);
            Assert.AreEqual(total, announced.Total);
            Assert.AreEqual(total, announced.Yellow + announced.Blue + announced.Red);
            Assert.AreEqual(total, announced.Good + announced.Bad);
            Assert.Greater(announced.Good, 0);
            Assert.Greater(announced.Bad, 0);
            Assert.AreEqual(total - 1, Engine.CandiesInDispenser, "One candy of the new round comes out immediately.");
            int dealt = Math.Min(cfg.ItemsPerRound, cfg.MaxItems);
            Assert.IsTrue(Engine.AlivePlayers.All(p => p.Items.Count == dealt), "New round items dealt to all living players.");
            Assert.AreSame(Human, Engine.Current, "Turn still continues after good.");
            var speech = Read<TextMesh>(director, "speech");
            Assert.IsNotNull(speech, "Waiter must exist in the scene.");
            yield return Until(() => speech.text.Contains(Strings.WaiterRound.Format(round + 1)), "waiter announcement of the new round");
            StringAssert.Contains($"{announced.Good} good, {announced.Bad} bad", speech.text);
            yield return Until(() => HumanReady, "turn after round announcement");
            foreach (var player in Engine.Players)
                Assert.AreEqual(dealt, Props.Count(p => !Read<bool>(p, "pocket")
                    && Read<PlayerState>(Read<object>(p, "seat"), "player") == player), "items on the mat " + player.Name);
            Assert.IsTrue(heard.Contains(GameplaySound.Waiter), "Waiter cue not played.");
        }

        [UnityTest]
        public IEnumerator Ko_spectator_results_music_sfx_and_telemetry_csv_with_screens()
        {
            var music = Read<AudioSource>(audio, "musicSource");
            bool output = AudioOutputAvailable();
            yield return null; // probe is destroyed before counting AudioSources
            Assert.AreEqual(5, director.GetComponents<AudioSource>().Length, "4 SFX + 1 music, created once.");
            int sources = AudioSources();
            AssertMusic(music, output, "menu");

            // Settings volume applies to AudioListener immediately, then is restored (Settings are saved in PlayerPrefs).
            Select(Button("Settings"));
            bool down = Button("Volume -").interactable;
            float volume = AudioListener.volume;
            Select(Button(down ? "Volume -" : "Volume +"));
            Assert.AreNotEqual(volume, AudioListener.volume);
            Assert.AreEqual(director.Settings.Volume01, AudioListener.volume, .0001f);
            Select(Button(down ? "Volume +" : "Volume -"));
            Assert.AreEqual(volume, AudioListener.volume, .0001f);
            Select(Button("Back"));

            director.recordTelemetry = true;
            yield return StartMatch(2);
            AssertMusic(music, output, "game");

            // Grab + eat (VR), item, then Give to a bot.
            var hand = Hand(0);
            int eats = Engine.Stats.EatSelf;
            Grab(hand);
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "human eats");
            yield return Until(() => HumanReady, "continue after good");
            Engine.DebugSetHp(Human, Engine.Config.MaxHp - 1);
            yield return UseItem(ItemType.HotMilk);
            // After Give, bots only have yellow bad: at most -1 per bot before the human's turn again.
            Engine.DebugSetPool(Candies(CandyColor.Yellow, true, 1).Concat(Candies(CandyColor.Yellow, false, 24)));
            int gives = Engine.Stats.Gives;
            Grab(hand);
            Write(hand, "hovered", BotTargets.First(t => t.interactable));
            Write(input, "actedThisFrame", false);
            Invoke(input, "ReleaseCandy", hand);
            yield return Until(() => Engine.Stats.Gives == gives + 1, "human Give");
            yield return Until(() => HumanReady, "human turn after bots");

            // KO: the human becomes a spectator in their seat, bots continue until a winner.
            Engine.DebugSetHp(Human, 1);
            SetPool(false);
            Select(Candy);
            yield return Until(() => director.SpectatorInputLocked, "human KO");
            Assert.IsTrue(Human.Eliminated);
            var avatar = director.artRoot.Find("Seats/Seat_0").GetComponentInChildren<SeatedAvatar>(true);
            Assert.IsNotNull(avatar, "Human avatar must exist in the scene.");
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            yield return Until(() => renderers.All(r => r.sharedMaterials.All(m => m.name.EndsWith(" Spectator"))), "KO avatar transparent");
            Assert.IsFalse(Candy != null && Candy.interactable);
            Assert.IsFalse(BotTargets.Any(t => t.interactable));
            yield return Screenshot("05-spectator");
            int turn = Engine.TurnNumber;
            yield return FinishMatch();
            Assert.Greater(Engine.TurnNumber, turn, "Bots continue the match after the human is KO.");
            AssertResults();
            yield return Until(() => heard.Contains(GameplaySound.Win), "win cue");
            yield return Screenshot("06-results");

            // Telemetry CSV: same format as the debug table, one GAME_OVER at the end.
            var telemetry = Read<TelemetryRecorder>(director, "telemetry");
            Assert.IsNotNull(telemetry, "recordTelemetry=true must create a CSV.");
            var lines = File.ReadAllLines(telemetry.FilePath);
            Assert.AreEqual(TelemetryRecorder.Header, lines[0]);
            var rows = lines.Skip(1).Select(l => l.Split(',')).ToArray();
            Assert.IsTrue(rows.All(r => r.Length == 14), "Every row has 14 columns.");
            var human = rows.Where(r => r[7] == "0").ToArray();
            Assert.IsTrue(human.Any(r => r[8] == "EAT"), "Human eat row.");
            Assert.IsTrue(human.Any(r => r[8] == "ITEM" && r[9] == ItemInfo.Name(ItemType.HotMilk)), "Human item row.");
            Assert.IsTrue(human.Any(r => r[8] == "GIVE" && Engine.Players.Any(p => p.IsBot && p.Name == r[9])),"Human Give row.");
            Assert.IsTrue(rows.Any(r => r[7] == "1"), "Bot action row.");
            Assert.IsTrue(rows.All(r => r[8] == "GAME_OVER" || double.Parse(r[10], CultureInfo.InvariantCulture) >= 0));
            Assert.AreEqual(1, rows.Count(r => r[8] == "GAME_OVER"));
            Assert.AreEqual("GAME_OVER", rows.Last()[8]);
            StringAssert.Contains("winner=" + Engine.Winner.Name, rows.Last()[9]);
            Directory.CreateDirectory("Logs/ci");
            File.Copy(telemetry.FilePath, "Logs/ci/regression-telemetry.csv", true);

            foreach (GameplaySound sound in Enum.GetValues(typeof(GameplaySound)))
                Assert.IsTrue(heard.Contains(sound), "Cue never played: " + sound);
            Assert.AreEqual(sources, AudioSources(), "AudioSource count grew during the game.");
            AssertMusic(music, output, "results");
            var previous = Engine;
            Select(Button("Play again"));
            yield return Until(() => Engine != null && Engine != previous, "Play again");
            AssertMusic(music, output, "Play again");
            director.ReturnToMenu();
            yield return null;
            AssertMusic(music, output, "back to menu");
            Assert.AreEqual(sources, AudioSources(), "AudioSource count grew after Play again/menu.");
        }

        [UnityTest]
        public IEnumerator Five_restarts_keep_objects_materials_audio_sources_and_clips_stable()
        {
            yield return StartMatch(2);
            Census baseline = null;
            // Cycle 0 is the baseline; five restarts after it: four via Restart (Play again button/R), one via the menu.
            for (int cycle = 0; cycle <= 5; cycle++)
            {
                if (cycle == 3)
                {
                    director.ReturnToMenu();
                    yield return null;
                    yield return StartMatch(2);
                }
                else if (cycle > 0)
                {
                    var previous = Engine;
                    director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
                    director.Restart();
                    yield return Until(() => Engine != null && Engine != previous, "engine restart " + cycle);
                    SetPool(true);
                    yield return Until(() => HumanReady, "restart ready " + cycle);
                }
                // Activity that creates temporary objects (candy, popups, item animations) before counting.
                int eats = Engine.Stats.EatSelf;
                Grab(Hand(0));
                EatAtMouth();
                yield return Until(() => Engine.Stats.EatSelf == eats + 1 && HumanReady, "eat cycle " + cycle);
                yield return UseItem(ItemType.HotMilk);
                yield return new WaitForSeconds(5); // popups finished and Destroy processed
                var census = TakeCensus();
                TestContext.WriteLine($"Cycle {cycle}: {census}");
                if (baseline == null) baseline = census;
                else baseline.AssertSame(census, cycle == 3 ? "restart via menu" : "restart " + cycle);
            }
        }

        [UnityTest]
        public IEnumerator Focus_lost_and_regained_cycles_freeze_timer_and_restore_input()
        {
            director.rules.TurnTimeLimitSeconds = 60;
            yield return StartMatch(1);
            Time.timeScale = 1;
            int sources = AudioSources();
            var hand = Hand(0);
            // Update stops before device polling when focus is lost.
            Write(input, "active", true);
            var cycles = new (string lose, object lost, string regain, object regained)[]
            {
                ("OnApplicationFocus", false, "OnApplicationFocus", true),
                ("OnApplicationPause", true, "OnApplicationPause", false),
                ("OnInputFocusLost", null, "OnInputFocusAcquired", null)
            };
            for (int round = 1; round <= 2; round++)
                foreach (var cycle in cycles)
                {
                    string label = $"{cycle.lose} cycle {round}";
                    // Grab starts with a released-button sample: after focus returns, buttons must be released first.
                    Grab(hand);
                    FocusCallback(cycle.lose, cycle.lost);
                    Invoke(input, "Update");
                    Assert.IsFalse(input.HasFocus, label);
                    Assert.IsFalse(input.IsHoldingCandy, "Candy returned when focus is lost: " + label);
                    float timer = Read<float>(director, "turnTimer"), thinking = Read<float>(director, "decisionThink");
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.AreEqual(timer, Read<float>(director, "turnTimer"), .0001f, "timer frozen: " + label);
                    Assert.AreEqual(thinking, Read<float>(director, "decisionThink"), .0001f, "think time frozen: " + label);
                    Assert.IsTrue(Candy.interactable);
                    FocusCallback(cycle.regain, cycle.regained);
                    Assert.IsFalse(input.HasFocus, "The resume frame must not count the pause delta: " + label);
                    // A focused Update resets this flag before device polling; the fixture does not call that polling.
                    Write(input, "suspended", false);
                    yield return Until(() => input.HasFocus, "focus restored: " + label);
                    yield return new WaitForSecondsRealtime(.1f);
                    float now = Read<float>(director, "turnTimer");
                    Assert.Less(now, timer, "timer running again: " + label);
                    Assert.Greater(now, timer - 2, label);
                    Assert.AreEqual(sources, AudioSources(), label);
                }
            int eats = Engine.Stats.EatSelf;
            Grab(hand);
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "input recovers after six focus cycles");
        }

        [UnityTest]
        public IEnumerator Controller_path_works_and_pinch_never_doubles_input()
        {
            yield return StartMatch(1);
            var controller = Hand(1);
            var pinch = Hand(0);
            // Pinch source as in UpdateInputSource when the Meta XR hand is tracked and the controller is not.
            var pinchPose = new GameObject("BOB-023 Pinch").transform;
            pinchPose.SetParent(director.transform);
            Write(pinch, "pose", pinchPose);
            Write(pinch, "usesHandTracking", true);
            Write(pinch, "handTracked", true);
            Write(pinch, "pointerValid", true);
            SetPinch(pinch, true);
            var buttons = new object[] { pinch, false, false, false };
            Invoke(input, "ReadButtons", buttons);
            Assert.IsTrue((bool)buttons[1], "Index pinch = trigger.");
            Assert.IsFalse((bool)buttons[2] || (bool)buttons[3], "Pinch is never read as grip or B/Y.");
            SetPinch(pinch, false);
            Invoke(input, "ReadButtons", buttons);
            Assert.IsFalse((bool)buttons[1]);

            // Controller and pinch select the same item in the same frame: only one action, in either order.
            foreach (bool pinchFirst in new[] { false, true })
            {
                var type = pinchFirst ? ItemType.Cupcake : ItemType.HotMilk;
                Engine.DebugGiveItem(Human, type);
                RefreshProps();
                var prop = FindProp(Human, type);
                int selects = 0, used = Engine.Stats.ItemsUsed[(int)type];
                var select = prop.onSelect;
                prop.onSelect = () =>
                {
                    selects++;
                    select();
                };
                Aim(controller, prop);
                Aim(pinch, prop);
                Sample(controller, true, false, false);
                Sample(pinch, true, false, false);
                Write(input, "actedThisFrame", false);
                foreach (var hand in pinchFirst ? new[] { pinch, controller } : new[] { controller, pinch })
                    Invoke(input, "ProcessHandInput", hand, true, true, false, false);
                Assert.AreEqual(1, selects, "Two input sources in the same frame may produce only one action.");
                yield return Until(() => Engine.Stats.ItemsUsed[(int)type] == used + 1 && HumanReady, "item " + type);
                Assert.AreEqual(used + 1, Engine.Stats.ItemsUsed[(int)type]);
                Sample(controller, true, false, false);
                Sample(pinch, true, false, false);
            }

            // The controller path stays intact: grab, then eat at the mouth.
            int eats = Engine.Stats.EatSelf;
            Grab(controller);
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1 && HumanReady, "eat via controller");

            // Pinch holds the candy; a controller trigger in another frame does not double grab/eat.
            eats = Engine.Stats.EatSelf;
            Aim(pinch, Candy);
            Sample(pinch, true, false, false);
            Sample(pinch, true, true, false);
            Assert.IsTrue(input.IsHoldingCandy);
            Assert.AreSame(pinch, Read<object>(input, "holder"));
            Aim(controller, Candy);
            Sample(controller, true, false, false);
            Sample(controller, true, true, false);
            Assert.AreSame(pinch, Read<object>(input, "holder"), "Controller does not steal the candy held by pinch.");
            Assert.AreEqual(eats, Engine.Stats.EatSelf);
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1 && HumanReady, "eat via pinch");
            yield return new WaitForSeconds(1);
            Assert.AreEqual(eats + 1, Engine.Stats.EatSelf, "One pinch = one eat.");
            Assert.AreEqual(0, Engine.Stats.Gives);
        }

        // ------------------------------------------------------------------ match flow

        IEnumerator StartMatch(int bots, bool good = true)
        {
            Select(Button("Play vs Bots"));
            SetBotCount(bots);
            director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
            bool onboardingDone = Read<bool>(director, "onboardingDone");
            Select(Button("Start"));
            if (!onboardingDone)
            {
                yield return Until(() => Button("Skip").interactable, "onboarding before the match");
                Select(Button("Skip"));
            }
            yield return Until(() => Engine != null, "game after Start");
            SetPool(good);
            yield return Until(() => HumanReady, "first human turn");
            Assert.AreSame(Human, Engine.Current);
        }

        int HumanFirstSeed()
        {
            var seats = Room.BuildSeats(director.HumanName, director.botNames);
            for (int seed = 100; seed <= 1100; seed++)
            {
                var probe = new GameEngine(director.rules.Clone(), seed) { KeepLog = false };
                probe.Start(seats);
                if (!probe.Current.IsBot) return seed;
            }
            Assert.Fail("No seed found where the human goes first.");
            return 0;
        }

        static IEnumerable<Candy> Candies(CandyColor color, bool good, int count) =>
            Enumerable.Range(0, count).Select(_ => new Candy(color, good));

        void SetPool(bool good) => Engine.DebugSetPool(Candies(good ? CandyColor.Yellow : CandyColor.Blue, good, 64));

        void SetBotCount(int count)
        {
            Assert.That(count, Is.InRange(1, Room.MaxBots));
            while (Room.BotCount > count) Select(Button("Fewer bots"));
            while (Room.BotCount < count) Select(Button("More bots"));
        }

        IEnumerator UseItem(ItemType type)
        {
            int used = Engine.Stats.ItemsUsed[(int)type];
            Engine.DebugGiveItem(Human, type);
            RefreshProps();
            Select(FindProp(Human, type));
            yield return Until(() => Engine.Stats.ItemsUsed[(int)type] == used + 1, "use " + type);
            if (type != ItemType.LittlePocket) yield return Until(() => HumanReady, "animation " + type);
        }

        IEnumerator FinishMatch()
        {
            float deadline = Time.realtimeSinceStartup + 90;
            while (!Text("Final Results").gameObject.activeSelf)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Match stuck: " + Engine.Phase + ", turn " + Engine.TurnNumber);
                if (HumanReady) Select(Candy);
                PollAudio();
                yield return null;
            }
        }

        void AssertResults()
        {
            Assert.IsTrue(Engine.IsOver);
            Assert.IsNotNull(Engine.Winner);
            Assert.AreEqual(1, Engine.AlivePlayers.Count());
            var result = Text("Final Results");
            Assert.IsTrue(result.gameObject.activeSelf);
            StringAssert.Contains(Strings.Winner.Text, result.text);
            foreach (var (place, player) in FinalStandings.Rank(Engine.Players.OrderBy(p => p.Seat), p => p.IsAlive ? int.MaxValue : p.EliminatedOnTurn))
                StringAssert.Contains(place + ". " + player.Name, result.text);
            Assert.IsTrue(Button("Play again").interactable);
            Assert.IsTrue(Button("Back to menu").interactable);
        }

        void RefreshProps() => Invoke(director, "RefreshProps");

        TableSelectable FindProp(PlayerState owner, ItemType type) => PropButton(Props.Single(p =>
            !Read<bool>(p, "pocket") && Read<ItemType>(p, "type") == type
            && Read<PlayerState>(Read<object>(p, "seat"), "player") == owner));

        static TableSelectable PropButton(object prop) => Read<TableSelectable>(prop, "selectable");

        // ------------------------------------------------------------------ VR input without headset

        object Hand(int index) => Read<Array>(input, "hands").GetValue(index);

        /// <summary>Aim the hand ray from above the target: controller via pose, pinch via the Meta XR aim ray.</summary>
        void Aim(object hand, Component target)
        {
            var colliders = target.GetComponentsInChildren<Collider>();
            Assert.IsNotEmpty(colliders, target.name + " has no collider");
            var bounds = colliders[0].bounds;
            foreach (var c in colliders) bounds.Encapsulate(c.bounds);
            var origin = bounds.center + Vector3.up * (bounds.extents.y + .1f);
            var pose = Read<Transform>(hand, "pose");
            if (pose == null)
            {
                pose = new GameObject("BOB-023 Hand").transform;
                pose.SetParent(director.transform);
                Write(hand, "pose", pose);
            }
            pose.SetPositionAndRotation(origin, Quaternion.LookRotation(Vector3.down));
            if (Read<bool>(hand, "usesHandTracking")) Write(hand, "handRay", new Ray(origin, Vector3.down));
            Physics.SyncTransforms();
        }

        void Grab(object hand)
        {
            Aim(hand, Candy);
            var pose = Read<Transform>(hand, "pose");
            Assert.AreSame(Candy, TableSelectable.Raycast(new Ray(pose.position, pose.forward), input.rayLength, out _), "Hand ray must hit the candy.");
            Sample(hand, true, false, false);
            Sample(hand, true, true, false);
            Assert.IsTrue(input.IsHoldingCandy);
        }

        void EatAtMouth()
        {
            var head = Camera.main.transform;
            Candy.transform.position = head.position - head.up * .08f + head.forward * .05f;
            var holder = Read<object>(input, "holder");
            var pose = Read<Transform>(holder, "pose");
            pose.position = Candy.transform.position - pose.forward * .06f;
            Write(input, "actedThisFrame", false);
            Invoke(input, "ProcessHeldCandy", true);
        }

        void Sample(object hand, bool tracked, bool trigger, bool grip)
        {
            Write(input, "actedThisFrame", false);
            Physics.SyncTransforms();
            Invoke(input, "ProcessHandInput", hand, tracked, trigger, grip, false);
            PollAudio();
        }

        static void SetPinch(object hand, bool pinched)
        {
            var state = Read<OVRPlugin.HandState>(hand, "handState");
            state.Pinches = pinched ? OVRPlugin.HandFingerPinch.Index : 0;
            Write(hand, "handState", state);
        }

        void FocusCallback(string method, object argument)
        {
            if (argument == null) Invoke(input, method);
            else input.SendMessage(method, argument, SendMessageOptions.RequireReceiver);
        }

        // ------------------------------------------------------------------ audio

        void PollAudio()
        {
            var clips = Read<AudioClip[]>(audio, "clips");
            if (clips == null) return;
            foreach (var source in Read<AudioSource[]>(audio, "sources"))
            {
                int index = source != null && source.clip != null ? Array.IndexOf(clips, source.clip) : -1;
                if (index >= 0) heard.Add((GameplaySound)index);
            }
        }

        static int AudioSources() => Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        /// <summary>A batch Editor without an audio device never reports isPlaying; the probe tells that apart from a music bug.</summary>
        bool AudioOutputAvailable()
        {
            var probe = new GameObject("BOB-023 Audio Probe").AddComponent<AudioSource>();
            probe.clip = audio.backgroundMusic;
            probe.volume = 0;
            probe.Play();
            bool playing = probe.isPlaying;
            Object.Destroy(probe.gameObject);
            TestContext.WriteLine("Editor audio output: " + (playing ? "present" : "none, music isPlaying not checked"));
            return playing;
        }

        void AssertMusic(AudioSource music, bool output, string where)
        {
            Assert.IsNotNull(music);
            Assert.IsTrue(music.loop, "Background music must loop.");
            Assert.AreSame(audio.backgroundMusic, music.clip, $"Background music not started at {where} (audio active="
                + $"{audio.isActiveAndEnabled}, requested={Read<bool>(audio, "musicRequested")}, isPlaying={music.isPlaying})");
            if (output) Assert.IsTrue(music.isPlaying, "Background music stopped at " + where);
            Assert.AreEqual(director.Settings.Volume01, AudioListener.volume, .0001f, "Settings volume at " + where);
        }

        // ------------------------------------------------------------------ object counts

        sealed class Census
        {
            public string[] objects, materials, sources, clips;
            public int selectables;

            public void AssertSame(Census other, string label)
            {
                Check("GameObject", objects, other.objects, label);
                Check("Material", materials, other.materials, label);
                Check("AudioSource", sources, other.sources, label);
                Check("AudioClip", clips, other.clips, label);
                Assert.AreEqual(selectables, other.selectables, "TableSelectable count grew after " + label);
            }

            // The failure message names what grew/shrank so the leak can be found right away.
            static void Check(string kind, string[] before, string[] after, string label)
            {
                if (before.Length == after.Length) return;
                var counts = before.GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
                var diff = after.GroupBy(n => n)
                    .Select(g => (name: g.Key, delta: g.Count() - (counts.TryGetValue(g.Key, out int c) ? c : 0)))
                    .Concat(counts.Where(p => !after.Contains(p.Key)).Select(p => (name: p.Key, delta: -p.Value)))
                    .Where(d => d.delta != 0).Take(12).Select(d => $"{d.name} {d.delta:+0;-0}");
                Assert.Fail($"{kind} {before.Length} -> {after.Length} after {label}: {string.Join(", ", diff)}");
            }

            public override string ToString() =>
                $"{objects.Length} GameObject, {materials.Length} Material, {sources.Length} AudioSource, {clips.Length} AudioClip, {selectables} TableSelectable";
        }

        Census TakeCensus() => new Census
        {
            objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(o => o.name).ToArray(),
            materials = Resources.FindObjectsOfTypeAll<Material>().Select(m => m.name).ToArray(),
            sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(s => s.name).ToArray(),
            clips = Resources.FindObjectsOfTypeAll<AudioClip>().Select(c => c.name).ToArray(),
            selectables = director.GetComponentsInChildren<TableSelectable>(true).Length
        };

        // ------------------------------------------------------------------ screenshot

        /// <summary>Render the player seat camera to a RenderTexture (not ScreenCapture), then save a 1280x720 PNG.</summary>
        IEnumerator Screenshot(string name)
        {
            // At the menu the desktop camera has not been moved to the seat yet; PlayGame calls the same when the game starts.
            if (Engine == null) Invoke(director, "PlaceDesktopCamera");
            var camera = Camera.main;
            Assert.IsNotNull(camera, "Player seat camera (MainCamera) missing.");
            const int width = 1280, height = 720;
            float oldAspect = camera.aspect;
            camera.aspect = width / (float)height;
            yield return null; // LateUpdate lays out labels for the actual screenshot aspect ratio.
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
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
            }
            var pixels = texture.GetPixels32();
            int colors = pixels.Where((_, i) => i % 97 == 0).Select(p => (p.r, p.g, p.b)).Distinct().Count();
            Directory.CreateDirectory(Screens);
            File.WriteAllBytes(Path.Combine(Screens, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
            try
            {
                Assert.Greater(colors, 32, $"Screenshot {name} empty/uniform ({colors} colors).");
                AssertLabelLayout(camera, name);
            }
            finally { camera.aspect = oldAspect; }
        }

        // Project renderer corners independently, including the entire card background.
        // Do not use runtime layout helpers: catch bad anchors, oversized cards and simultaneous effects.
        void AssertLabelLayout(Camera camera, string scenario)
        {
            var seat = director.artRoot.Find("Seats/Seat_0/HeadAnchor");
            Assert.IsNotNull(seat);
            Assert.Less(Vector3.Distance(seat.position, camera.transform.position), .15f, scenario + ": camera is not at the player seat.");
            var labels = director.GetComponentsInChildren<TextMesh>()
                .Where(t => t.gameObject.activeInHierarchy && !string.IsNullOrEmpty(t.text)).ToArray();
            var visible = new List<(TextMesh text, Rect rect)>();
            foreach (var text in labels)
            {
                if (camera.WorldToViewportPoint(text.transform.position).z <= camera.nearClipPlane) continue;
                var renderers = text.GetComponentsInChildren<MeshRenderer>();
                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var renderer in renderers)
                {
                    Assert.IsTrue(renderer.sharedMaterial.shader.isSupported, renderer.name + " shader is unsupported.");
                    if (renderer.name == "Card")
                    {
                        Assert.AreEqual("BiteOrBetray/TextCard", renderer.sharedMaterial.shader.name);
                        Assert.AreEqual(1, renderer.sharedMaterial.passCount, "The frame must not add a pass/draw.");
                    }
                    var bounds = renderer.localBounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 local = bounds.center + Vector3.Scale(bounds.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector2 point = camera.WorldToViewportPoint(renderer.transform.TransformPoint(local));
                        min = Vector2.Min(min, point);
                        max = Vector2.Max(max, point);
                    }
                }
                var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                if (!rect.Overlaps(new Rect(0, 0, 1, 1))) continue;
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0), scenario + ": " + text.name + " clipped left");
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(1), scenario + ": " + text.name + " clipped right");
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0), scenario + ": " + text.name + " clipped below");
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(1), scenario + ": " + text.name + " clipped above");
                visible.Add((text, rect));
            }
            Assert.Greater(visible.Count, 2, scenario + ": the audit must check multiple labels from the player seat.");
            string report = scenario + ": " + visible.Count + " label";
#if UNITY_EDITOR
            int draws = UnityEditor.UnityStats.drawCalls;
            report += ", draw=" + draws;
            Assert.That(draws, Is.InRange(1, 100), scenario + ": camera draw count is outside the budget of 100.");
#endif
            Directory.CreateDirectory("Logs/ci");
            File.AppendAllText("Logs/ci/ui-layout.txt", report + "\n");
            TestContext.WriteLine(report);
            foreach (var bot in ActiveBots())
            {
                var head = ProjectBounds(camera, HeadBounds(bot));
                foreach (var label in visible)
                    Assert.IsFalse(label.rect.Overlaps(head),
                        $"{scenario}: {label.text.name} {label.rect} covers bot head {bot.name} {head}");
            }
            var dispenser = director.artRoot.Find("Dispenser").GetComponentsInChildren<Renderer>()
                .Where(r => r.enabled && !r.forceRenderingOff).ToArray();
            Assert.IsNotEmpty(dispenser, "The dispenser audit requires rendered geometry.");
            foreach (var part in dispenser)
            {
                var bounds = PosedBounds(part);
                var rect = ProjectBounds(camera, bounds);
                foreach (var label in visible)
                    Assert.IsFalse(label.rect.Overlaps(rect),
                        $"{scenario}: {label.text.name} {label.rect} overlaps dispenser geometry {part.name} {rect}");
            }
            for (int i = 0; i < visible.Count; i++)
                for (int j = i + 1; j < visible.Count; j++)
                    Assert.IsFalse(visible[i].rect.Overlaps(visible[j].rect),
                        $"{scenario}: {visible[i].text.name} {visible[i].rect} overlaps {visible[j].text.name} {visible[j].rect}");
        }

        void AssertResultsPresentation()
        {
            Assert.IsFalse(Text("Table Info").gameObject.activeInHierarchy, "Hide the round board during results.");
            Assert.IsEmpty(Read<TextMesh>(director, "speech").text, "Retire the waiter announcement during results.");
            string headline = Text("Final Results").text.Split('\n')[0];
            Assert.IsFalse(Caption.Contains(headline), "Only the standings card should announce the winner.");
            Assert.IsFalse(director.GetComponentsInChildren<TextMesh>().Any(t => t.name == "Popup" && t.gameObject.activeInHierarchy),
                "Action and WIN popups must be hidden on the first results frame and remain suppressed.");
        }

        // Independently sample head geometry in the authored rest pose, then follow its bone through reactions.
        // Idle sway, glances and raised hands would otherwise slice a random neck/shoulder band into the head bounds.
        Bounds HeadBounds(CharacterAnimator bot)
        {
            var neck = bot.GetComponentsInChildren<Transform>().First(t => t.name == "Head" || t.name.StartsWith("Head."));
            if (headBounds.TryGetValue(neck, out var cached)) return TransformBounds(neck, cached);
            var result = new Bounds();
            bool found = false;
            bool animating = bot.enabled;
            bot.enabled = false; // OnDisable restores the rest pose; re-enabled before the next LateUpdate.
            try
            {
                foreach (var renderer in bot.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !r.forceRenderingOff))
                {
                    Mesh baked = null;
                    try
                    {
                        Vector3[] vertices;
                        if (renderer is SkinnedMeshRenderer skin)
                        {
                            baked = new Mesh();
                            skin.BakeMesh(baked);
                            vertices = baked.vertices;
                        }
                        else
                        {
                            var bounds = renderer.localBounds;
                            vertices = Enumerable.Range(0, 8).Select(i => bounds.center + Vector3.Scale(bounds.extents,
                                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))).ToArray();
                        }
                        foreach (var vertex in vertices)
                        {
                            var world = renderer.transform.TransformPoint(vertex);
                            if (world.y < neck.position.y - .02f) continue;
                            var point = neck.InverseTransformPoint(world);
                            if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                            else result.Encapsulate(point);
                        }
                    }
                    finally { if (baked != null) Object.Destroy(baked); }
                }
            }
            finally { bot.enabled = animating; }
            Assert.IsTrue(found, bot.name + ": no rendered head vertices found.");
            headBounds.Add(neck, result);
            return TransformBounds(neck, result);
        }

        static Bounds TransformBounds(Transform transform, Bounds local)
        {
            var result = new Bounds(transform.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }

        static Rect ProjectBounds(Camera camera, Bounds bounds)
        {
            var points = Enumerable.Range(0, 8).Select(i => camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))))
                .Where(p => p.z > camera.nearClipPlane).ToArray();
            Assert.IsNotEmpty(points, "Geometry must be in front of the seat camera.");
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }

        // ------------------------------------------------------------------ util

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Button " + label);
        TextMesh Text(string name) => director.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == name);
        static string Label(TableSelectable button) => button.GetComponent<TextMesh>().text;
        string Caption => Read<TextMesh>(director, "caption").text;

        void Select(TableSelectable target)
        {
            Assert.IsNotNull(target);
            Assert.IsTrue(target.gameObject.activeInHierarchy && target.isActiveAndEnabled && target.interactable, target.name + " not interactable");
            target.Select();
            PollAudio();
        }

        IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, message);
                PollAudio();
                yield return null;
            }
            PollAudio();
        }

        static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
        static void Write(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    }
}
