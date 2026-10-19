using System;
using System.Collections.Generic;
using System.Linq;
using BiteOrBetray.Core;
using UnityEngine;

namespace BiteOrBetray.DebugTools
{
    /// <summary>
    /// Test table without VR. Attach to an empty GameObject and press Play.
    /// Human seats take turns on the same screen (hotseat); bots act automatically.
    /// </summary>
    public class BiteOrBetrayDebugGame : MonoBehaviour
    {
        [Header("Rules (applied when the game starts)")]
        public RuleConfig rules = new RuleConfig();

        [Header("Bot")]
        public bool autoPlayBots = true;
        [Range(0f, 3f)] public float botDelay = 0.8f;

        [Header("Timer & telemetry")]
        [Tooltip("Thinking timer per turn for human seats. Limit set in Rules > Turn Time Limit Seconds.")]
        public bool useTurnTimer = true;
        [Tooltip("Record thinking seconds for each decision to CSV, to calibrate the Simulator timing model.")]
        public bool recordTelemetry = true;

        [Header("Miscellaneous")]
        [Tooltip("0 = random each game")] public int seed;
        [Range(0.75f, 2f)] public float uiScale = 1.15f;
        public bool logToConsole;

        class SeatSetup
        {
            public string name;
            public bool bot;
            public BotDifficulty diff = BotDifficulty.Normal;
        }

        readonly List<SeatSetup> setup = new List<SeatSetup>();
        readonly Queue<Action> queued = new Queue<Action>();

        GameEngine engine;
        System.Random botRng;
        float botTimer;
        int lastTurn = -1;
        int lastLogCount;
        int usedSeed;
        Vector2 logScroll, controlScroll;
        bool godMode, choosingGiveTarget;
        int grappleItemIndex = -1, grapplePocketIndex = -1, grappleTargetSeat = -1;
        string message = "";
        TelemetryRecorder telemetry;
        int timerTurn = -1;
        float turnTimeLeft, turnThinkUsed, decisionStart;
        bool gameOverRecorded;

        public static string TelemetryFolder => System.IO.Path.Combine(Application.persistentDataPath, "telemetry");
        float TimeLimit => rules.TurnTimeLimitSeconds;
        bool TimerOn => useTurnTimer && TimeLimit > 0;
        string seedText = "0";

        GUIStyle wrap, bold, title, big, wrapButton;

        void Awake()
        {
            setup.Add(new SeatSetup { name = "You", bot = false });
            setup.Add(new SeatSetup { name = "Bot A", bot = true });
            setup.Add(new SeatSetup { name = "Bot B", bot = true, diff = BotDifficulty.Hard });
            seedText = seed.ToString();
        }

        void Update()
        {
            while (queued.Count > 0) queued.Dequeue()();

            if (engine == null) return;
            if (engine.TurnNumber != lastTurn)
            {
                lastTurn = engine.TurnNumber;
                ResetTransientUi();
            }
            if (engine.IsOver)
            {
                if (!gameOverRecorded)
                {
                    gameOverRecorded = true;
                    telemetry?.Finish(engine);
                }
                botTimer = 0;
                return;
            }
            if (engine.TurnNumber != timerTurn)
            {
                timerTurn = engine.TurnNumber;
                turnTimeLeft = TimeLimit;
                turnThinkUsed = 0;
                decisionStart = Time.realtimeSinceStartup;
            }
            if (!engine.Current.IsBot)
            {
                botTimer = 0;
                turnThinkUsed += Time.deltaTime;
                if (TimerOn)
                {
                    turnTimeLeft -= Time.deltaTime;
                    if (turnTimeLeft <= 0)
                    {
                        var who = engine.Current;
                        float think = Time.realtimeSinceStartup - decisionStart;
                        engine.TimeoutTurn();
                        telemetry?.Record(engine, who, "TIMEOUT", "forced to eat", think, turnThinkUsed);
                        message = $"Time's up! {who.Name} is forced to eat candy.";
                        decisionStart = Time.realtimeSinceStartup;
                        timerTurn = -1;
                    }
                }
                return;
            }
            if (!autoPlayBots) return;
            botTimer += Time.deltaTime;
            if (botTimer >= botDelay)
            {
                botTimer = 0;
                StepBot();
            }
        }

