using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BiteOrBetray.Core
{
    /// <summary>
    /// Aturan room online (BOB-035) tanpa jaringan: kode room angka untuk keypad di meja, syarat host boleh mulai, dan
    /// susunan kursi (host lokal, klien online urut masuk, sisa kursi diisi bot).
    /// </summary>
    public static class OnlineRoom
    {
        public const int CodeLength = 6;
        public const int MinHumans = 2;
        public const int MaxSeats = 6;

        /// <summary>Kode acak 6 angka. Angka pertama bukan 0 supaya tidak terbaca seperti kode yang terpotong.</summary>
        public static string NewCode(Random rng)
        {
            var code = new StringBuilder(CodeLength);
            code.Append((char)('1' + rng.Next(9)));
            while (code.Length < CodeLength) code.Append((char)('0' + rng.Next(10)));
            return code.ToString();
        }

        public static bool IsValidCode(string code) => code != null && code.Length == CodeLength && code.All(c => c >= '0' && c <= '9');

        /// <summary>"482913" tampil "482 913": dua kelompok lebih mudah dibaca dan didiktekan di VR.</summary>
        public static string FormatCode(string code) =>
            IsValidCode(code) ? code.Substring(0, CodeLength / 2) + " " + code.Substring(CodeLength / 2) : code ?? "";

        public static bool CanStart(int humans, int seatCount = MaxSeats) => humans >= MinHumans && humans <= Math.Min(seatCount, MaxSeats);

        /// <summary>
        /// Kursi untuk NetworkTableSession.StartTable: host di kursi pertama, klien online sesuai urutan, lalu bot sampai
        /// seatCount. Bot memakai nama dan tingkat per slot meja; nama kembar diberi nomor. Nama "Player N" memakai bahasa host.
        /// </summary>
        public static List<TableSeat> BuildSeats(IList<ulong> onlineClients, int seatCount, IList<string> botNames = null,
            IList<BotDifficulty> difficulties = null)
        {
            if (onlineClients == null) throw new ArgumentNullException(nameof(onlineClients));
            if (seatCount < MinHumans || seatCount > MaxSeats) throw new ArgumentException($"Meja online butuh {MinHumans}-{MaxSeats} kursi.");
            int humans = onlineClients.Count + 1;
            if (onlineClients.Distinct().Count() != onlineClients.Count) throw new ArgumentException("Klien online kembar.");
            if (!CanStart(humans, seatCount))
                throw new ArgumentException(humans < MinHumans ? Strings.NeedHumans.Format(MinHumans) : Strings.TooManyPlayers.Text);

            var seats = new List<TableSeat> { new TableSeat(Strings.PlayerNumber.Format(1), SeatController.Local) };
            for (int i = 0; i < onlineClients.Count; i++) seats.Add(new TableSeat(Strings.PlayerNumber.Format(i + 2), SeatController.Online, onlineClients[i]));
            var used = new HashSet<string>(seats.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
            for (int slot = 0; seats.Count < seatCount; slot++)
            {
                string original = botNames != null && slot < botNames.Count && !string.IsNullOrWhiteSpace(botNames[slot]) ? botNames[slot] : $"Bot {slot + 1}";
                string name = original;
                for (int n = 2; !used.Add(name); n++) name = $"{original} {n}";
                var difficulty = difficulties != null && slot < difficulties.Count ? difficulties[slot] : BotDifficulty.Normal;
                seats.Add(new TableSeat(name, SeatController.Bot, 0, difficulty));
            }
            return seats;
        }
    }

    /// <summary>Isi keypad angka diegetic: maksimal 6 angka, Hapus satu angka, dan tampilan sisa kotak kosong.</summary>
    public sealed class RoomCodeEntry
    {
        readonly StringBuilder digits = new StringBuilder(OnlineRoom.CodeLength);

        public string Code => digits.ToString();
        public bool Complete => digits.Length == OnlineRoom.CodeLength;

        /// <summary>False kalau bukan angka atau kode sudah penuh.</summary>
        public bool Add(char digit)
        {
            if (digit < '0' || digit > '9' || Complete) return false;
            digits.Append(digit);
            return true;
        }

        public bool Backspace()
        {
            if (digits.Length == 0) return false;
            digits.Length--;
            return true;
        }

        public void Clear() => digits.Length = 0;

        /// <summary>"48_ ___" untuk kode yang baru terisi dua angka; pengelompokan sama dengan FormatCode.</summary>
        public string Display
        {
            get
            {
                var text = new StringBuilder();
                for (int i = 0; i < OnlineRoom.CodeLength; i++)
                {
                    if (i == OnlineRoom.CodeLength / 2) text.Append(' ');
                    text.Append(i < digits.Length ? digits[i] : '_');
                }
                return text.ToString();
            }
        }
    }
}
