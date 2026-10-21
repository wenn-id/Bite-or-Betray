using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// The built-in online objects of the CandyParlour3D scene (BOB-046), with no test-owned stand-ins: BoB_Network
    /// (NetworkManager + UnityTransport + SessionRoomLobby) and BoB_TableSession (in-scene NetworkObject +
    /// NetworkTableSession). The real scene's main menu must show Create/Join Room enabled, the NGO host from the scene
    /// spawns the in-scene table, and client scene sync passes through the same active scene (not reloaded).
    /// NetworkManager moves to DontDestroyOnLoad when active; leftovers from earlier scenes are destroyed before and after tests.
    /// </summary>
    public class OnlineSceneTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const ushort Port = 7792;
        CandyTableDirector director;
        Scene scene;
        bool oldRunInBackground;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            oldRunInBackground = Application.runInBackground;
            yield return EditorXR.Stop();
            DestroyManagers();
            yield return null;
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "Director must come from the gameplay scene.");
            director.recordTelemetry = false;
            yield return Until(() => Read<bool>(director, "ready"), "menu finished Start");
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening) manager.Shutdown();
            float deadline = Time.realtimeSinceStartup + 10;
            while (manager != null && (manager.IsListening || manager.ShutdownInProgress) && Time.realtimeSinceStartup < deadline) yield return null;
            DestroyManagers();
            yield return null;
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-046 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Application.runInBackground = oldRunInBackground;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Scene_has_one_network_manager_lobby_and_in_scene_table_bound_to_director()
        {
            var managers = Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, managers.Length, "Scene contains exactly one NetworkManager.");
            var manager = managers[0];
            Assert.AreEqual("BoB_Network", manager.name);
            Assert.AreSame(manager, NetworkManager.Singleton);
            var transport = manager.GetComponent<UnityTransport>();
            Assert.IsNotNull(transport, "UnityTransport on BoB_Network.");
            Assert.AreSame(transport, manager.NetworkConfig.NetworkTransport);
            Assert.IsTrue(manager.NetworkConfig.EnableSceneManagement, "Clients sync the scene with the host; the table is a scene object.");

            var lobbies = Object.FindObjectsByType<SessionRoomLobby>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, lobbies.Length, "Scene contains exactly one SessionRoomLobby.");
            Assert.AreSame(manager.gameObject, lobbies[0].gameObject);

            var tables = Object.FindObjectsByType<NetworkTableSession>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, tables.Length, "Scene contains exactly one NetworkTableSession.");
            var table = tables[0];
            Assert.AreEqual("BoB_TableSession", table.name);
            Assert.AreEqual(scene, table.gameObject.scene, "The online table stays a gameplay scene object.");
            Assert.AreSame(director, table.director, "The online table's director is assigned in the scene.");
            Assert.AreSame(table, lobbies[0].table);
            var networkObject = table.GetComponent<NetworkObject>();
            Assert.IsNotNull(networkObject);
            Assert.IsTrue(networkObject.InScenePlaced, "The table NetworkObject is marked in-scene when saved, not at runtime.");
            Assert.AreNotEqual(0u, (uint)typeof(NetworkObject).GetField("GlobalObjectIdHash", Fields).GetValue(networkObject),
                "The table's in-scene GlobalObjectIdHash is saved.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Main_menu_of_real_scene_enables_create_and_join_room()
        {
            Assert.IsNotEmpty(Application.cloudProjectId, "UGS project is not linked yet (BOB-040).");
            var lobby = Object.FindFirstObjectByType<SessionRoomLobby>();
            Assert.IsNotNull(lobby);
            Assert.IsTrue(lobby.Ready, "Scene lobby ready: NetworkManager, online table and UGS project.");
            // The director opens the menu itself in Start; no lobby is installed by the test.
            Assert.AreSame(lobby, director.Lobby, "Director uses the scene's built-in lobby.");
            foreach (string label in new[] { "Create Room", "Join Room" })
            {
                var button = Button(label);
                Assert.IsTrue(button.gameObject.activeInHierarchy, label + " is shown in the main menu.");
                Assert.IsTrue(button.interactable, label + " is enabled (not grayed out).");
            }
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Scene_host_spawns_in_scene_table_and_client_sync_keeps_loaded_scene()
        {
            Application.runInBackground = true;
            var manager = NetworkManager.Singleton;
            var table = Object.FindFirstObjectByType<NetworkTableSession>();
            Assert.IsNotNull(manager);
            Assert.IsNotNull(table);
            manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", Port, "127.0.0.1");
            Assert.IsTrue(manager.StartHost(), "NGO host from the scene object failed to start on 127.0.0.1:" + Port);
            // The test does not spawn the table: the NGO scene object sweep only finds it if the in-scene hash is saved.
            yield return Until(() => table.IsSpawned && director.Network == table, "in-scene table spawned by host and bound to director");
            Assert.IsTrue(table.NetworkObject.InScenePlaced);

            // NGO path when a client receives sync (OnClientBeginSync): the host's main scene, already the client's active
            // scene, is passed through, not reloaded. Two processes (real host + client) are still checked by the owner.
            var sceneManager = manager.SceneManager;
            Assert.IsNotNull(sceneManager, "NGO scene management is active.");
            Assert.AreEqual(LoadSceneMode.Single, sceneManager.ClientSynchronizationMode);
            var handler = typeof(NetworkSceneManager).GetField("SceneManagerHandler", Fields)?.GetValue(sceneManager);
            Assert.IsNotNull(handler, "NGO SceneManagerHandler field not found.");
            var passThrough = handler.GetType().GetMethod("ClientShouldPassThrough", Fields);
            Assert.IsNotNull(passThrough, "NGO ClientShouldPassThrough not found.");
            Assert.IsTrue((bool)passThrough.Invoke(handler, new object[] { scene.name, true, sceneManager.ClientSynchronizationMode, manager }),
                "A client that already has CandyParlour3D open must not reload the scene during sync.");

            manager.Shutdown();
            yield return Until(() => !manager.IsListening && !manager.ShutdownInProgress, "NGO host stopped");
            yield return Until(() => director.Network == null, "table detached from director after host stops");
            Assert.IsTrue(Button("Create Room").gameObject.activeInHierarchy, "Stopping the host returns to the main menu.");
        }

        // Host candy/item visuals are sent with their original localScale. The scene FBX root has scale 100, so ShowItem items (1.2x)
        // and Punch candy (1.35x) used to trigger IOException "Invalid coordinates" in the host's LateUpdate; the test uses the combined limit.
        [UnityTest]
        public IEnumerator Scene_item_and_candy_scales_survive_visual_codec()
        {
            var templates = Read<IDictionary>(director, "itemTemplates").Values.Cast<GameObject>()
                .Concat(Read<IDictionary>(director, "candyTemplates").Values.Cast<GameObject>()).ToList();
            Assert.Greater(templates.Count, 0, "Item/candy templates from BoB_Art3D.");
            foreach (var template in templates)
            {
                var scale = template.transform.localScale * 1.2f * 1.35f;
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                    Assert.DoesNotThrow(() => AvatarVisualWire.WriteScale(writer, scale), template.name + " scale " + scale);
                Assert.AreEqual(6, stream.Length, "Scale stays 6 bytes so the visual packet size does not change.");
                stream.Position = 0;
                var decoded = AvatarVisualWire.ReadScale(new BinaryReader(stream));
                for (int i = 0; i < 3; i++)
                    Assert.AreEqual(scale[i], decoded[i], Mathf.Abs(scale[i]) * .001f + 1e-6f, template.name + " axis " + i);
            }
            yield return null;
        }

        // The scene NetworkManager moves to DontDestroyOnLoad and is not unloaded with the scene.
        static void DestroyManagers()
        {
            foreach (var manager in Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(manager.gameObject);
        }

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Button " + label);

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
    }
}
