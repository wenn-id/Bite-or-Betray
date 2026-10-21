using System;
using System.Collections.Generic;

namespace BiteOrBetray.Core
{
    public enum CandyColor { Yellow, Blue, Red }

    public enum ItemType
    {
        PlasticFork,
        HammerCrush,
        HotMilk,
        MagicGloves,
        Cupcake,
        LittlePocket,
        GrapplingHook,
        CloverCoin
    }

    public enum BotDifficulty { Easy, Normal, Hard }

    public enum GamePhase { NotStarted, AwaitingAction, AwaitingPocketChoice, GameOver }

    /// <summary>What happens when the turn timer expires.</summary>
    public enum TimeoutRule
    {
        /// <summary>Force the active candy to be eaten, then end the turn regardless of the result.</summary>
        ForcedEatEndsTurn,
        /// <summary>Force the active candy to be eaten with normal eating rules (good = continue, controller starts a new timer).</summary>
        ForcedEat
    }

    /// <summary>One candy. Its color is always visible; its contents (good/bad) stay hidden until revealed.</summary>
    public sealed class Candy
    {
        static int nextId;

        public int Id { get; }
        public CandyColor Color { get; }
        public bool IsGood { get; }
        public bool Revealed { get; internal set; }

        public Candy(CandyColor color, bool isGood)
        {
            Id = ++nextId;
            Color = color;
            IsGood = isGood;
        }

        // Host identity for the client view; does not change the local engine's ID counter.
        internal Candy(int id, CandyColor color, bool isGood)
        {
            Id = id;
            Color = color;
            IsGood = isGood;
        }

        public string FlavorName => CandyRules.FlavorName(Color, IsGood);

        public override string ToString() => $"{CandyRules.ColorName(Color)} ({(IsGood ? "good" : "bad")})";
    }

    /// <summary>Candy effect table from the GDD.</summary>
    public static class CandyRules
    {
        public static int GoodHeal(CandyColor c) => c == CandyColor.Yellow ? 0 : 1;

        public static int BadDamage(CandyColor c) => c == CandyColor.Blue ? 2 : 1;

        /// <summary>Damage cap for one candy, including Clover. Prevents a one-turn kill
        /// with Fork + Clover + Give and bad blue candy (2 x 2 = 4 = starting HP).</summary>
        public const int MaxDamagePerCandy = 3;

        public static bool BadParalyzes(CandyColor c) => c == CandyColor.Red;

        public static string ColorName(CandyColor c)
        {
            switch (c)
            {
                case CandyColor.Yellow: return Strings.Yellow.Text;
                case CandyColor.Blue: return Strings.Blue.Text;
                default: return Strings.Red.Text;
            }
        }

        public static string FlavorName(CandyColor c, bool good)
        {
            switch (c)
            {
                case CandyColor.Yellow: return (good ? Strings.FlavorYellowGood : Strings.FlavorYellowBad).Text;
                case CandyColor.Blue: return (good ? Strings.FlavorBlueGood : Strings.FlavorBlueBad).Text;
                default: return (good ? Strings.FlavorRedGood : Strings.FlavorRedBad).Text;
            }
        }

        public static string EffectSummary(CandyColor c)
        {
            string bad = $"HP -{BadDamage(c)}" + (BadParalyzes(c) ? " + paralyzed" : "");
            return (GoodHeal(c) == 0 ? "Good: no effect" : $"Good: HP +{GoodHeal(c)}") + $"  |  Bad: {bad}";
        }
    }

    public static class ItemInfo
    {
        public static readonly ItemType[] All = (ItemType[])Enum.GetValues(typeof(ItemType));

        /// <summary>Item names match in both languages; descriptions follow Localization.Language.</summary>
        public static string Name(ItemType t)
        {
            switch (t)
            {
                case ItemType.PlasticFork: return "Plastic Fork";
                case ItemType.HammerCrush: return "Hammer Crush";
                case ItemType.HotMilk: return "Hot Milk";
                case ItemType.MagicGloves: return "Magic Gloves";
                case ItemType.Cupcake: return "Cupcake";
                case ItemType.LittlePocket: return "Little Pocket";
                case ItemType.GrapplingHook: return "Grappling Hook";
                default: return "Clover Coin";
            }
        }

        public static string Description(ItemType t)
        {
            switch (t)
            {
                case ItemType.PlasticFork: return Strings.PlasticForkInfo.Text;
                case ItemType.HammerCrush: return Strings.HammerCrushInfo.Text;
                case ItemType.HotMilk: return Strings.HotMilkInfo.Text;
                case ItemType.MagicGloves: return Strings.MagicGlovesInfo.Text;
                case ItemType.Cupcake: return Strings.CupcakeInfo.Text;
                case ItemType.LittlePocket: return Strings.LittlePocketInfo.Text;
                case ItemType.GrapplingHook: return Strings.GrapplingHookInfo.Text;
                default: return Strings.CloverCoinInfo.Format(CandyRules.MaxDamagePerCandy);
            }
        }
    }

    /// <summary>
    /// Rule values. Defaults = final balancing (Oct 2026): max HP 5, starting HP 4, spawn rates from simulations
    /// targeting durations of 7-10 minutes for 2p, 11-16 for 3-4p, and 16-21 for 5-6p.
    /// </summary>
    [Serializable]
    public class RuleConfig
    {
        public int StartHp = 4;
        public int MaxHp = 5;
        public int MaxItems = 6;

