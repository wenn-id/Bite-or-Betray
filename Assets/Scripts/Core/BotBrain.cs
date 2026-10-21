using System;
using System.Collections.Generic;
using System.Linq;

namespace BiteOrBetray.Core
{
    public enum BotActionKind { Eat, Give, UseItem, ChoosePocket, DiscardPocket }

    public sealed class BotDecision
    {
        public BotActionKind Kind;
        public int Index = -1;
        public int TargetSeat = -1;
        public ItemArgs Args;
        /// <summary>Brief reason for table narration (active language) and telemetry.</summary>
        public string Reason;

        public override string ToString() => $"{Kind} ({Reason})";
    }

    /// <summary>
    /// Simple bot AI. Bots only use public information: remaining good/bad counts, colors, and revealed candy.
    /// Easy = partly random, Normal = probability + basic items, Hard = HP/color value + Clover combos.
    /// </summary>
    public static class BotBrain
    {
        /// <summary>Make and execute one decision. On failure, fall back to an action that is always valid.</summary>
        public static ActionResult Step(GameEngine g, Random rng)
        {
            if (g.IsOver || g.Current == null || !g.Current.IsBot) return ActionResult.Fail("It is not the bot's turn.");
            var d = Decide(g, rng);
            var r = Execute(g, d);
            if (!r.Success)
                r = g.Phase == GamePhase.AwaitingPocketChoice ? g.DiscardPocket() : g.Eat();
            return r;
        }

        public static ActionResult Execute(GameEngine g, BotDecision d)
        {
            switch (d.Kind)
            {
                case BotActionKind.Eat: return g.Eat();
                case BotActionKind.Give: return g.Give(d.TargetSeat);
                case BotActionKind.UseItem: return g.UseItem(d.Index, d.Args);
                case BotActionKind.ChoosePocket: return g.ChoosePocketItem(d.Index, d.Args);
                default: return g.DiscardPocket();
            }
        }

        public static double EstimateGood(GameEngine g)
        {
            var c = g.ActiveCandy;
            if (c != null && c.Revealed) return c.IsGood ? 1 : 0;
            int total = g.GoodLeft + g.BadLeft;
            return total <= 0 ? 0.5 : (double)g.GoodLeft / total;
        }

        public static BotDecision Decide(GameEngine g, Random rng)
        {
            var me = g.Current;
            if (g.Phase == GamePhase.AwaitingPocketChoice) return DecidePocket(g, me, rng);
            if (me.Difficulty == BotDifficulty.Easy) return DecideEasy(g, me, rng);

            bool hard = me.Difficulty == BotDifficulty.Hard;
            int max = g.Config.MaxHp;
            var c = g.ActiveCandy;
            double pGood = EstimateGood(g);

            int milk = Find(me, ItemType.HotMilk);
            if (milk >= 0 && me.Hp <= max - (hard ? 1 : 2)) return Use(milk, Strings.WhyLowHpMilk.Text);
            if (hard && me.Hp < max)
            {
                var heal = TryGrapple(g, me, ItemType.HotMilk);
                if (heal != null) return heal;
            }

            // A zero good/bad count also reveals the contents without wasting a Fork.
            if (c.Revealed || pGood == 0 || pGood == 1)
            {
                int clover = Find(me, ItemType.CloverCoin);
                if (pGood == 1)
                {
                    if (hard && clover >= 0 && !g.CloverOnActive && !me.HasCupcake
                        && g.GoodHealNow(c.Color) > 0 && me.Hp + g.GoodHealNow(c.Color) < max)
                        return Use(clover, Strings.WhyGoodClover.Text);
                    return Eat(Strings.WhySurelyGood.Text);
                }
                if (me.HasCupcake) return Eat(Strings.WhyCupcakeSafe.Text);
                int target = PickTarget(g, me, rng, true);
                if (clover >= 0 && !g.CloverOnActive && !g.Players[target].HasCupcake)
                    return Use(clover, Strings.WhyBadClover.Text);
                return Give(target, Strings.WhySurelyBad.Text);
            }

            // While immune, save the Fork for the next candy that still carries a risk.
            if (hard && me.HasCupcake) return Eat(Strings.WhyCupcake.Text);
            int fork = Find(me, ItemType.PlasticFork);
            if (fork >= 0) return Use(fork, Strings.WhyCheckCandy.Text);
            int pocket = Find(me, ItemType.LittlePocket);
            if (pocket >= 0) return Use(pocket, Strings.WhyOpenPocket.Text);
            var grab = TryGrapple(g, me, ItemType.PlasticFork);
            if (grab != null) return grab;

            if (me.HasCupcake) return Eat(Strings.WhyCupcake.Text);

            // Yellow Clover does not increase an opponent's healing; also exploit a high bad probability.
            int coin = Find(me, ItemType.CloverCoin);
            if (coin >= 0 && !g.CloverOnActive && (pGood < 0.5 || c.Color == CandyColor.Yellow && pGood < 0.65))
                return Use(coin, Strings.WhyBadClover.Text);

            if (pGood < 0.5)
            {
                int hammer = Find(me, ItemType.HammerCrush);
                if (!hard && hammer >= 0) return Use(hammer, Strings.WhySmash.Text);
                int cup = Find(me, ItemType.Cupcake);
                if (cup >= 0) return Use(cup, Strings.WhyUseCupcake.Text);
                if (hard)
                {
                    grab = TryGrapple(g, me, ItemType.Cupcake);
                    if (grab != null) return grab;
                }
            }

            double threshold = me.Hp == max ? 0.65 : 0.5;
            if (hard ? ShouldEat(g, me, pGood) : pGood >= threshold)
                return Eat(Strings.WhyGoodChance.Format(pGood));
            return Give(PickTarget(g, me, rng, true), Strings.WhyLowGoodChance.Format(pGood));
        }

