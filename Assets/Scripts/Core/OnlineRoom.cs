using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BiteOrBetray.Core
{
    /// <summary>
    /// Online room rules (BOB-035) without networking: numeric room codes for the table keypad, host start requirements, and
    /// seat layout (local host, online clients in join order, remaining seats filled by bots).
    /// </summary>
    public static class OnlineRoom
    {
        public const int CodeLength = 6;
        public const int MinHumans = 2;
        public const int MaxSeats = 6;

        /// <summary>Random 6-digit code. The first digit is nonzero so the code does not look truncated.</summary>
        public static string NewCode(Random rng)
        {
            var code = new StringBuilder(CodeLength);
            code.Append((char)('1' + rng.Next(9)));
            while (code.Length < CodeLength) code.Append((char)('0' + rng.Next(10)));
            return code.ToString();
        }

        public static bool IsValidCode(string code) => code != null && code.Length == CodeLength && code.All(c => c >= '0' && c <= '9');

        /// <summary>Display "482913" as "482 913": two groups are easier to read and dictate in VR.</summary>
        public static string FormatCode(string code) =>
            IsValidCode(code) ? code.Substring(0, CodeLength / 2) + " " + code.Substring(CodeLength / 2) : code ?? "";

        public static bool CanStart(int humans, int seatCount = MaxSeats) => humans >= MinHumans && humans <= Math.Min(seatCount, MaxSeats);

        /// <summary>
        /// Seats for NetworkTableSession.StartTable: host first, online clients in order, then bots up to
        /// seatCount. Bots use per-slot names and levels; duplicate names get numbers. "Player N" uses the host language.
        /// </summary>
        public static List<TableSeat> BuildSeats(IList<ulong> onlineClients, int seatCount, IList<string> botNames = null,
            IList<BotDifficulty> difficulties = null)
        {
            if (onlineClients == null) throw new ArgumentNullException(nameof(onlineClients));
            if (seatCount < MinHumans || seatCount > MaxSeats) throw new ArgumentException($"Online table needs {MinHumans}-{MaxSeats} seats.");
            int humans = onlineClients.Count + 1;
            if (onlineClients.Distinct().Count() != onlineClients.Count) throw new ArgumentException("Duplicate online clients.");
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

    /// <summary>Diegetic numeric keypad content: at most 6 digits, delete one digit, and display remaining empty slots.</summary>
    public sealed class RoomCodeEntry
    {
        readonly StringBuilder digits = new StringBuilder(OnlineRoom.CodeLength);

        public string Code => digits.ToString();
        public bool Complete => digits.Length == OnlineRoom.CodeLength;

        /// <summary>False for a non-digit or a full code.</summary>
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

        /// <summary>"48_ ___" for a code with two digits entered; grouping matches FormatCode.</summary>
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
