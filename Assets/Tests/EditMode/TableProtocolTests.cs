using System;
using System.IO;
using System.Linq;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    public sealed class TableProtocolTests
    {
        static TableAuthority Create(int seed = 34, long revision = 0) => new TableAuthority(new RuleConfig(), seed,
            new[] {
                new TableSeat("Lokal", SeatController.Local),
                new TableSeat("Pemain online 🍬", SeatController.Online, 42),
                new TableSeat("Bot", SeatController.Bot, difficulty: BotDifficulty.Hard)
            }, revision);

        static TableSnapshot Snapshot(TableAuthority host) => TableSnapshot.Capture(host.Engine, host.Seats, host.Revision, 12.5f, true, host.GameId);

        [TestCase(BotActionKind.Eat)]
        [TestCase(BotActionKind.Give)]
        [TestCase(BotActionKind.UseItem)]
        [TestCase(BotActionKind.ChoosePocket)]
        [TestCase(BotActionKind.DiscardPocket)]
        public void ActionRoundTripPreservesArguments(BotActionKind kind)
        {
            var source = new TableAction { Revision = 3000000000L, Seat = 4, Decision = new BotDecision {
                Kind = kind, Index = 2, TargetSeat = 5, Args = new ItemArgs(3, 1)
            }};
            var copy = TableWire.ReadAction(TableWire.WriteAction(source));
            Assert.That(copy.Revision, Is.EqualTo(source.Revision));
            Assert.That(copy.Seat, Is.EqualTo(source.Seat));
            Assert.That(copy.Decision.Kind, Is.EqualTo(kind));
            Assert.That(copy.Decision.Index, Is.EqualTo(2));
            Assert.That(copy.Decision.TargetSeat, Is.EqualTo(5));
            Assert.That(copy.Decision.Args.TargetSeat, Is.EqualTo(3));
            Assert.That(copy.Decision.Args.TargetItemIndex, Is.EqualTo(1));
            source.Decision.Args = null;
            Assert.That(TableWire.ReadAction(TableWire.WriteAction(source)).Decision.Args, Is.Null);
        }

        [Test]
        public void SnapshotRoundTripPreservesPublicStateAndSeatMapping()
        {
            var host = Create();
            var g = host.Engine;
            g.DebugGiveItem(g.Current, ItemType.CloverCoin);
            g.DebugSetCupcake(g.Current, true);
            g.DebugSetParalyzed(g.Players[1], true);
            g.DebugSetHp(g.Players[2], 2);
            g.DebugGiveItem(g.Current, ItemType.LittlePocket);
            g.UseItem(g.Current.Items.Count - 1);
            var source = Snapshot(host);
            var copy = TableWire.ReadSnapshot(TableWire.WriteSnapshot(source));
            Assert.That(TableWire.WriteSnapshot(copy), Is.EqualTo(TableWire.WriteSnapshot(source)));
            Assert.That(copy.Revision, Is.EqualTo(host.Revision));
            Assert.That(copy.GameId, Is.EqualTo(host.GameId));
            Assert.That(copy.SecondsLeft, Is.EqualTo(12.5f));
            Assert.That(copy.InputOpen, Is.True);
            Assert.That(copy.Phase, Is.EqualTo(GamePhase.AwaitingPocketChoice));
            Assert.That(copy.RoundNumber, Is.EqualTo(g.RoundNumber));
            Assert.That(copy.TurnNumber, Is.EqualTo(g.TurnNumber));
            Assert.That(copy.Current.Seat, Is.EqualTo(g.CurrentSeat));
            Assert.That(copy.Config.MaxHp, Is.EqualTo(g.Config.MaxHp));
            Assert.That(copy.Config.TurnTimeLimitSeconds, Is.EqualTo(g.Config.TurnTimeLimitSeconds));
            Assert.That(copy.CandiesInDispenser, Is.EqualTo(g.CandiesInDispenser));
            Assert.That(copy.GoodLeft, Is.EqualTo(g.GoodLeft));
            Assert.That(copy.BadLeft, Is.EqualTo(g.BadLeft));
            Assert.That(copy.PocketOptions, Is.EqualTo(g.PocketOptions));
            Assert.That(copy.Announcement.ToString(), Is.EqualTo(g.Announcement.ToString()));
            foreach (var p in g.Players)
            {
                var view = copy.Players[p.Seat];
                Assert.That(view.ConfigIndex, Is.EqualTo(p.ConfigIndex));
                Assert.That(view.Name, Is.EqualTo(p.Name));
                Assert.That(view.IsBot, Is.EqualTo(p.IsBot));
                Assert.That(view.Difficulty, Is.EqualTo(p.Difficulty));
                Assert.That(view.D20Roll, Is.EqualTo(p.D20Roll));
                Assert.That(view.Hp, Is.EqualTo(p.Hp));
                Assert.That(view.HasCupcake, Is.EqualTo(p.HasCupcake));
                Assert.That(view.Paralyzed, Is.EqualTo(p.Paralyzed));
                Assert.That(view.Items, Is.EqualTo(p.Items));
                Assert.That(copy.Seats[p.ConfigIndex].Controller, Is.EqualTo(host.Seats[p.ConfigIndex].Controller));
                Assert.That(copy.Seats[p.ConfigIndex].ClientId, Is.EqualTo(host.Seats[p.ConfigIndex].ClientId));
            }
            var inventory = copy.Current.Items.ToArray();
            g.DebugGiveItem(g.Current, ItemType.HotMilk);
            Assert.That(copy.Current.Items, Is.EqualTo(inventory), "Snapshot tidak boleh merujuk inventory engine.");
            Assert.That(copy.Current, Is.Not.SameAs(g.Current));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SnapshotDoesNotLeakHiddenCandy(bool good)
        {
            var host = Create();
            var g = host.Engine;
            g.Config.RandomDraw = false;
            g.DebugSetPool(new[] { new Candy(CandyColor.Red, good), new Candy(CandyColor.Blue, !good) });
            var hidden = Snapshot(host);
            Assert.That(hidden.ActiveCandy.Id, Is.EqualTo(g.ActiveCandy.Id));
            Assert.That(hidden.ActiveCandy.Revealed, Is.False);
            Assert.That(hidden.ActiveCandy.IsGood, Is.False, "Good/bad tersembunyi harus disamarkan di wire.");
            g.DebugGiveItem(g.Current, ItemType.PlasticFork);
            g.UseItem(g.Current.Items.Count - 1);
            var revealed = Snapshot(host);
            Assert.That(revealed.ActiveCandy.Revealed, Is.True);
            Assert.That(revealed.ActiveCandy.IsGood, Is.EqualTo(good));
        }

        [Test]
        public void SnapshotRoundTripIncludesGameOverAndEliminationOrder()
        {
            var host = Create();
            var g = host.Engine;
            for (int guard = 0; guard < 1000 && !g.IsOver; guard++)
            {
                g.DebugSetHp(g.Current, 1);
                g.DebugSetPool(new[] { new Candy(CandyColor.Blue, false), new Candy(CandyColor.Yellow, true) });
                host.ExecuteHost(new BotDecision { Kind = BotActionKind.Eat });
            }
            var copy = TableWire.ReadSnapshot(TableWire.WriteSnapshot(Snapshot(host)));
            Assert.That(copy.IsOver, Is.True);
            Assert.That(copy.Winner.Seat, Is.EqualTo(g.Winner.Seat));
            Assert.That(copy.ActiveCandy, Is.Null);
            Assert.That(copy.Players.Select(p => p.EliminatedOnTurn), Is.EqualTo(g.Players.Select(p => p.EliminatedOnTurn)));
            Assert.That(copy.Players.Select(p => p.Eliminated), Is.EqualTo(g.Players.Select(p => p.Eliminated)));
        }

        static TableAuthority OnlineTurn()
        {
            // Cari hasil d20 yang menempatkan kursi online bukan pada ConfigIndex yang sama dengan Seat.
            for (int seed = 1; seed < 100; seed++)
            {
                var host = Create(seed);
                if (host.Engine.Current.ConfigIndex == 1) return host;
            }
            throw new Exception("Seed giliran online tidak ditemukan.");
        }

        [Test]
        public void HostRejectsSpoofedStaleWrongTurnAndReplayActions()
        {
            var host = OnlineTurn();
            var command = new TableAction { Revision = host.Revision, Seat = host.Engine.CurrentSeat,
                Decision = new BotDecision { Kind = BotActionKind.UseItem, Index = 999 } };
            Assert.That(host.ExecuteRemote(99, command).Success, Is.False);
            Assert.That(host.Revision, Is.EqualTo(1));
            command.Revision--;
            Assert.That(host.ExecuteRemote(42, command).Success, Is.False);
            command.Revision++;
            command.Seat = 2;
            Assert.That(host.ExecuteRemote(42, command).Success, Is.False);
            command.Seat = host.Engine.CurrentSeat;
            Assert.That(host.ExecuteRemote(42, command).Success, Is.False, "Item tidak ada harus ditolak engine.");
            Assert.That(host.Revision, Is.EqualTo(1));
            command.Decision = new BotDecision { Kind = BotActionKind.Eat };
            Assert.That(host.ExecuteRemote(42, command).Success, Is.True);
            Assert.That(host.Revision, Is.EqualTo(2));
            Assert.That(host.ExecuteRemote(42, command).Success, Is.False, "Duplikat paket tidak boleh mengeksekusi dua kali.");
        }

        [TestCase(SeatController.Local)]
        [TestCase(SeatController.Bot)]
        public void OnlineClientCannotControlLocalOrBotSeat(SeatController controller)
        {
            var host = new TableAuthority(new RuleConfig(), 34, new[] {
                new TableSeat("A", controller, 42), new TableSeat("B", controller, 42)
            });
            var command = new TableAction { Revision = host.Revision, Seat = host.Engine.CurrentSeat,
                Decision = new BotDecision { Kind = BotActionKind.Eat } };
            Assert.That(host.ExecuteRemote(42, command).Success, Is.False);
            Assert.That(host.ExecuteHost(command.Decision).Success, Is.True);
            Assert.That(host.Revision, Is.EqualTo(2));
        }

        [Test]
        public void HostTimeoutAndRestartInvalidateOldRevision()
        {
            var host = OnlineTurn();
            var action = new TableAction { Revision = host.Revision, Seat = host.Engine.CurrentSeat,
                Decision = new BotDecision { Kind = BotActionKind.Eat } };
            Assert.That(host.ExecuteHost(null, true).Success, Is.True);
            Assert.That(host.ExecuteRemote(42, action).Success, Is.False);
            var next = Create(revision: host.Revision);
            Assert.That(next.Revision, Is.GreaterThan(host.Revision));
            Assert.That(next.ExecuteRemote(42, action).Success, Is.False);
        }

        [Test]
        public void DisconnectedClientSeatIsTakenByBotAndGameContinues()
        {
            var host = OnlineTurn();
            var stale = new TableAction { Revision = host.Revision, Seat = host.Engine.CurrentSeat,
                Decision = new BotDecision { Kind = BotActionKind.Eat } };
            Assert.That(host.ReplaceWithBot(99), Is.EqualTo(-1), "Klien tanpa kursi tidak mengubah meja.");
            Assert.That(host.Revision, Is.EqualTo(1));
            Assert.That(host.ReplaceWithBot(42), Is.EqualTo(1));
            Assert.That(host.Revision, Is.EqualTo(2), "Revisi naik supaya klien lain menerima pemilik kursi baru.");
            Assert.That(host.Seats[1].Controller, Is.EqualTo(SeatController.Bot));
            Assert.That(host.Seats[1].Name, Is.EqualTo("Pemain online 🍬"));
            Assert.That(host.Engine.Current.IsBot, Is.True);
            stale.Revision = host.Revision;
            Assert.That(host.ExecuteRemote(42, stale).Success, Is.False, "Klien yang sudah putus tidak boleh lagi beraksi.");
            Assert.That(host.ReplaceWithBot(42), Is.EqualTo(-1), "Putus dua kali tidak menaikkan revisi lagi.");
            Assert.That(host.Revision, Is.EqualTo(2));
            var view = Snapshot(host);
            Assert.That(view.Seats[1].Controller, Is.EqualTo(SeatController.Bot));
            Assert.That(view.Players.Single(p => p.ConfigIndex == 1).IsBot, Is.True);
            // Bot brain host memainkan kursi itu sampai ada pemenang; tidak ada giliran yang menunggu klien.
            var rng = new Random(5);
            for (int step = 0; step < 2000 && !host.Engine.IsOver; step++)
            {
                var result = host.Engine.Current.IsBot ? host.ExecuteHost(BotBrain.Decide(host.Engine, rng)) : host.ExecuteHost(null, true);
                Assert.That(result.Success, Is.True, result.Error);
            }
            Assert.That(host.Engine.IsOver, Is.True);
        }

        [Test]
        public void DisconnectedClientIsBotInSeatsForNextGame()
        {
            var seats = new[] { new TableSeat("A", SeatController.Local), new TableSeat("B", SeatController.Online, 42),
                new TableSeat("C", SeatController.Online, 43) };
            Assert.That(TableSeat.ReplaceWithBot(seats, 43), Is.EqualTo(2));
            Assert.That(seats[2].Controller, Is.EqualTo(SeatController.Bot));
            Assert.That(seats[2].ClientId, Is.EqualTo(0UL));
            Assert.That(seats[1].Controller, Is.EqualTo(SeatController.Online));
            var next = new TableAuthority(new RuleConfig(), 34, seats);
            Assert.That(next.Engine.Players.Single(p => p.ConfigIndex == 2).IsBot, Is.True);
            Assert.That(next.Engine.Players.Single(p => p.ConfigIndex == 1).IsBot, Is.False);
        }

        [Test]
        public void HostCopiesBindingsAndRejectsDuplicateOnlineOwners()
        {
            var seats = new[] { new TableSeat("A", SeatController.Local), new TableSeat("B", SeatController.Online, 42) };
            var host = new TableAuthority(new RuleConfig(), 34, seats);
            seats[1].ClientId = 99;
            Assert.That(host.Seats[1].ClientId, Is.EqualTo(42));
            seats[0].Controller = SeatController.Online;
            seats[0].ClientId = 99;
            Assert.Throws<ArgumentException>(() => new TableAuthority(new RuleConfig(), 34, seats));
        }

        [Test]
        public void WireRejectsTruncatedUnknownVersionOversizedAndTrailingData()
        {
            var packet = TableWire.WriteAction(new TableAction { Revision = 1, Seat = 0,
                Decision = new BotDecision { Kind = BotActionKind.Eat } });
            Assert.Throws<EndOfStreamException>(() => TableWire.ReadAction(packet.Take(packet.Length - 1).ToArray()));
            Assert.Throws<InvalidDataException>(() => TableWire.ReadAction(packet.Concat(new byte[1]).ToArray()));
            packet[0] = 99;
            Assert.Throws<InvalidDataException>(() => TableWire.ReadAction(packet));
            Assert.Throws<InvalidDataException>(() => TableWire.ReadSnapshot(new byte[TableWire.MaxPacketBytes + 1]));
            var invalid = TableWire.WriteAction(new TableAction { Decision = new BotDecision { Kind = (BotActionKind)99 } });
            Assert.Throws<InvalidDataException>(() => TableWire.ReadAction(invalid));
            var snapshot = TableWire.WriteSnapshot(Snapshot(Create()));
            Assert.Throws<EndOfStreamException>(() => TableWire.ReadSnapshot(snapshot.Take(snapshot.Length - 1).ToArray()));
        }
    }
}
