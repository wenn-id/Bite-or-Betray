using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Core;
using BiteOrBetray.DebugTools;
using BiteOrBetray.Table3D.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = System.Random;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Run GameEngine in CandyParlour3D: bots (BotBrain) and one human seat (Seat_0), then visualize
    /// every action: candy leaving the dispenser, being eaten or fed, character reactions, HP, items, and waiter announcements.
    /// The engine is synchronous, so the director snapshots state before an action and plays the differences one by one.
    /// All references are found by ArtSceneBuilder object names, so rebuilding the scene is safe.
    /// </summary>
    public sealed partial class CandyTableDirector : MonoBehaviour
    {
        [Header("Game")]
        public bool autoStart = true;
        public bool loop = true;
        [Tooltip("Seat_0 is played by a human. Disable to watch bots only.")]
        public bool humanPlayer = true;
        [Tooltip("Empty = the localized name for the player, based on the language in Settings.")]
        public string humanName = "";
        [Tooltip("0 = random each game.")]
        public int seed;
        [Tooltip("Before the first game, choose bot count (1-5) and difficulty using table buttons. Needs a human seat; otherwise all bots participate.")]
        public bool roomSetup = true;
        [Tooltip("Open the app at the table main menu (Play vs Bots, Create/Join Room, Settings), instead of starting immediately. Needs a human seat.")]
        public bool mainMenu = true;
        [Tooltip("Before the first game this session, show how to play at the table (draw, eat, Give, items). Skippable. Needs a human seat.")]
        public bool onboarding = true;
        [Tooltip("Initial bot levels per seat: Bot_Yellow, Bot_Blue, Bot_Red, Bot_Mint, Bot_Peach. Can be changed at the table before starting.")]
        public BotDifficulty[] difficulties = { BotDifficulty.Normal, BotDifficulty.Hard, BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard };
        [Tooltip("Bot display names per seat. Deliberately differ from candy color names (yellow/blue/red) to avoid ambiguous narration.")]
        public string[] botNames = { "Yellow", "Blue", "Red", "Mint", "Peach" };
        public RuleConfig rules = new RuleConfig();

        [Header("Telemetry")]
        [Tooltip("Record actions and thinking seconds to CSV in persistentDataPath/telemetry, like the debug table.")]
        public bool recordTelemetry = true;

        [Header("Tempo")]
        [Range(.25f, 4)] public float tempo = 1;
        public Vector2 thinkSeconds = new Vector2(1.2f, 2.4f);

        [Header("Display")]
        public bool showLog;
        [Tooltip("Empty = find BoB_Art3D in the scene.")]
        public Transform artRoot;
        [Tooltip("Shared URP Unlit material asset for text card backgrounds, without runtime copies.")]
        public Material cardMaterial;

        static readonly string[] Colors = { "Yellow", "Blue", "Red", "Mint", "Peach" };
        static readonly Color Ink = new Color(.23f, .15f, .12f);
        static readonly Color GoodColor = new Color(.13f, .55f, .3f);
        static readonly Color BadColor = new Color(.78f, .18f, .16f);
        static readonly Color InfoColor = new Color(.15f, .4f, .65f);
        static readonly Color GoldColor = new Color(.75f, .55f, .05f);
        static readonly Color StatusColor = new Color(.5f, .25f, .65f);
        static readonly Color DisabledColor = new Color(.55f, .5f, .48f);

        const int LowHp = 2;

        sealed class Seat
        {
            public string name, offlineName;
            public int index = -1;
            public CharacterAnimator character;
            public AvatarReactionController avatarReaction;
            public SeatedAvatar avatar;
            public SpectatorAppearance characterAppearance;
            public Transform anchor, mat;
            public TextMesh label;
            public WorldTextLayout.Obstacle headBounds;
            public PlayerState player;
            public (string name, bool hasPlayer, bool eliminated, int hp, int maxHp, int items, bool paralyzed, bool cupcake, bool turn) labelState;
            public string labelText;
            bool paralyzed, defeated;

            public bool IsHuman => character == null;

            public Transform Head
            {
                get
                {
                    if (character != null && avatar != null) return avatar.eyeReference;
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
                set
                {
                    defeated = value;
                    if (character != null)
                    {
                        character.defeated = value;
                        if (value && characterAppearance == null) characterAppearance = new SpectatorAppearance(character.transform);
                        characterAppearance?.SetSpectator(value);
                    }
                    avatar?.SetSpectator(value);
                    avatarReaction?.SetStatus(paralyzed, defeated);
                }
            }

            // Visual only: low HP -> weakness/dizziness/nausea. HP rules stay in the engine.
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
                paralyzed = false;
                Defeated = false;
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

        enum MenuPage { None, Main, Settings }

        // Settings persist across sessions through PlayerPrefs.
        sealed class PlayerPrefsStore : ISettingsStore
        {
            public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
            public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
            public void Save() => PlayerPrefs.Save();
        }

        static readonly ISettingsStore Prefs = new PlayerPrefsStore();

        sealed class Prop
        {
            public Seat seat;
            public int index;
            public ItemType type;
            public bool pocket;
            public string panelLabel;
            public TableSelectable selectable;
        }

        // seats = this game's participants (human + selected bots, matching SeatConfig order); allSeats = every scene seat.
        readonly List<Seat> seats = new List<Seat>();
        readonly List<Seat> allSeats = new List<Seat>();
        readonly Seat[] botSlots = new Seat[RoomSetup.SlotCount];
        readonly List<WorldTextLayout.Label> labels = new List<WorldTextLayout.Label>();
        readonly List<WorldTextLayout.Obstacle> textObstacles = new List<WorldTextLayout.Obstacle>();
        readonly List<GameObject> temporary = new List<GameObject>();
        readonly List<GameObject> hiddenDecor = new List<GameObject>();
        readonly List<Prop> props = new List<Prop>();
        readonly List<string> log = new List<string>();
        readonly Dictionary<CandyColor, GameObject> candyTemplates = new Dictionary<CandyColor, GameObject>();
        readonly Dictionary<ItemType, GameObject> itemTemplates = new Dictionary<ItemType, GameObject>();
        readonly Dictionary<Seat, TableSelectable> botTargets = new Dictionary<Seat, TableSelectable>();
        readonly Dictionary<Seat, TableSelectable> difficultyButtons = new Dictionary<Seat, TableSelectable>();
        CharacterAnimator waiter;
        Seat human;
        Transform spawnPoint;
        Vector3 trayPosition, tableCenter;
        TextMesh caption, board, speech, candyLabel, resultText;
        TextMesh[] mainMenuColumn;
        Font font;
        GameEngine engine;
        NetworkTableSession network;
        TableSnapshot remoteState;
        long displayedRevision = -1;
        int localConfigIndex = -1;
        bool remoteInputOpen, actionPending, boundAsClient;
        ITableState State => remoteState != null ? (ITableState)remoteState : engine;
        bool NetworkClient => network != null && network.IsSpawned && !network.IsServer;
        bool NetworkActive => network != null && network.IsSpawned;
        bool NetworkListening => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        Candy shownCandy;
        GameObject candyObject;
        TableSelectable candySelectable, hovered, discardButton, cancelButton, restartButton;
        TableSelectable fewerButton, moreButton, globalButton, startButton, menuButton;
        TextMesh botCountText;
        RoomSetup room;
        bool settingUp;
        MenuPage menuPage;
        TableSelectable playBotsButton, createRoomButton, joinRoomButton, settingsButton;
        TableSelectable volumeDownButton, volumeUpButton, languageButton, handButton, snapButton, backButton;
        readonly List<(TableSelectable button, Phrase label)> phraseButtons = new List<(TableSelectable, Phrase)>();
        TableSelectable voiceDownButton, voiceUpButton, voiceMuteButton;
        TextMesh menuTitle, volumeText;
        TextMesh voiceTitle, voiceVolumeText;
        Vector3 menuPanel, menuRight;
        readonly List<GameObject> lobbyItems = new List<GameObject>();
        bool lobbyOpen;
        TextMesh onboardingText;
        TableSelectable nextButton, skipButton;
        List<OnboardingPage> onboardingPages;
        int onboardingPage = -1;
        bool onboardingDone;
        GameSettings settings;
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
        GameplayAudio gameplayAudio;
        RoomVoiceChat voice;
        GameObject grabbedCandy;

        /// <summary>
        /// Called by Back to menu after stopping the game and clearing the table; with a main menu, after it opens
        /// (for example so the lobby leaves its room). Without a main menu or listeners, Back to menu opens
        /// bot setup.
        /// </summary>
        public event Action MenuRequested;

        /// <summary>Online lobby for Create/Join Room (BOB-035). Null = buttons disabled. See IRoomLobby.</summary>
        public IRoomLobby Lobby { get; set; }

        /// <summary>Player settings (volume, voice, dominant hand, mouth snap, language) loaded from PlayerPrefs.</summary>
        public GameSettings Settings => settings;

        /// <summary>Human seat name. Empty or the legacy localized scene value uses "You" in the selected language.</summary>
        public string HumanName => string.IsNullOrWhiteSpace(humanName) || humanName == Strings.You.Indonesian ? Strings.You.Text : humanName;

        public bool VoiceRoomActive => voice != null && voice.InRoom;

        /// <summary>Spectators can only point at voice Settings; gameplay actions remain locked.</summary>
        public bool IsVoiceSetting(TableSelectable target) => target != null
            && (target == voiceDownButton || target == voiceUpButton || target == voiceMuteButton);

        /// <summary>Called by the lobby after joining/leaving a session; knockout rules do not change the voice channel.</summary>
        public void SetVoiceRoom(string sessionId)
        {
            voice?.SetRoom(sessionId);
            RefreshVoiceSettings();
        }

        bool InMenu => menuPage != MenuPage.None;
        bool InOnboarding => onboardingPage >= 0;

        public bool SpectatorInputLocked => human?.player != null && human.player.Eliminated && State != null && !State.IsOver;
        bool HumanCanAct => human?.player != null && human.player.IsAlive && State != null && !State.IsOver && State.Current == human.player
            && (!NetworkClient || (localConfigIndex == human.player.ConfigIndex && remoteInputOpen && !actionPending));

        void Awake()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            // Serialized values from old scenes must also be disabled in release builds.
            showLog = false;
#endif
        }

        void Start()
        {
            gameplayAudio = GetComponent<GameplayAudio>();
            if (gameplayAudio == null) gameplayAudio = gameObject.AddComponent<GameplayAudio>();
            vrInput = GetComponent<VRTableInput>();
            if (vrInput == null) vrInput = gameObject.AddComponent<VRTableInput>();
            settings = GameSettings.Load(Prefs);
            voice = new RoomVoiceChat(settings, message =>
            {
                if (this != null) ShowMessage(Strings.VoiceChat.Text, message);
            });
            ApplySettings();
            ready = Setup();
            if (ready) gameplayAudio.PlayMusic();
            if (NetworkActive || NetworkListening)
            {
                if (NetworkClient && network.Latest != null) ApplyNetworkSnapshot(network.Latest);
                return;
            }
            if (!ready || !autoStart) return;
            if (playBotsButton != null) OpenMainMenu();
            else if (startButton != null) OpenRoomSetup();
            else Restart();
        }

        void OnDestroy()
        {
            voice?.SetRoom(null);
            if (network != null)
            {
                network.SnapshotReceived -= ApplyNetworkSnapshot;
                network.ActionRejected -= NetworkActionRejected;
                network.TableStopped -= NetworkTableStopped;
            }
            foreach (var s in allSeats)
            {
                s.avatar?.SetSpectator(false);
                s.characterAppearance?.Dispose();
            }
            foreach (var t in candyTemplates.Values) if (t != null) t.SetActive(true);
            foreach (var t in hiddenDecor) if (t != null) t.SetActive(true);
        }

        [ContextMenu("Restart game")]
        public void Restart()
        {
            if (!ready || NetworkClient || (NetworkListening && !NetworkActive)) return;
            if (NetworkActive && network.Seats == null) return;
            StopAllCoroutines();
            // SetMode also disables Discard/Cancel buttons and Little Pocket options if restarting mid-turn.
            SetMode(HumanMode.None);
            SetButton(restartButton, false);
            SetButton(menuButton, false);
            // Play again and R use the last room choices; from bot setup, this is the Start button.
            settingUp = false;
            ShowMenu(MenuPage.None);
            HideOnboarding();
            LobbyOpen = false;
            if (NetworkActive) ApplyNetworkSeats(network.Seats);
            else ApplyRoom();
            StartCoroutine(RunGames());
        }

        /// <summary>
        /// Offline room selection at the table: bot count, each bot's level or all bots, then Start. Unselected bots are
        /// hidden and their seats are empty. Opened before the first game and via Bot setup on the results screen.
        /// </summary>
        [ContextMenu("Bot setup")]
        public void OpenRoomSetup()
        {
            if (!ready || startButton == null || NetworkActive || NetworkListening) return;
            StopGame();
            settingUp = true;
            ShowMenu(MenuPage.None);
            ApplyRoom();
            Focus(human, null);
            Say(Strings.SetupTitle.Text, (VRTableInput.XRActive ? Strings.SetupHintVr : Strings.SetupHintDesktop).Text);
        }

        /// <summary>Back to menu button on the results and bot setup screens. See MenuRequested.</summary>
        [ContextMenu("Back to menu")]
        public void ReturnToMenu()
        {
            if (!ready) return;
            if (playBotsButton != null) OpenMainMenu();
            else if (MenuRequested == null)
            {
                OpenRoomSetup();
                return;
            }
            else
            {
                StopGame();
                settingUp = false;
                ApplyRoom();
                if (caption != null) caption.text = "";
            }
            MenuRequested?.Invoke();
        }

        /// <summary>
        /// Main menu on the table in front of the player: Play vs Bots (to bot setup), Create Room, Join Room, Settings. Stop the
        /// current game; bots from the last room choices stay seated for atmosphere.
        /// </summary>
        [ContextMenu("Main menu")]
        public void OpenMainMenu()
        {
            if (!ready || playBotsButton == null) return;
            StopGame();
            settingUp = false;
            LobbyOpen = false;
            if (!LobbyAlive) Lobby = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IRoomLobby>().FirstOrDefault();
            ShowMenu(MenuPage.Main);
            ApplyRoom();
            Focus(human, null);
            SayMenuHint();
        }

        void SayMenuHint() => Say((VRTableInput.XRActive ? Strings.MenuHintVr : Strings.MenuHintDesktop).Text);

        void PlayVsBots()
        {
            ShowMenu(MenuPage.None);
            if (startButton != null) OpenRoomSetup();
            else Restart();
        }

        // A destroyed lobby (Unity component) is treated as absent; lobby exceptions must not stop the menu.
        bool LobbyAlive => Lobby != null && !(Lobby is UnityEngine.Object o && o == null);

        bool LobbyReady
        {
            get
            {
                if (!LobbyAlive) return false;
                try { return Lobby.Ready; }
                catch (Exception e)
                {
                    Debug.LogWarning("[Bite or Betray] Cannot check lobby: " + e.Message, this);
                    return false;
                }
            }
        }

        void OpenLobby(bool create)
        {
            if (!LobbyReady)
            {
                ShowMenu(MenuPage.Main);
                Say(Strings.OnlineUnavailable.Text, Strings.OnlineUnavailableDetail.Text);
                return;
            }
            ShowMenu(MenuPage.None);
            try
            {
                if (create) Lobby.CreateRoom(this);
                else Lobby.JoinRoom(this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                OpenMainMenu();
                Say((create ? Strings.RoomCreateFailed : Strings.RoomJoinFailed).Text, e.Message);
            }
        }

        void ChangeSettings(Action<GameSettings> change)
        {
            change(settings);
            settings.Save(Prefs);
            ApplySettings();
            ShowMenu(MenuPage.Settings);
        }

        void ChangeVoiceSettings(Action<GameSettings> change)
        {
            change(settings);
            settings.Save(Prefs);
            voice?.ApplySettings();
            RefreshVoiceSettings();
        }

        // The voice Settings panel beside the menu stays available while in a room, including when spectating after a knockout.
        void RefreshVoiceSettings()
        {
            if (settings == null) return;
            bool show = menuPage == MenuPage.Settings || VoiceRoomActive;
            SetButton(voiceDownButton, show && settings.VoiceVolume > 0);
            SetButton(voiceUpButton, show && settings.VoiceVolume < 100);
            SetButton(voiceMuteButton, show);
            if (voiceTitle != null)
            {
                voiceTitle.gameObject.SetActive(show);
                voiceTitle.text = $"<b>{Strings.VoiceSettings}</b>";
            }
            if (voiceVolumeText != null)
            {
                voiceVolumeText.gameObject.SetActive(show);
                voiceVolumeText.text = $"<b>{Strings.VoiceVolume.Format(settings.VoiceVolume)}</b>";
            }
            if (voiceMuteButton != null)
                SetButtonText(voiceMuteButton, $"<b>{Strings.MicrophoneValue.Format((settings.VoiceMuted ? Strings.MicMuted : Strings.MicOn).Text)}</b>",
                    Strings.MicrophoneTip.Text);
        }

        // Master volume goes through the audio hub; dominant hand and mouth snap go to VR input; language goes to all table text.
        void ApplySettings()
        {
            gameplayAudio.SetMasterVolume(settings.Volume01);
            vrInput.mouthDistance = settings.MouthDistance;
            vrInput.leftHanded = settings.LeftHanded;
            voice?.ApplySettings();
            if (Localization.Language == settings.Language) return;
            Localization.Language = settings.Language;
            if (ready) RefreshLanguage();
        }

        /// <summary>
        /// Language changes in Settings immediately rebuild button labels, human seat names, menus, boards, and seat labels.
        /// Other text (narration, onboarding, lobby, popups) is read from Strings each time it appears.
        /// </summary>
        void RefreshLanguage()
        {
            foreach (var (button, label) in phraseButtons) SetButtonText(button, $"<b>{label}</b>", label.Text);
            if (!NetworkActive) ApplyRoom();
            boardText = null;
            foreach (var s in allSeats) s.labelText = null;
            ShowMenu(menuPage);
            if (InMenu) SayMenuHint();
        }

        static string SnapName(MouthSnap snap) =>
            (snap == MouthSnap.Low ? Strings.SnapLow : snap == MouthSnap.High ? Strings.SnapHigh : Strings.SnapNormal).Text;

        /// <summary>Show one menu page (or hide all) with the latest Settings values.</summary>
        void ShowMenu(MenuPage page)
        {
            menuPage = playBotsButton != null ? page : MenuPage.None;
            bool main = menuPage == MenuPage.Main, options = menuPage == MenuPage.Settings;
            if (menuTitle != null)
            {
                menuTitle.gameObject.SetActive(InMenu);
                menuTitle.text = options ? $"<b>{Strings.Settings}</b>" : "<b>Menu</b>";
            }
            SetButton(playBotsButton, main);
            bool online = main && LobbyReady;
            SetLobbyButton(createRoomButton, main, online, Strings.CreateRoom.Text, Strings.OnlineUnavailableNote.Text);
            SetLobbyButton(joinRoomButton, main, online, Strings.JoinRoom.Text, Strings.OnlineUnavailableNote.Text);
            SetButton(settingsButton, main);
            SetButton(volumeDownButton, options && settings.Volume > 0);
            SetButton(volumeUpButton, options && settings.Volume < 100);
            if (volumeText != null)
            {
                volumeText.gameObject.SetActive(options);
                volumeText.text = $"<b>{Strings.Volume.Format(settings.Volume)}</b>";
            }
            SetButton(languageButton, options);
            SetButton(handButton, options);
            SetButton(snapButton, options);
            SetButton(backButton, options);
            RefreshVoiceSettings();
            if (!options) return;
            SetButtonText(languageButton, $"<b>{Strings.LanguageValue.Format(Localization.NativeName(settings.Language))}</b>",
                Strings.LanguageTip.Text);
            SetButtonText(handButton, $"<b>{Strings.DominantHandValue.Format((settings.LeftHanded ? Strings.HandLeft : Strings.HandRight).Text)}</b>",
                Strings.DominantHandTip.Text);
            SetButtonText(snapButton, $"<b>{Strings.MouthSnapValue.Format(SnapName(settings.MouthSnap))}</b>",
                Strings.MouthSnapTip.Text);
        }

        /// <summary>
        /// Online buttons stay visible but gray and cannot be pointed at while unavailable (lobby not ready,
        /// incomplete code, too few players); a small note appears below a disabled button's label.
        /// </summary>
        public static void SetLobbyButton(TableSelectable button, bool show, bool enabled, string label, string note = null)
        {
            SetButton(button, show);
            if (button == null || !show) return;
            button.interactable = enabled;
            var text = button.GetComponent<TextMesh>();
            text.color = enabled ? WorldTextLayout.Cream : DisabledColor;
            text.text = enabled || note == null ? $"<b>{label}</b>" : $"<b>{label}</b>\n<size=32>{note}</size>";
        }

        // ------------------------------------------------------------------ online lobby panel (BOB-035)

        /// <summary>Currently bound online table session (null if not spawned).</summary>
        public NetworkTableSession Network => network;

        /// <summary>Scene seat count (player + bots), the online room participant limit.</summary>
        public int SeatCount => allSeats.Count;

        /// <summary>
        /// The lobby is using the table menu panel (room code, keypad). Mouse pointer is also active. Automatically set false
        /// when opening the main menu or starting a game; all lobby buttons/text are hidden too.
        /// </summary>
        public bool LobbyOpen
        {
            get => lobbyOpen;
            set
            {
                lobbyOpen = value;
                if (value) return;
                foreach (var item in lobbyItems) if (item != null) item.SetActive(false);
            }
        }

        /// <summary>Lobby panel button (hidden). offset: x to the player's right, y above the menu panel base, in meters.</summary>
        public TableSelectable AddLobbyButton(string label, Vector2 offset, Action onSelect)
        {
            var button = MakeButton(label, LobbySpot(offset), onSelect);
            lobbyItems.Add(button.gameObject);
            return button;
        }

        /// <summary>As above with a Strings label; object names remain English.</summary>
        public TableSelectable AddLobbyButton(Phrase label, Vector2 offset, Action onSelect)
        {
            var button = MakeButton(label, LobbySpot(offset), onSelect);
            lobbyItems.Add(button.gameObject);
            return button;
        }

        /// <summary>Lobby panel text card (hidden); show with gameObject.SetActive.</summary>
        public TextMesh AddLobbyText(string name, Vector2 offset, float size = .007f)
        {
            var text = MakeText(name, LobbySpot(offset), size, Ink, true);
            text.gameObject.SetActive(false);
            lobbyItems.Add(text.gameObject);
            return text;
        }

        /// <summary>Message in the narration above the table (lobby status, network errors, incorrect code).</summary>
        public void ShowMessage(string title, string detail = null) => Say(title, detail);

        Vector3 LobbySpot(Vector2 offset)
        {
            if (playBotsButton == null) throw new InvalidOperationException("Lobby panel needs a main menu and human seat.");
            return menuPanel + menuRight * offset.x + Vector3.up * offset.y;
        }

        /// <summary>Stop the current game and clear the table without starting a new game.</summary>
        void StopGame()
        {
            StopAllCoroutines();
            SetMode(HumanMode.None);
            HideOnboarding();
            ResetVisuals();
            RestoreNetworkAvatars();
            engine = null;
            telemetry = null;
            remoteState = null;
            remoteInputOpen = false;
            actionPending = false;
            humanChoice = null;
            displayedRevision = -1;
            network?.StopTable();
            boardText = null;
            board.text = "";
        }

        void ChangeBotCount(int delta)
        {
            room.BotCount += delta;
            ApplyRoom();
        }

        void CycleBotDifficulty(Seat bot)
        {
            room.CycleDifficulty(bot.index);
            ApplyRoom();
        }

        void CycleGlobalDifficulty()
        {
            room.CycleGlobalDifficulty();
            ApplyRoom();
        }

        /// <summary>Apply room choices: participant list, visible bot characters, and selection buttons.</summary>
        void ApplyRoom()
        {
            seats.Clear();
            if (human != null) human.offlineName = HumanName;
            foreach (var seat in allSeats) seat.name = seat.offlineName;
            if (human != null) seats.Add(human);
            foreach (int slot in room.ActiveSlots) seats.Add(botSlots[slot]);
            foreach (var s in allSeats)
            {
                bool playing = seats.Contains(s);
                if (s.character != null) s.character.gameObject.SetActive(playing);
                if (botTargets.TryGetValue(s, out var target)) target.gameObject.SetActive(playing);
                if (s.label != null) s.label.gameObject.SetActive(playing && !settingUp && !InMenu);
                if (!difficultyButtons.TryGetValue(s, out var button)) continue;
                SetButton(button, settingUp && playing);
                SetButtonText(button, $"<b>{s.name}</b>\n{Localization.Difficulty(room.DifficultyOf(s.index))}", Strings.ChangeLevelTip.Format(s.name));
            }
            SetButton(fewerButton, settingUp && room.BotCount > RoomSetup.MinBots);
            SetButton(moreButton, settingUp && room.BotCount < room.MaxBots);
            SetButton(globalButton, settingUp);
            SetButton(startButton, settingUp);
            // Bot setup can return to the main menu (same button as on the results screen, above Start).
            if (playBotsButton != null) SetButton(menuButton, settingUp);
            if (globalButton != null)
                SetButtonText(globalButton, $"<b>{Strings.AllBotsValue.Format(room.GlobalDifficulty is BotDifficulty d ? Localization.Difficulty(d) : Strings.Mixed.Text)}</b>",
                    Strings.AllBotsTip.Text);
            if (botCountText != null)
            {
                botCountText.gameObject.SetActive(settingUp);
                botCountText.text = $"<b>{Strings.BotCount.Format(room.BotCount)}</b>";
            }
        }

        /// <summary>Called by NetworkTableSession on spawn; clients never start RunGames.</summary>
        public void BindNetwork(NetworkTableSession session)
        {
            if (network == session) return;
            if (network != null) throw new InvalidOperationException("Table already bound to an online session.");
            network = session;
            // Record the role on spawn: NGO server/client flags are no longer reliable when despawning during shutdown.
            boundAsClient = !session.IsServer;
            network.SnapshotReceived += ApplyNetworkSnapshot;
            network.ActionRejected += NetworkActionRejected;
            network.TableStopped += NetworkTableStopped;
            if (!ready) return;
            StopGame();
            settingUp = false;
            ShowMenu(MenuPage.None);
            Say(Strings.WaitingForHost.Text);
        }

        public void UnbindNetwork(NetworkTableSession session)
        {
            if (network != session) return;
            network.SnapshotReceived -= ApplyNetworkSnapshot;
            network.ActionRejected -= NetworkActionRejected;
            network.TableStopped -= NetworkTableStopped;
            if (ready) StopGame();
            network = null;
            remoteState = null;
            actionPending = false;
            bool hostLost = boundAsClient;
            boundAsClient = false;
            // A lobby that left earlier has already opened the menu with its own message; do not overwrite it with the menu greeting.
            if (!ready || InMenu) return;
            OpenMainMenu();
            // No host migration (BOB-037): host departure/disconnection ends the client's game with a clear reason.
            if (hostLost) Say(Strings.HostLost.Text, Strings.HostLostDetail.Text);
        }

        public void StartNetworkTable(NetworkTableSession session)
        {
            if (!ready || network != session || !session.IsHost)
                throw new InvalidOperationException("Host table not ready yet.");
            Restart();
        }

        /// <summary>
        /// Online player disconnected (BOB-037): replace their avatar with a bot character in the same seat and continue the game.
        /// The host calls this from NetworkTableSession; clients call it when a snapshot changes an Online seat to Bot.
        /// </summary>
        internal void ShowSeatTakenByBot(int config)
        {
            if (!ready || config < 0 || config >= seats.Count || seats[config] == human) return;
            RestoreNetworkAvatar(config);
            var seat = seats[config];
            Popup(Above(seat, .45f), $"<b>{Strings.Disconnected}</b>\n{Strings.ReplacedByBot}", BadColor, 3f);
            Say(Strings.SeatDisconnected.Format(seat.name), Strings.SeatDisconnectedDetail.Text);
        }

        void ApplyNetworkSeats(IReadOnlyList<TableSeat> bindings)
        {
            localConfigIndex = -1;
            for (int i = 0; i < bindings.Count; i++)
                if ((network.IsHost && bindings[i].Controller == SeatController.Local)
                    || (!network.IsServer && bindings[i].Controller == SeatController.Online
                        && bindings[i].ClientId == network.NetworkManager.LocalClientId)) localConfigIndex = i;
            if (bindings.Count > allSeats.Count || (localConfigIndex >= 0 && human == null))
                throw new InvalidOperationException("Not enough scene seats for the online configuration.");
            seats.Clear();
            var others = allSeats.Where(s => s != human).ToList();
            int next = 0;
            for (int i = 0; i < bindings.Count; i++)
            {
                // Each machine places its own player in Seat_0; ConfigIndex still belongs to the host.
                bool atCamera = i == localConfigIndex || (localConfigIndex < 0 && human != null && i == 0);
                var seat = atCamera ? human : others[next++];
                seat.name = bindings[i].Name;
                seats.Add(seat);
            }
            foreach (var seat in allSeats)
            {
                bool playing = seats.Contains(seat);
                if (seat.character != null) seat.character.gameObject.SetActive(playing);
                if (seat.label != null) seat.label.gameObject.SetActive(playing);
                if (botTargets.TryGetValue(seat, out var target)) target.gameObject.SetActive(playing);
            }
            CreateBotTargets();
            ConfigureNetworkAvatars(bindings);
            foreach (var seat in seats)
                if (seat.label == null) seat.label = MakeSeatLabel(seat);
            if (human != null) PlaceDesktopCamera();
        }

        void ApplyNetworkSnapshot(TableSnapshot snapshot)
        {
            if (!ready || !NetworkClient) return;
            turnTimer = snapshot.SecondsLeft;
            bool changed = remoteState == null || snapshot.Revision != displayedRevision;
            bool inputChanged = snapshot.InputOpen != remoteInputOpen;
            remoteInputOpen = snapshot.InputOpen;
            if (!changed)
            {
                if (inputChanged) SetMode(remoteInputOpen ? (State.Phase == GamePhase.AwaitingPocketChoice ? HumanMode.Pocket : HumanMode.Action) : HumanMode.None);
                return;
            }
            bool newGame = remoteState == null || remoteState.GameId != snapshot.GameId;
            if (newGame)
            {
                ResetVisuals();
                settingUp = false;
                ShowMenu(MenuPage.None);
                HideOnboarding();
                LobbyOpen = false;
                ApplyNetworkSeats(snapshot.Seats);
            }
            var replaced = newGame ? new List<int>() : Enumerable.Range(0, Math.Min(snapshot.Seats.Length, remoteState.Seats.Length))
                .Where(i => remoteState.Seats[i].Controller == SeatController.Online && snapshot.Seats[i].Controller == SeatController.Bot).ToList();
            remoteState = snapshot;
            displayedRevision = snapshot.Revision;
            actionPending = false;
            foreach (var player in snapshot.Players)
            {
                var seat = SeatOf(player);
                seat.player = player;
                seat.Paralyzed = player.Paralyzed;
                seat.Defeated = player.Eliminated;
                seat.ShowHealth(player.Hp);
            }
            RefreshProps();
            // Candy/item transforms follow the host view, including actions still being animated.
            if (shownCandy?.Id == snapshot.ActiveCandy?.Id) shownCandy = snapshot.ActiveCandy;
            SetMode(remoteInputOpen ? (snapshot.Phase == GamePhase.AwaitingPocketChoice ? HumanMode.Pocket : HumanMode.Action) : HumanMode.None);
            Focus(SeatOf(snapshot.Current), candyObject != null ? candyObject.transform : null);
            if (snapshot.IsOver)
            {
                string title = snapshot.Winner == null ? Strings.NoWinner.Text : Strings.PlayerWins.Format(snapshot.Winner.Name);
                Say(Strings.GameOverSummary.Format(State.RoundNumber, State.TurnNumber));
                ShowResult(title);
                SetButton(menuButton, true);
            }
            else Say(Strings.TurnOf.Format(snapshot.Current.Name), (snapshot.Phase == GamePhase.AwaitingPocketChoice ? Strings.ChoosePocketItem : Strings.WaitingForPlayer).Text);
            foreach (int config in replaced) ShowSeatTakenByBot(config);
        }

        void NetworkTableStopped()
        {
            if (!ready || !NetworkClient) return;
            StopGame();
            settingUp = false;
            ShowMenu(MenuPage.None);
            Say(Strings.WaitingForHost.Text);
        }

        void NetworkActionRejected(string message)
        {
            actionPending = false;
            if (ready && NetworkClient && State != null)
            {
                SetMode(remoteInputOpen ? (State.Phase == GamePhase.AwaitingPocketChoice ? HumanMode.Pocket : HumanMode.Action) : HumanMode.None);
                Say(Strings.ActionRejected.Text, message);
            }
        }

        ActionResult ExecuteDecision(BotDecision decision, bool timeout = false) => NetworkActive
            ? network.Execute(decision, timeout)
            : timeout ? engine.TimeoutTurn() : BotBrain.Execute(engine, decision);

        // ------------------------------------------------------------------ setup

        bool Setup()
        {
            if (artRoot == null) artRoot = GameObject.Find("BoB_Art3D")?.transform;
            if (artRoot == null) return Fail("BoB_Art3D not found. Build the scene through the Bite or Betray menu first.");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cardMaterial == null) return Fail("Text card material not assigned in the scene.");

            var seatsRoot = artRoot.Find("Seats");
            var mats = artRoot.Cast<Transform>().Where(t => t.name == "ItemMat").ToList();
            if (humanPlayer && seatsRoot != null)
            {
                var seat0 = seatsRoot.Find("Seat_0");
                if (seat0 != null)
                    human = new Seat { name = HumanName, anchor = seat0.Find("HeadAnchor") ?? seat0, mat = Nearest(mats, seat0.position),
                        avatar = seat0.GetComponentInChildren<SeatedAvatar>(true),
                        avatarReaction = seat0.GetComponentInChildren<AvatarReactionController>(true) };
            }
            if (human != null) allSeats.Add(human);
            for (int i = 0; i < Colors.Length && seatsRoot != null; i++)
            {
                var bot = FindDeep(seatsRoot, "Bot_" + Colors[i]);
                if (bot == null) continue;
                string botName = botNames != null && i < botNames.Length && !string.IsNullOrWhiteSpace(botNames[i]) ? botNames[i] : Colors[i];
                botSlots[i] = new Seat { name = botName, index = i, character = GetAnimator(bot), mat = Nearest(mats, bot.position) };
                allSeats.Add(botSlots[i]);
            }
            if (allSeats.Count < 2) return Fail("Needs at least 2 participants in BoB_Art3D/Seats.");
            // Without table selection, all existing bots participate (default RoomSetup).
            room = new RoomSetup(botSlots.Select(s => s != null).ToArray(), difficulties);
            // Names are used in narration and engine logs: report duplicates, then number them to avoid ambiguity.
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in allSeats)
            {
                string original = s.name;
                for (int n = 2; !usedNames.Add(s.name); n++) s.name = $"{original} {n}";
                s.offlineName = s.name;
                if (s.name != original) Debug.LogWarning($"[Bite or Betray] Duplicate participant name \"{original}\", renamed to \"{s.name}\".", this);
            }

            var waiterT = artRoot.Find("Waiter");
            if (waiterT != null) waiter = GetAnimator(waiterT);
            var dispenser = artRoot.Find("Dispenser");
            foreach (var seat in allSeats)
            {
                if (seat.character == null) continue;
                var obstacle = WorldTextLayout.Obstacle.Head(seat.character.transform);
                seat.headBounds = obstacle;
                if (obstacle != null) textObstacles.Add(obstacle);
            }
            if (dispenser != null)
                foreach (var renderer in dispenser.GetComponentsInChildren<Renderer>())
                    textObstacles.Add(new WorldTextLayout.Obstacle(renderer));
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

            // Decorative items at the table center serve as templates; real items appear on each seat's pad.
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
                // Table buttons (pointed at with VR controllers or clicked with a mouse), to the right of the player pad.
                Vector3 toCenter = Flat(tableCenter - human.mat.position);
                Vector3 spot = human.mat.position + Vector3.Cross(Vector3.up, toCenter) * .3f - toCenter * .05f + Vector3.up * .07f;
                discardButton = MakeButton(Strings.Discard, spot, DiscardPocket);
                cancelButton = MakeButton(Strings.Cancel, spot, Cancel);
                // Only shown in results when the two buttons above are disabled, so they may share a position.
                restartButton = MakeButton(Strings.PlayAgain, spot, Restart);
                menuButton = MakeButton(Strings.BackToMenu, spot + Vector3.up * .08f, ReturnToMenu);
                // Results above the table directly in front of the player, readable from the seat without turning.
                resultText = MakeText("Final Results", human.mat.position + toCenter * .3f + Vector3.up * .3f, .0065f, Ink, true);
                Vector3 right = Vector3.Cross(Vector3.up, toCenter);
                Vector3 panel = human.mat.position + toCenter * .12f + Vector3.up * .16f;
                if (roomSetup)
                {
                    // Room selection: panel in front of the player above the table, each bot's level above its head (positioned in LateUpdate).
                    startButton = MakeButton(Strings.Start, spot, Restart);
                    fewerButton = MakeButton(Strings.FewerBots, panel - right * .17f, () => ChangeBotCount(-1));
                    moreButton = MakeButton(Strings.MoreBots, panel + right * .17f, () => ChangeBotCount(1));
                    botCountText = MakeText("Bot Count", panel, .007f, Ink, true);
                    botCountText.gameObject.SetActive(false);
                    globalButton = MakeButton(Strings.AllBots, panel + Vector3.up * .08f, CycleGlobalDifficulty);
                    foreach (var bot in botSlots)
                    {
                        if (bot == null) continue;
                        var seat = bot;
                        difficultyButtons[bot] = MakeButton(bot.name, Above(bot, .4f), () => CycleBotDifficulty(seat));
                        AttachToHead(difficultyButtons[bot].GetComponent<TextMesh>(), bot);
                    }
                }
                if (mainMenu)
                {
                    // Main menu and Settings: button column in the bot setup panel position (never shown together).
                    menuPanel = panel;
                    menuRight = right;
                    menuTitle = MakeText("Menu Title", panel + Vector3.up * .33f, .009f, Ink, true);
                    menuTitle.gameObject.SetActive(false);
                    playBotsButton = MakeButton(Strings.PlayVsBots, panel + Vector3.up * .24f, PlayVsBots);
                    createRoomButton = MakeButton(Strings.CreateRoom, panel + Vector3.up * .16f, () => OpenLobby(true));
                    joinRoomButton = MakeButton(Strings.JoinRoom, panel + Vector3.up * .08f, () => OpenLobby(false));
                    settingsButton = MakeButton(Strings.Settings, panel, () => ShowMenu(MenuPage.Settings));
                    volumeDownButton = MakeButton("Volume -", panel - right * .17f + Vector3.up * .24f, () => ChangeSettings(s => s.ChangeVolume(-1)));
                    volumeUpButton = MakeButton("Volume +", panel + right * .17f + Vector3.up * .24f, () => ChangeSettings(s => s.ChangeVolume(1)));
                    volumeText = MakeText("Volume", panel + Vector3.up * .24f, .007f, Ink, true);
                    volumeText.gameObject.SetActive(false);
                    // Language sits at the top after volume for easy discovery; Back moves one row below snap.
                    languageButton = MakeButton(Strings.LanguageButton, panel + Vector3.up * .16f, () => ChangeSettings(s => s.CycleLanguage()));
                    handButton = MakeButton(Strings.DominantHand, panel + Vector3.up * .08f, () => ChangeSettings(s => s.LeftHanded = !s.LeftHanded));
                    snapButton = MakeButton(Strings.MouthSnap, panel, () => ChangeSettings(s => s.CycleMouthSnap()));
                    backButton = MakeButton(Strings.Back, panel - Vector3.up * .08f, () => ShowMenu(MenuPage.Main));
                    var voicePanel = panel + right * .62f;
                    voiceTitle = MakeText("Voice Settings", voicePanel + Vector3.up * .24f, .009f, Ink, true);
                    voiceTitle.gameObject.SetActive(false);
                    voiceVolumeText = MakeText("Voice Volume", voicePanel + Vector3.up * .16f, .007f, Ink, true);
                    voiceVolumeText.gameObject.SetActive(false);
                    voiceDownButton = MakeButton("-", voicePanel - right * .17f + Vector3.up * .16f,
                        () => ChangeVoiceSettings(s => s.ChangeVoiceVolume(-1)));
                    voiceUpButton = MakeButton("+", voicePanel + right * .17f + Vector3.up * .16f,
                        () => ChangeVoiceSettings(s => s.ChangeVoiceVolume(1)));
                    voiceMuteButton = MakeButton(Strings.Microphone, voicePanel + Vector3.up * .08f,
                        () => ChangeVoiceSettings(s => s.VoiceMuted = !s.VoiceMuted));
                }
                if (onboarding)
                {
                    // How to play before the first game: card at the menu panel, Next at Start, Skip above it.
                    onboardingText = MakeText("How to Play", panel + Vector3.up * .2f, .0065f, Ink, true);
                    onboardingText.gameObject.SetActive(false);
                    nextButton = MakeButton(Strings.Next, spot, NextOnboardingPage);
                    skipButton = MakeButton(Strings.Skip, spot + Vector3.up * .08f, EndOnboarding);
                }
            }
            else
            {
                restartButton = MakeButton(Strings.PlayAgain, tableCenter + Vector3.up * .6f, Restart);
                menuButton = MakeButton(Strings.BackToMenu, tableCenter + Vector3.up * .52f, ReturnToMenu);
                resultText = MakeText("Final Results", tableCenter + Vector3.up * .32f, .0065f, Ink, true);
            }
            resultText.gameObject.SetActive(false);
            board = MakeText("Table Info", tableCenter + Vector3.up * .30f, .0075f, Ink, true);
            caption = MakeText("Narration", tableCenter + Vector3.up * .52f, .009f, Ink, true);
            if (playBotsButton != null)
                mainMenuColumn = new[] { menuTitle, playBotsButton.GetComponent<TextMesh>(), createRoomButton.GetComponent<TextMesh>(),
                    joinRoomButton.GetComponent<TextMesh>(), settingsButton.GetComponent<TextMesh>(), caption };
            candyLabel = MakeText("Candy Label", trayPosition + Vector3.up * .1f, .007f, Ink);
            if (waiter != null) speech = MakeText("Waiter Announcement", waiter.transform.position + Vector3.up * 1.85f, .0065f, Ink, true);
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
                var go = new GameObject("Click " + s.name);
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

        // ------------------------------------------------------------------ game flow

        IEnumerator RunGames()
        {
            yield return null; // wait for CharacterAnimator to finish Start (Head exists)
            CreateBotTargets();
            foreach (var s in seats)
                if (s.label == null) s.label = MakeSeatLabel(s);
            if (!NetworkActive && onboardingText != null && !onboardingDone)
            {
                onboardingPages = OnboardingGuide.Pages(rules, VRTableInput.XRActive);
                Focus(human, null);
                ShowOnboardingPage(0);
                while (InOnboarding) yield return null;
            }
            do
            {
                yield return PlayGame();
                if (!loop) break;
                yield return Wait(6);
            } while (true);
        }

        // ------------------------------------------------------------------ onboarding

        void NextOnboardingPage()
        {
            if (InOnboarding) ShowOnboardingPage(onboardingPage + 1);
        }

        void ShowOnboardingPage(int page)
        {
            if (page >= onboardingPages.Count)
            {
                EndOnboarding();
                return;
            }
            onboardingPage = page;
            var p = onboardingPages[page];
            onboardingText.text = $"<b>{p.Title}</b>\n<size=36>{string.Join("\n", p.Lines)}</size>";
            onboardingText.gameObject.SetActive(true);
            bool last = page == onboardingPages.Count - 1;
            SetButton(nextButton, true);
            SetButtonText(nextButton, $"<b>{(last ? Strings.StartPlaying : Strings.Next)}</b>", (last ? Strings.StartGameTip : Strings.NextPageTip).Text);
            SetButton(skipButton, !last);
            Say(Strings.OnboardingTitle.Format(page + 1, onboardingPages.Count),
                (VRTableInput.XRActive ? Strings.OnboardingHintVr : Strings.OnboardingHintDesktop).Text);
        }

        /// <summary>Completed or skipped: do not show again this session; start the first game immediately.</summary>
        void EndOnboarding()
        {
            if (!InOnboarding) return;
            onboardingDone = true;
            HideOnboarding();
        }

        void HideOnboarding()
        {
            onboardingPage = -1;
            if (onboardingText != null) onboardingText.gameObject.SetActive(false);
            SetButton(nextButton, false);
            SetButton(skipButton, false);
        }

        IEnumerator PlayGame()
        {
            ResetVisuals();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Evidence that restart creates no duplicates: this count must match every game. Destroy finishes at the end of the frame.
            yield return null;
            Debug.Log($"[Bite or Betray] Game #{gameIndex + 1}: {transform.childCount} child objects, "
                + $"{GetComponentsInChildren<TableSelectable>(true).Length} TableSelectable, {props.Count} pad items");
#endif
            gameIndex++;
            int gameSeed = seed != 0 ? seed + gameIndex - 1 : Environment.TickCount;
            var rng = new Random(gameSeed + 7);
            if (NetworkClient || (NetworkListening && !NetworkActive)) yield break;
            if (NetworkActive) engine = network.BeginGame(rules, gameSeed);
            else
            {
                engine = new GameEngine(rules.Clone(), gameSeed);
                // BuildSeats order matches seats from ApplyRoom, so SeatOf stays correct.
                engine.Start(room.BuildSeats(human?.name, botSlots.Select(s => s?.name).ToArray()));
            }
            engine.Logged += OnLogged;
            foreach (var line in engine.LogLines) OnLogged(line);
            foreach (var p in State.Players) SeatOf(p).player = p;
            telemetry = null;
            if (recordTelemetry)
            {
                try
                {
                    telemetry = new TelemetryRecorder(System.IO.Path.Combine(Application.persistentDataPath, "telemetry"), gameSeed, seats.Count);
                    Debug.Log("[BoB] Telemetry: " + telemetry.FilePath, this);
                }
                catch (Exception e) { Debug.LogWarning("[BoB] Cannot create telemetry: " + e.Message, this); }
            }
            if (human != null) PlaceDesktopCamera();
            RefreshProps();
            if (NetworkActive) network.Publish(turnTimer);

            Say(Strings.NewGame.Text, $"seed {gameSeed}");
            foreach (var s in seats)
            {
                Popup(Above(s, .45f), $"d20: <b>{s.player.D20Roll}</b>", Ink, 2.6f);
                s.Play(CharacterReaction.Nod);
            }
            yield return Wait(2.8f);
            Say(Strings.TurnOrder.Format(string.Join(" > ", State.Players.Select(p => p.Name))));
            yield return Wait(1.8f);
            yield return AnnounceRound();
            yield return SyncCandy();

            int lastTurn = -1;
            BotActionKind? lastKind = BotActionKind.Eat; // null = last action rejected, nothing eaten
            while (!State.IsOver)
            {
                var actor = SeatOf(State.Current);
                bool newTurn = State.TurnNumber != lastTurn;
                if (newTurn)
                {
                    Say(actor.IsHuman ? Strings.YourTurn.Text : Strings.TurnOf.Format(actor.name),
                        actor.IsHuman ? $"HP {actor.player.Hp}" : $"HP {actor.player.Hp}  ·  {Localization.Difficulty(actor.player.Difficulty)}");
                    lastTurn = State.TurnNumber;
                    turnTimer = rules.TurnTimeLimitSeconds;
                    turnThinkUsed = 0;
                }
                else if (lastKind == BotActionKind.Eat) Say(actor.IsHuman ? Strings.SafeContinue.Text : Strings.BotContinues.Format(actor.name));
                Focus(actor, candyObject != null ? candyObject.transform : null);

                BotDecision decision;
                Snapshot before;
                ActionResult result;
                if (!actor.player.IsBot)
                {
                    TableAction remoteAction = null;
                    ulong remoteSender = 0;
                    if (NetworkActive && network.Seats[actor.player.ConfigIndex].Controller == SeatController.Online)
                    {
                        humanTimeout = false;
                        decisionThink = 0;
                        network.SetInput(true, turnTimer);
                        network.Publish(turnTimer);
                        while (!network.TryTakeRemote(out remoteAction, out remoteSender))
                        {
                            // Client disconnected during their turn: a bot already owns the seat; repeat the turn through the bot path.
                            if (!network.IsOnlineSeat(actor.player.ConfigIndex)) break;
                            decisionThink += Time.unscaledDeltaTime;
                            turnThinkUsed += Time.unscaledDeltaTime;
                            if (rules.TurnTimeLimitSeconds > 0)
                            {
                                turnTimer -= Time.deltaTime;
                                if (turnTimer <= 0) { humanTimeout = true; break; }
                            }
                            network.SetInput(true, turnTimer);
                            yield return null;
                        }
                        network.SetInput(false, turnTimer);
                        if (remoteAction == null && !humanTimeout)
                        {
                            lastKind = null;
                            continue;
                        }
                        humanChoice = remoteAction?.Decision;
                    }
                    else yield return HumanTurn(actor);
                    before = Capture();
                    if (humanTimeout)
                    {
                        decision = new BotDecision { Kind = BotActionKind.Eat };
                        result = ExecuteDecision(null, true);
                        // ForcedEat + good candy continues the turn: restart the timer to avoid consecutive timeouts.
                        turnTimer = rules.TurnTimeLimitSeconds;
                        Popup(Above(actor, .3f), $"<b>{Strings.TimeUp}</b>", BadColor);
                    }
                    else
                    {
                        decision = humanChoice;
                        result = remoteAction != null ? network.ExecuteRemote(remoteSender, remoteAction)
                            : ExecuteDecision(decision);
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[Bite or Betray] {actor.name}: {(humanTimeout ? "timeout" : DescribeChoice(decision))} -> "
                        + (result.Success ? "OK" : "rejected: " + result.Error));
#endif
                    reason = humanTimeout ? Strings.TimeUpReason.Text : null;
                }
                else
                {
                    actor.Thinking = true;
                    float decisionStart = Time.realtimeSinceStartup;
                    yield return Wait(State.Phase == GamePhase.AwaitingPocketChoice ? .9f
                        : Mathf.Lerp(thinkSeconds.x, thinkSeconds.y, (float)rng.NextDouble()));
                    decisionThink = Time.realtimeSinceStartup - decisionStart;
                    before = Capture();
                    decision = Decide(rng, out result);
                    actor.Thinking = false;
                    reason = decision.Reason != null ? $"\"{decision.Reason}\"" : null;
                }
                if (NetworkActive) network.Publish(turnTimer);
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
                case BotActionKind.Eat: return "eat candy";
                case BotActionKind.Give: return "feed " + State.Players[d.TargetSeat].Name;
                case BotActionKind.UseItem: return "use item #" + d.Index;
                case BotActionKind.ChoosePocket: return "choose Little Pocket #" + d.Index;
                default: return "discard Little Pocket";
            }
        }

        void RecordDecision(Seat actor, BotDecision d, Snapshot before)
        {
            if (telemetry == null) return;
            string action = d.Kind.ToString().ToUpperInvariant(), detail = d.Reason;
            if (actor.IsHuman)
            {
                // Action names and details follow the debug table; items come from the snapshot before the engine changes them.
                detail = "";
                if (humanTimeout) { action = "TIMEOUT"; detail = "forced to eat"; }
                else if (d.Kind == BotActionKind.Give) detail = State.Players[d.TargetSeat].Name;
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
                // Write results before the final animation so restarting cannot lose GAME_OVER.
                if (State.IsOver) telemetry.Finish(engine);
            }
            catch (Exception e)
            {
                telemetry = null;
                Debug.LogWarning("[BoB] Telemetry stopped: " + e.Message, this);
            }
        }

        BotDecision Decide(Random rng, out ActionResult result)
        {
            BotDecision d = null;
            result = ActionResult.Fail("");
            try
            {
                d = BotBrain.Decide(engine, rng);
                result = ExecuteDecision(d);
            }
            catch (Exception e) { Debug.LogException(e, this); }
            if (result.Success) return d;
            d = State.Phase == GamePhase.AwaitingPocketChoice
                ? new BotDecision { Kind = BotActionKind.DiscardPocket, Reason = "fallback" }
                : new BotDecision { Kind = BotActionKind.Eat, Reason = "fallback" };
            result = ExecuteDecision(d);
            return d;
        }

        Snapshot Capture()
        {
            var players = State.Players;
            return new Snapshot {
                candy = State.ActiveCandy,
                clover = State.CloverOnActive,
                round = State.RoundNumber,
                paralyzeApplied = engine.Stats.ParalyzeApplied,
                skipped = engine.Stats.TurnsSkipped,
                hp = players.Select(p => p.Hp).ToArray(),
                cupcake = players.Select(p => p.HasCupcake).ToArray(),
                eliminated = players.Select(p => p.Eliminated).ToArray(),
                items = players.Select(p => new List<ItemType>(p.Items)).ToArray(),
                pocket = new List<ItemType>(State.PocketOptions)
            };
        }

        IEnumerator Visualize(Seat actor, BotDecision d, Snapshot b)
        {
            RefreshProps(); // used items disappear from the pad immediately
            Seat eater = null;
            switch (d.Kind)
            {
                case BotActionKind.Eat:
                    eater = actor;
                    yield return Consume(actor, null, b);
                    break;
                case BotActionKind.Give:
                    eater = SeatOf(State.Players[d.TargetSeat]);
                    yield return Consume(eater, actor, b);
                    break;
                case BotActionKind.UseItem:
                    var used = b.items[actor.player.Seat][d.Index];
                    Say(Strings.UsesItem.Format(actor.name, ItemInfo.Name(used)), reason);
                    yield return ItemEffect(actor, used, d, b);
                    break;
                case BotActionKind.ChoosePocket:
                    var chosen = b.pocket[d.Index];
                    Say(Strings.PicksFromPocket.Format(actor.name, ItemInfo.Name(chosen)), reason);
                    yield return ItemEffect(actor, chosen, d, b);
                    break;
                default:
                    Say(Strings.DiscardsPocket.Format(actor.name), reason);
                    actor.Play(CharacterReaction.Nod);
                    yield return Wait(1);
                    break;
            }
            yield return Resolve(b, eater);
        }

        // ------------------------------------------------------------------ human seat

        /// <summary>Immediate feedback when a controller picks up candy, before the eat/Give decision.</summary>
        public void NotifyCandyGrabbed()
        {
            if (!HumanCanAct || mode != HumanMode.Action || candyObject == null) return;
            grabbedCandy = candyObject;
            gameplayAudio?.Play(GameplaySound.Pickup);
        }

        void PlayGameplaySound(GameplaySound sound)
        {
            ReceiveGameplaySound(sound);
            if (NetworkActive && network.IsServer) network.BroadcastGameplaySound(sound);
        }

        internal void ReceiveGameplaySound(GameplaySound sound)
        {
            if (!ready) return;
            // The VR player already hears pickup at their hand; the host cue serves bots, desktop, and other seats.
            if (sound == GameplaySound.Pickup && candyObject != null && grabbedCandy == candyObject) return;
            gameplayAudio?.Play(sound);
        }

        IEnumerator HumanTurn(Seat me)
        {
            humanChoice = null;
            humanTimeout = false;
            decisionThink = 0;
            bool pocket = State.Phase == GamePhase.AwaitingPocketChoice;
            if (NetworkActive) network.SetInput(true, turnTimer);
            SetMode(pocket ? HumanMode.Pocket : HumanMode.Action);
            if (pocket) Say(Strings.PocketPrompt.Text, Strings.PocketPromptDetail.Text);
            else Say(Strings.YourTurn.Text, (VRTableInput.XRActive ? Strings.YourTurnHintVr : Strings.YourTurnHintDesktop).Text);
            while (humanChoice == null && !humanTimeout)
            {
                // Freeze the timer and thinking time while unfocused; only update the numbers per frame.
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
                if (NetworkActive) network.SetInput(true, turnTimer);
                yield return null;
            }
            if (NetworkActive) network.SetInput(false, turnTimer);
            SetMode(HumanMode.None);
        }

        void SetMode(HumanMode next)
        {
            mode = HumanCanAct ? next : HumanMode.None;
            if (mode == HumanMode.None) vrInput?.ClearGameplayInteraction();
            if (mode != HumanMode.Grapple) grappleItem = -1;
            if (candySelectable != null)
            {
                candySelectable.interactable = mode == HumanMode.Action;
                candySelectable.tooltip = Strings.EatTip.Text;
            }
            foreach (var pair in botTargets)
            {
                pair.Value.interactable = mode == HumanMode.Action && pair.Key.player != null && pair.Key.player.IsAlive;
                pair.Value.tooltip = Strings.FeedTip.Format(pair.Key.name);
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
                    ? Strings.StealTip.Format(ItemInfo.Name(prop.type), prop.seat.name)
                    : $"{ItemInfo.Name(prop.type)}: {ItemInfo.Description(prop.type)}";
            }
        }

        void OnCandySelected()
        {
            if (HumanCanAct && mode == HumanMode.Action) humanChoice = new BotDecision { Kind = BotActionKind.Eat };
        }

        void OnBotSelected(Seat target)
        {
            if (HumanCanAct && mode == HumanMode.Action && target.player.IsAlive)
                humanChoice = new BotDecision { Kind = BotActionKind.Give, TargetSeat = target.player.Seat };
        }

        void OnPropSelected(Prop prop)
        {
            if (!HumanCanAct || mode == HumanMode.None || !prop.selectable.interactable) return;
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
                if (!State.Players.Any(p => p != human.player && p.IsAlive && p.Items.Any(i => i != ItemType.GrapplingHook)))
                {
                    Popup(Above(human, .3f), Strings.NothingToSteal.Text, BadColor, 2.2f);
                    return;
                }
                grappleFromPocket = prop.pocket;
                SetMode(HumanMode.Grapple);
                grappleItem = prop.index;
                Say(Strings.GrappleTitle.Text, (VRTableInput.XRActive ? Strings.GrappleHintVr : Strings.GrappleHintDesktop).Text);
                return;
            }
            humanChoice = prop.pocket
                ? new BotDecision { Kind = BotActionKind.ChoosePocket, Index = prop.index }
                : new BotDecision { Kind = BotActionKind.UseItem, Index = prop.index };
        }

        /// <summary>
        /// Cancel Grappling Hook target selection (Esc, controller B/Y, or the table's Cancel button). During
        /// onboarding, the same button skips it.
        /// </summary>
        public void Cancel()
        {
            if (InOnboarding)
            {
                EndOnboarding();
                return;
            }
            if (!HumanCanAct || mode != HumanMode.Grapple) return;
            SetMode(grappleFromPocket ? HumanMode.Pocket : HumanMode.Action);
            Say((grappleFromPocket ? Strings.PocketPrompt : Strings.YourTurn).Text);
        }

        void DiscardPocket()
        {
            if (!HumanCanAct) return;
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

        // ------------------------------------------------------------------ items on the pad

        void RefreshProps()
        {
            UnityEngine.Profiling.Profiler.BeginSample("BoB.RefreshProps");
            try
            {
                // Slots with unchanged item types keep the same objects and callbacks, including across online snapshots.
                foreach (var s in allSeats)
                    SyncProps(s, State != null && s.mat != null && s.player != null && s.player.IsAlive && seats.Contains(s)
                        ? s.player.Items : null, false);
                SetMode(mode);
            }
            finally { UnityEngine.Profiling.Profiler.EndSample(); }
        }

        void SyncProps(Seat seat, IReadOnlyList<ItemType> items, bool pocket)
        {
            int count = items?.Count ?? 0;
            for (int i = props.Count - 1; i >= 0; i--)
            {
                var prop = props[i];
                if (prop.seat != seat || prop.pocket != pocket) continue;
                if (prop.selectable != null && prop.index < count && prop.type == items[prop.index]) continue;
                if (prop.selectable != null)
                {
                    // Unity defers Destroy; disable immediately so old colliders cannot be selected this frame.
                    prop.selectable.gameObject.SetActive(false);
                    Destroy(prop.selectable.gameObject);
                }
                props.RemoveAt(i);
            }
            for (int i = 0; i < count; i++)
            {
                Prop existing = null;
                foreach (var prop in props)
                    if (prop.seat == seat && prop.pocket == pocket && prop.index == i) { existing = prop; break; }
                var position = MatSlot(seat, i, count) + (pocket ? Vector3.up * .16f : Vector3.zero);
                if (existing != null) existing.selectable.transform.position = position;
                else AddProp(seat, i, items[i], pocket, position);
            }
        }

        static void SetButtonText(TableSelectable button, string text, string tooltip)
        {
            button.GetComponent<TextMesh>().text = text;
            button.tooltip = tooltip;
        }

        static void SetButton(TableSelectable button, bool on)
        {
            if (button == null) return;
            button.gameObject.SetActive(on);
            button.interactable = on;
        }

        // Strings-labeled buttons: object names stay English (used by tests); RefreshLanguage updates their text.
        TableSelectable MakeButton(Phrase label, Vector3 position, Action onSelect)
        {
            var button = MakeButton(label.English, position, onSelect);
            SetButtonText(button, $"<b>{label}</b>", label.Text);
            phraseButtons.Add((button, label));
            return button;
        }

        TableSelectable MakeButton(string label, Vector3 position, Action onSelect)
        {
            var text = MakeText("Button " + label, position, .007f, WorldTextLayout.Cream, true);
            text.text = $"<b>{label}</b>";
            text.gameObject.AddComponent<BoxCollider>(); // size follows the text in LateUpdate
            var button = text.gameObject.AddComponent<TableSelectable>();
            button.kind = TableSelectable.Kind.Button;
            button.tooltip = label;
            button.onSelect = () =>
            {
                onSelect?.Invoke();
                // Callbacks may reset the table or change volume; play afterward so the cue remains audible.
                gameplayAudio?.Play(GameplaySound.MenuSelect);
            };
            text.gameObject.SetActive(false);
            return button;
        }

        void ShowPocketOptions(bool show)
        {
            if (human != null) SyncProps(human, show && State != null ? State.PocketOptions : null, true);
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
            prop.panelLabel = $"{seat.name}: {ItemInfo.Name(type)}";
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

        // ------------------------------------------------------------------ eating and feeding

        IEnumerator Consume(Seat eater, Seat giver, Snapshot b)
        {
            var candy = b.candy;
            PlayGameplaySound(GameplaySound.Pickup);
            string color = CandyRules.ColorName(candy.Color).ToLowerInvariant();
            if (giver == null)
            {
                Say(Strings.Eats.Format(eater.name, color), reason);
                eater.Play(CharacterReaction.Eat);
                yield return Wait(.3f);
            }
            else
            {
                PlayGameplaySound(GameplaySound.Give);
                Say(Strings.Feeds.Format(giver.name, color, eater.name), reason);
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
            PlayGameplaySound(candy.IsGood ? GameplaySound.EatGood : GameplaySound.EatBad);
            Popup(Above(eater, .45f), $"{candy.FlavorName}\n<b>{(candy.IsGood ? "GOOD" : "BAD")}</b>", candy.IsGood ? GoodColor : BadColor);
            if (b.clover) Popup(Above(eater, .65f), "<b>Clover x2!</b>", GoldColor);
            if (blocked)
            {
                eater.Play(CharacterReaction.Nod);
                Popup(Above(eater, .6f), Strings.ImmuneCupcake.Text, InfoColor);
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

        // ------------------------------------------------------------------ item effects

        IEnumerator ItemEffect(Seat actor, ItemType type, BotDecision d, Snapshot b)
        {
            PlayGameplaySound(GameplaySound.UseItem);
            var item = ShowItem(type, actor);
            TrackNetworkItem(item, type);
            actor.Play(CharacterReaction.Nod);
            yield return Wait(.7f);
            Vector3 overCandy = trayPosition + Vector3.up * .08f;
            switch (type)
            {
                case ItemType.PlasticFork:
                {
                    yield return MoveItem(item, overCandy, .4f, true);
                    var c = State.ActiveCandy ?? b.candy;
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
                    if (candyObject != null && State.ActiveCandy != b.candy)
                    {
                        yield return Move(candyObject.transform, spawnPoint.position, .45f, .1f, Vector3.zero);
                        Destroy(candyObject);
                        candyObject = null;
                        shownCandy = null;
                    }
                    else Popup(Above(actor, .5f), Strings.DispenserEmpty.Text, Ink);
                    yield return Wait(.3f);
                    break;
                case ItemType.Cupcake:
                    yield return MoveItem(item, Mouth(actor), .4f, false);
                    Popup(Above(actor, .5f), Strings.ImmuneOnce.Text, InfoColor);
                    yield return Wait(.6f);
                    break;
                case ItemType.CloverCoin:
                    yield return MoveItem(item, overCandy, .4f, false);
                    Popup(trayPosition + Vector3.up * .18f, "<b>Clover x2!</b>", GoldColor);
                    yield return Wait(.6f);
                    break;
                case ItemType.LittlePocket:
                    if (!actor.IsHuman)
                        Popup(Above(actor, .55f), Strings.PocketContents.Format(string.Join(", ", State.PocketOptions.Select(ItemInfo.Name))), Ink, 3);
                    yield return Wait(1.2f);
                    break;
                case ItemType.GrapplingHook:
                {
                    var target = SeatOf(State.Players[d.Args.TargetSeat]);
                    var stolen = b.items[d.Args.TargetSeat][d.Args.TargetItemIndex];
                    Say(Strings.Grapples.Format(actor.name, target.name, ItemInfo.Name(stolen)), reason);
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

        // ------------------------------------------------------------------ effects after an action

        IEnumerator Resolve(Snapshot b, Seat eater)
        {
            foreach (var s in seats)
            {
                int delta = s.player.Hp - b.hp[s.player.Seat];
                if (delta < 0) PlayGameplaySound(GameplaySound.Damage);
                s.ShowHealth(s.player.Hp);
                if (delta != 0) Popup(Above(s, .5f), delta > 0 ? $"<b>+{delta} HP</b>" : $"<b>{delta} HP</b>", delta > 0 ? GoodColor : BadColor);
            }
            if (engine.Stats.ParalyzeApplied > b.paralyzeApplied && eater != null && eater.player.IsAlive)
            {
                eater.Paralyzed = true;
                Popup(Above(eater, .7f), $"<b>{Strings.ParalyzedPopup}</b>", StatusColor);
                yield return Wait(.9f);
            }
            foreach (var s in seats)
            {
                if (!s.player.Eliminated || b.eliminated[s.player.Seat]) continue;
                PlayGameplaySound(GameplaySound.Elimination);
                s.Paralyzed = false;
                s.Defeated = true;
                if (s == human) SetMode(HumanMode.None);
                Say(s.IsHuman ? Strings.YouPoisoned.Text : Strings.PlayerPoisoned.Format(s.name),
                    s.IsHuman ? Strings.SpectatorRest.Text : null);
                Popup(Above(s, .7f), "<b>KO!</b>", BadColor);
                foreach (var other in seats)
                    if (other != s && other.player.IsAlive) other.Play(CharacterReaction.Shocked);
                yield return Wait(1.8f);
            }
            if (engine.Stats.TurnsSkipped > b.skipped)
                foreach (var s in seats)
                {
                    if (!s.Paralyzed || s.player.Paralyzed) continue;
                    Say(s.IsHuman ? Strings.YouStillParalyzed.Text : Strings.PlayerStillParalyzed.Format(s.name));
                    yield return Wait(1.4f);
                    s.Paralyzed = false;
                }
            if (State.RoundNumber != b.round) yield return AnnounceRound();
            yield return SyncCandy();
        }

        IEnumerator AnnounceRound()
        {
            var a = State.Announcement;
            PlayGameplaySound(GameplaySound.Waiter);
            if (waiter != null) waiter.Play(CharacterReaction.Wave);
            if (speech != null)
                speech.text = $"<b>{Strings.WaiterRound.Format(State.RoundNumber)}</b>\n{Strings.WaiterCounts.Format(a.Total, a.Yellow, a.Blue, a.Red)}\n{a.Good} good, {a.Bad} bad";
            Say(Strings.WaiterBrings.Format(State.RoundNumber), Strings.GoodBadOf.Format(a.Good, a.Bad, a.Total));
            if (rules.ItemsPerRound > 0 && (State.RoundNumber > 1 || rules.DealItemsOnFirstRound))
                foreach (var s in seats)
                    if (s.player.IsAlive) Popup(Above(s, .5f), Strings.ItemsDealt.Format(rules.ItemsPerRound), InfoColor);
            RefreshProps();
            yield return Wait(3);
            StartCoroutine(ClearSpeech(speech != null ? speech.text : null));
        }

        // Waiter announcements disappear automatically; remaining good/bad counts stay on the table info board.
        IEnumerator ClearSpeech(string text)
        {
            yield return Wait(4);
            if (speech != null && speech.text == text) speech.text = "";
        }

        IEnumerator SyncCandy()
        {
            var c = State.ActiveCandy;
            if (c == shownCandy) yield break;
            if (NetworkClient && c != null && shownCandy != null && c.Id == shownCandy.Id)
            {
                shownCandy = c;
                yield break;
            }
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
            var winner = State.Winner != null ? SeatOf(State.Winner) : null;
            Focus(winner, null);
            string title = winner == null ? Strings.NoWinner.Text : winner.IsHuman ? Strings.YouWin.Text : Strings.PlayerWins.Format(winner.name);
            // Without a menu, listener or bot setup, Back to menu restarts the match.
            bool menu = playBotsButton != null || MenuRequested != null || startButton != null;
            Say(Strings.GameOverSummary.Format(State.RoundNumber, State.TurnNumber),
                (VRTableInput.XRActive ? Strings.PlayAgainHintVr : Strings.PlayAgainHintDesktop).Text
                + (menu ? Strings.OrBackToMenu.Text : "") + (VRTableInput.XRActive ? " + trigger" : ""));
            ShowResult(title);
            SetButton(restartButton, !NetworkClient);
            SetButton(menuButton, menu);
            if (waiter != null) waiter.Play(CharacterReaction.Wave);
            if (winner != null)
            {
                PlayGameplaySound(GameplaySound.Win);
                for (int i = 0; i < 2; i++)
                {
                    winner.Play(CharacterReaction.Cheer);
                    if (winner.IsHuman) foreach (var s in seats) if (s != winner) s.Play(CharacterReaction.Shocked);
                    yield return Wait(2.3f);
                }
            }
            yield return Wait(2);
        }

        /// <summary>Show final standings and retire the round board and action effects immediately.</summary>
        void ShowResult(string title)
        {
            if (resultText == null) return;
            board.gameObject.SetActive(false);
            if (speech != null) speech.text = "";
            foreach (var entry in labels)
                if (entry.text != null && entry.text.name == "Popup") entry.text.gameObject.SetActive(false);
            var lines = new List<string> { $"<b>{title}</b>" };
            foreach (var (place, p) in FinalStandings.Rank(State.Players.OrderBy(p => p.Seat), p => p.IsAlive ? int.MaxValue : p.EliminatedOnTurn))
            {
                var s = SeatOf(p);
                string line = $"{place}. {s.name}  ·  " + (p.IsAlive ? $"<color=#bf8c0d><b>{Strings.Winner}</b></color>" : Strings.OutOnTurn.Format(p.EliminatedOnTurn));
                lines.Add(s.IsHuman ? $"<b>{line}</b>" : line);
            }
            resultText.text = string.Join("\n", lines);
            resultText.gameObject.SetActive(true);
        }

        void ResetVisuals()
        {
            gameplayAudio?.StopEffects();
            grabbedCandy = null;
            ResetNetworkVisuals();
            foreach (var t in temporary) if (t != null) Destroy(t);
            temporary.Clear();
            foreach (var prop in props) if (prop.selectable != null) Destroy(prop.selectable.gameObject);
            props.Clear();
            if (candyObject != null) Destroy(candyObject);
            candyObject = null;
            candySelectable = null;
            shownCandy = null;
            SetButton(restartButton, false);
            SetButton(menuButton, false);
            if (resultText != null) resultText.gameObject.SetActive(false);
            if (board != null) board.gameObject.SetActive(true);
            log.Clear();
            logDirty = true;
            if (speech != null) speech.text = "";
            if (engine != null) engine.Logged -= OnLogged;
            foreach (var s in allSeats)
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
            go.name = "Candy " + color;
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
            if (resultText != null && resultText.gameObject.activeSelf) yield break;
            var label = MakeText("Popup", position, .0085f, color);
            label.text = text;
            temporary.Add(label.gameObject);
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                if (label == null) yield break;
                PositionText(label, position + Vector3.up * (time * .08f));
                float fade = 1 - Mathf.InverseLerp(duration * .65f, duration, time);
                label.color = new Color(color.r, color.g, color.b, fade);
                yield return null;
            }
            labels.RemoveAll(entry => entry.text == label);
            Destroy(label.gameObject);
        }

        TextMesh MakeText(string name, Vector3 position, float size, Color color, bool card = false)
        {
            var text = WorldText.Create(name, transform, font, size, color);
            var go = text.gameObject;
            go.transform.position = position;
            var entry = new WorldTextLayout.Label(text, position);
            labels.Add(entry);
            if (card && cardMaterial != null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "Card";
                quad.transform.SetParent(go.transform, false);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = cardMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                entry.card = renderer;
            }
            return text;
        }

        TextMesh MakeSeatLabel(Seat seat)
        {
            var text = MakeText("Label " + seat.name, LabelPosition(seat), .0065f, Ink, true);
            AttachToHead(text, seat);
            return text;
        }

        void AttachToHead(TextMesh text, Seat seat)
        {
            foreach (var entry in labels)
                if (entry.text == text) { entry.head = seat.headBounds; return; }
        }

        /// <summary>Point above the bot's head. For humans: above their pad, visible from their own seat.</summary>
        Vector3 Above(Seat s, float height)
        {
            if (s.IsHuman) return (s.mat != null ? s.mat.position : tableCenter) + Vector3.up * (.18f + height * .4f);
            var head = s.Head;
            return (head != null ? head.position : s.character.transform.position + Vector3.up * 1.1f) + Vector3.up * height;
        }

        // Human label at the pad's left corner so it does not obscure the candy tray in front.
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

        // The engine creates players in seats order, so ConfigIndex directly identifies the seat (without name lookup).
        Seat SeatOf(PlayerState p) => seats[p.ConfigIndex];

        WaitForSeconds Wait(float seconds) => new WaitForSeconds(seconds / Mathf.Max(.05f, tempo));

        void Say(string text, string detail = null)
        {
            if (caption != null) caption.text = detail == null ? $"<b>{WorldText.Wrap(text, 54)}</b>"
                : $"<b>{WorldText.Wrap(text, 54)}</b>\n<size=36>{WorldText.Wrap(detail, 64)}</size>";
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
            if (NetworkListening && !NetworkActive && engine != null) StopGame();
            if (NetworkClient && humanChoice != null)
            {
                var choice = humanChoice;
                humanChoice = null;
                if (HumanCanAct)
                {
                    network.Submit(choice, human.player.Seat);
                    actionPending = true;
                    SetMode(HumanMode.None);
                }
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && !SpectatorInputLocked)
            {
                // R does not start a game from the main menu; select Play vs Bots. During onboarding, R skips it.
                if (keyboard.rKey.wasPressedThisFrame && InOnboarding) EndOnboarding();
                else if (keyboard.rKey.wasPressedThisFrame && !InMenu && !lobbyOpen) Restart();
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
            bool pointing = VoiceRoomActive || mode != HumanMode.None || settingUp || InMenu || lobbyOpen || InOnboarding || (restartButton != null && restartButton.interactable);
            if (pointing && mouse != null && camera != null && Cursor.lockState != CursorLockMode.Locked
                && (mode == HumanMode.None || !PanelRect.Contains(GuiMouse(mouse))))
            {
                var ray = camera.ScreenPointToRay(mouse.position.ReadValue());
                target = TableSelectable.Raycast(ray, 15, out _);
                if (SpectatorInputLocked && !IsVoiceSetting(target)) target = null;
            }
            if (target != hovered)
            {
                if (hovered != null) hovered.SetHover(false);
                hovered = target;
                // Bots do not grow on hover: CharacterAnimator uses their scale. A tooltip is enough for highlighting.
                if (hovered != null && !botTargets.ContainsValue(hovered)) hovered.SetHover(true);
            }
            if (hovered != null && mouse.leftButton.wasPressedThisFrame) hovered.Select();
        }

        void LateUpdate()
        {
            if (!ready) return;
            var camera = Camera.main;
            if (State != null)
            {
                UpdateBoardText();
                foreach (var s in seats)
                {
                    if (s.label == null) continue;
                    PositionText(s.label, LabelPosition(s));
                    UpdateSeatText(s);
                }
            }
            if (settingUp)
                foreach (var pair in difficultyButtons)
                    if (pair.Value.gameObject.activeSelf) PositionText(pair.Value.GetComponent<TextMesh>(), Above(pair.Key, .4f));
            if (candyLabel != null)
            {
                var c = State?.ActiveCandy;
                candyLabel.gameObject.SetActive(candyObject != null && c == shownCandy && c != null);
                if (candyLabel.gameObject.activeSelf)
                {
                    PositionText(candyLabel, candyObject.transform.position + Vector3.up * .09f);
                    candyLabel.text = (c.Revealed ? (c.IsGood ? "<color=#21884c><b>GOOD</b></color>" : "<color=#c42e29><b>BAD</b></color>") : "<b>?</b>")
                        + (State.CloverOnActive ? " <color=#bf8c0d><b>x2</b></color>" : "");
                }
            }
            if (camera != null) WorldTextLayout.Arrange(camera, labels, textObstacles, menuPage == MenuPage.Main ? mainMenuColumn : null);
        }

        void PositionText(TextMesh text, Vector3 position)
        {
            foreach (var entry in labels)
                if (entry.text == text) { entry.anchor = position; return; }
        }

        int DisplaySeconds => mode != HumanMode.None && (State?.Config.TurnTimeLimitSeconds ?? rules.TurnTimeLimitSeconds) > 0
            ? Mathf.CeilToInt(Mathf.Max(0, turnTimer)) : -1;

        void UpdateBoardText()
        {
            var state = (State.RoundNumber, State.CandiesInDispenser, State.GoodLeft, State.BadLeft, DisplaySeconds);
            if (boardText != null && boardState == state) return;
            boardState = state;
            string timer = boardState.seconds >= 0 ? $"\n<color=#c42e29>{Strings.BoardTimer.Format(boardState.seconds)}</color>" : "";
            string text = Strings.Board.Format(boardState.round, boardState.candies, boardState.good, boardState.bad) + timer;
            if (text == boardText) return;
            boardText = text;
            board.text = text;
        }

        void UpdateSeatText(Seat s)
        {
            var p = s.player;
            var state = (s.name, p != null, p != null && p.Eliminated, p?.Hp ?? 0, State.Config.MaxHp,
                p?.Items.Count ?? 0, p != null && (p.Paralyzed || s.Paralyzed), p != null && p.HasCupcake,
                p != null && State.Current == p && !State.IsOver);
            // Compare values before interpolation so idle frames do not create new strings.
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
            string status = (p.Paralyzed || s.Paralyzed ? $"\n<color=#8040a6>{Strings.Paralyzed}</color>" : "")
                + (p.HasCupcake ? $"\n<color=#2666a6>{Strings.Immune}</color>" : "");
            string turn = State.Current == p && !State.IsOver ? "> " : "";
            return $"<b>{turn}{s.name}</b>\n{Strings.SeatStatus.Format(p.Hp, State.Config.MaxHp, p.Items.Count)}{status}";
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
            if (State == null) return;
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
                    logText = $"<b>Bite or Betray</b>   R restart   [ ] tempo x{tempo:0.0#}\n"
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

        // Desktop fallback buttons: same actions as clicking table objects.
        void HumanPanel()
        {
            var me = human.player;
            // Fixed Rect avoids layout objects and LINQ copies on each OnGUI event while awaiting input.
            GUI.BeginGroup(PanelRect, GUI.skin.box);
            var state = (DisplaySeconds, me.Hp, State.Config.MaxHp, me.HasCupcake);
            if (panelText == null || panelState != state)
            {
                panelState = state;
                string timer = panelState.seconds >= 0 ? "  ·  " + Strings.PanelSeconds.Format(panelState.seconds) : "";
                panelText = $"<b>{Strings.PanelYourTurn}</b>{timer}\nHP {me.Hp}/{State.Config.MaxHp}" + (me.HasCupcake ? "  ·  " + Strings.Immune.Text : "");
            }
            GUI.Label(new Rect(8, 4, 304, 40), panelText, panelStyle);
            float y = 46;
            switch (mode)
            {
                case HumanMode.Action:
                    if (PanelButton(Strings.PanelEat.Text, ref y)) { OnCandySelected(); break; }
                    GUI.Label(new Rect(8, y, 304, 20), Strings.PanelFeedTo.Text);
                    y += 20;
                    int column = 0, count = 0;
                    foreach (var s in seats) if (!s.IsHuman && s.player.IsAlive) count++;
                    foreach (var s in seats)
                        if (!s.IsHuman && s.player.IsAlive && GUI.Button(new Rect(8 + column++ * 304f / count, y, 304f / count, 22), s.name))
                            OnBotSelected(s);
                    y += 24;
                    if (me.Items.Count > 0) { GUI.Label(new Rect(8, y, 304, 20), Strings.PanelUseItem.Text); y += 20; }
                    for (int i = 0; i < props.Count; i++)
                    {
                        var prop = props[i];
                        if (prop.seat == human && !prop.pocket && PanelButton(ItemInfo.Name(prop.type), ref y))
                        { OnPropSelected(prop); break; }
                    }
                    break;
                case HumanMode.Pocket:
                    GUI.Label(new Rect(8, y, 304, 20), Strings.PanelPocket.Text);
                    y += 20;
                    bool selected = false;
                    for (int i = 0; i < props.Count; i++)
                    {
                        var prop = props[i];
                        if (prop.pocket && PanelButton(ItemInfo.Name(prop.type), ref y))
                        { OnPropSelected(prop); selected = true; break; }
                    }
                    if (!selected && PanelButton(Strings.PanelDiscard.Text, ref y)) DiscardPocket();
                    break;
                case HumanMode.Grapple:
                    GUI.Label(new Rect(8, y, 304, 20), Strings.PanelGrapple.Text);
                    y += 20;
                    if (PanelButton(Strings.PanelCancel.Text, ref y)) { Cancel(); break; }
                    int targetIndex = 0;
                    for (int i = 0; i < props.Count; i++)
                    {
                        var prop = props[i];
                        if (!prop.selectable.interactable || prop.pocket || prop.seat == human) continue;
                        // At most five bots x six items, in two columns so every target fits in the panel.
                        if (GUI.Button(new Rect(8 + (targetIndex % 2) * 152, y + (targetIndex / 2) * 15, 152, 15), prop.panelLabel))
                        { OnPropSelected(prop); break; }
                        targetIndex++;
                    }
                    break;
            }
            GUI.EndGroup();
        }

        static bool PanelButton(string label, ref float y)
        {
            bool pressed = GUI.Button(new Rect(8, y, 304, 22), label);
            y += 24;
            return pressed;
        }
    }
}
