using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Core;
using BiteOrBetray.DebugTools;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = System.Random;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Menjalankan GameEngine di scene CandyParlour3D: bot (BotBrain) dan satu kursi manusia (Seat_0), lalu memvisualkan
    /// setiap aksi: permen keluar dari dispenser, dimakan atau disuapkan, reaksi karakter, HP, item, dan pengumuman pelayan.
    /// Engine bersifat sinkron, jadi director mengambil snapshot sebelum aksi lalu memainkan perbedaannya satu per satu.
    /// Semua referensi dicari dari nama objek buatan ArtSceneBuilder, jadi aman kalau scene dibangun ulang.
    /// </summary>
    public sealed class CandyTableDirector : MonoBehaviour
    {
        [Header("Game")]
        public bool autoStart = true;
        public bool loop = true;
        [Tooltip("Kursi Seat_0 dimainkan manusia. Matikan untuk menonton bot saja.")]
        public bool humanPlayer = true;
        public string humanName = "Kamu";
        [Tooltip("0 = acak setiap game.")]
        public int seed;
        [Tooltip("Tingkat bot per kursi: Bot_Yellow, Bot_Blue, Bot_Red, Bot_Mint, Bot_Peach.")]
        public BotDifficulty[] difficulties = { BotDifficulty.Normal, BotDifficulty.Hard, BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard };
        [Tooltip("Nama tampilan bot per kursi. Sengaja beda dari nama warna permen (kuning/biru/merah) supaya narasi tidak rancu.")]
        public string[] botNames = { "Yellow", "Blue", "Red", "Mint", "Peach" };
        public RuleConfig rules = new RuleConfig();

        [Header("Telemetry")]
        [Tooltip("Catat aksi dan detik berpikir ke CSV di persistentDataPath/telemetry, seperti meja debug.")]
        public bool recordTelemetry = true;

        [Header("Tempo")]
        [Range(.25f, 4)] public float tempo = 1;
        public Vector2 thinkSeconds = new Vector2(1.2f, 2.4f);

        [Header("Tampilan")]
        public bool showLog;
        [Tooltip("Kosong = cari BoB_Art3D di scene.")]
        public Transform artRoot;

        static readonly string[] Colors = { "Yellow", "Blue", "Red", "Mint", "Peach" };
        static readonly Color Ink = new Color(.23f, .15f, .12f);
        static readonly Color GoodColor = new Color(.13f, .55f, .3f);
        static readonly Color BadColor = new Color(.78f, .18f, .16f);
        static readonly Color InfoColor = new Color(.15f, .4f, .65f);
        static readonly Color GoldColor = new Color(.75f, .55f, .05f);
        static readonly Color StatusColor = new Color(.5f, .25f, .65f);

        const int LowHp = 2;

        sealed class Seat
        {
            public string name;
            public int index = -1;
            public CharacterAnimator character;
            public AvatarReactionController avatarReaction;
            public Transform anchor, mat;
            public TextMesh label;
            public PlayerState player;
            public (string name, bool hasPlayer, bool eliminated, int hp, int maxHp, int items, bool paralyzed, bool cupcake, bool turn) labelState;
            public string labelText;
            bool paralyzed, defeated;

            public bool IsHuman => character == null;

            public Transform Head
            {
                get
                {
                    if (character != null) return character.Head;
                    var camera = Camera.main;
                    return camera != null ? camera.transform : anchor;
                }
            }

            public bool Thinking { set { if (character != null) character.thinking = value; } }
            public Transform Attention { set { if (character != null) character.attention = value; } }

            public bool Paralyzed
            {
                get => paralyzed;
                set { paralyzed = value; if (character != null) character.paralyzed = value; avatarReaction?.SetStatus(paralyzed, defeated); }
            }

            public bool Defeated
            {
                get => defeated;
                set { defeated = value; if (character != null) character.defeated = value; avatarReaction?.SetStatus(paralyzed, defeated); }
            }

            // Visual saja: HP rendah -> lemas/pusing/mual. Aturan HP tetap di engine.
            public void ShowHealth(int hp)
            {
                float sickness = player == null || player.Eliminated ? 0 : Mathf.Clamp01((LowHp + 1 - hp) / (float)LowHp);
                if (character != null) character.sickness = sickness;
                avatarReaction?.SetSickness(sickness);
            }

            public void Play(CharacterReaction reaction, Transform target = null)
            {
                if (character != null) character.Play(reaction, target);
                avatarReaction?.Play(reaction, target);
            }

            public void ResetState()
            {
                paralyzed = defeated = false;
                if (character != null) character.ResetState();
                avatarReaction?.ResetState();
            }
        }

        sealed class Snapshot
        {
            public Candy candy;
            public bool clover;
            public int round, paralyzeApplied, skipped;
            public int[] hp;
            public bool[] cupcake, eliminated;
            public List<ItemType>[] items;
            public List<ItemType> pocket;
        }

        enum HumanMode { None, Action, Grapple, Pocket }

        sealed class Prop
        {
            public Seat seat;
            public int index;
            public ItemType type;
            public bool pocket;
            public TableSelectable selectable;
        }

        readonly List<Seat> seats = new List<Seat>();
        readonly List<Transform> billboards = new List<Transform>();
        readonly List<GameObject> temporary = new List<GameObject>();
        readonly List<GameObject> hiddenDecor = new List<GameObject>();
        readonly List<Prop> props = new List<Prop>();
        readonly List<string> log = new List<string>();
        readonly List<(TextMesh text, Transform card)> cards = new List<(TextMesh, Transform)>();
        readonly Dictionary<CandyColor, GameObject> candyTemplates = new Dictionary<CandyColor, GameObject>();
        readonly Dictionary<ItemType, GameObject> itemTemplates = new Dictionary<ItemType, GameObject>();
        readonly Dictionary<Seat, TableSelectable> botTargets = new Dictionary<Seat, TableSelectable>();
        CharacterAnimator waiter;
        Seat human;
        Transform spawnPoint;
        Vector3 trayPosition, tableCenter;
        TextMesh caption, board, speech, candyLabel;
        Font font;
        Material cardMaterial;
        GameEngine engine;
        Candy shownCandy;
        GameObject candyObject;
        TableSelectable candySelectable, hovered, discardButton, cancelButton, restartButton;
        string reason;
        int gameIndex;
        bool ready;
        (int round, int candies, int good, int bad, int seconds) boardState;
        string boardText, logText, panelText;
        (int seconds, int hp, int maxHp, bool cupcake) panelState;
        bool logDirty = true;
        float logTempo;
        GUIStyle logStyle, tooltipStyle, panelStyle;

        HumanMode mode;
        BotDecision humanChoice;
        bool humanTimeout, grappleFromPocket;
        int grappleItem = -1;
        float turnTimer;
        float decisionThink, turnThinkUsed;
        TelemetryRecorder telemetry;
        VRTableInput vrInput;

        void Awake()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // Nilai serialized dari scene lama juga harus mati di build release.
            showLog = false;
#endif
        }

        void Start()
        {
            vrInput = GetComponent<VRTableInput>();
            if (vrInput == null) vrInput = gameObject.AddComponent<VRTableInput>();
            ready = Setup();
            if (ready && autoStart) Restart();
        }

        void OnDestroy()
        {
            foreach (var t in candyTemplates.Values) if (t != null) t.SetActive(true);
            foreach (var t in hiddenDecor) if (t != null) t.SetActive(true);
        }

        [ContextMenu("Mulai ulang game")]
        public void Restart()
        {
            if (!ready) return;
            StopAllCoroutines();
            // SetMode juga mematikan tombol Buang isi/Batal dan opsi Little Pocket kalau restart terjadi di tengah giliran.
            SetMode(HumanMode.None);
            SetButton(restartButton, false);
            StartCoroutine(RunGames());
        }

        // ------------------------------------------------------------------ setup

        bool Setup()
        {
            if (artRoot == null) artRoot = GameObject.Find("BoB_Art3D")?.transform;
            if (artRoot == null) return Fail("BoB_Art3D tidak ditemukan. Bangun scene lewat menu Bite or Betray dulu.");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cardMaterial = MakeCardMaterial();

            var seatsRoot = artRoot.Find("Seats");
            var mats = artRoot.Cast<Transform>().Where(t => t.name == "ItemMat").ToList();
            if (humanPlayer && seatsRoot != null)
            {
                var seat0 = seatsRoot.Find("Seat_0");
                if (seat0 != null)
                    human = new Seat { name = humanName, anchor = seat0.Find("HeadAnchor") ?? seat0, mat = Nearest(mats, seat0.position),
                        avatarReaction = seat0.GetComponentInChildren<AvatarReactionController>(true) };
            }
            for (int i = 0; i < Colors.Length && seatsRoot != null; i++)
            {
                var bot = FindDeep(seatsRoot, "Bot_" + Colors[i]);
                if (bot == null) continue;
                string botName = botNames != null && i < botNames.Length && !string.IsNullOrWhiteSpace(botNames[i]) ? botNames[i] : Colors[i];
                seats.Add(new Seat { name = botName, index = i, character = GetAnimator(bot), mat = Nearest(mats, bot.position) });
            }
            if (human != null) seats.Insert(0, human);
            if (seats.Count < 2) return Fail("Butuh minimal 2 peserta di BoB_Art3D/Seats.");
            // Nama dipakai di narasi dan log engine: nama kembar dilaporkan lalu diberi nomor supaya tidak rancu.
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in seats)
            {
                string original = s.name;
                for (int n = 2; !usedNames.Add(s.name); n++) s.name = $"{original} {n}";
                if (s.name != original) Debug.LogWarning($"[Bite or Betray] Nama peserta kembar \"{original}\", diganti jadi \"{s.name}\".", this);
            }

            var waiterT = artRoot.Find("Waiter");
            if (waiterT != null) waiter = GetAnimator(waiterT);
            var dispenser = artRoot.Find("Dispenser");
            tableCenter = dispenser != null ? dispenser.position : artRoot.position + Vector3.up * .76f;
            spawnPoint = FindDeep(artRoot, "CandySpawnPoint") ?? dispenser;

            foreach (CandyColor color in Enum.GetValues(typeof(CandyColor)))
            {
                var t = artRoot.Find("Candy_" + color);
                if (t != null) candyTemplates[color] = t.gameObject;
            }
            var tray = artRoot.Find("CandyTray");
            trayPosition = candyTemplates.TryGetValue(CandyColor.Yellow, out var yellow) ? yellow.transform.position
                : tray != null ? tray.position + Vector3.up * .053f : tableCenter + new Vector3(0, .05f, -.3f);
            foreach (var t in candyTemplates.Values) t.SetActive(false);

            // Item dekorasi di tengah meja dipakai sebagai cetakan; item asli tampil di alas tiap kursi.
            var items = artRoot.Find("Item Models");
            foreach (ItemType type in ItemInfo.All)
            {
                var t = items != null ? items.Find(type.ToString()) : null;
                if (t == null) continue;
                itemTemplates[type] = t.gameObject;
                hiddenDecor.Add(t.gameObject);
                t.gameObject.SetActive(false);
            }

            if (human != null && human.mat != null)
            {
                // Tombol di meja (bisa ditunjuk controller VR maupun diklik mouse), di sisi kanan alas pemain.
                Vector3 toCenter = Flat(tableCenter - human.mat.position);
                Vector3 spot = human.mat.position + Vector3.Cross(Vector3.up, toCenter) * .3f - toCenter * .05f + Vector3.up * .07f;
                discardButton = MakeButton("Buang isi", spot, DiscardPocket);
                cancelButton = MakeButton("Batal", spot, Cancel);
                // Hanya muncul di layar akhir, saat dua tombol di atas sudah mati, jadi boleh berbagi tempat.
                restartButton = MakeButton("Main lagi", spot, Restart);
            }
            else restartButton = MakeButton("Main lagi", tableCenter + Vector3.up * .6f, Restart);
            board = MakeText("Info Meja", tableCenter + Vector3.up * .74f, .0075f, Ink, true);
            caption = MakeText("Narasi", tableCenter + Vector3.up * .9f, .009f, Ink, true);
            candyLabel = MakeText("Label Permen", trayPosition + Vector3.up * .1f, .007f, Ink);
            if (waiter != null) speech = MakeText("Pengumuman Pelayan", waiter.transform.position + Vector3.up * 1.85f, .0065f, Ink, true);
            return true;
        }

        bool Fail(string message)
        {
            Debug.LogWarning("[Bite or Betray] " + message, this);
            return false;
        }

        static CharacterAnimator GetAnimator(Transform t)
        {
            var c = t.GetComponent<CharacterAnimator>();
            return c != null ? c : t.gameObject.AddComponent<CharacterAnimator>();
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static Transform Nearest(List<Transform> list, Vector3 position) =>
            list.OrderBy(t => Vector3.Distance(Flat3(t.position), Flat3(position))).FirstOrDefault();

        static Vector3 Flat3(Vector3 v) => new Vector3(v.x, 0, v.z);

        void CreateBotTargets()
        {
            foreach (var s in seats)
            {
                if (s.IsHuman || botTargets.ContainsKey(s)) continue;
                var renderers = s.character.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                var go = new GameObject("Klik " + s.name);
                go.transform.SetParent(transform, false);
                go.transform.position = bounds.center;
                go.AddComponent<BoxCollider>().size = bounds.size;
                var selectable = go.AddComponent<TableSelectable>();
                selectable.kind = TableSelectable.Kind.Bot;
                var seat = s;
                selectable.onSelect = () => OnBotSelected(seat);
                botTargets[s] = selectable;
            }
        }

        // ------------------------------------------------------------------ alur game

        IEnumerator RunGames()
        {
            yield return null; // tunggu CharacterAnimator selesai Start (Head sudah ada)
            CreateBotTargets();
            foreach (var s in seats)
                if (s.label == null) s.label = MakeText("Label " + s.name, LabelPosition(s), .0065f, Ink, true);
            do
            {
                yield return PlayGame();
                if (!loop) break;
                yield return Wait(6);
            } while (true);
        }

        IEnumerator PlayGame()
        {
            ResetVisuals();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Bukti restart tidak menggandakan objek: angka ini harus sama di setiap game. Destroy baru selesai di akhir frame.
            yield return null;
            Debug.Log($"[Bite or Betray] Game #{gameIndex + 1}: {transform.childCount} objek anak, "
                + $"{GetComponentsInChildren<TableSelectable>(true).Length} TableSelectable, {props.Count} item di alas");
#endif
            gameIndex++;
            int gameSeed = seed != 0 ? seed + gameIndex - 1 : Environment.TickCount;
            var rng = new Random(gameSeed + 7);
            engine = new GameEngine(rules.Clone(), gameSeed);
            engine.Logged += OnLogged;
            engine.Start(seats.Select(s => new SeatConfig(s.name, !s.IsHuman,
                difficulties != null && s.index >= 0 && s.index < difficulties.Length ? difficulties[s.index] : BotDifficulty.Normal)).ToList());
            foreach (var p in engine.Players) SeatOf(p).player = p;
            telemetry = null;
            if (recordTelemetry)
            {
                try
                {
                    telemetry = new TelemetryRecorder(System.IO.Path.Combine(Application.persistentDataPath, "telemetry"), gameSeed, seats.Count);
                    Debug.Log("[BoB] Telemetry: " + telemetry.FilePath, this);
                }
                catch (Exception e) { Debug.LogWarning("[BoB] Telemetry tidak bisa dibuat: " + e.Message, this); }
            }
            if (human != null) PlaceDesktopCamera();
            RefreshProps();

            Say("Game baru! Semua pemain melempar d20 untuk urutan giliran.", $"seed {gameSeed}");
            foreach (var s in seats)
            {
                Popup(Above(s, .45f), $"d20: <b>{s.player.D20Roll}</b>", Ink, 2.6f);
                s.Play(CharacterReaction.Nod);
            }
            yield return Wait(2.8f);
            Say("Urutan giliran: " + string.Join(" > ", engine.Players.Select(p => p.Name)));
            yield return Wait(1.8f);
            yield return AnnounceRound();
            yield return SyncCandy();

            int lastTurn = -1;
            BotActionKind? lastKind = BotActionKind.Eat; // null = aksi terakhir ditolak, tidak ada yang dimakan
            while (!engine.IsOver)
            {
                var actor = SeatOf(engine.Current);
                bool newTurn = engine.TurnNumber != lastTurn;
                if (newTurn)
                {
                    Say(actor.IsHuman ? "Giliran kamu!" : $"Giliran {actor.name}",
                        actor.IsHuman ? $"HP {actor.player.Hp}" : $"HP {actor.player.Hp}  ·  {actor.player.Difficulty}");
                    lastTurn = engine.TurnNumber;
                    turnTimer = rules.TurnTimeLimitSeconds;
                    turnThinkUsed = 0;
                }
                else if (lastKind == BotActionKind.Eat) Say(actor.IsHuman ? "Aman! Lanjut, permen berikutnya." : $"{actor.name} lanjut, permen berikutnya!");
                Focus(actor, candyObject != null ? candyObject.transform : null);

                BotDecision decision;
                Snapshot before;
                ActionResult result;
                if (actor.IsHuman)
                {
                    yield return HumanTurn(actor);
                    before = Capture();
                    if (humanTimeout)
                    {
                        decision = new BotDecision { Kind = BotActionKind.Eat };
                        result = engine.TimeoutTurn();
                        // Dengan ForcedEat + permen good giliran berlanjut: timer harus mulai lagi supaya tidak timeout beruntun.
                        turnTimer = rules.TurnTimeLimitSeconds;
                        Popup(Above(actor, .3f), "<b>Waktu habis!</b>", BadColor);
                    }
                    else
                    {
                        decision = humanChoice;
                        result = BotBrain.Execute(engine, decision);
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Bite or Betray] {actor.name}: {(humanTimeout ? "waktu habis" : DescribeChoice(decision))} -> "
                        + (result.Success ? "OK" : "ditolak: " + result.Error));
#endif
                    reason = humanTimeout ? "waktu habis, permen dimakan paksa" : null;
                }
                else
                {
                    actor.Thinking = true;
                    float decisionStart = Time.realtimeSinceStartup;
                    yield return Wait(engine.Phase == GamePhase.AwaitingPocketChoice ? .9f
                        : Mathf.Lerp(thinkSeconds.x, thinkSeconds.y, (float)rng.NextDouble()));
                    decisionThink = Time.realtimeSinceStartup - decisionStart;
                    before = Capture();
                    decision = Decide(rng, out result);
                    actor.Thinking = false;
                    reason = decision.Reason != null ? $"\"{decision.Reason}\"" : null;
                }
                if (!result.Success)
                {
                    Popup(Above(actor, .3f), result.Error, BadColor, 2.5f);
                    lastKind = null;
                    continue;
                }
                RecordDecision(actor, decision, before);
                if (actor.IsHuman && humanTimeout) turnThinkUsed = 0;
                lastKind = decision.Kind;
                yield return Visualize(actor, decision, before);
                RefreshProps();
                yield return Wait(.5f);
            }
            yield return GameOver();
        }

        string DescribeChoice(BotDecision d)
        {
            switch (d.Kind)
            {
                case BotActionKind.Eat: return "makan permen";
                case BotActionKind.Give: return "suapi " + engine.Players[d.TargetSeat].Name;
                case BotActionKind.UseItem: return "pakai item #" + d.Index;
                case BotActionKind.ChoosePocket: return "pilih Little Pocket #" + d.Index;
                default: return "buang Little Pocket";
            }
        }

        void RecordDecision(Seat actor, BotDecision d, Snapshot before)
        {
            if (telemetry == null) return;
            string action = d.Kind.ToString().ToUpperInvariant(), detail = d.Reason;
            if (actor.IsHuman)
            {
                // Nama aksi dan detail mengikuti meja debug; item diambil dari snapshot sebelum engine mengubahnya.
                detail = "";
                if (humanTimeout) { action = "TIMEOUT"; detail = "dipaksa makan"; }
                else if (d.Kind == BotActionKind.Give) detail = engine.Players[d.TargetSeat].Name;
                else if (d.Kind == BotActionKind.UseItem || d.Kind == BotActionKind.ChoosePocket)
                {
                    action = d.Kind == BotActionKind.UseItem ? "ITEM" : "POCKET_CHOICE";
                    var item = d.Kind == BotActionKind.UseItem ? before.items[actor.player.Seat][d.Index] : before.pocket[d.Index];
                    detail = item == ItemType.GrapplingHook && d.Args != null
                        ? "Grappling Hook -> " + ItemInfo.Name(before.items[d.Args.TargetSeat][d.Args.TargetItemIndex])
                        : ItemInfo.Name(item);
                }
                else if (d.Kind == BotActionKind.DiscardPocket) action = "POCKET_DISCARD";
            }
            try
            {
                telemetry.Record(engine, actor.player, action, detail, decisionThink, actor.IsHuman ? turnThinkUsed : decisionThink);
                // Tulis hasil sebelum animasi akhir agar restart tidak menghilangkan GAME_OVER.
                if (engine.IsOver) telemetry.Finish(engine);
            }
            catch (Exception e)
            {
                telemetry = null;
                Debug.LogWarning("[BoB] Telemetry dihentikan: " + e.Message, this);
            }
        }

        BotDecision Decide(Random rng, out ActionResult result)
        {
            BotDecision d = null;
            result = ActionResult.Fail("");
            try
            {
                d = BotBrain.Decide(engine, rng);
                result = BotBrain.Execute(engine, d);
            }
            catch (Exception e) { Debug.LogException(e, this); }
            if (result.Success) return d;
            d = engine.Phase == GamePhase.AwaitingPocketChoice
                ? new BotDecision { Kind = BotActionKind.DiscardPocket, Reason = "fallback" }
                : new BotDecision { Kind = BotActionKind.Eat, Reason = "fallback" };
            result = BotBrain.Execute(engine, d);
            return d;
        }

        Snapshot Capture()
        {
            var players = engine.Players;
            return new Snapshot {
                candy = engine.ActiveCandy,
                clover = engine.CloverOnActive,
                round = engine.RoundNumber,
                paralyzeApplied = engine.Stats.ParalyzeApplied,
                skipped = engine.Stats.TurnsSkipped,
                hp = players.Select(p => p.Hp).ToArray(),
                cupcake = players.Select(p => p.HasCupcake).ToArray(),
                eliminated = players.Select(p => p.Eliminated).ToArray(),
                items = players.Select(p => new List<ItemType>(p.Items)).ToArray(),
                pocket = new List<ItemType>(engine.PocketOptions)
            };
        }

        IEnumerator Visualize(Seat actor, BotDecision d, Snapshot b)
        {
            RefreshProps(); // item yang dipakai langsung hilang dari alas
            Seat eater = null;
            switch (d.Kind)
            {
                case BotActionKind.Eat:
                    eater = actor;
                    yield return Consume(actor, null, b);
                    break;
                case BotActionKind.Give:
                    eater = SeatOf(engine.Players[d.TargetSeat]);
                    yield return Consume(eater, actor, b);
                    break;
                case BotActionKind.UseItem:
                    var used = b.items[actor.player.Seat][d.Index];
                    Say($"{actor.name} memakai {ItemInfo.Name(used)}.", reason);
                    yield return ItemEffect(actor, used, d, b);
                    break;
                case BotActionKind.ChoosePocket:
                    var chosen = b.pocket[d.Index];
                    Say($"{actor.name} memilih {ItemInfo.Name(chosen)} dari Little Pocket.", reason);
                    yield return ItemEffect(actor, chosen, d, b);
                    break;
                default:
                    Say($"{actor.name} membuang isi Little Pocket.", reason);
                    actor.Play(CharacterReaction.Nod);
                    yield return Wait(1);
                    break;
            }
            yield return Resolve(b, eater);
        }

        // ------------------------------------------------------------------ kursi manusia

        IEnumerator HumanTurn(Seat me)
        {
            humanChoice = null;
            humanTimeout = false;
            decisionThink = 0;
            bool pocket = engine.Phase == GamePhase.AwaitingPocketChoice;
            SetMode(pocket ? HumanMode.Pocket : HumanMode.Action);
            if (pocket) Say("Pilih 1 item dari Little Pocket", "pilih salah satu item yang melayang, atau tombol Buang isi");
            else Say("Giliran kamu!", VRTableInput.XRActive
                ? "ambil permen: dekatkan ke mulut = makan, arahkan ke bot lalu lepas = suapi  ·  tunjuk item + trigger = pakai"
                : "klik permen = makan  ·  klik bot = suapi  ·  klik item di alasmu = pakai");
            while (humanChoice == null && !humanTimeout)
            {
                // Timer dan waktu berpikir dibekukan selama fokus hilang; hanya angka yang diperbarui per frame.
                if (vrInput.HasFocus)
                {
                    decisionThink += Time.unscaledDeltaTime;
                    turnThinkUsed += Time.unscaledDeltaTime;
                    if (rules.TurnTimeLimitSeconds > 0)
                    {
                        turnTimer -= Time.deltaTime;
                        if (turnTimer <= 0) humanTimeout = true;
                    }
                }
                yield return null;
            }
            SetMode(HumanMode.None);
        }

        void SetMode(HumanMode next)
        {
            mode = next;
            if (mode != HumanMode.Grapple) grappleItem = -1;
            if (candySelectable != null)
            {
                candySelectable.interactable = mode == HumanMode.Action;
                candySelectable.tooltip = "Makan permen ini";
            }
            foreach (var pair in botTargets)
            {
                pair.Value.interactable = mode == HumanMode.Action && pair.Key.player != null && pair.Key.player.IsAlive;
                pair.Value.tooltip = $"Suapkan permen ke {pair.Key.name}";
            }
            ShowPocketOptions(mode == HumanMode.Pocket || (mode == HumanMode.Grapple && grappleFromPocket));
            SetButton(discardButton, mode == HumanMode.Pocket);
            SetButton(cancelButton, mode == HumanMode.Grapple);
            foreach (var prop in props)
            {
                bool mine = prop.seat == human;
                prop.selectable.interactable = prop.pocket ? mode == HumanMode.Pocket
                    : mode == HumanMode.Action ? mine
                    : mode == HumanMode.Grapple && !mine && prop.type != ItemType.GrapplingHook && prop.seat.player.IsAlive;
                prop.selectable.tooltip = mode == HumanMode.Grapple && !mine
                    ? $"Curi {ItemInfo.Name(prop.type)} dari {prop.seat.name}"
                    : $"{ItemInfo.Name(prop.type)}: {ItemInfo.Description(prop.type)}";
            }
        }

        void OnCandySelected()
        {
            if (mode == HumanMode.Action) humanChoice = new BotDecision { Kind = BotActionKind.Eat };
        }

        void OnBotSelected(Seat target)
        {
            if (mode == HumanMode.Action && target.player.IsAlive)
                humanChoice = new BotDecision { Kind = BotActionKind.Give, TargetSeat = target.player.Seat };
        }

        void OnPropSelected(Prop prop)
        {
            if (mode == HumanMode.Grapple)
            {
                var args = new ItemArgs(prop.seat.player.Seat, prop.index);
                humanChoice = grappleFromPocket
                    ? new BotDecision { Kind = BotActionKind.ChoosePocket, Index = grappleItem, Args = args }
                    : new BotDecision { Kind = BotActionKind.UseItem, Index = grappleItem, Args = args };
                return;
            }
            if (prop.type == ItemType.GrapplingHook)
            {
                if (!engine.HasGrappleTarget(human.player))
                {
                    Popup(Above(human, .3f), "Tidak ada item lawan yang bisa dicuri", BadColor, 2.2f);
                    return;
                }
                grappleFromPocket = prop.pocket;
                SetMode(HumanMode.Grapple);
                grappleItem = prop.index;
                Say("Grappling Hook: pilih item lawan", VRTableInput.XRActive
                    ? "tunjuk item di alas bot lain  ·  B/Y atau tombol Batal"
                    : "klik item di alas bot lain  ·  Esc untuk batal");
                return;
            }
            humanChoice = prop.pocket
                ? new BotDecision { Kind = BotActionKind.ChoosePocket, Index = prop.index }
                : new BotDecision { Kind = BotActionKind.UseItem, Index = prop.index };
        }

        /// <summary>Batalkan pilihan target Grappling Hook (Esc, tombol B/Y controller, atau tombol Batal di meja).</summary>
        public void Cancel()
        {
            if (mode != HumanMode.Grapple) return;
            SetMode(grappleFromPocket ? HumanMode.Pocket : HumanMode.Action);
            Say(grappleFromPocket ? "Pilih 1 item dari Little Pocket" : "Giliran kamu!");
        }

        void DiscardPocket()
        {
            if (mode == HumanMode.Pocket || (mode == HumanMode.Grapple && grappleFromPocket))
                humanChoice = new BotDecision { Kind = BotActionKind.DiscardPocket };
        }

        void PlaceDesktopCamera()
        {
            if (VRTableInput.XRActive || Camera.main == null || human.anchor == null) return;
            var camera = Camera.main.transform;
            camera.SetPositionAndRotation(human.anchor.position,
                Quaternion.LookRotation(tableCenter + Vector3.up * .25f - human.anchor.position));
        }

        // ------------------------------------------------------------------ item di alas

        void RefreshProps()
        {
            foreach (var prop in props) if (prop.selectable != null) Destroy(prop.selectable.gameObject);
            props.Clear();
            if (engine == null) return;
            foreach (var s in seats)
            {
                if (s.mat == null || s.player == null || !s.player.IsAlive) continue;
                var items = s.player.Items;
                for (int i = 0; i < items.Count; i++)
                    AddProp(s, i, items[i], false, MatSlot(s, i, items.Count));
            }
            SetMode(mode);
        }

        static void SetButton(TableSelectable button, bool on)
        {
            if (button == null) return;
            button.gameObject.SetActive(on);
            button.interactable = on;
        }

        TableSelectable MakeButton(string label, Vector3 position, Action onSelect)
        {
            var text = MakeText("Tombol " + label, position, .007f, BadColor, true);
            text.text = $"<b>{label}</b>";
            text.gameObject.AddComponent<BoxCollider>(); // ukurannya mengikuti teks di LateUpdate
            var button = text.gameObject.AddComponent<TableSelectable>();
            button.kind = TableSelectable.Kind.Button;
            button.tooltip = label;
            button.onSelect = onSelect;
            text.gameObject.SetActive(false);
            return button;
        }

        void ShowPocketOptions(bool show)
        {
            bool shown = props.Any(p => p.pocket);
            if (show == shown) return;
            if (!show)
            {
                foreach (var prop in props.Where(p => p.pocket)) Destroy(prop.selectable.gameObject);
                props.RemoveAll(p => p.pocket);
                return;
            }
            var options = engine.PocketOptions;
            for (int i = 0; i < options.Count; i++)
                AddProp(human, i, options[i], true, MatSlot(human, i, options.Count) + Vector3.up * .16f);
        }

        Vector3 MatSlot(Seat s, int index, int count)
        {
            Vector3 toCenter = Flat(tableCenter - s.mat.position);
            Vector3 right = Vector3.Cross(Vector3.up, toCenter);
            return s.mat.position + right * ((index - (count - 1) * .5f) * .075f) + Vector3.up * .035f;
        }

        void AddProp(Seat seat, int index, ItemType type, bool pocket, Vector3 position)
        {
            if (!itemTemplates.TryGetValue(type, out var template)) return;
            Vector3 toCenter = Flat(tableCenter - seat.mat.position);
            float yaw = Vector3.SignedAngle(Flat(template.transform.forward), -toCenter, Vector3.up);
            var go = Instantiate(template, position, Quaternion.AngleAxis(yaw, Vector3.up) * template.transform.rotation, transform);
            go.SetActive(true);
            go.name = (pocket ? "Pocket " : "Item ") + type;
            go.transform.localScale = template.transform.localScale * (pocket ? .7f : .55f);
            if (go.GetComponentInChildren<Collider>() == null) AddBoundsCollider(go);
            var prop = new Prop { seat = seat, index = index, type = type, pocket = pocket, selectable = go.AddComponent<TableSelectable>() };
            prop.selectable.kind = TableSelectable.Kind.Item;
            prop.selectable.onSelect = () => OnPropSelected(prop);
            props.Add(prop);
        }

        static void AddBoundsCollider(GameObject go)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var mesh in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.sharedMesh == null) continue;
                Bounds source = mesh.sharedMesh.bounds;
                Matrix4x4 matrix = go.transform.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            var box = go.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size * 1.3f;
        }

        // ------------------------------------------------------------------ makan dan suap

        IEnumerator Consume(Seat eater, Seat giver, Snapshot b)
        {
            var candy = b.candy;
            string color = CandyRules.ColorName(candy.Color).ToLowerInvariant();
            if (giver == null)
            {
                Say($"{eater.name} memakan permen {color}.", reason);
                eater.Play(CharacterReaction.Eat);
                yield return Wait(.3f);
            }
            else
            {
                Say($"{giver.name} menyuapkan permen {color} ke {eater.name}!", reason);
                Focus(eater, giver.Head);
                giver.Play(CharacterReaction.Give, eater.Head);
                yield return Wait(.45f);
            }
            if (candyObject != null)
            {
                yield return Move(candyObject.transform, Mouth(eater), .45f, .15f);
                yield return Move(candyObject.transform, Mouth(eater), .15f, 0, Vector3.zero);
                Destroy(candyObject);
            }
            candyObject = null;
            shownCandy = null;
            if (giver != null) eater.Play(CharacterReaction.Nod);
            yield return Wait(.6f);

            bool blocked = b.cupcake[eater.player.Seat] && !eater.player.HasCupcake;
            Popup(Above(eater, .45f), $"{candy.FlavorName}\n<b>{(candy.IsGood ? "GOOD" : "BAD")}</b>", candy.IsGood ? GoodColor : BadColor);
            if (b.clover) Popup(Above(eater, .65f), "<b>Clover x2!</b>", GoldColor);
            if (blocked)
            {
                eater.Play(CharacterReaction.Nod);
                Popup(Above(eater, .6f), "Kebal (cupcake)", InfoColor);
            }
            else if (candy.IsGood) eater.Play(CharacterReaction.Yummy);
            else
            {
                eater.Play(CharacterReaction.Yuck);
                yield return Wait(.7f);
                eater.Play(CharacterReaction.Hurt);
                if (giver != null) giver.Play(CharacterReaction.Laugh);
                else foreach (var s in seats)
                    if (s != eater && s.player.IsAlive && UnityEngine.Random.value < .4f) s.Play(CharacterReaction.Laugh);
            }
            yield return Wait(1.1f);
        }

        // ------------------------------------------------------------------ efek item

        IEnumerator ItemEffect(Seat actor, ItemType type, BotDecision d, Snapshot b)
        {
            var item = ShowItem(type, actor);
            actor.Play(CharacterReaction.Nod);
            yield return Wait(.7f);
            Vector3 overCandy = trayPosition + Vector3.up * .08f;
            switch (type)
            {
                case ItemType.PlasticFork:
                {
                    yield return MoveItem(item, overCandy, .4f, true);
                    var c = engine.ActiveCandy ?? b.candy;
                    Popup(trayPosition + Vector3.up * .18f, $"<b>{(c.IsGood ? "GOOD" : "BAD")}</b>", c.IsGood ? GoodColor : BadColor);
                    if (!c.IsGood) actor.Play(CharacterReaction.Shocked);
                    yield return Wait(.9f);
                    break;
                }
                case ItemType.HammerCrush:
                {
                    yield return MoveItem(item, overCandy, .4f, true);
                    var crushed = b.candy;
                    if (candyObject != null)
                    {
                        yield return Move(candyObject.transform, candyObject.transform.position, .2f, 0, Vector3.zero);
                        Destroy(candyObject);
                    }
                    candyObject = null;
                    shownCandy = null;
                    Popup(trayPosition + Vector3.up * .18f, $"{crushed.FlavorName}\n<b>{(crushed.IsGood ? "GOOD" : "BAD")}</b>",
                        crushed.IsGood ? GoodColor : BadColor);
                    yield return Wait(.9f);
                    break;
                }
                case ItemType.HotMilk:
                    yield return MoveItem(item, Mouth(actor), .4f, false);
                    actor.Play(CharacterReaction.Yummy);
                    yield return Wait(.8f);
                    break;
                case ItemType.MagicGloves:
                    yield return MoveItem(item, overCandy, .4f, false);
                    if (candyObject != null && engine.ActiveCandy != b.candy)
                    {
                        yield return Move(candyObject.transform, spawnPoint.position, .45f, .1f, Vector3.zero);
                        Destroy(candyObject);
                        candyObject = null;
                        shownCandy = null;
                    }
                    else Popup(Above(actor, .5f), "Dispenser kosong", Ink);
                    yield return Wait(.3f);
                    break;
                case ItemType.Cupcake:
                    yield return MoveItem(item, Mouth(actor), .4f, false);
                    Popup(Above(actor, .5f), "Kebal 1x", InfoColor);
                    yield return Wait(.6f);
                    break;
                case ItemType.CloverCoin:
                    yield return MoveItem(item, overCandy, .4f, false);
                    Popup(trayPosition + Vector3.up * .18f, "<b>Clover x2!</b>", GoldColor);
                    yield return Wait(.6f);
                    break;
                case ItemType.LittlePocket:
                    if (!actor.IsHuman)
                        Popup(Above(actor, .55f), "Isi: " + string.Join(", ", engine.PocketOptions.Select(ItemInfo.Name)), Ink, 3);
                    yield return Wait(1.2f);
                    break;
                case ItemType.GrapplingHook:
                {
                    var target = SeatOf(engine.Players[d.Args.TargetSeat]);
                    var stolen = b.items[d.Args.TargetSeat][d.Args.TargetItemIndex];
                    Say($"{actor.name} menembakkan Grappling Hook ke {target.name} dan mengambil {ItemInfo.Name(stolen)}!", reason);
                    actor.Play(CharacterReaction.Give, target.Head);
                    yield return MoveItem(item, Above(target, 0), .5f, false);
                    target.Play(CharacterReaction.Shocked);
                    yield return MoveItem(item, Mouth(actor), .4f, false);
                    yield return ItemEffect(actor, stolen, null, b);
                    break;
                }
            }
            if (item != null) Destroy(item.gameObject);
        }

        Transform ShowItem(ItemType type, Seat actor)
        {
            if (!itemTemplates.TryGetValue(type, out var template)) return null;
            Vector3 position = actor.IsHuman && actor.mat != null
                ? actor.mat.position + Vector3.up * .2f
                : Mouth(actor) + Flat(tableCenter - Mouth(actor)) * .14f + Vector3.up * .12f;
            var item = Instantiate(template, position, template.transform.rotation, transform);
            item.SetActive(true);
            foreach (var c in item.GetComponentsInChildren<Collider>()) Destroy(c);
            temporary.Add(item);
            Vector3 scale = template.transform.localScale;
            item.transform.localScale = Vector3.zero;
            StartCoroutine(Move(item.transform, position, .25f, 0, scale * 1.2f));
            return item.transform;
        }

        IEnumerator MoveItem(Transform item, Vector3 to, float duration, bool punchCandy)
        {
            if (item == null) yield break;
            yield return Move(item, to, duration, .08f);
            if (punchCandy && candyObject != null) yield return Punch(candyObject.transform);
        }

        // ------------------------------------------------------------------ efek setelah aksi

        IEnumerator Resolve(Snapshot b, Seat eater)
        {
            foreach (var s in seats)
            {
                int delta = s.player.Hp - b.hp[s.player.Seat];
                s.ShowHealth(s.player.Hp);
                if (delta != 0) Popup(Above(s, .5f), delta > 0 ? $"<b>+{delta} HP</b>" : $"<b>{delta} HP</b>", delta > 0 ? GoodColor : BadColor);
            }
            if (engine.Stats.ParalyzeApplied > b.paralyzeApplied && eater != null && eater.player.IsAlive)
            {
                eater.Paralyzed = true;
                Popup(Above(eater, .7f), "<b>Lumpuh!</b>", StatusColor);
                yield return Wait(.9f);
            }
            foreach (var s in seats)
            {
                if (!s.player.Eliminated || b.eliminated[s.player.Seat]) continue;
                s.Paralyzed = false;
                s.Defeated = true;
                Say(s.IsHuman ? "Kamu keracunan! HP habis, kamu kalah." : $"{s.name} keracunan! HP habis, {s.name} kalah.",
                    s.IsHuman ? "sisa game ditonton sebagai spectator" : null);
                Popup(Above(s, .7f), "<b>KO!</b>", BadColor);
                foreach (var other in seats)
                    if (other != s && other.player.IsAlive) other.Play(CharacterReaction.Shocked);
                yield return Wait(1.8f);
            }
            if (engine.Stats.TurnsSkipped > b.skipped)
                foreach (var s in seats)
                {
                    if (!s.Paralyzed || s.player.Paralyzed) continue;
                    Say(s.IsHuman ? "Kamu masih lumpuh, giliranmu dilewati." : $"{s.name} masih lumpuh, gilirannya dilewati.");
                    yield return Wait(1.4f);
                    s.Paralyzed = false;
                }
            if (engine.RoundNumber != b.round) yield return AnnounceRound();
            yield return SyncCandy();
        }

        IEnumerator AnnounceRound()
        {
            var a = engine.Announcement;
            if (waiter != null) waiter.Play(CharacterReaction.Wave);
            if (speech != null)
                speech.text = $"<b>Ronde {engine.RoundNumber}!</b>\n{a.Total} permen: {a.Yellow} kuning, {a.Blue} biru, {a.Red} merah\n{a.Good} good, {a.Bad} bad";
            Say($"Pelayan membawa nampan ronde {engine.RoundNumber}.", $"{a.Good} good, {a.Bad} bad dari {a.Total} permen");
            if (rules.ItemsPerRound > 0 && (engine.RoundNumber > 1 || rules.DealItemsOnFirstRound))
                foreach (var s in seats)
                    if (s.player.IsAlive) Popup(Above(s, .5f), $"+{rules.ItemsPerRound} item", InfoColor);
            RefreshProps();
            yield return Wait(3);
            StartCoroutine(ClearSpeech(speech != null ? speech.text : null));
        }

        // Pengumuman pelayan hilang sendiri; sisa good/bad tetap ada di papan info meja.
        IEnumerator ClearSpeech(string text)
        {
            yield return Wait(4);
            if (speech != null && speech.text == text) speech.text = "";
        }

        IEnumerator SyncCandy()
        {
            var c = engine.ActiveCandy;
            if (c == shownCandy) yield break;
            if (candyObject != null)
            {
                yield return Move(candyObject.transform, candyObject.transform.position, .25f, 0, Vector3.zero);
                Destroy(candyObject);
            }
            candyObject = null;
            candySelectable = null;
            shownCandy = c;
            if (c == null) yield break;
            candyObject = SpawnCandy(c.Color, spawnPoint.position);
            Vector3 scale = candyObject.transform.localScale;
            candyObject.transform.localScale = scale * .2f;
            yield return Move(candyObject.transform, trayPosition, .6f, .12f, scale);
            AddBoundsCollider(candyObject);
            candySelectable = candyObject.AddComponent<TableSelectable>();
            candySelectable.kind = TableSelectable.Kind.Candy;
            candySelectable.onSelect = OnCandySelected;
            SetMode(mode);
            yield return Wait(.3f);
        }

        IEnumerator GameOver()
        {
            var winner = engine.Winner != null ? SeatOf(engine.Winner) : null;
            Focus(winner, null);
            string title = winner == null ? "Tidak ada pemenang." : winner.IsHuman ? "KAMU MENANG!" : $"{winner.name} MENANG!";
            Say(title, $"{engine.RoundNumber} ronde, {engine.TurnNumber} giliran  ·  "
                + (VRTableInput.XRActive ? "tunjuk Main lagi + trigger untuk main lagi" : "klik Main lagi (atau R) untuk main lagi"));
            SetButton(restartButton, true);
            if (waiter != null) waiter.Play(CharacterReaction.Wave);
            if (winner != null)
            {
                Popup(Above(winner, .6f), "<b>MENANG!</b>", GoldColor, 4);
                for (int i = 0; i < 2; i++)
                {
                    winner.Play(CharacterReaction.Cheer);
                    if (winner.IsHuman) foreach (var s in seats) if (s != winner) s.Play(CharacterReaction.Shocked);
                    yield return Wait(2.3f);
                }
            }
            yield return Wait(2);
        }

        void ResetVisuals()
        {
            foreach (var t in temporary) if (t != null) Destroy(t);
            temporary.Clear();
            foreach (var prop in props) if (prop.selectable != null) Destroy(prop.selectable.gameObject);
            props.Clear();
            if (candyObject != null) Destroy(candyObject);
            candyObject = null;
            candySelectable = null;
            shownCandy = null;
            SetButton(restartButton, false);
            log.Clear();
            logDirty = true;
            if (speech != null) speech.text = "";
            if (engine != null) engine.Logged -= OnLogged;
            foreach (var s in seats)
            {
                s.ResetState();
                s.player = null;
            }
            if (waiter != null) waiter.ResetState();
        }

        void Focus(Seat active, Transform activeLook)
        {
            Transform head = active?.Head;
            foreach (var s in seats) s.Attention = s == active ? activeLook : head;
            if (waiter != null) waiter.attention = head;
        }

        // ------------------------------------------------------------------ visual helper

        GameObject SpawnCandy(CandyColor color, Vector3 position)
        {
            GameObject go;
            if (candyTemplates.TryGetValue(color, out var template))
            {
                go = Instantiate(template, position, template.transform.rotation, transform);
                go.SetActive(true);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform, false);
                go.transform.position = position;
                go.transform.localScale = Vector3.one * .05f;
            }
            go.name = "Permen " + color;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            temporary.Add(go);
            return go;
        }

        IEnumerator Move(Transform t, Vector3 to, float duration, float arc, Vector3? scale = null)
        {
            if (t == null) yield break;
            Vector3 from = t.position, scaleFrom = t.localScale, scaleTo = scale ?? t.localScale;
            duration /= Mathf.Max(.05f, tempo);
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                if (t == null) yield break;
                float u = Mathf.Clamp01(time / duration), e = u * u * (3 - 2 * u);
                t.position = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(u * Mathf.PI) * arc;
                t.localScale = Vector3.Lerp(scaleFrom, scaleTo, e);
                yield return null;
            }
            if (t == null) yield break;
            t.position = to;
            t.localScale = scaleTo;
        }

        IEnumerator Punch(Transform t)
        {
            Vector3 rest = t.localScale;
            yield return Move(t, t.position, .08f, 0, rest * 1.35f);
            yield return Move(t, t.position, .15f, 0, rest);
        }

        void Popup(Vector3 position, string text, Color color, float duration = 1.8f) =>
            StartCoroutine(PopupRoutine(position, text, color, duration));

        IEnumerator PopupRoutine(Vector3 position, string text, Color color, float duration)
        {
            var label = MakeText("Popup", position, .0085f, color);
            label.text = text;
            temporary.Add(label.gameObject);
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                if (label == null) yield break;
                label.transform.position = position + Vector3.up * (time * .08f);
                float fade = 1 - Mathf.InverseLerp(duration * .65f, duration, time);
                label.color = new Color(color.r, color.g, color.b, fade);
                yield return null;
            }
            billboards.Remove(label.transform);
            Destroy(label.gameObject);
        }

        static Material MakeCardMaterial()
        {
            // Unlit supaya kartu tidak ikut gelap-terang. Kalau shader ini nanti di-strip di build, tambahkan ke
            // Graphics Settings > Always Included Shaders; tanpa shader, teks tetap tampil tanpa kartu.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var material = new Material(shader) { name = "BoB Text Card" };
            material.SetColor("_BaseColor", new Color(1, .97f, .9f));
            return material;
        }

        TextMesh MakeText(string name, Vector3 position, float size, Color color, bool card = false)
        {
            var text = WorldText.Create(name, transform, font, size, color);
            var go = text.gameObject;
            go.transform.position = position;
            billboards.Add(go.transform);
            if (card && cardMaterial != null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "Kartu";
                quad.transform.SetParent(go.transform, false);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = cardMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                cards.Add((text, quad.transform));
            }
            return text;
        }

        /// <summary>Titik di atas kepala bot. Untuk manusia: di atas alasnya, supaya terlihat dari kursi sendiri.</summary>
        Vector3 Above(Seat s, float height)
        {
            if (s.IsHuman) return (s.mat != null ? s.mat.position : tableCenter) + Vector3.up * (.18f + height * .4f);
            var head = s.Head;
            return (head != null ? head.position : s.character.transform.position + Vector3.up * 1.1f) + Vector3.up * height;
        }

        // Label manusia di pojok kiri alasnya, supaya tidak menutupi nampan permen di depannya.
        Vector3 LabelPosition(Seat s)
        {
            if (!s.IsHuman || s.mat == null) return Above(s, .33f);
            Vector3 toCenter = Flat(tableCenter - s.mat.position);
            return s.mat.position - Vector3.Cross(Vector3.up, toCenter) * .3f - toCenter * .05f + Vector3.up * .07f;
        }

        Vector3 Mouth(Seat s)
        {
            var head = s.Head;
            Vector3 p = head != null ? head.position : tableCenter;
            if (s.IsHuman) return p + Flat(tableCenter - p) * .15f + Vector3.down * .12f;
            return p + Flat(tableCenter - p) * .2f + Vector3.down * .07f;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        // Engine membuat pemain sesuai urutan seats, jadi ConfigIndex langsung menunjuk kursinya (tidak lewat nama).
        Seat SeatOf(PlayerState p) => seats[p.ConfigIndex];

        WaitForSeconds Wait(float seconds) => new WaitForSeconds(seconds / Mathf.Max(.05f, tempo));

        void Say(string text, string detail = null)
        {
            if (caption != null) caption.text = detail == null ? $"<b>{text}</b>" : $"<b>{text}</b>\n<size=36>{detail}</size>";
        }

        void OnLogged(string message)
        {
            log.Add(message);
            if (log.Count > 200) log.RemoveAt(0);
            logDirty = true;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (!ready) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) Restart();
                if (keyboard.leftBracketKey.wasPressedThisFrame) tempo = Mathf.Max(.25f, tempo / 1.5f);
                if (keyboard.rightBracketKey.wasPressedThisFrame) tempo = Mathf.Min(4, tempo * 1.5f);
                if (keyboard.escapeKey.wasPressedThisFrame) Cancel();
                if (keyboard.xKey.wasPressedThisFrame) DiscardPocket();
            }
            UpdatePointer();
        }

        void UpdatePointer()
        {
            TableSelectable target = null;
            var mouse = Mouse.current;
            var camera = Camera.main;
            bool pointing = mode != HumanMode.None || (restartButton != null && restartButton.interactable);
            if (pointing && mouse != null && camera != null && Cursor.lockState != CursorLockMode.Locked
                && !PanelRect.Contains(GuiMouse(mouse)))
            {
                var ray = camera.ScreenPointToRay(mouse.position.ReadValue());
                target = TableSelectable.Raycast(ray, 15, out _);
            }
            if (target != hovered)
            {
                if (hovered != null) hovered.SetHover(false);
                hovered = target;
                // Bot tidak ikut membesar: skalanya dipakai CharacterAnimator. Highlight-nya cukup tooltip.
                if (hovered != null && !botTargets.ContainsValue(hovered)) hovered.SetHover(true);
            }
            if (hovered != null && mouse.leftButton.wasPressedThisFrame) hovered.Select();
        }

        void LateUpdate()
        {
            if (!ready) return;
            var camera = Camera.main;
            if (engine != null)
            {
                UpdateBoardText();
                foreach (var s in seats)
                {
                    if (s.label == null) continue;
                    s.label.transform.position = LabelPosition(s);
                    UpdateSeatText(s);
                }
            }
            if (candyLabel != null)
            {
                var c = engine?.ActiveCandy;
                candyLabel.gameObject.SetActive(candyObject != null && c == shownCandy && c != null);
                if (candyLabel.gameObject.activeSelf)
                {
                    candyLabel.transform.position = candyObject.transform.position + Vector3.up * .09f;
                    candyLabel.text = (c.Revealed ? (c.IsGood ? "<color=#21884c><b>GOOD</b></color>" : "<color=#c42e29><b>BAD</b></color>") : "<b>?</b>")
                        + (engine.CloverOnActive ? " <color=#bf8c0d><b>x2</b></color>" : "");
                }
            }
            foreach (var (text, card) in cards)
            {
                if (text == null || card == null) continue;
                var bounds = text.GetComponent<MeshRenderer>().localBounds;
                bool visible = text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text);
                card.gameObject.SetActive(visible);
                if (!visible) continue;
                card.localPosition = new Vector3(bounds.center.x, bounds.center.y, .002f);
                card.localScale = new Vector3(bounds.size.x + .03f, bounds.size.y + .02f, 1);
                if (text.TryGetComponent(out BoxCollider box))
                {
                    box.center = new Vector3(bounds.center.x, bounds.center.y, 0);
                    box.size = new Vector3(bounds.size.x + .03f, bounds.size.y + .02f, .02f);
                }
            }
            if (camera == null) return;
            for (int i = billboards.Count - 1; i >= 0; i--)
            {
                var t = billboards[i];
                if (t == null) { billboards.RemoveAt(i); continue; }
                Vector3 away = t.position - camera.transform.position;
                if (away.sqrMagnitude < 1e-6f) continue;
                t.rotation = Quaternion.LookRotation(away);
                // Ukuran asli pas untuk jarak duduk di meja; dari jauh (kamera overview) teks ikut membesar.
                t.localScale = Vector3.one * Mathf.Clamp(away.magnitude / 1.1f, 1, 2.2f);
            }
        }

        int DisplaySeconds => mode != HumanMode.None && rules.TurnTimeLimitSeconds > 0
            ? Mathf.CeilToInt(Mathf.Max(0, turnTimer)) : -1;

        void UpdateBoardText()
        {
            var state = (engine.RoundNumber, engine.CandiesInDispenser, engine.GoodLeft, engine.BadLeft, DisplaySeconds);
            if (boardText != null && boardState == state) return;
            boardState = state;
            string timer = boardState.seconds >= 0 ? $"\n<color=#c42e29>Waktu: {boardState.seconds} detik</color>" : "";
            string text = $"Ronde {boardState.round}  ·  Dispenser: {boardState.candies} permen\nSisa: {boardState.good} good, {boardState.bad} bad{timer}";
            if (text == boardText) return;
            boardText = text;
            board.text = text;
        }

        void UpdateSeatText(Seat s)
        {
            var p = s.player;
            var state = (s.name, p != null, p != null && p.Eliminated, p?.Hp ?? 0, engine.Config.MaxHp,
                p?.Items.Count ?? 0, p != null && (p.Paralyzed || s.Paralyzed), p != null && p.HasCupcake,
                p != null && engine.Current == p && !engine.IsOver);
            // Bandingkan nilai sebelum interpolasi supaya frame diam tidak membuat string baru.
            if (s.labelText != null && s.labelState == state) return;
            s.labelState = state;
            string text = SeatText(s);
            if (text == s.labelText) return;
            s.labelText = text;
            s.label.text = text;
        }

        string SeatText(Seat s)
        {
            var p = s.player;
            if (p == null) return $"<b>{s.name}</b>";
            if (p.Eliminated) return $"<b>{s.name}</b>\n<color=#c42e29>KO</color>";
            string status = (p.Paralyzed || s.Paralyzed ? "  <color=#8040a6>Lumpuh</color>" : "")
                + (p.HasCupcake ? "  <color=#2666a6>Kebal</color>" : "");
            string turn = engine.Current == p && !engine.IsOver ? "> " : "";
            return $"<b>{turn}{s.name}</b>\nHP {p.Hp}/{engine.Config.MaxHp}  ·  {p.Items.Count} item{status}";
        }

        // ------------------------------------------------------------------ panel desktop

        void EnsureGuiStyles()
        {
            if (logStyle != null) return;
            logStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, richText = true };
            tooltipStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            panelStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 };
        }

        void OnGUI()
        {
            if (engine == null) return;
            EnsureGuiStyles();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (showLog && Event.current.type == EventType.Repaint)
            {
                const int lines = 9;
                var rect = new Rect(10, Screen.height - 24 - lines * 18, 560, lines * 18 + 16);
                GUI.Box(rect, GUIContent.none);
                if (logDirty || logText == null || logTempo != tempo)
                {
                    logTempo = tempo;
                    logDirty = false;
                    logText = $"<b>Bite or Betray</b>   R mulai ulang   [ ] tempo x{tempo:0.0#}\n"
                        + string.Join("\n", log.Skip(Math.Max(0, log.Count - (lines - 1))));
                }
                GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, rect.height - 8), logText, logStyle);
            }
