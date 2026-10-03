using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BiteOrBetray.Core
{
    /// <summary>Penghitung kejadian dalam satu game.</summary>
    public sealed class GameStats
    {
        public int CandiesDispensed;
        public int EatSelf, EatSelfGood, EatSelfBad;
        public int Gives, GivesGood, GivesBad;
        public int CupcakeBlocks, CloverTriggers, ParalyzeApplied, TurnsSkipped;
        public int HpHealed, HpLost, Eliminations;
        public int ItemsDiscardedFull, PocketDiscarded, Timeouts;
        /// <summary>Jumlah item yang aktif per jenis, termasuk hasil curian Grappling Hook dan pilihan Little Pocket.</summary>
        public readonly int[] ItemsUsed = new int[ItemInfo.All.Length];
        /// <summary>Item yang keluar dari pembagian ronde per jenis (termasuk yang langsung dibuang karena inventory penuh).</summary>
        public readonly int[] ItemsSpawned = new int[ItemInfo.All.Length];
        public readonly int[] CandiesSpawnedByColor = new int[3];
        public readonly int[] GoodSpawnedByColor = new int[3];
        public readonly int[] BadSpawnedByColor = new int[3];
        /// <summary>Permen yang dimakan atau disuapkan (keluar dari permainan lewat mulut), per warna.</summary>
        public readonly int[] GoodConsumedByColor = new int[3];
        public readonly int[] BadConsumedByColor = new int[3];
        public readonly System.Collections.Generic.List<RoundRecord> Rounds = new System.Collections.Generic.List<RoundRecord>();
        public RoundRecord CurrentRound => Rounds.Count > 0 ? Rounds[Rounds.Count - 1] : null;
        public readonly System.Collections.Generic.List<TurnRecord> Turns = new System.Collections.Generic.List<TurnRecord>();
        public TurnRecord CurrentTurn => Turns.Count > 0 ? Turns[Turns.Count - 1] : null;
    }

    /// <summary>Catatan satu giliran: berapa keputusan yang diambil pemain di giliran itu.</summary>
    public sealed class TurnRecord
    {
        public int Round;
        public int Seat;
        public bool IsBot;
        public int Decisions;
        public bool TimedOut;
    }

    /// <summary>Catatan satu ronde untuk analisis.</summary>
    public sealed class RoundRecord
    {
        public int Number;
        public int AlivePlayers;
        public int Candies, Yellow, Blue, Red, Good, Bad;
        public int ItemsSpawned, ItemsReceived;
        /// <summary>Giliran yang dimulai di ronde ini (tidak termasuk giliran yang dilewati karena paralyzed).</summary>
        public int Turns;
        public int TurnsSkipped;
    }

    /// <summary>Perkiraan lama tiap kejadian di VR (detik). Angka awal, ganti dengan hasil telemetry playtest.</summary>
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
        /// <summary>True: semua kursi dihitung sebagai manusia (untuk balancing). False: kursi bot memakai BotThinkPerDecision.</summary>
        public bool AssumeAllHuman = true;
        /// <summary>Batas waktu berpikir per giliran (timer). 0 = tanpa batas.</summary>
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

        /// <summary>Total waktu berpikir dalam satu game, dengan batas timer per giliran.</summary>
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
        /// <summary>Model waktu per aksi untuk estimasi durasi di VR.</summary>
        public TimeModel Time = new TimeModel();
    }

    public sealed class GameResult
    {
        public int Seed;
        public int Players;
        public bool Finished;
        public string WinnerName;
        public BotDifficulty WinnerDifficulty;
        /// <summary>Posisi giliran pemenang (0 = jalan pertama).</summary>
        public int WinnerSeat;
        public int WinnerHp;
        public int Turns;
        public int Rounds;
        public int FirstEliminationTurn;
        public GameStats Stats;
    }

    public static class Simulator
    {
        /// <param name="progress">Dipanggil tiap beberapa game dengan (selesai, total). Kembalikan true untuk membatalkan.</param>
        public static SimulationReport Run(SimulationSettings s, Func<int, int, bool> progress = null)
        {
            if (s.Seats == null || s.Seats.Length < 2 || s.Seats.Length > 6)
                throw new ArgumentException("Jumlah kursi simulasi harus 2-6.");

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
            g.Start(s.Seats.Select((d, i) => new SeatConfig($"Kursi{i + 1}-{d}", true, d)).ToList());
            var rng = new Random(seed * 31 + 7);
            int steps = 0;
            while (!g.IsOver && steps < s.MaxStepsPerGame)
            {
                BotBrain.Step(g, rng);
                steps++;
            }

            var firstOut = g.Players.Where(p => p.Eliminated).Select(p => p.EliminatedOnTurn).DefaultIfEmpty(0).Min();
            var r = new GameResult
            {
                Seed = seed,
                Players = g.Players.Count,
                Finished = g.IsOver && g.Winner != null,
                Turns = g.TurnNumber,
                Rounds = g.RoundNumber,
                FirstEliminationTurn = firstOut,
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

        /// <summary>Target durasi per game yang disepakati di GDD.</summary>
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

            sb.AppendLine("# Laporan simulasi Bite or Betray");
            sb.AppendLine();
            sb.AppendLine($"- Game: {n}{(Cancelled ? " (dibatalkan sebelum selesai)" : "")}, selesai normal: {done.Count}");
            sb.AppendLine($"- Pemain: {s.Seats.Length} bot ({string.Join(", ", s.Seats)}), seed {s.StartSeed}-{s.StartSeed + Math.Max(0, n - 1)}");
            double extra = r.BadRatioPerExtraSeat * Math.Max(0, s.Seats.Length - 2);
            sb.AppendLine($"- Rules: HP {r.StartHp}/{r.MaxHp}, permen {r.CandiesPerAlivePlayer}/pemain hidup (min {r.MinCandies}, maks {r.MaxCandies}), " +
                          $"rasio bad {Pct(r.MinBadRatio + extra)}-{Pct(r.MaxBadRatio + extra)} untuk {s.Seats.Length} kursi, item {r.ItemsPerRound}/ronde (maks {r.MaxItems})");
            sb.AppendLine();
            if (n == 0) return sb.ToString();

            // durasi
            var turns = Results.Select(x => x.Turns).OrderBy(x => x).ToList();
            var rounds = Results.Select(x => x.Rounds).OrderBy(x => x).ToList();
            var firstOut = Results.Where(x => x.FirstEliminationTurn > 0).Select(x => x.FirstEliminationTurn).OrderBy(x => x).ToList();
            double avgTurns = turns.Average();
            sb.AppendLine("## Durasi game");
            sb.AppendLine();
            sb.AppendLine("| | Rata-rata | Median | Min | P90 | Maks |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
            sb.AppendLine($"| Giliran | {F(avgTurns)} | {F(Percentile(turns, .5))} | {turns.First()} | {F(Percentile(turns, .9))} | {turns.Last()} |");
            sb.AppendLine($"| Ronde | {F(rounds.Average())} | {F(Percentile(rounds, .5))} | {rounds.First()} | {F(Percentile(rounds, .9))} | {rounds.Last()} |");
            if (firstOut.Count > 0)
                sb.AppendLine($"| Giliran pemain pertama kalah | {F(firstOut.Average())} | {F(Percentile(firstOut, .5))} | {firstOut.First()} | {F(Percentile(firstOut, .9))} | {firstOut.Last()} |");
            var mins = Results.Select(x => (int)Math.Round(s.Time.EstimateSeconds(x))).OrderBy(x => x).ToList();
            double minutes = mins.Average() / 60.0;
            sb.AppendLine($"| Estimasi menit di VR | {F(minutes)} | {F(Percentile(mins, .5) / 60)} | {F(mins.First() / 60.0)} | {F(Percentile(mins, .9) / 60)} | {F(mins.Last() / 60.0)} |");
            sb.AppendLine();
            sb.AppendLine($"Estimasi menit memakai model waktu per aksi: pembuka dan penutup, pelayan, dispenser, berpikir {F(s.Time.ThinkPerDecision)} detik per keputusan" +
                          (s.Time.TurnThinkLimit > 0 ? $" (maks {F(s.Time.TurnThinkLimit)} detik per giliran)" : "") +
                          ", makan, menyuapi, dan durasi tiap item." + (s.Time.AssumeAllHuman ? " Semua kursi dihitung sebagai manusia." : ""));
            sb.AppendLine($"Rata-rata {F(Results.Average(x => x.Stats.Turns.Count == 0 ? 0 : x.Stats.Turns.Average(t => t.Decisions)))} keputusan per giliran; " +
                          $"{Pct(Results.Sum(x => x.Stats.Turns.Count(t => s.Time.TurnThinkLimit > 0 && t.Decisions * s.Time.ThinkPerDecision > s.Time.TurnThinkLimit)) / (double)Math.Max(1, Results.Sum(x => x.Stats.Turns.Count)))} giliran akan kena batas timer.");
            var target = TargetMinutes(s.Seats.Length);
            sb.AppendLine($"Target untuk {s.Seats.Length} pemain: {F(target.min)}-{F(target.max)} menit. Hasil: " +
                          (minutes < target.min ? "terlalu singkat." : minutes > target.max ? "terlalu lama." : "masuk target."));
            if (firstOut.Count > 0)
                sb.AppendLine($"Pemain pertama kalah rata-rata di {Pct(firstOut.Average() / avgTurns)} durasi game.");
            sb.AppendLine();

            // pemenang per difficulty
            sb.AppendLine("## Pemenang per tingkat bot");
            sb.AppendLine();
            sb.AppendLine("| Tingkat | Kursi | Menang | Win rate | Win rate wajar |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (BotDifficulty d in Enum.GetValues(typeof(BotDifficulty)))
            {
                int seats = s.Seats.Count(x => x == d);
                if (seats == 0) continue;
                int wins = done.Count(x => x.WinnerDifficulty == d);
                sb.AppendLine($"| {d} | {seats} | {wins} | {Pct(done.Count == 0 ? 0 : (double)wins / done.Count)} | {Pct((double)seats / s.Seats.Length)} |");
            }
            sb.AppendLine();

            // pemenang per urutan
            sb.AppendLine("## Pemenang per urutan giliran");
            sb.AppendLine();
            sb.AppendLine("Urutan ditentukan d20, jadi posisi tidak terikat ke tingkat bot. Win rate wajar per posisi: " + Pct(1.0 / s.Seats.Length) + ".");
            sb.AppendLine();
            sb.AppendLine("| Posisi | Menang | Win rate |");
            sb.AppendLine("| --- | --- | --- |");
            for (int i = 0; i < s.Seats.Length; i++)
            {
                int wins = done.Count(x => x.WinnerSeat == i);
                sb.AppendLine($"| {i + 1}{(i == 0 ? " (jalan pertama)" : "")} | {wins} | {Pct(done.Count == 0 ? 0 : (double)wins / done.Count)} |");
            }
            sb.AppendLine();
            if (done.Count > 0) sb.AppendLine($"Sisa HP pemenang rata-rata: {F(done.Average(x => x.WinnerHp))}.");
            sb.AppendLine();

            // aksi
            double Avg(Func<GameStats, int> f) => Results.Average(x => f(x.Stats));
            int eats = Results.Sum(x => x.Stats.EatSelf), gives = Results.Sum(x => x.Stats.Gives);
            sb.AppendLine("## Aksi per game (rata-rata)");
            sb.AppendLine();
            sb.AppendLine("| Hal | Rata-rata | Catatan |");
            sb.AppendLine("| --- | --- | --- |");
            sb.AppendLine($"| Permen keluar dari dispenser | {F(Avg(x => x.CandiesDispensed))} | termasuk hasil Hammer dan Gloves |");
            sb.AppendLine($"| Eat (makan sendiri) | {F(Avg(x => x.EatSelf))} | {Pct(eats == 0 ? 0 : (double)Results.Sum(x => x.Stats.EatSelfGood) / eats)} good |");
            sb.AppendLine($"| Give (suapi lawan) | {F(Avg(x => x.Gives))} | {Pct(gives == 0 ? 0 : (double)Results.Sum(x => x.Stats.GivesBad) / gives)} bad |");
            sb.AppendLine($"| HP pulih | {F(Avg(x => x.HpHealed))} | dari permen good dan Hot Milk |");
            sb.AppendLine($"| HP hilang | {F(Avg(x => x.HpLost))} | dari permen bad |");
            sb.AppendLine($"| Paralyzed terjadi | {F(Avg(x => x.ParalyzeApplied))} | giliran dilewati: {F(Avg(x => x.TurnsSkipped))} |");
            sb.AppendLine($"| Cupcake menahan efek | {F(Avg(x => x.CupcakeBlocks))} | |");
            sb.AppendLine($"| Clover tergandakan | {F(Avg(x => x.CloverTriggers))} | |");
            sb.AppendLine($"| Item terbuang (inventory penuh) | {F(Avg(x => x.ItemsDiscardedFull))} | |");
            sb.AppendLine();

            sb.AppendLine("## Item aktif per game (rata-rata)");
            sb.AppendLine();
            sb.AppendLine("| Item | Rata-rata |");
            sb.AppendLine("| --- | --- |");
            foreach (var it in ItemInfo.All)
                sb.AppendLine($"| {ItemInfo.Name(it)} | {F(Results.Average(x => x.Stats.ItemsUsed[(int)it]))} |");
            sb.AppendLine();

            // catatan otomatis
            var notes = new List<string>();
            int unfinished = n - done.Count;
            if (unfinished > 0) notes.Add($"{unfinished} game tidak selesai dalam {s.MaxStepsPerGame} langkah. Kemungkinan ada loop, cek seed di CSV.");
            if (minutes > target.max) notes.Add($"Game rata-rata {F(minutes)} menit, di atas target {F(target.max)} menit. Coba naikkan rasio bad atau kurangi item per ronde.");
            if (minutes < target.min) notes.Add($"Game rata-rata {F(minutes)} menit, di bawah target {F(target.min)} menit.");
            double healRatio = Avg(x => x.HpLost) == 0 ? 0 : Avg(x => x.HpHealed) / Avg(x => x.HpLost);
            if (healRatio > 0.6) notes.Add($"HP yang pulih sekitar {Pct(healRatio)} dari HP yang hilang, sehingga game cenderung panjang.");
            if (done.Count >= 50)
            {
                double first = (double)done.Count(x => x.WinnerSeat == 0) / done.Count;
                double fair = 1.0 / s.Seats.Length;
                if (Math.Abs(first - fair) > 0.08) notes.Add($"Pemain yang jalan pertama menang {Pct(first)}, jauh dari wajar {Pct(fair)}.");
            }
            if (notes.Count > 0)
            {
                sb.AppendLine("## Catatan otomatis");
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
