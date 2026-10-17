using System.Collections.Generic;
using System.Linq;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    /// <summary>Unit test untuk aturan dan edge case di GDD. Jalankan lewat Window > General > Test Runner > EditMode.</summary>
    public class RulesTests
    {
        static GameEngine NewGame(int playerCount = 2, RuleConfig cfg = null)
        {
            cfg = cfg ?? new RuleConfig { RandomDraw = false, ItemsPerRound = 0 };
            var g = new GameEngine(cfg, 1234);
            var seats = new List<SeatConfig>();
            for (int i = 0; i < playerCount; i++) seats.Add(new SeatConfig("P" + (i + 1)));
            g.Start(seats);
            return g;
        }

        static Candy C(CandyColor color, bool good) => new Candy(color, good);
        static Candy YG => C(CandyColor.Yellow, true);
        static Candy YB => C(CandyColor.Yellow, false);

        static PlayerState Seat(GameEngine g, int i) => g.Players[i];

        static ActionResult UseFirst(GameEngine g, ItemType t, ItemArgs args = null) =>
            g.UseItem(g.Current.Items.ToList().IndexOf(t), args);

        /// <summary>Buka Little Pocket berulang sampai isinya memuat item tertentu. Return indeks pilihan itu.</summary>
        static int OpenPocketWith(GameEngine g, ItemType want)
        {
            for (int i = 0; i < 200; i++)
            {
                g.DebugGiveItem(g.Current, ItemType.LittlePocket);
                UseFirst(g, ItemType.LittlePocket);
                int idx = g.PocketOptions.ToList().IndexOf(want);
                if (idx >= 0) return idx;
                g.DiscardPocket();
            }
            Assert.Fail($"Little Pocket tidak pernah berisi {want}");
            return -1;
        }

        static RuleConfig ForcedEatCfg() =>
            new RuleConfig { RandomDraw = false, ItemsPerRound = 0, TimeoutAction = TimeoutRule.ForcedEat };

        // ---------------------------------------------------------- setup

        [Test]
        public void TurnOrder_D20RollsAreUniqueAndSortedDescending()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var g = new GameEngine(new RuleConfig(), seed);
                g.Start(Enumerable.Range(0, 6).Select(i => new SeatConfig("P" + i)).ToList());
                var rolls = g.Players.Select(p => p.D20Roll).ToList();
                Assert.AreEqual(rolls.Count, rolls.Distinct().Count());
                CollectionAssert.AreEqual(rolls.OrderByDescending(r => r).ToList(), rolls);
                Assert.AreEqual(0, g.CurrentSeat);
            }
        }

        [Test]
        public void Start_ConfigIndexPointsBackToSeatConfig()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var g = new GameEngine(new RuleConfig(), seed);
                var seats = Enumerable.Range(0, 5).Select(i => new SeatConfig("Sama", i > 0, (BotDifficulty)(i % 3))).ToList();
                g.Start(seats);
                CollectionAssert.AreEquivalent(Enumerable.Range(0, 5), g.Players.Select(p => p.ConfigIndex));
                foreach (var p in g.Players)
                {
                    Assert.AreEqual(seats[p.ConfigIndex].IsBot, p.IsBot);
                    Assert.AreEqual(seats[p.ConfigIndex].Difficulty, p.Difficulty);
                }
            }
        }

        [Test]
        public void Start_EveryoneStartsAt4Hp_Max5()
        {
            var g = NewGame(4);
            Assert.IsTrue(g.Players.All(p => p.Hp == 4));
            Assert.AreEqual(5, g.Config.MaxHp);
        }

        // ---------------------------------------------------------- validasi RuleConfig

        [Test]
        public void RuleConfig_DefaultAndBoundaryValuesAreValid()
        {
            Assert.IsTrue(new RuleConfig().Validate().Success);
            var edge = new RuleConfig
            {
                StartHp = 1, MaxHp = 1, MaxItems = 0, ItemsPerRound = 0, CandiesPerAlivePlayer = 1,
                MinCandies = 2, MaxCandies = 2, MinBadRatio = 0f, MaxBadRatio = 0f, BadRatioPerExtraSeat = 0f,
                TurnTimeLimitSeconds = 0, TimeoutAction = TimeoutRule.ForcedEat
            };
            Assert.IsTrue(edge.Validate().Success, edge.Validate().Error);
            var full = new RuleConfig { StartHp = 5, MaxHp = 5, MinBadRatio = 1f, MaxBadRatio = 1f, BadRatioPerExtraSeat = 1f };
            Assert.IsTrue(full.Validate().Success, full.Validate().Error);
        }

        [Test]
        public void RuleConfig_RejectsInvalidValues_WithFieldNameInMessage()
        {
            var cases = new (string field, System.Action<RuleConfig> breakIt)[]
            {
                ("MaxHp", c => c.MaxHp = 0),
                ("StartHp", c => c.StartHp = 0),
                ("StartHp", c => c.StartHp = 6),
                ("MaxItems", c => c.MaxItems = -1),
                ("MaxItems", c => c.MaxItems = 0),
                ("ItemsPerRound", c => c.ItemsPerRound = -1),
                ("CandiesPerAlivePlayer", c => c.CandiesPerAlivePlayer = 0),
                ("MinCandies", c => c.MinCandies = 1),
                ("MaxCandies", c => c.MaxCandies = 3),
                ("MinBadRatio", c => c.MinBadRatio = -0.1f),
                ("MaxBadRatio", c => c.MaxBadRatio = 1.5f),
                ("MaxBadRatio", c => c.MaxBadRatio = float.NaN),
                ("MinBadRatio", c => c.MinBadRatio = 0.6f),
                ("BadRatioPerExtraSeat", c => c.BadRatioPerExtraSeat = -0.04f),
                ("TurnTimeLimitSeconds", c => c.TurnTimeLimitSeconds = -1),
                ("TimeoutAction", c => c.TimeoutAction = (TimeoutRule)99),
            };
            foreach (var (field, breakIt) in cases)
            {
                var cfg = new RuleConfig();
                breakIt(cfg);
                var r = cfg.Validate();
                Assert.IsFalse(r.Success, $"{field} seharusnya ditolak");
                StringAssert.Contains(field, r.Error);
            }
        }

        [Test]
        public void RuleConfig_ReportsAllProblemsAtOnce()
        {
            var r = new RuleConfig { StartHp = 9, MinCandies = 0, MaxBadRatio = 2f }.Validate();
            Assert.IsFalse(r.Success);
            StringAssert.StartsWith("RuleConfig tidak valid:", r.Error);
            StringAssert.Contains("StartHp (9) tidak boleh lebih dari MaxHp (5)", r.Error);
            StringAssert.Contains("MinCandies", r.Error);
            StringAssert.Contains("MaxBadRatio", r.Error);
        }

        [Test]
        public void Start_RejectsInvalidConfig_BeforeDealing()
        {
            var g = new GameEngine(new RuleConfig { StartHp = 6, MaxHp = 5 }, 1);
            var ex = Assert.Throws<System.ArgumentException>(() => g.Start(new[] { new SeatConfig("A"), new SeatConfig("B") }));
            StringAssert.Contains("StartHp", ex.Message);
            Assert.AreEqual(GamePhase.NotStarted, g.Phase);
            Assert.AreEqual(0, g.Players.Count);
            Assert.AreEqual(0, g.RoundNumber);
        }

        // ---------------------------------------------------------- eat / give

        [Test]
        public void EatGood_HealsUpToMax_AndTurnContinues()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { C(CandyColor.Blue, true), YG, YG });
            g.DebugSetHp(Seat(g, 0), 5);
            g.Eat();
            Assert.AreEqual(5, Seat(g, 0).Hp, "HP tidak boleh lebih dari HP maks (5)");
            Assert.AreEqual(0, g.CurrentSeat, "makan good = giliran lanjut");
            Assert.IsNotNull(g.ActiveCandy);
        }

        [Test]
        public void EatBad_Damages_AndTurnEnds()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { C(CandyColor.Blue, false), YG, YG });
            g.Eat();
            Assert.AreEqual(2, Seat(g, 0).Hp, "biru bad: 4 - 2");
            Assert.AreEqual(1, g.CurrentSeat);
        }

        [Test]
        public void Give_AppliesToTarget_AndAlwaysEndsTurn()
        {
            var g = NewGame(3);
            g.DebugSetPool(new[] { C(CandyColor.Blue, true), YG, YG });
            g.DebugSetHp(Seat(g, 2), 3);
            g.Give(2);
            Assert.AreEqual(4, Seat(g, 2).Hp, "biru good: +1");
            Assert.AreEqual(1, g.CurrentSeat, "give good tetap mengakhiri giliran");
        }

        [Test]
        public void Give_ToSelf_IsRejected()
        {
            var g = NewGame();
            Assert.IsFalse(g.Give(0).Success);
        }

        [Test]
        public void EatGoodOnLastCandy_ContinuesInNewRound()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YG });
            g.DebugSetHp(Seat(g, 0), 5);
            int round = g.RoundNumber;
            g.Eat();
            Assert.AreEqual(round + 1, g.RoundNumber);
            Assert.AreEqual(0, g.CurrentSeat);
            Assert.IsNotNull(g.ActiveCandy);
        }

        // ---------------------------------------------------------- status

        [Test]
        public void RedBad_Paralyzes_AndTurnIsSkippedOnceThenCleared()
        {
            var g = NewGame(3);
            g.DebugSetPool(new[] { C(CandyColor.Red, false), YG, YG, YG });
            g.Give(1);
            Assert.AreEqual(3, Seat(g, 1).Hp, "merah bad: -1");
            Assert.AreEqual(2, g.CurrentSeat, "P2 paralyzed, giliran langsung ke P3");
            Assert.IsFalse(Seat(g, 1).Paralyzed, "paralyzed hilang setelah giliran dilewati");
        }

        [Test]
        public void Paralyzed_DoesNotStack()
        {
            var g = NewGame(3);
            g.DebugSetPool(new[] { C(CandyColor.Red, false), C(CandyColor.Red, false), YG, YG, YG, YG });
            g.Give(2); // P1 -> P3 paralyzed
            g.Give(2); // P2 -> P3 lagi, tidak stack
            Assert.AreEqual(0, g.CurrentSeat, "P3 dilewati sekali");
            g.Give(1); // P1
            g.Give(0); // P2
            Assert.AreEqual(2, g.CurrentSeat, "P3 tidak dilewati dua kali");
        }

        [Test]
        public void Cupcake_EatBad_NoEffect_TurnContinues_CupcakeConsumed()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { C(CandyColor.Blue, false), YG, YG });
            g.DebugGiveItem(Seat(g, 0), ItemType.Cupcake);
            UseFirst(g, ItemType.Cupcake);
            Assert.IsTrue(Seat(g, 0).HasCupcake);
            g.Eat();
            Assert.AreEqual(4, Seat(g, 0).Hp);
            Assert.AreEqual(0, g.CurrentSeat, "dengan cupcake, makan bad tetap lanjut");
            Assert.IsFalse(Seat(g, 0).HasCupcake);
        }

        [Test]
        public void Cupcake_BlocksGoodEffectToo()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { C(CandyColor.Blue, true), YG });
            g.DebugSetHp(Seat(g, 0), 3);
            g.DebugSetCupcake(Seat(g, 0), true);
            g.Eat();
            Assert.AreEqual(3, Seat(g, 0).Hp);
        }

        [Test]
        public void CloverOnGiveToCupcakeHolder_NoEffect_CupcakeGone_TurnEnds()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { C(CandyColor.Blue, false), YG, YG });
            g.DebugSetCupcake(Seat(g, 1), true);
            g.DebugGiveItem(Seat(g, 0), ItemType.CloverCoin);
            UseFirst(g, ItemType.CloverCoin);
            g.Give(1);
            Assert.AreEqual(4, Seat(g, 1).Hp);
            Assert.IsFalse(Seat(g, 1).HasCupcake);
            Assert.AreEqual(1, g.CurrentSeat);
        }

        [Test]
        public void Clover_DoublesRedBad_ParalyzedOnlyOnce()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { C(CandyColor.Red, false), YG, YG });
            g.DebugGiveItem(Seat(g, 0), ItemType.CloverCoin);
            UseFirst(g, ItemType.CloverCoin);
            Assert.IsTrue(g.CloverOnActive);
            g.Give(1);
            Assert.AreEqual(2, Seat(g, 1).Hp, "merah bad x2 = -2");
            Assert.AreEqual(0, g.CurrentSeat, "P2 paralyzed (tidak dobel), giliran P2 dilewati sekali");
            Assert.IsFalse(g.CloverOnActive, "clover habis setelah permennya dimakan");
        }

        [Test]
        public void ForkCloverGive_BlueBad_DamageCappedAt3_NoOneTurnKill()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { C(CandyColor.Blue, false), YG, YG });
            g.DebugGiveItem(Seat(g, 0), ItemType.PlasticFork);
            g.DebugGiveItem(Seat(g, 0), ItemType.CloverCoin);
            UseFirst(g, ItemType.PlasticFork);
            UseFirst(g, ItemType.CloverCoin);
            g.Give(1);
            Assert.AreEqual(1, Seat(g, 1).Hp, "biru bad x2 = 4, dibatasi 3: HP 4 -> 1");
            Assert.IsTrue(Seat(g, 1).IsAlive, "tidak ada one-turn kill dari HP awal");
        }

        [Test]
        public void DamageCap_StillKillsLowHpTarget()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { C(CandyColor.Blue, false), YG, YG });
            g.DebugSetHp(Seat(g, 1), 3);
            g.DebugGiveItem(Seat(g, 0), ItemType.CloverCoin);
            UseFirst(g, ItemType.CloverCoin);
            g.Give(1);
            Assert.IsTrue(Seat(g, 1).Eliminated, "HP 3 - 3 = 0, tetap KO");
            Assert.IsTrue(g.IsOver);
        }

        [Test]
        public void Clover_DoublesGood_CappedAtMax()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { C(CandyColor.Blue, true), YG });
            g.DebugSetHp(Seat(g, 0), 4);
            g.DebugGiveItem(Seat(g, 0), ItemType.CloverCoin);
            UseFirst(g, ItemType.CloverCoin);
            g.Eat();
            Assert.AreEqual(5, Seat(g, 0).Hp, "biru good x2 = +2, tapi maks 5");
        }

        [Test]
        public void Clover_DoesNotStack()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YB, YG });
            g.DebugGiveItem(Seat(g, 0), ItemType.CloverCoin);
            g.DebugGiveItem(Seat(g, 0), ItemType.CloverCoin);
            UseFirst(g, ItemType.CloverCoin);
            UseFirst(g, ItemType.CloverCoin);
            g.Give(1);
            Assert.AreEqual(2, Seat(g, 1).Hp, "maksimal x2, bukan x4");
        }

        [Test]
        public void Clover_MovesToNewCandy_WhenHammered()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YG, C(CandyColor.Blue, false), YG });
            g.DebugGiveItem(Seat(g, 0), ItemType.CloverCoin);
            g.DebugGiveItem(Seat(g, 0), ItemType.HammerCrush);
            UseFirst(g, ItemType.CloverCoin);
            UseFirst(g, ItemType.HammerCrush);
            Assert.IsTrue(g.CloverOnActive);
            g.DebugSetHp(Seat(g, 1), 5);
            g.Give(1);
            Assert.AreEqual(2, Seat(g, 1).Hp, "biru bad x2 = -4, dibatasi -3");
        }

        [Test]
        public void Clover_CarriesIntoNewRound_WhenLastCandyHammered()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YG });
            g.DebugGiveItem(Seat(g, 0), ItemType.CloverCoin);
            g.DebugGiveItem(Seat(g, 0), ItemType.HammerCrush);
            int round = g.RoundNumber;
            UseFirst(g, ItemType.CloverCoin);
            UseFirst(g, ItemType.HammerCrush);
            Assert.AreEqual(round + 1, g.RoundNumber, "permen terakhir hancur = ronde baru");
            Assert.AreEqual(0, g.CurrentSeat, "giliran tetap lanjut");
            Assert.IsTrue(g.CloverOnActive, "clover pindah ke permen pertama ronde baru");
            Assert.IsNotNull(g.ActiveCandy);
        }

        // ---------------------------------------------------------- item

        [Test]
        public void Fork_RevealsWithoutDestroying()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YB, YG });
            var c = g.ActiveCandy;
            g.DebugGiveItem(Seat(g, 0), ItemType.PlasticFork);
            UseFirst(g, ItemType.PlasticFork);
            Assert.AreSame(c, g.ActiveCandy);
            Assert.IsTrue(c.Revealed);
            Assert.AreEqual(0, g.CurrentSeat);
        }

        [Test]
        public void Hammer_DestroysAndDispensesNew_TurnContinues()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YB, YG });
            var c = g.ActiveCandy;
            g.DebugGiveItem(Seat(g, 0), ItemType.HammerCrush);
            UseFirst(g, ItemType.HammerCrush);
            Assert.AreNotSame(c, g.ActiveCandy);
            Assert.AreEqual(0, g.CandiesInDispenser);
            Assert.AreEqual(1, g.GoodLeft);
            Assert.AreEqual(0, g.BadLeft);
        }

        [Test]
        public void Gloves_SwapsCandy_OldGoesBackToDispenser()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YG, YB });
            var a = g.ActiveCandy;
            g.DebugGiveItem(Seat(g, 0), ItemType.MagicGloves);
            UseFirst(g, ItemType.MagicGloves);
            Assert.AreNotSame(a, g.ActiveCandy);
            Assert.Contains(a, g.DebugPool.ToList());
        }

        [Test]
        public void Gloves_OnLastCandy_ReturnsSameCandy()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YG });
            var a = g.ActiveCandy;
            g.DebugGiveItem(Seat(g, 0), ItemType.MagicGloves);
            Assert.IsTrue(UseFirst(g, ItemType.MagicGloves).Success);
            Assert.AreSame(a, g.ActiveCandy);
        }

        // Mendokumentasikan perilaku SAAT INI, bukan keputusan final: permen yang sudah ditusuk Fork lalu ditukar Gloves
        // kembali ke dispenser dalam keadaan tersembunyi lagi (Revealed = false). Pertanyaan desain untuk owner dicatat
        // di BOB-016 (tasks.json): tetap tersembunyi, atau tetap terbuka saat keluar lagi? Ubah tes ini kalau aturannya diubah.
        [Test]
        public void GlovesAfterFork_ForkedCandyStaysRevealed()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YB, YG });
            var forked = g.ActiveCandy;
            g.DebugGiveItem(Seat(g, 0), ItemType.PlasticFork);
            g.DebugGiveItem(Seat(g, 0), ItemType.MagicGloves);
            UseFirst(g, ItemType.PlasticFork);
            Assert.IsTrue(forked.Revealed);

            UseFirst(g, ItemType.MagicGloves);
            Assert.AreNotSame(forked, g.ActiveCandy);
            Assert.IsFalse(g.ActiveCandy.Revealed, "permen baru dari Gloves tidak terungkap");
            Assert.Contains(forked, g.DebugPool.ToList());
            Assert.IsTrue(forked.Revealed, "keputusan owner: isi yang sudah terlihat tetap terbuka di dispenser");
            Assert.AreEqual(1, g.GoodLeft, "sisa good/bad tidak berubah karena tidak ada permen yang keluar dari permainan");
            Assert.AreEqual(1, g.BadLeft);
            Assert.AreEqual(0, g.CurrentSeat, "Gloves tidak mengakhiri giliran");

            g.Eat(); // kuning good, giliran lanjut, permen yang tadi ditusuk keluar lagi
            Assert.AreSame(forked, g.ActiveCandy);
            Assert.IsTrue(forked.Revealed, "keluar lagi tetap terbuka");
        }

        [Test]
        public void GlovesAfterFork_OnLastCandy_StaysRevealed()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YB });
            var forked = g.ActiveCandy;
            g.DebugGiveItem(Seat(g, 0), ItemType.PlasticFork);
            g.DebugGiveItem(Seat(g, 0), ItemType.MagicGloves);
            UseFirst(g, ItemType.PlasticFork);
            Assert.IsTrue(UseFirst(g, ItemType.MagicGloves).Success);
            Assert.AreSame(forked, g.ActiveCandy, "dispenser kosong: permen tidak ditukar");
            Assert.IsTrue(forked.Revealed, "tidak ditukar = tetap terbuka");
        }

        [Test]
        public void HotMilk_HealsOne_CappedAtMax()
        {
            var g = NewGame();
            g.DebugGiveItem(Seat(g, 0), ItemType.HotMilk);
            g.DebugGiveItem(Seat(g, 0), ItemType.HotMilk);
            UseFirst(g, ItemType.HotMilk);
            Assert.AreEqual(5, Seat(g, 0).Hp);
            UseFirst(g, ItemType.HotMilk);
            Assert.AreEqual(5, Seat(g, 0).Hp);
            Assert.AreEqual(0, Seat(g, 0).Items.Count, "item tetap terbuang walaupun HP penuh");
        }

        [Test]
        public void LittlePocket_ThreeOptions_NeverContainsPocket()
        {
            var g = NewGame();
            for (int i = 0; i < 200; i++)
            {
                g.DebugGiveItem(Seat(g, 0), ItemType.LittlePocket);
                UseFirst(g, ItemType.LittlePocket);
                Assert.AreEqual(GamePhase.AwaitingPocketChoice, g.Phase);
                Assert.AreEqual(3, g.PocketOptions.Count);
                CollectionAssert.DoesNotContain(g.PocketOptions.ToList(), ItemType.LittlePocket);
                g.DiscardPocket();
            }
        }

        [Test]
        public void LittlePocket_BlocksOtherActionsUntilChosen()
        {
            var g = NewGame();
            g.DebugGiveItem(Seat(g, 0), ItemType.LittlePocket);
            UseFirst(g, ItemType.LittlePocket);
            Assert.IsFalse(g.Eat().Success);
            g.DiscardPocket();
            Assert.IsTrue(g.Eat().Success);
        }

        [Test]
        public void Grappling_StealsAndActivatesImmediately()
        {
            var g = NewGame();
            g.DebugSetHp(Seat(g, 0), 3);
            g.DebugGiveItem(Seat(g, 0), ItemType.GrapplingHook);
            g.DebugGiveItem(Seat(g, 1), ItemType.HotMilk);
            Assert.IsTrue(UseFirst(g, ItemType.GrapplingHook, new ItemArgs(1, 0)).Success);
            Assert.AreEqual(4, Seat(g, 0).Hp);
            Assert.AreEqual(0, Seat(g, 1).Items.Count);
            Assert.AreEqual(0, Seat(g, 0).Items.Count, "item curian tidak disimpan");
        }

        [Test]
        public void Grappling_CannotStealGrappling()
        {
            var g = NewGame();
            g.DebugGiveItem(Seat(g, 0), ItemType.GrapplingHook);
            g.DebugGiveItem(Seat(g, 1), ItemType.GrapplingHook);
            Assert.IsFalse(UseFirst(g, ItemType.GrapplingHook, new ItemArgs(1, 0)).Success);
            Assert.AreEqual(1, Seat(g, 0).Items.Count, "grappling tidak terpakai kalau gagal");
            Assert.IsFalse(g.HasGrappleTarget(Seat(g, 0)));
        }

        [Test]
        public void Grappling_CanTargetParalyzed_NotEliminated()
        {
            var g = NewGame(3);
            g.DebugSetParalyzed(Seat(g, 1), true);
            g.DebugGiveItem(Seat(g, 0), ItemType.GrapplingHook);
            g.DebugGiveItem(Seat(g, 0), ItemType.GrapplingHook);
            g.DebugGiveItem(Seat(g, 1), ItemType.PlasticFork);
            Assert.IsTrue(UseFirst(g, ItemType.GrapplingHook, new ItemArgs(1, 0)).Success);

            g.DebugSetPool(new[] { YB, YG, YG });
            g.DebugSetHp(Seat(g, 2), 1);
            g.DebugGiveItem(Seat(g, 2), ItemType.HotMilk);
            g.Give(2); // P3 kalah
            Assert.IsTrue(Seat(g, 2).Eliminated);
            Assert.AreEqual(0, Seat(g, 2).Items.Count, "item pemain kalah hilang");
        }

        [Test]
        public void PocketToGrappling_InvalidTarget_KeepsPocketOpen()
        {
            var g = NewGame();
            g.DebugGiveItem(Seat(g, 1), ItemType.GrapplingHook);
            int idx = OpenPocketWith(g, ItemType.GrapplingHook);
            var options = g.PocketOptions.ToList();

            Assert.IsFalse(g.ChoosePocketItem(idx).Success, "Grappling dari Pocket tetap butuh target");
            Assert.IsFalse(g.ChoosePocketItem(idx, new ItemArgs(0, 0)).Success, "tidak bisa menarget diri sendiri");
            Assert.IsFalse(g.ChoosePocketItem(idx, new ItemArgs(1, 0)).Success, "tidak bisa mengambil Grappling Hook");
            Assert.AreEqual(GamePhase.AwaitingPocketChoice, g.Phase, "pilihan gagal tidak menutup Pocket");
            CollectionAssert.AreEqual(options, g.PocketOptions.ToList());
            Assert.AreEqual(1, Seat(g, 1).Items.Count);
            Assert.IsTrue(g.DiscardPocket().Success);
        }

        [Test]
        public void PocketToGrappling_StealsAndActivatesImmediately()
        {
            var g = NewGame();
            g.DebugSetHp(Seat(g, 0), 3);
            g.DebugGiveItem(Seat(g, 1), ItemType.HotMilk);
            int idx = OpenPocketWith(g, ItemType.GrapplingHook);

            Assert.IsTrue(g.ChoosePocketItem(idx, new ItemArgs(1, 0)).Success);
            Assert.AreEqual(GamePhase.AwaitingAction, g.Phase);
            Assert.AreEqual(0, g.PocketOptions.Count);
            Assert.AreEqual(4, Seat(g, 0).Hp, "Hot Milk curian langsung dipakai");
            Assert.AreEqual(0, Seat(g, 1).Items.Count);
            Assert.AreEqual(0, Seat(g, 0).Items.Count, "Pocket dan item curian tidak masuk inventory");
            Assert.AreEqual(0, g.CurrentSeat, "giliran tetap lanjut");
        }

        [Test]
        public void PocketToGrappling_StealingPocket_OpensNewPocket()
        {
            var g = NewGame();
            g.DebugGiveItem(Seat(g, 1), ItemType.LittlePocket);
            int idx = OpenPocketWith(g, ItemType.GrapplingHook);

            Assert.IsTrue(g.ChoosePocketItem(idx, new ItemArgs(1, 0)).Success);
            Assert.AreEqual(GamePhase.AwaitingPocketChoice, g.Phase, "Pocket curian langsung dibuka");
            Assert.AreEqual(3, g.PocketOptions.Count);
            CollectionAssert.DoesNotContain(g.PocketOptions.ToList(), ItemType.LittlePocket);
            Assert.AreEqual(0, Seat(g, 1).Items.Count);
            Assert.IsFalse(g.Eat().Success, "Pocket baru tetap harus dipilih/dibuang dulu");
        }

        [Test]
        public void Inventory_OverflowIsDiscardedInDealOrder()
        {
            var g = NewGame();
            var p = Seat(g, 0);
            for (int i = 0; i < 4; i++) g.DebugGiveItem(p, ItemType.HotMilk);
            g.DealItems(p, new[] { ItemType.PlasticFork, ItemType.HammerCrush, ItemType.Cupcake, ItemType.CloverCoin });
            Assert.AreEqual(6, p.Items.Count);
            Assert.AreEqual(ItemType.PlasticFork, p.Items[4]);
            Assert.AreEqual(ItemType.HammerCrush, p.Items[5]);
        }

        // ---------------------------------------------------------- menang / kalah

        [Test]
        public void LastSurvivorWins()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YB, YG });
            g.DebugSetHp(Seat(g, 1), 1);
            g.Give(1);
            Assert.IsTrue(Seat(g, 1).Eliminated);
            Assert.IsTrue(g.IsOver);
            Assert.AreSame(Seat(g, 0), g.Winner);
        }

        [Test]
        public void EliminatedPlayersAreSkipped()
        {
            var g = NewGame(3);
            g.DebugSetPool(new[] { YB, YG, YG, YG });
            g.DebugSetHp(Seat(g, 1), 1);
            g.Give(1);
            Assert.IsFalse(g.IsOver);
            Assert.AreEqual(2, g.CurrentSeat);
        }

        // ---------------------------------------------------------- balancing final

        [Test]
        public void CandyTable_MatchesFinalBalancing()
        {
            Assert.AreEqual(0, CandyRules.GoodHeal(CandyColor.Yellow));
            Assert.AreEqual(1, CandyRules.GoodHeal(CandyColor.Blue));
            Assert.AreEqual(1, CandyRules.GoodHeal(CandyColor.Red));
            Assert.AreEqual(1, CandyRules.BadDamage(CandyColor.Yellow));
            Assert.AreEqual(2, CandyRules.BadDamage(CandyColor.Blue));
            Assert.AreEqual(1, CandyRules.BadDamage(CandyColor.Red));
            Assert.IsTrue(CandyRules.BadParalyzes(CandyColor.Red));
        }

        [Test]
        public void YellowGood_NoEffect_ButTurnContinues()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YG, YG });
            g.DebugSetHp(Seat(g, 0), 3);
            g.Eat();
            Assert.AreEqual(3, Seat(g, 0).Hp);
            Assert.AreEqual(0, g.CurrentSeat, "good tetap good: giliran lanjut");
        }

        [Test]
        public void BadRatio_ScalesWithTableSize()
        {
            RuleConfig Cfg() => new RuleConfig { RandomDraw = false, ItemsPerRound = 0, MinBadRatio = 0.5f, MaxBadRatio = 0.5f, BadRatioPerExtraSeat = 0.1f };
            var two = NewGame(2, Cfg());
            Assert.AreEqual(6, two.Announcement.Total, "2 pemain x 3 = 6");
            Assert.AreEqual(3, two.Announcement.Bad, "rasio 50%");
            var six = NewGame(6, Cfg());
            Assert.AreEqual(12, six.Announcement.Total, "6 x 3 = 18, dibatasi maks 12");
            Assert.AreEqual(11, six.Announcement.Bad, "rasio 50% + 4 x 10% = 90%, tetap minimal 1 good");
        }

        // ---------------------------------------------------------- timer

        [Test]
        public void Timeout_ForcedEat_EndsTurnEvenIfGood()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { C(CandyColor.Blue, true), YG });
            g.DebugSetHp(Seat(g, 0), 3);
            Assert.IsTrue(g.TimeoutTurn().Success);
            Assert.AreEqual(4, Seat(g, 0).Hp, "permen tetap dimakan dan efeknya berlaku");
            Assert.AreEqual(1, g.CurrentSeat, "waktu habis = giliran berakhir");
            Assert.AreEqual(1, g.Stats.Timeouts);
        }

        [Test]
        public void Timeout_DiscardsOpenPocketFirst()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YB, YG });
            g.DebugGiveItem(Seat(g, 0), ItemType.LittlePocket);
            UseFirst(g, ItemType.LittlePocket);
            Assert.IsTrue(g.TimeoutTurn().Success);
            Assert.AreEqual(3, Seat(g, 0).Hp);
            Assert.AreEqual(1, g.CurrentSeat);
        }

        [Test]
        public void TimeoutForcedEat_Good_TurnContinuesWithNewCandy()
        {
            var g = NewGame(2, ForcedEatCfg());
            g.DebugSetPool(new[] { C(CandyColor.Blue, true), YG });
            g.DebugSetHp(Seat(g, 0), 3);
            var eaten = g.ActiveCandy;
            Assert.IsTrue(g.TimeoutTurn().Success);
            Assert.AreEqual(4, Seat(g, 0).Hp);
            Assert.AreEqual(0, g.CurrentSeat, "ForcedEat: good = giliran lanjut seperti eat biasa");
            Assert.AreEqual(GamePhase.AwaitingAction, g.Phase);
            Assert.IsNotNull(g.ActiveCandy);
            Assert.AreNotSame(eaten, g.ActiveCandy);
            Assert.AreEqual(1, g.Stats.Timeouts);
            Assert.IsTrue(g.Stats.CurrentTurn.TimedOut);
        }

        [Test]
        public void TimeoutForcedEat_Bad_EndsTurn_CupcakeBadContinues()
        {
            var g = NewGame(2, ForcedEatCfg());
            g.DebugSetPool(new[] { YB, YB, YG });
            g.DebugSetCupcake(Seat(g, 0), true);
            g.TimeoutTurn();
            Assert.AreEqual(4, Seat(g, 0).Hp, "cupcake menahan efek");
            Assert.AreEqual(0, g.CurrentSeat, "kebal cupcake = lanjut, sama seperti Eat");
            g.TimeoutTurn();
            Assert.AreEqual(3, Seat(g, 0).Hp);
            Assert.AreEqual(1, g.CurrentSeat, "bad tanpa cupcake = giliran berakhir");
            Assert.AreEqual(2, g.Stats.Timeouts);
        }

        [Test]
        public void TimeoutForcedEat_GoodOnLastCandy_ContinuesInNewRound_AfterOpenPocketDiscarded()
        {
            var g = NewGame(2, ForcedEatCfg());
            g.DebugSetPool(new[] { YG });
            g.DebugGiveItem(Seat(g, 0), ItemType.LittlePocket);
            UseFirst(g, ItemType.LittlePocket);
            int round = g.RoundNumber;
            Assert.IsTrue(g.TimeoutTurn().Success);
            Assert.AreEqual(0, g.PocketOptions.Count, "Pocket terbuka dibuang dulu");
            Assert.AreEqual(round + 1, g.RoundNumber);
            Assert.AreEqual(0, g.CurrentSeat);
            Assert.AreEqual(GamePhase.AwaitingAction, g.Phase);
            Assert.IsNotNull(g.ActiveCandy);
        }

        [Test]
        public void TimeoutForcedEat_LethalBad_EndsGame()
        {
            var g = NewGame(2, ForcedEatCfg());
            g.DebugSetPool(new[] { C(CandyColor.Blue, false), YG });
            g.DebugSetHp(Seat(g, 0), 2);
            Assert.IsTrue(g.TimeoutTurn().Success);
            Assert.IsTrue(Seat(g, 0).Eliminated);
            Assert.IsTrue(g.IsOver);
            Assert.AreSame(Seat(g, 1), g.Winner);
            Assert.IsFalse(g.TimeoutTurn().Success, "timeout setelah game selesai ditolak");
        }

        [Test]
        public void DecisionsAreCountedPerTurn()
        {
            var g = NewGame();
            g.DebugSetPool(new[] { YG, YG, YB, YG });
            g.DebugGiveItem(Seat(g, 0), ItemType.HotMilk);
            UseFirst(g, ItemType.HotMilk);
            g.Eat();
            g.Eat();
            g.Give(1);
            Assert.AreEqual(4, g.Stats.Turns[0].Decisions, "1 item + 2 eat + 1 give");
        }

        [Test]
        public void TimeModel_CapsThinkingPerTurn()
        {
            var r = new GameResult { Stats = new GameStats(), Rounds = 1 };
            r.Stats.Turns.Add(new TurnRecord { Decisions = 10 });
            var t = new TimeModel { ThinkPerDecision = 7, TurnThinkLimit = 30 };
            Assert.AreEqual(30, t.ThinkSeconds(r), 1e-9, "10 x 7 = 70, dibatasi 30");
            t.TurnThinkLimit = 0;
            Assert.AreEqual(70, t.ThinkSeconds(r), 1e-9);
        }

        // ---------------------------------------------------------- simulator

        [Test]
        public void Simulator_ProducesReportAndCsv()
        {
            var report = Simulator.Run(new SimulationSettings { Games = 20 });
            Assert.AreEqual(20, report.Results.Count);
            Assert.IsTrue(report.Results.All(r => r.Finished));
            StringAssert.Contains("Laporan simulasi", report.ToMarkdown());
            Assert.AreEqual(21, report.ToCsv().Trim().Split('\n').Length, "header + 20 baris");
        }

        [Test]
        public void Matrix_CombosCountAndRoundStats()
        {
            Assert.AreEqual(6, SimulationMatrix.Combos(2).Count);
            Assert.AreEqual(10, SimulationMatrix.Combos(3).Count);
            Assert.AreEqual(28, SimulationMatrix.Combos(6).Count);
            var reps = SimulationMatrix.Run(new[] { 3 }, 10, new RuleConfig(), new TimeModel());
            var row = SimulationMatrix.Summarize(reps[0]);
            Assert.Greater(row.CandiesPerRound, 0);
            Assert.AreEqual(row.ItemsSpawnedPerMatch, reps[0].Results.Average(r => r.Stats.Rounds.Sum(x => x.ItemsSpawned)), 1e-9);
        }

        // ---------------------------------------------------------- bot

        [Test]
        public void BotsFinishFullGames()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                var g = new GameEngine(new RuleConfig(), seed);
                var diffs = new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard };
                g.Start(Enumerable.Range(0, 2 + seed % 5)
                    .Select(i => new SeatConfig("Bot" + i, true, diffs[(i + seed) % 3])).ToList());
                var rng = new System.Random(seed);
                int steps = 0;
                while (!g.IsOver && steps < 20000)
                {
                    BotBrain.Step(g, rng);
                    steps++;
                }
                Assert.IsTrue(g.IsOver, $"seed {seed} tidak selesai dalam {steps} langkah");
                Assert.IsNotNull(g.Winner);
            }
        }
    }
}
