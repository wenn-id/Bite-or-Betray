using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    public class BalanceTests
    {
        // Tempo 1: jeda director saat ini, dengan cadangan animasi/interaksi; bukan telemetry Quest.
        static TimeModel OfflineTime() => new TimeModel
        {
            AssumeAllHuman = false, BotThinkPerDecision = 1.8f,
            WaiterFirstRound = 3, WaiterNextRounds = 3, Dispense = 0.9f, Eat = 4, Give = 4.5f
        };

        static SimulationSettings Settings(BotDifficulty[] seats, int games = 128) => new SimulationSettings
        {
            Seats = seats, Games = games, StartSeed = 1, MaxStepsPerGame = 2000,
            HumanProxyIndex = Math.Max(0, Array.IndexOf(seats, BotDifficulty.Normal)), Time = OfflineTime()
        };

        static void AssertProgress(SimulationReport report)
        {
            Assert.IsTrue(report.Results.All(r => r.Finished), SimulationMatrix.ComboName(report.Settings.Seats));
            Assert.AreEqual(0, report.Results.Sum(r => r.FailedDecisions), "Fallback tidak boleh menutupi keputusan tidak valid.");
            Assert.IsTrue(report.Results.All(r => r.Steps < report.Settings.MaxStepsPerGame));
        }

        static double WinRate(SimulationReport report, BotDifficulty level) =>
            (double)report.Results.Count(r => r.Finished && r.WinnerDifficulty == level)
            / report.Results.Count / report.Settings.Seats.Count(d => d == level);

        [TestCase(BotDifficulty.Easy, BotDifficulty.Normal)]
        [TestCase(BotDifficulty.Normal, BotDifficulty.Hard)]
        [TestCase(BotDifficulty.Easy, BotDifficulty.Hard)]
        public void Higher_difficulty_wins_more_duels(BotDifficulty lower, BotDifficulty higher)
        {
            var report = Simulator.Run(Settings(new[] { lower, higher }));
            AssertProgress(report);
            Assert.Greater(WinRate(report, higher) - WinRate(report, lower), 0.1,
                $"{lower} < {higher}, seed 1-128, d20 mengacak urutan giliran.");
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(6)]
        public void Mixed_table_preserves_difficulty_order_per_seat(int players)
        {
            var levels = new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard };
            var report = Simulator.Run(Settings(Enumerable.Range(0, players).Select(i => levels[i % 3]).ToArray()));
            AssertProgress(report);
            Assert.Greater(WinRate(report, BotDifficulty.Normal), WinRate(report, BotDifficulty.Easy) + 0.03);
            Assert.Greater(WinRate(report, BotDifficulty.Hard), WinRate(report, BotDifficulty.Normal) + 0.03);
        }

        [TestCase(BotDifficulty.Easy)]
        [TestCase(BotDifficulty.Normal)]
        [TestCase(BotDifficulty.Hard)]
        public void Three_bots_and_normal_proxy_have_reasonable_estimated_duration(BotDifficulty level)
        {
            var report = Simulator.Run(Settings(new[] { BotDifficulty.Normal, level, level, level }));
            AssertProgress(report);
            Assert.That(report.AverageMinutes, Is.InRange(5.0, 10.0), "Estimasi, bukan pengukuran runtime.");
        }

        [Test]
        public void Matrix_finishes_without_invalid_decisions()
        {
            // Pemeriksaan harian kecil; env ini menjalankan ulang pengukuran massal di Docs/Balance.md.
            string count = Environment.GetEnvironmentVariable("BOB_BALANCE_GAMES");
            int games = string.IsNullOrEmpty(count) ? 16 : int.Parse(count, CultureInfo.InvariantCulture);
            Assert.Greater(games, 0);
            var lines = new List<string>
            {
                "players,combo,games,win_easy,win_normal,win_hard,avg_min,p10_min,p90_min,max_steps,max_rounds,failed_decisions,unfinished,items_per_game,clover_per_game"
            };
            foreach (int players in Enumerable.Range(2, 5))
                foreach (var seats in SimulationMatrix.Combos(players))
                {
                    var report = Simulator.Run(Settings(seats, games));
                    AssertProgress(report);
                    var row = SimulationMatrix.Summarize(report);
                    string N(double value) => double.IsNaN(value) ? "" : value.ToString("0.0000", CultureInfo.InvariantCulture);
                    lines.Add(string.Join(",", players, SimulationMatrix.ComboName(seats), games,
                        N(row.WinEasy), N(row.WinNormal), N(row.WinHard), N(row.AvgMin), N(row.P10Min), N(row.P90Min),
                        report.Results.Max(r => r.Steps), report.Results.Max(r => r.Rounds),
                        report.Results.Sum(r => r.FailedDecisions), report.Results.Count(r => !r.Finished),
                        N(report.Results.Average(r => r.Stats.ItemsUsed.Sum())), N(row.CloverPerMatch)));
                }
            string output = Environment.GetEnvironmentVariable("BOB_BALANCE_OUTPUT");
            if (!string.IsNullOrEmpty(output)) File.WriteAllLines(output, lines);
            TestContext.WriteLine($"80 konfigurasi x {games} game: selesai, tanpa fallback; CSV: {output}");
        }

        static GameEngine Situation(BotDifficulty level, params Candy[] candies)
        {
            var g = new GameEngine(new RuleConfig { ItemsPerRound = 0, RandomDraw = false }, 42);
            g.Start(new[] { new SeatConfig("A", true, level), new SeatConfig("B", true, level) });
            g.DebugSetPool(candies);
            return g;
        }

        [Test]
        public void Hard_keeps_fork_when_cupcake_already_protects()
        {
            var g = Situation(BotDifficulty.Hard, new Candy(CandyColor.Blue, true), new Candy(CandyColor.Blue, false));
            g.DebugSetCupcake(g.Current, true);
            g.DebugGiveItem(g.Current, ItemType.PlasticFork);
            Assert.AreEqual(BotActionKind.Eat, BotBrain.Decide(g, new Random(1)).Kind);
        }

        [TestCase(BotDifficulty.Normal, true)]
        [TestCase(BotDifficulty.Normal, false)]
        [TestCase(BotDifficulty.Hard, true)]
        [TestCase(BotDifficulty.Hard, false)]
        public void Public_counts_replace_redundant_fork(BotDifficulty level, bool good)
        {
            var g = Situation(level, new Candy(CandyColor.Yellow, good));
            g.DebugGiveItem(g.Current, ItemType.PlasticFork);
            g.DebugGiveItem(g.Current, ItemType.CloverCoin);
            var decision = BotBrain.Decide(g, new Random(1));
            Assert.AreEqual(good ? BotActionKind.Eat : BotActionKind.UseItem, decision.Kind);
            if (!good) Assert.AreEqual(1, decision.Index, "Clover menyerang; Fork tidak diperlukan.");
        }

        [Test]
        public void Hard_avoids_lethal_blue_gamble_without_reading_hidden_flavor()
        {
            var g = Situation(BotDifficulty.Hard, new Candy(CandyColor.Blue, true), new Candy(CandyColor.Blue, false));
            g.DebugSetHp(g.Current, 2);
            Assert.AreEqual(BotActionKind.Give, BotBrain.Decide(g, new Random(1)).Kind);
        }

        [TestCase(BotDifficulty.Easy)]
        [TestCase(BotDifficulty.Normal)]
        [TestCase(BotDifficulty.Hard)]
        public void Hidden_flavors_do_not_change_decision(BotDifficulty level)
        {
            var a = Situation(level, new Candy(CandyColor.Blue, true), new Candy(CandyColor.Red, false));
            var b = Situation(level, new Candy(CandyColor.Blue, false), new Candy(CandyColor.Red, true));
            var first = BotBrain.Decide(a, new Random(19));
            var second = BotBrain.Decide(b, new Random(19));
            Assert.AreEqual(first.Kind, second.Kind);
            Assert.AreEqual(first.TargetSeat, second.TargetSeat);
            Assert.AreEqual(first.Index, second.Index);
        }

        [Test]
        public void Human_proxy_changes_time_only_and_tracks_configuration_after_d20()
        {
            var settings = Settings(new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard });
            settings.HumanProxyIndex = -1;
            var bots = Simulator.RunOne(settings, 42);
            settings.HumanProxyIndex = 1;
            var human = Simulator.RunOne(settings, 42);
            Assert.AreEqual(bots.WinnerName, human.WinnerName);
            Assert.AreEqual(bots.Steps, human.Steps);
            Assert.AreEqual(bots.Stats.CandiesDispensed, human.Stats.CandiesDispensed);
            var g = new GameEngine(settings.Rules.Clone(), 42);
            g.Start(settings.Seats.Select((d, i) => new SeatConfig($"P{i}", true, d)).ToList());
            int proxySeat = g.Players.Single(p => p.ConfigIndex == 1).Seat;
            Assert.IsTrue(human.Stats.Turns.Any(t => !t.IsBot));
            Assert.IsTrue(human.Stats.Turns.All(t => t.IsBot == (t.Seat != proxySeat)));
            Assert.Greater(settings.Time.EstimateSeconds(human), settings.Time.EstimateSeconds(bots));
        }
    }
}
