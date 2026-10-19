using System.Collections.Generic;
using System.Linq;

namespace BiteOrBetray.Core
{
    /// <summary>Satu halaman onboarding: judul dan baris penjelasan (tanpa format, ditata oleh meja).</summary>
    public sealed class OnboardingPage
    {
        public string Title;
        public string[] Lines;
    }

    /// <summary>
    /// Isi onboarding singkat sebelum game pertama: tujuan, mengambil, makan, Give, lalu item. Angka dan efek dibaca
    /// dari RuleConfig, CandyRules, dan ItemInfo (sumber yang sama dengan GameEngine), jadi teks ikut kalau aturan diubah.
    /// Cara kontrol berbeda untuk VR (pointer + trigger) dan desktop (klik mouse). Teks dalam Localization.Language.
    /// </summary>
    public static class OnboardingGuide
    {
        public static List<OnboardingPage> Pages(RuleConfig rules, bool vr)
        {
            var pages = new List<OnboardingPage>
            {
                Page(Strings.GuideRulesTitle.Text,
                    Strings.GuideStartHp.Format(rules.StartHp, rules.MaxHp),
                    Strings.GuideActiveCandy.Text,
                    Strings.GuideHidden.Text,
                    Strings.GuideWaiter.Text,
                    Strings.GuideLose.Text),
                Page(Strings.GuideGrabTitle.Text, vr
                    ? new[] {
                        Strings.GuideGrabVr.Text,
                        Strings.GuideFollowHand.Text,
                        Strings.GuideReleaseReturns.Text,
                        Strings.GuideChooseOne.Text }
                    : new[] {
                        Strings.GuideClickSelect.Text,
                        Strings.GuideHover.Text,
                        Strings.GuideChooseOne.Text,
                        Strings.GuidePanel.Text }),
                Page(Strings.GuideEatTitle.Text, new[] {
                        (vr ? Strings.GuideEatVr : Strings.GuideEatDesktop).Text,
                        Strings.GuideGood.Text,
                        Strings.GuideBad.Text }
                    .Concat(((CandyColor[])System.Enum.GetValues(typeof(CandyColor))).Select(EffectLine))
                    .Concat(TimerLine(rules)).ToArray()),
                Page(Strings.GuideGiveTitle.Text,
                    (vr ? Strings.GuideGiveVr : Strings.GuideGiveDesktop).Text,
                    Strings.GuideGiveEats.Text,
                    Strings.GuideGiveEnds.Text,
                    Strings.GuideGiveOut.Text)
            };
            // Tanpa pembagian item, pemain tidak pernah punya item; halaman item tidak perlu.
            if (rules.ItemsPerRound <= 0) return pages;
            pages.Add(Page(Strings.GuideItemsTitle.Text,
                (rules.DealItemsOnFirstRound ? Strings.GuideItemsEveryRound : Strings.GuideItemsFromRound2).Format(rules.ItemsPerRound),
                Strings.GuideItemsMax.Format(rules.MaxItems),
                (vr ? Strings.GuideItemsUseVr : Strings.GuideItemsUseDesktop).Text,
                Strings.GuideItemsKeepTurn.Text,
                Strings.GuideItemsTargets.Text,
                (vr ? Strings.GuideItemsInfoVr : Strings.GuideItemsInfoDesktop).Text));
            pages.Add(Page(Strings.GuideItemListTitle.Text, ItemInfo.All.Select(t => $"{ItemInfo.Name(t)}: {ItemInfo.Description(t)}").ToArray()));
            return pages;
        }

        /// <summary>Efek satu warna saat dimakan, mis. "Red: good HP +1  ·  bad HP -1 + paralyzed (next turn skipped)".</summary>
        public static string EffectLine(CandyColor color)
        {
            int heal = CandyRules.GoodHeal(color);
            string good = heal == 0 ? Strings.EffectGoodNone.Text : $"good HP +{heal}";
            string bad = $"bad HP -{CandyRules.BadDamage(color)}"
                + (CandyRules.BadParalyzes(color) ? Strings.EffectParalyze.Text : "");
            return $"{CandyRules.ColorName(color)}: {good}  ·  {bad}";
        }

        static IEnumerable<string> TimerLine(RuleConfig rules)
        {
            if (rules.TurnTimeLimitSeconds > 0)
                yield return Strings.GuideTimer.Format(rules.TurnTimeLimitSeconds);
        }

        static OnboardingPage Page(string title, params string[] lines) => new OnboardingPage { Title = title, Lines = lines };
    }
}
