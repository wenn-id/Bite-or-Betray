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
        public string Reason;

        public override string ToString() => $"{Kind} ({Reason})";
    }

    /// <summary>
    /// AI bot sederhana. Bot hanya memakai info publik: sisa good/bad, warna, dan permen yang sudah terungkap.
    /// Easy = setengah acak, Normal = hitung peluang + item dasar, Hard = Normal + combo clover.
    /// </summary>
    public static class BotBrain
    {
        /// <summary>Ambil satu keputusan lalu jalankan. Kalau gagal, fallback ke aksi yang selalu valid.</summary>
        public static ActionResult Step(GameEngine g, Random rng)
        {
            if (g.IsOver || g.Current == null || !g.Current.IsBot) return ActionResult.Fail("Bukan giliran bot.");
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
            if (milk >= 0 && me.Hp <= max - (hard ? 1 : 2)) return Use(milk, "HP kurang, minum susu");

            if (c.Revealed)
            {
                int clover = Find(me, ItemType.CloverCoin);
                if (c.IsGood)
                {
                    if (hard && clover >= 0 && !g.CloverOnActive && !me.HasCupcake
                        && me.Hp + g.GoodHealNow(c.Color) * 2 <= max)
                        return Use(clover, "good + clover");
                    return Eat("permen sudah pasti good");
                }
                if (me.HasCupcake) return Eat("kebal cupcake, aman makan dan lanjut");
                int target = PickTarget(g, me, rng, true);
                if (hard && clover >= 0 && !g.CloverOnActive && !g.Players[target].HasCupcake)
                    return Use(clover, "bad + clover untuk lawan");
                return Give(target, "permen sudah pasti bad");
            }

            int fork = Find(me, ItemType.PlasticFork);
            if (fork >= 0) return Use(fork, "cek isi permen");
            int pocket = Find(me, ItemType.LittlePocket);
            if (pocket >= 0) return Use(pocket, "buka kantong");
            var grab = TryGrapple(g, me, ItemType.PlasticFork);
            if (grab != null) return grab;

            if (me.HasCupcake) return Eat("kebal cupcake");

            if (pGood < 0.5)
            {
                int hammer = Find(me, ItemType.HammerCrush);
                if (hammer >= 0) return Use(hammer, "peluang bad tinggi, hancurkan");
                int cup = Find(me, ItemType.Cupcake);
                if (cup >= 0) return Use(cup, "peluang bad tinggi, pakai cupcake");
                if (hard)
                {
                    grab = TryGrapple(g, me, ItemType.HammerCrush, ItemType.Cupcake);
                    if (grab != null) return grab;
                }
            }

            double threshold = hard && me.Hp <= 3 ? 0.6 : 0.5;
            if (pGood >= threshold) return Eat($"peluang good {pGood:P0}");
            return Give(PickTarget(g, me, rng, true), $"peluang good cuma {pGood:P0}");
        }

        static BotDecision DecideEasy(GameEngine g, PlayerState me, Random rng)
        {
            var c = g.ActiveCandy;
            if (me.Items.Count > 0 && rng.NextDouble() < 0.3)
            {
                int i = rng.Next(me.Items.Count);
                if (me.Items[i] != ItemType.GrapplingHook) return Use(i, "iseng pakai item");
            }
            if (c.Revealed)
                return c.IsGood ? Eat("good") : Give(PickTarget(g, me, rng, false), "bad");
            return rng.NextDouble() < 0.55 ? Eat("nekat") : Give(PickTarget(g, me, rng, false), "lempar ke orang");
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
                return new BotDecision { Kind = BotActionKind.ChoosePocket, Index = pick, Args = a, Reason = "pilih acak" };
            }
            if (best < 0 || bestScore < 0.3)
                return new BotDecision { Kind = BotActionKind.DiscardPocket, Reason = "isi kantong tidak berguna" };
            return new BotDecision { Kind = BotActionKind.ChoosePocket, Index = best, Args = bestArgs, Reason = $"pilih {ItemInfo.Name(opts[best])}" };
        }

        /// <summary>Nilai kegunaan item di situasi saat ini (semakin besar semakin berguna).</summary>
        static double Score(GameEngine g, PlayerState me, ItemType type)
        {
            var c = g.ActiveCandy;
            bool known = c != null && c.Revealed;
            bool good = known && c.IsGood;
            int max = g.Config.MaxHp;
            switch (type)
            {
                case ItemType.PlasticFork: return known ? 0 : 5;
                case ItemType.Cupcake: return me.HasCupcake || good ? 0.1 : 4;
                case ItemType.HammerCrush: return good ? 0 : 3;
                case ItemType.HotMilk: return me.Hp < max ? 4 : 0.5;
                case ItemType.CloverCoin:
                    if (g.CloverOnActive) return 0;
                    if (known && !good) return 3.5;
                    if (good && me.Hp <= max - 4) return 3;
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
                            Reason = $"curi {ItemInfo.Name(w)} dari {t.Name}"
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
