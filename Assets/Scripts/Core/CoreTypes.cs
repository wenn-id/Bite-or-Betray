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

    /// <summary>Apa yang terjadi kalau timer giliran habis.</summary>
    public enum TimeoutRule
    {
        /// <summary>Permen aktif dimakan paksa, lalu giliran berakhir apa pun hasilnya.</summary>
        ForcedEatEndsTurn,
        /// <summary>Permen aktif dimakan paksa dengan aturan eat biasa (good = lanjut, controller memulai timer baru).</summary>
        ForcedEat
    }

    /// <summary>Satu butir permen. Warna selalu terlihat, isi (good/bad) tersembunyi sampai terungkap.</summary>
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

        public string FlavorName => CandyRules.FlavorName(Color, IsGood);

        public override string ToString() => $"{CandyRules.ColorName(Color)} ({(IsGood ? "good" : "bad")})";
    }

    /// <summary>Tabel efek permen sesuai GDD.</summary>
    public static class CandyRules
    {
        public static int GoodHeal(CandyColor c) => c == CandyColor.Yellow ? 0 : 1;

        public static int BadDamage(CandyColor c) => c == CandyColor.Blue ? 2 : 1;

        /// <summary>Batas damage satu permen, termasuk setelah Clover. Mencegah one-turn kill
        /// Fork + Clover + Give dengan biru bad (2 x 2 = 4 = HP awal).</summary>
        public const int MaxDamagePerCandy = 3;

        public static bool BadParalyzes(CandyColor c) => c == CandyColor.Red;

        public static string ColorName(CandyColor c)
        {
            switch (c)
            {
                case CandyColor.Yellow: return "Kuning";
                case CandyColor.Blue: return "Biru";
                default: return "Merah";
            }
        }

        public static string FlavorName(CandyColor c, bool good)
        {
            switch (c)
            {
                case CandyColor.Yellow: return good ? "Mango float" : "Muntah bayi";
                case CandyColor.Blue: return good ? "Leci soda" : "Pemutih pakaian";
                default: return good ? "Strawberry smoothie" : "Hair shampoo";
            }
        }

        public static string EffectSummary(CandyColor c)
        {
            string bad = $"HP -{BadDamage(c)}" + (BadParalyzes(c) ? " + paralyzed" : "");
            return (GoodHeal(c) == 0 ? "Good: tanpa efek" : $"Good: HP +{GoodHeal(c)}") + $"  |  Bad: {bad}";
        }
    }

    public static class ItemInfo
    {
        public static readonly ItemType[] All = (ItemType[])Enum.GetValues(typeof(ItemType));

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
                case ItemType.PlasticFork: return "Tusuk permen aktif, isinya terlihat semua pemain.";
                case ItemType.HammerCrush: return "Hancurkan permen aktif (isinya terlihat), keluarkan permen baru.";
                case ItemType.HotMilk: return "HP +1 (tidak melebihi HP maks).";
                case ItemType.MagicGloves: return "Tukar permen aktif dengan permen baru dari dispenser, tanpa info.";
                case ItemType.Cupcake: return "Kebal terhadap efek 1 permen berikutnya yang dimakan.";
                case ItemType.LittlePocket: return "Buka 3 item acak, pilih 1 dan langsung pakai.";
                case ItemType.GrapplingHook: return "Ambil 1 item pemain lain dan langsung pakai.";
                default: return $"Efek permen aktif x2 untuk siapa pun yang memakannya (damage maks {CandyRules.MaxDamagePerCandy}).";
            }
        }
    }

    /// <summary>
    /// Angka-angka aturan. Default = balancing final (Okt 2026): HP maks 5, mulai 4, spawn hasil simulasi
    /// untuk target durasi 2p 7-10 menit, 3-4p 11-16 menit, 5-6p 16-21 menit.
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
        /// <summary>Rasio bad ditambah sebesar ini untuk setiap kursi di atas 2 (dihitung dari jumlah pemain awal).
        /// Pelayan menyesuaikan nampan dengan ukuran meja, supaya meja besar tidak jauh lebih lama.</summary>
        public float BadRatioPerExtraSeat = 0.04f;

        public int ItemsPerRound = 2;
        public bool DealItemsOnFirstRound = true;

        /// <summary>Batas waktu berpikir per giliran (detik). Waktu animasi tidak dihitung. 0 = tanpa timer.
        /// Engine tidak menghitung waktu; controller (meja test / VR) yang memanggil TimeoutTurn().</summary>
        public int TurnTimeLimitSeconds = 30;
        public TimeoutRule TimeoutAction = TimeoutRule.ForcedEatEndsTurn;

        /// <summary>True: dispenser mengambil permen acak. False: selalu permen paling depan (untuk unit test).</summary>
        public bool RandomDraw = true;

        public RuleConfig Clone() => (RuleConfig)MemberwiseClone();

        /// <summary>Cek kombinasi angka aturan. Gagal = semua masalah digabung dalam satu pesan.
        /// GameEngine.Start memanggil ini dan melempar ArgumentException kalau tidak valid.</summary>
        public ActionResult Validate()
        {
            var errors = new List<string>();
            if (MaxHp < 1) errors.Add($"MaxHp ({MaxHp}) minimal 1.");
            if (StartHp < 1) errors.Add($"StartHp ({StartHp}) minimal 1.");
            else if (StartHp > MaxHp) errors.Add($"StartHp ({StartHp}) tidak boleh lebih dari MaxHp ({MaxHp}).");
            if (MaxItems < 0) errors.Add($"MaxItems ({MaxItems}) tidak boleh negatif.");
            if (ItemsPerRound < 0) errors.Add($"ItemsPerRound ({ItemsPerRound}) tidak boleh negatif.");
            else if (ItemsPerRound > 0 && MaxItems == 0) errors.Add("MaxItems 0 dengan ItemsPerRound > 0: semua item akan dibuang.");

            if (CandiesPerAlivePlayer < 1) errors.Add($"CandiesPerAlivePlayer ({CandiesPerAlivePlayer}) minimal 1.");
            // Satu ronde butuh minimal 1 good dan 1 bad.
            if (MinCandies < 2) errors.Add($"MinCandies ({MinCandies}) minimal 2 (1 good + 1 bad).");
            if (MaxCandies < MinCandies) errors.Add($"MaxCandies ({MaxCandies}) tidak boleh kurang dari MinCandies ({MinCandies}).");

            // Ditulis !(x >= a && x <= b) supaya NaN ikut tertolak.
            bool minOk = MinBadRatio >= 0f && MinBadRatio <= 1f, maxOk = MaxBadRatio >= 0f && MaxBadRatio <= 1f;
            if (!minOk) errors.Add($"MinBadRatio ({MinBadRatio}) harus di antara 0 dan 1.");
            if (!maxOk) errors.Add($"MaxBadRatio ({MaxBadRatio}) harus di antara 0 dan 1.");
            if (minOk && maxOk && MinBadRatio > MaxBadRatio)
                errors.Add($"MinBadRatio ({MinBadRatio}) tidak boleh lebih dari MaxBadRatio ({MaxBadRatio}).");
            if (!(BadRatioPerExtraSeat >= 0f && BadRatioPerExtraSeat <= 1f))
                errors.Add($"BadRatioPerExtraSeat ({BadRatioPerExtraSeat}) harus di antara 0 dan 1.");

            if (TurnTimeLimitSeconds < 0) errors.Add($"TurnTimeLimitSeconds ({TurnTimeLimitSeconds}) tidak boleh negatif (0 = tanpa timer).");
            if (!Enum.IsDefined(typeof(TimeoutRule), TimeoutAction)) errors.Add($"TimeoutAction ({(int)TimeoutAction}) tidak dikenal.");

            return errors.Count == 0 ? ActionResult.Ok : ActionResult.Fail("RuleConfig tidak valid: " + string.Join(" ", errors));
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

    /// <summary>Argumen tambahan untuk item yang butuh target (Grappling Hook).</summary>
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

    /// <summary>Info yang diumumkan pelayan: jumlah per warna dan total good/bad. Pembagian good/bad per warna dirahasiakan.</summary>
    public sealed class RoundAnnouncement
    {
        public int Total, Yellow, Blue, Red, Good, Bad;

        public override string ToString() =>
            $"{Total} permen: {Yellow} kuning, {Blue} biru, {Red} merah  |  {Good} good, {Bad} bad";
    }
}
