using System;
using System.Collections.Generic;
using System.Linq;

namespace BiteOrBetray.Core
{
    /// <summary>
    /// Offline room choices before the game: bot count (1-5) and difficulty per bot. Slot = bot seat at the table
    /// (0 = Bot_Yellow ... 4 = Bot_Peach, clockwise from the player); unselected slots stay empty.
    /// </summary>
    public sealed class RoomSetup
    {
        public const int SlotCount = 5;
        public const int MinBots = 1;

        // Seat layouts per bot count keep the table balanced from the player's seat (slot 2 = directly opposite).
        static readonly int[][] Layouts =
        {
            new[] { 2 },
            new[] { 1, 3 },
            new[] { 1, 2, 3 },
            new[] { 0, 1, 3, 4 },
            new[] { 0, 1, 2, 3, 4 }
        };

        readonly bool[] available = new bool[SlotCount];
        readonly BotDifficulty[] difficulties = new BotDifficulty[SlotCount];
        readonly List<int> activeSlots = new List<int>();

        /// <param name="availableSlots">Slots with bot characters in the scene. Null = all slots exist.</param>
        /// <param name="defaults">Initial levels per slot. Slots without values default to Normal.</param>
        public RoomSetup(IList<bool> availableSlots = null, IList<BotDifficulty> defaults = null)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                available[i] = availableSlots == null || (i < availableSlots.Count && availableSlots[i]);
                difficulties[i] = defaults != null && i < defaults.Count ? defaults[i] : BotDifficulty.Normal;
            }
            MaxBots = available.Count(a => a);
            BotCount = MaxBots;
        }

        /// <summary>Number of slots with characters (upper limit for BotCount).</summary>
        public int MaxBots { get; }

        public int BotCount
        {
            get => activeSlots.Count;
            set
            {
                int count = Math.Max(Math.Min(MinBots, MaxBots), Math.Min(MaxBots, value));
                activeSlots.Clear();
                if (count == 0) return;
                // Prefer a balanced layout; replace slots missing from the scene with other available slots.
                foreach (int slot in Layouts[count - 1])
                    if (available[slot]) activeSlots.Add(slot);
                for (int slot = 0; slot < SlotCount && activeSlots.Count < count; slot++)
                    if (available[slot] && !activeSlots.Contains(slot)) activeSlots.Add(slot);
                activeSlots.Sort();
            }
        }

        /// <summary>Participating bot slots in ascending order. Matches bot order in BuildSeats.</summary>
        public IReadOnlyList<int> ActiveSlots => activeSlots;

        public bool IsActive(int slot) => activeSlots.Contains(slot);

        public BotDifficulty DifficultyOf(int slot) => difficulties[slot];

        public void SetDifficulty(int slot, BotDifficulty difficulty) => difficulties[slot] = difficulty;

        /// <summary>Easy > Normal > Hard > Easy.</summary>
        public static BotDifficulty Next(BotDifficulty d) => d == BotDifficulty.Hard ? BotDifficulty.Easy : d + 1;

        public void CycleDifficulty(int slot) => difficulties[slot] = Next(difficulties[slot]);

        /// <summary>Shared difficulty of all participating bots; null for mixed levels.</summary>
        public BotDifficulty? GlobalDifficulty
        {
            get
            {
                if (activeSlots.Count == 0) return null;
                var first = difficulties[activeSlots[0]];
                return activeSlots.All(s => difficulties[s] == first) ? first : (BotDifficulty?)null;
            }
        }

        /// <summary>Set every bot (including inactive ones) to the next level. Start at Easy for mixed levels.</summary>
        public void CycleGlobalDifficulty()
        {
            var next = GlobalDifficulty is BotDifficulty d ? Next(d) : BotDifficulty.Easy;
            for (int i = 0; i < SlotCount; i++) difficulties[i] = next;
        }

        /// <summary>
        /// Participants for GameEngine.Start: human at index 0 (if humanName is not null), then bots in ActiveSlots
        /// in order. slotNames is indexed by slot.
        /// </summary>
        public List<SeatConfig> BuildSeats(string humanName, IList<string> slotNames)
        {
            var seats = new List<SeatConfig>();
            if (humanName != null) seats.Add(new SeatConfig(humanName));
            foreach (int slot in activeSlots)
            {
                string name = slotNames != null && slot < slotNames.Count && !string.IsNullOrWhiteSpace(slotNames[slot])
                    ? slotNames[slot] : "Bot " + (slot + 1);
                seats.Add(new SeatConfig(name, true, difficulties[slot]));
            }
            return seats;
        }
    }
}
