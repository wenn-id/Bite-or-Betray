using System;
using System.Globalization;
using System.IO;
using System.Linq;
using BiteOrBetray.Core;

namespace BiteOrBetray.DebugTools
{
    /// <summary>
    /// Mencatat setiap keputusan pemain ke CSV: berapa detik pemain berpikir sebelum beraksi.
    /// Dipakai untuk mengkalibrasi TimeModel di Simulator (menu Bite or Betray > Simulator > Kalibrasi dari telemetry).
    /// Dipakai bersama oleh BiteOrBetrayDebugGame dan CandyTableDirector (VR).
    /// </summary>
    public sealed class TelemetryRecorder
    {
        public const string Header =
            "session,seed,players,round,turn,seat,player,is_bot,action,detail,think_seconds,turn_think_seconds,hp_after,timestamp";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        readonly string session;
        readonly int seed;
        readonly DateTime startedAt = DateTime.Now;

        public string FilePath { get; }

        public TelemetryRecorder(string folder, int seed, int players)
        {
            Directory.CreateDirectory(folder);
            this.seed = seed;
            session = DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
            FilePath = Path.Combine(folder, $"bob_telemetry_{session}_{players}p.csv");
            File.WriteAllText(FilePath, Header + Environment.NewLine);
        }

        public void Record(GameEngine g, PlayerState actor, string action, string detail, double thinkSeconds, double turnThinkSeconds)
        {
            Append(g, actor, action, detail, thinkSeconds, turnThinkSeconds);
        }

        public void Finish(GameEngine g)
        {
            double total = (DateTime.Now - startedAt).TotalSeconds;
            Append(g, g.Winner, "GAME_OVER",
                $"rounds={g.RoundNumber};turns={g.TurnNumber};winner={(g.Winner != null ? g.Winner.Name : "-")};real_seconds={total.ToString("0.0", Inv)}",
                0, 0);
        }

        void Append(GameEngine g, PlayerState p, string action, string detail, double think, double turnThink)
        {
            string Clean(string s) => (s ?? "").Replace(",", ";").Replace("\n", " ");
            var line = string.Join(",", new[]
            {
                session, seed.ToString(Inv), g.Players.Count.ToString(Inv), g.RoundNumber.ToString(Inv), g.TurnNumber.ToString(Inv),
                p != null ? p.Seat.ToString(Inv) : "", Clean(p?.Name), p != null && p.IsBot ? "1" : "0", action, Clean(detail),
                think.ToString("0.00", Inv), turnThink.ToString("0.00", Inv), p != null ? p.Hp.ToString(Inv) : "",
                DateTime.Now.ToString("s", Inv)
            });
            File.AppendAllText(FilePath, line + Environment.NewLine);
        }

        /// <summary>Rata-rata detik berpikir per keputusan pemain manusia dari semua CSV telemetry di folder.</summary>
        public static bool TryAverageHumanThink(string folder, out double average, out int samples, out int timeouts)
        {
            average = 0;
            samples = 0;
            timeouts = 0;
            if (!Directory.Exists(folder)) return false;
            double sum = 0;
            foreach (var file in Directory.GetFiles(folder, "bob_telemetry_*.csv"))
            {
                foreach (var line in File.ReadLines(file).Skip(1))
                {
                    var c = line.Split(',');
                    if (c.Length < 12 || c[7] != "0") continue;
                    if (c[8] == "TIMEOUT") timeouts++;
                    if (c[8] == "GAME_OVER" || c[8] == "TIMEOUT") continue;
                    if (!double.TryParse(c[10], NumberStyles.Float, Inv, out double t)) continue;
                    sum += t;
                    samples++;
                }
            }
            if (samples == 0) return false;
            average = sum / samples;
            return true;
        }
    }
}
