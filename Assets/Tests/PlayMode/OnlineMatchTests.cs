using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D;
using BiteOrBetray.Table3D.Net;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// Online tanpa headset (BOB-041): host dan klien NGO dalam satu proses Editor, tersambung lewat UnityTransport di
    /// 127.0.0.1. Host memakai scene gameplay sungguhan (director, bot, kursi manusia lokal lewat TableSelectable).
    /// Klien adalah NetworkManager kedua dengan NetworkTableSession tanpa scene: aksinya dipilih dari snapshot yang
    /// diterima lalu dikirim lewat Submit, jalur RPC yang sama dengan director klien. Meja host di-spawn dinamis dan
    /// klien memetakannya lewat prefab handler, karena dua salinan scene tidak bisa disinkronkan scene management dalam
    /// satu proses. Objek online bawaan scene (BOB-046) diganti milik tes; objek scene itu sendiri dicek OnlineSceneTests.
    /// Setiap revisi yang sama di kedua sisi dibandingkan: giliran, permen aktif, HP, status, inventory dan pemenang.
    /// </summary>
    public class OnlineMatchTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const uint TableHash = 0xB0B0041u;
        const ushort Port = 7791;
        CandyTableDirector director;
        Scene scene;
        NetworkManager host, client;
        NetworkTableSession hostTable, clientTable;
        readonly List<GameObject> created = new List<GameObject>();
        readonly HashSet<long> synced = new HashSet<long>();
        float oldTimeScale;
        bool oldRunInBackground;
        int clientConfig, attempt, rejected;
        long submitted, attemptRevision;

        GameEngine Engine => hostTable.Authority?.Engine;
        TableSelectable Candy => Read<TableSelectable>(director, "candySelectable");
        bool HostReady => Candy != null && Candy.interactable && Read<BotDecision>(director, "humanChoice") == null;
        bool ResultShown => Text("Hasil Akhir").gameObject.activeSelf;
        string ClientName => hostTable.Authority.Seats[clientConfig].Name;

        [UnitySetUp]
        public IEnumerator StartHostAndClient()
        {
            oldTimeScale = Time.timeScale;
            oldRunInBackground = Application.runInBackground;
            Time.timeScale = 1;
            Application.runInBackground = true;
            synced.Clear();
            clientConfig = -1;
            attempt = rejected = 0;
            submitted = attemptRevision = -1;
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "Director wajib berasal dari scene gameplay.");
            yield return Until(() => Read<bool>(director, "ready"), "menu selesai Start");
            // Objek online bawaan scene (BOB-046) diganti host/klien milik tes.
            foreach (var existing in Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(existing.gameObject);
            foreach (var existing in Object.FindObjectsByType<NetworkTableSession>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(existing.gameObject);
            yield return null;
            director.loop = false;
            director.recordTelemetry = false;
            director.tempo = 4;
            director.thinkSeconds = Vector2.zero;
            director.rules.TurnTimeLimitSeconds = 0;
            director.seed = 4101;
            var input = director.GetComponent<VRTableInput>();
            Assert.IsNotNull(input);
            Assert.IsFalse(input.IsXRActive, "Suite ini dijalankan Editor tanpa perangkat XR.");
            // Sama dengan OfflineMatchTests: polling perangkat dimatikan, komponen input dan coroutine director tetap asli.
            input.enabled = false;
            Invoke(input, "CreateVisuals");
            input.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            input.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Invoke(input, "OnInputFocusAcquired");
            yield return Until(() => input.HasFocus, "fokus awal fixture");

            host = CreateManager("BOB-041 Host");
            hostTable = CreateTable("BOB-041 Meja Host", director);
            Assert.IsTrue(host.StartHost(), "Host NGO gagal mulai di 127.0.0.1:" + Port);
            Assert.AreSame(host, NetworkManager.Singleton, "Director dan lobby memakai NetworkManager.Singleton sebagai host.");
            // Diaktifkan sesudah StartHost supaya tidak disapu sebagai objek scene yang belum terdaftar.
            hostTable.gameObject.SetActive(true);
            hostTable.GetComponent<NetworkObject>().Spawn();
            yield return Until(() => director.Network == hostTable, "meja host terikat ke director");

            // Director klien tidak pernah aktif (tanpa scene): NetworkTableSession klien hanya butuh referensinya.
            var clientDirector = Track(new GameObject("BOB-041 Director Klien"));
            clientDirector.SetActive(false);
            client = CreateManager("BOB-041 Klien");
            clientTable = CreateTable("BOB-041 Meja Klien", clientDirector.AddComponent<CandyTableDirector>());
            Assert.IsTrue(client.PrefabHandler.AddHandler(TableHash, new ExistingTable(clientTable.GetComponent<NetworkObject>())));
            clientTable.ActionRejected += OnRejected;
            Assert.IsTrue(client.StartClient(), "Klien NGO gagal mulai.");
            Assert.AreSame(host, NetworkManager.Singleton, "Klien kedua tidak boleh mengambil alih singleton host.");
            yield return Until(() => client.IsConnectedClient && clientTable.IsSpawned && host.ConnectedClientsIds.Count == 2,
                "klien tersambung dan menerima meja host");
            Assert.IsFalse(clientTable.IsServer);
            Time.timeScale = 20;
        }

        [UnityTearDown]
        public IEnumerator StopOnline()
        {
            Time.timeScale = oldTimeScale;
            if (director != null) director.ReturnToMenu();
            foreach (var manager in new[] { client, host })
                if (manager != null && manager.IsListening) manager.Shutdown();
            float deadline = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < deadline && new[] { client, host }.Any(m => m != null && (m.IsListening || m.ShutdownInProgress)))
                yield return null;
            foreach (var go in created) if (go != null) Object.Destroy(go);
            created.Clear();
            yield return null;
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-041 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Application.runInBackground = oldRunInBackground;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Host_and_client_play_with_bots_until_winner_with_matching_snapshots()
        {
            yield return StartOnlineTable();
            float deadline = Time.realtimeSinceStartup + 180;
            while (!ResultShown || clientTable.Latest.Revision != hostTable.Authority.Revision)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"Game online macet: {Engine.Phase}, giliran {Engine.TurnNumber}, "
                    + $"revisi host {hostTable.Authority.Revision}, klien {clientTable.Latest.Revision}");
                Step();
                yield return null;
            }
            AssertSynced("akhir game");
            Assert.IsTrue(Engine.IsOver);
            Assert.IsTrue(clientTable.Latest.IsOver);
            Assert.IsNotNull(Engine.Winner);
            Assert.AreEqual(1, Engine.AlivePlayers.Count());
            StringAssert.Contains(Strings.Winner.Text, Text("Hasil Akhir").text);
            Assert.GreaterOrEqual(synced.Count, 5, "Snapshot klien jarang menyusul revisi host.");
            Assert.Less(rejected, 50, "Aksi klien ditolak berulang-ulang oleh host.");
            Assert.IsTrue(Engine.LogLines.Any(line => IsActionOf(line, ClientName)), "Aksi klien harus dieksekusi host.");
            Assert.IsTrue(Engine.LogLines.Any(line => IsActionOf(line, hostTable.Authority.Seats[0].Name)), "Manusia di host ikut beraksi.");
            Assert.IsTrue(Engine.Players.Where(p => p.IsBot).Any(p => Engine.LogLines.Any(line => IsActionOf(line, p.Name))), "Bot ikut beraksi.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Client_disconnect_mid_game_seat_played_by_bot_until_winner()
        {
            yield return StartOnlineTable();
            // Putus tepat saat giliran klien terbuka (jalur BOB-037 yang paling rawan), setelah klien sempat beraksi.
            float deadline = Time.realtimeSinceStartup + 180;
            while (true)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Klien tidak pernah mendapat giliran kedua.");
                Assert.IsFalse(Engine.IsOver, "Game selesai sebelum klien sempat putus.");
                var s = clientTable.Latest;
                bool acted = Engine.LogLines.Any(line => IsActionOf(line, ClientName));
                bool turnOpen = s.InputOpen && !s.IsOver && s.Current.ConfigIndex == clientConfig && s.Revision != submitted;
                if ((acted && turnOpen) || s.Players.Single(p => p.ConfigIndex == clientConfig).Eliminated) break;
                Step();
                yield return null;
            }
            var player = Engine.Players.Single(p => p.ConfigIndex == clientConfig);
            bool onTurn = Engine.Current == player;
            int lines = Engine.LogLines.Count;
            client.Shutdown();
            yield return Until(() => hostTable.Authority.Seats[clientConfig].Controller == SeatController.Bot, "host mengganti kursi klien dengan bot");
            Assert.IsTrue(player.IsBot);
            Assert.AreEqual(SeatController.Bot, hostTable.Seats[clientConfig].Controller, "Main lagi juga memakai bot di kursi itu.");
            Assert.IsFalse(hostTable.IsOnlineSeat(clientConfig));
            deadline = Time.realtimeSinceStartup + 180;
            while (!ResultShown)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"Game macet setelah klien putus: {Engine.Phase}, giliran {Engine.TurnNumber}");
                if (HostReady) Select(Candy);
                yield return null;
            }
            Assert.IsTrue(Engine.IsOver);
            Assert.IsNotNull(Engine.Winner);
            Assert.IsFalse(client.IsListening);
            Assert.AreEqual(1, host.ConnectedClientsIds.Count, "Hanya host yang tersisa.");
            if (onTurn) Assert.IsTrue(Engine.LogLines.Skip(lines).Any(line => IsActionOf(line, player.Name)), "Bot melanjutkan giliran klien yang putus.");
        }

        IEnumerator StartOnlineTable()
        {
            var clients = host.ConnectedClientsIds.Where(id => id != host.LocalClientId).ToList();
            CollectionAssert.AreEqual(new[] { client.LocalClientId }, clients);
            // Susunan kursi yang sama dengan tombol Mulai di lobby.
            hostTable.StartTable(OnlineRoom.BuildSeats(clients, director.SeatCount, director.botNames, director.difficulties));
            yield return Until(() => Engine != null && clientTable.Latest != null && clientTable.Latest.GameId == hostTable.Authority.GameId,
                "snapshot game pertama sampai di klien");
            clientConfig = Array.FindIndex(clientTable.Latest.Seats, s => s.Controller == SeatController.Online && s.ClientId == client.LocalClientId);
            Assert.AreEqual(1, clientConfig, "Klien duduk di kursi kedua, sesudah host.");
            Assert.AreEqual(SeatController.Local, hostTable.Authority.Seats[0].Controller);
            Assert.AreEqual(director.SeatCount, Engine.Players.Count);
            Assert.AreEqual(director.SeatCount - 2, hostTable.Authority.Seats.Count(s => s.Controller == SeatController.Bot), "Kursi kosong diisi bot.");
            Assert.IsFalse(Engine.Players.Single(p => p.ConfigIndex == clientConfig).IsBot);
        }

        void Step()
        {
            if (HostReady) Select(Candy);
            var s = clientTable.Latest;
            var authority = hostTable.Authority;
            if (s != null && authority != null && s.GameId == authority.GameId && s.Revision == authority.Revision && synced.Add(s.Revision))
                AssertSynced("revisi " + s.Revision);
            PlayClient();
        }

        // Satu kiriman per revisi; kalau ditolak, percobaan berikutnya memakai pilihan yang lebih sederhana.
        void PlayClient()
        {
            var s = clientTable.Latest;
            if (s == null || s.IsOver || !s.InputOpen || s.Current.ConfigIndex != clientConfig || s.Revision == submitted) return;
            if (s.Revision != attemptRevision)
            {
                attemptRevision = s.Revision;
                attempt = 0;
            }
            submitted = s.Revision;
            clientTable.Submit(ClientDecision(s, attempt++), s.CurrentSeat);
        }

        void OnRejected(string message)
        {
            rejected++;
            submitted = -1;
        }

        // Klien memakai item dulu (inventory berubah karena aksi klien), lalu makan. Grappling butuh target, jadi dilewati.
        static BotDecision ClientDecision(TableSnapshot s, int attempt)
        {
            if (s.Phase == GamePhase.AwaitingPocketChoice)
            {
                int option = attempt == 0 ? IndexOf(s.PocketOptions, t => t != ItemType.GrapplingHook) : -1;
                return option >= 0 ? new BotDecision { Kind = BotActionKind.ChoosePocket, Index = option }
                    : new BotDecision { Kind = BotActionKind.DiscardPocket };
            }
            int item = attempt == 0 ? IndexOf(s.Current.Items, t => t != ItemType.GrapplingHook) : -1;
            return item >= 0 ? new BotDecision { Kind = BotActionKind.UseItem, Index = item } : new BotDecision { Kind = BotActionKind.Eat };
        }

        static int IndexOf(IReadOnlyList<ItemType> items, Func<ItemType, bool> match)
        {
            for (int i = 0; i < items.Count; i++) if (match(items[i])) return i;
            return -1;
        }

        static bool IsActionOf(string line, string name) =>
            line.StartsWith(name + " memakan") || line.StartsWith(name + " menyuapkan") || line.StartsWith(name + " memakai");

        void AssertSynced(string when)
        {
            var remote = clientTable.Latest;
            var authority = hostTable.Authority;
            var engine = authority.Engine;
            string at = when + ": ";
            Assert.AreEqual(authority.GameId, remote.GameId, at + "GameId");
            Assert.AreEqual(authority.Revision, remote.Revision, at + "revisi");
            Assert.AreEqual(engine.Phase, remote.Phase, at + "fase");
            Assert.AreEqual(engine.RoundNumber, remote.RoundNumber, at + "ronde");
            Assert.AreEqual(engine.TurnNumber, remote.TurnNumber, at + "giliran");
            Assert.AreEqual(engine.CurrentSeat, remote.CurrentSeat, at + "kursi giliran");
            Assert.AreEqual(engine.Winner?.Seat ?? -1, remote.WinnerSeat, at + "pemenang");
            Assert.AreEqual(engine.CandiesInDispenser, remote.CandiesInDispenser, at + "isi dispenser");
            Assert.AreEqual(engine.GoodLeft, remote.GoodLeft, at + "sisa good");
            Assert.AreEqual(engine.BadLeft, remote.BadLeft, at + "sisa bad");
            Assert.AreEqual(engine.ActiveCandy?.Id, remote.ActiveCandy?.Id, at + "permen aktif");
            Assert.AreEqual(engine.ActiveCandy?.Color, remote.ActiveCandy?.Color, at + "warna permen");
            Assert.AreEqual(engine.ActiveCandy?.Revealed, remote.ActiveCandy?.Revealed, at + "permen terungkap");
            Assert.AreEqual(engine.CloverOnActive, remote.CloverOnActive, at + "clover");
            CollectionAssert.AreEqual(engine.PocketOptions, remote.PocketOptions, at + "isi Little Pocket");
            Assert.AreEqual(authority.Seats.Count, remote.Seats.Length, at + "jumlah kursi");
            for (int i = 0; i < authority.Seats.Count; i++)
            {
                Assert.AreEqual(authority.Seats[i].Name, remote.Seats[i].Name, at + "nama kursi " + i);
                Assert.AreEqual(authority.Seats[i].Controller, remote.Seats[i].Controller, at + "pengendali kursi " + i);
                Assert.AreEqual(authority.Seats[i].ClientId, remote.Seats[i].ClientId, at + "klien kursi " + i);
            }
            Assert.AreEqual(engine.Players.Count, remote.Players.Count, at + "jumlah pemain");
            for (int i = 0; i < engine.Players.Count; i++)
            {
                var expected = engine.Players[i];
                var actual = remote.Players[i];
                string who = at + expected.Name + " ";
                Assert.AreEqual(expected.Name, actual.Name, who + "nama");
                Assert.AreEqual(expected.ConfigIndex, actual.ConfigIndex, who + "ConfigIndex");
                Assert.AreEqual(expected.IsBot, actual.IsBot, who + "bot");
                Assert.AreEqual(expected.D20Roll, actual.D20Roll, who + "d20");
                Assert.AreEqual(expected.Hp, actual.Hp, who + "HP");
                Assert.AreEqual(expected.Eliminated, actual.Eliminated, who + "KO");
                Assert.AreEqual(expected.Paralyzed, actual.Paralyzed, who + "lumpuh");
                Assert.AreEqual(expected.HasCupcake, actual.HasCupcake, who + "cupcake");
                CollectionAssert.AreEqual(expected.Items, actual.Items, who + "inventory");
            }
        }

        NetworkManager CreateManager(string name)
        {
            var go = Track(new GameObject(name));
            go.SetActive(false);
            var manager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            transport.ConnectionData = new UnityTransport.ConnectionAddressData { Address = "127.0.0.1", Port = Port, ServerListenAddress = "127.0.0.1" };
            // Klien tidak memuat scene host: scene sudah ada di proses yang sama, meja dipetakan lewat prefab handler.
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
            go.SetActive(true);
            return manager;
        }

        NetworkTableSession CreateTable(string name, CandyTableDirector owner)
        {
            var go = Track(new GameObject(name));
            go.SetActive(false);
            var table = go.AddComponent<NetworkTableSession>();
            table.director = owner;
            // Objek runtime tidak punya hash dari Editor; hash sama di host dan klien menggantikan prefab terdaftar.
            var hash = typeof(NetworkObject).GetField("GlobalObjectIdHash", Fields);
            Assert.IsNotNull(hash, "Field GlobalObjectIdHash NGO tidak ditemukan.");
            hash.SetValue(go.GetComponent<NetworkObject>(), TableHash);
            return table;
        }

        GameObject Track(GameObject go)
        {
            created.Add(go);
            return go;
        }

        // Klien tidak punya prefab meja: objek yang sudah disiapkan dipakai sebagai instance meja host.
        sealed class ExistingTable : INetworkPrefabInstanceHandler
        {
            readonly NetworkObject table;

            public ExistingTable(NetworkObject table) => this.table = table;

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            {
                table.gameObject.SetActive(true);
                return table;
            }

            public void Destroy(NetworkObject networkObject)
            {
                if (networkObject != null) Object.Destroy(networkObject.gameObject);
            }
        }

        TextMesh Text(string name) => director.GetComponentsInChildren<TextMesh>(true).Single(t => t.name == name);

        static void Select(TableSelectable target)
        {
            Assert.IsNotNull(target);
            Assert.IsTrue(target.gameObject.activeInHierarchy && target.isActiveAndEnabled && target.interactable, target.name + " tidak interaktif");
            target.Select();
        }

        IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, message);
                yield return null;
            }
        }

        static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    }
}
