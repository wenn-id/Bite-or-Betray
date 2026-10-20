using System.Linq;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    /// <summary>
    /// Onboarding content must match the engine rules: topic order, RuleConfig numbers, candy effects, and items. Default
    /// English; one test ensures the Indonesian version uses the same numbers.
    /// </summary>
    public class OnboardingGuideTests
    {
        static string Text(OnboardingPage page) => page.Title + "\n" + string.Join("\n", page.Lines);

        [SetUp, TearDown]
        public void English() => Localization.Language = Language.English;

        [Test]
        public void Pages_FollowOrder_TakeEatGiveItem()
        {
            foreach (bool vr in new[] { true, false })
            {
                var titles = OnboardingGuide.Pages(new RuleConfig(), vr).Select(p => p.Title).ToList();
                CollectionAssert.AreEqual(new[] { "How to play", "Grabbing candy", "Eating", "Give (feeding)", "Items", "Item list" }, titles);
            }
        }

        [Test]
        public void Numbers_ComeFromRuleConfig()
        {
            var rules = new RuleConfig { StartHp = 3, MaxHp = 7, ItemsPerRound = 1, MaxItems = 4, TurnTimeLimitSeconds = 45 };
            string all = string.Join("\n", OnboardingGuide.Pages(rules, true).Select(Text));
            StringAssert.Contains("HP 3 (max 7)", all);
            StringAssert.Contains("random items: 1 each", all);
            StringAssert.Contains("Max 4 items", all);
            StringAssert.Contains("45 seconds", all);
        }

        [Test]
        public void Indonesian_SameNumbersAndOrder()
        {
            var rules = new RuleConfig { StartHp = 3, MaxHp = 7, ItemsPerRound = 1, MaxItems = 4, TurnTimeLimitSeconds = 45 };
            Localization.Language = Language.Indonesian;
            var pages = OnboardingGuide.Pages(rules, true);
            CollectionAssert.AreEqual(new[] { "Cara main", "Mengambil permen", "Makan", "Give (menyuapi)", "Item", "Daftar item" }, pages.Select(p => p.Title));
            string all = string.Join("\n", pages.Select(Text));
            StringAssert.Contains("HP 3 (maks 7)", all);
            StringAssert.Contains("dapat 1 item acak", all);
            StringAssert.Contains("Maks 4 item", all);
            StringAssert.Contains("45 detik", all);
            StringAssert.Contains("ke mulutmu", all);
            Assert.AreEqual("Merah: good HP +1  ·  bad HP -1 + lumpuh (giliran berikutnya dilewati)", OnboardingGuide.EffectLine(CandyColor.Red));
            CollectionAssert.Contains(pages.Last().Lines, "Hot Milk: HP +1 (tidak melebihi HP maks).");
        }

        [Test]
        public void EffectLines_MatchCandyRules()
        {
            Assert.AreEqual("Yellow: good, no effect  ·  bad HP -1", OnboardingGuide.EffectLine(CandyColor.Yellow));
            Assert.AreEqual("Blue: good HP +1  ·  bad HP -2", OnboardingGuide.EffectLine(CandyColor.Blue));
            Assert.AreEqual("Red: good HP +1  ·  bad HP -1 + paralyzed (next turn skipped)", OnboardingGuide.EffectLine(CandyColor.Red));
            var eat = OnboardingGuide.Pages(new RuleConfig(), true).First(p => p.Title == "Eating");
            foreach (CandyColor c in System.Enum.GetValues(typeof(CandyColor)))
                CollectionAssert.Contains(eat.Lines, OnboardingGuide.EffectLine(c));
        }

        [Test]
        public void ItemList_HasEveryItemWithEngineDescription()
        {
            var list = OnboardingGuide.Pages(new RuleConfig(), false).Last();
            Assert.AreEqual(ItemInfo.All.Length, list.Lines.Length);
            foreach (var item in ItemInfo.All)
                CollectionAssert.Contains(list.Lines, $"{ItemInfo.Name(item)}: {ItemInfo.Description(item)}");
        }

        [Test]
        public void NoTimerAndNoItems_PagesLeftOut()
        {
            var pages = OnboardingGuide.Pages(new RuleConfig { TurnTimeLimitSeconds = 0, ItemsPerRound = 0 }, true);
            Assert.IsFalse(pages.Any(p => p.Title.StartsWith("Item")));
            Assert.IsFalse(pages.Any(p => Text(p).Contains("seconds")));
        }

        [Test]
        public void Controls_DifferForVrAndDesktop()
        {
            string vr = string.Join("\n", OnboardingGuide.Pages(new RuleConfig(), true).Select(Text));
            string desktop = string.Join("\n", OnboardingGuide.Pages(new RuleConfig(), false).Select(Text));
            StringAssert.Contains("trigger", vr);
            StringAssert.Contains("to your mouth", vr);
            StringAssert.DoesNotContain("Click", vr);
            StringAssert.DoesNotContain("trigger", desktop);
            StringAssert.Contains("Click another bot", desktop);
        }
    }
}
