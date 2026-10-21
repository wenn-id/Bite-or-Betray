#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// BOB-052: 1080p30 Devpost trailer clips with English UI, recorded by GameplayRecorder (BOB-045) in the real scene.
    /// The Trailer category is not part of the regular PlayMode run: the laptop runner runs it separately (-testCategory Trailer) only when
    /// Docs/Release/record-trailer.txt = yes, with output folder -bobTrailerDir ~/bob-ci/videos/&lt;commit&gt;. Without that argument
    /// (run manually from the Test Runner) clips are written to temporaryCachePath and then deleted.
    /// Seat camera clips (eyes at the player seat): menu, Bot setup, gameplay with items, spectator, results, online lobby
    /// with room code. The Close-up Bot and Top-down Table presets use a bots-only match, like the Record Video menu.
    /// Each clip appends one line to clips.txt (name, frames, scene); the runner joins the frames into an MP4 with ffmpeg.
    /// Player actions go through TableSelectable like RegressionTests, so VR hands/pointers are not shown. Waits use
    /// game time: while recording, Time.captureFramerate 30 makes one frame = 1/30 second of video.
    /// </summary>
    // Category on each test too: the -testCategory "!Trailer" filter must exclude the test cases, not only the fixture.
    [Category("Trailer")]
    public class TrailerClipTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Type Recorder = Type.GetType("BiteOrBetray.EditorTools.GameplayRecorder, BiteOrBetray.Editor", true);
        CandyTableDirector director;
        VRTableInput input;
        Scene scene;
        string root, preset;
        bool keep;
        float oldTimeScale;
        bool oldRunInBackground;
        int? oldLanguage;

        static bool Recording => (bool)Recorder.GetProperty("Recording").GetValue(null);
        static int Frames => (int)Recorder.GetProperty("Frames").GetValue(null);
        GameEngine Engine => Read<GameEngine>(director, "engine");
        TableSelectable Candy => Read<TableSelectable>(director, "candySelectable");
        bool HumanReady => Candy != null && Candy.interactable && Read<BotDecision>(director, "humanChoice") == null;
        PlayerState Human => Engine.Players.Single(p => !p.IsBot);
        RoomSetup Room => Read<RoomSetup>(director, "room");
        IEnumerable<object> Props => Read<IList>(director, "props").Cast<object>();
        Vector3 Seat => (director.artRoot.Find("Seats/Seat_0/HeadAnchor") ?? director.artRoot.Find("Seats/Seat_0")).position;

        [SetUp]
        public void Remember()
        {
            // NUnit uses one fixture instance for all tests: the preset from Clip_preset_* must not carry over to the seat clips.
            preset = null;
            oldTimeScale = Time.timeScale;
            oldRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-bobTrailerDir");
            keep = i >= 0 && i + 1 < args.Length;
            root = keep ? args[i + 1] : Path.Combine(Application.temporaryCachePath, "BoBTrailerTest");
            Directory.CreateDirectory(root);
            // Trailer is always English (the default language); the laptop's choice is restored in TearDown.
            oldLanguage = PlayerPrefs.HasKey(GameSettings.LanguageKey) ? PlayerPrefs.GetInt(GameSettings.LanguageKey) : (int?)null;
            PlayerPrefs.SetInt(GameSettings.LanguageKey, (int)Language.English);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= Configure;
            Call("Stop");
            var lobby = director != null ? director.Lobby as MonoBehaviour : null;
            if (lobby != null && Read<object>(lobby, "session") != null)
            {
                Invoke(lobby, "Leave", null, null);
                var manager = NetworkManager.Singleton;
                float deadline = Time.realtimeSinceStartup + 10;
                while (manager != null && (manager.IsListening || manager.ShutdownInProgress) && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSecondsRealtime(2); // session deletion runs async
            }
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-052 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = oldTimeScale;
            Application.runInBackground = oldRunInBackground;
            if (oldLanguage.HasValue) PlayerPrefs.SetInt(GameSettings.LanguageKey, oldLanguage.Value);
            else PlayerPrefs.DeleteKey(GameSettings.LanguageKey);
            PlayerPrefs.Save();
            Localization.Language = Language.English;
            if (!keep && Directory.Exists(root)) Directory.Delete(root, true);
        }

        // ------------------------------------------------------------------ player seat clips

        [UnityTest, Category("Trailer"), Timeout(1800000)]
        public IEnumerator Clip_menu_and_bot_setup_from_player_seat()
        {
            yield return Load();
            Begin("01-menu");
            yield return Wait(7);
            End("01-menu", "Main menu from the seat: Play vs Bots, Create Room, Join Room, Settings");

            Begin("02-bot-setup");
            yield return Wait(1.5f);
            Select(Button("Play vs Bots"));
            yield return Wait(2);
            while (Room.BotCount < Room.MaxBots)
            {
                Select(Button("More bots"));
                yield return Wait(.8f);
            }
            yield return Wait(1);
            // One bot's level is cycled Easy -> Normal -> Hard above its head.
            var level = Read<IDictionary>(director, "difficultyButtons").Values.Cast<TableSelectable>()
                .First(b => b.gameObject.activeInHierarchy && b.interactable);
            for (int i = 0; i < 3; i++)
            {
                Select(level);
                yield return Wait(1.2f);
            }
            while (Room.BotCount > 3)
            {
                Select(Button("Fewer bots"));
                yield return Wait(.8f);
            }
            yield return Wait(1.5f);
            director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
            Select(Button("Start"));
            if (!Read<bool>(director, "onboardingDone"))
            {
                yield return Until(() => Button("Skip").interactable, "onboarding before the match");
                yield return Wait(3);
                Select(Button("Skip"));
            }
            yield return Until(() => Engine != null, "game after Start");
            yield return Wait(4); // d20 roll and waiter round announcement
            End("02-bot-setup", "Table setup: bot count 1-5, per-bot level Easy/Normal/Hard, Start, d20 turn order");
        }

        [UnityTest, Category("Trailer"), Timeout(1800000)]
        public IEnumerator Clip_gameplay_with_items_from_player_seat()
        {
            yield return Load();
            yield return StartMatch(3);
            int max = Engine.Config.MaxHp;
            var victim = Engine.Players.First(p => p.IsBot && p.IsAlive);
            Engine.DebugSetHp(Human, max - 1);
            Engine.DebugGiveItem(victim, ItemType.HotMilk);
            foreach (var type in new[] { ItemType.PlasticFork, ItemType.HammerCrush, ItemType.GrapplingHook, ItemType.LittlePocket })
                Engine.DebugGiveItem(Human, type);
            // Fixed candy order (RandomDraw=false): Fork reveals good, which is then eaten, Hammer crushes bad, good,
            // then the bad that ends the turn; the rest is mixed for the bots.
            Engine.DebugSetPool(Candies(CandyColor.Yellow, true, 1).Concat(Candies(CandyColor.Blue, false, 1))
                .Concat(Candies(CandyColor.Red, true, 1)).Concat(Candies(CandyColor.Yellow, false, 1)).Concat(Mixed(8)));
            Invoke(director, "RefreshProps");
            yield return Until(() => HumanReady, "human turn with items");

            Begin("03-gameplay-item");
            yield return Wait(2);
            yield return UseItem(ItemType.PlasticFork);
            yield return Wait(2);
            yield return Eat();
            yield return Until(() => HumanReady, "continue after good");
            yield return Wait(1.5f);
            yield return UseItem(ItemType.HammerCrush);
            yield return Wait(2);
            // Grappling Hook: steal the bot's Hot Milk, which is used right away.
            int hooks = Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook];
            Select(FindProp(Human, ItemType.GrapplingHook));
            yield return Wait(1);
            Select(FindProp(victim, ItemType.HotMilk));
            yield return Until(() => Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook] == hooks + 1 && HumanReady, "Grappling Hook");
            yield return Wait(2);
            // Little Pocket: three options, pick one that needs no target.
            yield return UseItem(ItemType.LittlePocket);
            yield return Until(() => Props.Any(p => Read<bool>(p, "pocket") && PropButton(p).interactable), "Little Pocket options");
            yield return Wait(2);
            var option = Props.Where(p => Read<bool>(p, "pocket")).First(p => Read<ItemType>(p, "type") != ItemType.GrapplingHook);
            Select(PropButton(option));
            yield return Until(() => Engine.Phase == GamePhase.AwaitingAction && HumanReady, "Little Pocket pick used");
            // Eat until a bad candy ends the turn (the Pocket pick may swap or crush the active candy).
            for (int i = 0; i < 4 && Engine.Current == Human && !Engine.IsOver; i++)
            {
                yield return Until(() => HumanReady || Engine.Current != Human || Engine.IsOver, "human turn continues");
                if (Engine.Current != Human || Engine.IsOver) break;
                yield return Wait(1.5f);
                yield return Eat();
            }
            yield return Wait(22, () => Engine.IsOver);
            End("03-gameplay-item", "Gameplay with items from the seat: Plastic Fork reveal, eat, Hammer Crush, Grappling Hook steal, Little Pocket pick, bots' turns");
        }

        [UnityTest, Category("Trailer"), Timeout(1800000)]
        public IEnumerator Clip_spectator_and_results_from_player_seat()
        {
            yield return Load();
            yield return StartMatch(3);
            // Human is knocked out by a bad blue; bots are near death so the match ends within the results clip.
            var bots = Engine.Players.Where(p => p.IsBot).ToArray();
            Engine.DebugSetHp(Human, 1);
            for (int i = 0; i < bots.Length; i++) Engine.DebugSetHp(bots[i], i == 0 ? Engine.Config.MaxHp : i);
            var pool = Candies(CandyColor.Blue, false, 1).ToList();
            for (int i = 0; i < 12; i++)
                pool.AddRange(Candies(CandyColor.Yellow, false, 1).Concat(Candies(CandyColor.Red, true, 1)).Concat(Candies(CandyColor.Yellow, false, 1)));
            Engine.DebugSetPool(pool);
            yield return Until(() => HumanReady, "human turn before KO");

            Begin("04-spectator");
            yield return Wait(1.5f);
            yield return Eat();
            yield return Until(() => director.SpectatorInputLocked || Engine.IsOver, "human KO");
            Assert.IsTrue(Human.Eliminated);
            yield return Wait(14, () => Engine.IsOver);
            End("04-spectator", "Spectator: knocked-out player turns transparent and keeps watching from the seat");

            Begin("05-results");
            var result = director.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == "Final Results");
            yield return Until(() => result.gameObject.activeInHierarchy, "results screen", 180);
            yield return Wait(7);
            Assert.IsTrue(Engine.IsOver && Engine.Winner != null);
            StringAssert.Contains(Strings.Winner.Text, result.text);
            End("05-results", "Results: last bots standing, winner card with knockout order, Play again / Back to menu");
        }

        [UnityTest, Category("Trailer"), Timeout(600000)]
        public IEnumerator Clip_online_lobby_with_room_code_from_player_seat()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                Assert.Ignore("No network: online lobby clip not recorded.");
            yield return Load();
            var lobby = director.Lobby as MonoBehaviour;
            Assert.IsNotNull(lobby, "Scene must have a SessionRoomLobby (BOB-046).");
            Assert.IsTrue(Button("Create Room").interactable, "Create Room enabled: NetworkManager, online table and UGS project ready.");
            Begin("06-lobby-online");
            yield return Wait(2);
            Select(Button("Create Room"));
            yield return Until(() => Read<string>(lobby, "code") != null || !Read<bool>(lobby, "busy"), "Create Room finished", 120);
            string code = Read<string>(lobby, "code");
            Assert.IsNotNull(code, "Create Room failed: " + Read<TextMesh>(director, "caption").text);
            var codeText = Read<TextMesh>(lobby, "codeText");
            Assert.IsTrue(codeText.gameObject.activeInHierarchy, "Room code shown on the table panel.");
            StringAssert.Contains(OnlineRoom.FormatCode(code), codeText.text);
            yield return Wait(8);
            End("06-lobby-online", "Online lobby: Create Room, 6-digit room code, players in room, empty seats filled by bots");
        }

        // ------------------------------------------------------------------ camera presets (bots-only match)

        [UnityTest, Category("Trailer"), Timeout(1800000)]
        public IEnumerator Clip_preset_close_up_bot_reacting() =>
            RecordPreset("CloseUpBot", "07-close-up-bot", "Bot close-up camera: bots eating, giving and reacting");

        [UnityTest, Category("Trailer"), Timeout(1800000)]
        public IEnumerator Clip_preset_table_from_above() =>
            RecordPreset("TopDown", "08-top-down", "Top-down table camera, slow orbit");

        IEnumerator RecordPreset(string shot, string name, string what)
        {
            preset = shot;
            yield return Load();
            Call("Begin", shot, Path.Combine(root, name), false);
            float deadline = Time.realtimeSinceStartup + 1500;
            // The preset stops by itself after its duration (45 / 30 seconds of video).
            while (Recording)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Preset " + shot + " did not finish.");
                yield return null;
            }
            End(name, what, false);
        }

        // ------------------------------------------------------------------ recording

        void Begin(string name)
        {
            Time.timeScale = 1;
            Call("Begin", "Seat", Path.Combine(root, name), false);
            Assert.IsTrue(Recording);
        }

        /// <summary>Stop the clip, check the 1920x1080 frames on disk, then record it in clips.txt for the runner.</summary>
        void End(string name, string what, bool seat = true)
        {
            if (seat) Assert.Less(Vector3.Distance(Seat, Camera.main.transform.position), .15f, name + ": camera must be at the player seat.");
            Call("Stop");
            Assert.IsFalse(Recording);
            int frames = Frames;
            string folder = Path.Combine(root, name);
            int files = Directory.GetFiles(folder, "frame_*.jpg").Length;
            Assert.Greater(frames, 30, name + ": clip too short.");
            Assert.AreEqual(frames, files, name + ": frame count on disk.");
            var image = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(image.LoadImage(File.ReadAllBytes(Path.Combine(folder, $"frame_{frames - 1:D6}.jpg"))));
                Assert.AreEqual((1920, 1080), (image.width, image.height), name);
            }
            finally { Object.Destroy(image); }
            Assert.IsTrue(File.Exists(Path.Combine(folder, "make_video.cmd")));
            Assert.IsTrue(File.Exists(Path.Combine(folder, "make_video.sh")));
            File.AppendAllText(Path.Combine(root, "clips.txt"), $"{name}\t{frames}\t{what}\n");
            TestContext.WriteLine($"{name}: {frames} frames ({frames / 30f:0.0} s) in {folder}");
        }

        // ------------------------------------------------------------------ table

        IEnumerator Load()
        {
            Time.timeScale = 1;
            yield return EditorXR.Stop();
            if (preset != null) SceneManager.sceneLoaded += Configure;
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            SceneManager.sceneLoaded -= Configure;
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "Director must come from the gameplay scene.");
            yield return Until(() => Read<bool>(director, "ready"), "director ready", 60);
            if (preset != null) yield break;
            director.loop = false;
            director.recordTelemetry = false;
            director.rules.TurnTimeLimitSeconds = 0;
            director.rules.RandomDraw = false;
            // Same as RegressionTests: device polling disabled, actions via TableSelectable, focus kept.
            input = director.GetComponent<VRTableInput>();
            input.enabled = false;
            Invoke(input, "CreateVisuals");
            input.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            input.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Invoke(input, "OnInputFocusAcquired");
            yield return Until(() => input.HasFocus, "initial focus");
            Assert.AreEqual("<b>Play vs Bots</b>", Button("Play vs Bots").GetComponent<TextMesh>().text, "Trailer must be English.");
        }

        // sceneLoaded runs after Awake and before Start, same as the Record Video menu.
        void Configure(Scene loaded, LoadSceneMode mode)
        {
            var d = Object.FindFirstObjectByType<CandyTableDirector>();
            if (d != null) Call("Configure", d, preset);
        }

        // Setup before the clip is sped up; Begin restores timeScale 1.
        IEnumerator StartMatch(int bots)
        {
            Time.timeScale = 4;
            Select(Button("Play vs Bots"));
            while (Room.BotCount > bots) Select(Button("Fewer bots"));
            while (Room.BotCount < bots) Select(Button("More bots"));
            director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
            bool onboardingDone = Read<bool>(director, "onboardingDone");
            Select(Button("Start"));
            if (!onboardingDone)
            {
                yield return Until(() => Button("Skip").interactable, "onboarding before the match");
                Select(Button("Skip"));
            }
            yield return Until(() => Engine != null, "game after Start");
            yield return Until(() => HumanReady, "first human turn", 60);
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

        IEnumerator UseItem(ItemType type)
        {
            int used = Engine.Stats.ItemsUsed[(int)type];
            Select(FindProp(Human, type));
            yield return Until(() => Engine.Stats.ItemsUsed[(int)type] == used + 1, "use " + type);
            if (type != ItemType.LittlePocket) yield return Until(() => HumanReady, "animation " + type);
        }

        IEnumerator Eat()
        {
            int eats = Engine.Stats.EatSelf;
            Select(Candy);
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "eat candy");
        }

        static IEnumerable<Candy> Candies(CandyColor color, bool good, int count) =>
            Enumerable.Range(0, count).Select(_ => new Candy(color, good));

        static IEnumerable<Candy> Mixed(int sets) => Enumerable.Range(0, sets).SelectMany(_ =>
            Candies(CandyColor.Yellow, true, 1).Concat(Candies(CandyColor.Red, false, 1))
                .Concat(Candies(CandyColor.Blue, true, 1)).Concat(Candies(CandyColor.Yellow, false, 1)));

        TableSelectable FindProp(PlayerState owner, ItemType type)
        {
            var prop = Props.FirstOrDefault(p => !Read<bool>(p, "pocket") && Read<ItemType>(p, "type") == type
                && Read<PlayerState>(Read<object>(p, "seat"), "player") == owner);
            Assert.IsNotNull(prop, $"Item {type} of {owner.Name} is not on the pad.");
            return PropButton(prop);
        }

        static TableSelectable PropButton(object prop) => Read<TableSelectable>(prop, "selectable");

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Button " + label);

        static void Select(TableSelectable target)
        {
            Assert.IsNotNull(target);
            Assert.IsTrue(target.gameObject.activeInHierarchy && target.isActiveAndEnabled && target.interactable, target.name + " not interactable");
            target.Select();
        }

        /// <summary>Wait seconds of game time (video seconds while recording); stops early if stop is satisfied.</summary>
        static IEnumerator Wait(float seconds, Func<bool> stop = null)
        {
            float end = Time.time + seconds;
            while (Time.time < end && (stop == null || !stop())) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, string message, float seconds = 30)
        {
            float end = Time.time + seconds, realtime = Time.realtimeSinceStartup + 600;
            while (!condition())
            {
                Assert.Less(Time.time, end, message);
                Assert.Less(Time.realtimeSinceStartup, realtime, message + " (real time)");
                yield return null;
            }
        }

        static object Call(string method, params object[] args) =>
            Recorder.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    }
}
#endif