#endif
            if (hovered != null && !string.IsNullOrEmpty(hovered.tooltip) && Mouse.current != null)
            {
                Vector2 m = Mouse.current.position.ReadValue();
                GUI.Box(new Rect(m.x + 18, Screen.height - m.y + 8, 300, 44), hovered.tooltip, tooltipStyle);
            }
            if (mode != HumanMode.None && human?.player != null) HumanPanel();
        }

        static Rect PanelRect => new Rect(Screen.width - 330, Screen.height - 330, 320, 320);

        static Vector2 GuiMouse(Mouse mouse)
        {
            Vector2 m = mouse.position.ReadValue();
            return new Vector2(m.x, Screen.height - m.y);
        }

        // Tombol cadangan untuk desktop: aksi yang sama dengan klik objek di meja.
        void HumanPanel()
        {
            var me = human.player;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            var state = (DisplaySeconds, me.Hp, engine.Config.MaxHp, me.HasCupcake);
            if (panelText == null || panelState != state)
            {
                panelState = state;
                string timer = panelState.seconds >= 0 ? $"  ·  {panelState.seconds} detik" : "";
                panelText = $"<b>Giliran kamu</b>{timer}\nHP {me.Hp}/{engine.Config.MaxHp}" + (me.HasCupcake ? "  ·  Kebal" : "");
            }
            GUILayout.Label(panelText, panelStyle);
            switch (mode)
            {
                case HumanMode.Action:
                    if (GUILayout.Button("Makan permen")) OnCandySelected();
                    GUILayout.Label("Suapkan ke:");
                    GUILayout.BeginHorizontal();
                    foreach (var s in seats)
                        if (!s.IsHuman && s.player.IsAlive && GUILayout.Button(s.name)) OnBotSelected(s);
                    GUILayout.EndHorizontal();
                    if (me.Items.Count > 0) GUILayout.Label("Pakai item:");
                    foreach (var prop in props.Where(p => p.seat == human && !p.pocket).ToList())
                        if (GUILayout.Button(ItemInfo.Name(prop.type))) OnPropSelected(prop);
                    break;
                case HumanMode.Pocket:
                    GUILayout.Label("Little Pocket, pilih 1:");
                    foreach (var prop in props.Where(p => p.pocket).ToList())
                        if (GUILayout.Button(ItemInfo.Name(prop.type))) OnPropSelected(prop);
                    if (GUILayout.Button("Buang isinya (X)")) DiscardPocket();
                    break;
                case HumanMode.Grapple:
                    GUILayout.Label("Grappling Hook, curi item:");
                    foreach (var prop in props.Where(p => p.selectable.interactable && !p.pocket && p.seat != human).ToList())
                        if (GUILayout.Button($"{prop.seat.name}: {ItemInfo.Name(prop.type)}")) OnPropSelected(prop);
                    if (GUILayout.Button("Batal (Esc)")) Cancel();
                    break;
            }
            GUILayout.EndArea();
        }
    }
}
