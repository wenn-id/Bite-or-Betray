using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BiteOrBetray.Core
{
    /// <summary>Player text language. Defaults to English (Devpost judges); Indonesian can be selected in Settings.</summary>
    public enum Language { English, Indonesian }

    /// <summary>One player text in two languages. Placeholders {0}, {1}, ... must match in both versions (checked by EditMode tests).</summary>
    public sealed class Phrase
    {
        public readonly string English, Indonesian;

        public Phrase(string english, string indonesian)
        {
            English = english;
            Indonesian = indonesian;
        }

        public string In(Language language) => language == Language.Indonesian ? Indonesian : English;

        /// <summary>Text in the active language (Localization.Language).</summary>
        public string Text => In(Localization.Language);

        public string Format(params object[] args) => string.Format(Text, args);

        public override string ToString() => Text;
    }

    /// <summary>
    /// Active language for all player text. The director sets it from GameSettings at startup and when players change it
    /// in Settings; text is read again each time it is shown, so switching languages needs no restart. Debug/telemetry logs
    /// deliberately do not use this table.
    /// </summary>
    public static class Localization
    {
        static Language language = Language.English;

        public static Language Language
        {
            get => language;
            set => language = Enum.IsDefined(typeof(Language), value) ? value : Language.English;
        }

        /// <summary>English > Indonesian > English.</summary>
        public static Language Next(Language l) => l == Language.Indonesian ? Language.English : Language.Indonesian;

        /// <summary>Language name in its own language, so players can read it after choosing the wrong language.</summary>
        public static string NativeName(Language l) => l == Language.Indonesian ? "Bahasa Indonesia" : "English";

        public static string Difficulty(BotDifficulty d) =>
            (d == BotDifficulty.Easy ? Strings.Easy : d == BotDifficulty.Hard ? Strings.Hard : Strings.Normal).Text;

        /// <summary>All Strings entries with their field names (for completeness tests).</summary>
        public static IEnumerable<(string key, Phrase phrase)> All =>
            typeof(Strings).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(Phrase)).Select(f => (f.Name, (Phrase)f.GetValue(null)));
    }

    /// <summary>
    /// Player text table: menus, bot setup, onboarding, turn narration, boards, seat labels, items/candy, final
    /// results, lobby, Settings, voice, error messages, and waiter bubbles. Item names (Plastic Fork, etc.) and bot names
    /// are the same in both languages.
    /// </summary>
    public static class Strings
    {
        // ------------------------------------------------------------------ main menu and Settings
        public static readonly Phrase PlayVsBots = new Phrase("Play vs Bots", "Main vs Bot");
        public static readonly Phrase CreateRoom = new Phrase("Create Room", "Buat Room");
        public static readonly Phrase JoinRoom = new Phrase("Join Room", "Gabung Room");
        public static readonly Phrase Settings = new Phrase("Settings", "Settings");
        public static readonly Phrase OnlineUnavailableNote = new Phrase("online not available yet", "online belum tersedia");
        public static readonly Phrase MenuHintVr = new Phrase("point at a menu button on the table + trigger", "tunjuk tombol menu di meja + trigger");
        public static readonly Phrase MenuHintDesktop = new Phrase("click a menu button on the table", "klik tombol menu di meja");
        public static readonly Phrase OnlineUnavailable = new Phrase("Online not available yet", "Online belum tersedia");
        public static readonly Phrase OnlineUnavailableDetail = new Phrase("pick Play vs Bots for now", "untuk sekarang pilih Main vs Bot");
        public static readonly Phrase RoomCreateFailed = new Phrase("Could not create the room", "Room gagal dibuat");
        public static readonly Phrase RoomJoinFailed = new Phrase("Could not join the room", "Gagal gabung room");
        public static readonly Phrase Back = new Phrase("Back", "Kembali");
        public static readonly Phrase Volume = new Phrase("Volume {0}%", "Volume {0}%");
        public static readonly Phrase DominantHand = new Phrase("Dominant hand", "Tangan dominan");
        public static readonly Phrase DominantHandValue = new Phrase("Dominant hand: {0}", "Tangan dominan: {0}");
        public static readonly Phrase HandLeft = new Phrase("Left", "Kiri");
        public static readonly Phrase HandRight = new Phrase("Right", "Kanan");
        public static readonly Phrase DominantHandTip = new Phrase("The hand that wins when both hands act at the same time",
            "Tangan yang didahulukan kalau kedua tangan beraksi bersamaan");
        public static readonly Phrase MouthSnap = new Phrase("Mouth snap", "Snap ke mulut");
        public static readonly Phrase MouthSnapValue = new Phrase("Mouth snap: {0}", "Snap ke mulut: {0}");
        public static readonly Phrase SnapLow = new Phrase("Low", "Rendah");
        public static readonly Phrase SnapNormal = new Phrase("Normal", "Normal");
        public static readonly Phrase SnapHigh = new Phrase("High", "Tinggi");
        public static readonly Phrase MouthSnapTip = new Phrase("How close the candy must be to your mouth to be eaten",
            "Seberapa dekat permen ke mulut supaya dimakan");
        public static readonly Phrase LanguageButton = new Phrase("Language", "Bahasa");
        public static readonly Phrase LanguageValue = new Phrase("Language: {0}", "Bahasa: {0}");
        public static readonly Phrase LanguageTip = new Phrase("Switch between English and Bahasa Indonesia", "Ganti antara English dan Bahasa Indonesia");

        // ------------------------------------------------------------------ voice
        public static readonly Phrase VoiceSettings = new Phrase("Voice Settings", "Settings Voice");
        public static readonly Phrase VoiceVolume = new Phrase("Voice {0}%", "Voice {0}%");
        public static readonly Phrase Microphone = new Phrase("Microphone", "Mikrofon");
        public static readonly Phrase MicrophoneValue = new Phrase("Microphone: {0}", "Mikrofon: {0}");
        public static readonly Phrase MicMuted = new Phrase("Muted", "Mute");
        public static readonly Phrase MicOn = new Phrase("On", "Nyala");
        public static readonly Phrase MicrophoneTip = new Phrase("Mute your own microphone; you still hear the room. Spectators can talk too.",
            "Mute mikrofon sendiri; tetap mendengar room. Spectator juga boleh bicara.");
        public static readonly Phrase VoiceChat = new Phrase("Voice chat", "Voice chat");
        public static readonly Phrase VoiceUnavailable = new Phrase("Voice not available yet: check microphone permission, internet and the Vivox service.",
            "Voice belum tersedia: cek izin mikrofon, internet dan layanan Vivox.");

        // ------------------------------------------------------------------ bot setup
        public static readonly Phrase SetupTitle = new Phrase("Table setup", "Atur meja");
        public static readonly Phrase SetupHintVr = new Phrase("point at a button + trigger: number of bots, each bot's level (above its head), then Start",
            "tunjuk tombol + trigger: jumlah bot, tingkat tiap bot (di atas kepalanya), lalu Mulai");
        public static readonly Phrase SetupHintDesktop = new Phrase("click the buttons on the table: number of bots, each bot's level (above its head), then Start (or R)",
            "klik tombol di meja: jumlah bot, tingkat tiap bot (di atas kepalanya), lalu Mulai (atau R)");
        public static readonly Phrase Start = new Phrase("Start", "Mulai");
        public static readonly Phrase FewerBots = new Phrase("Fewer bots", "Kurangi bot");
        public static readonly Phrase MoreBots = new Phrase("More bots", "Tambah bot");
        public static readonly Phrase AllBots = new Phrase("All bots", "Semua bot");
        public static readonly Phrase AllBotsValue = new Phrase("All bots: {0}", "Semua bot: {0}");
        public static readonly Phrase Mixed = new Phrase("Mixed", "Campur");
        public static readonly Phrase AllBotsTip = new Phrase("Give every bot the same level", "Samakan tingkat semua bot");
        public static readonly Phrase ChangeLevelTip = new Phrase("Change {0}'s level", "Ganti tingkat {0}");
        public static readonly Phrase BotCount = new Phrase("Bots: {0}", "{0} bot");
        public static readonly Phrase Easy = new Phrase("Easy", "Mudah");
        public static readonly Phrase Normal = new Phrase("Normal", "Normal");
        public static readonly Phrase Hard = new Phrase("Hard", "Sulit");

        // ------------------------------------------------------------------ onboarding (buttons and narration)
        public static readonly Phrase Next = new Phrase("Next", "Lanjut");
        public static readonly Phrase Skip = new Phrase("Skip", "Lewati");
        public static readonly Phrase StartPlaying = new Phrase("Start playing", "Mulai main");
        public static readonly Phrase StartGameTip = new Phrase("Start the game", "Mulai game");
        public static readonly Phrase NextPageTip = new Phrase("Next page", "Halaman berikutnya");
        public static readonly Phrase OnboardingTitle = new Phrase("How to play {0}/{1}", "Cara main {0}/{1}");
        public static readonly Phrase OnboardingHintVr = new Phrase("point at Next + trigger  ·  B/Y or Skip = play right away",
            "tunjuk Lanjut + trigger  ·  B/Y atau Lewati = langsung main");
        public static readonly Phrase OnboardingHintDesktop = new Phrase("click Next  ·  Esc or Skip = play right away",
            "klik Lanjut  ·  Esc atau Lewati = langsung main");

        // ------------------------------------------------------------------ onboarding content (OnboardingGuide)
        public static readonly Phrase GuideRulesTitle = new Phrase("How to play", "Cara main");
        public static readonly Phrase GuideStartHp = new Phrase("Every player starts with HP {0} (max {1}).", "Semua pemain mulai dengan HP {0} (maks {1}).");
        public static readonly Phrase GuideActiveCandy = new Phrase("Each turn there is one active candy on the tray.", "Tiap giliran ada satu permen aktif di nampan.");
        public static readonly Phrase GuideHidden = new Phrase("Its color is visible; whether it is good or bad is hidden.", "Warnanya terlihat, isinya good atau bad tersembunyi.");
        public static readonly Phrase GuideWaiter = new Phrase("The waiter only announces the count per color and the total good/bad.",
            "Pelayan hanya mengumumkan jumlah per warna dan total good/bad.");
        public static readonly Phrase GuideLose = new Phrase("HP runs out = you lose. The last player standing wins.", "HP habis = kalah. Pemain terakhir yang bertahan menang.");
        public static readonly Phrase GuideGrabTitle = new Phrase("Grabbing candy", "Mengambil permen");
        public static readonly Phrase GuideGrabVr = new Phrase("Point at the candy on the tray, then hold trigger or grip.", "Tunjuk permen di nampan, lalu tahan trigger atau grip.");
        public static readonly Phrase GuideFollowHand = new Phrase("The candy follows your hand while you hold the button.", "Permen ikut tanganmu selama tombol ditahan.");
        public static readonly Phrase GuideReleaseReturns = new Phrase("Release without pointing at a bot = the candy goes back to the tray.",
            "Lepas tanpa menunjuk bot = permen kembali ke nampan.");
        public static readonly Phrase GuideChooseOne = new Phrase("On your turn pick one: eat, Give, or use an item.", "Di giliranmu pilih satu: makan, Give, atau pakai item.");
        public static readonly Phrase GuideClickSelect = new Phrase("Click a candy, bot, or item on the table to select it.", "Klik permen, bot, atau item di meja untuk memilihnya.");
        public static readonly Phrase GuideHover = new Phrase("Hover the mouse over an object first to see what it does.", "Arahkan mouse ke objek dulu untuk melihat keterangannya.");
        public static readonly Phrase GuidePanel = new Phrase("The bottom-right panel has the same buttons as a backup.", "Panel kanan bawah berisi tombol yang sama sebagai cadangan.");
        public static readonly Phrase GuideEatTitle = new Phrase("Eating", "Makan");
        public static readonly Phrase GuideEatVr = new Phrase("Bring the candy you are holding to your mouth.", "Dekatkan permen yang dipegang ke mulutmu.");
        public static readonly Phrase GuideEatDesktop = new Phrase("Click the candy on the tray.", "Klik permen di nampan.");
        public static readonly Phrase GuideGood = new Phrase("Good: your turn goes on with the next candy.", "Good: giliranmu lanjut dengan permen berikutnya.");
        public static readonly Phrase GuideBad = new Phrase("Bad: you take its effect and your turn ends.", "Bad: kamu kena efeknya dan giliranmu berakhir.");
        public static readonly Phrase GuideTimer = new Phrase("{0} seconds to think; time's up = the candy is force-eaten.",
            "Waktu berpikir {0} detik; habis = permen dimakan paksa.");
        public static readonly Phrase EffectGoodNone = new Phrase("good, no effect", "good tanpa efek");
        public static readonly Phrase EffectParalyze = new Phrase(" + paralyzed (next turn skipped)", " + lumpuh (giliran berikutnya dilewati)");
        public static readonly Phrase GuideGiveTitle = new Phrase("Give (feeding)", "Give (menyuapi)");
        public static readonly Phrase GuideGiveVr = new Phrase("Hold a candy, point at another bot, then release the button.", "Pegang permen, tunjuk bot lain, lalu lepas tombolnya.");
        public static readonly Phrase GuideGiveDesktop = new Phrase("Click another bot to feed it.", "Klik bot lain untuk menyuapinya.");
        public static readonly Phrase GuideGiveEats = new Phrase("That bot eats the candy and takes its effect.", "Bot itu yang memakan permen dan menanggung efeknya.");
        public static readonly Phrase GuideGiveEnds = new Phrase("After Give your turn always ends, good or bad.", "Setelah Give giliranmu selalu berakhir, good atau bad.");
        public static readonly Phrase GuideGiveOut = new Phrase("You can't feed a player who is already out.", "Tidak bisa menyuapi pemain yang sudah kalah.");
        public static readonly Phrase GuideItemsTitle = new Phrase("Items", "Item");
        public static readonly Phrase GuideItemsEveryRound = new Phrase("Every round all players get random items: {0} each.", "Tiap ronde semua pemain dapat {0} item acak.");
        public static readonly Phrase GuideItemsFromRound2 = new Phrase("From round 2, every round all players get random items: {0} each.",
            "Mulai ronde 2, tiap ronde semua pemain dapat {0} item acak.");
        public static readonly Phrase GuideItemsMax = new Phrase("Max {0} items; when full, new items are discarded.", "Maks {0} item; kalau penuh, item baru dibuang.");
        public static readonly Phrase GuideItemsUseVr = new Phrase("Items sit on the mat in front of you: point + trigger to use one.",
            "Item ada di alas depanmu: tunjuk + trigger untuk memakai.");
        public static readonly Phrase GuideItemsUseDesktop = new Phrase("Items sit on the mat in front of you: click to use one.", "Item ada di alas depanmu: klik untuk memakai.");
        public static readonly Phrase GuideItemsKeepTurn = new Phrase("Using an item does not end your turn; the item is used up.",
            "Memakai item tidak mengakhiri giliran; item langsung habis.");
        public static readonly Phrase GuideItemsTargets = new Phrase("Grappling Hook: pick an opponent's item. Little Pocket: pick 1 of 3.",
            "Grappling Hook: pilih item lawan. Little Pocket: pilih 1 dari 3.");
        public static readonly Phrase GuideItemsInfoVr = new Phrase("Point at an item to see what it does.", "Tunjuk item untuk melihat fungsinya.");
        public static readonly Phrase GuideItemsInfoDesktop = new Phrase("Hover the mouse over an item to see what it does.", "Arahkan mouse ke item untuk melihat fungsinya.");
        public static readonly Phrase GuideItemListTitle = new Phrase("Item list", "Daftar item");

        // ------------------------------------------------------------------ candy and items
        public static readonly Phrase Yellow = new Phrase("Yellow", "Kuning");
        public static readonly Phrase Blue = new Phrase("Blue", "Biru");
        public static readonly Phrase Red = new Phrase("Red", "Merah");
        public static readonly Phrase FlavorYellowGood = new Phrase("Mango float", "Mango float");
        public static readonly Phrase FlavorYellowBad = new Phrase("Baby vomit", "Muntah bayi");
        public static readonly Phrase FlavorBlueGood = new Phrase("Lychee soda", "Leci soda");
        public static readonly Phrase FlavorBlueBad = new Phrase("Laundry bleach", "Pemutih pakaian");
        public static readonly Phrase FlavorRedGood = new Phrase("Strawberry smoothie", "Strawberry smoothie");
        public static readonly Phrase FlavorRedBad = new Phrase("Hair shampoo", "Hair shampoo");
        public static readonly Phrase PlasticForkInfo = new Phrase("Pierce the active candy; every player sees what's inside.",
            "Tusuk permen aktif, isinya terlihat semua pemain.");
        public static readonly Phrase HammerCrushInfo = new Phrase("Smash the active candy (its content is shown) and release a new one.",
            "Hancurkan permen aktif (isinya terlihat), keluarkan permen baru.");
        public static readonly Phrase HotMilkInfo = new Phrase("HP +1 (never above max HP).", "HP +1 (tidak melebihi HP maks).");
        public static readonly Phrase MagicGlovesInfo = new Phrase("Swap the active candy for a new one from the dispenser, without info.",
            "Tukar permen aktif dengan permen baru dari dispenser, tanpa info.");
        public static readonly Phrase CupcakeInfo = new Phrase("Immune to the effect of the next candy you eat.", "Kebal terhadap efek 1 permen berikutnya yang dimakan.");
        public static readonly Phrase LittlePocketInfo = new Phrase("Open 3 random items, pick 1 and use it right away.", "Buka 3 item acak, pilih 1 dan langsung pakai.");
        public static readonly Phrase GrapplingHookInfo = new Phrase("Take 1 item from another player and use it right away.", "Ambil 1 item pemain lain dan langsung pakai.");
        public static readonly Phrase CloverCoinInfo = new Phrase("Active candy effect x2 for whoever eats it (max damage {0}).",
            "Efek permen aktif x2 untuk siapa pun yang memakannya (damage maks {0}).");

        // ------------------------------------------------------------------ game flow and narration
        public static readonly Phrase You = new Phrase("You", "Kamu");
        public static readonly Phrase NewGame = new Phrase("New game! Every player rolls a d20 for the turn order.", "Game baru! Semua pemain melempar d20 untuk urutan giliran.");
        public static readonly Phrase TurnOrder = new Phrase("Turn order: {0}", "Urutan giliran: {0}");
        public static readonly Phrase YourTurn = new Phrase("Your turn!", "Giliran kamu!");
        public static readonly Phrase TurnOf = new Phrase("{0}'s turn", "Giliran {0}");
        public static readonly Phrase SafeContinue = new Phrase("Safe! Keep going, next candy.", "Aman! Lanjut, permen berikutnya.");
        public static readonly Phrase BotContinues = new Phrase("{0} keeps going, next candy!", "{0} lanjut, permen berikutnya!");
        public static readonly Phrase TimeUp = new Phrase("Time's up!", "Waktu habis!");
        public static readonly Phrase TimeUpReason = new Phrase("time's up, the candy was force-eaten", "waktu habis, permen dimakan paksa");
        public static readonly Phrase UsesItem = new Phrase("{0} uses {1}.", "{0} memakai {1}.");
        public static readonly Phrase PicksFromPocket = new Phrase("{0} picks {1} from the Little Pocket.", "{0} memilih {1} dari Little Pocket.");
        public static readonly Phrase DiscardsPocket = new Phrase("{0} discards the Little Pocket.", "{0} membuang isi Little Pocket.");
        public static readonly Phrase PocketPrompt = new Phrase("Pick 1 item from the Little Pocket", "Pilih 1 item dari Little Pocket");
        public static readonly Phrase PocketPromptDetail = new Phrase("pick one of the floating items, or the Discard button",
            "pilih salah satu item yang melayang, atau tombol Buang isi");
        public static readonly Phrase YourTurnHintVr = new Phrase("grab the candy: to your mouth = eat, aim at a bot and release = feed  ·  point at an item + trigger = use",
            "ambil permen: dekatkan ke mulut = makan, arahkan ke bot lalu lepas = suapi  ·  tunjuk item + trigger = pakai");
        public static readonly Phrase YourTurnHintDesktop = new Phrase("click the candy = eat  ·  click a bot = feed  ·  click an item on your mat = use",
            "klik permen = makan  ·  klik bot = suapi  ·  klik item di alasmu = pakai");
        public static readonly Phrase EatTip = new Phrase("Eat this candy", "Makan permen ini");
        public static readonly Phrase FeedTip = new Phrase("Feed the candy to {0}", "Suapkan permen ke {0}");
        public static readonly Phrase StealTip = new Phrase("Steal {0} from {1}", "Curi {0} dari {1}");
        public static readonly Phrase NothingToSteal = new Phrase("No opponent item to steal", "Tidak ada item lawan yang bisa dicuri");
        public static readonly Phrase GrappleTitle = new Phrase("Grappling Hook: pick an opponent's item", "Grappling Hook: pilih item lawan");
        public static readonly Phrase GrappleHintVr = new Phrase("point at an item on another bot's mat  ·  B/Y or the Cancel button",
            "tunjuk item di alas bot lain  ·  B/Y atau tombol Batal");
        public static readonly Phrase GrappleHintDesktop = new Phrase("click an item on another bot's mat  ·  Esc to cancel", "klik item di alas bot lain  ·  Esc untuk batal");
        public static readonly Phrase Discard = new Phrase("Discard", "Buang isi");
        public static readonly Phrase Cancel = new Phrase("Cancel", "Batal");
        public static readonly Phrase Eats = new Phrase("{0} eats a {1} candy.", "{0} memakan permen {1}.");
        public static readonly Phrase Feeds = new Phrase("{0} feeds a {1} candy to {2}!", "{0} menyuapkan permen {1} ke {2}!");
        public static readonly Phrase ImmuneCupcake = new Phrase("Immune (cupcake)", "Kebal (cupcake)");
        public static readonly Phrase DispenserEmpty = new Phrase("Dispenser empty", "Dispenser kosong");
        public static readonly Phrase ImmuneOnce = new Phrase("Immune 1x", "Kebal 1x");
        public static readonly Phrase PocketContents = new Phrase("Inside: {0}", "Isi: {0}");
        public static readonly Phrase Grapples = new Phrase("{0} fires the Grappling Hook at {1} and takes {2}!",
            "{0} menembakkan Grappling Hook ke {1} dan mengambil {2}!");
        public static readonly Phrase ParalyzedPopup = new Phrase("Paralyzed!", "Lumpuh!");
        public static readonly Phrase YouPoisoned = new Phrase("You're poisoned! Out of HP, you lose.", "Kamu keracunan! HP habis, kamu kalah.");
        public static readonly Phrase PlayerPoisoned = new Phrase("{0} is poisoned! Out of HP, {0} is out.", "{0} keracunan! HP habis, {0} kalah.");
        public static readonly Phrase SpectatorRest = new Phrase("watch the rest of the game as a spectator", "sisa game ditonton sebagai spectator");
        public static readonly Phrase YouStillParalyzed = new Phrase("You're still paralyzed, your turn is skipped.", "Kamu masih lumpuh, giliranmu dilewati.");
        public static readonly Phrase PlayerStillParalyzed = new Phrase("{0} is still paralyzed, their turn is skipped.", "{0} masih lumpuh, gilirannya dilewati.");

        // ------------------------------------------------------------------ waiter, board and seat labels
        public static readonly Phrase WaiterRound = new Phrase("Round {0}!", "Ronde {0}!");
        public static readonly Phrase WaiterCounts = new Phrase("{0} candies: {1} yellow, {2} blue, {3} red", "{0} permen: {1} kuning, {2} biru, {3} merah");
        public static readonly Phrase WaiterBrings = new Phrase("The waiter brings the round {0} tray.", "Pelayan membawa nampan ronde {0}.");
        public static readonly Phrase GoodBadOf = new Phrase("{0} good, {1} bad out of {2} candies", "{0} good, {1} bad dari {2} permen");
        public static readonly Phrase ItemsDealt = new Phrase("Items +{0}", "+{0} item");
        public static readonly Phrase Board = new Phrase("Round {0}  ·  Dispenser: {1} candies\nLeft: {2} good, {3} bad", "Ronde {0}  ·  Dispenser: {1} permen\nSisa: {2} good, {3} bad");
        public static readonly Phrase BoardTimer = new Phrase("Time: {0} s", "Waktu: {0} detik");
        public static readonly Phrase SeatStatus = new Phrase("HP {0}/{1}  ·  Items {2}", "HP {0}/{1}  ·  {2} item");
        public static readonly Phrase Paralyzed = new Phrase("Paralyzed", "Lumpuh");
        public static readonly Phrase Immune = new Phrase("Immune", "Kebal");

        // ------------------------------------------------------------------ panel desktop
        public static readonly Phrase PanelYourTurn = new Phrase("Your turn", "Giliran kamu");
        public static readonly Phrase PanelSeconds = new Phrase("{0} s", "{0} detik");
        public static readonly Phrase PanelEat = new Phrase("Eat candy", "Makan permen");
        public static readonly Phrase PanelFeedTo = new Phrase("Feed to:", "Suapkan ke:");
        public static readonly Phrase PanelUseItem = new Phrase("Use item:", "Pakai item:");
        public static readonly Phrase PanelPocket = new Phrase("Little Pocket, pick 1:", "Little Pocket, pilih 1:");
        public static readonly Phrase PanelDiscard = new Phrase("Discard it (X)", "Buang isinya (X)");
        public static readonly Phrase PanelGrapple = new Phrase("Grappling Hook, steal an item:", "Grappling Hook, curi item:");
        public static readonly Phrase PanelCancel = new Phrase("Cancel (Esc)", "Batal (Esc)");

        // ------------------------------------------------------------------ final results
        public static readonly Phrase PlayAgain = new Phrase("Play again", "Main lagi");
        public static readonly Phrase BackToMenu = new Phrase("Back to menu", "Kembali ke menu");
        public static readonly Phrase NoWinner = new Phrase("No winner.", "Tidak ada pemenang.");
        public static readonly Phrase YouWin = new Phrase("YOU WIN!", "KAMU MENANG!");
        public static readonly Phrase PlayerWins = new Phrase("{0} WINS!", "{0} MENANG!");
        public static readonly Phrase WinPopup = new Phrase("WIN!", "MENANG!");
        public static readonly Phrase Winner = new Phrase("WINNER", "MENANG");
        public static readonly Phrase OutOnTurn = new Phrase("out on turn {0}", "kalah di giliran {0}");
        public static readonly Phrase GameOverSummary = new Phrase("{0} rounds, {1} turns", "{0} ronde, {1} giliran");
        public static readonly Phrase PlayAgainHintVr = new Phrase("point at Play again", "tunjuk Main lagi");
        public static readonly Phrase PlayAgainHintDesktop = new Phrase("click Play again (or R)", "klik Main lagi (atau R)");
        public static readonly Phrase OrBackToMenu = new Phrase(" or Back to menu", " atau Kembali ke menu");

        // ------------------------------------------------------------------ online: table
        public static readonly Phrase WaitingForHost = new Phrase("Waiting for the host to start the game.", "Menunggu host memulai game.");
        public static readonly Phrase HostLost = new Phrase("Lost connection to the host", "Koneksi ke host terputus");
        public static readonly Phrase HostLostDetail = new Phrase("the online game ended, back to the main menu", "game online berakhir, kembali ke menu utama");
        public static readonly Phrase Disconnected = new Phrase("Disconnected", "Terputus");
        public static readonly Phrase ReplacedByBot = new Phrase("replaced by a bot", "diganti bot");
        public static readonly Phrase SeatDisconnected = new Phrase("{0} disconnected", "{0} terputus");
        public static readonly Phrase SeatDisconnectedDetail = new Phrase("a bot plays this seat, the game goes on", "kursinya dimainkan bot, game lanjut");
        public static readonly Phrase ChoosePocketItem = new Phrase("Choosing a Little Pocket item", "Pilih item Little Pocket");
        public static readonly Phrase WaitingForPlayer = new Phrase("Waiting for the player's move", "Menunggu aksi pemain");
        public static readonly Phrase ActionRejected = new Phrase("Action rejected by the host", "Aksi ditolak host");
        public static readonly Phrase PlayerNumber = new Phrase("Player {0}", "Pemain {0}");
        public static readonly Phrase NeedHumans = new Phrase("At least {0} human players are needed.", "Butuh minimal {0} pemain manusia.");
        public static readonly Phrase TooManyPlayers = new Phrase("More players than seats.", "Pemain lebih banyak dari kursi.");
        public static readonly Phrase WaitPreviousAction = new Phrase("Wait for the previous action to finish.", "Tunggu aksi sebelumnya selesai.");
        public static readonly Phrase InvalidActionPacket = new Phrase("Invalid action packet.", "Paket aksi tidak valid.");
        public static readonly Phrase HostEngineInactive = new Phrase("The host engine is not running yet.", "Engine host belum aktif.");
        public static readonly Phrase UnknownAction = new Phrase("Unknown action.", "Aksi tidak dikenal.");
        public static readonly Phrase StaleAction = new Phrase("The table changed before this action arrived.", "State aksi sudah berubah.");
        public static readonly Phrase NotYourTurn = new Phrase("Not your turn.", "Bukan giliranmu.");
        public static readonly Phrase SeatNotSender = new Phrase("That seat does not belong to the sender.", "Kursi bukan milik pengirim.");

        // ------------------------------------------------------------------ online: lobby and room code
        public static readonly Phrase JoinHintVr = new Phrase("point at the digits + trigger, then Join", "tunjuk angka + trigger, lalu Gabung");
        public static readonly Phrase JoinHintDesktop = new Phrase("click the digits or type 0-9, then Join (Enter)", "klik angka atau ketik 0-9, lalu Gabung (Enter)");
        public static readonly Phrase LeaveRoom = new Phrase("Leave room", "Keluar room");
        public static readonly Phrase LeftRoom = new Phrase("Left the room", "Keluar dari room");
        public static readonly Phrase DigitTip = new Phrase("Digit {0}", "Angka {0}");
        public static readonly Phrase Erase = new Phrase("Delete", "Hapus");
        public static readonly Phrase Join = new Phrase("Join", "Gabung");
        public static readonly Phrase Searching = new Phrase("searching...", "mencari...");
        public static readonly Phrase SixDigits = new Phrase("6 digits", "6 angka");
        public static readonly Phrase EnterCode = new Phrase("Enter the room code", "Masukkan kode room");
        public static readonly Phrase RoomCode = new Phrase("Room code", "Kode room");
        public static readonly Phrase PlayersInRoom = new Phrase("{0}/{1} players", "{0}/{1} pemain");
        public static readonly Phrase EmptySeatsBots = new Phrase("{0} empty seats filled by bots", "{0} kursi kosong diisi bot");
        public static readonly Phrase TableFull = new Phrase("table full", "meja penuh");
        public static readonly Phrase WaitingForHostShort = new Phrase("waiting for the host to start the game", "menunggu host memulai game");
        public static readonly Phrase NeedPlayers = new Phrase("needs {0} players", "butuh {0} pemain");
        public static readonly Phrase RoomTitle = new Phrase("Room {0}", "Room {0}");
        public static readonly Phrase PressStart = new Phrase("press Start once everyone is in", "tekan Mulai kalau semua sudah masuk");
        public static readonly Phrase ShareCode = new Phrase("share this code with your friends, then wait for them to join",
            "bagikan kode ini ke teman, lalu tunggu mereka gabung");
        public static readonly Phrase CreatingRoom = new Phrase("Creating room...", "Membuat room...");
        public static readonly Phrase ConnectingServer = new Phrase("connecting to the server", "menghubungkan ke server");
        public static readonly Phrase NoFreeCode = new Phrase("Could not find a free room code, try again.", "Tidak menemukan kode room kosong, coba lagi.");
        public static readonly Phrase SearchingRoom = new Phrase("Looking for room {0}...", "Mencari room {0}...");
        public static readonly Phrase CodeNotFound = new Phrase("Code {0} not found", "Kode {0} tidak ditemukan");
        public static readonly Phrase CodeNotFoundDetail = new Phrase("check the code; the room may also be full or already started",
            "cek lagi kodenya; room bisa juga penuh atau sudah mulai");
        public static readonly Phrase JoiningRoom = new Phrase("Joining room {0}...", "Masuk room {0}...");
        public static readonly Phrase ConnectingHost = new Phrase("connecting to the host", "menyambung ke host");
        public static readonly Phrase JoinedRoom = new Phrase("Joined room {0}", "Masuk room {0}");
        public static readonly Phrase StartFailed = new Phrase("The game could not start", "Game gagal dimulai");
        public static readonly Phrase RoomClosed = new Phrase("Room closed", "Room ditutup");
        public static readonly Phrase RoomClosedDetail = new Phrase("the host left or the room connection dropped", "host keluar atau koneksi ke room putus");
        public static readonly Phrase HostConnectionLost = new Phrase("Connection to the host lost", "Koneksi ke host putus");
        public static readonly Phrase LoginFailed = new Phrase("Unity Gaming Services login failed; check the internet and the UGS project",
            "login Unity Gaming Services gagal; cek internet dan project UGS");
        public static readonly Phrase ServerRejected = new Phrase("server rejected ({0}): {1}", "server menolak ({0}): {1}");
        public static readonly Phrase UnknownError = new Phrase("unknown error", "error tidak dikenal");

        // ------------------------------------------------------------------ input VR (tooltip pointer)
        public static readonly Phrase ReleaseTo = new Phrase("Release = {0}", "Lepas = {0}");
        public static readonly Phrase HoldHint = new Phrase("Bring to your mouth = eat\nAim at a bot and release = feed", "Dekatkan ke mulut = makan\nArahkan ke bot lalu lepas = suapi");
        public static readonly Phrase PinchHint = new Phrase("\nIndex pinch = select / hold = grab", "\nPinch telunjuk = pilih / tahan = pegang");

        // ------------------------------------------------------------------ engine action rejection (popup above the player)
        public static readonly Phrase GameAlreadyOver = new Phrase("The game is already over.", "Game sudah selesai.");
        public static readonly Phrase PocketFirst = new Phrase("Pick an item from the Little Pocket first.", "Pilih item dari Little Pocket dulu.");
        public static readonly Phrase CannotActYet = new Phrase("You can't act yet.", "Belum bisa beraksi.");
        public static readonly Phrase NoTarget = new Phrase("That target doesn't exist.", "Target tidak ada.");
        public static readonly Phrase CannotFeedSelf = new Phrase("You can't feed yourself, use Eat.", "Tidak bisa menyuapi diri sendiri, pakai Eat.");
        public static readonly Phrase TargetOut = new Phrase("That target is already out.", "Target sudah kalah.");
        public static readonly Phrase PickTargetItem = new Phrase("Pick a target and the item to take.", "Pilih target dan item yang mau diambil.");
        public static readonly Phrase InvalidGrappleTarget = new Phrase("Invalid Grappling Hook target.", "Target Grappling Hook tidak valid.");
        public static readonly Phrase NoTargetItem = new Phrase("The target item doesn't exist.", "Item target tidak ada.");
        public static readonly Phrase GrappleOnGrapple = new Phrase("Grappling Hook can't take a Grappling Hook.", "Grappling Hook tidak bisa mengambil Grappling Hook.");
        public static readonly Phrase NoItem = new Phrase("That item doesn't exist.", "Item tidak ada.");
        public static readonly Phrase NoPocketOpen = new Phrase("No Little Pocket is open.", "Tidak ada Little Pocket yang terbuka.");
        public static readonly Phrase NoSuchOption = new Phrase("That option doesn't exist.", "Pilihan tidak ada.");

        // ------------------------------------------------------------------ bot reasons (narration below the bot action)
        public static readonly Phrase WhyLowHpMilk = new Phrase("low HP, drink the milk", "HP kurang, minum susu");
        public static readonly Phrase WhyGoodClover = new Phrase("good + clover", "good + clover");
        public static readonly Phrase WhySurelyGood = new Phrase("this candy is surely good", "permen sudah pasti good");
        public static readonly Phrase WhyCupcakeSafe = new Phrase("cupcake immunity, safe to eat and keep going", "kebal cupcake, aman makan dan lanjut");
        public static readonly Phrase WhyBadClover = new Phrase("bad + clover for an opponent", "bad + clover untuk lawan");
        public static readonly Phrase WhySurelyBad = new Phrase("this candy is surely bad", "permen sudah pasti bad");
        public static readonly Phrase WhyCheckCandy = new Phrase("check what's inside", "cek isi permen");
        public static readonly Phrase WhyOpenPocket = new Phrase("open the pocket", "buka kantong");
        public static readonly Phrase WhyCupcake = new Phrase("cupcake immunity", "kebal cupcake");
        public static readonly Phrase WhySmash = new Phrase("high bad chance, smash it", "peluang bad tinggi, hancurkan");
        public static readonly Phrase WhyUseCupcake = new Phrase("high bad chance, use the cupcake", "peluang bad tinggi, pakai cupcake");
        public static readonly Phrase WhyGoodChance = new Phrase("good chance {0:P0}", "peluang good {0:P0}");
        public static readonly Phrase WhyLowGoodChance = new Phrase("good chance only {0:P0}", "peluang good cuma {0:P0}");
        public static readonly Phrase WhyRandomItem = new Phrase("using an item just for fun", "iseng pakai item");
        public static readonly Phrase WhyGood = new Phrase("good", "good");
        public static readonly Phrase WhyBad = new Phrase("bad", "bad");
        public static readonly Phrase WhyReckless = new Phrase("reckless", "nekat");
        public static readonly Phrase WhyPassOn = new Phrase("pass it to someone", "lempar ke orang");
        public static readonly Phrase WhyRandomPick = new Phrase("random pick", "pilih acak");
        public static readonly Phrase WhyUselessPocket = new Phrase("the pocket has nothing useful", "isi kantong tidak berguna");
        public static readonly Phrase WhyPick = new Phrase("pick {0}", "pilih {0}");
        public static readonly Phrase WhySteal = new Phrase("steal {0} from {1}", "curi {0} dari {1}");
    }
}
