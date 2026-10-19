using System;
using System.Collections;
using System.Linq;
using System.Reflection;
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
    /// <summary>
    /// Integrasi scene sungguhan tanpa keyboard/headset. Hanya sampel perangkat dan isi dispenser/inventory yang
    /// disiapkan tes; keputusan tetap lewat input VR, TableSelectable dan coroutine director, bukan aksi GameEngine.
    /// Reflection membatasi akses fixture tanpa menambah API debug publik pada game.
    /// </summary>
    public class OfflineMatchTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        CandyTableDirector director;
        VRTableInput input;
        Scene scene;
        float oldTimeScale, oldVolume;
        bool oldRunInBackground;

        GameEngine Engine => Read<GameEngine>(director, "engine");
        TableSelectable Candy => Read<TableSelectable>(director, "candySelectable");
        bool HumanReady => Candy != null && Candy.interactable && Read<BotDecision>(director, "humanChoice") == null;
        PlayerState Human => Engine.Players.Single(p => !p.IsBot);

        [UnitySetUp]
        public IEnumerator LoadTable()
        {
            oldTimeScale = Time.timeScale;
            oldVolume = AudioListener.volume;
            oldRunInBackground = Application.runInBackground;
            Time.timeScale = 1;
            Application.runInBackground = true;
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "Director wajib berasal dari scene gameplay.");
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
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-015 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = oldTimeScale;
            AudioListener.volume = oldVolume;
            Application.runInBackground = oldRunInBackground;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Full_match_via_table_actions_items_grapple_results_replay_and_menu()
        {
            Select(Button("Play vs Bots"));
            SetBotCount(1);
            for (int count = 1; count <= 5; count++)
            {
                AssertRoom(count);
                if (count < 5) Select(Button("More bots"));
            }
            Assert.IsFalse(Button("More bots").interactable);
            for (int i = 0; i < 3; i++)
            {
                Select(Read<TableSelectable>(director, "globalButton"));
                AssertRoom(5);
            }
            var difficulty = (TableSelectable)Read<IDictionary>(director, "difficultyButtons").Values.Cast<object>().First();
            Select(difficulty);
            var expectedSeats = Room.BuildSeats(director.HumanName, director.botNames);
            yield return StartConfiguredMatch();
            AssertSeats(expectedSeats);

            // Ambil dan makan dengan jalur mulut VR, lalu item biasa dari alas pemain.
            int eats = Engine.Stats.EatSelf;
            var hand = Hand(0);
            Grab(hand);
            Assert.IsTrue(input.IsHoldingCandy);
            Assert.AreSame(Candy.gameObject, Read<GameObject>(director, "grabbedCandy"));
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "makan manusia");
            yield return Until(() => HumanReady, "giliran berlanjut setelah good");
            Assert.IsFalse(input.IsHoldingCandy);

            foreach (var type in new[] { ItemType.PlasticFork, ItemType.HammerCrush, ItemType.HotMilk,
                         ItemType.MagicGloves, ItemType.Cupcake, ItemType.CloverCoin })
            {
                if (type == ItemType.HotMilk) Engine.DebugSetHp(Human, director.rules.MaxHp - 2);
                int hp = Human.Hp;
                var before = Engine.ActiveCandy;
                yield return UseItem(type);
                switch (type)
                {
                    case ItemType.PlasticFork: Assert.IsTrue(Engine.ActiveCandy.Revealed); break;
                    case ItemType.HammerCrush:
                    case ItemType.MagicGloves: Assert.AreNotSame(before, Engine.ActiveCandy); break;
                    case ItemType.HotMilk: Assert.AreEqual(hp + 1, Human.Hp); break;
                    case ItemType.Cupcake: Assert.IsTrue(Human.HasCupcake); break;
                    case ItemType.CloverCoin: Assert.IsTrue(Engine.CloverOnActive); break;
                }
            }

            // Target tersedia sebelum membuka Pocket; Grappling dari Pocket juga harus bisa diselesaikan.
            var victim = Engine.Players.First(p => p.IsBot);
            Engine.DebugGiveItem(victim, ItemType.HotMilk);
            RefreshProps();
            yield return UseItem(ItemType.LittlePocket);
            Assert.AreEqual(GamePhase.AwaitingPocketChoice, Engine.Phase);
            Assert.AreEqual(3, Engine.PocketOptions.Count);
            yield return Until(() => Props.Any(p => Read<bool>(p, "pocket") && PropButton(p).interactable), "opsi Pocket");
            var options = Props.Where(p => Read<bool>(p, "pocket")).ToArray();
            var option = options.FirstOrDefault(p => Read<ItemType>(p, "type") != ItemType.GrapplingHook) ?? options[0];
            Select(PropButton(option));
            if (Read<ItemType>(option, "type") == ItemType.GrapplingHook) Select(FindProp(victim, ItemType.HotMilk));
            yield return Until(() => Engine.Phase == GamePhase.AwaitingAction && HumanReady, "pilihan Pocket dipakai");
            Assert.IsEmpty(Engine.PocketOptions);
            yield return UseItem(ItemType.LittlePocket);
            yield return Until(() => Button("Discard").interactable, "buang Pocket");
            Select(Button("Discard"));
            yield return Until(() => HumanReady, "Pocket dibuang");
            Assert.AreEqual(1, Engine.Stats.PocketDiscarded);

            Engine.DebugGiveItem(Human, ItemType.GrapplingHook);
            if (victim.Items.Count == 0) Engine.DebugGiveItem(victim, ItemType.HotMilk);
            RefreshProps();
            var hook = FindProp(Human, ItemType.GrapplingHook);
            int hooks = Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook];
            Select(hook);
            Assert.IsTrue(Button("Cancel").interactable);
            Assert.IsFalse(Candy.interactable);
            Select(Button("Cancel"));
            Assert.IsTrue(Candy.interactable);
            Assert.AreEqual(hooks, Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook], "Batal tidak menghabiskan item.");
            Engine.DebugSetHp(Human, 1);
            Select(hook);
            Select(FindProp(victim, ItemType.HotMilk));
            yield return Until(() => Engine.Stats.ItemsUsed[(int)ItemType.GrapplingHook] == hooks + 1, "Grappling manusia");
            yield return Until(() => HumanReady, "animasi Grappling selesai");
            Assert.IsEmpty(victim.Items);
            Assert.AreEqual(2, Human.Hp, "Item curian langsung dipakai oleh manusia.");
            foreach (var type in ItemInfo.All) Assert.Greater(Engine.Stats.ItemsUsed[(int)type], 0, type.ToString());

            // Lepas permen ke target bot: callback Give yang sama dengan controller VR.
            int gives = Engine.Stats.Gives;
            Grab(hand);
            Write(hand, "hovered", BotTargets.First(t => t.interactable));
            Write(input, "actedThisFrame", false);
            Invoke(input, "ReleaseCandy", hand);
            yield return Until(() => Engine.Stats.Gives == gives + 1, "Give manusia");
            Assert.IsFalse(input.IsHoldingCandy);
            SetPool(false);
            yield return FinishMatch();
            AssertResults();
            Assert.IsTrue(Engine.LogLines.Any(line => Engine.Players.Any(p => p.IsBot
                && (line.StartsWith(p.Name + " eats") || line.StartsWith(p.Name + " feeds")))), "Bots must also act.");

            int selectables = -1, children = -1;
            // Satu replay dari tombol akhir, empat lagi lewat API Restart yang juga dipakai tombol tersebut.
            for (int replay = 0; replay < 5; replay++)
            {
                var previous = Engine;
                director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
                if (replay == 0) Select(Button("Play again"));
                else director.Restart();
                yield return Until(() => Engine != null && Engine != previous, "engine replay baru");
                SetPool(true);
                yield return Until(() => HumanReady, "replay siap");
                yield return new WaitForSeconds(5); // popup d20/ronde sudah habis sebelum menghitung objek tetap
                AssertSeats(expectedSeats);
                Assert.IsFalse(Text("Final Results").gameObject.activeSelf);
                Assert.IsFalse(Text("How to Play").gameObject.activeSelf, "Onboarding appears only once per session.");
                int currentSelectables = director.GetComponentsInChildren<TableSelectable>(true).Length;
                int currentChildren = director.transform.childCount;
                TestContext.WriteLine($"Replay {replay + 1}: {currentChildren} anak, {currentSelectables} selectable, {Props.Count()} prop");
                if (replay > 0)
                {
                    Assert.AreEqual(selectables, currentSelectables, "Restart menggandakan selectable.");
                    Assert.AreEqual(children, currentChildren, "Restart menggandakan objek.");
                }
                selectables = currentSelectables;
                children = currentChildren;
                Assert.IsEmpty(Props);
            }
            Select(Candy);
            yield return Until(() => Engine.Stats.EatSelf == 1, "satu aksi setelah lima restart");
            SetPool(false);
            yield return FinishMatch();
            AssertResults();
            int menuRequests = 0;
            director.MenuRequested += () => menuRequests++;
            Select(Button("Back to menu"));
            yield return null;
            Assert.AreEqual(1, menuRequests);
            Assert.IsNull(Engine);
            Assert.IsTrue(Button("Play vs Bots").interactable);
            Assert.IsFalse(Text("Final Results").gameObject.activeSelf);
            Assert.IsNull(Candy);
            Assert.IsEmpty(Props);
        }

        [UnityTest]
        public IEnumerator Bot_counts_one_to_five_start_with_selected_seats_and_difficulties()
        {
            for (int count = 1; count <= 5; count++)
            {
                Select(Button("Play vs Bots"));
                SetBotCount(count);
                Select(Read<TableSelectable>(director, "globalButton"));
                var difficulty = Room.GlobalDifficulty;
                Assert.IsTrue(difficulty.HasValue);
                AssertRoom(count);
                var expected = Room.BuildSeats(director.HumanName, director.botNames);
                yield return StartConfiguredMatch();
                AssertSeats(expected);
                Assert.AreEqual(count, Engine.Players.Count(p => p.IsBot));
                Assert.IsTrue(Engine.Players.Where(p => p.IsBot).All(p => p.Difficulty == difficulty));
                director.ReturnToMenu();
                yield return null;
                Assert.IsNull(Engine);
            }
        }

        [UnityTest]
        public IEnumerator Lost_application_focus_freezes_turn_timer_and_returns_held_candy()
        {
            yield return CheckFocus("OnApplicationFocus", false, true);
        }

        [UnityTest]
        public IEnumerator Application_pause_freezes_turn_timer_and_returns_held_candy()
        {
            yield return CheckFocus("OnApplicationPause", true, false);
        }

        IEnumerator CheckFocus(string callback, bool lost, bool restored)
        {
            director.rules.TurnTimeLimitSeconds = 60;
            yield return StartMatch(2);
            Time.timeScale = 1;
            var hand = Hand(0);
            Grab(hand);
            int eats = Engine.Stats.EatSelf, gives = Engine.Stats.Gives;
            // Update berhenti sebelum polling perangkat ketika fokus hilang.
            Write(input, "active", true);
            input.SendMessage(callback, lost, SendMessageOptions.RequireReceiver);
            Invoke(input, "Update");
            Assert.IsFalse(input.HasFocus);
            Assert.IsFalse(input.IsHoldingCandy);
            float timer = Read<float>(director, "turnTimer");
            float thinking = Read<float>(director, "decisionThink");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(timer, Read<float>(director, "turnTimer"), .0001f);
            Assert.AreEqual(thinking, Read<float>(director, "decisionThink"), .0001f);
            Assert.AreEqual(eats, Engine.Stats.EatSelf);
            Assert.AreEqual(gives, Engine.Stats.Gives);
            Assert.IsTrue(Candy.interactable);
            input.SendMessage(callback, restored, SendMessageOptions.RequireReceiver);
            Assert.IsFalse(input.HasFocus, "Frame resume tidak boleh menghitung delta pause.");
            yield return Until(() => input.HasFocus, "fokus kembali");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.Less(Read<float>(director, "turnTimer"), timer);
            Assert.Greater(Read<float>(director, "turnTimer"), timer - 2);
            Sample(hand, true, false, false);
            Grab(hand);
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "input pulih setelah fokus");
        }

        [UnityTest]
        public IEnumerator Tracking_loss_with_trigger_held_recovers_without_accidental_give()
        {
            yield return CheckTrackingLoss(false);
        }

        [UnityTest]
        public IEnumerator Tracking_loss_with_grip_held_recovers_without_accidental_give()
        {
            yield return CheckTrackingLoss(true);
        }

        IEnumerator CheckTrackingLoss(bool withGrip)
        {
            yield return StartMatch(2);
            var hand = Hand(0);
            Grab(hand, withGrip);
            int eats = Engine.Stats.EatSelf, gives = Engine.Stats.Gives;
            Vector3 home = Read<Vector3>(input, "candyHome");
            Candy.transform.position += Vector3.up * .3f;
            Write(hand, "hovered", BotTargets.First(t => t.interactable));
            Sample(hand, false, !withGrip, withGrip);
            Invoke(input, "ProcessHeldCandy", false);
            Assert.AreEqual(eats, Engine.Stats.EatSelf);
            Assert.AreEqual(gives, Engine.Stats.Gives);
            Sample(hand, false, false, false);
            Assert.IsFalse(input.IsHoldingCandy, "Pelepasan saat tracking hilang harus membebaskan pegangan.");
            yield return new WaitForSeconds(.3f);
            Assert.Less(Vector3.Distance(home, Candy.transform.position), .001f);
            // Pulih tanpa tombol, ambil lagi, lalu makan: tidak ada status tombol/holder yang macet.
            Sample(hand, true, false, false);
            Grab(hand, withGrip);
            Assert.IsTrue(input.IsHoldingCandy);
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, "makan setelah tracking pulih");
            Assert.AreEqual(gives, Engine.Stats.Gives);
            Assert.IsFalse(input.IsHoldingCandy);
        }

        [UnityTest]
        public IEnumerator Eliminated_human_is_locked_transparent_spectator_until_winner_and_replay()
        {
            yield return StartMatch(2, false);
            var human = Human;
            Engine.DebugSetHp(human, 1);
            var avatar = director.artRoot.Find("Seats/Seat_0").GetComponentInChildren<SeatedAvatar>(true);
            Assert.IsNotNull(avatar, "Avatar manusia wajib ada di scene.");
            var renderers = avatar.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(renderers);
            var originals = renderers.Select(r => r.sharedMaterials).ToArray();
            var camera = Camera.main;
            Assert.IsNotNull(camera);
            Vector3 cameraPosition = camera.transform.position;
            var staleCandy = Candy.onSelect;
            Engine.DebugGiveItem(human, ItemType.HotMilk);
            RefreshProps();
            var staleItem = FindProp(human, ItemType.HotMilk).onSelect;
            var staleGive = BotTargets.First(t => t.interactable).onSelect;
            Grab(Hand(0));
            EatAtMouth();
            yield return Until(() => director.SpectatorInputLocked, "manusia KO");
            yield return Until(() => renderers.All(r => r.sharedMaterials.All(m => m.name.EndsWith(" Spectator"))), "avatar KO transparan");
            Assert.IsTrue(human.Eliminated);
            Assert.IsFalse(input.IsHoldingCandy);
            Assert.IsTrue(camera.enabled && camera.gameObject.activeInHierarchy);
            Assert.AreEqual(cameraPosition, camera.transform.position, "Kamera spectator tetap di kursi.");
            for (int i = 0; i < renderers.Length; i++)
            {
                var materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    Assert.AreNotSame(originals[i][j], materials[j]);
                    Assert.AreEqual(originals[i][j].GetColor("_BaseColor").a * .3f, materials[j].GetColor("_BaseColor").a, .001f);
                    Assert.AreEqual(1, materials[j].GetFloat("_Surface"));
                    Assert.AreEqual((int)RenderQueue.Transparent, materials[j].renderQueue);
                }
                Assert.AreEqual(ShadowCastingMode.Off, renderers[i].shadowCastingMode);
            }
            Assert.IsFalse(Candy != null && Candy.interactable);
            Assert.IsFalse(BotTargets.Any(t => t.interactable));
            Assert.IsFalse(Props.Any(p => PropButton(p).interactable));
            Assert.IsFalse(Button("Play again").interactable);
            var choice = Read<BotDecision>(director, "humanChoice");
            staleCandy();
            staleGive();
            staleItem();
            director.Cancel();
            Assert.AreSame(choice, Read<BotDecision>(director, "humanChoice"), "Callback lama juga tidak boleh memberi aksi spectator.");
            int turn = Engine.TurnNumber;
            yield return FinishMatch();
            Assert.Greater(Engine.TurnNumber, turn, "Bot melanjutkan pertandingan setelah manusia KO.");
            Assert.AreNotSame(human, Engine.Winner);
            AssertResults();
            foreach (var slot in Room.ActiveSlots)
            {
                var character = Read<Component>(Read<Array>(director, "botSlots").GetValue(slot), "character");
                var player = Engine.Players.Single(p => p.ConfigIndex == Room.ActiveSlots.ToList().IndexOf(slot) + 1);
                var materials = character.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
                Assert.IsNotEmpty(materials);
                Assert.AreEqual(player.Eliminated, materials.All(m => m.name.EndsWith(" Spectator")), "Tampilan bot KO " + player.Name);
            }
            var previous = Engine;
            Select(Button("Play again"));
            yield return Until(() => Engine != null && Engine != previous, "replay spectator");
            Assert.IsFalse(director.SpectatorInputLocked);
            Assert.IsFalse(Human.Eliminated);
            for (int i = 0; i < renderers.Length; i++) CollectionAssert.AreEqual(originals[i], renderers[i].sharedMaterials);
        }

        RoomSetup Room => Read<RoomSetup>(director, "room");
        System.Collections.Generic.IEnumerable<object> Props => Read<IList>(director, "props").Cast<object>();
        System.Collections.Generic.IEnumerable<TableSelectable> BotTargets =>
            Read<IDictionary>(director, "botTargets").Values.Cast<TableSelectable>();

        IEnumerator StartMatch(int bots, bool good = true)
        {
            Select(Button("Play vs Bots"));
            SetBotCount(bots);
            yield return StartConfiguredMatch(good);
        }

        IEnumerator StartConfiguredMatch(bool good = true)
        {
            director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
            bool onboardingDone = Read<bool>(director, "onboardingDone");
            Select(Button("Start"));
            if (!onboardingDone)
            {
                yield return Until(() => Button("Skip").interactable, "onboarding sebelum pertandingan");
                Assert.IsTrue(Text("How to Play").gameObject.activeSelf);
                Assert.IsNull(Engine);
                Select(Button("Skip"));
            }
            yield return Until(() => Engine != null, "game setelah Lewati");
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

        void SetPool(bool good) => Engine.DebugSetPool(Enumerable.Range(0, 64)
            .Select(_ => new Candy(good ? CandyColor.Yellow : CandyColor.Blue, good)));

        void SetBotCount(int count)
        {
            Assert.That(count, Is.InRange(1, Room.MaxBots));
            while (Room.BotCount > count) Select(Button("Fewer bots"));
            while (Room.BotCount < count) Select(Button("More bots"));
        }

        void AssertRoom(int count)
        {
            Assert.AreEqual(count, Room.BotCount);
            Assert.AreEqual($"<b>{Strings.BotCount.Format(count)}</b>", Text("Bot Count").text);
            var slots = Read<Array>(director, "botSlots");
            for (int i = 0; i < slots.Length; i++)
                Assert.AreEqual(Room.IsActive(i), Read<Component>(slots.GetValue(i), "character").gameObject.activeSelf, "slot " + i);
        }

        void AssertSeats(System.Collections.Generic.IList<SeatConfig> expected)
        {
            Assert.AreEqual(expected.Count, Engine.Players.Count);
            foreach (var player in Engine.Players)
            {
                var config = expected[player.ConfigIndex];
                Assert.AreEqual(config.Name, player.Name);
                Assert.AreEqual(config.IsBot, player.IsBot);
                Assert.AreEqual(config.Difficulty, player.Difficulty);
            }
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

        void RefreshProps() => Invoke(director, "RefreshProps");

        TableSelectable FindProp(PlayerState owner, ItemType type) => PropButton(Props.Single(p =>
            !Read<bool>(p, "pocket") && Read<ItemType>(p, "type") == type
            && Read<PlayerState>(Read<object>(p, "seat"), "player") == owner));

        static TableSelectable PropButton(object prop) => Read<TableSelectable>(prop, "selectable");
        object Hand(int index) => Read<Array>(input, "hands").GetValue(index);

        void Grab(object hand, bool withGrip = false)
        {
            var pose = Read<Transform>(hand, "pose");
            if (pose == null)
            {
                pose = new GameObject("BOB-015 Hand").transform;
                pose.SetParent(director.transform);
            }
            var bounds = Candy.GetComponent<Collider>().bounds;
            pose.SetPositionAndRotation(bounds.center + Vector3.up * (bounds.extents.y + .1f), Quaternion.LookRotation(Vector3.down));
            Write(hand, "pose", pose);
            Physics.SyncTransforms();
            Assert.AreSame(Candy, TableSelectable.Raycast(new Ray(pose.position, pose.forward), input.rayLength, out _), "Ray tangan harus mengenai permen.");
            Sample(hand, true, false, false);
            Sample(hand, true, !withGrip, withGrip);
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
        }

        IEnumerator FinishMatch()
        {
            float deadline = Time.realtimeSinceStartup + 90;
            while (!Text("Final Results").gameObject.activeSelf)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Pertandingan macet: " + Engine.Phase + ", giliran " + Engine.TurnNumber);
                if (HumanReady) Select(Candy);
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

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Button " + label);
        TextMesh Text(string name) => director.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == name);

        static void Select(TableSelectable target)
        {
            Assert.IsNotNull(target);
            Assert.IsTrue(target.gameObject.activeInHierarchy && target.isActiveAndEnabled && target.interactable, target.name + " tidak interaktif");
            target.Select();
        }

        IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, message);
                yield return null;
            }
        }

        static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
        static void Write(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    }
}
