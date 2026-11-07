using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BiteOrBetray.Core;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BiteOrBetray.Table3D.Net
{
    /// <summary>
    /// Online lobby (BOB-035) via Multiplayer Services Sessions: Lobby + Relay, and a session that starts NGO
    /// as host/client. Create Room: host gets a 6-digit code on the table panel, waits for clients, then Start (at least 2
    /// humans; empty seats filled by bots). Join Room: table numeric keypad. Default Lobby codes contain letters, so store a
    /// SHA-256 hash of the numeric code as a public indexed session property (OnlineRoom.CodeIndex) and find it with a query;
    /// the raw code itself is never stored queryably and is verified as the NGO connection payload in OnConnectionApproval.
    /// Needs NetworkManager + UnityTransport and NetworkTableSession in the scene (Bite or Betray > Install Online in Scene).
    /// </summary>
    public sealed class SessionRoomLobby : MonoBehaviour, IRoomLobby
    {
        const string CodeProperty = "code";

        [Tooltip("Empty = find NetworkTableSession in the scene.")]
        public NetworkTableSession table;

        enum Page { None, Host, Keypad, Joined }

        static readonly Key[] DigitKeys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        static readonly Key[] NumpadKeys = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

        readonly RoomCodeEntry entry = new RoomCodeEntry();
        readonly List<TableSelectable> digitButtons = new List<TableSelectable>();
        CandyTableDirector director;
        ISession session;
        string code;
        bool roomLocked; // M10: set once the session lock lands, before StartTable (see StartGame).
        Page page;
        bool busy;
        int shownPlayers = -1;
        TextMesh codeText, infoText;
        TableSelectable startButton, leaveButton, eraseButton, joinButton, cancelButton;

        NetworkTableSession Table => table != null ? table : table = FindFirstObjectByType<NetworkTableSession>();

        /// <summary>Packages, NetworkManager, online table in the scene, and linked UGS project (BOB-040). Sign in on Create/Join.</summary>
        public bool Ready => NetworkManager.Singleton != null && Table != null && !string.IsNullOrEmpty(Application.cloudProjectId);

        public void CreateRoom(CandyTableDirector director)
        {
            Bind(director);
            CreateAsync();
        }

        public void JoinRoom(CandyTableDirector director)
        {
            Bind(director);
            entry.Clear();
            ShowPage(Page.Keypad);
            director.ShowMessage(Strings.JoinRoom.Text, (VRTableInput.XRActive ? Strings.JoinHintVr : Strings.JoinHintDesktop).Text);
        }

        void Bind(CandyTableDirector target)
        {
            if (director == target) return;
            if (director != null) director.MenuRequested -= OnMenuRequested;
            director = target;
            director.MenuRequested += OnMenuRequested;
            BuildPanel();
        }

        // Returning to the menu from online results: leave the room so Play vs Bots returns to offline mode.
        void OnMenuRequested()
        {
            if (session != null) Leave(null, null);
        }

        void BuildPanel()
        {
            codeText = director.AddLobbyText("Room Code", new Vector2(0, .36f), .012f);
            infoText = director.AddLobbyText("Room Info", new Vector2(0, .17f));
            startButton = director.AddLobbyButton(Strings.Start, new Vector2(0, .08f), StartGame);
            leaveButton = director.AddLobbyButton(Strings.LeaveRoom, Vector2.zero, () => Leave(Strings.LeftRoom.Text, null));
            digitButtons.Clear();
            for (int digit = 0; digit <= 9; digit++)
            {
                // Telephone keypad layout: 1-2-3 at the top, 0 on the bottom row between Delete and Join.
                int index = digit == 0 ? 10 : digit - 1;
                var position = new Vector2((index % 3 - 1) * .08f, .25f - index / 3 * .07f);
                char c = (char)('0' + digit);
                var button = director.AddLobbyButton($" {c} ", position, () => Type(c));
                button.tooltip = Strings.DigitTip.Format(c);
                digitButtons.Add(button);
            }
            eraseButton = director.AddLobbyButton(Strings.Erase, new Vector2(-.18f, .04f), Erase);
            joinButton = director.AddLobbyButton(Strings.Join, new Vector2(.18f, .04f), Join);
            cancelButton = director.AddLobbyButton(Strings.Cancel, new Vector2(0, -.04f), () => director.OpenMainMenu());
        }

        void ShowPage(Page next)
        {
            page = next;
            shownPlayers = -1;
            director.LobbyOpen = next != Page.None;
            if (next == Page.None) return;
            bool keypad = next == Page.Keypad;
            codeText.gameObject.SetActive(true);
            infoText.gameObject.SetActive(!keypad);
            for (int digit = 0; digit < digitButtons.Count; digit++)
                CandyTableDirector.SetLobbyButton(digitButtons[digit], keypad, !busy, $" {digit} ");
            CandyTableDirector.SetLobbyButton(eraseButton, keypad, !busy, Strings.Erase.Text);
            CandyTableDirector.SetLobbyButton(joinButton, keypad, !busy && entry.Complete, Strings.Join.Text, (busy ? Strings.Searching : Strings.SixDigits).Text);
            CandyTableDirector.SetLobbyButton(cancelButton, keypad, !busy, Strings.Cancel.Text);
            CandyTableDirector.SetLobbyButton(leaveButton, !keypad, true, Strings.LeaveRoom.Text);
            CandyTableDirector.SetLobbyButton(startButton, next == Page.Host, false, Strings.Start.Text);
            codeText.text = keypad ? $"<size=32>{Strings.EnterCode}</size>\n<b>{entry.Display}</b>"
                : $"<size=32>{Strings.RoomCode}</size>\n<b>{OnlineRoom.FormatCode(code)}</b>";
            RefreshPlayers();
        }

        void Update()
        {
            if (director == null) return;
            // Main menu opened elsewhere (e.g. NGO session despawn): the director already hid the lobby panel.
            if (!director.LobbyOpen) page = Page.None;
            if (page == Page.Keypad && !busy) ReadKeyboard();
            RefreshPlayers();
        }

        void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            for (int digit = 0; digit <= 9; digit++)
                if (keyboard[DigitKeys[digit]].wasPressedThisFrame || keyboard[NumpadKeys[digit]].wasPressedThisFrame) Type((char)('0' + digit));
            if (keyboard.backspaceKey.wasPressedThisFrame) Erase();
            if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && entry.Complete) Join();
            if (keyboard.escapeKey.wasPressedThisFrame) director.OpenMainMenu();
        }

        // Player count from NGO (host) or session (client); enable Start once at least 2 humans are connected.
        void RefreshPlayers()
        {
            if (page != Page.Host && page != Page.Joined) return;
            var network = NetworkManager.Singleton;
            int players = page == Page.Host && network != null && network.IsHost ? network.ConnectedClientsIds.Count : session?.PlayerCount ?? 0;
            if (players == shownPlayers) return;
            shownPlayers = players;
            int seats = MaxPlayers;
            infoText.text = $"<b>{Strings.PlayersInRoom.Format(players, seats)}</b>\n<size=32>" + (page == Page.Host
                ? (seats > players ? Strings.EmptySeatsBots.Format(seats - players) : Strings.TableFull.Text) : Strings.WaitingForHostShort.Text) + "</size>";
            if (page != Page.Host) return;
            bool canStart = OnlineRoom.CanStart(players, seats);
            CandyTableDirector.SetLobbyButton(startButton, true, canStart && !busy, Strings.Start.Text, Strings.NeedPlayers.Format(OnlineRoom.MinHumans));
            director.ShowMessage(Strings.RoomTitle.Format(OnlineRoom.FormatCode(code)), (canStart ? Strings.PressStart : Strings.ShareCode).Text);
        }

        int MaxPlayers => Mathf.Clamp(director.SeatCount, OnlineRoom.MinHumans, OnlineRoom.MaxSeats);

        void Type(char digit)
        {
            if (busy || !entry.Add(digit)) return;
            ShowPage(Page.Keypad);
        }

        void Erase()
        {
            if (busy || !entry.Backspace()) return;
            ShowPage(Page.Keypad);
        }

        // ------------------------------------------------------------------ Sessions

        async void CreateAsync()
        {
            roomLocked = false;
            busy = true;
            ShowPage(Page.None);
            director.ShowMessage(Strings.CreatingRoom.Text, Strings.ConnectingServer.Text);
            try
            {
                await SignInAsync();
                string fresh = null;
                // Collision with another open room code is very rare; try a few times before giving up.
                for (int i = 0; i < 4 && fresh == null; i++)
                {
                    string candidate = OnlineRoom.NewCode(); // M10: CSPRNG code, not TickCount-seeded System.Random.
                    if ((await FindAsync(candidate)) == null) fresh = candidate;
                }
                if (fresh == null) throw new InvalidOperationException(Strings.NoFreeCode.Text);
                // M10 (round 2): the session MUST be public (IsPrivate = false) — private sessions
                // are never returned by QuerySessionsAsync, so with IsPrivate = true no client
                // could ever find the room. The code is not the security boundary: StringIndex1
                // holds only a SHA-256 hash of the code (OnlineRoom.CodeIndex), and the real join
                // gate is OnConnectionApproval, which verifies the raw code from the NGO payload.
                code = fresh;
                var options = new SessionOptions
                {
                    MaxPlayers = MaxPlayers,
                    IsPrivate = false,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        [CodeProperty] = new SessionProperty(OnlineRoom.CodeIndex(fresh), VisibilityPropertyOptions.Public, PropertyIndex.String1)
                    }
                }.WithRelayNetwork();
                if (NetworkManager.Singleton != null)
                {
                    // ConnectionApproval is part of the NGO NetworkConfig hash: it must be true on
                    // BOTH sides (here and in Join) or host and client configs mismatch. The scene
                    // does not serialize it (CandyParlour3D.unity stores no ConnectionApproval),
                    // so set it in code before the session starts NGO as host.
                    if (NetworkManager.Singleton.NetworkConfig != null)
                        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
                    NetworkManager.Singleton.ConnectionApprovalCallback = OnConnectionApproval;
                }
                var created = await MultiplayerService.Instance.CreateSessionAsync(options);
                if (!Attach(created)) return;
                busy = false;
                ShowPage(Page.Host);
            }
            catch (Exception e) { Fail(Strings.RoomCreateFailed.Text, e, Page.None); }
        }

        async void Join()
        {
            if (busy || !entry.Complete) return;
            busy = true;
            string wanted = entry.Code;
            ShowPage(Page.Keypad);
            director.ShowMessage(Strings.SearchingRoom.Format(OnlineRoom.FormatCode(wanted)));
            try
            {
                await SignInAsync();
                var info = await FindAsync(wanted);
                if (info == null)
                {
                    busy = false;
                    entry.Clear();
                    if (this == null || page != Page.Keypad || !director.LobbyOpen) return;
                    ShowPage(Page.Keypad);
                    director.ShowMessage(Strings.CodeNotFound.Format(OnlineRoom.FormatCode(wanted)), Strings.CodeNotFoundDetail.Text);
                    return;
                }
                director.ShowMessage(Strings.JoiningRoom.Format(OnlineRoom.FormatCode(wanted)), Strings.ConnectingHost.Text);
                // M10: the room code travels as the NGO connection payload so the host's approval
                // callback can verify the joiner. ConnectionApproval must be true on this side too
                // (same NetworkConfig hash as the host); Relay forwards ConnectionData as the
                // approval payload per the review's check against NGO 2.13.3 — still needs a live
                // Relay join test before shipping (see FIX-REPORT, round 2).
                var manager = NetworkManager.Singleton;
                if (manager != null && manager.NetworkConfig != null)
                {
                    manager.NetworkConfig.ConnectionApproval = true;
                    manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(wanted);
                }
                var joined = await MultiplayerService.Instance.JoinSessionByIdAsync(info.Id, new JoinSessionOptions());
                if (!Attach(joined)) return;
                code = wanted;
                busy = false;
                ShowPage(Page.Joined);
                director.ShowMessage(Strings.JoinedRoom.Format(OnlineRoom.FormatCode(code)), Strings.WaitingForHostShort.Text);
            }
            catch (Exception e) { Fail(Strings.RoomJoinFailed.Text, e, Page.Keypad); }
        }

        // Button callback, so async void is the correct shape here: awaiting the lock inside
        // the existing try/catch means a join cannot slip in between StartTable and the lock.
        async void StartGame()
        {
            var network = NetworkManager.Singleton;
            if (busy || page != Page.Host || network == null || !network.IsHost) return;
            try
            {
                var clients = network.ConnectedClientsIds.Where(id => id != network.LocalClientId).ToList();
                var seats = OnlineRoom.BuildSeats(clients, MaxPlayers, director.botNames, director.difficulties);
                await LockAsync(session);
                roomLocked = true; // M10: no further joins from here on; StartTable runs next.
                ShowPage(Page.None);
                Table.StartTable(seats);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                ShowPage(Page.Host);
                director.ShowMessage(Strings.StartFailed.Text, e.Message);
            }
        }

        // An active game accepts no new players (seats already assigned); just log lock failures.
        // M10: Task, not async void — StartGame awaits it so the lock lands before StartTable.
        static async Task LockAsync(ISession target)
        {
            try
            {
                if (target == null || !target.IsHost) return;
                var host = target.AsHost();
                host.IsLocked = true;
                await host.SavePropertiesAsync();
            }
            catch (Exception e) { Debug.LogWarning("[BoB] Cannot lock room: " + e.Message); }
        }

        static async Task<ISessionInfo> FindAsync(string wanted)
        {
            // Discovery key is the hash, never the raw code (see OnlineRoom.CodeIndex).
            var query = new QuerySessionsOptions
            {
                FilterOptions = new List<FilterOption> { new FilterOption(FilterField.StringIndex1, OnlineRoom.CodeIndex(wanted), FilterOperation.Equal) }
            };
            var results = await MultiplayerService.Instance.QuerySessionsAsync(query);
            return results.Sessions.FirstOrDefault();
        }

        static async Task SignInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
