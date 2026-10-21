using System;
using System.Linq;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    /// <summary>Online room BOB-035: numeric room code, keypad, start requirements, and empty seats filled by bots.</summary>
    public class OnlineRoomTests
    {
        static readonly string[] BotNames = { "Yellow", "Blue", "Red", "Mint", "Peach" };

        // "Player N" names follow the host language; tests use the default language.
        [SetUp, TearDown]
        public void English() => Localization.Language = Language.English;

        [Test]
        public void NewCode_IsSixDigitsWithoutLeadingZero()
        {
            var rng = new Random(5);
            for (int i = 0; i < 500; i++)
            {
                string code = OnlineRoom.NewCode(rng);
                Assert.IsTrue(OnlineRoom.IsValidCode(code), code);
                Assert.AreNotEqual('0', code[0], code);
            }
        }

        [Test]
        public void IsValidCode_RejectsWrongLengthAndLetters()
        {
            Assert.IsTrue(OnlineRoom.IsValidCode("012345"));
            Assert.IsFalse(OnlineRoom.IsValidCode(null));
            Assert.IsFalse(OnlineRoom.IsValidCode(""));
            Assert.IsFalse(OnlineRoom.IsValidCode("12345"));
            Assert.IsFalse(OnlineRoom.IsValidCode("1234567"));
            Assert.IsFalse(OnlineRoom.IsValidCode("12A456"));
            Assert.IsFalse(OnlineRoom.IsValidCode("123 45"));
        }

        [Test]
        public void FormatCode_SplitsIntoTwoGroups()
        {
            Assert.AreEqual("482 913", OnlineRoom.FormatCode("482913"));
            Assert.AreEqual("abc", OnlineRoom.FormatCode("abc"));
            Assert.AreEqual("", OnlineRoom.FormatCode(null));
        }

        [Test]
        public void CanStart_NeedsTwoHumansWithinSeats()
        {
            Assert.IsFalse(OnlineRoom.CanStart(1));
            Assert.IsTrue(OnlineRoom.CanStart(2));
            Assert.IsTrue(OnlineRoom.CanStart(6));
            Assert.IsFalse(OnlineRoom.CanStart(7));
            Assert.IsFalse(OnlineRoom.CanStart(5, 4));
        }

        [Test]
        public void BuildSeats_HostThenClientsThenBotsFillEmptySeats()
        {
            var difficulties = new[] { BotDifficulty.Easy, BotDifficulty.Hard, BotDifficulty.Normal, BotDifficulty.Hard, BotDifficulty.Easy };
            var seats = OnlineRoom.BuildSeats(new ulong[] { 3, 1 }, 6, BotNames, difficulties);

            Assert.AreEqual(6, seats.Count);
            Assert.AreEqual(SeatController.Local, seats[0].Controller);
            Assert.AreEqual(SeatController.Online, seats[1].Controller);
            Assert.AreEqual(3UL, seats[1].ClientId);
            Assert.AreEqual(1UL, seats[2].ClientId);
            CollectionAssert.AreEqual(new[] { "Player 1", "Player 2", "Player 3", "Yellow", "Blue", "Red" }, seats.Select(s => s.Name));
            Assert.IsTrue(seats.Skip(3).All(s => s.Controller == SeatController.Bot));
            CollectionAssert.AreEqual(difficulties.Take(3), seats.Skip(3).Select(s => s.Difficulty));
        }

        [Test]
        public void BuildSeats_FullRoomHasNoBotsAndSmallTableFillsToSeatCount()
        {
            var full = OnlineRoom.BuildSeats(new ulong[] { 1, 2, 3, 4, 5 }, 6, BotNames);
            Assert.IsFalse(full.Any(s => s.Controller == SeatController.Bot));

            var small = OnlineRoom.BuildSeats(new ulong[] { 1 }, 3);
            CollectionAssert.AreEqual(new[] { "Player 1", "Player 2", "Bot 1" }, small.Select(s => s.Name));
            Assert.AreEqual(BotDifficulty.Normal, small[2].Difficulty);
        }

        [Test]
        public void BuildSeats_RenamesBotThatCollidesWithPlayerName()
        {
            var seats = OnlineRoom.BuildSeats(new ulong[] { 1 }, 4, new[] { "Player 2", "", "Blue" });
            CollectionAssert.AreEqual(new[] { "Player 1", "Player 2", "Player 2 2", "Bot 2" }, seats.Select(s => s.Name));
        }

        [Test]
        public void BuildSeats_RejectsTooFewHumansOrInvalidTables()
        {
            Assert.Throws<ArgumentException>(() => OnlineRoom.BuildSeats(new ulong[0], 6));
            Assert.Throws<ArgumentException>(() => OnlineRoom.BuildSeats(new ulong[] { 1, 1 }, 6));
            Assert.Throws<ArgumentException>(() => OnlineRoom.BuildSeats(new ulong[] { 1, 2 }, 2));
            Assert.Throws<ArgumentException>(() => OnlineRoom.BuildSeats(new ulong[] { 1 }, 7));
            Assert.Throws<ArgumentNullException>(() => OnlineRoom.BuildSeats(null, 6));
        }

        [Test]
        public void RoomCodeEntry_AcceptsSixDigitsAndBackspace()
        {
            var entry = new RoomCodeEntry();
            Assert.AreEqual("___ ___", entry.Display);
            Assert.IsFalse(entry.Add('a'));
            Assert.IsFalse(entry.Backspace());
            foreach (char c in "4829") Assert.IsTrue(entry.Add(c));
            Assert.AreEqual("482 9__", entry.Display);
            Assert.IsFalse(entry.Complete);
            Assert.IsTrue(entry.Backspace());
            foreach (char c in "913") entry.Add(c);
            Assert.IsTrue(entry.Complete);
            Assert.AreEqual("482913", entry.Code);
            Assert.IsFalse(entry.Add('7'));
            Assert.AreEqual("482 913", entry.Display);
            entry.Clear();
            Assert.AreEqual("", entry.Code);
        }
    }
}
