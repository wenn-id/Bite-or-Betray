using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BiteOrBetray.Core;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = System.Random;

namespace BiteOrBetray.Table3D.Net
{
    /// <summary>
    /// Lobby online (BOB-035) lewat Sessions dari Multiplayer Services: Lobby + Relay, dan session yang memulai NGO
    /// sebagai host/klien. Buat Room: host mendapat kode 6 angka di panel meja, menunggu klien, lalu Mulai (minimal 2
    /// manusia; kursi kosong diisi bot). Gabung Room: keypad angka di meja. Kode Lobby bawaan berhuruf, jadi kode angka
    /// disimpan sebagai properti session publik terindeks dan dicari dengan query.
    /// Butuh NetworkManager + UnityTransport dan NetworkTableSession di scene (Bite or Betray > Pasang Online di Scene).
    /// </summary>
    public sealed class SessionRoomLobby : MonoBehaviour, IRoomLobby
    {
        const string CodeProperty = "code";

        [Tooltip("Kosong = cari NetworkTableSession di scene.")]
        public NetworkTableSession table;

        enum Page { None, Host, Keypad, Joined }

        static readonly Key[] DigitKeys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        static readonly Key[] NumpadKeys = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

        readonly RoomCodeEntry entry = new RoomCodeEntry();
        readonly List<TableSelectable> digitButtons = new List<TableSelectable>();
        readonly Random rng = new Random();
        CandyTableDirector director;
        ISession session;
        string code;
        Page page;
        bool busy;
        int shownPlayers = -1;
        TextMesh codeText, infoText;
        TableSelectable startButton, leaveButton, eraseButton, joinButton, cancelButton;

        NetworkTableSession Table => table != null ? table : table = FindFirstObjectByType<NetworkTableSession>();

        /// <summary>Package, NetworkManager, meja online di scene, dan project UGS ter-link (BOB-040). Login saat Buat/Gabung.</summary>
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

        // Kembali ke menu dari layar akhir game online: keluar dari room supaya Main vs Bot kembali offline.
        void OnMenuRequested()
        {
            if (session != null) Leave(null, null);
        }

        void BuildPanel()
        {
            codeText = director.AddLobbyText("Kode Room", new Vector2(0, .36f), .012f);
            infoText = director.AddLobbyText("Info Room", new Vector2(0, .17f));
            startButton = director.AddLobbyButton(Strings.Start, new Vector2(0, .08f), StartGame);
            leaveButton = director.AddLobbyButton(Strings.LeaveRoom, Vector2.zero, () => Leave(Strings.LeftRoom.Text, null));
            digitButtons.Clear();
            for (int digit = 0; digit <= 9; digit++)
            {
                // Susunan keypad telepon: 1-2-3 di atas, 0 di baris bawah di antara Hapus dan Gabung.
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
            // Menu utama dibuka dari tempat lain (mis. sesi NGO despawn): panel lobby sudah disembunyikan director.
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

        // Jumlah pemain dari NGO (host) atau session (klien); tombol Mulai hidup kalau minimal 2 manusia tersambung.
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
            busy = true;
            ShowPage(Page.None);
            director.ShowMessage(Strings.CreatingRoom.Text, Strings.ConnectingServer.Text);
            try
            {
                await SignInAsync();
                string fresh = null;
                // Kode bentrok dengan room lain yang masih buka sangat jarang; coba beberapa kali sebelum menyerah.
                for (int i = 0; i < 4 && fresh == null; i++)
                {
                    string candidate = OnlineRoom.NewCode(rng);
                    if ((await FindAsync(candidate)) == null) fresh = candidate;
                }
                if (fresh == null) throw new InvalidOperationException(Strings.NoFreeCode.Text);
                var options = new SessionOptions
                {
                    MaxPlayers = MaxPlayers,
                    IsPrivate = false,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        [CodeProperty] = new SessionProperty(fresh, VisibilityPropertyOptions.Public, PropertyIndex.String1)
                    }
                }.WithRelayNetwork();
                var created = await MultiplayerService.Instance.CreateSessionAsync(options);
                if (!Attach(created)) return;
                code = fresh;
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
                var joined = await MultiplayerService.Instance.JoinSessionByIdAsync(info.Id, new JoinSessionOptions());
                if (!Attach(joined)) return;
                code = wanted;
                busy = false;
                ShowPage(Page.Joined);
                director.ShowMessage(Strings.JoinedRoom.Format(OnlineRoom.FormatCode(code)), Strings.WaitingForHostShort.Text);
            }
            catch (Exception e) { Fail(Strings.RoomJoinFailed.Text, e, Page.Keypad); }
        }

        void StartGame()
        {
            var network = NetworkManager.Singleton;
            if (busy || page != Page.Host || network == null || !network.IsHost) return;
            try
            {
                var clients = network.ConnectedClientsIds.Where(id => id != network.LocalClientId).ToList();
                var seats = OnlineRoom.BuildSeats(clients, MaxPlayers, director.botNames, director.difficulties);
                LockAsync(session);
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

        // Room yang sudah main tidak menerima pemain baru (kursi sudah dibagi); gagal kunci cukup dicatat.
        static async void LockAsync(ISession target)
        {
            try
            {
                if (target == null || !target.IsHost) return;
                var host = target.AsHost();
                host.IsLocked = true;
                await host.SavePropertiesAsync();
            }
            catch (Exception e) { Debug.LogWarning("[BoB] Room tidak bisa dikunci: " + e.Message); }
        }

        static async Task<ISessionInfo> FindAsync(string wanted)
        {
            var query = new QuerySessionsOptions
            {
                FilterOptions = new List<FilterOption> { new FilterOption(FilterField.StringIndex1, wanted, FilterOperation.Equal) }
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
                // Multiplayer Play Mode menjalankan beberapa editor dengan cache login yang sama; profil acak = pemain beda.
                options.SetProfile("editor" + Guid.NewGuid().ToString("N").Substring(0, 8));
#endif
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // Session baru dipakai hanya kalau lobby masih hidup dan pemain belum membatalkan; selain itu langsung ditinggal.
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

        /// <summary>Tinggalkan room (host menghapusnya), matikan NGO, lalu kembali ke menu utama dengan pesan opsional.</summary>
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

        void Detach()
        {
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
            catch (Exception e) { Debug.LogWarning("[BoB] Keluar room gagal: " + e.Message); }
        }

        // Error jaringan/layanan tidak boleh menghentikan meja: tampil sebagai pesan, lalu ke keypad atau menu utama.
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
