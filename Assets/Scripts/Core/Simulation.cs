using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BiteOrBetray.Core
{
    /// <summary>Event counters for one game.</summary>
    public sealed class GameStats
    {
        public int CandiesDispensed;
        public int EatSelf, EatSelfGood, EatSelfBad;
        public int Gives, GivesGood, GivesBad;
        public int CupcakeBlocks, CloverTriggers, ParalyzeApplied, TurnsSkipped;
        public int HpHealed, HpLost, Eliminations;
        public int ItemsDiscardedFull, PocketDiscarded, Timeouts;
        /// <summary>Item activations by type, including Grappling Hook thefts and Little Pocket choices.</summary>
        public readonly int[] ItemsUsed = new int[ItemInfo.All.Length];
        /// <summary>Items dealt per round by type (including immediate discards when the inventory is full).</summary>
        public readonly int[] ItemsSpawned = new int[ItemInfo.All.Length];
        public readonly int[] CandiesSpawnedByColor = new int[3];
        public readonly int[] GoodSpawnedByColor = new int[3];
        public readonly int[] BadSpawnedByColor = new int[3];
        /// <summary>Candy eaten or fed (leaving play through a mouth), by color.</summary>
        public readonly int[] GoodConsumedByColor = new int[3];
        public readonly int[] BadConsumedByColor = new int[3];
        public readonly System.Collections.Generic.List<RoundRecord> Rounds = new System.Collections.Generic.List<RoundRecord>();
        public RoundRecord CurrentRound => Rounds.Count > 0 ? Rounds[Rounds.Count - 1] : null;
        public readonly System.Collections.Generic.List<TurnRecord> Turns = new System.Collections.Generic.List<TurnRecord>();
        public TurnRecord CurrentTurn => Turns.Count > 0 ? Turns[Turns.Count - 1] : null;
    }

    /// <summary>One turn record: how many decisions the player made during that turn.</summary>
    public sealed class TurnRecord
    {
        public int Round;
        public int Seat;
        public bool IsBot;
        public int Decisions;
        public bool TimedOut;
    }

    /// <summary>One round record for analysis.</summary>
    public sealed class RoundRecord
    {
        public int Number;
        public int AlivePlayers;
        public int Candies, Yellow, Blue, Red, Good, Bad;
        public int ItemsSpawned, ItemsReceived;
        /// <summary>Turns started in this round (excluding turns skipped due to paralysis).</summary>
        public int Turns;
        public int TurnsSkipped;
    }

    /// <summary>Estimated time per VR event (seconds). Initial values; replace with playtest telemetry.</summary>
    [Serializable]
    public class TimeModel
    {

        public float GameIntro = 30f;
        public float GameOutro = 15f;
        public float WaiterFirstRound = 25f;
        public float WaiterNextRounds = 10f;
        public float Dispense = 2.5f;

        public float ThinkPerDecision = 7f;
        public float BotThinkPerDecision = 2.5f;
        /// <summary>True: treat every seat as human (for balancing). False: bot seats use BotThinkPerDecision.</summary>
        public bool AssumeAllHuman = true;
        /// <summary>Thinking time limit per turn (timer). 0 = unlimited.</summary>
        public float TurnThinkLimit = 30f;

        public float Eat = 5f;
        public float Give = 6f;
        public float SkipTurn = 2f;

        public float PlasticFork = 4f;
        public float HammerCrush = 5f;
        public float HotMilk = 4f;
        public float MagicGloves = 4f;
        public float Cupcake = 4f;
        public float LittlePocket = 4f;
        public float PocketChoice = 4f;
        public float GrapplingHook = 7f;
        public float CloverCoin = 3f;

        public TimeModel Clone() => (TimeModel)MemberwiseClone();

        public float ItemSeconds(ItemType t)
        {
            switch (t)
            {
                case ItemType.PlasticFork: return PlasticFork;
                case ItemType.HammerCrush: return HammerCrush;
                case ItemType.HotMilk: return HotMilk;
                case ItemType.MagicGloves: return MagicGloves;
                case ItemType.Cupcake: return Cupcake;
                case ItemType.LittlePocket: return LittlePocket + PocketChoice;
                case ItemType.GrapplingHook: return GrapplingHook;
                default: return CloverCoin;
            }
        }

        /// <summary>Total thinking time in one game, with a per-turn timer limit.</summary>
        public double ThinkSeconds(GameResult r)
        {
            double total = 0;
            foreach (var t in r.Stats.Turns)
            {
                double per = AssumeAllHuman || !t.IsBot ? ThinkPerDecision : BotThinkPerDecision;
                double think = t.Decisions * per;
                if (TurnThinkLimit > 0 && think > TurnThinkLimit) think = TurnThinkLimit;
                total += think;
            }
            return total;
        }

        public double EstimateSeconds(GameResult r)
        {
            var t = r.Stats;
            double items = 0;
            foreach (var it in ItemInfo.All) items += t.ItemsUsed[(int)it] * ItemSeconds(it);
            return GameIntro + GameOutro
                   + WaiterFirstRound + Math.Max(0, r.Rounds - 1) * WaiterNextRounds
                   + t.CandiesDispensed * Dispense
                   + ThinkSeconds(r)
                   + t.EatSelf * Eat
                   + t.Gives * Give
                   + items
                   + t.TurnsSkipped * SkipTurn;
        }
    }

    public sealed class SimulationSettings
    {
        public int Games = 500;
        public BotDifficulty[] Seats = { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard };
        public RuleConfig Rules = new RuleConfig();
        public int StartSeed = 1;
        public int MaxStepsPerGame = 50000;
        /// <summary>Seat configuration index for the human timing model; decisions still use a bot. -1 = all bots.</summary>
        public int HumanProxyIndex = -1;
        /// <summary>Timing model per action for VR duration estimates.</summary>
        public TimeModel Time = new TimeModel();
    }

    public sealed class GameResult
    {
        public int Seed;
        public int Players;
        public bool Finished;
        public string WinnerName;
        public BotDifficulty WinnerDifficulty;
        /// <summary>Winner's turn position (0 = first turn).</summary>
        public int WinnerSeat;
        public int WinnerHp;
        public int Turns;
        public int Rounds;
        public int FirstEliminationTurn;
        public int Steps;
        /// <summary>Invalid decisions before fallback; must remain visible in balancing results.</summary>
        public int FailedDecisions;
        public GameStats Stats;
    }

    public static class Simulator
    {
        /// <param name="progress">Called every few games with (completed, total). Return true to cancel.</param>
        public static SimulationReport Run(SimulationSettings s, Func<int, int, bool> progress = null)
        {
            if (s.Seats == null || s.Seats.Length < 2 || s.Seats.Length > 6)
                throw new ArgumentException("Simulation seat count must be 2-6.");
            if (s.HumanProxyIndex < -1 || s.HumanProxyIndex >= s.Seats.Length)
                throw new ArgumentException("Human proxy index outside seat configuration.");

            var results = new List<GameResult>();
            bool cancelled = false;
            for (int i = 0; i < s.Games; i++)
            {
                if (progress != null && i % 10 == 0 && progress(i, s.Games))
                {
                    cancelled = true;
                    break;
                }
                results.Add(RunOne(s, s.StartSeed + i));
            }
            return new SimulationReport(s, results, cancelled);
        }

        public static GameResult RunOne(SimulationSettings s, int seed)
        {
            var g = new GameEngine(s.Rules.Clone(), seed) { KeepLog = false };
            g.Start(s.Seats.Select((d, i) => new SeatConfig($"Seat{i + 1}-{d}", true, d)).ToList());
            var rng = new Random(seed * 31 + 7);
            int steps = 0, failedDecisions = 0;
            while (!g.IsOver && steps < s.MaxStepsPerGame)
            {
                var action = BotBrain.Execute(g, BotBrain.Decide(g, rng));
                if (!action.Success)
                {
                    failedDecisions++;
                    if (g.Phase == GamePhase.AwaitingPocketChoice) g.DiscardPocket(); else g.Eat();
                }
                steps++;
            }

            // d20 randomizes seating; the configuration index still refers to the same participant.
            var proxy = g.Players.FirstOrDefault(p => p.ConfigIndex == s.HumanProxyIndex);
            if (proxy != null)
                foreach (var turn in g.Stats.Turns.Where(t => t.Seat == proxy.Seat)) turn.IsBot = false;
            var firstOut = g.Players.Where(p => p.Eliminated).Select(p => p.EliminatedOnTurn).DefaultIfEmpty(0).Min();
            var r = new GameResult
            {
                Seed = seed,
                Players = g.Players.Count,
                Finished = g.IsOver && g.Winner != null,
                Turns = g.TurnNumber,
                Rounds = g.RoundNumber,
                FirstEliminationTurn = firstOut,
                Steps = steps,
                FailedDecisions = failedDecisions,
                Stats = g.Stats
            };
            if (r.Finished)
            {
                r.WinnerName = g.Winner.Name;
                r.WinnerDifficulty = g.Winner.Difficulty;
                r.WinnerSeat = g.Winner.Seat;
                r.WinnerHp = g.Winner.Hp;
            }
            return r;
        }
    }

    public sealed class SimulationReport
    {
        public readonly SimulationSettings Settings;
        public readonly List<GameResult> Results;
        public readonly bool Cancelled;

        public SimulationReport(SimulationSettings settings, List<GameResult> results, bool cancelled)
        {
            Settings = settings;
            Results = results;
            Cancelled = cancelled;
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string F(double v) => v.ToString("0.0", Inv);
        static string Pct(double v) => (v * 100).ToString("0.0", Inv) + "%";

        /// <summary>Target game duration agreed in the GDD.</summary>
        public static (double min, double max) TargetMinutes(int players) =>
            players <= 2 ? (7, 10) : players <= 4 ? (11, 16) : (16, 21);

        public double AverageMinutes => Results.Count == 0 ? 0 : Results.Average(x => Settings.Time.EstimateSeconds(x)) / 60.0;

        static double Percentile(List<int> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            double idx = (sorted.Count - 1) * p;
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (idx - lo);
        }

        public string ToMarkdown()
        {
            var s = Settings;
            var r = s.Rules;
            var done = Results.Where(x => x.Finished).ToList();
            int n = Results.Count;
            var sb = new StringBuilder();

            sb.AppendLine("# Bite or Betray simulation report");
            sb.AppendLine();
            sb.AppendLine($"- Game: {n}{(Cancelled ? " (canceled before completion)" : "")}, completed normally: {done.Count}");
            sb.AppendLine($"- Players: {s.Seats.Length} bots ({string.Join(", ", s.Seats)}), seed {s.StartSeed}-{s.StartSeed + Math.Max(0, n - 1)}");
            sb.AppendLine($"- Failed decisions before fallback: {Results.Sum(x => x.FailedDecisions)}; most steps: {Results.Select(x => x.Steps).DefaultIfEmpty(0).Max()}");
            if (s.HumanProxyIndex >= 0)
                sb.AppendLine($"- Timing model: configuration seat {s.HumanProxyIndex + 1} treated as human; strategy remains a bot.");
            double extra = r.BadRatioPerExtraSeat * Math.Max(0, s.Seats.Length - 2);
            sb.AppendLine($"- Rules: HP {r.StartHp}/{r.MaxHp}, candy {r.CandiesPerAlivePlayer}/living player (min {r.MinCandies}, max {r.MaxCandies}), " +
                          $"bad ratio {Pct(r.MinBadRatio + extra)}-{Pct(r.MaxBadRatio + extra)} for {s.Seats.Length} seats, items {r.ItemsPerRound}/round (max {r.MaxItems})");
            sb.AppendLine();
            if (n == 0) return sb.ToString();

            // duration
            var turns = Results.Select(x => x.Turns).OrderBy(x => x).ToList();
            var rounds = Results.Select(x => x.Rounds).OrderBy(x => x).ToList();
            var firstOut = Results.Where(x => x.FirstEliminationTurn > 0).Select(x => x.FirstEliminationTurn).OrderBy(x => x).ToList();
            double avgTurns = turns.Average();
            sb.AppendLine("## Game duration");
            sb.AppendLine();
            sb.AppendLine("| | Average | Median | Min | P90 | Max |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
            sb.AppendLine($"| Turns | {F(avgTurns)} | {F(Percentile(turns, .5))} | {turns.First()} | {F(Percentile(turns, .9))} | {turns.Last()} |");
            sb.AppendLine($"| Rounds | {F(rounds.Average())} | {F(Percentile(rounds, .5))} | {rounds.First()} | {F(Percentile(rounds, .9))} | {rounds.Last()} |");
            if (firstOut.Count > 0)
                sb.AppendLine($"| First elimination turn | {F(firstOut.Average())} | {F(Percentile(firstOut, .5))} | {firstOut.First()} | {F(Percentile(firstOut, .9))} | {firstOut.Last()} |");
            var mins = Results.Select(x => (int)Math.Round(s.Time.EstimateSeconds(x))).OrderBy(x => x).ToList();
            double minutes = mins.Average() / 60.0;
            sb.AppendLine($"| Estimated VR minutes | {F(minutes)} | {F(Percentile(mins, .5) / 60)} | {F(mins.First() / 60.0)} | {F(Percentile(mins, .9) / 60)} | {F(mins.Last() / 60.0)} |");
            sb.AppendLine();
            sb.AppendLine($"Estimated minutes use a per-action timing model: opening and closing, waiter, dispenser, thinking {F(s.Time.ThinkPerDecision)} seconds per decision" +
                          (s.Time.TurnThinkLimit > 0 ? $" (max {F(s.Time.TurnThinkLimit)} seconds per turn)" : "") +
                          ", eating, feeding, and each item's duration." + (s.Time.AssumeAllHuman ? " All seats treated as human." : ""));
            sb.AppendLine($"Average {F(Results.Average(x => x.Stats.Turns.Count == 0 ? 0 : x.Stats.Turns.Average(t => t.Decisions)))} decisions per turn; " +
                          $"{Pct(Results.Sum(x => x.Stats.Turns.Count(t => s.Time.TurnThinkLimit > 0 && t.Decisions * s.Time.ThinkPerDecision > s.Time.TurnThinkLimit)) / (double)Math.Max(1, Results.Sum(x => x.Stats.Turns.Count)))} of turns would hit the timer limit.");
            var target = TargetMinutes(s.Seats.Length);
            sb.AppendLine($"Target for {s.Seats.Length} players: {F(target.min)}-{F(target.max)} minutes. Result: " +
                          (minutes < target.min ? "too short." : minutes > target.max ? "too long." : "within target."));
            if (firstOut.Count > 0)
                sb.AppendLine($"First elimination occurs on average at {Pct(firstOut.Average() / avgTurns)} of game duration.");
            sb.AppendLine();

            // winners by difficulty
            sb.AppendLine("## Winners by bot difficulty");
            sb.AppendLine();
            sb.AppendLine("| Difficulty | Seats | Wins | Win rate | Expected win rate |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (BotDifficulty d in Enum.GetValues(typeof(BotDifficulty)))
            {
                int seats = s.Seats.Count(x => x == d);
                if (seats == 0) continue;
                int wins = done.Count(x => x.WinnerDifficulty == d);
                sb.AppendLine($"| {d} | {seats} | {wins} | {Pct(done.Count == 0 ? 0 : (double)wins / done.Count)} | {Pct((double)seats / s.Seats.Length)} |");
            }
            sb.AppendLine();

            // winners by turn order
            sb.AppendLine("## Winners by turn order");
            sb.AppendLine();
            sb.AppendLine("d20 determines order, so position is independent of bot difficulty. Expected win rate per position: " + Pct(1.0 / s.Seats.Length) + ".");
            sb.AppendLine();
            sb.AppendLine("| Position | Wins | Win rate |");
            sb.AppendLine("| --- | --- | --- |");
            for (int i = 0; i < s.Seats.Length; i++)
            {
                int wins = done.Count(x => x.WinnerSeat == i);
                sb.AppendLine($"| {i + 1}{(i == 0 ? " (jalan pertama)" : "")} | {wins} | {Pct(done.Count == 0 ? 0 : (double)wins / done.Count)} |");
            }
            sb.AppendLine();
            if (done.Count > 0) sb.AppendLine($"Average winner's remaining HP: {F(done.Average(x => x.WinnerHp))}.");
            sb.AppendLine();

            // actions
            double Avg(Func<GameStats, int> f) => Results.Average(x => f(x.Stats));
            int eats = Results.Sum(x => x.Stats.EatSelf), gives = Results.Sum(x => x.Stats.Gives);
            sb.AppendLine("## Actions per game (average)");
            sb.AppendLine();
            sb.AppendLine("| Metric | Average | Notes |");
            sb.AppendLine("| --- | --- | --- |");
            sb.AppendLine($"| Candy drawn from dispenser | {F(Avg(x => x.CandiesDispensed))} | including Hammer and Gloves draws |");
            sb.AppendLine($"| Eat (self) | {F(Avg(x => x.EatSelf))} | {Pct(eats == 0 ? 0 : (double)Results.Sum(x => x.Stats.EatSelfGood) / eats)} good |");
            sb.AppendLine($"| Give (feed opponent) | {F(Avg(x => x.Gives))} | {Pct(gives == 0 ? 0 : (double)Results.Sum(x => x.Stats.GivesBad) / gives)} bad |");
            sb.AppendLine($"| HP healed | {F(Avg(x => x.HpHealed))} | from good candy and Hot Milk |");
            sb.AppendLine($"| HP lost | {F(Avg(x => x.HpLost))} | from bad candy |");
            sb.AppendLine($"| Paralysis applied | {F(Avg(x => x.ParalyzeApplied))} | turns skipped: {F(Avg(x => x.TurnsSkipped))} |");
            sb.AppendLine($"| Cupcake blocks | {F(Avg(x => x.CupcakeBlocks))} | |");
            sb.AppendLine($"| Clover triggers | {F(Avg(x => x.CloverTriggers))} | |");
            sb.AppendLine($"| Items discarded (inventory full) | {F(Avg(x => x.ItemsDiscardedFull))} | |");
            sb.AppendLine();

            sb.AppendLine("## Items activated per game (average)");
            sb.AppendLine();
            sb.AppendLine("| Item | Average |");
            sb.AppendLine("| --- | --- |");
            foreach (var it in ItemInfo.All)
                sb.AppendLine($"| {ItemInfo.Name(it)} | {F(Results.Average(x => x.Stats.ItemsUsed[(int)it]))} |");
            sb.AppendLine();

            // automatic notes
            var notes = new List<string>();
            int unfinished = n - done.Count;
            if (unfinished > 0) notes.Add($"{unfinished} games did not finish within {s.MaxStepsPerGame} steps. Possible loop; check seeds in the CSV.");
            if (minutes > target.max) notes.Add($"Games average {F(minutes)} minutes, above the {F(target.max)} minute target. Try increasing the bad ratio or reducing items per round.");
            if (minutes < target.min) notes.Add($"Games average {F(minutes)} minutes, below the {F(target.min)} minute target.");
            double healRatio = Avg(x => x.HpLost) == 0 ? 0 : Avg(x => x.HpHealed) / Avg(x => x.HpLost);
            if (healRatio > 0.6) notes.Add($"HP healed is about {Pct(healRatio)} of HP lost, so games tend to run long.");
            if (done.Count >= 50)
            {
                double first = (double)done.Count(x => x.WinnerSeat == 0) / done.Count;
                double fair = 1.0 / s.Seats.Length;
                if (Math.Abs(first - fair) > 0.08) notes.Add($"The first player wins {Pct(first)}, far from the expected {Pct(fair)}.");
            }
            if (notes.Count > 0)
            {
                sb.AppendLine("## Automatic notes");
                sb.AppendLine();
                foreach (var t in notes) sb.AppendLine("- " + t);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.Append("seed,players,finished,winner,winner_difficulty,winner_turn_position,winner_hp,turns,rounds,first_elimination_turn,");
            sb.Append("est_minutes,dispensed,eat,eat_good,give,give_bad,hp_healed,hp_lost,paralyze,cupcake_blocks,clover_triggers,items_discarded_full");
            foreach (var it in ItemInfo.All) sb.Append(",item_" + it);
            sb.AppendLine();
            foreach (var x in Results)
            {
                var t = x.Stats;
                sb.Append(string.Join(",", new object[]
                {
                    x.Seed, x.Players, x.Finished ? 1 : 0, x.WinnerName ?? "", x.Finished ? x.WinnerDifficulty.ToString() : "",
                    x.Finished ? x.WinnerSeat + 1 : 0, x.WinnerHp, x.Turns, x.Rounds, x.FirstEliminationTurn,
                    (Settings.Time.EstimateSeconds(x) / 60.0).ToString("0.00", Inv), t.CandiesDispensed, t.EatSelf, t.EatSelfGood, t.Gives, t.GivesBad, t.HpHealed, t.HpLost,
                    t.ParalyzeApplied, t.CupcakeBlocks, t.CloverTriggers, t.ItemsDiscardedFull
                }));
                foreach (var it in ItemInfo.All) sb.Append(",").Append(t.ItemsUsed[(int)it]);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
