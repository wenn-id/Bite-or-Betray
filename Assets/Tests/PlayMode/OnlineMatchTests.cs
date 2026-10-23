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
    /// Online without a headset (BOB-041): NGO host and client in one Editor process, connected via UnityTransport on
    /// 127.0.0.1. The host uses the real gameplay scene (director, bots, local human seat via TableSelectable).
    /// The client is a second NetworkManager with a scene-less NetworkTableSession: its actions are chosen from the received
    /// snapshot and sent via Submit, the same RPC path as the client director. The host table is spawned dynamically and
    /// the client maps it via a prefab handler, because scene management cannot sync two copies of the scene in
    /// one process. The scene's built-in online objects (BOB-046) are replaced by the test's own; those scene objects are checked by OnlineSceneTests.
    /// Every revision that matches on both sides is compared: turn, active candy, HP, status, inventory and winner.
    /// </summary>
    public partial class OnlineMatchTests
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
        bool ResultShown => Text("Final Results").gameObject.activeSelf;
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
            Assert.IsNotNull(director, "Director must come from the gameplay scene.");
            yield return Until(() => Read<bool>(director, "ready"), "menu finished Start");
            // The scene's built-in online objects (BOB-046) are replaced by the test's host/client.
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
            Assert.IsFalse(input.IsXRActive, "This suite runs in the Editor without an XR device.");
            // Same as OfflineMatchTests: device polling disabled, input component and director coroutines stay real.
            input.enabled = false;
            Invoke(input, "CreateVisuals");
            input.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            input.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Invoke(input, "OnInputFocusAcquired");
            yield return Until(() => input.HasFocus, "initial fixture focus");

            host = CreateManager("BOB-041 Host");
            hostTable = CreateTable("BOB-041 Host Table", director);
            Assert.IsTrue(host.StartHost(), "NGO host failed to start on 127.0.0.1:" + Port);
            Assert.AreSame(host, NetworkManager.Singleton, "Director and lobby use NetworkManager.Singleton as host.");
            // Activated after StartHost so it is not swept up as an unregistered scene object.
            hostTable.gameObject.SetActive(true);
            hostTable.GetComponent<NetworkObject>().Spawn();
            yield return Until(() => director.Network == hostTable, "host table bound to director");

            // The client director is never active (no scene): the client NetworkTableSession only needs its reference.
            var clientDirector = Track(new GameObject("BOB-041 Client Director"));
            clientDirector.SetActive(false);
            client = CreateManager("BOB-041 Client");
            clientTable = CreateTable("BOB-041 Client Table", clientDirector.AddComponent<CandyTableDirector>());
            Assert.IsTrue(client.PrefabHandler.AddHandler(TableHash, new ExistingTable(clientTable.GetComponent<NetworkObject>())));
            clientTable.ActionRejected += OnRejected;
            Assert.IsTrue(client.StartClient(), "NGO client failed to start.");
            Assert.AreSame(host, NetworkManager.Singleton, "The second client must not take over the host singleton.");
            yield return Until(() => client.IsConnectedClient && clientTable.IsSpawned && host.ConnectedClientsIds.Count == 2,
                "client connected and received the host table");
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
                Assert.Less(Time.realtimeSinceStartup, deadline, $"Online game stuck: {Engine.Phase}, turn {Engine.TurnNumber}, "
                    + $"host revision {hostTable.Authority.Revision}, client {clientTable.Latest.Revision}");
                Step();
                yield return null;
            }
            AssertSynced("end of game");
            Assert.IsTrue(Engine.IsOver);
            Assert.IsTrue(clientTable.Latest.IsOver);
            Assert.IsNotNull(Engine.Winner);
            Assert.AreEqual(1, Engine.AlivePlayers.Count());
            StringAssert.Contains(Strings.Winner.Text, Text("Final Results").text);
            Assert.GreaterOrEqual(synced.Count, 5, "Client snapshots rarely caught up with the host revision.");
            Assert.Less(rejected, 50, "Client actions repeatedly rejected by the host.");
            Assert.IsTrue(Engine.LogLines.Any(line => IsActionOf(line, ClientName)), "Client actions must be executed by the host.");
            Assert.IsTrue(Engine.LogLines.Any(line => IsActionOf(line, hostTable.Authority.Seats[0].Name)), "The human on the host also acts.");
            Assert.IsTrue(Engine.Players.Where(p => p.IsBot).Any(p => Engine.LogLines.Any(line => IsActionOf(line, p.Name))), "Bots also act.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Client_disconnect_mid_game_seat_played_by_bot_until_winner()
        {
            yield return StartOnlineTable();
            // Disconnect exactly when the client's turn is open (the most fragile BOB-037 path), after the client has acted.
            float deadline = Time.realtimeSinceStartup + 180;
            while (true)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Client never got a second turn.");
                Assert.IsFalse(Engine.IsOver, "Game ended before the client could disconnect.");
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
            yield return Until(() => hostTable.Authority.Seats[clientConfig].Controller == SeatController.Bot, "host replaces the client seat with a bot");
            Assert.IsTrue(player.IsBot);
            Assert.AreEqual(SeatController.Bot, hostTable.Seats[clientConfig].Controller, "Play again also uses a bot in that seat.");
            Assert.IsFalse(hostTable.IsOnlineSeat(clientConfig));
            deadline = Time.realtimeSinceStartup + 180;
            while (!ResultShown)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"Game stuck after client disconnect: {Engine.Phase}, turn {Engine.TurnNumber}");
                if (HostReady) Select(Candy);
                yield return null;
            }
            Assert.IsTrue(Engine.IsOver);
            Assert.IsNotNull(Engine.Winner);
            Assert.IsFalse(client.IsListening);
            Assert.AreEqual(1, host.ConnectedClientsIds.Count, "Only the host remains.");
            if (onTurn) Assert.IsTrue(Engine.LogLines.Skip(lines).Any(line => IsActionOf(line, player.Name)), "Bot continues the disconnected client's turn.");
        }

        IEnumerator StartOnlineTable()
        {
            var clients = host.ConnectedClientsIds.Where(id => id != host.LocalClientId).ToList();
            CollectionAssert.AreEqual(new[] { client.LocalClientId }, clients);
            // Same seat layout as the Start button in the lobby.
            hostTable.StartTable(OnlineRoom.BuildSeats(clients, director.SeatCount, director.botNames, director.difficulties));
            yield return Until(() => Engine != null && clientTable.Latest != null && clientTable.Latest.GameId == hostTable.Authority.GameId,
                "first game snapshot reached the client");
            clientConfig = Array.FindIndex(clientTable.Latest.Seats, s => s.Controller == SeatController.Online && s.ClientId == client.LocalClientId);
            Assert.AreEqual(1, clientConfig, "Client sits in the second seat, after the host.");
            Assert.AreEqual(SeatController.Local, hostTable.Authority.Seats[0].Controller);
            Assert.AreEqual(director.SeatCount, Engine.Players.Count);
            Assert.AreEqual(director.SeatCount - 2, hostTable.Authority.Seats.Count(s => s.Controller == SeatController.Bot), "Empty seats filled by bots.");
            Assert.IsFalse(Engine.Players.Single(p => p.ConfigIndex == clientConfig).IsBot);
        }

        void Step()
        {
            if (HostReady) Select(Candy);
            var s = clientTable.Latest;
            var authority = hostTable.Authority;
            if (s != null && authority != null && s.GameId == authority.GameId && s.Revision == authority.Revision && synced.Add(s.Revision))
                AssertSynced("revision " + s.Revision);
            PlayClient();
        }

        // One submission per revision; if rejected, the next attempt uses a simpler choice.
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

        // The client uses items first (inventory changes due to client actions), then eats. Grappling needs a target, so it is skipped.
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
            line.StartsWith(name + " eats") || line.StartsWith(name + " feeds") || line.StartsWith(name + " uses");

        void AssertSynced(string when)
        {
            var remote = clientTable.Latest;
            var authority = hostTable.Authority;
            var engine = authority.Engine;
            string at = when + ": ";
            Assert.AreEqual(authority.GameId, remote.GameId, at + "GameId");
            Assert.AreEqual(authority.Revision, remote.Revision, at + "revision");
            Assert.AreEqual(engine.Phase, remote.Phase, at + "phase");
            Assert.AreEqual(engine.RoundNumber, remote.RoundNumber, at + "round");
            Assert.AreEqual(engine.TurnNumber, remote.TurnNumber, at + "turn");
            Assert.AreEqual(engine.CurrentSeat, remote.CurrentSeat, at + "turn seat");
            Assert.AreEqual(engine.Winner?.Seat ?? -1, remote.WinnerSeat, at + "winner");
            Assert.AreEqual(engine.CandiesInDispenser, remote.CandiesInDispenser, at + "dispenser contents");
            Assert.AreEqual(engine.GoodLeft, remote.GoodLeft, at + "good left");
            Assert.AreEqual(engine.BadLeft, remote.BadLeft, at + "bad left");
            Assert.AreEqual(engine.ActiveCandy?.Id, remote.ActiveCandy?.Id, at + "active candy");
            Assert.AreEqual(engine.ActiveCandy?.Color, remote.ActiveCandy?.Color, at + "candy color");
            Assert.AreEqual(engine.ActiveCandy?.Revealed, remote.ActiveCandy?.Revealed, at + "candy revealed");
            Assert.AreEqual(engine.CloverOnActive, remote.CloverOnActive, at + "clover");
            CollectionAssert.AreEqual(engine.PocketOptions, remote.PocketOptions, at + "Little Pocket contents");
            Assert.AreEqual(authority.Seats.Count, remote.Seats.Length, at + "seat count");
            for (int i = 0; i < authority.Seats.Count; i++)
            {
                Assert.AreEqual(authority.Seats[i].Name, remote.Seats[i].Name, at + "seat name " + i);
                Assert.AreEqual(authority.Seats[i].Controller, remote.Seats[i].Controller, at + "seat controller " + i);
                Assert.AreEqual(authority.Seats[i].ClientId, remote.Seats[i].ClientId, at + "seat client " + i);
            }
            Assert.AreEqual(engine.Players.Count, remote.Players.Count, at + "player count");
            for (int i = 0; i < engine.Players.Count; i++)
            {
                var expected = engine.Players[i];
                var actual = remote.Players[i];
                string who = at + expected.Name + " ";
                Assert.AreEqual(expected.Name, actual.Name, who + "name");
                Assert.AreEqual(expected.ConfigIndex, actual.ConfigIndex, who + "ConfigIndex");
                Assert.AreEqual(expected.IsBot, actual.IsBot, who + "bot");
                Assert.AreEqual(expected.D20Roll, actual.D20Roll, who + "d20");
                Assert.AreEqual(expected.Hp, actual.Hp, who + "HP");
                Assert.AreEqual(expected.Eliminated, actual.Eliminated, who + "KO");
                Assert.AreEqual(expected.Paralyzed, actual.Paralyzed, who + "paralyzed");
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
            // The client does not load the host scene: the scene already exists in the same process, the table is mapped via the prefab handler.
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
            // Runtime objects have no hash from the Editor; the same hash on host and client stands in for a registered prefab.
            var hash = typeof(NetworkObject).GetField("GlobalObjectIdHash", Fields);
            Assert.IsNotNull(hash, "NGO field GlobalObjectIdHash not found.");
            hash.SetValue(go.GetComponent<NetworkObject>(), TableHash);
            return table;
        }

        GameObject Track(GameObject go)
        {
            created.Add(go);
            return go;
        }

        // The client has no table prefab: the prepared object is used as the host table instance.
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
            Assert.IsTrue(target.gameObject.activeInHierarchy && target.isActiveAndEnabled && target.interactable, target.name + " not interactable");
            target.Select();
        }

        IEnumerator Until(Func<bool> condition, string message, float seconds = 20)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
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
