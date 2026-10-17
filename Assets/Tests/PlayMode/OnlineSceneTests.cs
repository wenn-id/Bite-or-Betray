using System;
using System.Collections;
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
    /// Objek online bawaan scene CandyParlour3D (BOB-046), tanpa objek pengganti milik tes: BoB_Network
    /// (NetworkManager + UnityTransport + SessionRoomLobby) dan BoB_TableSession (NetworkObject in-scene +
    /// NetworkTableSession). Menu utama scene asli harus menampilkan Buat/Gabung Room aktif, host NGO dari scene
    /// men-spawn meja in-scene, dan sinkronisasi scene klien melewati scene aktif yang sama (tidak dimuat ulang).
    /// NetworkManager pindah ke DontDestroyOnLoad saat aktif; sisa scene sebelumnya dibuang sebelum dan sesudah tes.
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
            Assert.IsNotNull(director, "Director wajib berasal dari scene gameplay.");
            director.recordTelemetry = false;
            yield return Until(() => Read<bool>(director, "ready"), "menu selesai Start");
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
            Assert.AreEqual(1, managers.Length, "Scene berisi tepat satu NetworkManager.");
            var manager = managers[0];
            Assert.AreEqual("BoB_Network", manager.name);
            Assert.AreSame(manager, NetworkManager.Singleton);
            var transport = manager.GetComponent<UnityTransport>();
            Assert.IsNotNull(transport, "UnityTransport di BoB_Network.");
            Assert.AreSame(transport, manager.NetworkConfig.NetworkTransport);
            Assert.IsTrue(manager.NetworkConfig.EnableSceneManagement, "Klien menyamakan scene dengan host; meja adalah objek scene.");

            var lobbies = Object.FindObjectsByType<SessionRoomLobby>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, lobbies.Length, "Scene berisi tepat satu SessionRoomLobby.");
            Assert.AreSame(manager.gameObject, lobbies[0].gameObject);

            var tables = Object.FindObjectsByType<NetworkTableSession>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, tables.Length, "Scene berisi tepat satu NetworkTableSession.");
            var table = tables[0];
            Assert.AreEqual("BoB_TableSession", table.name);
            Assert.AreEqual(scene, table.gameObject.scene, "Meja online tetap objek scene gameplay.");
            Assert.AreSame(director, table.director, "Director meja online terisi di scene.");
            Assert.AreSame(table, lobbies[0].table);
            var networkObject = table.GetComponent<NetworkObject>();
            Assert.IsNotNull(networkObject);
            Assert.IsTrue(networkObject.InScenePlaced, "NetworkObject meja ditandai in-scene saat disimpan, bukan saat runtime.");
            Assert.AreNotEqual(0u, (uint)typeof(NetworkObject).GetField("GlobalObjectIdHash", Fields).GetValue(networkObject),
                "GlobalObjectIdHash in-scene meja tersimpan.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Main_menu_of_real_scene_enables_create_and_join_room()
        {
            Assert.IsNotEmpty(Application.cloudProjectId, "Project UGS belum ter-link (BOB-040).");
            var lobby = Object.FindFirstObjectByType<SessionRoomLobby>();
            Assert.IsNotNull(lobby);
            Assert.IsTrue(lobby.Ready, "Lobby scene siap: NetworkManager, meja online dan project UGS.");
            // Menu dibuka sendiri oleh director saat Start; tidak ada lobby yang dipasang tes.
            Assert.AreSame(lobby, director.Lobby, "Director memakai lobby bawaan scene.");
            foreach (string label in new[] { "Buat Room", "Gabung Room" })
            {
                var button = Button(label);
                Assert.IsTrue(button.gameObject.activeInHierarchy, label + " tampil di menu utama.");
                Assert.IsTrue(button.interactable, label + " aktif (tidak abu-abu).");
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
            Assert.IsTrue(manager.StartHost(), "Host NGO dari objek scene gagal mulai di 127.0.0.1:" + Port);
            // Meja tidak di-spawn tes: sapuan objek scene NGO hanya menemukannya kalau hash in-scene tersimpan.
            yield return Until(() => table.IsSpawned && director.Network == table, "meja in-scene di-spawn host dan terikat ke director");
            Assert.IsTrue(table.NetworkObject.InScenePlaced);

            // Jalur NGO saat klien menerima sinkronisasi (OnClientBeginSync): scene utama host yang sudah menjadi scene
            // aktif klien dilewati, bukan dimuat ulang. Dua proses (host + klien sungguhan) tetap dicek owner.
            var sceneManager = manager.SceneManager;
            Assert.IsNotNull(sceneManager, "Scene management NGO aktif.");
            Assert.AreEqual(LoadSceneMode.Single, sceneManager.ClientSynchronizationMode);
            var handler = typeof(NetworkSceneManager).GetField("SceneManagerHandler", Fields)?.GetValue(sceneManager);
            Assert.IsNotNull(handler, "Field SceneManagerHandler NGO tidak ditemukan.");
            var passThrough = handler.GetType().GetMethod("ClientShouldPassThrough", Fields);
            Assert.IsNotNull(passThrough, "ClientShouldPassThrough NGO tidak ditemukan.");
            Assert.IsTrue((bool)passThrough.Invoke(handler, new object[] { scene.name, true, sceneManager.ClientSynchronizationMode, manager }),
                "Klien yang sudah membuka CandyParlour3D tidak boleh memuat ulang scene saat sinkronisasi.");

            manager.Shutdown();
            yield return Until(() => !manager.IsListening && !manager.ShutdownInProgress, "host NGO berhenti");
            yield return Until(() => director.Network == null, "meja lepas dari director setelah host berhenti");
            Assert.IsTrue(Button("Buat Room").gameObject.activeInHierarchy, "Host berhenti kembali ke menu utama.");
        }

        // NetworkManager scene pindah ke DontDestroyOnLoad dan tidak ikut terbongkar bersama scene.
        static void DestroyManagers()
        {
            foreach (var manager in Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(manager.gameObject);
        }

        TableSelectable Button(string label) => director.GetComponentsInChildren<TableSelectable>(true)
            .Single(t => t.name == "Tombol " + label);

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
