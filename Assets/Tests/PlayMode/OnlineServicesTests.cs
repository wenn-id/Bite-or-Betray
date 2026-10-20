using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D;
using BiteOrBetray.Table3D.Net;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// Real Multiplayer Services (BOB-041; UGS project linked in BOB-040). The Create Room button in the table menu uses the
    /// real SessionRoomLobby: anonymous sign-in, a Lobby + Relay session that starts NGO as host, a 6-digit code on the panel.
    /// That code is looked up with the same query as Join Room, Leave room deletes the session, then the closed code
    /// shows up as a message. Vivox joins the same room channel and leaves together with the room.
    /// The scene's built-in online objects (BOB-046) are replaced by an identical test-owned setup so the transport can be inspected directly.
    /// One process has only one UGS identity: a second player joining via Relay and hearing each other's voice cannot be
    /// proven here yet (host+client gameplay is proven by OnlineMatchTests via local UnityTransport).
    /// Without a network the tests are skipped (Assert.Ignore).
    /// </summary>
    public class OnlineServicesTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        CandyTableDirector director;
        Scene scene;
        NetworkManager manager;
        UnityTransport transport;
        SessionRoomLobby lobby;
        readonly List<GameObject> created = new List<GameObject>();
        bool oldRunInBackground;

        string Caption => Read<TextMesh>(director, "caption").text;
        string Code => Read<string>(lobby, "code");
        ISession Session => Read<ISession>(lobby, "session");

        [UnitySetUp]
        public IEnumerator LoadTableWithLobby()
        {
            oldRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "Director must come from the gameplay scene.");
            yield return Until(() => Read<bool>(director, "ready"), "menu finished Start");
            foreach (var existing in Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(existing.gameObject);
            foreach (var existing in Object.FindObjectsByType<NetworkTableSession>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(existing.gameObject);
            yield return null;
            director.recordTelemetry = false;

            // Same setup as OnlineSceneSetup: NetworkManager + UnityTransport + lobby, scene management enabled.
            var network = new GameObject("BOB-041 Network");
            created.Add(network);
            network.SetActive(false);
            manager = network.AddComponent<NetworkManager>();
            transport = network.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = true };
            // The online table stays inactive: this test does not start a game, and a runtime object is not an NGO scene object.
            var tableObject = new GameObject("BOB-041 Table");
            created.Add(tableObject);
            tableObject.SetActive(false);
            var table = tableObject.AddComponent<NetworkTableSession>();
            table.director = director;
            lobby = network.AddComponent<SessionRoomLobby>();
            lobby.table = table;
            network.SetActive(true);
            director.Lobby = lobby;
            director.OpenMainMenu();
        }

        [UnityTearDown]
        public IEnumerator LeaveAndUnload()
        {
            if (lobby != null && Session != null) Invoke(lobby, "Leave", null, null);
            if (manager != null && manager.IsListening) manager.Shutdown();
            float deadline = Time.realtimeSinceStartup + 10;
            while (manager != null && (manager.IsListening || manager.ShutdownInProgress) && Time.realtimeSinceStartup < deadline) yield return null;
            // Session deletion and Vivox logout run async; give them time before the scene is unloaded.
            yield return new WaitForSecondsRealtime(2);
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

        [UnityTest, Timeout(240000)]
        public IEnumerator Create_room_with_relay_find_by_code_and_leave()
        {
            RequireInternet();
            yield return CreateRoom();
            string code = Code;
            var session = Session;
            Assert.IsTrue(OnlineRoom.IsValidCode(code), "Room code must be 6 digits: " + code);
            Assert.IsTrue(AuthenticationService.Instance.IsSignedIn, "UGS anonymous sign-in.");
            Assert.IsNotEmpty(AuthenticationService.Instance.PlayerId);
            Assert.IsTrue(session.IsHost);
            Assert.AreEqual(1, session.PlayerCount);
            Assert.AreEqual(code, session.Properties["code"].Value, "The numeric code is stored in a public session property.");
            Assert.AreSame(manager, NetworkManager.Singleton);
            Assert.IsTrue(manager.IsHost && manager.IsListening, "Session starts NGO as host.");
            Assert.AreEqual(UnityTransport.ProtocolType.RelayUnityTransport, transport.Protocol, "NGO host runs via Relay.");
            var codeText = Read<TextMesh>(lobby, "codeText");
            Assert.IsTrue(codeText.gameObject.activeInHierarchy, "Room code is shown on the table panel.");
            StringAssert.Contains(OnlineRoom.FormatCode(code), codeText.text);
            StringAssert.Contains(Strings.PlayersInRoom.Format(1, director.SeatCount), Read<TextMesh>(lobby, "infoText").text);
            Assert.IsFalse(Read<TableSelectable>(lobby, "startButton").interactable, $"Start needs at least {OnlineRoom.MinHumans} players.");

            // Same query as Join Room. The pause respects the per-player Lobby query rate limit.
            ISessionInfo found = null;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Await(Find(code), result => found = result, "find session by code");
            Assert.IsNotNull(found, "Session not found with code " + code);
            Assert.AreEqual(session.Id, found.Id);

            Select(Read<TableSelectable>(lobby, "leaveButton"));
            Assert.IsNull(Session);
            Assert.IsTrue(Button("Create Room").gameObject.activeInHierarchy, "Leaving the room returns to the main menu.");
            yield return Until(() => !manager.IsListening, "NGO stops after leaving the room");
            // The host leaving deletes the session; Lobby needs a few seconds until the query no longer finds it.
            float deadline = Time.realtimeSinceStartup + 40;
            do
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Session can still be found after the host left the room.");
                yield return new WaitForSecondsRealtime(2);
                yield return Await(Find(code), result => found = result, "find session after leaving");
            } while (found != null);

            // A closed code is rejected with a message at the table, without a crash and without NGO starting.
            yield return new WaitForSecondsRealtime(1.5f);
            Select(Button("Join Room"));
            var digits = Read<IList>(lobby, "digitButtons");
            foreach (char c in code) Select((TableSelectable)digits[c - '0']);
            Select(Read<TableSelectable>(lobby, "joinButton"));
            yield return Until(() => Caption.Contains(Strings.CodeNotFound.Format(OnlineRoom.FormatCode(code))), "code not found message", 30);
            StringAssert.Contains(OnlineRoom.FormatCode(code), Caption);
            Assert.IsNull(Session);
            Assert.IsFalse(manager.IsListening);
            Select(Read<TableSelectable>(lobby, "cancelButton"));
            Assert.IsTrue(Button("Create Room").gameObject.activeInHierarchy);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Voice_joins_room_channel_and_leaves_with_room()
        {
            RequireInternet();
            yield return CreateRoom();
            string channel = "bob-room-" + Session.Id;
            var voice = Read<object>(director, "voice");
            Assert.IsTrue(director.VoiceRoomActive);
            yield return Until(() => Read<string>(voice, "activeChannel") == channel || Caption.Contains(Strings.VoiceUnavailable.Text), "join Vivox channel", 60);
            Assert.AreEqual(channel, Read<string>(voice, "activeChannel"), "Vivox failed: " + voice.GetType().GetProperty("LastError").GetValue(voice) + " | " + Caption);
            var service = VivoxService.Instance;
            Assert.IsTrue(service.IsLoggedIn);
            Assert.IsTrue(service.ActiveChannels.ContainsKey(channel), "Room channel is active in Vivox.");
            Assert.AreEqual(director.Settings.VoiceMuted, service.IsInputDeviceMuted, "Microphone mute from Settings is applied to Vivox.");

            Select(Read<TableSelectable>(lobby, "leaveButton"));
            Assert.IsFalse(director.VoiceRoomActive);
            yield return Until(() => Read<string>(voice, "activeChannel") == null && !service.IsLoggedIn, "leave Vivox together with the room", 30);
        }

        static void RequireInternet()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                Assert.Ignore("No network: Multiplayer Services/Vivox tests skipped.");
            Assert.IsNotEmpty(Application.cloudProjectId, "UGS project is not linked yet (BOB-040).");
        }

        IEnumerator CreateRoom()
        {
            Assert.IsTrue(Button("Create Room").interactable, "Create Room is enabled when NetworkManager, the online table and the UGS project are ready.");
            Select(Button("Create Room"));
            yield return Until(() => Code != null || !Read<bool>(lobby, "busy"), "Create Room finished", 60);
            Assert.IsNotNull(Code, "Create Room failed: " + Caption);
        }

        // The lobby's own code lookup (used by Create Room and Join Room), not a copy of the query in the test.
        static Task<ISessionInfo> Find(string code) => (Task<ISessionInfo>)typeof(SessionRoomLobby)
            .GetMethod("FindAsync", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { code });

        static IEnumerator Await<T>(Task<T> task, Action<T> done, string what)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!task.IsCompleted)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, what);
                yield return null;
            }
            if (task.IsFaulted) Assert.Fail(what + ": " + task.Exception.GetBaseException().Message);
            done(task.Result);
        }

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Button " + label);

        static void Select(TableSelectable target)
        {
            Assert.IsNotNull(target);
            Assert.IsTrue(target.gameObject.activeInHierarchy && target.isActiveAndEnabled && target.interactable, target.name + " is not interactable");
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
