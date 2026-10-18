using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BiteOrBetray.Core;
using BiteOrBetray.DebugTools;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// Regression pengganti uji Quest 3 (BOB-023). Satu tes per skenario supaya bagian "Tes gagal" di CI.md langsung
    /// menyebut skenarionya. Fixture sama dengan OfflineMatchTests: tes hanya menyiapkan sampel perangkat, isi dispenser,
    /// HP dan inventory; aksi tetap lewat TableSelectable/VRTableInput dan coroutine director.
    /// Screenshot dari kamera kursi pemain (Camera.Render ke RenderTexture) disimpan ke Logs/ci/screens/; runner
    /// menyalinnya ke Docs/Autopilot/screens/ untuk persetujuan owner.
    /// </summary>
    public class RegressionTests
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
        // Cue yang pernah dipasang ke pool SFX; klip tetap terpasang setelah selesai, jadi polling per frame cukup.
        readonly HashSet<GameplaySound> heard = new HashSet<GameplaySound>();

        GameEngine Engine => Read<GameEngine>(director, "engine");
        TableSelectable Candy => Read<TableSelectable>(director, "candySelectable");
        bool HumanReady => Candy != null && Candy.interactable && Read<BotDecision>(director, "humanChoice") == null;
        PlayerState Human => Engine.Players.Single(p => !p.IsBot);
        RoomSetup Room => Read<RoomSetup>(director, "room");
        IEnumerable<object> Props => Read<IList>(director, "props").Cast<object>();
        IEnumerable<TableSelectable> BotTargets => Read<IDictionary>(director, "botTargets").Values.Cast<TableSelectable>();

        [UnitySetUp]
        public IEnumerator LoadTable()
        {
            oldTimeScale = Time.timeScale;
            oldVolume = AudioListener.volume;
            oldRunInBackground = Application.runInBackground;
            Time.timeScale = 1;
            Application.runInBackground = true;
            heard.Clear();
            // Screenshot dan teks yang dicek suite ini selalu English (bahasa bawaan); pilihan di laptop dikembalikan di TearDown.
            oldLanguage = PlayerPrefs.HasKey(GameSettings.LanguageKey) ? PlayerPrefs.GetInt(GameSettings.LanguageKey) : (int?)null;
            PlayerPrefs.SetInt(GameSettings.LanguageKey, (int)Language.English);
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "Director wajib berasal dari scene gameplay.");
            // Until memanggil PollAudio, jadi audio diambil sebelum menunggu apa pun.
            audio = director.GetComponent<GameplayAudio>();
            Assert.IsNotNull(audio, "GameplayAudio wajib terpasang di director.");
            yield return Until(() => Read<bool>(director, "ready"), "menu selesai Start");
            Assert.IsTrue(Button("Play vs Bots").interactable);
            Assert.IsNull(Engine, "Scene harus dibuka di menu, belum ada pertandingan.");
            director.loop = false;
            director.recordTelemetry = false;
            director.tempo = 4;
            director.thinkSeconds = Vector2.zero;
            director.rules.ItemsPerRound = 0;
            director.rules.TurnTimeLimitSeconds = 0;
            director.rules.RandomDraw = false;
            input = director.GetComponent<VRTableInput>();
            Assert.IsNotNull(input);
            Assert.IsFalse(input.IsXRActive, "Suite ini dijalankan Editor tanpa perangkat XR.");
            // Polling perangkat diganti sampel tes; komponen/coroutine input yang asli tetap dipakai.
            input.enabled = false;
            Invoke(input, "CreateVisuals");
            input.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            input.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Invoke(input, "OnInputFocusAcquired");
            yield return Until(() => input.HasFocus, "fokus awal fixture");
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

        // ------------------------------------------------------------------ skenario

        [UnityTest]
        public IEnumerator Menu_bot_setup_all_onboarding_pages_and_gameplay_with_screens()
        {
            Assert.IsTrue(Text("Judul Menu").gameObject.activeSelf);
            Assert.AreEqual("<b>Play vs Bots</b>", Label(Button("Play vs Bots")), "Menu screenshot harus English.");
            yield return Screenshot("01-menu");
            Select(Button("Play vs Bots"));
            SetBotCount(3);
            Assert.AreEqual($"<b>{Strings.BotCount.Format(3)}</b>", Text("Jumlah Bot").text);
            Assert.IsTrue(Button("Start").interactable);
            yield return Screenshot("02-atur-bot");

            // Onboarding memakai aturan bawaan (item + timer) supaya semua halaman tampil; game-nya tetap tanpa timer.
            var defaults = new RuleConfig();
            director.rules.ItemsPerRound = defaults.ItemsPerRound;
            director.rules.TurnTimeLimitSeconds = defaults.TurnTimeLimitSeconds;
            var pages = OnboardingGuide.Pages(director.rules, false);
            Assert.AreEqual(6, pages.Count, "Cara main, ambil, makan, Give, Item, Daftar item.");
            Select(Button("Start"));
            for (int page = 0; page < pages.Count; page++)
            {
                int expected = page;
                yield return Until(() => Read<int>(director, "onboardingPage") == expected, "halaman onboarding " + (page + 1));
                var text = Text("Cara Main");
                Assert.IsTrue(text.gameObject.activeSelf);
                Assert.AreEqual($"<b>{pages[page].Title}</b>\n<size=36>{string.Join("\n", pages[page].Lines)}</size>", text.text);
                Assert.IsNull(Engine, "Game belum dimulai selama onboarding.");
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
            yield return Until(() => Engine != null, "game setelah Mulai main");
            Assert.IsFalse(Text("Cara Main").gameObject.activeSelf);
            SetPool(true);
            yield return Until(() => HumanReady, "giliran manusia pertama");
            Assert.AreSame(Human, Engine.Current);
            foreach (var type in new[] { ItemType.HotMilk, ItemType.PlasticFork, ItemType.CloverCoin })
                Engine.DebugGiveItem(Human, type);
            RefreshProps();
            yield return new WaitForSeconds(5); // popup d20/ronde sudah hilang
            yield return Screenshot("04-gameplay");

            // Onboarding hanya sekali per sesi: kembali ke menu lalu Mulai langsung memulai game.
            director.ReturnToMenu();
            yield return null;
            Select(Button("Play vs Bots"));
            director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
            Select(Button("Start"));
            yield return Until(() => Engine != null, "game kedua tanpa onboarding");
            Assert.AreEqual(-1, Read<int>(director, "onboardingPage"));
            Assert.IsFalse(Text("Cara Main").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator UI_labels_do_not_overlap_in_menu_gameplay_spectator_and_results()
        {
            yield return Screenshot("BOB-049-menu");
            Assert.IsFalse(director.GetComponentsInChildren<TextMesh>().Any(t => t.text.Contains("Bite or Betray")),
                "Judul permainan cukup di papan nama ruangan.");
            var button = Button("Play vs Bots");
            // Kontrol negatif: audit harus benar-benar menolak dua bounds yang ditumpuk dengan sengaja.
            Button("Settings").transform.position = button.transform.position;
            var overlap = Assert.Throws<AssertionException>(() => AssertLabelLayout(Camera.main, "kontrol overlap"));
            StringAssert.Contains("bertumpuk", overlap.Message);
            yield return null; // posisi kembali dari jangkar runtime
            var text = button.GetComponent<TextMesh>();
            Color normal = text.color;
            button.SetHover(true);
            yield return null;
            Assert.AreNotEqual(normal, text.color, "Hover harus terlihat tanpa membesarkan tombol.");
            button.SetHover(false);
            button.interactable = false;
            yield return null;
            Assert.AreNotEqual(normal, text.color, "Disabled harus terbaca sebagai status berbeda.");
            var block = new MaterialPropertyBlock();
            text.transform.Find("Kartu").GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.AreEqual(3, block.GetFloat("_State"));
            button.interactable = true;

            yield return StartMatch(5, false);
            // Dua efek serentak di dispenser menguji kasus BAD/KO yang sebelumnya saling menimpa.
            var origin = Candy.transform.position + Vector3.up * .09f;
            Invoke(director, "Popup", origin, "BAD", Color.red, 4f);
            Invoke(director, "Popup", origin, "KO", Color.red, 4f);
            yield return Screenshot("BOB-049-gameplay");
            Engine.DebugSetHp(Human, 1);
            director.thinkSeconds = new Vector2(4, 4);
            Select(Candy);
            yield return Until(() => director.SpectatorInputLocked, "KO untuk audit label spectator");
            yield return Screenshot("BOB-049-spectator");
            director.thinkSeconds = Vector2.zero;
            yield return FinishMatch();
            AssertResults();
            yield return Screenshot("BOB-049-results");
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
            Assert.IsTrue(language.interactable, "Tombol bahasa ada di Settings.");
            Assert.AreEqual("<b>Language: English</b>", Label(language));
            Assert.AreEqual("<b>Settings</b>", Text("Judul Menu").text);
            Select(language);

            // Langsung berlaku tanpa restart: Settings, menu utama, Atur bot, narasi dan nama kursi manusia.
            Assert.AreEqual(Language.Indonesian, director.Settings.Language);
            Assert.AreEqual(Language.Indonesian, Localization.Language);
            Assert.AreEqual("<b>Bahasa: Bahasa Indonesia</b>", Label(language));
            Assert.AreEqual("<b>Kembali</b>", Label(Button("Back")));
            StringAssert.StartsWith("<b>Tangan dominan: ", Label(Button("Dominant hand")));
            StringAssert.Contains(Strings.MenuHintDesktop.Indonesian, Caption);
            Assert.AreEqual((int)Language.Indonesian, PlayerPrefs.GetInt(GameSettings.LanguageKey, -1), "Bahasa tersimpan di PlayerPrefs.");
            Select(Button("Back"));
            Assert.AreEqual("<b>Main vs Bot</b>", Label(Button("Play vs Bots")));
            Select(Button("Play vs Bots"));
            StringAssert.Contains("Atur meja", Caption);
            Assert.AreEqual($"<b>{Room.BotCount} bot</b>", Text("Jumlah Bot").text);
            Assert.AreEqual("<b>Mulai</b>", Label(Button("Start")));
            Assert.AreEqual("Kamu", director.HumanName);
            Select(Button("Back to menu"));

            // Sesi baru: scene dimuat ulang (lewat scene kosong seperti TearDown), bahasa dibaca lagi dari PlayerPrefs.
            SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-048 Sesi baru"));
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            audio = director.GetComponent<GameplayAudio>();
            yield return Until(() => Read<bool>(director, "ready"), "menu sesi baru");
            Assert.AreEqual(Language.Indonesian, director.Settings.Language);
            Assert.AreEqual("<b>Main vs Bot</b>", Label(Button("Play vs Bots")));
            StringAssert.Contains(Strings.MenuHintDesktop.Indonesian, Caption);

            // Kembali ke English lewat tombol yang sama, juga langsung dan tersimpan.
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

            // Plastic Fork: permen aktif yang sama terungkap untuk semua pemain.
            var candy = Engine.ActiveCandy;
            yield return UseItem(ItemType.PlasticFork);
            Assert.AreSame(candy, Engine.ActiveCandy);
            Assert.IsTrue(candy.Revealed);
            yield return Until(() => Text("Label Permen").text.Contains("GOOD"), "label permen terungkap");

            // Magic Gloves: permen lama kembali ke dispenser, permen lain keluar tertutup. Permen yang sudah ditusuk Fork
            // tetap terbuka di dispenser (keputusan owner, RulesTests.GlovesAfterFork_ForkedCandyStaysRevealed).
            int pool = Engine.CandiesInDispenser;
            yield return UseItem(ItemType.MagicGloves);
            Assert.AreNotSame(candy, Engine.ActiveCandy);
            Assert.AreSame(candy, Engine.DebugPool.Last(), "RandomDraw=false: permen lama masuk di belakang.");
            Assert.IsTrue(candy.Revealed, "Permen hasil Fork tetap terbuka setelah Gloves.");
            Assert.IsFalse(Engine.ActiveCandy.Revealed, "Permen baru dari Gloves tidak terungkap.");
            Assert.AreEqual(pool, Engine.CandiesInDispenser);

            // Hammer Crush: permen aktif hancur dan terungkap, sisa good berkurang, permen baru keluar.
            candy = Engine.ActiveCandy;
            int good = Engine.GoodLeft;
            yield return UseItem(ItemType.HammerCrush);
            Assert.IsTrue(candy.Revealed);
            Assert.AreNotSame(candy, Engine.ActiveCandy);
            Assert.AreEqual(good - 1, Engine.GoodLeft);
            Assert.AreEqual(pool - 1, Engine.CandiesInDispenser);

            // Hot Milk: HP +1, tidak melebihi HP maks.
            Engine.DebugSetHp(Human, max - 2);
            yield return UseItem(ItemType.HotMilk);
            Assert.AreEqual(max - 1, Human.Hp);
            Engine.DebugSetHp(Human, max);
            yield return UseItem(ItemType.HotMilk);
            Assert.AreEqual(max, Human.Hp, "Hot Milk di HP maks terbuang.");

            // Cupcake: efek permen bad berikutnya diblok sekali, giliran tetap berlanjut.
            yield return UseItem(ItemType.Cupcake);
            Assert.IsTrue(Human.HasCupcake);
            Engine.DebugSetPool(Candies(CandyColor.Blue, false, 1).Concat(Candies(CandyColor.Yellow, true, 63)));
            int blocks = Engine.Stats.CupcakeBlocks, eats = Engine.Stats.EatSelf;
            Select(Candy);
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "makan bad dengan cupcake");
            Assert.AreEqual(max, Human.Hp);
            Assert.IsFalse(Human.HasCupcake);
            Assert.AreEqual(blocks + 1, Engine.Stats.CupcakeBlocks);
            Assert.AreSame(Human, Engine.Current, "Cupcake: giliran berlanjut walau permen bad.");
            yield return Until(() => HumanReady, "giliran berlanjut setelah cupcake");

            // Clover Coin: menempel ke permen aktif dan terpakai saat permen dimakan.
            yield return UseItem(ItemType.CloverCoin);
            Assert.IsTrue(Engine.CloverOnActive);
            int triggers = Engine.Stats.CloverTriggers;
            eats = Engine.Stats.EatSelf;
            Select(Candy);
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "makan permen clover");
            Assert.AreEqual(triggers + 1, Engine.Stats.CloverTriggers);
            Assert.IsFalse(Engine.CloverOnActive);
            yield return Until(() => HumanReady, "giliran berlanjut setelah clover");

            // Little Pocket: 3 pilihan acak tanpa Pocket; pilihan langsung dipakai.
            Engine.DebugGiveItem(victim, ItemType.HotMilk);
            yield return UseItem(ItemType.LittlePocket);
            Assert.AreEqual(GamePhase.AwaitingPocketChoice, Engine.Phase);
            Assert.AreEqual(3, Engine.PocketOptions.Count);
            CollectionAssert.DoesNotContain(Engine.PocketOptions, ItemType.LittlePocket);
            yield return Until(() => Props.Any(p => Read<bool>(p, "pocket") && PropButton(p).interactable), "opsi Pocket");
            var options = Props.Where(p => Read<bool>(p, "pocket")).ToArray();
            Assert.AreEqual(3, options.Length);
            var option = options.FirstOrDefault(p => Read<ItemType>(p, "type") != ItemType.GrapplingHook) ?? options[0];
            var chosen = Read<ItemType>(option, "type");
            int chosenUsed = Engine.Stats.ItemsUsed[(int)chosen];
            Select(PropButton(option));
            if (chosen == ItemType.GrapplingHook) Select(FindProp(victim, ItemType.HotMilk));
            yield return Until(() => Engine.Phase == GamePhase.AwaitingAction && HumanReady, "pilihan Pocket dipakai");
            Assert.AreEqual(chosenUsed + 1, Engine.Stats.ItemsUsed[(int)chosen], "Pilihan Pocket " + chosen);
            Assert.IsEmpty(Engine.PocketOptions);

            // Grappling Hook: ambil 1 item lawan dan langsung pakai.
            if (!victim.Items.Contains(ItemType.HotMilk)) Engine.DebugGiveItem(victim, ItemType.HotMilk);
            Engine.DebugSetHp(Human, max - 1);
            Engine.DebugGiveItem(Human, ItemType.GrapplingHook);
            RefreshProps();
            int victimItems = victim.Items.Count, milk = Engine.Stats.ItemsUsed[(int)ItemType.HotMilk];
            int hooks = Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook];
            Select(FindProp(Human, ItemType.GrapplingHook));
            Select(FindProp(victim, ItemType.HotMilk));
            yield return Until(() => Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook] == hooks + 1, "Grappling manusia");
            yield return Until(() => HumanReady, "animasi Grappling selesai");
            Assert.AreEqual(victimItems - 1, victim.Items.Count);
            Assert.AreEqual(milk + 1, Engine.Stats.ItemsUsed[(int)ItemType.HotMilk], "Item curian langsung dipakai.");
            Assert.AreEqual(max, Human.Hp);
            foreach (var type in ItemInfo.All) Assert.Greater(Engine.Stats.ItemsUsed[(int)type], 0, ItemInfo.Name(type));
            Assert.IsTrue(heard.Contains(GameplaySound.UseItem), "Cue item tidak diputar.");
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
                yield return Until(() => HumanReady, "giliran manusia: " + label);
                Engine.DebugSetHp(Human, start);
                Engine.DebugSetHp(bot, max);
                // Pengisi kuning bad: bot hanya bisa makan/menyuapi -1, lalu giliran kembali ke manusia.
                Engine.DebugSetPool(Candies(color, good, 1).Concat(Candies(CandyColor.Yellow, false, 16)));
                if (clover) yield return UseItem(ItemType.CloverCoin);
                int mult = clover ? 2 : 1;
                int expected = good ? Math.Min(max, start + CandyRules.GoodHeal(color) * mult)
                    : Math.Max(0, start - Math.Min(CandyRules.BadDamage(color) * mult, CandyRules.MaxDamagePerCandy));
                int eats = Engine.Stats.EatSelf, skipped = Engine.Stats.TurnsSkipped, logs = Engine.LogLines.Count;
                // Bergantian jalur VR (ambil ke mulut) dan klik/pilih permen.
                if (i % 2 == 0)
                {
                    Grab(Hand(0));
                    EatAtMouth();
                }
                else Select(Candy);
                yield return Until(() => Engine.Stats.EatSelf == eats + 1, "makan " + label);
                Assert.AreEqual(expected, Human.Hp, label);
                Assert.AreEqual(!good && CandyRules.BadParalyzes(color), Human.Paralyzed, "lumpuh " + label);
                Assert.AreEqual(good, Engine.Current == Human, "giliran berlanjut hanya setelah good: " + label);
                if (clover && !good && color == CandyColor.Blue)
                {
                    Assert.AreEqual(max - 3, Human.Hp, "Biru bad x2 = 4 dibatasi 3.");
                    Assert.IsTrue(Engine.LogLines.Skip(logs).Any(l => l.StartsWith("Damage dibatasi maks 3")));
                }
                if (Human.Paralyzed)
                {
                    yield return Until(() => Engine.Stats.TurnsSkipped > skipped, "giliran lumpuh dilewati: " + label);
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
            Assert.IsTrue(Engine.Players.All(p => p.Items.Count == 0), "Ronde 1 tanpa item (DealItemsOnFirstRound=false).");
            int round = Engine.RoundNumber, rounds = Engine.Stats.Rounds.Count, eats = Engine.Stats.EatSelf;
            Engine.DebugSetPool(Candies(CandyColor.Yellow, true, 1));
            Assert.AreEqual(0, Engine.CandiesInDispenser);
            Select(Candy);
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "makan permen terakhir ronde");
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
            Assert.AreEqual(total - 1, Engine.CandiesInDispenser, "Satu permen ronde baru langsung keluar.");
            int dealt = Math.Min(cfg.ItemsPerRound, cfg.MaxItems);
            Assert.IsTrue(Engine.AlivePlayers.All(p => p.Items.Count == dealt), "Item ronde baru dibagikan ke semua pemain hidup.");
            Assert.AreSame(Human, Engine.Current, "Giliran tetap berlanjut setelah good.");
            var speech = Read<TextMesh>(director, "speech");
            Assert.IsNotNull(speech, "Pelayan wajib ada di scene.");
            yield return Until(() => speech.text.Contains(Strings.WaiterRound.Format(round + 1)), "pengumuman pelayan ronde baru");
            StringAssert.Contains($"{announced.Good} good, {announced.Bad} bad", speech.text);
            yield return Until(() => HumanReady, "giliran setelah pengumuman ronde");
            foreach (var player in Engine.Players)
                Assert.AreEqual(dealt, Props.Count(p => !Read<bool>(p, "pocket")
                    && Read<PlayerState>(Read<object>(p, "seat"), "player") == player), "item di alas " + player.Name);
            Assert.IsTrue(heard.Contains(GameplaySound.Waiter), "Cue pelayan tidak diputar.");
        }

        [UnityTest]
        public IEnumerator Ko_spectator_results_music_sfx_and_telemetry_csv_with_screens()
        {
            var music = Read<AudioSource>(audio, "musicSource");
            bool output = AudioOutputAvailable();
            yield return null; // probe sudah dihancurkan sebelum menghitung AudioSource
            Assert.AreEqual(5, director.GetComponents<AudioSource>().Length, "4 SFX + 1 musik, dibuat sekali.");
            int sources = AudioSources();
            AssertMusic(music, output, "menu");

            // Volume Settings langsung berlaku ke AudioListener, lalu dikembalikan (Settings tersimpan di PlayerPrefs).
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

            // Ambil + makan (VR), item, lalu Give ke bot.
            var hand = Hand(0);
            int eats = Engine.Stats.EatSelf;
            Grab(hand);
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "makan manusia");
            yield return Until(() => HumanReady, "lanjut setelah good");
            Engine.DebugSetHp(Human, Engine.Config.MaxHp - 1);
            yield return UseItem(ItemType.HotMilk);
            // Sesudah Give, bot hanya punya kuning bad: paling banyak -1 per bot sebelum giliran manusia lagi.
            Engine.DebugSetPool(Candies(CandyColor.Yellow, true, 1).Concat(Candies(CandyColor.Yellow, false, 24)));
            int gives = Engine.Stats.Gives;
            Grab(hand);
            Write(hand, "hovered", BotTargets.First(t => t.interactable));
            Write(input, "actedThisFrame", false);
            Invoke(input, "ReleaseCandy", hand);
            yield return Until(() => Engine.Stats.Gives == gives + 1, "Give manusia");
            yield return Until(() => HumanReady, "giliran manusia setelah bot");

            // KO: manusia jadi spectator di kursinya, bot lanjut sampai pemenang.
            Engine.DebugSetHp(Human, 1);
            SetPool(false);
            Select(Candy);
            yield return Until(() => director.SpectatorInputLocked, "manusia KO");
            Assert.IsTrue(Human.Eliminated);
            var avatar = director.artRoot.Find("Seats/Seat_0").GetComponentInChildren<SeatedAvatar>(true);
            Assert.IsNotNull(avatar, "Avatar manusia wajib ada di scene.");
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            yield return Until(() => renderers.All(r => r.sharedMaterials.All(m => m.name.EndsWith(" Spectator"))), "avatar KO transparan");
            Assert.IsFalse(Candy != null && Candy.interactable);
            Assert.IsFalse(BotTargets.Any(t => t.interactable));
            yield return Screenshot("05-spectator");
            int turn = Engine.TurnNumber;
            yield return FinishMatch();
            Assert.Greater(Engine.TurnNumber, turn, "Bot melanjutkan pertandingan setelah manusia KO.");
            AssertResults();
            yield return Until(() => heard.Contains(GameplaySound.Win), "cue menang");
            yield return Screenshot("06-hasil-akhir");

            // CSV telemetry: format sama dengan meja debug, satu GAME_OVER di akhir.
            var telemetry = Read<TelemetryRecorder>(director, "telemetry");
            Assert.IsNotNull(telemetry, "recordTelemetry=true wajib membuat CSV.");
            var lines = File.ReadAllLines(telemetry.FilePath);
            Assert.AreEqual(TelemetryRecorder.Header, lines[0]);
            var rows = lines.Skip(1).Select(l => l.Split(',')).ToArray();
            Assert.IsTrue(rows.All(r => r.Length == 14), "Setiap baris 14 kolom.");
            var human = rows.Where(r => r[7] == "0").ToArray();
            Assert.IsTrue(human.Any(r => r[8] == "EAT"), "Baris makan manusia.");
            Assert.IsTrue(human.Any(r => r[8] == "ITEM" && r[9] == ItemInfo.Name(ItemType.HotMilk)), "Baris item manusia.");
            Assert.IsTrue(human.Any(r => r[8] == "GIVE" && Engine.Players.Any(p => p.IsBot && p.Name == r[9])),"Baris Give manusia.");
            Assert.IsTrue(rows.Any(r => r[7] == "1"), "Baris aksi bot.");
            Assert.IsTrue(rows.All(r => r[8] == "GAME_OVER" || double.Parse(r[10], CultureInfo.InvariantCulture) >= 0));
            Assert.AreEqual(1, rows.Count(r => r[8] == "GAME_OVER"));
            Assert.AreEqual("GAME_OVER", rows.Last()[8]);
            StringAssert.Contains("winner=" + Engine.Winner.Name, rows.Last()[9]);
            Directory.CreateDirectory("Logs/ci");
            File.Copy(telemetry.FilePath, "Logs/ci/regression-telemetry.csv", true);

            foreach (GameplaySound sound in Enum.GetValues(typeof(GameplaySound)))
                Assert.IsTrue(heard.Contains(sound), "Cue tidak pernah diputar: " + sound);
            Assert.AreEqual(sources, AudioSources(), "AudioSource bertambah selama game.");
            AssertMusic(music, output, "hasil akhir");
            var previous = Engine;
            Select(Button("Play again"));
            yield return Until(() => Engine != null && Engine != previous, "Main lagi");
            AssertMusic(music, output, "Main lagi");
            director.ReturnToMenu();
            yield return null;
            AssertMusic(music, output, "kembali ke menu");
            Assert.AreEqual(sources, AudioSources(), "AudioSource bertambah setelah Main lagi/menu.");
        }

        [UnityTest]
        public IEnumerator Five_restarts_keep_objects_materials_audio_sources_and_clips_stable()
        {
            yield return StartMatch(2);
            Census baseline = null;
            // Siklus 0 menjadi patokan; lima restart sesudahnya: empat lewat Restart (tombol Main lagi/R), satu lewat menu.
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
                    yield return Until(() => HumanReady, "restart siap " + cycle);
                }
                // Aktivitas yang membuat objek sementara (permen, popup, animasi item) sebelum dihitung.
                int eats = Engine.Stats.EatSelf;
                Grab(Hand(0));
                EatAtMouth();
                yield return Until(() => Engine.Stats.EatSelf == eats + 1 && HumanReady, "makan siklus " + cycle);
                yield return UseItem(ItemType.HotMilk);
                yield return new WaitForSeconds(5); // popup selesai dan Destroy sudah diproses
                var census = TakeCensus();
                TestContext.WriteLine($"Siklus {cycle}: {census}");
                if (baseline == null) baseline = census;
                else baseline.AssertSame(census, cycle == 3 ? "restart lewat menu" : "restart " + cycle);
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
            // Update berhenti sebelum polling perangkat ketika fokus hilang.
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
                    string label = $"{cycle.lose} siklus {round}";
                    // Grab mulai dengan sampel tombol lepas: setelah fokus kembali tombol wajib dilepas dulu.
                    Grab(hand);
                    FocusCallback(cycle.lose, cycle.lost);
                    Invoke(input, "Update");
                    Assert.IsFalse(input.HasFocus, label);
                    Assert.IsFalse(input.IsHoldingCandy, "Permen dikembalikan saat fokus hilang: " + label);
                    float timer = Read<float>(director, "turnTimer"), thinking = Read<float>(director, "decisionThink");
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.AreEqual(timer, Read<float>(director, "turnTimer"), .0001f, "timer beku: " + label);
                    Assert.AreEqual(thinking, Read<float>(director, "decisionThink"), .0001f, "waktu pikir beku: " + label);
                    Assert.IsTrue(Candy.interactable);
                    FocusCallback(cycle.regain, cycle.regained);
                    Assert.IsFalse(input.HasFocus, "Frame resume tidak boleh menghitung delta pause: " + label);
                    // Update yang fokus mereset tanda ini sebelum polling perangkat; fixture tidak memanggil polling itu.
                    Write(input, "suspended", false);
                    yield return Until(() => input.HasFocus, "fokus kembali: " + label);
                    yield return new WaitForSecondsRealtime(.1f);
                    float now = Read<float>(director, "turnTimer");
                    Assert.Less(now, timer, "timer jalan lagi: " + label);
                    Assert.Greater(now, timer - 2, label);
                    Assert.AreEqual(sources, AudioSources(), label);
                }
            int eats = Engine.Stats.EatSelf;
            Grab(hand);
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "input pulih setelah enam siklus fokus");
        }

        [UnityTest]
        public IEnumerator Controller_path_works_and_pinch_never_doubles_input()
        {
            yield return StartMatch(1);
            var controller = Hand(1);
            var pinch = Hand(0);
            // Sumber pinch seperti UpdateInputSource saat tangan Meta XR terlacak dan controller tidak.
            var pinchPose = new GameObject("BOB-023 Pinch").transform;
            pinchPose.SetParent(director.transform);
            Write(pinch, "pose", pinchPose);
            Write(pinch, "usesHandTracking", true);
            Write(pinch, "handTracked", true);
            Write(pinch, "pointerValid", true);
            SetPinch(pinch, true);
            var buttons = new object[] { pinch, false, false, false };
            Invoke(input, "ReadButtons", buttons);
            Assert.IsTrue((bool)buttons[1], "Pinch telunjuk = trigger.");
            Assert.IsFalse((bool)buttons[2] || (bool)buttons[3], "Pinch tidak pernah dibaca sebagai grip atau B/Y.");
            SetPinch(pinch, false);
            Invoke(input, "ReadButtons", buttons);
            Assert.IsFalse((bool)buttons[1]);

            // Controller dan pinch memilih item yang sama di frame yang sama: hanya satu aksi, urutan mana pun.
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
                Assert.AreEqual(1, selects, "Dua sumber input di frame yang sama hanya boleh satu aksi.");
                yield return Until(() => Engine.Stats.ItemsUsed[(int)type] == used + 1 && HumanReady, "item " + type);
                Assert.AreEqual(used + 1, Engine.Stats.ItemsUsed[(int)type]);
                Sample(controller, true, false, false);
                Sample(pinch, true, false, false);
            }

            // Jalur controller tetap utuh: ambil lalu makan di mulut.
            int eats = Engine.Stats.EatSelf;
            Grab(controller);
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1 && HumanReady, "makan lewat controller");

            // Pinch memegang permen; trigger controller di frame lain tidak mengambil/memakan ganda.
            eats = Engine.Stats.EatSelf;
            Aim(pinch, Candy);
            Sample(pinch, true, false, false);
            Sample(pinch, true, true, false);
            Assert.IsTrue(input.IsHoldingCandy);
            Assert.AreSame(pinch, Read<object>(input, "holder"));
            Aim(controller, Candy);
            Sample(controller, true, false, false);
            Sample(controller, true, true, false);
            Assert.AreSame(pinch, Read<object>(input, "holder"), "Controller tidak merebut permen yang dipegang pinch.");
            Assert.AreEqual(eats, Engine.Stats.EatSelf);
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1 && HumanReady, "makan lewat pinch");
            yield return new WaitForSeconds(1);
            Assert.AreEqual(eats + 1, Engine.Stats.EatSelf, "Satu pinch = satu makan.");
            Assert.AreEqual(0, Engine.Stats.Gives);
        }

        // ------------------------------------------------------------------ alur pertandingan

        IEnumerator StartMatch(int bots, bool good = true)
        {
            Select(Button("Play vs Bots"));
            SetBotCount(bots);
            director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
            bool onboardingDone = Read<bool>(director, "onboardingDone");
            Select(Button("Start"));
            if (!onboardingDone)
            {
                yield return Until(() => Button("Skip").interactable, "onboarding sebelum pertandingan");
                Select(Button("Skip"));
            }
            yield return Until(() => Engine != null, "game setelah Mulai");
            SetPool(good);
            yield return Until(() => HumanReady, "giliran manusia pertama");
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
            yield return Until(() => Engine.Stats.ItemsUsed[(int)type] == used + 1, "pakai " + type);
            if (type != ItemType.LittlePocket) yield return Until(() => HumanReady, "animasi " + type);
        }

        IEnumerator FinishMatch()
        {
            float deadline = Time.realtimeSinceStartup + 90;
            while (!Text("Hasil Akhir").gameObject.activeSelf)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Pertandingan macet: " + Engine.Phase + ", giliran " + Engine.TurnNumber);
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
            var result = Text("Hasil Akhir");
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

        // ------------------------------------------------------------------ input VR tanpa headset

        object Hand(int index) => Read<Array>(input, "hands").GetValue(index);

        /// <summary>Arahkan ray tangan dari atas target: controller lewat pose, pinch lewat ray aim Meta XR.</summary>
        void Aim(object hand, Component target)
        {
            var colliders = target.GetComponentsInChildren<Collider>();
            Assert.IsNotEmpty(colliders, target.name + " tanpa collider");
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
            Assert.AreSame(Candy, TableSelectable.Raycast(new Ray(pose.position, pose.forward), input.rayLength, out _), "Ray tangan harus mengenai permen.");
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

        /// <summary>Editor batch tanpa perangkat audio tidak pernah melaporkan isPlaying; probe membedakannya dari bug musik.</summary>
        bool AudioOutputAvailable()
        {
            var probe = new GameObject("BOB-023 Audio Probe").AddComponent<AudioSource>();
            probe.clip = audio.backgroundMusic;
            probe.volume = 0;
            probe.Play();
            bool playing = probe.isPlaying;
            Object.Destroy(probe.gameObject);
            TestContext.WriteLine("Output audio Editor: " + (playing ? "ada" : "tidak ada, isPlaying musik tidak diperiksa"));
            return playing;
        }

        void AssertMusic(AudioSource music, bool output, string where)
        {
            Assert.IsNotNull(music);
            Assert.IsTrue(music.loop, "Musik latar harus loop.");
            Assert.AreSame(audio.backgroundMusic, music.clip, $"Musik latar belum dimulai di {where} (audio aktif="
                + $"{audio.isActiveAndEnabled}, diminta={Read<bool>(audio, "musicRequested")}, isPlaying={music.isPlaying})");
            if (output) Assert.IsTrue(music.isPlaying, "Musik latar berhenti di " + where);
            Assert.AreEqual(director.Settings.Volume01, AudioListener.volume, .0001f, "Volume Settings di " + where);
        }

        // ------------------------------------------------------------------ jumlah objek

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
                Assert.AreEqual(selectables, other.selectables, "TableSelectable bertambah setelah " + label);
            }

            // Pesan gagal menyebut nama yang bertambah/berkurang supaya kebocorannya bisa langsung dicari.
            static void Check(string kind, string[] before, string[] after, string label)
            {
                if (before.Length == after.Length) return;
                var counts = before.GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
                var diff = after.GroupBy(n => n)
                    .Select(g => (name: g.Key, delta: g.Count() - (counts.TryGetValue(g.Key, out int c) ? c : 0)))
                    .Concat(counts.Where(p => !after.Contains(p.Key)).Select(p => (name: p.Key, delta: -p.Value)))
                    .Where(d => d.delta != 0).Take(12).Select(d => $"{d.name} {d.delta:+0;-0}");
                Assert.Fail($"{kind} {before.Length} -> {after.Length} setelah {label}: {string.Join(", ", diff)}");
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

        /// <summary>Render kamera kursi pemain ke RenderTexture (bukan ScreenCapture) lalu simpan PNG 1280x720.</summary>
        IEnumerator Screenshot(string name)
        {
            // Di menu kamera desktop belum dipindah ke kursi; PlayGame memanggil hal yang sama saat game dimulai.
            if (Engine == null) Invoke(director, "PlaceDesktopCamera");
            var camera = Camera.main;
            Assert.IsNotNull(camera, "Kamera kursi pemain (MainCamera) tidak ada.");
            const int width = 1280, height = 720;
            float oldAspect = camera.aspect;
            camera.aspect = width / (float)height;
            yield return null; // LateUpdate menata label untuk rasio screenshot yang sebenarnya.
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
                Assert.Greater(colors, 32, $"Screenshot {name} kosong/seragam ({colors} warna).");
                AssertLabelLayout(camera, name);
            }
            finally { camera.aspect = oldAspect; }
        }

        // Proyeksi mandiri dari delapan sudut bounds lokal renderer, termasuk seluruh latar kartu.
        // Tidak memakai helper layout runtime: tes harus menangkap posisi salah, kartu besar dan efek serentak.
        void AssertLabelLayout(Camera camera, string scenario)
        {
            var seat = director.artRoot.Find("Seats/Seat_0/HeadAnchor");
            Assert.IsNotNull(seat);
            Assert.Less(Vector3.Distance(seat.position, camera.transform.position), .15f, scenario + ": kamera bukan di kursi pemain.");
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
                    Assert.IsTrue(renderer.sharedMaterial.shader.isSupported, renderer.name + " shader tidak didukung.");
                    if (renderer.name == "Kartu")
                    {
                        Assert.AreEqual("BiteOrBetray/TextCard", renderer.sharedMaterial.shader.name);
                        Assert.AreEqual(1, renderer.sharedMaterial.passCount, "Bingkai tidak boleh menambah pass/draw.");
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
                Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(0), scenario + ": " + text.name + " terpotong kiri");
                Assert.That(rect.xMax, Is.LessThanOrEqualTo(1), scenario + ": " + text.name + " terpotong kanan");
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0), scenario + ": " + text.name + " terpotong bawah");
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(1), scenario + ": " + text.name + " terpotong atas");
                visible.Add((text, rect));
            }
            Assert.Greater(visible.Count, 2, scenario + ": audit harus memeriksa beberapa label dari kursi pemain.");
            string report = scenario + ": " + visible.Count + " label";
#if UNITY_EDITOR
            int draws = UnityEditor.UnityStats.drawCalls;
            report += ", draw=" + draws;
            Assert.That(draws, Is.InRange(1, 100), scenario + ": draw render kamera di luar budget 100.");
#endif
            Directory.CreateDirectory("Logs/ci");
            File.AppendAllText("Logs/ci/ui-layout.txt", report + "\n");
            TestContext.WriteLine(report);
            for (int i = 0; i < visible.Count; i++)
                for (int j = i + 1; j < visible.Count; j++)
                    Assert.IsFalse(visible[i].rect.Overlaps(visible[j].rect),
                        $"{scenario}: {visible[i].text.name} {visible[i].rect} bertumpuk dengan {visible[j].text.name} {visible[j].rect}");
        }

        // ------------------------------------------------------------------ util

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Tombol " + label);
        TextMesh Text(string name) => director.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == name);
        static string Label(TableSelectable button) => button.GetComponent<TextMesh>().text;
        string Caption => Read<TextMesh>(director, "caption").text;

        void Select(TableSelectable target)
        {
            Assert.IsNotNull(target);
            Assert.IsTrue(target.gameObject.activeInHierarchy && target.isActiveAndEnabled && target.interactable, target.name + " tidak interaktif");
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
