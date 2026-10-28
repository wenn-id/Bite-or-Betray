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
    /// BOB-063, client side of the host leaving. Unlike OnlineMatchTests, the real gameplay scene is the online client
    /// here: its director binds to the client NetworkTableSession and shows the lobby wait, the client turn and the host
    /// candy exactly as on a joined headset. The host is a scene-less NetworkTableSession driven through its public host
    /// API (seats, BeginGame, Execute, Publish); an inactive director only supplies the candy transform for the visual
    /// stream. When the host leaves in the lobby or mid-match while the client holds the candy, the client must return to
    /// the main menu with the host-lost message, release the candy and still play an offline match with VR input.
    /// </summary>
    public class OnlineHostLeaveTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const uint TableHash = 0xB0B0063u;
        const ushort Port = 7793;
        const int ClientConfig = 1;
        CandyTableDirector director, hostDirector;
        VRTableInput input;
        Scene scene;
        NetworkManager host, client;
        NetworkTableSession hostTable, clientTable;
        readonly List<GameObject> created = new List<GameObject>();
        float oldTimeScale;
        bool oldRunInBackground;

        GameEngine Engine => Read<GameEngine>(director, "engine");
        TableSelectable Candy => Read<TableSelectable>(director, "candySelectable");
        bool HumanReady => Candy != null && Candy.interactable && Read<BotDecision>(director, "humanChoice") == null;

        [UnitySetUp]
        public IEnumerator StartHostAndClientScene()
        {
            oldTimeScale = Time.timeScale;
            oldRunInBackground = Application.runInBackground;
            Time.timeScale = 1;
            Application.runInBackground = true;
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "Director must come from the gameplay scene.");
            yield return Until(() => Read<bool>(director, "ready"), "menu finished Start");
            // The scene's built-in online objects (BOB-046) are replaced by the test's host/client, as in OnlineMatchTests.
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
            input = director.GetComponent<VRTableInput>();
            Assert.IsNotNull(input);
            Assert.IsFalse(input.IsXRActive, "This suite runs in the Editor without an XR device.");
            // Same as OfflineMatchTests: device polling disabled, input component and director coroutines stay real.
            input.enabled = false;
            Invoke(input, "CreateVisuals");
            input.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
            input.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
            Invoke(input, "OnInputFocusAcquired");
            yield return Until(() => input.HasFocus, "initial fixture focus");

            // Inactive: never runs Start/coroutines; the host table only reads its candy for the visual stream.
            var hostObject = Track(new GameObject("BOB-063 Host Director"));
            hostObject.SetActive(false);
            hostDirector = hostObject.AddComponent<CandyTableDirector>();
            hostDirector.artRoot = director.artRoot;
            host = CreateManager("BOB-063 Host");
            hostTable = CreateTable("BOB-063 Host Table", hostDirector);
            Assert.IsTrue(host.StartHost(), "NGO host failed to start on 127.0.0.1:" + Port);
            hostTable.gameObject.SetActive(true);
            hostTable.GetComponent<NetworkObject>().Spawn();

            client = CreateManager("BOB-063 Client");
            clientTable = CreateTable("BOB-063 Client Table", director);
            Assert.IsTrue(client.PrefabHandler.AddHandler(TableHash, new ExistingTable(clientTable.GetComponent<NetworkObject>())));
            Assert.IsTrue(client.StartClient(), "NGO client failed to start.");
            yield return Until(() => client.IsConnectedClient && clientTable.IsSpawned && director.Network == clientTable,
                "client scene bound to the host table");
            Assert.IsFalse(clientTable.IsServer);
            StringAssert.Contains(Strings.WaitingForHost.Text, Text("Narration").text, "Client scene waits in the room.");
        }

        [UnityTearDown]
        public IEnumerator StopOnline()
        {
            Time.timeScale = oldTimeScale;
            foreach (var manager in new[] { client, host })
                if (manager != null && manager.IsListening) manager.Shutdown();
            float deadline = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < deadline && new[] { client, host }.Any(m => m != null && (m.IsListening || m.ShutdownInProgress)))
                yield return null;
            if (director != null) director.ReturnToMenu();
            foreach (var go in created) if (go != null) Object.Destroy(go);
            created.Clear();
            yield return null;
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-063 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Application.runInBackground = oldRunInBackground;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Host_leaving_lobby_returns_client_to_menu_with_message_and_working_input()
        {
            yield return HostLeaves("lobby");
            yield return OfflineInputWorks("lobby");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator Host_leaving_mid_match_while_client_holds_candy_returns_client_to_menu_with_message()
        {
            var engine = StartHostGame();
            yield return Until(() => Read<TableSnapshot>(director, "remoteState")?.GameId == hostTable.Authority.GameId
                && Read<bool>(director, "remoteInputOpen"), "client scene shows its open turn");
            Assert.AreEqual(ClientConfig, Read<int>(director, "localConfigIndex"), "Client sits in the second host seat.");
            // The host candy on the tray, sent through the visual stream like a real host's SyncCandy.
            var color = engine.ActiveCandy.Color;
            var template = Read<IDictionary>(director, "candyTemplates")[color] as GameObject;
            var candy = Track(new GameObject("BOB-063 Host Candy"));
            candy.transform.SetPositionAndRotation(Read<Vector3>(director, "trayPosition"), template != null ? template.transform.rotation : Quaternion.identity);
            candy.transform.localScale = template != null ? template.transform.localScale : Vector3.one * .05f;
            Write(hostDirector, "candyObject", candy);
            Write(hostDirector, "shownCandy", engine.ActiveCandy);
            yield return Until(() => Candy != null && Candy.interactable, "client scene shows the host candy and may pick it up");
            yield return new WaitForSeconds(.3f); // received candy settles on its target pose
            Grab(Hand(0));
            Assert.IsTrue(input.IsHoldingCandy);
            yield return HostLeaves("mid-match");
            Assert.IsNull(Read<TableSnapshot>(director, "remoteState"), "client game state cleared");
            yield return OfflineInputWorks("mid-match");
        }

        // Seats as the lobby Start button builds them; the host plays Give until the client's turn, then opens its input.
        GameEngine StartHostGame()
        {
            var seats = OnlineRoom.BuildSeats(new[] { client.LocalClientId }, director.SeatCount, director.botNames, director.difficulties);
            Assert.AreEqual(SeatController.Online, seats[ClientConfig].Controller);
            // StartTable would start the (inactive) host director; set the seats it validates and copies directly.
            typeof(NetworkTableSession).GetProperty("Seats").SetValue(hostTable, seats.ToArray());
            var engine = hostTable.BeginGame(director.rules.Clone(), 4101);
            for (int turn = 0; engine.Current.ConfigIndex != ClientConfig; turn++)
            {
                Assert.Less(turn, 20, "Client turn never came.");
                var target = engine.Players.First(p => p.IsAlive && p != engine.Current && p.ConfigIndex != ClientConfig);
                Assert.IsTrue(hostTable.Execute(new BotDecision { Kind = BotActionKind.Give, TargetSeat = target.Seat }).Success);
                Assert.IsFalse(engine.IsOver);
            }
            Assert.AreEqual(GamePhase.AwaitingAction, engine.Phase);
            hostTable.SetInput(true, 0);
            hostTable.Publish(0);
            return engine;
        }

        IEnumerator HostLeaves(string moment)
        {
            host.Shutdown();
            // A client without a disconnect packet notices only after the transport timeout (30 s by default).
            yield return Until(() => director.Network == null, moment + ": client table detached after the host left", 45);
            yield return Until(() => !host.IsListening && !host.ShutdownInProgress && !client.IsListening && !client.ShutdownInProgress,
                moment + ": host and client stopped");
            Assert.IsFalse(input.IsHoldingCandy, moment + ": held candy released");
            Assert.IsNull(Candy, moment + ": host candy removed");
            Assert.IsNull(Engine, moment);
            var play = Button("Play vs Bots");
            Assert.IsTrue(play.gameObject.activeInHierarchy && play.interactable, moment + ": client back at the main menu");
            StringAssert.Contains(Strings.HostLost.Text, Text("Narration").text, moment + ": client sees why the online game ended");
        }

        // No stuck input after the online session: an offline match starts and the human eats via the VR grab path.
        IEnumerator OfflineInputWorks(string moment)
        {
            Time.timeScale = 20;
            Select(Button("Play vs Bots"));
            director.seed = HumanFirstSeed() - Read<int>(director, "gameIndex");
            bool onboardingDone = Read<bool>(director, "onboardingDone");
            Select(Button("Start"));
            if (!onboardingDone)
            {
                yield return Until(() => Button("Skip").interactable, moment + ": onboarding before the offline match");
                Select(Button("Skip"));
            }
            yield return Until(() => Engine != null && HumanReady, moment + ": offline human turn");
            int eats = Engine.Stats.EatSelf;
            Grab(Hand(0));
            EatAtMouth();
            yield return Until(() => Engine.Stats.EatSelf == eats + 1, moment + ": human eats after the online session");
            Assert.IsFalse(input.IsHoldingCandy);
        }

        int HumanFirstSeed()
        {
            var seats = Read<RoomSetup>(director, "room").BuildSeats(director.HumanName, director.botNames);
            for (int seed = 100; seed <= 1100; seed++)
            {
                var probe = new GameEngine(director.rules.Clone(), seed) { KeepLog = false };
                probe.Start(seats);
                if (!probe.Current.IsBot) return seed;
            }
            Assert.Fail("No seed found where the human goes first.");
            return 0;
        }

        object Hand(int index) => Read<Array>(input, "hands").GetValue(index);

        void Grab(object hand)
        {
            var pose = Read<Transform>(hand, "pose");
            if (pose == null)
            {
                pose = Track(new GameObject("BOB-063 Hand")).transform;
                Write(hand, "pose", pose);
            }
            var bounds = Candy.GetComponent<Collider>().bounds;
            pose.SetPositionAndRotation(bounds.center + Vector3.up * (bounds.extents.y + .1f), Quaternion.LookRotation(Vector3.down));
            Physics.SyncTransforms();
            Assert.AreSame(Candy, TableSelectable.Raycast(new Ray(pose.position, pose.forward), input.rayLength, out _), "Hand ray must hit the candy.");
            Sample(hand, true, false, false);
            Sample(hand, true, true, false);
            Assert.IsTrue(input.IsHoldingCandy);
        }

        void EatAtMouth()
        {
            var head = Camera.main.transform;
            Candy.transform.position = head.position - head.up * .08f + head.forward * .05f;
            var pose = Read<Transform>(Read<object>(input, "holder"), "pose");
            pose.position = Candy.transform.position - pose.forward * .06f;
            Write(input, "actedThisFrame", false);
            Invoke(input, "ProcessHeldCandy", true);
        }

        void Sample(object hand, bool tracked, bool trigger, bool grip)
        {
            Write(input, "actedThisFrame", false);
            Physics.SyncTransforms();
            Invoke(input, "ProcessHandInput", hand, tracked, trigger, grip, false);
        }

        NetworkManager CreateManager(string name)
        {
            var go = Track(new GameObject(name));
            go.SetActive(false);
            var manager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            transport.ConnectionData = new UnityTransport.ConnectionAddressData { Address = "127.0.0.1", Port = Port, ServerListenAddress = "127.0.0.1" };
            // One scene in one process: the client table is mapped via the prefab handler, not scene sync.
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

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Button " + label);
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
        static void Write(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    }
}
