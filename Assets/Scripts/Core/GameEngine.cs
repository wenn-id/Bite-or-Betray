using System;
using System.Collections.Generic;
using System.Linq;

namespace BiteOrBetray.Core
{
    /// <summary>
    /// All Bite or Betray rules in pure C# (without UnityEngine), following the GDD.
    /// Actions are always performed by the player whose turn it is (Current).
    /// </summary>
    public sealed class GameEngine : ITableState
    {
        readonly RuleConfig cfg;
        readonly Random rng;
        readonly List<PlayerState> players = new List<PlayerState>();
        readonly List<Candy> pool = new List<Candy>();
        readonly List<ItemType> pocketOptions = new List<ItemType>();
        readonly List<string> log = new List<string>();

        public GameEngine(RuleConfig config = null, int? seed = null)
        {
            cfg = config ?? new RuleConfig();
            rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public event Action<string> Logged;

        public RuleConfig Config => cfg;
        public IReadOnlyList<PlayerState> Players => players;
        public IReadOnlyList<string> LogLines => log;
        public GamePhase Phase { get; private set; } = GamePhase.NotStarted;
        public bool IsOver => Phase == GamePhase.GameOver;
        public int RoundNumber { get; private set; }
        public int TurnNumber { get; private set; }
        public int CurrentSeat { get; private set; }
        public PlayerState Current => players.Count > 0 ? players[CurrentSeat] : null;
        public PlayerState Winner { get; private set; }

        public Candy ActiveCandy { get; private set; }
        /// <summary>Clover attaches to the active candy slot: it carries over when Hammer/Gloves replace the candy or a round changes.</summary>
        public bool CloverOnActive { get; private set; }
        public RoundAnnouncement Announcement { get; private set; }
        public int CandiesInDispenser => pool.Count;
        /// <summary>Remaining good/bad in the dispenser + active candy. Public information, since every candy leaving play is revealed.</summary>
        public int GoodLeft { get; private set; }
        public int BadLeft { get; private set; }
        public IReadOnlyList<ItemType> PocketOptions => pocketOptions;

        public IEnumerable<PlayerState> AlivePlayers => players.Where(p => p.IsAlive);

        /// <summary>Statistics for the simulator and balance analysis.</summary>
        public GameStats Stats { get; } = new GameStats();

        // ------------------------------------------------------------------ setup

        public void Start(IList<SeatConfig> seats)
        {
            if (Phase != GamePhase.NotStarted) throw new InvalidOperationException("Game already started.");
            var valid = cfg.Validate();
            if (!valid.Success) throw new ArgumentException(valid.Error);
            if (seats == null || seats.Count < 2 || seats.Count > 6) throw new ArgumentException("Participant count must be 2-6.");

            var temp = seats.Select((s, i) => new PlayerState(s.Name, s.IsBot, s.Difficulty, Math.Min(cfg.StartHp, cfg.MaxHp)) { ConfigIndex = i }).ToList();
            RollTurnOrder(temp);
            temp.Sort((a, b) => b.D20Roll.CompareTo(a.D20Roll));
            for (int i = 0; i < temp.Count; i++)
            {
                temp[i].Seat = i;
                players.Add(temp[i]);
            }
            Write("Turn order and seating: " + string.Join(" > ", players.Select(p => $"{p.Name} ({p.D20Roll})")));

            CurrentSeat = 0;
            StartNewRound();
            BeginTurn();
        }

        void RollTurnOrder(List<PlayerState> list)
        {
            foreach (var p in list)
            {
                p.D20Roll = rng.Next(1, 21);
                Write($"{p.Name} rolls d20: {p.D20Roll}");
            }
            while (true)
            {
                var tied = list.GroupBy(p => p.D20Roll).Where(g => g.Count() > 1).SelectMany(g => g).ToList();
                if (tied.Count == 0) return;
                Write("Tied rolls, roll again: " + string.Join(", ", tied.Select(p => p.Name)));
                foreach (var p in tied)
                {
                    p.D20Roll = rng.Next(1, 21);
                    Write($"{p.Name} rerolls d20: {p.D20Roll}");
                }
            }
        }

        void StartNewRound()
        {
            RoundNumber++;
            pool.Clear();

            int alive = AlivePlayers.Count();
            int count = Clamp(alive * cfg.CandiesPerAlivePlayer, Math.Max(2, cfg.MinCandies), Math.Max(2, cfg.MaxCandies));
            double ratio = cfg.MinBadRatio + rng.NextDouble() * (cfg.MaxBadRatio - cfg.MinBadRatio)
                           + cfg.BadRatioPerExtraSeat * Math.Max(0, players.Count - 2);
            int bad = Clamp((int)Math.Round(count * ratio), 1, count - 1);

            var colors = new List<CandyColor>();
            var allColors = (CandyColor[])Enum.GetValues(typeof(CandyColor));
            foreach (var c in allColors)
                if (colors.Count < count) colors.Add(c);
            while (colors.Count < count) colors.Add(allColors[rng.Next(allColors.Length)]);

            var goodFlags = new List<bool>();
            for (int i = 0; i < count; i++) goodFlags.Add(i >= bad);
            Shuffle(goodFlags);

            for (int i = 0; i < count; i++) pool.Add(new Candy(colors[i], goodFlags[i]));
            Shuffle(pool);

            Recount();
            var rec = new RoundRecord
            {
                Number = RoundNumber, AlivePlayers = alive, Candies = count,
                Yellow = Announcement.Yellow, Blue = Announcement.Blue, Red = Announcement.Red,
                Good = Announcement.Good, Bad = Announcement.Bad
            };
            Stats.Rounds.Add(rec);
            foreach (var c in pool)
            {
                Stats.CandiesSpawnedByColor[(int)c.Color]++;
                if (c.IsGood) Stats.GoodSpawnedByColor[(int)c.Color]++; else Stats.BadSpawnedByColor[(int)c.Color]++;
            }
            Write($"===== Round {RoundNumber} =====");
            Write("The waiter brings a tray: " + Announcement);
            DealRoundItems();
        }

        void DealRoundItems()
        {
            if (cfg.ItemsPerRound <= 0) return;
            if (RoundNumber == 1 && !cfg.DealItemsOnFirstRound) return;
            foreach (var p in AlivePlayers)
            {
                var items = new List<ItemType>();
                for (int i = 0; i < cfg.ItemsPerRound; i++) items.Add(ItemInfo.All[rng.Next(ItemInfo.All.Length)]);
                int before = p.items.Count;
                DealItems(p, items);
                foreach (var it in items) Stats.ItemsSpawned[(int)it]++;
                var rec = Stats.CurrentRound;
                if (rec != null)
                {
                    rec.ItemsSpawned += items.Count;
                    rec.ItemsReceived += p.items.Count - before;
                }
            }
        }

        /// <summary>Deal items one at a time. If the inventory is full, automatically discard the rest.</summary>
        public void DealItems(PlayerState p, IEnumerable<ItemType> items)
        {
            var got = new List<string>();
            var lost = new List<string>();
            foreach (var it in items)
            {
                if (p.items.Count < cfg.MaxItems)
                {
                    p.items.Add(it);
                    got.Add(ItemInfo.Name(it));
                }
                else
                {
                    lost.Add(ItemInfo.Name(it));
                    Stats.ItemsDiscardedFull++;
                }
            }
            if (got.Count > 0) Write($"{p.Name} receives items: {string.Join(", ", got)}");
            if (lost.Count > 0) Write($"{p.Name}'s inventory is full, discarded items: {string.Join(", ", lost)}");
        }

        void Recount()
        {
            var all = new List<Candy>(pool);
            if (ActiveCandy != null) all.Add(ActiveCandy);
            Announcement = new RoundAnnouncement
            {
                Total = all.Count,
                Yellow = all.Count(c => c.Color == CandyColor.Yellow),
                Blue = all.Count(c => c.Color == CandyColor.Blue),
                Red = all.Count(c => c.Color == CandyColor.Red),
                Good = all.Count(c => c.IsGood),
                Bad = all.Count(c => !c.IsGood)
            };
            GoodLeft = Announcement.Good;
            BadLeft = Announcement.Bad;
        }

        // ------------------------------------------------------------------ turn flow

        void BeginTurn()
        {
            for (int guard = 0; guard <= players.Count; guard++)
            {
                if (CheckGameOver()) return;
                var p = Current;
                if (!p.IsAlive)
                {
                    CurrentSeat = NextAliveSeat(CurrentSeat);
                    continue;
                }
                if (p.Paralyzed)
                {
                    p.Paralyzed = false;
                    Stats.TurnsSkipped++;
                    if (Stats.CurrentRound != null) Stats.CurrentRound.TurnsSkipped++;
                    Write($"{p.Name} is paralyzed: turn skipped, paralysis cleared.");
                    CurrentSeat = NextAliveSeat(CurrentSeat);
                    continue;
                }
                TurnNumber++;
                if (Stats.CurrentRound != null) Stats.CurrentRound.Turns++;
                Stats.Turns.Add(new TurnRecord { Round = RoundNumber, Seat = p.Seat, IsBot = p.IsBot });
                Write($"--- {p.Name}'s turn (HP {p.Hp}) ---");
                Phase = GamePhase.AwaitingAction;
                DispenseIfNeeded();
                return;
            }
        }

        void EndTurn()
        {
            CurrentSeat = NextAliveSeat(CurrentSeat);
            BeginTurn();
        }

        int NextAliveSeat(int from)
        {
            for (int k = 1; k <= players.Count; k++)
            {
                int j = (from + k) % players.Count;
                if (players[j].IsAlive) return j;
            }
            return from;
        }

        void DispenseIfNeeded()
        {
            if (ActiveCandy != null || IsOver) return;
            if (pool.Count == 0)
            {
                Write("Dispenser empty.");
                StartNewRound();
            }
            ActiveCandy = DrawFromPool();
            Stats.CandiesDispensed++;
            Write($"Dispenser draws {CandyRules.ColorName(ActiveCandy.Color)} candy"
                  + (CloverOnActive ? " (clover active)" : "")
                  + $". Remaining in dispenser: {pool.Count}");
        }

        Candy DrawFromPool()
        {
            int i = cfg.RandomDraw ? rng.Next(pool.Count) : 0;
            var c = pool[i];
            pool.RemoveAt(i);
            return c;
        }

        Candy TakeActive(out bool clover)
        {
            var c = ActiveCandy;
            ActiveCandy = null;
            clover = CloverOnActive;
            CloverOnActive = false;
            return c;
        }

        bool CheckGameOver()
        {
            if (Phase == GamePhase.GameOver) return true;
            var alive = AlivePlayers.ToList();
            if (alive.Count > 1) return false;
            Winner = alive.FirstOrDefault();
            Phase = GamePhase.GameOver;
            pocketOptions.Clear();
            Write(Winner != null ? $"===== {Winner.Name} WINS! =====" : "===== No winner =====");
            return true;
        }

        ActionResult Guard()
        {
            if (Phase == GamePhase.GameOver) return ActionResult.Fail(Strings.GameAlreadyOver.Text);
            if (Phase == GamePhase.AwaitingPocketChoice) return ActionResult.Fail(Strings.PocketFirst.Text);
            if (Phase != GamePhase.AwaitingAction || ActiveCandy == null) return ActionResult.Fail(Strings.CannotActYet.Text);
            return ActionResult.Ok;
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Eat the active candy yourself. Good (or cupcake immunity) = continue the turn, bad = end the turn.</summary>
        public ActionResult Eat()
        {
            var g = Guard();
            if (!g.Success) return g;

            var p = Current;
            CountDecision();
            var candy = TakeActive(out bool clover);
            bool hadCupcake = p.HasCupcake;
            Stats.EatSelf++;
            if (candy.IsGood) Stats.EatSelfGood++; else Stats.EatSelfBad++;
            Write($"{p.Name} eats {CandyRules.ColorName(candy.Color)} candy.");
            ApplyCandy(p, candy, clover);
            if (CheckGameOver()) return ActionResult.Ok;

            if (p.IsAlive && (candy.IsGood || hadCupcake))
            {
                Write($"{p.Name}'s turn continues.");
                DispenseIfNeeded();
            }
            else EndTurn();
            return ActionResult.Ok;
        }

        /// <summary>Feed the active candy to another player. Always ends the turn.</summary>
        public ActionResult Give(int targetSeat)
        {
            var g = Guard();
            if (!g.Success) return g;
            if (targetSeat < 0 || targetSeat >= players.Count) return ActionResult.Fail(Strings.NoTarget.Text);
            var t = players[targetSeat];
            if (t == Current) return ActionResult.Fail(Strings.CannotFeedSelf.Text);
            if (!t.IsAlive) return ActionResult.Fail(Strings.TargetOut.Text);

            var p = Current;
            CountDecision();
            var candy = TakeActive(out bool clover);
            Stats.Gives++;
            if (candy.IsGood) Stats.GivesGood++; else Stats.GivesBad++;
            Write($"{p.Name} feeds {CandyRules.ColorName(candy.Color)} candy to {t.Name}.");
            ApplyCandy(t, candy, clover);
            if (CheckGameOver()) return ActionResult.Ok;
            EndTurn();
            return ActionResult.Ok;
        }

        void CountDecision()
        {
            var t = Stats.CurrentTurn;
            if (t != null) t.Decisions++;
        }

        /// <summary>
        /// Called by the controller when the turn timer expires. Discard an open Little Pocket, then force the active
        /// candy to be eaten by the current player, according to RuleConfig.TimeoutAction.
        /// </summary>
        public ActionResult TimeoutTurn()
        {
            if (Phase == GamePhase.GameOver) return ActionResult.Fail(Strings.GameAlreadyOver.Text);
            if (Phase == GamePhase.AwaitingPocketChoice)
            {
                pocketOptions.Clear();
                Phase = GamePhase.AwaitingAction;
                Write("Little Pocket contents discarded due to timeout.");
            }
            var g = Guard();
            if (!g.Success) return g;

            var p = Current;
            Stats.Timeouts++;
            if (Stats.CurrentTurn != null) Stats.CurrentTurn.TimedOut = true;
            var candy = TakeActive(out bool clover);
            bool hadCupcake = p.HasCupcake;
            Write($"Time's up! {p.Name} is forced to eat {CandyRules.ColorName(candy.Color)} candy.");
            ApplyCandy(p, candy, clover);
            if (CheckGameOver()) return ActionResult.Ok;
            if (cfg.TimeoutAction == TimeoutRule.ForcedEat && p.IsAlive && (candy.IsGood || hadCupcake))
            {
                Write($"{p.Name}'s turn continues.");
                DispenseIfNeeded();
            }
            else EndTurn();
            return ActionResult.Ok;
        }

        void ApplyCandy(PlayerState target, Candy candy, bool clover)
        {
            candy.Revealed = true;
            if (candy.IsGood) GoodLeft--; else BadLeft--;
            if (candy.IsGood) Stats.GoodConsumedByColor[(int)candy.Color]++; else Stats.BadConsumedByColor[(int)candy.Color]++;
            Write($"It tastes like {candy.FlavorName} ({(candy.IsGood ? "GOOD" : "BAD")}).");

            if (target.HasCupcake)
            {
                target.HasCupcake = false;
                Stats.CupcakeBlocks++;
                Write($"{target.Name} is immune thanks to cupcake: no effect. Cupcake effect expires.");
                return;
            }

            int mult = clover ? 2 : 1;
            if (clover)
            {
                Stats.CloverTriggers++;
                Write("Clover: effects doubled!");
            }

            int before = target.Hp;
            if (candy.IsGood)
            {
                target.Hp = Math.Min(cfg.MaxHp, target.Hp + GoodHealNow(candy.Color) * mult);
                Stats.HpHealed += target.Hp - before;
                Write(GoodHealNow(candy.Color) == 0
                    ? $"{target.Name} is safe, no effect."
                    : $"{target.Name} HP {before} -> {target.Hp}" + (target.Hp == cfg.MaxHp ? " (max)" : "") + ".");
                return;
            }

            int damage = BadDamageNow(candy.Color) * mult;
            if (damage > CandyRules.MaxDamagePerCandy)
            {
                damage = CandyRules.MaxDamagePerCandy;
                Write($"Damage capped at {damage} per candy.");
            }
            target.Hp = Math.Max(0, target.Hp - damage);
            Stats.HpLost += before - target.Hp;
            Write($"{target.Name} HP {before} -> {target.Hp}.");
            if (CandyRules.BadParalyzes(candy.Color))
            {
                if (target.Paralyzed) Write($"{target.Name} is already paralyzed (status does not stack).");
                else
                {
                    target.Paralyzed = true;
                    Stats.ParalyzeApplied++;
                    Write($"{target.Name} is paralyzed: next turn skipped.");
                }
            }
            if (target.Hp <= 0) Eliminate(target);
        }

        void Eliminate(PlayerState p)
        {
            p.Hp = 0;
            p.Eliminated = true;
            p.EliminatedOnTurn = TurnNumber;
            Stats.Eliminations++;
            p.Paralyzed = false;
            p.HasCupcake = false;
            p.items.Clear();
            Write($"{p.Name} is poisoned! HP 0, eliminated and spectating. All their items are lost.");
        }

        public int GoodHealNow(CandyColor c) => CandyRules.GoodHeal(c);

        public int BadDamageNow(CandyColor c) => CandyRules.BadDamage(c);

        // ------------------------------------------------------------------ item

        public bool HasGrappleTarget(PlayerState user) =>
            players.Any(t => t != user && t.IsAlive && t.items.Any(i => i != ItemType.GrapplingHook));

        public ActionResult ValidateItem(PlayerState user, ItemType type, ItemArgs args)
        {
            if (type != ItemType.GrapplingHook) return ActionResult.Ok;
            if (args == null) return ActionResult.Fail(Strings.PickTargetItem.Text);
            if (args.TargetSeat < 0 || args.TargetSeat >= players.Count) return ActionResult.Fail(Strings.NoTarget.Text);
            var t = players[args.TargetSeat];
            if (t == user || !t.IsAlive) return ActionResult.Fail(Strings.InvalidGrappleTarget.Text);
            if (args.TargetItemIndex < 0 || args.TargetItemIndex >= t.items.Count) return ActionResult.Fail(Strings.NoTargetItem.Text);
            if (t.items[args.TargetItemIndex] == ItemType.GrapplingHook)
                return ActionResult.Fail(Strings.GrappleOnGrapple.Text);
            return ActionResult.Ok;
        }

        /// <summary>Use an inventory item. Does not end the turn. Grappling Hook needs args.</summary>
        public ActionResult UseItem(int itemIndex, ItemArgs args = null)
        {
            var g = Guard();
            if (!g.Success) return g;
            var p = Current;
            if (itemIndex < 0 || itemIndex >= p.items.Count) return ActionResult.Fail(Strings.NoItem.Text);
            var type = p.items[itemIndex];
            var v = ValidateItem(p, type, args);
            if (!v.Success) return v;

            p.items.RemoveAt(itemIndex);
            CountDecision();
            Write($"{p.Name} uses {ItemInfo.Name(type)}.");
            ExecuteItem(p, type, args);
            return ActionResult.Ok;
        }

        /// <summary>Choose 1 of 3 Little Pocket contents. Used immediately; cannot be stored.</summary>
        public ActionResult ChoosePocketItem(int optionIndex, ItemArgs args = null)
        {
            if (Phase != GamePhase.AwaitingPocketChoice) return ActionResult.Fail(Strings.NoPocketOpen.Text);
            if (optionIndex < 0 || optionIndex >= pocketOptions.Count) return ActionResult.Fail(Strings.NoSuchOption.Text);
            var type = pocketOptions[optionIndex];
            var v = ValidateItem(Current, type, args);
            if (!v.Success) return v;

            pocketOptions.Clear();
            Phase = GamePhase.AwaitingAction;
            CountDecision();
            Write($"{Current.Name} chooses {ItemInfo.Name(type)} from Little Pocket.");
            ExecuteItem(Current, type, args);
            return ActionResult.Ok;
        }

        public ActionResult DiscardPocket()
        {
            if (Phase != GamePhase.AwaitingPocketChoice) return ActionResult.Fail(Strings.NoPocketOpen.Text);
            pocketOptions.Clear();
            Phase = GamePhase.AwaitingAction;
            Stats.PocketDiscarded++;
            CountDecision();
            Write($"{Current.Name} discards Little Pocket contents.");
            return ActionResult.Ok;
        }

        void ExecuteItem(PlayerState p, ItemType type, ItemArgs args)
        {
            Stats.ItemsUsed[(int)type]++;
            switch (type)
            {
                case ItemType.PlasticFork:
                {
                    var c = ActiveCandy;
                    c.Revealed = true;
                    Write($"Fork pierces {CandyRules.ColorName(c.Color)} candy: {(c.IsGood ? "GOOD" : "BAD")} ({c.FlavorName}). Every player sees it.");
                    break;
                }
                case ItemType.HammerCrush:
                {
                    var c = ActiveCandy;
                    ActiveCandy = null;
                    c.Revealed = true;
                    if (c.IsGood) GoodLeft--; else BadLeft--;
                    Write($"Hammer crushes {CandyRules.ColorName(c.Color)} candy: {(c.IsGood ? "GOOD" : "BAD")} ({c.FlavorName}).");
                    if (CloverOnActive) Write("Clover moves to the next candy.");
                    DispenseIfNeeded();
                    break;
                }
                case ItemType.HotMilk:
                {
                    int before = p.Hp;
                    p.Hp = Math.Min(cfg.MaxHp, p.Hp + 1);
                    Stats.HpHealed += p.Hp - before;
                    Write(before == p.Hp ? $"{p.Name}'s HP is already full, Hot Milk wasted." : $"{p.Name} HP {before} -> {p.Hp}.");
                    break;
                }
                case ItemType.MagicGloves:
                {
                    if (pool.Count == 0)
                    {
                        Write("Dispenser empty: the same candy comes out again.");
                        break;
                    }
                    var old = ActiveCandy;
                    var fresh = DrawFromPool();
                    // Candy revealed by a Fork stays revealed when returned to the dispenser: every player has seen its contents.
                    int pos = cfg.RandomDraw ? rng.Next(pool.Count + 1) : pool.Count;
                    pool.Insert(pos, old);
                    ActiveCandy = fresh;
                    Write($"Gloves swap the candy. Drawn candy: {CandyRules.ColorName(fresh.Color)}" + (CloverOnActive ? " (clover stays active)" : "") + ".");
                    break;
                }
                case ItemType.Cupcake:
                    if (p.HasCupcake) Write($"{p.Name} already has a cupcake effect (does not stack). Cupcake wasted.");
                    else
                    {
                        p.HasCupcake = true;
                        Write($"{p.Name} is immune to the next candy eaten.");
                    }
                    break;
                case ItemType.CloverCoin:
                    if (CloverOnActive) Write("Clover already active (does not stack). Clover Coin wasted.");
                    else
                    {
                        CloverOnActive = true;
                        Write("Clover active on this candy: whoever eats it receives x2 effects.");
                    }
                    break;
                case ItemType.LittlePocket:
                {
                    pocketOptions.Clear();
                    var choices = ItemInfo.All.Where(t => t != ItemType.LittlePocket).ToArray();
                    for (int i = 0; i < 3; i++) pocketOptions.Add(choices[rng.Next(choices.Length)]);
                    Phase = GamePhase.AwaitingPocketChoice;
                    Write("Little Pocket contains: " + string.Join(", ", pocketOptions.Select(ItemInfo.Name)));
                    break;
                }
                case ItemType.GrapplingHook:
                {
                    var t = players[args.TargetSeat];
                    var stolen = t.items[args.TargetItemIndex];
                    t.items.RemoveAt(args.TargetItemIndex);
                    Write($"{p.Name} fires Grappling Hook at {t.Name} and takes {ItemInfo.Name(stolen)}. Item used immediately.");
                    ExecuteItem(p, stolen, null);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ debug / test

        /// <summary>Replace dispenser contents with specific candy (order applies when RandomDraw = false).</summary>
        public void DebugSetPool(IEnumerable<Candy> candies)
        {
            pool.Clear();
            pool.AddRange(candies);
            ActiveCandy = null;
            Recount();
            Write("[DEBUG] Dispenser contents replaced: " + Announcement);
            if (Phase == GamePhase.AwaitingAction) DispenseIfNeeded();
        }

        public IReadOnlyList<Candy> DebugPool => pool;

        public void DebugSetHp(PlayerState p, int hp) => p.Hp = Clamp(hp, 1, cfg.MaxHp);

        public void DebugSetCupcake(PlayerState p, bool value) => p.HasCupcake = value;

        public void DebugSetParalyzed(PlayerState p, bool value) => p.Paralyzed = value;

        /// <summary>Add an item without the inventory limit (tests only).</summary>
        public void DebugGiveItem(PlayerState p, ItemType t) => p.items.Add(t);

        // ------------------------------------------------------------------ util

        /// <summary>Disable for faster batch simulations with lower memory use.</summary>
        public bool KeepLog { get; set; } = true;

        void Write(string msg)
        {
            if (!KeepLog && Logged == null) return;
            if (KeepLog) log.Add(msg);
            Logged?.Invoke(msg);
        }

        void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
