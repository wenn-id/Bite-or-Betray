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
    /// Cara kontrol berbeda untuk VR (pointer + trigger) dan desktop (klik mouse).
    /// </summary>
    public static class OnboardingGuide
    {
        public static List<OnboardingPage> Pages(RuleConfig rules, bool vr)
        {
            var pages = new List<OnboardingPage>
            {
                Page("Cara main",
                    $"Semua pemain mulai dengan HP {rules.StartHp} (maks {rules.MaxHp}).",
                    "Tiap giliran ada satu permen aktif di nampan.",
                    "Warnanya terlihat, isinya good atau bad tersembunyi.",
                    "Pelayan hanya mengumumkan jumlah per warna dan total good/bad.",
                    "HP habis = kalah. Pemain terakhir yang bertahan menang."),
                Page("Mengambil permen", vr
                    ? new[] {
                        "Tunjuk permen di nampan, lalu tahan trigger atau grip.",
                        "Permen ikut tanganmu selama tombol ditahan.",
                        "Lepas tanpa menunjuk bot = permen kembali ke nampan.",
                        "Di giliranmu pilih satu: makan, Give, atau pakai item." }
                    : new[] {
                        "Klik permen, bot, atau item di meja untuk memilihnya.",
                        "Arahkan mouse ke objek dulu untuk melihat keterangannya.",
                        "Di giliranmu pilih satu: makan, Give, atau pakai item.",
                        "Panel kanan bawah berisi tombol yang sama sebagai cadangan." }),
                Page("Makan", new[] {
                        vr ? "Dekatkan permen yang dipegang ke mulutmu." : "Klik permen di nampan.",
                        "Good: giliranmu lanjut dengan permen berikutnya.",
                        "Bad: kamu kena efeknya dan giliranmu berakhir." }
                    .Concat(((CandyColor[])System.Enum.GetValues(typeof(CandyColor))).Select(EffectLine))
                    .Concat(TimerLine(rules)).ToArray()),
                Page("Give (menyuapi)",
                    vr ? "Pegang permen, tunjuk bot lain, lalu lepas tombolnya." : "Klik bot lain untuk menyuapinya.",
                    "Bot itu yang memakan permen dan menanggung efeknya.",
                    "Setelah Give giliranmu selalu berakhir, good atau bad.",
                    "Tidak bisa menyuapi pemain yang sudah kalah.")
            };
            // Tanpa pembagian item, pemain tidak pernah punya item; halaman item tidak perlu.
            if (rules.ItemsPerRound <= 0) return pages;
            pages.Add(Page("Item",
                $"{(rules.DealItemsOnFirstRound ? "Tiap ronde" : "Mulai ronde 2, tiap ronde")} semua pemain dapat {rules.ItemsPerRound} item acak.",
                $"Maks {rules.MaxItems} item; kalau penuh, item baru dibuang.",
                vr ? "Item ada di alas depanmu: tunjuk + trigger untuk memakai." : "Item ada di alas depanmu: klik untuk memakai.",
                "Memakai item tidak mengakhiri giliran; item langsung habis.",
                "Grappling Hook: pilih item lawan. Little Pocket: pilih 1 dari 3.",
                vr ? "Tunjuk item untuk melihat fungsinya." : "Arahkan mouse ke item untuk melihat fungsinya."));
            pages.Add(Page("Daftar item", ItemInfo.All.Select(t => $"{ItemInfo.Name(t)}: {ItemInfo.Description(t)}").ToArray()));
            return pages;
        }

        /// <summary>Efek satu warna saat dimakan, mis. "Merah: good HP +1  ·  bad HP -1 + lumpuh (giliran berikutnya dilewati)".</summary>
        public static string EffectLine(CandyColor color)
        {
            int heal = CandyRules.GoodHeal(color);
            string good = heal == 0 ? "good tanpa efek" : $"good HP +{heal}";
            string bad = $"bad HP -{CandyRules.BadDamage(color)}"
                + (CandyRules.BadParalyzes(color) ? " + lumpuh (giliran berikutnya dilewati)" : "");
            return $"{CandyRules.ColorName(color)}: {good}  ·  {bad}";
        }

        static IEnumerable<string> TimerLine(RuleConfig rules)
        {
            if (rules.TurnTimeLimitSeconds > 0)
                yield return $"Waktu berpikir {rules.TurnTimeLimitSeconds} detik; habis = permen dimakan paksa.";
        }

        static OnboardingPage Page(string title, params string[] lines) => new OnboardingPage { Title = title, Lines = lines };
    }
}
