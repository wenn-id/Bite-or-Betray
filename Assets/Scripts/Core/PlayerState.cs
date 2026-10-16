using System.Collections.Generic;

namespace BiteOrBetray.Core
{
    public sealed class PlayerState
    {
        internal readonly List<ItemType> items = new List<ItemType>();

        internal PlayerState(string name, bool isBot, BotDifficulty difficulty, int hp)
        {
            Name = name;
            IsBot = isBot;
            Difficulty = difficulty;
            Hp = hp;
        }

        /// <summary>Posisi duduk sekaligus urutan giliran (0 = jalan pertama).</summary>
        public int Seat { get; internal set; }
        /// <summary>Index peserta di daftar SeatConfig yang diberikan ke Start (sebelum diurutkan d20).</summary>
        public int ConfigIndex { get; internal set; }
        public string Name { get; }
        public bool IsBot { get; }
        public BotDifficulty Difficulty { get; }
        public int D20Roll { get; internal set; }

        public int Hp { get; internal set; }
        public bool Paralyzed { get; internal set; }
        public bool HasCupcake { get; internal set; }
        public bool Eliminated { get; internal set; }
        /// <summary>Nomor giliran (TurnNumber) saat pemain ini kalah. 0 kalau belum kalah.</summary>
        public int EliminatedOnTurn { get; internal set; }

        public bool IsAlive => !Eliminated;
        public IReadOnlyList<ItemType> Items => items;

        public override string ToString() => Name;
    }
}
