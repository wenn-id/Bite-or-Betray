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

        /// <summary>Seat position and turn order (0 = first turn).</summary>
        public int Seat { get; internal set; }
        /// <summary>Participant index in the SeatConfig list passed to Start (before d20 sorting).</summary>
        public int ConfigIndex { get; internal set; }
        public string Name { get; }
        /// <summary>Can become true during a game when an online player disconnects and a bot takes over their seat (BOB-037).</summary>
        public bool IsBot { get; internal set; }
        public BotDifficulty Difficulty { get; }
        public int D20Roll { get; internal set; }

        public int Hp { get; internal set; }
        public bool Paralyzed { get; internal set; }
        public bool HasCupcake { get; internal set; }
        public bool Eliminated { get; internal set; }
        /// <summary>Turn number (TurnNumber) when this player was eliminated. 0 if not eliminated.</summary>
        public int EliminatedOnTurn { get; internal set; }

        public bool IsAlive => !Eliminated;
        public IReadOnlyList<ItemType> Items => items;

        public override string ToString() => Name;
    }
}
