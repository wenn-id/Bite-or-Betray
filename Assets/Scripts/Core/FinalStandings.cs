using System;
using System.Collections.Generic;
using System.Linq;

namespace BiteOrBetray.Core
{
    /// <summary>
    /// Final standings for the win/loss screen. Surviving players (winners) come first, then eliminated players
    /// by survival time, so the order from the bottom matches elimination order. Players eliminated on the same turn share a rank.
    /// </summary>
    public static class FinalStandings
    {
        public static List<(int place, PlayerState player)> Of(GameEngine engine) =>
            Rank(engine.Players.OrderBy(p => p.Seat), p => p.IsAlive ? int.MaxValue : p.EliminatedOnTurn);

        /// <summary>
        /// Sort by last surviving turn in descending order (int.MaxValue = still alive). Ties share ranks
        /// (1, 2, 2, 4) and retain input order.
        /// </summary>
        public static List<(int place, T item)> Rank<T>(IEnumerable<T> items, Func<T, int> survivedUntil)
        {
            var sorted = items.Select((item, i) => (item, i, turn: survivedUntil(item)))
                .OrderByDescending(x => x.turn).ThenBy(x => x.i).ToList();
            var result = new List<(int place, T item)>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                int place = i > 0 && sorted[i].turn == sorted[i - 1].turn ? result[i - 1].place : i + 1;
                result.Add((place, sorted[i].item));
            }
            return result;
        }
    }
}
