using System.Collections.Generic;
using System.Linq;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    /// <summary>Pemilihan room offline: jumlah bot, susunan kursi, tingkat kesulitan, dan SeatConfig untuk GameEngine.</summary>
    public class RoomSetupTests
    {
        static readonly string[] Names = { "Yellow", "Blue", "Red", "Mint", "Peach" };

        [Test]
        public void Default_AllAvailableBotsPlay()
        {
            var room = new RoomSetup();
            Assert.AreEqual(5, room.MaxBots);
            Assert.AreEqual(5, room.BotCount);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, room.ActiveSlots);
        }

        [Test]
        public void BotCount_ClampedToOneAndAvailableSlots()
        {
            var room = new RoomSetup();
            room.BotCount = 0;
            Assert.AreEqual(1, room.BotCount);
            room.BotCount = 9;
            Assert.AreEqual(5, room.BotCount);

            var partial = new RoomSetup(new[] { true, false, true, false, false });
            Assert.AreEqual(2, partial.MaxBots);
            partial.BotCount = 5;
            CollectionAssert.AreEqual(new[] { 0, 2 }, partial.ActiveSlots);
        }

        [Test]
        public void BotCount_UsesBalancedLayout()
        {
            var room = new RoomSetup();
            var expected = new[] { new[] { 2 }, new[] { 1, 3 }, new[] { 1, 2, 3 }, new[] { 0, 1, 3, 4 }, new[] { 0, 1, 2, 3, 4 } };
            for (int count = 1; count <= 5; count++)
            {
                room.BotCount = count;
                CollectionAssert.AreEqual(expected[count - 1], room.ActiveSlots, $"{count} bot");
                for (int slot = 0; slot < RoomSetup.SlotCount; slot++)
                    Assert.AreEqual(expected[count - 1].Contains(slot), room.IsActive(slot));
            }
        }

        [Test]
        public void BotCount_MissingPreferredSlotIsReplaced()
        {
            // Slot seberang (2) tidak ada di scene: 1 bot tetap dapat kursi lain.
            var room = new RoomSetup(new[] { true, true, false, true, true }) { BotCount = 1 };
            Assert.AreEqual(1, room.BotCount);
            Assert.IsFalse(room.IsActive(2));
            room.BotCount = 3;
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, room.ActiveSlots);
        }

        [Test]
        public void Difficulty_PerBotCyclesAndKeepsDefaults()
        {
            var room = new RoomSetup(null, new[] { BotDifficulty.Easy, BotDifficulty.Hard });
            Assert.AreEqual(BotDifficulty.Easy, room.DifficultyOf(0));
            Assert.AreEqual(BotDifficulty.Hard, room.DifficultyOf(1));
            Assert.AreEqual(BotDifficulty.Normal, room.DifficultyOf(4));
            room.CycleDifficulty(0);
            Assert.AreEqual(BotDifficulty.Normal, room.DifficultyOf(0));
            room.CycleDifficulty(1);
            Assert.AreEqual(BotDifficulty.Easy, room.DifficultyOf(1));
            // Tingkat per slot tidak hilang saat jumlah bot diubah.
            room.BotCount = 1;
            room.BotCount = 5;
            Assert.AreEqual(BotDifficulty.Easy, room.DifficultyOf(1));
        }

        [Test]
        public void Difficulty_GlobalOnlyCountsActiveBotsAndSetsAll()
        {
            var room = new RoomSetup(null, new[] { BotDifficulty.Easy, BotDifficulty.Hard, BotDifficulty.Hard, BotDifficulty.Hard, BotDifficulty.Easy });
            Assert.IsNull(room.GlobalDifficulty);
            room.BotCount = 3; // slot 1, 2, 3
            Assert.AreEqual(BotDifficulty.Hard, room.GlobalDifficulty);
            room.CycleGlobalDifficulty();
            Assert.AreEqual(BotDifficulty.Easy, room.GlobalDifficulty);
            room.BotCount = 5;
            Assert.AreEqual(BotDifficulty.Easy, room.GlobalDifficulty);

            room.CycleDifficulty(4);
            Assert.IsNull(room.GlobalDifficulty);
            room.CycleGlobalDifficulty(); // dari campuran mulai dari Easy
            Assert.AreEqual(BotDifficulty.Easy, room.GlobalDifficulty);
        }

        [Test]
        public void BuildSeats_HumanFirstThenActiveSlotsInOrder()
        {
            var room = new RoomSetup(null, new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard, BotDifficulty.Easy, BotDifficulty.Hard })
                { BotCount = 2 };
            var seats = room.BuildSeats("Kamu", Names);
            Assert.AreEqual(3, seats.Count);
            Assert.AreEqual("Kamu", seats[0].Name);
            Assert.IsFalse(seats[0].IsBot);
            Assert.AreEqual(new[] { "Blue", "Mint" }, seats.Skip(1).Select(s => s.Name).ToArray());
            Assert.IsTrue(seats.Skip(1).All(s => s.IsBot));
            Assert.AreEqual(new[] { BotDifficulty.Normal, BotDifficulty.Easy }, seats.Skip(1).Select(s => s.Difficulty).ToArray());

            var botsOnly = new RoomSetup().BuildSeats(null, null);
            Assert.AreEqual(5, botsOnly.Count);
            Assert.IsTrue(botsOnly.All(s => s.IsBot && !string.IsNullOrEmpty(s.Name)));
        }

        [Test]
        public void BuildSeats_EngineStartsWithChosenSeats()
        {
            for (int count = 1; count <= 5; count++)
            {
                var room = new RoomSetup { BotCount = count };
                room.CycleGlobalDifficulty(); // Normal -> Hard
                var seats = room.BuildSeats("Kamu", Names);
                var g = new GameEngine(new RuleConfig(), count);
                g.Start(seats);
                Assert.AreEqual(count + 1, g.Players.Count);
                Assert.AreEqual(1, g.Players.Count(p => !p.IsBot));
                foreach (var p in g.Players.Where(p => p.IsBot))
                {
                    Assert.AreEqual(BotDifficulty.Hard, p.Difficulty);
                    // ConfigIndex menunjuk kembali ke slot: index 0 = manusia, 1.. = ActiveSlots.
                    Assert.AreEqual(Names[room.ActiveSlots[p.ConfigIndex - 1]], p.Name);
                }
            }
        }

        [Test]
        public void BuildSeats_BotsPlayFullGameWithChosenDifficulty()
        {
            var room = new RoomSetup { BotCount = 3 };
            room.SetDifficulty(1, BotDifficulty.Easy);
            var seats = room.BuildSeats(null, Names);
            var g = new GameEngine(new RuleConfig(), 7);
            g.Start(seats);
            var rng = new System.Random(7);
            for (int step = 0; step < 2000 && !g.IsOver; step++) Assert.IsTrue(BotBrain.Step(g, rng).Success);
            Assert.IsTrue(g.IsOver);
            Assert.AreEqual(BotDifficulty.Easy, g.Players.Single(p => p.Name == "Blue").Difficulty);
        }
    }
}
