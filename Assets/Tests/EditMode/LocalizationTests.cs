using System.Linq;
using System.Text.RegularExpressions;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    /// <summary>
    /// Tabel teks pemain (BOB-048): setiap kunci punya versi English dan Indonesia dengan placeholder yang sama, dan teks
    /// Core yang tampil di meja (item, permen, error engine, alasan bot) mengikuti bahasa aktif.
    /// </summary>
    public class LocalizationTests
    {
        static readonly Regex Placeholder = new Regex(@"\{(\d+)(:[^}]*)?\}");

        static string[] Placeholders(string text) =>
            Placeholder.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(v => v).ToArray();

        [SetUp, TearDown]
        public void English() => Localization.Language = Language.English;

        [Test]
        public void EveryKey_HasEnglishAndIndonesian()
        {
            var all = Localization.All.ToList();
            Assert.Greater(all.Count, 200, "Tabel Strings terbaca lewat reflection.");
            foreach (var (key, phrase) in all)
            {
                Assert.IsNotNull(phrase, key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(phrase.English), key + " tanpa English");
                Assert.IsFalse(string.IsNullOrWhiteSpace(phrase.Indonesian), key + " tanpa Indonesia");
                CollectionAssert.AreEqual(Placeholders(phrase.English), Placeholders(phrase.Indonesian), key + ": placeholder EN/ID beda");
                // Format dengan argumen contoh tidak boleh melempar (kurung kurawal liar, indeks salah).
                var args = Enumerable.Range(0, 4).Select(i => (object)(i + 1)).ToArray();
                Assert.DoesNotThrow(() => string.Format(phrase.English, args), key);
                Assert.DoesNotThrow(() => string.Format(phrase.Indonesian, args), key);
            }
            Assert.AreEqual(all.Count, all.Select(p => p.phrase).Distinct().Count(), "Satu Phrase dipakai dua kunci.");
        }

        [Test]
        public void DefaultIsEnglish_AndSwitchAppliesImmediately()
        {
            Assert.AreEqual(Language.English, default(Language));
            Assert.AreEqual("Play vs Bots", Strings.PlayVsBots.Text);
            Localization.Language = Language.Indonesian;
            Assert.AreEqual("Main vs Bot", Strings.PlayVsBots.Text);
            Assert.AreEqual("Giliran Blue", Strings.TurnOf.Format("Blue"));
            Localization.Language = (Language)7;
            Assert.AreEqual(Language.English, Localization.Language, "Bahasa tidak dikenal kembali ke English.");
            Assert.AreEqual("Blue's turn", Strings.TurnOf.Format("Blue"));
        }

        [Test]
        public void CoreTexts_FollowLanguage()
        {
            Assert.AreEqual("Red", CandyRules.ColorName(CandyColor.Red));
            Assert.AreEqual("Baby vomit", CandyRules.FlavorName(CandyColor.Yellow, false));
            StringAssert.StartsWith("HP +1", ItemInfo.Description(ItemType.HotMilk));
            StringAssert.Contains($"max damage {CandyRules.MaxDamagePerCandy}", ItemInfo.Description(ItemType.CloverCoin));
            Assert.AreEqual("Hard", Localization.Difficulty(BotDifficulty.Hard));
            Localization.Language = Language.Indonesian;
            Assert.AreEqual("Merah", CandyRules.ColorName(CandyColor.Red));
            Assert.AreEqual("Muntah bayi", CandyRules.FlavorName(CandyColor.Yellow, false));
            StringAssert.Contains($"damage maks {CandyRules.MaxDamagePerCandy}", ItemInfo.Description(ItemType.CloverCoin));
            Assert.AreEqual("Sulit", Localization.Difficulty(BotDifficulty.Hard));
            // Nama item sama di kedua bahasa.
            Assert.AreEqual("Plastic Fork", ItemInfo.Name(ItemType.PlasticFork));
        }

        [Test]
        public void EngineErrorsAndBotReasons_FollowLanguage()
        {
            foreach (var language in new[] { Language.English, Language.Indonesian })
            {
                Localization.Language = language;
                var g = new GameEngine(new RuleConfig { RandomDraw = false }, 3);
                g.Start(new[] { new SeatConfig("A"), new SeatConfig("B", true) });
                Assert.AreEqual(Strings.NoTarget.In(language), g.Give(9).Error);
                Assert.AreEqual(Strings.CannotFeedSelf.In(language), g.Give(g.CurrentSeat).Error);
                Assert.AreEqual(Strings.NoPocketOpen.In(language), g.ChoosePocketItem(0).Error);
            }
            Assert.AreEqual("Target tidak ada.", Strings.NoTarget.Indonesian);
            Assert.AreEqual("That target doesn't exist.", Strings.NoTarget.English);
        }
    }
}
