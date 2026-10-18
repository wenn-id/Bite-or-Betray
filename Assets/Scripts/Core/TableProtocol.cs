using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BiteOrBetray.Core
{
    /// <summary>State baca bersama untuk engine host dan tampilan klien; tidak menyediakan aksi atau RNG.</summary>
    public interface ITableState
    {
        IReadOnlyList<PlayerState> Players { get; }
        GamePhase Phase { get; }
        bool IsOver { get; }
        int RoundNumber { get; }
        int TurnNumber { get; }
        PlayerState Current { get; }
        PlayerState Winner { get; }
        Candy ActiveCandy { get; }
        bool CloverOnActive { get; }
        RoundAnnouncement Announcement { get; }
        int CandiesInDispenser { get; }
        int GoodLeft { get; }
        int BadLeft { get; }
        IReadOnlyList<ItemType> PocketOptions { get; }
        RuleConfig Config { get; }
    }

    public enum SeatController { Local, Online, Bot }

    public sealed class TableSeat
    {
        public string Name;
        public SeatController Controller;
        public ulong ClientId;
        public BotDifficulty Difficulty;

        public TableSeat(string name, SeatController controller, ulong clientId = 0, BotDifficulty difficulty = BotDifficulty.Normal)
        {
            Name = name;
            Controller = controller;
            ClientId = clientId;
            Difficulty = difficulty;
        }

        /// <summary>Kursi online milik klien yang putus menjadi bot (BOB-037). Index kursi, -1 kalau klien tidak punya kursi.</summary>
        public static int ReplaceWithBot(IReadOnlyList<TableSeat> seats, ulong clientId)
        {
            for (int i = 0; seats != null && i < seats.Count; i++)
            {
                if (seats[i].Controller != SeatController.Online || seats[i].ClientId != clientId) continue;
                seats[i].Controller = SeatController.Bot;
                seats[i].ClientId = 0;
                return i;
            }
            return -1;
        }
    }

    /// <summary>Kursi aksi adalah urutan d20; pemilik kursi dicari melalui ConfigIndex, bukan nama pemain.</summary>
    public sealed class TableAction
    {
        public long Revision;
        public int Seat;
        public BotDecision Decision;
    }

    /// <summary>Salinan info publik saja. Isi permen yang belum terungkap dan urutan dispenser tidak dikirim.</summary>
    public sealed class TableSnapshot : ITableState
    {
        public long Revision;
        public long GameId;
        public float SecondsLeft;
        public bool InputOpen;
        public TableSeat[] Seats;
        public IReadOnlyList<PlayerState> Players { get; internal set; }
        public GamePhase Phase { get; internal set; }
        public bool IsOver => Phase == GamePhase.GameOver;
        public int RoundNumber { get; internal set; }
        public int TurnNumber { get; internal set; }
        public int CurrentSeat { get; internal set; }
        public int WinnerSeat { get; internal set; } = -1;
        public PlayerState Current => Players[CurrentSeat];
        public PlayerState Winner => WinnerSeat < 0 ? null : Players[WinnerSeat];
        public Candy ActiveCandy { get; internal set; }
        public bool CloverOnActive { get; internal set; }
        public RoundAnnouncement Announcement { get; internal set; }
        public int CandiesInDispenser { get; internal set; }
        public int GoodLeft { get; internal set; }
        public int BadLeft { get; internal set; }
        public IReadOnlyList<ItemType> PocketOptions { get; internal set; }
        public RuleConfig Config { get; internal set; }

        public static TableSnapshot Capture(GameEngine engine, IReadOnlyList<TableSeat> seats, long revision, float secondsLeft, bool inputOpen, long gameId = 0)
        {
            // Lewat codec supaya snapshot tidak memegang inventory, aturan, atau Candy milik engine.
            return TableWire.ReadSnapshot(TableWire.WriteSnapshot(new TableSnapshot {
                Revision = revision, GameId = gameId, SecondsLeft = secondsLeft, InputOpen = inputOpen, Seats = seats.ToArray(),
                Players = engine.Players, Phase = engine.Phase, RoundNumber = engine.RoundNumber,
                TurnNumber = engine.TurnNumber, CurrentSeat = engine.CurrentSeat, WinnerSeat = engine.Winner?.Seat ?? -1,
                ActiveCandy = engine.ActiveCandy, CloverOnActive = engine.CloverOnActive, Announcement = engine.Announcement,
                CandiesInDispenser = engine.CandiesInDispenser, GoodLeft = engine.GoodLeft, BadLeft = engine.BadLeft,
                PocketOptions = engine.PocketOptions, Config = engine.Config
            }));
        }
    }

    /// <summary>Pintu aturan host: satu revisi per aksi berhasil, termasuk bot dan timeout.</summary>
    public sealed class TableAuthority
    {
        public GameEngine Engine { get; }
        public IReadOnlyList<TableSeat> Seats { get; }
        public long Revision { get; private set; }
        public long GameId { get; }

        public TableAuthority(RuleConfig config, int seed, IReadOnlyList<TableSeat> seats, long revision = 0)
        {
            if (seats == null || seats.Count < 2 || seats.Count > 6) throw new ArgumentException("Butuh 2-6 kursi.");
            if (seats.Any(s => s == null || !Enum.IsDefined(typeof(SeatController), s.Controller)
                || string.IsNullOrWhiteSpace(s.Name) || System.Text.Encoding.UTF8.GetByteCount(s.Name) > TableWire.MaxNameBytes))
                throw new ArgumentException("Konfigurasi kursi tidak valid.");
            if (seats.Where(s => s.Controller == SeatController.Online).GroupBy(s => s.ClientId).Any(g => g.Count() > 1))
                throw new ArgumentException("Satu klien online hanya boleh punya satu kursi.");
            Seats = seats.Select(s => new TableSeat(s.Name, s.Controller, s.ClientId, s.Difficulty)).ToArray();
            Engine = new GameEngine(config.Clone(), seed);
            Engine.Start(Seats.Select(s => new SeatConfig(s.Name, s.Controller == SeatController.Bot, s.Difficulty)).ToArray());
            Revision = revision + 1;
            GameId = Revision;
        }

        public ActionResult ValidateRemote(ulong sender, TableAction action)
        {
            if (action == null || action.Decision == null || !Enum.IsDefined(typeof(BotActionKind), action.Decision.Kind))
                return ActionResult.Fail(Strings.UnknownAction.Text);
            if (action.Revision != Revision) return ActionResult.Fail(Strings.StaleAction.Text);
            if (Engine.IsOver || action.Seat != Engine.CurrentSeat) return ActionResult.Fail(Strings.NotYourTurn.Text);
            var seat = Seats[Engine.Current.ConfigIndex];
            return seat.Controller == SeatController.Online && seat.ClientId == sender
                ? ActionResult.Ok : ActionResult.Fail(Strings.SeatNotSender.Text);
        }

        /// <summary>
        /// Klien putus: kursinya dimainkan bot sampai game selesai, aksi lama dari klien itu ditolak, dan revisi naik supaya
        /// snapshot berikutnya membawa perubahan pemilik kursi. Index kursi, -1 kalau klien tidak punya kursi.
        /// </summary>
        public int ReplaceWithBot(ulong clientId)
        {
            int config = TableSeat.ReplaceWithBot(Seats, clientId);
            if (config < 0) return -1;
            Engine.Players.First(p => p.ConfigIndex == config).IsBot = true;
            Revision++;
            return config;
        }

        public ActionResult ExecuteRemote(ulong sender, TableAction action)
        {
            var valid = ValidateRemote(sender, action);
            return valid.Success ? ExecuteHost(action.Decision) : valid;
        }

        public ActionResult ExecuteHost(BotDecision decision, bool timeout = false)
        {
            if (!timeout && (decision == null || !Enum.IsDefined(typeof(BotActionKind), decision.Kind)))
                return ActionResult.Fail(Strings.UnknownAction.Text);
            var result = timeout ? Engine.TimeoutTurn() : BotBrain.Execute(Engine, decision);
            if (result.Success) Revision++;
            return result;
        }
    }

    /// <summary>Format biner berversi, tanpa Unity/NGO; panjang koleksi dibatasi sebelum alokasi.</summary>
    public static class TableWire
    {
        const int Version = 1;
        public const int MaxNameBytes = 128;
        public const int MaxPacketBytes = 32768;

        static byte[] Write(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                write(writer);
                if (stream.Length > MaxPacketBytes) throw new InvalidDataException("Paket terlalu besar.");
                return stream.ToArray();
            }
        }

        static T Read<T>(byte[] bytes, Func<BinaryReader, T> read)
        {
            if (bytes == null || bytes.Length > MaxPacketBytes) throw new InvalidDataException("Ukuran paket tidak valid.");
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != Version) throw new InvalidDataException("Versi protokol berbeda.");
                var result = read(reader);
                if (stream.Position != stream.Length) throw new InvalidDataException("Ada data sisa.");
                return result;
            }
        }

        static int Count(BinaryReader r, int max)
        {
            int count = r.ReadInt32();
            if (count < 0 || count > max) throw new InvalidDataException("Panjang koleksi tidak valid.");
            return count;
        }

        static T EnumValue<T>(BinaryReader r) where T : struct
        {
            int value = r.ReadInt32();
            if (!Enum.IsDefined(typeof(T), value)) throw new InvalidDataException("Nilai enum tidak valid.");
            return (T)Enum.ToObject(typeof(T), value);
        }

        static void Items(BinaryWriter w, IReadOnlyList<ItemType> items)
        {
            w.Write(items.Count);
            foreach (var item in items) w.Write((int)item);
        }

        static ItemType[] Items(BinaryReader r, int max)
        {
            var items = new ItemType[Count(r, max)];
            for (int i = 0; i < items.Length; i++) items[i] = EnumValue<ItemType>(r);
            return items;
        }

        public static byte[] WriteAction(TableAction action) => Write(w => {
            w.Write(action.Revision); w.Write(action.Seat);
            var d = action.Decision;
            w.Write((int)d.Kind); w.Write(d.Index); w.Write(d.TargetSeat); w.Write(d.Args != null);
            if (d.Args != null) { w.Write(d.Args.TargetSeat); w.Write(d.Args.TargetItemIndex); }
        });

        public static TableAction ReadAction(byte[] bytes) => Read(bytes, r => {
            var action = new TableAction { Revision = r.ReadInt64(), Seat = r.ReadInt32(), Decision = new BotDecision {
                Kind = EnumValue<BotActionKind>(r), Index = r.ReadInt32(), TargetSeat = r.ReadInt32()
            }};
            if (r.ReadBoolean()) action.Decision.Args = new ItemArgs(r.ReadInt32(), r.ReadInt32());
            return action;
        });

        public static byte[] WriteSnapshot(TableSnapshot s) => Write(w => {
            w.Write(s.Revision); w.Write(s.GameId); w.Write(s.SecondsLeft); w.Write(s.InputOpen);
            // Hanya angka aturan yang dibutuhkan tampilan; RNG dan aturan host tetap milik host.
            w.Write(s.Config.MaxHp); w.Write(s.Config.TurnTimeLimitSeconds);
            w.Write((int)s.Phase); w.Write(s.RoundNumber); w.Write(s.TurnNumber);
            w.Write(s.CurrentSeat); w.Write(s.WinnerSeat);
            w.Write(s.CandiesInDispenser); w.Write(s.GoodLeft); w.Write(s.BadLeft); w.Write(s.CloverOnActive);
            var a = s.Announcement;
            w.Write(a.Total); w.Write(a.Yellow); w.Write(a.Blue); w.Write(a.Red); w.Write(a.Good); w.Write(a.Bad);
            w.Write(s.ActiveCandy != null);
            if (s.ActiveCandy != null)
            {
                var c = s.ActiveCandy;
                w.Write(c.Id); w.Write((int)c.Color); w.Write(c.Revealed);
                w.Write(c.Revealed && c.IsGood);
            }
            Items(w, s.PocketOptions);
            w.Write(s.Seats.Length);
            foreach (var seat in s.Seats)
            {
                var name = System.Text.Encoding.UTF8.GetBytes(seat.Name);
                if (name.Length > MaxNameBytes) throw new InvalidDataException("Nama terlalu panjang.");
                w.Write(name.Length); w.Write(name); w.Write((int)seat.Controller); w.Write(seat.ClientId); w.Write((int)seat.Difficulty);
            }
            w.Write(s.Players.Count);
            foreach (var p in s.Players)
            {
                w.Write(p.Seat); w.Write(p.ConfigIndex); w.Write(p.D20Roll); w.Write(p.Hp);
                w.Write(p.Paralyzed); w.Write(p.HasCupcake); w.Write(p.Eliminated); w.Write(p.EliminatedOnTurn);
                Items(w, p.Items);
            }
        });

        public static TableSnapshot ReadSnapshot(byte[] bytes) => Read(bytes, r => {
            var s = new TableSnapshot { Revision = r.ReadInt64(), GameId = r.ReadInt64(), SecondsLeft = r.ReadSingle(), InputOpen = r.ReadBoolean(),
                Config = new RuleConfig { MaxHp = r.ReadInt32(), TurnTimeLimitSeconds = r.ReadInt32() },
                Phase = EnumValue<GamePhase>(r), RoundNumber = r.ReadInt32(), TurnNumber = r.ReadInt32(),
                CurrentSeat = r.ReadInt32(), WinnerSeat = r.ReadInt32(), CandiesInDispenser = r.ReadInt32(),
                GoodLeft = r.ReadInt32(), BadLeft = r.ReadInt32(), CloverOnActive = r.ReadBoolean(),
                Announcement = new RoundAnnouncement { Total = r.ReadInt32(), Yellow = r.ReadInt32(), Blue = r.ReadInt32(),
                    Red = r.ReadInt32(), Good = r.ReadInt32(), Bad = r.ReadInt32() }
            };
            if (float.IsNaN(s.SecondsLeft) || float.IsInfinity(s.SecondsLeft)) throw new InvalidDataException("Timer tidak valid.");
            if (r.ReadBoolean())
            {
                int id = r.ReadInt32();
                var color = EnumValue<CandyColor>(r);
                bool revealed = r.ReadBoolean(), good = r.ReadBoolean();
                s.ActiveCandy = new Candy(id, color, revealed && good) { Revealed = revealed };
            }
            s.PocketOptions = Items(r, 3);
            s.Seats = new TableSeat[Count(r, 6)];
            for (int i = 0; i < s.Seats.Length; i++)
            {
                int count = Count(r, MaxNameBytes);
                var name = r.ReadBytes(count);
                if (name.Length != count) throw new EndOfStreamException();
                s.Seats[i] = new TableSeat(System.Text.Encoding.UTF8.GetString(name), EnumValue<SeatController>(r),
                    r.ReadUInt64(), EnumValue<BotDifficulty>(r));
            }
            int players = Count(r, 6);
            if (players < 2 || players != s.Seats.Length || s.CurrentSeat < 0 || s.CurrentSeat >= players
                || s.WinnerSeat < -1 || s.WinnerSeat >= players) throw new InvalidDataException("Kursi snapshot tidak valid.");
            var list = new PlayerState[players];
            var configs = new HashSet<int>();
            for (int i = 0; i < players; i++)
            {
                int seat = r.ReadInt32(), config = r.ReadInt32(), roll = r.ReadInt32(), hp = r.ReadInt32();
                if (seat != i || config < 0 || config >= players || !configs.Add(config)) throw new InvalidDataException("Urutan kursi tidak valid.");
                var binding = s.Seats[config];
                var p = new PlayerState(binding.Name, binding.Controller == SeatController.Bot, binding.Difficulty, hp) {
                    Seat = seat, ConfigIndex = config, D20Roll = roll, Paralyzed = r.ReadBoolean(), HasCupcake = r.ReadBoolean(),
                    Eliminated = r.ReadBoolean(), EliminatedOnTurn = r.ReadInt32()
                };
                p.items.AddRange(Items(r, 256));
                list[i] = p;
            }
            s.Players = list;
            return s;
        });
    }
}