        void Queue(Action a) => queued.Enqueue(a);

        void StartGame()
        {
            if (!int.TryParse(seedText, out seed)) seed = 0;
            usedSeed = seed != 0 ? seed : UnityEngine.Random.Range(1, int.MaxValue);
            engine = new GameEngine(rules.Clone(), usedSeed);
            botRng = new System.Random(usedSeed + 7);
            if (logToConsole) engine.Logged += s => Debug.Log("[BoB] " + s);
            engine.Start(setup.Select(s => new SeatConfig(s.name, s.bot, s.diff)).ToList());
            telemetry = null;
            if (recordTelemetry)
            {
                try { telemetry = new TelemetryRecorder(TelemetryFolder, usedSeed, setup.Count); }
                catch (System.Exception e) { Debug.LogWarning("[BoB] Cannot create telemetry: " + e.Message); }
            }
            gameOverRecorded = false;
            timerTurn = -1;
            lastTurn = -1;
            lastLogCount = 0;
            message = $"Seed: {usedSeed} (enter in the Seed field to replay the same game)";
        }

        /// <summary>Execute a human player's action, then record their thinking time in seconds.</summary>
        void Human(string action, string detail, System.Func<ActionResult> act)
        {
            var who = engine.Current;
            float think = Time.realtimeSinceStartup - decisionStart;
            var r = act();
            if (r.Success) telemetry?.Record(engine, who, action, detail, think, turnThinkUsed);
            decisionStart = Time.realtimeSinceStartup;
            Report(r);
        }

        void StepBot()
        {
            var d = BotBrain.Decide(engine, botRng);
            var actor = engine.Current;
            var who = actor.Name;
            var r = BotBrain.Execute(engine, d);
            if (r.Success) telemetry?.Record(engine, actor, d.Kind.ToString().ToUpperInvariant(), d.Reason, botDelay, botDelay);
            if (!r.Success)
            {
                message = $"{who}: decision failed ({r.Error}), fallback.";
                if (engine.Phase == GamePhase.AwaitingPocketChoice) engine.DiscardPocket();
                else engine.Eat();
            }
            else message = $"{who} [bot]: {d}";
        }

        void Report(ActionResult r) => message = r.Success ? "" : "Cannot: " + r.Error;

        void ResetTransientUi()
        {
            choosingGiveTarget = false;
            grappleItemIndex = grapplePocketIndex = grappleTargetSeat = -1;
        }

        // ================================================================== GUI

        void InitStyles()
        {
            if (wrap != null) return;
            wrap = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };
            bold = new GUIStyle(wrap) { fontStyle = FontStyle.Bold };
            title = new GUIStyle(bold) { fontSize = 18 };
            big = new GUIStyle(bold) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            wrapButton = new GUIStyle(GUI.skin.button) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
        }

        void OnGUI()
        {
            InitStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            float w = Screen.width / uiScale, h = Screen.height / uiScale;
            if (engine == null) DrawSetup(w, h);
            else DrawGame(w, h);
        }

