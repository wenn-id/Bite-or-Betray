using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    public sealed class RulesSoakTests
    {
        const int TurnLimit = 2000;
        const int ActionLimit = 10000;

        [Test]
        public void Seeded_matches_preserve_rules_and_public_snapshots()
        {
            Run(2000);
        }

        [Test, Category("LongRunning"), Explicit("Long rules soak: 100,000 seeded matches; excluded from routine CI.")]
        public void Large_seeded_soak_preserves_rules_and_public_snapshots()
        {
            Run(100000);
        }

        static void Run(int matches)
        {
            var timer = Stopwatch.StartNew();
            var mixes = Enumerable.Range(1, 5).SelectMany(SimulationMatrix.Combos).ToArray();
            var coverage = new Coverage();
            for (int match = 0; match < matches; match++)
            {
                int seed = match + 1;
                var mix = mixes[match % mixes.Length];
                var seats = new List<TableSeat> { new TableSeat("Human", SeatController.Local) };
                for (int i = 0; i < mix.Length; i++)
                    seats.Add(new TableSeat("Bot" + i, SeatController.Bot, difficulty: mix[i]));
                var g = new GameEngine(new RuleConfig(), seed);
                var rng = new Random(unchecked(seed * 397 ^ 0x5eed));
                var audit = new MatchAudit(g, seats.ToArray());
                int step = 0;
                string action = "Start";
                try
                {
                    g.Start(seats.Select(s => new SeatConfig(s.Name, s.Controller == SeatController.Bot, s.Difficulty)).ToArray());
                    audit.CheckState(step);
                    while (!g.IsOver && step < ActionLimit && g.TurnNumber < TurnLimit)
                    {
                        bool human = !g.Current.IsBot;
                        // Timer expiry may interrupt either a normal action or a pending pocket choice.
                        bool timeout = human && rng.Next(12) == 0;
                        var d = timeout ? null : human ? RandomLegalDecision(g, rng) : BotBrain.Decide(g, rng);
                        action = timeout ? "Timeout" : $"{d.Kind} index={d.Index} target={d.TargetSeat} grapple={d.Args?.TargetSeat}/{d.Args?.TargetItemIndex}";
                        audit.BeforeAction(d, timeout);
                        coverage.Record(g, d, timeout, human);
                        var previousCandy = g.ActiveCandy;
                        var result = timeout ? g.TimeoutTurn() : BotBrain.Execute(g, d);
                        Require(result.Success, "Legal decision rejected: " + result.Error);
                        if (g.ActiveCandy != null && g.ActiveCandy != previousCandy && g.ActiveCandy.Revealed)
                            coverage.RevealedRedraws++;
                        step++;
                        audit.AfterAction();
                        audit.CheckState(step);
                    }
                    Require(g.IsOver, "Match exceeded the turn/action limit.");
                    audit.CheckStandings();
                    coverage.Skips += g.Stats.TurnsSkipped;
                    coverage.Actions += step;
                    coverage.MaxTurns = Math.Max(coverage.MaxTurns, g.TurnNumber);
                    if ((match + 1) % 10000 == 0)
                        TestContext.Progress.WriteLine($"Rules soak: {match + 1}/{matches} matches, {timer.Elapsed.TotalSeconds:F1}s.");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"seed={seed}, bots={SimulationMatrix.ComboName(mix)}, completedActions={step}, last={action}, "
                        + $"round={g.RoundNumber}, turn={g.TurnNumber}, seat={g.CurrentSeat}\n{ex}\n"
                        + string.Join("\n", g.LogLines.Skip(Math.Max(0, g.LogLines.Count - 25))));
                }
            }
            coverage.Check();
            TestContext.WriteLine($"{matches} matches, {mixes.Length} difficulty mixes, {coverage.Actions} actions, "
                + $"max {coverage.MaxTurns} turns, {timer.Elapsed.TotalSeconds:F2}s; "
                + $"human items: {string.Join(", ", coverage.Items)}, pocket choices: {string.Join(", ", coverage.PocketItems)}; "
                + $"Fork/Gloves swaps: {coverage.ForkGloves}, revealed redraws: {coverage.RevealedRedraws}.");
        }

        static BotDecision RandomLegalDecision(GameEngine g, Random rng)
        {
            var options = new List<BotDecision>();
            bool pocket = g.Phase == GamePhase.AwaitingPocketChoice;
            if (pocket) options.Add(new BotDecision { Kind = BotActionKind.DiscardPocket });
            else
            {
                options.Add(new BotDecision { Kind = BotActionKind.Eat });
                foreach (var p in g.Players)
                    if (p != g.Current && p.IsAlive)
                        options.Add(new BotDecision { Kind = BotActionKind.Give, TargetSeat = p.Seat });
            }
            var items = pocket ? g.PocketOptions : g.Current.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var kind = pocket ? BotActionKind.ChoosePocket : BotActionKind.UseItem;
                if (items[i] != ItemType.GrapplingHook)
                    options.Add(new BotDecision { Kind = kind, Index = i });
                else
                    foreach (var p in g.Players)
                        if (p != g.Current && p.IsAlive)
                            for (int j = 0; j < p.Items.Count; j++)
                                if (p.Items[j] != ItemType.GrapplingHook)
                                    options.Add(new BotDecision { Kind = kind, Index = i, Args = new ItemArgs(p.Seat, j) });
            }
            return options[rng.Next(options.Count)];
        }

        static ItemType? EffectiveItem(GameEngine g, BotDecision d)
        {
            if (d == null || (d.Kind != BotActionKind.UseItem && d.Kind != BotActionKind.ChoosePocket)) return null;
            var type = d.Kind == BotActionKind.UseItem ? g.Current.Items[d.Index] : g.PocketOptions[d.Index];
            return type == ItemType.GrapplingHook ? g.Players[d.Args.TargetSeat].Items[d.Args.TargetItemIndex] : type;
        }

        // Avoid allocating NUnit constraints/messages for millions of successful invariant checks.
        static void Require(bool condition, string message)
        {
            if (!condition) throw new AssertionException(message);
        }

        sealed class Coverage
        {
            public readonly int[] Items = new int[ItemInfo.All.Length];
            public readonly int[] PocketItems = new int[ItemInfo.All.Length];
            readonly HashSet<int> giveSeats = new HashSet<int>();
            readonly HashSet<int> grappleSeats = new HashSet<int>();
            readonly HashSet<int> grappleIndices = new HashSet<int>();
            int eats, discards, timeouts, pocketTimeouts;
            public int ForkGloves, RevealedRedraws, Skips, Actions, MaxTurns;

            public void Record(GameEngine g, BotDecision d, bool timeout, bool human)
            {
                if (EffectiveItem(g, d) == ItemType.MagicGloves)
                {
                    if (g.ActiveCandy.Revealed && g.CandiesInDispenser > 0) ForkGloves++;
                }
                if (!human) return;
                if (timeout)
                {
                    timeouts++;
                    if (g.Phase == GamePhase.AwaitingPocketChoice) pocketTimeouts++;
                    return;
                }
                if (d.Kind == BotActionKind.Eat) eats++;
                if (d.Kind == BotActionKind.Give) giveSeats.Add(d.TargetSeat);
                if (d.Kind == BotActionKind.DiscardPocket) discards++;
                if (d.Kind != BotActionKind.UseItem && d.Kind != BotActionKind.ChoosePocket) return;
                var type = d.Kind == BotActionKind.UseItem ? g.Current.Items[d.Index] : g.PocketOptions[d.Index];
                Items[(int)type]++;
                if (d.Kind == BotActionKind.ChoosePocket) PocketItems[(int)type]++;
                if (type == ItemType.GrapplingHook)
                {
                    grappleSeats.Add(d.Args.TargetSeat);
                    grappleIndices.Add(d.Args.TargetItemIndex);
                }
            }

            public void Check()
            {
                foreach (var type in ItemInfo.All)
                {
                    Require(Items[(int)type] > 0, "Human never used " + type);
                    if (type != ItemType.LittlePocket)
                        Require(PocketItems[(int)type] > 0, "Human never chose pocket option " + type);
                }
                Require(eats > 0 && discards > 0 && timeouts > 0 && pocketTimeouts > 0, "Missing human action coverage.");
                Require(giveSeats.Count == 6 && grappleSeats.Count == 6 && grappleIndices.Count == 6, "Missing target seat/item coverage.");
                Require(ForkGloves > 0 && RevealedRedraws > 0 && Skips > 0, "Missing reveal/paralysis coverage.");
            }
        }

        sealed class MatchAudit
        {
            readonly GameEngine g;
            readonly TableSeat[] seats;
            readonly HashSet<int> revealed = new HashSet<int>();
            readonly Dictionary<int, (CandyColor color, bool good)> candies = new Dictionary<int, (CandyColor, bool)>();
            readonly Dictionary<int, int> eliminated = new Dictionary<int, int>();
            readonly List<int> eliminationOrder = new List<int>();
            bool[] paralyzed;
            int previousSeat, previousTurn, previousSkips, logStart;
            bool endsTurn;
            Candy removed;

            public MatchAudit(GameEngine engine, TableSeat[] bindings)
            {
                g = engine;
                seats = bindings;
            }

            public void BeforeAction(BotDecision d, bool timeout)
            {
                Require(g.Current.IsAlive && !g.Current.Paralyzed, "Eliminated/paralyzed player attempted an action.");
                Require(!eliminated.ContainsKey(g.CurrentSeat), "Eliminated seat acted again.");
                previousSeat = g.CurrentSeat;
                previousTurn = g.TurnNumber;
                previousSkips = g.Stats.TurnsSkipped;
                logStart = g.LogLines.Count;
                paralyzed = g.Players.Select(p => p.Paralyzed).ToArray();
                bool eat = timeout || d.Kind == BotActionKind.Eat;
                bool give = !timeout && d.Kind == BotActionKind.Give;
                removed = null;
                endsTurn = timeout || give || eat && !g.ActiveCandy.IsGood && !g.Current.HasCupcake;
                if (eat || give)
                {
                    removed = g.ActiveCandy;
                    var target = give ? g.Players[d.TargetSeat] : g.Current;
                    if (!target.HasCupcake && !removed.IsGood && CandyRules.BadParalyzes(removed.Color))
                        paralyzed[target.Seat] = true;
                }
                var item = EffectiveItem(g, d);
                if (item == ItemType.PlasticFork) revealed.Add(g.ActiveCandy.Id);
                if (item == ItemType.HammerCrush) removed = g.ActiveCandy;
            }

            public void AfterAction()
            {
                if (removed != null) Require(removed.Revealed, "Candy leaving play was not revealed.");
                var expectedSkips = new List<int>();
                int expectedSeat = previousSeat;
                if (endsTurn && !g.IsOver)
                {
                    // When every survivor is paralyzed, clear one full lap before selecting a seat.
                    for (int i = 0; i < g.Players.Count * 2; i++)
                    {
                        expectedSeat = (expectedSeat + 1) % g.Players.Count;
                        var p = g.Players[expectedSeat];
                        if (!p.IsAlive) continue;
                        if (!paralyzed[expectedSeat]) break;
                        expectedSkips.Add(expectedSeat);
                        paralyzed[expectedSeat] = false;
                    }
                }
                Require(g.CurrentSeat == expectedSeat, "Turn order did not skip exactly the eliminated/paralyzed seats.");
                Require(g.TurnNumber == previousTurn + (endsTurn && !g.IsOver ? 1 : 0), "Wrong turn number after action.");
                Require(g.Stats.TurnsSkipped - previousSkips == expectedSkips.Count, "Wrong skipped-turn count.");
                foreach (var p in g.Players)
                    Require(p.Paralyzed == (p.IsAlive && paralyzed[p.Seat]), "Paralysis did not clear on exactly one skipped turn.");
                var skipLogs = new List<int>();
                for (int i = logStart; i < g.LogLines.Count; i++)
                {
                    string line = g.LogLines[i];
                    foreach (var p in g.Players)
                    {
                        if (line == $"{p.Name} is paralyzed: turn skipped, paralysis cleared.") skipLogs.Add(p.Seat);
                        if (!line.StartsWith(p.Name + " is poisoned!", StringComparison.Ordinal)) continue;
                        Require(!eliminated.ContainsKey(p.Seat), "Repeated elimination in log.");
                        eliminated.Add(p.Seat, previousTurn);
                        eliminationOrder.Add(p.Seat);
                    }
                }
                Require(skipLogs.SequenceEqual(expectedSkips), "Skip log disagrees with turn order.");
            }

            public void CheckState(int revision)
            {
                foreach (var p in g.Players)
                {
                    Require(p.Hp >= 0 && p.Hp <= g.Config.MaxHp, "HP outside rule limits.");
                    Require(p.Items.Count <= g.Config.MaxItems, "Inventory exceeds capacity.");
                    Require(p.Eliminated == (p.Hp == 0), "HP/elimination disagreement.");
                    Require(p.Eliminated == eliminated.ContainsKey(p.Seat), "Elimination log/state disagreement.");
                    if (p.Eliminated)
                    {
                        Require(p.EliminatedOnTurn == eliminated[p.Seat], "Elimination turn differs from log.");
                        Require(p.Items.Count == 0 && !p.HasCupcake && !p.Paralyzed, "Eliminated player retains items/status.");
                    }
                    else Require(p.EliminatedOnTurn == 0, "Survivor has an elimination turn.");
                }
                Require(g.Stats.Turns.Count == g.TurnNumber, "Turn history/count disagreement.");
                Require(g.Stats.Eliminations == eliminated.Count, "Elimination statistics disagree with log.");
                if (!g.IsOver)
                {
                    Require(g.Current.IsAlive && !g.Current.Paralyzed && g.ActiveCandy != null, "No valid active turn.");
                    Require(g.Winner == null, "Winner declared before game over.");
                }
                Require(g.PocketOptions.Count == (g.Phase == GamePhase.AwaitingPocketChoice ? 3 : 0), "Pocket phase/options disagree.");
                Require(!g.PocketOptions.Contains(ItemType.LittlePocket), "Pocket contains another pocket.");
                int good = 0, bad = 0;
                var present = new HashSet<int>();
                foreach (var c in g.DebugPool) CheckCandy(c, present, ref good, ref bad);
                if (g.ActiveCandy != null) CheckCandy(g.ActiveCandy, present, ref good, ref bad);
                Require(g.GoodLeft == good && g.BadLeft == bad, "Good/bad counters differ from actual candy contents.");
                Require(g.GoodLeft + g.BadLeft == g.CandiesInDispenser + (g.ActiveCandy == null ? 0 : 1), "Candy conservation failed.");
                Require(g.CandiesInDispenser == g.DebugPool.Count, "Dispenser count differs from pool.");
                CheckRanks();
                CheckSnapshot(revision);
            }

            void CheckRanks()
            {
                var standings = FinalStandings.Of(g);
                // Survivors share first place; each candy can eliminate only one player, so losers have unique ranks.
                var survivors = g.Players.Where(p => p.IsAlive).Select(p => p.Seat).ToArray();
                var expected = survivors.Concat(eliminationOrder.AsEnumerable().Reverse()).ToArray();
                Require(standings.Count == expected.Length, "Standings omit/duplicate a player.");
                for (int i = 0; i < standings.Count; i++)
                {
                    Require(standings[i].player.Seat == expected[i], "Standings disagree with logged elimination order.");
                    Require(standings[i].place == (i < survivors.Length ? 1 : i + 1), "Wrong rank for survival time.");
                }
            }

            void CheckCandy(Candy c, HashSet<int> present, ref int good, ref int bad)
            {
                Require(present.Add(c.Id), "Candy duplicated in active slot/dispenser.");
                if (candies.TryGetValue(c.Id, out var original))
                    Require(original.color == c.Color && original.good == c.IsGood, "Candy identity changed.");
                else candies.Add(c.Id, (c.Color, c.IsGood));
                Require(c.Revealed == revealed.Contains(c.Id), "Reveal knowledge lost or hidden candy exposed (Fork/Gloves).");
                if (c.IsGood) good++; else bad++;
            }

            void CheckSnapshot(int revision)
            {
                var s = TableSnapshot.Capture(g, seats, revision, 12.5f, !g.IsOver, 123);
                var bytes = TableWire.WriteSnapshot(s);
                var copy = TableWire.ReadSnapshot(bytes);
                Require(bytes.SequenceEqual(TableWire.WriteSnapshot(copy)), "Snapshot bytes changed on round-trip.");
                Require(copy.Revision == revision && copy.GameId == 123 && copy.SecondsLeft == 12.5f
                    && copy.InputOpen == !g.IsOver, "Snapshot envelope changed.");
                Require(copy.Phase == g.Phase && copy.IsOver == g.IsOver && copy.RoundNumber == g.RoundNumber
                    && copy.TurnNumber == g.TurnNumber && copy.CurrentSeat == g.CurrentSeat
                    && copy.WinnerSeat == (g.Winner?.Seat ?? -1), "Snapshot match state changed.");
                Require(copy.Config.MaxHp == g.Config.MaxHp && copy.Config.TurnTimeLimitSeconds == g.Config.TurnTimeLimitSeconds,
                    "Snapshot view rules changed.");
                Require(copy.CandiesInDispenser == g.CandiesInDispenser && copy.GoodLeft == g.GoodLeft
                    && copy.BadLeft == g.BadLeft && copy.CloverOnActive == g.CloverOnActive, "Snapshot candy counters changed.");
                var a = copy.Announcement;
                var b = g.Announcement;
                Require(a.Total == b.Total && a.Yellow == b.Yellow && a.Blue == b.Blue && a.Red == b.Red
                    && a.Good == b.Good && a.Bad == b.Bad, "Snapshot announcement changed.");
                Require(copy.PocketOptions.SequenceEqual(g.PocketOptions), "Snapshot pocket options changed.");
                Require((copy.ActiveCandy == null) == (g.ActiveCandy == null), "Snapshot active candy presence changed.");
                if (g.ActiveCandy != null)
                {
                    var c = copy.ActiveCandy;
                    Require(c.Id == g.ActiveCandy.Id && c.Color == g.ActiveCandy.Color && c.Revealed == g.ActiveCandy.Revealed,
                        "Snapshot candy identity/reveal changed.");
                    Require(c.IsGood == (g.ActiveCandy.Revealed && g.ActiveCandy.IsGood), "Snapshot leaked hidden candy or lost revealed contents.");
                    Require(!ReferenceEquals(c, g.ActiveCandy), "Snapshot shares host candy.");
                }
                Require(copy.Players.Count == g.Players.Count && copy.Seats.Length == seats.Length, "Snapshot seat count changed.");
                foreach (var p in g.Players)
                {
                    var q = copy.Players[p.Seat];
                    Require(q.Seat == p.Seat && q.ConfigIndex == p.ConfigIndex && q.Name == p.Name && q.IsBot == p.IsBot
                        && q.Difficulty == p.Difficulty && q.D20Roll == p.D20Roll && q.Hp == p.Hp && q.Paralyzed == p.Paralyzed
                        && q.HasCupcake == p.HasCupcake && q.Eliminated == p.Eliminated && q.EliminatedOnTurn == p.EliminatedOnTurn
                        && q.Items.SequenceEqual(p.Items), "Snapshot player state changed.");
                    Require(!ReferenceEquals(q, p) && !ReferenceEquals(q.Items, p.Items), "Snapshot shares host player/inventory.");
                    var binding = copy.Seats[p.ConfigIndex];
                    var source = seats[p.ConfigIndex];
                    Require(binding.Name == source.Name && binding.Controller == source.Controller
                        && binding.ClientId == source.ClientId && binding.Difficulty == source.Difficulty, "Snapshot seat binding changed.");
                }
            }

            public void CheckStandings()
            {
                Require(g.AlivePlayers.Count() == 1 && g.Winner == g.AlivePlayers.Single(), "Game must have exactly one winner.");
                Require(eliminationOrder.Count == g.Players.Count - 1, "Log must record every loser once.");
                Require(g.LogLines.Last() == $"===== {g.Winner.Name} WINS! =====", "Winner disagrees with final log entry.");
                CheckRanks();
            }
        }
    }
}