        public int CandiesPerAlivePlayer = 3;
        public int MinCandies = 4;
        public int MaxCandies = 12;
        public float MinBadRatio = 0.38f;
        public float MaxBadRatio = 0.52f;
        /// <summary>Increase the bad ratio by this amount for each seat above 2 (based on the initial player count).
        /// The waiter adjusts the tray to the table size so larger tables do not take much longer.</summary>
        public float BadRatioPerExtraSeat = 0.04f;

        public int ItemsPerRound = 2;
        public bool DealItemsOnFirstRound = true;

        /// <summary>Thinking time limit per turn (seconds). Animation time is excluded. 0 = no timer.
        /// The engine does not track time; the controller (test table / VR) calls TimeoutTurn().</summary>
        public int TurnTimeLimitSeconds = 30;
        public TimeoutRule TimeoutAction = TimeoutRule.ForcedEatEndsTurn;

        /// <summary>True: draw random candy from the dispenser. False: always draw the front candy (for unit tests).</summary>
        public bool RandomDraw = true;

        public RuleConfig Clone() => (RuleConfig)MemberwiseClone();

        /// <summary>Validate the combination of rule values. Failure combines all issues into one message.
        /// GameEngine.Start calls this and throws ArgumentException if invalid.</summary>
        public ActionResult Validate()
        {
            var errors = new List<string>();
            if (MaxHp < 1) errors.Add($"MaxHp ({MaxHp}) must be at least 1.");
            if (StartHp < 1) errors.Add($"StartHp ({StartHp}) must be at least 1.");
            else if (StartHp > MaxHp) errors.Add($"StartHp ({StartHp}) must not exceed MaxHp ({MaxHp}).");
            if (MaxItems < 0) errors.Add($"MaxItems ({MaxItems}) must not be negative.");
            if (ItemsPerRound < 0) errors.Add($"ItemsPerRound ({ItemsPerRound}) must not be negative.");
            else if (ItemsPerRound > 0 && MaxItems == 0) errors.Add("MaxItems 0 with ItemsPerRound > 0: all items will be discarded.");

            if (CandiesPerAlivePlayer < 1) errors.Add($"CandiesPerAlivePlayer ({CandiesPerAlivePlayer}) must be at least 1.");
            // A round needs at least 1 good and 1 bad candy.
            if (MinCandies < 2) errors.Add($"MinCandies ({MinCandies}) must be at least 2 (1 good + 1 bad).");
            if (MaxCandies < MinCandies) errors.Add($"MaxCandies ({MaxCandies}) must not be less than MinCandies ({MinCandies}).");

            // Use !(x >= a && x <= b) to reject NaN as well.
            bool minOk = MinBadRatio >= 0f && MinBadRatio <= 1f, maxOk = MaxBadRatio >= 0f && MaxBadRatio <= 1f;
            if (!minOk) errors.Add($"MinBadRatio ({MinBadRatio}) must be between 0 and 1.");
            if (!maxOk) errors.Add($"MaxBadRatio ({MaxBadRatio}) must be between 0 and 1.");
            if (minOk && maxOk && MinBadRatio > MaxBadRatio)
                errors.Add($"MinBadRatio ({MinBadRatio}) must not exceed MaxBadRatio ({MaxBadRatio}).");
            if (!(BadRatioPerExtraSeat >= 0f && BadRatioPerExtraSeat <= 1f))
                errors.Add($"BadRatioPerExtraSeat ({BadRatioPerExtraSeat}) must be between 0 and 1.");

            if (TurnTimeLimitSeconds < 0) errors.Add($"TurnTimeLimitSeconds ({TurnTimeLimitSeconds}) must not be negative (0 = no timer).");
            if (!Enum.IsDefined(typeof(TimeoutRule), TimeoutAction)) errors.Add($"Unknown TimeoutAction ({(int)TimeoutAction}).");

            return errors.Count == 0 ? ActionResult.Ok : ActionResult.Fail("Invalid RuleConfig: " + string.Join(" ", errors));
        }
    }

    public sealed class SeatConfig
    {
        public string Name;
        public bool IsBot;
        public BotDifficulty Difficulty;

        public SeatConfig(string name, bool isBot = false, BotDifficulty difficulty = BotDifficulty.Normal)
        {
            Name = name;
            IsBot = isBot;
            Difficulty = difficulty;
        }
    }

    /// <summary>Additional arguments for items that need a target (Grappling Hook).</summary>
    public sealed class ItemArgs
    {
        public int TargetSeat;
        public int TargetItemIndex;

        public ItemArgs(int targetSeat, int targetItemIndex)
        {
            TargetSeat = targetSeat;
            TargetItemIndex = targetItemIndex;
        }
    }

    public readonly struct ActionResult
    {
        public readonly bool Success;
        public readonly string Error;

        ActionResult(bool success, string error)
        {
            Success = success;
            Error = error;
        }

        public static ActionResult Ok => new ActionResult(true, null);
        public static ActionResult Fail(string error) => new ActionResult(false, error);
    }

    /// <summary>Waiter announcement: counts per color and total good/bad. The good/bad split per color stays secret.</summary>
    public sealed class RoundAnnouncement
    {
        public int Total, Yellow, Blue, Red, Good, Bad;

        public override string ToString() =>
            $"{Total} candy: {Yellow} yellow, {Blue} blue, {Red} red  |  {Good} good, {Bad} bad";
    }
}
