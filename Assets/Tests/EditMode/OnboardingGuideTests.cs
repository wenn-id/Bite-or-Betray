using System.Linq;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    /// <summary>Isi onboarding harus sama dengan aturan engine: urutan topik, angka RuleConfig, efek permen, dan item.</summary>
    public class OnboardingGuideTests
    {
        static string Text(OnboardingPage page) => page.Title + "\n" + string.Join("\n", page.Lines);

        [Test]
        public void Pages_FollowOrder_TakeEatGiveItem()
        {
            foreach (bool vr in new[] { true, false })
            {
                var titles = OnboardingGuide.Pages(new RuleConfig(), vr).Select(p => p.Title).ToList();
                CollectionAssert.AreEqual(new[] { "Cara main", "Mengambil permen", "Makan", "Give (menyuapi)", "Item", "Daftar item" }, titles);
            }
        }

        [Test]
        public void Numbers_ComeFromRuleConfig()
        {
            var rules = new RuleConfig { StartHp = 3, MaxHp = 7, ItemsPerRound = 1, MaxItems = 4, TurnTimeLimitSeconds = 45 };
            string all = string.Join("\n", OnboardingGuide.Pages(rules, true).Select(Text));
            StringAssert.Contains("HP 3 (maks 7)", all);
            StringAssert.Contains("dapat 1 item acak", all);
            StringAssert.Contains("Maks 4 item", all);
            StringAssert.Contains("45 detik", all);
        }

        [Test]
        public void EffectLines_MatchCandyRules()
        {
            Assert.AreEqual("Kuning: good tanpa efek  ·  bad HP -1", OnboardingGuide.EffectLine(CandyColor.Yellow));
            Assert.AreEqual("Biru: good HP +1  ·  bad HP -2", OnboardingGuide.EffectLine(CandyColor.Blue));
            Assert.AreEqual("Merah: good HP +1  ·  bad HP -1 + lumpuh (giliran berikutnya dilewati)", OnboardingGuide.EffectLine(CandyColor.Red));
            var eat = OnboardingGuide.Pages(new RuleConfig(), true).First(p => p.Title == "Makan");
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
            Assert.IsFalse(pages.Any(p => p.Title.StartsWith("Item") || p.Title == "Daftar item"));
            Assert.IsFalse(pages.Any(p => Text(p).Contains("detik")));
        }

        [Test]
        public void Controls_DifferForVrAndDesktop()
        {
            string vr = string.Join("\n", OnboardingGuide.Pages(new RuleConfig(), true).Select(Text));
            string desktop = string.Join("\n", OnboardingGuide.Pages(new RuleConfig(), false).Select(Text));
            StringAssert.Contains("trigger", vr);
            StringAssert.Contains("ke mulutmu", vr);
            StringAssert.DoesNotContain("Klik", vr);
            StringAssert.DoesNotContain("trigger", desktop);
            StringAssert.Contains("Klik bot", desktop);
        }
    }
}