        static bool ShouldEat(GameEngine g, PlayerState me, double pGood)
        {
            var c = g.ActiveCandy;
            int damage = Math.Min(CandyRules.MaxDamagePerCandy, g.BadDamageNow(c.Color) * (g.CloverOnActive ? 2 : 1));
            int heal = g.GoodHealNow(c.Color) * (g.CloverOnActive ? 2 : 1);
            // A knockout costs more than ordinary HP loss; good candy also allows another action.
            double eat = pGood * (Math.Min(heal, g.Config.MaxHp - me.Hp) + 0.35)
                         - (1 - pGood) * (damage + (damage >= me.Hp ? 3 : 0));
            var targets = g.Players.Where(p => p != me && p.IsAlive && !p.HasCupcake).ToList();
            double give = targets.Count == 0 ? 0 : targets.Max(p =>
                (1 - pGood) * (Math.Min(damage, p.Hp) + (damage >= p.Hp ? 2 : 0))
                - pGood * Math.Min(heal, g.Config.MaxHp - p.Hp));
            return eat >= give * 0.6;
        }

        static BotDecision DecideEasy(GameEngine g, PlayerState me, Random rng)
        {
            var c = g.ActiveCandy;
            if (me.Items.Count > 0 && rng.NextDouble() < 0.3)
            {
                int i = rng.Next(me.Items.Count);
                if (me.Items[i] != ItemType.GrapplingHook) return Use(i, Strings.WhyRandomItem.Text);
            }
            if (c.Revealed)
                return c.IsGood ? Eat(Strings.WhyGood.Text) : Give(PickTarget(g, me, rng, false), Strings.WhyBad.Text);
            return rng.NextDouble() < 0.55 ? Eat(Strings.WhyReckless.Text) : Give(PickTarget(g, me, rng, false), Strings.WhyPassOn.Text);
        }

        static BotDecision DecidePocket(GameEngine g, PlayerState me, Random rng)
        {
            var opts = g.PocketOptions;
            int best = -1;
            double bestScore = double.MinValue;
            ItemArgs bestArgs = null;
            var valid = new List<int>();

            for (int i = 0; i < opts.Count; i++)
            {
                var type = opts[i];
                ItemArgs args = null;
                double s;
                if (type == ItemType.GrapplingHook)
                {
                    args = BestGrappleArgs(g, me, out s);
                    if (args == null) continue;
                }
                else s = Score(g, me, type);

                if (!g.ValidateItem(me, type, args).Success) continue;
                valid.Add(i);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                    bestArgs = args;
                }
            }

