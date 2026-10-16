using System;
using System.Collections.Generic;
using System.Linq;

namespace BiteOrBetray.Core
{
    /// <summary>
    /// Peringkat akhir untuk layar menang/kalah. Pemain yang masih hidup (pemenang) di atas, lalu pemain kalah dari yang
    /// paling lama bertahan, jadi urutan dari bawah = urutan kalah. Pemain yang kalah di giliran yang sama berbagi peringkat.
    /// </summary>
    public static class FinalStandings
    {
        public static List<(int place, PlayerState player)> Of(GameEngine engine) =>
            Rank(engine.Players.OrderBy(p => p.Seat), p => p.IsAlive ? int.MaxValue : p.EliminatedOnTurn);

        /// <summary>
        /// Urutkan menurun menurut giliran terakhir bertahan (int.MaxValue = masih hidup). Nilai sama berbagi peringkat
        /// (1, 2, 2, 4) dan tetap memakai urutan masukan.
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
