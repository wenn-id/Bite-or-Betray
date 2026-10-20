using System.Linq;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    /// <summary>Win/lose screen ranking: winner on top, then losers ordered from the one who survived longest.</summary>
    public class FinalStandingsTests
    {
        [Test]
        public void Rank_AliveFirstThenLaterEliminationHigher()
        {
            var ranked = FinalStandings.Rank(new[] { ("A", 5), ("B", int.MaxValue), ("C", 12), ("D", 9) }, x => x.Item2);
            CollectionAssert.AreEqual(new[] { "B", "C", "D", "A" }, ranked.Select(r => r.item.Item1).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, ranked.Select(r => r.place).ToArray());
        }

        [Test]
        public void Rank_SameTurnSharesPlaceAndKeepsInputOrder()
        {
            var ranked = FinalStandings.Rank(new[] { ("A", 7), ("B", int.MaxValue), ("C", 7), ("D", 3) }, x => x.Item2);
            CollectionAssert.AreEqual(new[] { "B", "A", "C", "D" }, ranked.Select(r => r.item.Item1).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 2, 4 }, ranked.Select(r => r.place).ToArray());

            // No winner (the last two players lose at the same time): both rank 1.
            var draw = FinalStandings.Rank(new[] { ("A", 4), ("B", 9), ("C", 9) }, x => x.Item2);
            CollectionAssert.AreEqual(new[] { 1, 1, 3 }, draw.Select(r => r.place).ToArray());
        }

        [Test]
        public void Of_FullBotGame_WinnerFirstAndEveryoneRanked()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                var g = new GameEngine(new RuleConfig(), seed);
                g.Start(new RoomSetup { BotCount = 4 }.BuildSeats(null, null));
                var rng = new System.Random(seed);
                for (int step = 0; step < 2000 && !g.IsOver; step++) Assert.IsTrue(BotBrain.Step(g, rng).Success);
                Assert.IsTrue(g.IsOver);

                var standings = FinalStandings.Of(g);
                Assert.AreEqual(g.Players.Count, standings.Count);
                CollectionAssert.AreEquivalent(g.Players, standings.Select(s => s.player));
                if (g.Winner != null)
                {
                    Assert.AreSame(g.Winner, standings[0].player);
                    Assert.AreEqual(2, standings[1].place);
                }
                for (int i = 1; i < standings.Count; i++)
                {
                    Assert.IsTrue(standings[i].player.Eliminated);
                    Assert.LessOrEqual(standings[i].player.EliminatedOnTurn, standings[i - 1].player.IsAlive
                        ? int.MaxValue : standings[i - 1].player.EliminatedOnTurn);
                }
            }
        }
    }
}