#if UNITY_EDITOR
                // Multiplayer Play Mode runs several editors with the same login cache; random profiles = different players.
                options.SetProfile("editor" + Guid.NewGuid().ToString("N").Substring(0, 8));
#endif
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // Use the new session only if the lobby is still alive and the player has not canceled; otherwise leave immediately.
        bool Attach(ISession joined)
        {
            if (this == null || director == null)
            {
                LeaveAsync(joined);
                return false;
            }
            session = joined;
            director.SetVoiceRoom(joined.Id);
            session.RemovedFromSession += OnSessionLost;
            session.Deleted += OnSessionLost;
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            return true;
        }

        void OnSessionLost()
        {
            if (session != null) Leave(Strings.RoomClosed.Text, Strings.RoomClosedDetail.Text);
        }

        void OnClientDisconnect(ulong clientId)
        {
            var network = NetworkManager.Singleton;
            if (session != null && network != null && !network.IsServer && clientId == network.LocalClientId)
                Leave(Strings.HostConnectionLost.Text, network.DisconnectReason);
        }

        /// <summary>Leave the room (host deletes it), shut down NGO, then return to the main menu with an optional message.</summary>
        void Leave(string title, string detail)
        {
            var left = session;
            Detach();
            LeaveAsync(left);
            busy = false;
            ShowPage(Page.None);
            var network = NetworkManager.Singleton;
            if (network != null && network.IsListening) network.Shutdown();
            if (director == null) return;
            director.OpenMainMenu();
            if (title != null) director.ShowMessage(title, string.IsNullOrEmpty(detail) ? null : detail);
        }

        /// <summary>M10: host kick (see NetworkTableSession.KickClient). No per-player UI exists
        /// yet — the lobby shows only a player count — so this is the host API for a future
        /// per-player list. Disconnecting reuses the bot-takeover path for the kicked seat.</summary>
        public void KickClient(ulong clientId)
        {
            if (session == null || !session.IsHost) return;
            Table?.KickClient(clientId);
        }

        // M10: host-side join gate. The session code is the discovery key (FindAsync); this is
        // the enforcement point: reject when the game already started, the table is full, or the
        // client's connection payload is not the room code.
        // NOTE (round 2): Relay forwarding of ConnectionData as request.Payload was checked by
        // the reviewer against NGO 2.13.3, but a live Relay join (approve + wrong-code reject)
        // still has to be tested in the Editor before shipping.
        void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = false;
            response.CreatePlayerObject = false; // BoB never spawns player objects; the in-scene table is the only NetworkObject.
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsHost) return;
            // NGO invokes this callback for the host itself when StartHost runs, with an empty
            // payload — the host must never reject itself.
            if (request.ClientNetworkId == manager.LocalClientId) { response.Approved = true; return; }
            if (roomLocked) return; // session locked: StartTable already ran or is running
            if (Table != null && Table.Authority != null) return; // game already started; seats are assigned
            if (manager.ConnectedClientsIds.Count >= MaxPlayers) return; // table full (host counts as one client)
            if (string.IsNullOrEmpty(code)) return;
            // The client proves it knows the room code via the connection payload (set in Join()).
            // An empty payload means the client did not come through Join() and is rejected.
            if (request.Payload == null || request.Payload.Length == 0) return;
            if (Encoding.UTF8.GetString(request.Payload) != code) return;
            response.Approved = true;
        }

        void Detach()
        {
            roomLocked = false;
            if (session != null && session.IsHost && NetworkManager.Singleton != null)
                NetworkManager.Singleton.ConnectionApprovalCallback = null;
            // Never leave a stale room code as the connection payload for the next session.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.NetworkConfig != null)
                NetworkManager.Singleton.NetworkConfig.ConnectionData = Array.Empty<byte>();
            if (director != null) director.SetVoiceRoom(null);
            if (session != null)
            {
                session.RemovedFromSession -= OnSessionLost;
                session.Deleted -= OnSessionLost;
            }
            session = null;
            code = null;
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }

        static async void LeaveAsync(ISession target)
        {
            if (target == null) return;
            try
            {
                if (target.IsHost) await target.AsHost().DeleteAsync();
                else await target.LeaveAsync();
            }
            catch (Exception e) { Debug.LogWarning("[BoB] Room leave failed: " + e.Message); }
        }

        // Network/service errors must not stop the table: show a message, then return to the keypad or main menu.
        void Fail(string title, Exception e, Page back)
        {
            Debug.LogWarning($"[BoB] {title}: {e}", this);
            busy = false;
            if (this == null || director == null) return;
            if (session != null) Leave(null, null);
            if (back == Page.Keypad && page == Page.Keypad && director.LobbyOpen)
            {
                entry.Clear();
                ShowPage(Page.Keypad);
            }
            else director.OpenMainMenu();
            director.ShowMessage(title, Describe(e));
        }

        static string Describe(Exception e)
        {
            if (e is SessionException failed) return $"{failed.Error}: {Short(failed.Message)}";
            if (e is AuthenticationException || e is ServicesInitializationException)
                return Strings.LoginFailed.Text;
            if (e is RequestFailedException request) return Strings.ServerRejected.Format(request.ErrorCode, Short(request.Message));
            return Short(e.Message);
        }

        static string Short(string message) => string.IsNullOrEmpty(message) ? Strings.UnknownError.Text
            : message.Length > 90 ? message.Substring(0, 90) + "..." : message;

        void OnDestroy()
        {
            if (director != null) director.MenuRequested -= OnMenuRequested;
            var left = session;
            Detach();
            LeaveAsync(left);
        }
    }
}
