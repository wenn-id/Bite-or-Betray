using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BiteOrBetray.Core
{
    /// <summary>Summary of one bot combination in the simulation matrix.</summary>
    public sealed class MatrixRow
    {
        public int Players;
        public string Combo;
        public int Games;
        public double AvgMin, MedMin, P10Min, P90Min, TargetMin, TargetMax;
        public bool InTarget;
        public double AvgTurns, AvgRounds, MedRounds, P90Rounds, MaxRounds, TurnsPerRound, SkippedPerMatch;
        public double CandiesPerRound, YellowPerRound, BluePerRound, RedPerRound, GoodPerRound, BadPerRound;
        public double CandiesPerMatch, YellowPerMatch, BluePerMatch, RedPerMatch, GoodPerMatch, BadPerMatch;
        public double[] GoodByColorPerMatch = new double[3], BadByColorPerMatch = new double[3];
        public double ItemsSpawnedPerRound, ItemsSpawnedPerMatch, ItemsReceivedPerMatch, ItemsDiscardedPerMatch, ItemsUsedPerMatch;
        public double[] ItemSpawnShare = new double[ItemInfo.All.Length];
        public double[] ItemSpawnedPerMatch = new double[ItemInfo.All.Length];
        public double[] ItemUsedPerMatch = new double[ItemInfo.All.Length];
        public double EatPerMatch, EatGoodPct, GivePerMatch, GiveBadPct, ParalyzePerMatch, CupcakeBlocksPerMatch, CloverPerMatch;
        public double HpHealedPerMatch, HpLostPerMatch;
        public double FirstOutFrac, WinnerHp, FirstSeatWinRate;
        /// <summary>Win rate by seat for each level (NaN if that level is absent from the combination).</summary>
        public double WinEasy = double.NaN, WinNormal = double.NaN, WinHard = double.NaN;
        public double FairWin;
    }

    /// <summary>Round k profile for one player count (combining all combinations).</summary>
    public sealed class RoundProfileRow
    {
        public int Players, Round;
        public double ReachedPct, AlivePlayers, Candies, Yellow, Blue, Red, Good, Bad, Turns, ItemsSpawned;
    }

    public static class SimulationMatrix
    {
        static readonly BotDifficulty[] Levels = { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard };

        /// <summary>All bot level combinations (ignoring order) for n players.</summary>
        public static List<BotDifficulty[]> Combos(int players)
        {
            var list = new List<BotDifficulty[]>();
            void Rec(int start, List<BotDifficulty> cur)
            {
                if (cur.Count == players)
                {
                    list.Add(cur.ToArray());
                    return;
                }
                for (int i = start; i < Levels.Length; i++)
                {
                    cur.Add(Levels[i]);
                    Rec(i, cur);
                    cur.RemoveAt(cur.Count - 1);
                }
            }
            Rec(0, new List<BotDifficulty>());
            return list;
        }

        public static string ComboName(BotDifficulty[] combo) => string.Join(" + ", combo);

        public static MatrixRow Summarize(SimulationReport rep)
        {
            var s = rep.Settings;
            var res = rep.Results;
            int n = res.Count;
            var row = new MatrixRow { Players = s.Seats.Length, Combo = ComboName(s.Seats), Games = n };
            if (n == 0) return row;

            var mins = res.Select(x => s.Time.EstimateSeconds(x) / 60.0).OrderBy(x => x).ToList();
            var t = SimulationReport.TargetMinutes(row.Players);
            row.AvgMin = mins.Average();
            row.MedMin = mins[mins.Count / 2];
            row.P10Min = mins[mins.Count / 10];
            row.P90Min = mins[Math.Min(mins.Count - 1, mins.Count * 9 / 10)];
            row.TargetMin = t.min;
            row.TargetMax = t.max;
            row.InTarget = row.AvgMin >= t.min && row.AvgMin <= t.max;

            var rounds = res.Select(x => x.Rounds).OrderBy(x => x).ToList();
            row.AvgTurns = res.Average(x => x.Turns);
            row.AvgRounds = rounds.Average();
            row.MedRounds = rounds[rounds.Count / 2];
            row.P90Rounds = rounds[Math.Min(rounds.Count - 1, rounds.Count * 9 / 10)];
            row.MaxRounds = rounds.Last();
            var allRounds = res.SelectMany(x => x.Stats.Rounds).ToList();
            double totalRounds = allRounds.Count;
            row.TurnsPerRound = allRounds.Average(r => r.Turns);
            row.SkippedPerMatch = res.Average(x => x.Stats.TurnsSkipped);

            row.CandiesPerRound = allRounds.Average(r => r.Candies);
            row.YellowPerRound = allRounds.Average(r => r.Yellow);
            row.BluePerRound = allRounds.Average(r => r.Blue);
            row.RedPerRound = allRounds.Average(r => r.Red);
            row.GoodPerRound = allRounds.Average(r => r.Good);
            row.BadPerRound = allRounds.Average(r => r.Bad);
            row.CandiesPerMatch = res.Average(x => x.Stats.CandiesSpawnedByColor.Sum());
            row.YellowPerMatch = res.Average(x => x.Stats.CandiesSpawnedByColor[0]);
            row.BluePerMatch = res.Average(x => x.Stats.CandiesSpawnedByColor[1]);
            row.RedPerMatch = res.Average(x => x.Stats.CandiesSpawnedByColor[2]);
            row.GoodPerMatch = res.Average(x => x.Stats.GoodSpawnedByColor.Sum());
            row.BadPerMatch = res.Average(x => x.Stats.BadSpawnedByColor.Sum());
            for (int c = 0; c < 3; c++)
            {
                row.GoodByColorPerMatch[c] = res.Average(x => x.Stats.GoodSpawnedByColor[c]);
                row.BadByColorPerMatch[c] = res.Average(x => x.Stats.BadSpawnedByColor[c]);
            }

            row.ItemsSpawnedPerRound = allRounds.Average(r => r.ItemsSpawned);
            row.ItemsSpawnedPerMatch = res.Average(x => x.Stats.ItemsSpawned.Sum());
            row.ItemsReceivedPerMatch = allRounds.Sum(r => r.ItemsReceived) / (double)n;
            row.ItemsDiscardedPerMatch = res.Average(x => x.Stats.ItemsDiscardedFull);
            row.ItemsUsedPerMatch = res.Average(x => x.Stats.ItemsUsed.Sum());
            double spawnedTotal = res.Sum(x => x.Stats.ItemsSpawned.Sum());
            foreach (var it in ItemInfo.All)
            {
                int i = (int)it;
                row.ItemSpawnedPerMatch[i] = res.Average(x => x.Stats.ItemsSpawned[i]);
                row.ItemUsedPerMatch[i] = res.Average(x => x.Stats.ItemsUsed[i]);
                row.ItemSpawnShare[i] = spawnedTotal == 0 ? 0 : res.Sum(x => x.Stats.ItemsSpawned[i]) / spawnedTotal;
            }

            int eats = res.Sum(x => x.Stats.EatSelf), gives = res.Sum(x => x.Stats.Gives);
            row.EatPerMatch = res.Average(x => x.Stats.EatSelf);
            row.EatGoodPct = eats == 0 ? 0 : res.Sum(x => x.Stats.EatSelfGood) / (double)eats;
            row.GivePerMatch = res.Average(x => x.Stats.Gives);
            row.GiveBadPct = gives == 0 ? 0 : res.Sum(x => x.Stats.GivesBad) / (double)gives;
            row.ParalyzePerMatch = res.Average(x => x.Stats.ParalyzeApplied);
            row.CupcakeBlocksPerMatch = res.Average(x => x.Stats.CupcakeBlocks);
            row.CloverPerMatch = res.Average(x => x.Stats.CloverTriggers);
            row.HpHealedPerMatch = res.Average(x => x.Stats.HpHealed);
            row.HpLostPerMatch = res.Average(x => x.Stats.HpLost);

            row.FirstOutFrac = res.Where(x => x.FirstEliminationTurn > 0 && x.Turns > 0)
                .Select(x => (double)x.FirstEliminationTurn / x.Turns).DefaultIfEmpty(0).Average();
            var done = res.Where(x => x.Finished).ToList();
            row.WinnerHp = done.Count == 0 ? 0 : done.Average(x => x.WinnerHp);
            row.FirstSeatWinRate = done.Count == 0 ? 0 : done.Count(x => x.WinnerSeat == 0) / (double)done.Count;
            row.FairWin = 1.0 / row.Players;
            foreach (var d in Levels)
            {
                int seats = s.Seats.Count(x => x == d);
                if (seats == 0 || done.Count == 0) continue;
                double wr = done.Count(x => x.WinnerDifficulty == d) / (double)done.Count / seats;
                if (d == BotDifficulty.Easy) row.WinEasy = wr;
                else if (d == BotDifficulty.Normal) row.WinNormal = wr;
                else row.WinHard = wr;
            }
            return row;
        }

        public static List<RoundProfileRow> RoundProfile(int players, IEnumerable<SimulationReport> reports, int maxRound = 12)
        {
            var games = reports.SelectMany(r => r.Results).ToList();
            var rows = new List<RoundProfileRow>();
            if (games.Count == 0) return rows;
            for (int k = 1; k <= maxRound; k++)
            {
                var recs = games.Where(g => g.Stats.Rounds.Count >= k).Select(g => g.Stats.Rounds[k - 1]).ToList();
                if (recs.Count == 0) break;
                rows.Add(new RoundProfileRow
                {
                    Players = players,
                    Round = k,
                    ReachedPct = recs.Count / (double)games.Count,
                    AlivePlayers = recs.Average(r => r.AlivePlayers),
                    Candies = recs.Average(r => r.Candies),
                    Yellow = recs.Average(r => r.Yellow),
                    Blue = recs.Average(r => r.Blue),
                    Red = recs.Average(r => r.Red),
                    Good = recs.Average(r => r.Good),
                    Bad = recs.Average(r => r.Bad),
                    Turns = recs.Average(r => r.Turns),
                    ItemsSpawned = recs.Average(r => r.ItemsSpawned)
                });
            }
            return rows;
        }

        /// <summary>Run all bot level combinations for each player count.</summary>
        /// <param name="progress">(completed combinations, total combinations) -> true to cancel.</param>
        public static List<SimulationReport> Run(IEnumerable<int> playerCounts, int gamesPerCombo, RuleConfig rules, TimeModel time,
            int startSeed = 1, Func<int, int, bool> progress = null)
        {
            var combos = playerCounts.SelectMany(Combos).ToList();
            var reports = new List<SimulationReport>();
            for (int i = 0; i < combos.Count; i++)
            {
                if (progress != null && progress(i, combos.Count)) break;
                reports.Add(Simulator.Run(new SimulationSettings
                {
                    Games = gamesPerCombo, Seats = combos[i], Rules = rules.Clone(), Time = time.Clone(), StartSeed = startSeed
                }));
            }
            return reports;
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string N(double v) => double.IsNaN(v) ? "" : v.ToString("0.###", Inv);

        public static string ToCsv(IEnumerable<MatrixRow> rows)
        {
            var sb = new StringBuilder();
            var head = new List<string>
            {
                "players", "combo", "games", "avg_min", "median_min", "p10_min", "p90_min", "target_min", "target_max", "in_target",
                "avg_turns", "avg_rounds", "median_rounds", "p90_rounds", "max_rounds", "turns_per_round", "skipped_turns_per_match",
                "candies_per_round", "yellow_per_round", "blue_per_round", "red_per_round", "good_per_round", "bad_per_round",
                "candies_per_match", "yellow_per_match", "blue_per_match", "red_per_match", "good_per_match", "bad_per_match",
                "yellow_good_per_match", "yellow_bad_per_match", "blue_good_per_match", "blue_bad_per_match", "red_good_per_match", "red_bad_per_match",
                "items_spawned_per_round", "items_spawned_per_match", "items_received_per_match", "items_discarded_per_match", "items_used_per_match"
            };
            foreach (var it in ItemInfo.All) head.Add("spawn_share_" + it);
            foreach (var it in ItemInfo.All) head.Add("used_per_match_" + it);
            head.AddRange(new[]
            {
                "eat_per_match", "eat_good_pct", "give_per_match", "give_bad_pct", "paralyze_per_match", "cupcake_blocks_per_match",
                "clover_per_match", "hp_healed_per_match", "hp_lost_per_match", "first_out_frac", "winner_hp", "first_seat_win",
                "win_per_seat_easy", "win_per_seat_normal", "win_per_seat_hard", "fair_win"
            });
            sb.AppendLine(string.Join(",", head));
            foreach (var r in rows)
            {
                var v = new List<string>
                {
                    r.Players.ToString(), r.Combo, r.Games.ToString(), N(r.AvgMin), N(r.MedMin), N(r.P10Min), N(r.P90Min), N(r.TargetMin), N(r.TargetMax),
                    r.InTarget ? "1" : "0", N(r.AvgTurns), N(r.AvgRounds), N(r.MedRounds), N(r.P90Rounds), N(r.MaxRounds), N(r.TurnsPerRound), N(r.SkippedPerMatch),
                    N(r.CandiesPerRound), N(r.YellowPerRound), N(r.BluePerRound), N(r.RedPerRound), N(r.GoodPerRound), N(r.BadPerRound),
                    N(r.CandiesPerMatch), N(r.YellowPerMatch), N(r.BluePerMatch), N(r.RedPerMatch), N(r.GoodPerMatch), N(r.BadPerMatch),
                    N(r.GoodByColorPerMatch[0]), N(r.BadByColorPerMatch[0]), N(r.GoodByColorPerMatch[1]), N(r.BadByColorPerMatch[1]),
                    N(r.GoodByColorPerMatch[2]), N(r.BadByColorPerMatch[2]),
                    N(r.ItemsSpawnedPerRound), N(r.ItemsSpawnedPerMatch), N(r.ItemsReceivedPerMatch), N(r.ItemsDiscardedPerMatch), N(r.ItemsUsedPerMatch)
                };
                foreach (var it in ItemInfo.All) v.Add(N(r.ItemSpawnShare[(int)it]));
                foreach (var it in ItemInfo.All) v.Add(N(r.ItemUsedPerMatch[(int)it]));
                v.AddRange(new[]
                {
                    N(r.EatPerMatch), N(r.EatGoodPct), N(r.GivePerMatch), N(r.GiveBadPct), N(r.ParalyzePerMatch), N(r.CupcakeBlocksPerMatch),
                    N(r.CloverPerMatch), N(r.HpHealedPerMatch), N(r.HpLostPerMatch), N(r.FirstOutFrac), N(r.WinnerHp), N(r.FirstSeatWinRate),
                    N(r.WinEasy), N(r.WinNormal), N(r.WinHard), N(r.FairWin)
                });
                sb.AppendLine(string.Join(",", v));
            }
            return sb.ToString();
        }

        public static string RoundProfileCsv(IEnumerable<RoundProfileRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("players,round,reached_pct,alive_players,candies,yellow,blue,red,good,bad,turns,items_spawned");
            foreach (var r in rows)
                sb.AppendLine(string.Join(",", r.Players, r.Round, N(r.ReachedPct), N(r.AlivePlayers), N(r.Candies), N(r.Yellow), N(r.Blue),
                    N(r.Red), N(r.Good), N(r.Bad), N(r.Turns), N(r.ItemsSpawned)));
            return sb.ToString();
        }
    }
}