            if (me.Difficulty == BotDifficulty.Easy && valid.Count > 0)
            {
                int pick = valid[rng.Next(valid.Count)];
                ItemArgs a = opts[pick] == ItemType.GrapplingHook ? BestGrappleArgs(g, me, out _) : null;
                return new BotDecision { Kind = BotActionKind.ChoosePocket, Index = pick, Args = a, Reason = Strings.WhyRandomPick.Text };
            }
            if (best < 0 || bestScore < 0.3)
                return new BotDecision { Kind = BotActionKind.DiscardPocket, Reason = Strings.WhyUselessPocket.Text };
            return new BotDecision { Kind = BotActionKind.ChoosePocket, Index = best, Args = bestArgs, Reason = Strings.WhyPick.Format(ItemInfo.Name(opts[best])) };
        }

        /// <summary>Score an item's usefulness in the current situation (higher means more useful).</summary>
        static double Score(GameEngine g, PlayerState me, ItemType type)
        {
            var c = g.ActiveCandy;
            double pGood = EstimateGood(g);
            bool known = c != null && (c.Revealed || pGood == 0 || pGood == 1);
            bool good = known && pGood == 1;
            int max = g.Config.MaxHp;
            switch (type)
            {
                case ItemType.PlasticFork: return known || me.HasCupcake ? 0 : 5;
                case ItemType.Cupcake: return me.HasCupcake || good ? 0.1 : 4;
                case ItemType.HammerCrush: return good ? 0 : 3;
                case ItemType.HotMilk: return me.Hp < max ? 4 : 0.5;
                case ItemType.CloverCoin:
                    if (g.CloverOnActive) return 0;
                    if (known && !good) return 3.5;
                    if (good && !me.HasCupcake && g.GoodHealNow(c.Color) > 0
                        && me.Hp + g.GoodHealNow(c.Color) < max) return 3;
                    return 0.1;
                case ItemType.MagicGloves: return known && !good ? 1 : 0.4;
                case ItemType.LittlePocket: return 2;
                default: return 1;
            }
        }

        static ItemArgs BestGrappleArgs(GameEngine g, PlayerState me, out double score)
        {
            score = double.MinValue;
            ItemArgs best = null;
            foreach (var t in g.Players)
            {
                if (t == me || !t.IsAlive) continue;
                for (int i = 0; i < t.Items.Count; i++)
                {
                    if (t.Items[i] == ItemType.GrapplingHook) continue;
                    double s = Score(g, me, t.Items[i]);
                    if (s > score)
                    {
                        score = s;
                        best = new ItemArgs(t.Seat, i);
                    }
                }
            }
            return best;
        }

        static BotDecision TryGrapple(GameEngine g, PlayerState me, params ItemType[] wanted)
        {
            int hook = Find(me, ItemType.GrapplingHook);
            if (hook < 0) return null;
            foreach (var w in wanted)
                foreach (var t in g.Players)
                {
                    if (t == me || !t.IsAlive) continue;
                    int idx = t.Items.ToList().IndexOf(w);
                    if (idx >= 0)
                        return new BotDecision
                        {
                            Kind = BotActionKind.UseItem,
                            Index = hook,
                            Args = new ItemArgs(t.Seat, idx),
                            Reason = Strings.WhySteal.Format(ItemInfo.Name(w), t.Name)
                        };
                }
            return null;
        }

        static int PickTarget(GameEngine g, PlayerState me, Random rng, bool smart)
        {
            var others = g.Players.Where(p => p != me && p.IsAlive).ToList();
            if (!smart) return others[rng.Next(others.Count)].Seat;
            var noCupcake = others.Where(p => !p.HasCupcake).ToList();
            var pool = noCupcake.Count > 0 ? noCupcake : others;
            int minHp = pool.Min(p => p.Hp);
            var weakest = pool.Where(p => p.Hp == minHp).ToList();
            return weakest[rng.Next(weakest.Count)].Seat;
        }

        static int Find(PlayerState p, ItemType t)
        {
            for (int i = 0; i < p.Items.Count; i++)
                if (p.Items[i] == t) return i;
            return -1;
        }

        static BotDecision Eat(string why) => new BotDecision { Kind = BotActionKind.Eat, Reason = why };

        static BotDecision Give(int seat, string why) => new BotDecision { Kind = BotActionKind.Give, TargetSeat = seat, Reason = why };

        static BotDecision Use(int index, string why) => new BotDecision { Kind = BotActionKind.UseItem, Index = index, Reason = why };
    }
}
