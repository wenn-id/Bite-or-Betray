using System.Collections.Generic;
using System.Linq;

namespace BiteOrBetray.Core
{
    /// <summary>One onboarding page: title and explanation lines (unformatted, laid out by the table).</summary>
    public sealed class OnboardingPage
    {
        public string Title;
        public string[] Lines;
    }

    /// <summary>
    /// Brief onboarding before the first game: objective, drawing, eating, Give, then items. Values and effects are read
    /// from RuleConfig, CandyRules, and ItemInfo (the same sources as GameEngine), so text follows rule changes.
    /// Controls differ for VR (pointer + trigger) and desktop (mouse clicks). Text uses Localization.Language.
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
            // Without item dealing, players never have items; skip the item page.
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

        /// <summary>Effect of one color when eaten, e.g. "Red: good HP +1  ·  bad HP -1 + paralyzed (next turn skipped)".</summary>
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
