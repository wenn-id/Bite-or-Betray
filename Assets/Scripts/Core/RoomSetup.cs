using System;
using System.Collections.Generic;
using System.Linq;

namespace BiteOrBetray.Core
{
    /// <summary>
    /// Pilihan room offline sebelum game: jumlah bot (1-5) dan tingkat kesulitan per bot. Slot = kursi bot di meja
    /// (0 = Bot_Yellow ... 4 = Bot_Peach, searah jarum jam dari pemain); slot yang tidak terpilih dikosongkan.
    /// </summary>
    public sealed class RoomSetup
    {
        public const int SlotCount = 5;
        public const int MinBots = 1;

        // Susunan kursi per jumlah bot supaya meja tetap seimbang dilihat dari kursi pemain (slot 2 = tepat di seberang).
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

        /// <param name="availableSlots">Slot yang punya karakter bot di scene. Null = semua slot ada.</param>
        /// <param name="defaults">Tingkat awal per slot. Slot tanpa nilai = Normal.</param>
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

        /// <summary>Jumlah slot yang punya karakter (batas atas BotCount).</summary>
        public int MaxBots { get; }

        public int BotCount
        {
            get => activeSlots.Count;
            set
            {
                int count = Math.Max(Math.Min(MinBots, MaxBots), Math.Min(MaxBots, value));
                activeSlots.Clear();
                if (count == 0) return;
                // Utamakan susunan seimbang; slot yang tidak ada di scene diganti slot lain yang tersedia.
                foreach (int slot in Layouts[count - 1])
                    if (available[slot]) activeSlots.Add(slot);
                for (int slot = 0; slot < SlotCount && activeSlots.Count < count; slot++)
                    if (available[slot] && !activeSlots.Contains(slot)) activeSlots.Add(slot);
                activeSlots.Sort();
            }
        }

        /// <summary>Slot bot yang ikut main, urut naik. Urutan ini sama dengan urutan bot di BuildSeats.</summary>
        public IReadOnlyList<int> ActiveSlots => activeSlots;

        public bool IsActive(int slot) => activeSlots.Contains(slot);

        public BotDifficulty DifficultyOf(int slot) => difficulties[slot];

        public void SetDifficulty(int slot, BotDifficulty difficulty) => difficulties[slot] = difficulty;

        /// <summary>Easy > Normal > Hard > Easy.</summary>
        public static BotDifficulty Next(BotDifficulty d) => d == BotDifficulty.Hard ? BotDifficulty.Easy : d + 1;

        public void CycleDifficulty(int slot) => difficulties[slot] = Next(difficulties[slot]);

        /// <summary>Tingkat bersama semua bot yang ikut main; null kalau campuran.</summary>
        public BotDifficulty? GlobalDifficulty
        {
            get
            {
                if (activeSlots.Count == 0) return null;
                var first = difficulties[activeSlots[0]];
                return activeSlots.All(s => difficulties[s] == first) ? first : (BotDifficulty?)null;
            }
        }

        /// <summary>Samakan semua bot (termasuk yang belum ikut) ke tingkat berikutnya. Dari campuran mulai dari Easy.</summary>
        public void CycleGlobalDifficulty()
        {
            var next = GlobalDifficulty is BotDifficulty d ? Next(d) : BotDifficulty.Easy;
            for (int i = 0; i < SlotCount; i++) difficulties[i] = next;
        }

        /// <summary>
        /// Daftar peserta untuk GameEngine.Start: manusia di index 0 (kalau humanName tidak null), lalu bot di ActiveSlots
        /// sesuai urutan. slotNames diindeks per slot.
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