        void DrawSetup(float w, float h)
        {
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(620, w - 40), h - 40), GUI.skin.box);
            GUILayout.Label("BITE OR BETRAY: test table (without VR)", title);
            GUILayout.Label("Set up 2-6 seats, at least 1 human. Human seats take turns on this screen.", wrap);
            GUILayout.Space(6);

            for (int i = 0; i < setup.Count; i++)
            {
                int idx = i;
                var s = setup[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Seat {i + 1}", GUILayout.Width(60));
                s.name = GUILayout.TextField(s.name, 16, GUILayout.Width(130));
                if (GUILayout.Button(s.bot ? "Bot" : "Human", GUILayout.Width(80))) Queue(() => s.bot = !s.bot);
                GUI.enabled = s.bot;
                if (GUILayout.Button(s.bot ? s.diff.ToString() : "-", GUILayout.Width(80)))
                    Queue(() => s.diff = (BotDifficulty)(((int)s.diff + 1) % 3));
                GUI.enabled = setup.Count > 2;
                if (GUILayout.Button("Delete", GUILayout.Width(60))) Queue(() => setup.RemoveAt(idx));
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUI.enabled = setup.Count < 6;
            if (GUILayout.Button("+ Add seat", GUILayout.Width(160)))
                Queue(() => setup.Add(new SeatSetup { name = $"Bot {(char)('A' + setup.Count - 1)}", bot = true }));
            GUI.enabled = true;

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed (0 = random)", GUILayout.Width(110));
            seedText = GUILayout.TextField(seedText, 10, GUILayout.Width(110));
            GUILayout.EndHorizontal();
            autoPlayBots = GUILayout.Toggle(autoPlayBots, " Bots play automatically");
            GUILayout.Label($"Bot delay: {botDelay:0.0} seconds");
            botDelay = GUILayout.HorizontalSlider(botDelay, 0f, 3f, GUILayout.Width(250));
            useTurnTimer = GUILayout.Toggle(useTurnTimer, $" Timer {rules.TurnTimeLimitSeconds} seconds per turn (human seats)");
            recordTelemetry = GUILayout.Toggle(recordTelemetry, " Record telemetry (thinking seconds per decision)");
            GUILayout.Label("Spawn values (candy count, bad ratio, items per round) can be changed in the Inspector under Rules.", wrap);

            GUILayout.Space(10);
            bool hasHuman = setup.Any(s => !s.bot);
            GUI.enabled = hasHuman;
            if (GUILayout.Button(hasHuman ? "START GAME" : "Needs at least 1 human", GUILayout.Height(40))) Queue(StartGame);
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        void DrawGame(float w, float h)
        {
            const float leftW = 300, centerW = 420, pad = 10;
            float logX = leftW + centerW + pad * 3;

            GUILayout.BeginArea(new Rect(pad, pad, leftW, h - pad * 2), GUI.skin.box);
            GUILayout.Label($"Round {engine.RoundNumber}  |  Turn {engine.TurnNumber}", bold);
            foreach (var p in engine.Players) DrawPlayer(p);
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(leftW + pad * 2, pad, centerW, h - pad * 2), GUI.skin.box);
            controlScroll = GUILayout.BeginScrollView(controlScroll);
            DrawTableInfo();
            GUILayout.Space(8);
            if (engine.IsOver)
                GUILayout.Label(engine.Winner != null ? $"WINNER: {engine.Winner.Name}" : "No winner", big);
            else if (engine.Current.IsBot) DrawBotControls();
            else DrawHumanControls();
            GUILayout.Space(8);
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, wrap);
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Play again")) Queue(StartGame);
            if (GUILayout.Button("Back to setup")) Queue(() => engine = null);
            if (GUILayout.Button("Telemetry folder")) Queue(() => Application.OpenURL("file://" + TelemetryFolder));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(logX, pad, Mathf.Max(260, w - logX - pad), h - pad * 2), GUI.skin.box);
            DrawLog();
            GUILayout.EndArea();
        }

        void DrawPlayer(PlayerState p)
        {
            bool cur = p == engine.Current && !engine.IsOver;
            GUILayout.BeginVertical(GUI.skin.box);
            string kind = p.IsBot ? $"Bot {p.Difficulty}" : "Human";
            GUILayout.Label($"{(cur ? "> " : "")}{p.Name}   [{kind}]   d20: {p.D20Roll}", cur ? bold : wrap);
            if (p.Eliminated)
            {
                GUILayout.Label("ELIMINATED (spectator)", wrap);
            }
            else
            {
                int max = engine.Config.MaxHp;
                GUILayout.Label($"HP {p.Hp}/{max}   {new string('#', p.Hp)}{new string('.', Mathf.Max(0, max - p.Hp))}", wrap);
                var st = new List<string>();
                if (p.Paralyzed) st.Add("PARALYZED");
                if (p.HasCupcake) st.Add("CUPCAKE IMMUNITY");
                GUILayout.Label("Status: " + (st.Count == 0 ? "-" : string.Join(", ", st)), wrap);
                GUILayout.Label($"Item ({p.Items.Count}/{engine.Config.MaxItems}): " +
                                (p.Items.Count == 0 ? "-" : string.Join(", ", p.Items.Select(ItemInfo.Name))), wrap);
            }
            GUILayout.EndVertical();
        }

        void DrawTableInfo()
        {
            GUILayout.Label("The waiter announces:", bold);
            GUILayout.Label(engine.Announcement.ToString(), wrap);
            GUILayout.Label($"Remaining in dispenser: {engine.CandiesInDispenser}   |   Remaining good/bad (including active candy): {engine.GoodLeft}/{engine.BadLeft}", wrap);
            godMode = GUILayout.Toggle(godMode, " God mode (reveal active candy contents)");
            GUILayout.Space(6);

            GUILayout.Label("Active candy:", bold);
            var c = engine.ActiveCandy;
            if (c == null)
            {
                GUILayout.Label("-", wrap);
                return;
            }
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = ColorOf(c.Color);
            GUILayout.Box($"{CandyRules.ColorName(c.Color).ToUpper()} CANDY", GUILayout.Height(36), GUILayout.ExpandWidth(true));
            GUI.backgroundColor = prev;

            string info = c.Revealed ? $"Revealed: {(c.IsGood ? "GOOD" : "BAD")}, {c.FlavorName}" : "Contents unknown";
            if (!c.Revealed && godMode) info += $"   [god: {(c.IsGood ? "GOOD" : "BAD")}, {c.FlavorName}]";
            GUILayout.Label(info, wrap);
            GUILayout.Label(CandyRules.EffectSummary(c.Color), wrap);
            GUILayout.Label(engine.CloverOnActive ? "CLOVER ACTIVE: x2 effects for whoever eats it" : " ", bold);
        }

        void DrawBotControls()
        {
            var p = engine.Current;
            GUILayout.Label($"{p.Name}'s turn (bot {p.Difficulty})", title);
            autoPlayBots = GUILayout.Toggle(autoPlayBots, " Bots play automatically");
            GUILayout.Label($"Bot delay: {botDelay:0.0} seconds");
            botDelay = GUILayout.HorizontalSlider(botDelay, 0f, 3f);
            GUI.enabled = !autoPlayBots;
            if (GUILayout.Button("Next bot step", GUILayout.Height(30))) Queue(StepBot);
            GUI.enabled = true;
        }

        void DrawHumanControls()
        {
            var p = engine.Current;
            GUILayout.Label($"{p.Name}'s turn", title);
            if (TimerOn)
            {
                int secs = Mathf.Max(0, Mathf.CeilToInt(turnTimeLeft));
                int bars = Mathf.Clamp(Mathf.CeilToInt(turnTimeLeft / TimeLimit * 30f), 0, 30);
                GUILayout.Label($"Thinking time left: {secs} seconds   {new string('|', bars)}", secs <= 10 ? bold : wrap);
            }
            else GUILayout.Label("Turn timer disabled.", wrap);

            if (grappleItemIndex >= 0 || grapplePocketIndex >= 0)
            {
                DrawGrapple(p);
                return;
            }
            if (engine.Phase == GamePhase.AwaitingPocketChoice)
            {
                DrawPocket(p);
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("EAT\neat yourself", GUILayout.Height(44))) Queue(() => Human("EAT", "", engine.Eat));
            if (GUILayout.Button(choosingGiveTarget ? "Cancel" : "GIVE\nfeed another player", GUILayout.Height(44)))
                Queue(() => choosingGiveTarget = !choosingGiveTarget);
            GUILayout.EndHorizontal();

            if (choosingGiveTarget)
            {
                GUILayout.Label("Feed whom?", bold);
                foreach (var t in engine.Players)
                {
                    if (t == p || !t.IsAlive) continue;
                    var target = t;
                    string extra = (t.HasCupcake ? ", immune" : "") + (t.Paralyzed ? ", paralyzed" : "");
                    if (GUILayout.Button($"{t.Name} (HP {t.Hp}{extra})"))
                        Queue(() =>
                        {
                            choosingGiveTarget = false;
                            Human("GIVE", target.Name, () => engine.Give(target.Seat));
                        });
                }
            }

            GUILayout.Space(6);
            GUILayout.Label("Use an item (does not end the turn):", bold);
            if (p.Items.Count == 0) GUILayout.Label("No items.", wrap);
            for (int i = 0; i < p.Items.Count; i++)
            {
                int idx = i;
                var it = p.Items[i];
                if (!GUILayout.Button($"{ItemInfo.Name(it)}: {ItemInfo.Description(it)}", wrapButton)) continue;
                if (it == ItemType.GrapplingHook)
                    Queue(() =>
                    {
                        if (engine.HasGrappleTarget(p)) grappleItemIndex = idx;
                        else message = "No opponent items to take.";
                    });
                else Queue(() => Human("ITEM", ItemInfo.Name(it), () => engine.UseItem(idx)));
            }
        }

        void DrawPocket(PlayerState p)
        {
            GUILayout.Label("Little Pocket: choose 1 item, used immediately.", bold);
            var opts = engine.PocketOptions;
            for (int i = 0; i < opts.Count; i++)
            {
                int idx = i;
                var it = opts[i];
                if (!GUILayout.Button($"{ItemInfo.Name(it)}: {ItemInfo.Description(it)}", wrapButton)) continue;
                if (it == ItemType.GrapplingHook)
                    Queue(() =>
                    {
                        if (engine.HasGrappleTarget(p)) grapplePocketIndex = idx;
                        else message = "No opponent items to take.";
                    });
                else Queue(() => Human("POCKET_CHOICE", ItemInfo.Name(it), () => engine.ChoosePocketItem(idx)));
            }
            if (GUILayout.Button("Discard pocket contents")) Queue(() => Human("POCKET_DISCARD", "", engine.DiscardPocket));
        }

        void DrawGrapple(PlayerState p)
        {
            GUILayout.Label("Grappling Hook: choose a target, then the item to take.", bold);
            if (grappleTargetSeat < 0)
            {
                foreach (var t in engine.Players)
                {
                    if (t == p || !t.IsAlive) continue;
                    var target = t;
                    GUI.enabled = t.Items.Any(x => x != ItemType.GrapplingHook);
                    if (GUILayout.Button($"{t.Name} ({t.Items.Count} items)")) Queue(() => grappleTargetSeat = target.Seat);
                    GUI.enabled = true;
                }
            }
            else
            {
                var t = engine.Players[grappleTargetSeat];
                GUILayout.Label($"{t.Name}'s items:", wrap);
                for (int i = 0; i < t.Items.Count; i++)
                {
                    int idx = i;
                    var it = t.Items[i];
                    GUI.enabled = it != ItemType.GrapplingHook;
                    if (GUILayout.Button(ItemInfo.Name(it)))
                        Queue(() =>
                        {
                            var args = new ItemArgs(grappleTargetSeat, idx);
                            int pocketIdx = grapplePocketIndex, itemIdx = grappleItemIndex;
                            grappleItemIndex = grapplePocketIndex = grappleTargetSeat = -1;
                            Human(pocketIdx >= 0 ? "POCKET_CHOICE" : "ITEM", "Grappling Hook -> " + ItemInfo.Name(it), () => pocketIdx >= 0
                                ? engine.ChoosePocketItem(pocketIdx, args)
                                : engine.UseItem(itemIdx, args));
                        });
                    GUI.enabled = true;
                }
            }
            if (GUILayout.Button("Cancel")) Queue(() => grappleItemIndex = grapplePocketIndex = grappleTargetSeat = -1);
        }

        void DrawLog()
        {
            GUILayout.Label("Log", bold);
            var lines = engine.LogLines;
            if (lines.Count != lastLogCount)
            {
                lastLogCount = lines.Count;
                logScroll.y = float.MaxValue;
            }
            logScroll = GUILayout.BeginScrollView(logScroll);
            for (int i = Mathf.Max(0, lines.Count - 300); i < lines.Count; i++) GUILayout.Label(lines[i], wrap);
            GUILayout.EndScrollView();
        }

        static Color ColorOf(CandyColor c)
        {
            switch (c)
            {
                case CandyColor.Yellow: return new Color(1f, 0.88f, 0.25f);
                case CandyColor.Blue: return new Color(0.35f, 0.65f, 1f);
                default: return new Color(1f, 0.4f, 0.4f);
            }
        }
    }
}
