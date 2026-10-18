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
    /// BOB-052: klip trailer Devpost 1080p30 dengan UI English, direkam GameplayRecorder (BOB-045) di scene asli.
    /// Kategori Trailer tidak ikut PlayMode biasa: runner laptop menjalankannya terpisah (-testCategory Trailer) hanya saat
    /// Docs/Release/record-trailer.txt = yes, dengan folder hasil -bobTrailerDir ~/bob-ci/videos/&lt;commit&gt;. Tanpa argumen itu
    /// (dijalankan manual dari Test Runner) klip ditulis ke temporaryCachePath lalu dihapus.
    /// Klip kamera Kursi (mata di kursi pemain): menu, Atur bot, gameplay dengan item, spectator, hasil akhir, lobby online
    /// dengan kode room. Preset Close-up Bot dan Meja dari Atas memakai pertandingan bot saja seperti menu Rekam Video.
    /// Tiap klip menambah satu baris ke clips.txt (nama, frame, adegan); runner menyatukan frame jadi MP4 dengan ffmpeg.
    /// Aksi pemain lewat TableSelectable seperti RegressionTests, jadi tangan/pointer VR tidak tampil. Waktu tunggu memakai
    /// waktu game: selama merekam Time.captureFramerate 30 membuat satu frame = 1/30 detik video.
    /// </summary>
    // Kategori juga di tiap tes: filter -testCategory "!Trailer" harus mengecualikan test case, bukan hanya fixture.
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
            oldTimeScale = Time.timeScale;
            oldRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-bobTrailerDir");
            keep = i >= 0 && i + 1 < args.Length;
            root = keep ? args[i + 1] : Path.Combine(Application.temporaryCachePath, "BoBTrailerTest");
            Directory.CreateDirectory(root);
            // Trailer selalu English (bahasa bawaan); pilihan di laptop dikembalikan di TearDown.
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
                yield return new WaitForSecondsRealtime(2); // hapus session berjalan async
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

        // ------------------------------------------------------------------ klip kursi pemain

        [UnityTest, Category("Trailer"), Timeout(1800000)]
        public IEnumerator Klip_menu_dan_atur_bot_dari_kursi_pemain()
        {
            yield return Load();
            Begin("01-menu");
            yield return Wait(7);
            End("01-menu", "Main menu from the seat: Play vs Bots, Create Room, Join Room, Settings");

            Begin("02-atur-bot");
            yield return Wait(1.5f);
            Select(Button("Play vs Bots"));
            yield return Wait(2);
            while (Room.BotCount < Room.MaxBots)
            {
                Select(Button("More bots"));
                yield return Wait(.8f);
            }
            yield return Wait(1);
            // Tingkat satu bot diputar Easy -> Normal -> Hard di atas kepalanya.
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
                yield return Until(() => Button("Skip").interactable, "onboarding sebelum pertandingan");
                yield return Wait(3);
                Select(Button("Skip"));
            }
            yield return Until(() => Engine != null, "game setelah Start");
            yield return Wait(4); // undian d20 dan pengumuman ronde pelayan
            End("02-atur-bot", "Table setup: bot count 1-5, per-bot level Easy/Normal/Hard, Start, d20 turn order");
        }

        [UnityTest, Category("Trailer"), Timeout(1800000)]
        public IEnumerator Klip_gameplay_dengan_item_dari_kursi_pemain()
        {
            yield return Load();
            yield return StartMatch(3);
            int max = Engine.Config.MaxHp;
            var victim = Engine.Players.First(p => p.IsBot && p.IsAlive);
            Engine.DebugSetHp(Human, max - 1);
            Engine.DebugGiveItem(victim, ItemType.HotMilk);
            foreach (var type in new[] { ItemType.PlasticFork, ItemType.HammerCrush, ItemType.GrapplingHook, ItemType.LittlePocket })
                Engine.DebugGiveItem(Human, type);
            // Urutan permen tetap (RandomDraw=false): Fork membuka good lalu dimakan, Hammer menghancurkan bad, good,
            // lalu bad yang mengakhiri giliran; sisanya campuran untuk bot.
            Engine.DebugSetPool(Candies(CandyColor.Yellow, true, 1).Concat(Candies(CandyColor.Blue, false, 1))
                .Concat(Candies(CandyColor.Red, true, 1)).Concat(Candies(CandyColor.Yellow, false, 1)).Concat(Mixed(8)));
            Invoke(director, "RefreshProps");
            yield return Until(() => HumanReady, "giliran manusia dengan item");

            Begin("03-gameplay-item");
            yield return Wait(2);
            yield return UseItem(ItemType.PlasticFork);
            yield return Wait(2);
            yield return Eat();
            yield return Until(() => HumanReady, "lanjut setelah good");
            yield return Wait(1.5f);
            yield return UseItem(ItemType.HammerCrush);
            yield return Wait(2);
            // Grappling Hook: ambil Hot Milk bot lalu langsung dipakai.
            int hooks = Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook];
            Select(FindProp(Human, ItemType.GrapplingHook));
            yield return Wait(1);
            Select(FindProp(victim, ItemType.HotMilk));
            yield return Until(() => Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook] == hooks + 1 && HumanReady, "Grappling Hook");
            yield return Wait(2);
            // Little Pocket: tiga pilihan, ambil yang tidak butuh target.
            yield return UseItem(ItemType.LittlePocket);
            yield return Until(() => Props.Any(p => Read<bool>(p, "pocket") && PropButton(p).interactable), "opsi Little Pocket");
            yield return Wait(2);
            var option = Props.Where(p => Read<bool>(p, "pocket")).First(p => Read<ItemType>(p, "type") != ItemType.GrapplingHook);
            Select(PropButton(option));
            yield return Until(() => Engine.Phase == GamePhase.AwaitingAction && HumanReady, "pilihan Little Pocket dipakai");
            // Makan sampai permen bad mengakhiri giliran (pilihan Pocket bisa mengganti atau menghancurkan permen aktif).
            for (int i = 0; i < 4 && Engine.Current == Human && !Engine.IsOver; i++)
            {
                yield return Until(() => HumanReady || Engine.Current != Human || Engine.IsOver, "giliran manusia lanjut");
                if (Engine.Current != Human || Engine.IsOver) break;
                yield return Wait(1.5f);
                yield return Eat();
            }
            yield return Wait(22, () => Engine.IsOver);
            End("03-gameplay-item", "Gameplay with items from the seat: Plastic Fork reveal, eat, Hammer Crush, Grappling Hook steal, Little Pocket pick, bots' turns");
        }

        [UnityTest, Category("Trailer"), Timeout(1800000)]
        public IEnumerator Klip_spectator_dan_hasil_akhir_dari_kursi_pemain()
        {
            yield return Load();
            yield return StartMatch(3);
            // Manusia KO dengan biru bad; bot sekarat supaya pertandingan selesai dalam klip hasil akhir.
            var bots = Engine.Players.Where(p => p.IsBot).ToArray();
            Engine.DebugSetHp(Human, 1);
            for (int i = 0; i < bots.Length; i++) Engine.DebugSetHp(bots[i], i == 0 ? Engine.Config.MaxHp : i);
            var pool = Candies(CandyColor.Blue, false, 1).ToList();
            for (int i = 0; i < 12; i++)
                pool.AddRange(Candies(CandyColor.Yellow, false, 1).Concat(Candies(CandyColor.Red, true, 1)).Concat(Candies(CandyColor.Yellow, false, 1)));
            Engine.DebugSetPool(pool);
            yield return Until(() => HumanReady, "giliran manusia sebelum KO");

            Begin("04-spectator");
            yield return Wait(1.5f);
            yield return Eat();
            yield return Until(() => director.SpectatorInputLocked || Engine.IsOver, "manusia KO");
            Assert.IsTrue(Human.Eliminated);
            yield return Wait(14, () => Engine.IsOver);
            End("04-spectator", "Spectator: knocked-out player turns transparent and keeps watching from the seat");

            Begin("05-hasil-akhir");
            var result = director.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == "Hasil Akhir");
            yield return Until(() => result.gameObject.activeInHierarchy, "layar hasil akhir", 180);
            yield return Wait(7);
            Assert.IsTrue(Engine.IsOver && Engine.Winner != null);
            StringAssert.Contains(Strings.Winner.Text, result.text);
            End("05-hasil-akhir", "Results: last bots standing, winner card with knockout order, Play again / Back to menu");
        }

        [UnityTest, Category("Trailer"), Timeout(600000)]
        public IEnumerator Klip_lobby_online_dengan_kode_room_dari_kursi_pemain()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                Assert.Ignore("Tidak ada jaringan: klip lobby online tidak direkam.");
            yield return Load();
            var lobby = director.Lobby as MonoBehaviour;
            Assert.IsNotNull(lobby, "Scene wajib punya SessionRoomLobby (BOB-046).");
            Assert.IsTrue(Button("Create Room").interactable, "Create Room aktif: NetworkManager, meja online dan project UGS siap.");
            Begin("06-lobby-online");
            yield return Wait(2);
            Select(Button("Create Room"));
            yield return Until(() => Read<string>(lobby, "code") != null || !Read<bool>(lobby, "busy"), "Create Room selesai", 120);
            string code = Read<string>(lobby, "code");
            Assert.IsNotNull(code, "Create Room gagal: " + Read<TextMesh>(director, "caption").text);
            var codeText = Read<TextMesh>(lobby, "codeText");
            Assert.IsTrue(codeText.gameObject.activeInHierarchy, "Kode room tampil di panel meja.");
            StringAssert.Contains(OnlineRoom.FormatCode(code), codeText.text);
            yield return Wait(8);
            End("06-lobby-online", "Online lobby: Create Room, 6-digit room code, players in room, empty seats filled by bots");
        }

        // ------------------------------------------------------------------ preset kamera (pertandingan bot saja)

        [UnityTest, Category("Trailer"), Timeout(1800000)]
        public IEnumerator Klip_preset_close_up_bot_bereaksi() =>
            RecordPreset("CloseUpBot", "07-close-up-bot", "Bot close-up camera: bots eating, giving and reacting");

        [UnityTest, Category("Trailer"), Timeout(1800000)]
        public IEnumerator Klip_preset_meja_dari_atas() =>
            RecordPreset("MejaAtas", "08-meja-atas", "Top-down table camera, slow orbit");

        IEnumerator RecordPreset(string shot, string name, string what)
        {
            preset = shot;
            yield return Load();
            Call("Begin", shot, Path.Combine(root, name), false);
            float deadline = Time.realtimeSinceStartup + 1500;
            // Preset berhenti sendiri setelah durasinya (45 / 30 detik video).
            while (Recording)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Preset " + shot + " tidak selesai.");
                yield return null;
            }
            End(name, what, false);
        }

        // ------------------------------------------------------------------ rekaman

        void Begin(string name)
        {
            Time.timeScale = 1;
            Call("Begin", "Kursi", Path.Combine(root, name), false);
            Assert.IsTrue(Recording);
        }

        /// <summary>Hentikan klip, cek frame 1920x1080 di disk, lalu catat di clips.txt untuk runner.</summary>
        void End(string name, string what, bool seat = true)
        {
            if (seat) Assert.Less(Vector3.Distance(Seat, Camera.main.transform.position), .15f, name + ": kamera harus di kursi pemain.");
            Call("Stop");
            Assert.IsFalse(Recording);
            int frames = Frames;
            string folder = Path.Combine(root, name);
            int files = Directory.GetFiles(folder, "frame_*.jpg").Length;
            Assert.Greater(frames, 30, name + ": klip terlalu pendek.");
            Assert.AreEqual(frames, files, name + ": jumlah frame di disk.");
            var image = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(image.LoadImage(File.ReadAllBytes(Path.Combine(folder, $"frame_{frames - 1:D6}.jpg"))));
                Assert.AreEqual((1920, 1080), (image.width, image.height), name);
            }
            finally { Object.Destroy(image); }
            Assert.IsTrue(File.Exists(Path.Combine(folder, "buat_video.cmd")));
            File.AppendAllText(Path.Combine(root, "clips.txt"), $"{name}\t{frames}\t{what}\n");
            TestContext.WriteLine($"{name}: {frames} frame ({frames / 30f:0.0} detik) di {folder}");
        }

        // ------------------------------------------------------------------ meja

        IEnumerator Load()
        {
            Time.timeScale = 1;
            yield return EditorXR.Stop();
            if (preset != null) SceneManager.sceneLoaded += Configure;
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            SceneManager.sceneLoaded -= Configure;
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "Director wajib berasal dari scene gameplay.");
            yield return Until(() => Read<bool>(director, "ready"), "director siap", 60);
            if (preset != null) yield break;
            director.loop = false;
            director.recordTelemetry = false;
            director.rules.TurnTimeLimitSeconds = 0;
            director.rules.RandomDraw = false;
            // Sama dengan RegressionTests: polling perangkat dimatikan, aksi lewat TableSelectable, fokus tetap ada.
            input = director.GetComponent<VRTableInput>();
            input.enabled = false;
            Invoke(input, "CreateVisuals");
            input.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            input.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Invoke(input, "OnInputFocusAcquired");
            yield return Until(() => input.HasFocus, "fokus awal");
            Assert.AreEqual("<b>Play vs Bots</b>", Button("Play vs Bots").GetComponent<TextMesh>().text, "Trailer harus English.");
        }

        // sceneLoaded jalan setelah Awake dan sebelum Start, sama seperti menu Rekam Video.
        void Configure(Scene loaded, LoadSceneMode mode)
        {
            var d = Object.FindFirstObjectByType<CandyTableDirector>();
            if (d != null) Call("Configure", d, preset);
        }

        // Persiapan sebelum klip dipercepat; Begin mengembalikan timeScale 1.
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
                yield return Until(() => Button("Skip").interactable, "onboarding sebelum pertandingan");
                Select(Button("Skip"));
            }
            yield return Until(() => Engine != null, "game setelah Start");
            yield return Until(() => HumanReady, "giliran manusia pertama", 60);
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
            Assert.Fail("Tidak menemukan seed giliran manusia pertama.");
            return 0;
        }

        IEnumerator UseItem(ItemType type)
        {
            int used = Engine.Stats.ItemsUsed[(int)type];
            Select(FindProp(Human, type));
            yield return Until(() => Engine.Stats.ItemsUsed[(int)type] == used + 1, "pakai " + type);
            if (type != ItemType.LittlePocket) yield return Until(() => HumanReady, "animasi " + type);
        }

        IEnumerator Eat()
        {
            int eats = Engine.Stats.EatSelf;
            Select(Candy);
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "makan permen");
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
            Assert.IsNotNull(prop, $"Item {type} milik {owner.Name} tidak ada di alas.");
            return PropButton(prop);
        }

        static TableSelectable PropButton(object prop) => Read<TableSelectable>(prop, "selectable");

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Tombol " + label);

        static void Select(TableSelectable target)
        {
            Assert.IsNotNull(target);
            Assert.IsTrue(target.gameObject.activeInHierarchy && target.isActiveAndEnabled && target.interactable, target.name + " tidak interaktif");
            target.Select();
        }

        /// <summary>Tunggu detik waktu game (detik video saat merekam); berhenti lebih awal kalau stop terpenuhi.</summary>
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
                Assert.Less(Time.realtimeSinceStartup, realtime, message + " (waktu nyata)");
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
