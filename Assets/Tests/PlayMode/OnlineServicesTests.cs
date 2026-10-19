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
    /// Multiplayer Services nyata (BOB-041; project UGS ter-link di BOB-040). Tombol Buat Room di menu meja memakai
    /// SessionRoomLobby asli: login anonim, session Lobby + Relay yang memulai NGO sebagai host, kode 6 angka di panel.
    /// Kode itu dicari dengan query yang sama dengan Gabung Room, Keluar room menghapus session, lalu kode yang sudah
    /// ditutup tampil sebagai pesan. Vivox masuk channel room yang sama dan keluar bersama room.
    /// Objek online bawaan scene (BOB-046) diganti susunan yang sama milik tes, supaya transport bisa diperiksa langsung.
    /// Satu proses hanya punya satu identitas UGS: pemain kedua yang gabung lewat Relay dan saling dengar voice belum
    /// bisa dibuktikan di sini (gameplay host+klien dibuktikan OnlineMatchTests lewat UnityTransport lokal).
    /// Tanpa jaringan tes dilewati (Assert.Ignore).
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
            Assert.IsNotNull(director, "Director wajib berasal dari scene gameplay.");
            yield return Until(() => Read<bool>(director, "ready"), "menu selesai Start");
            foreach (var existing in Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(existing.gameObject);
            foreach (var existing in Object.FindObjectsByType<NetworkTableSession>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(existing.gameObject);
            yield return null;
            director.recordTelemetry = false;

            // Susunan sama dengan OnlineSceneSetup: NetworkManager + UnityTransport + lobby, scene management aktif.
            var network = new GameObject("BOB-041 Network");
            created.Add(network);
            network.SetActive(false);
            manager = network.AddComponent<NetworkManager>();
            transport = network.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = true };
            // Meja online tetap nonaktif: tes ini tidak memulai game, dan objek runtime bukan objek scene NGO.
            var tableObject = new GameObject("BOB-041 Meja");
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
            // Hapus session dan logout Vivox berjalan async; beri waktu sebelum scene dibongkar.
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
            Assert.IsTrue(OnlineRoom.IsValidCode(code), "Kode room harus 6 angka: " + code);
            Assert.IsTrue(AuthenticationService.Instance.IsSignedIn, "Login anonim UGS.");
            Assert.IsNotEmpty(AuthenticationService.Instance.PlayerId);
            Assert.IsTrue(session.IsHost);
            Assert.AreEqual(1, session.PlayerCount);
            Assert.AreEqual(code, session.Properties["code"].Value, "Kode angka disimpan di properti session publik.");
            Assert.AreSame(manager, NetworkManager.Singleton);
            Assert.IsTrue(manager.IsHost && manager.IsListening, "Session memulai NGO sebagai host.");
            Assert.AreEqual(UnityTransport.ProtocolType.RelayUnityTransport, transport.Protocol, "Host NGO berjalan lewat Relay.");
            var codeText = Read<TextMesh>(lobby, "codeText");
            Assert.IsTrue(codeText.gameObject.activeInHierarchy, "Kode room tampil di panel meja.");
            StringAssert.Contains(OnlineRoom.FormatCode(code), codeText.text);
            StringAssert.Contains(Strings.PlayersInRoom.Format(1, director.SeatCount), Read<TextMesh>(lobby, "infoText").text);
            Assert.IsFalse(Read<TableSelectable>(lobby, "startButton").interactable, $"Mulai butuh minimal {OnlineRoom.MinHumans} pemain.");

            // Query yang sama dengan Gabung Room. Jeda menjaga batas rate query Lobby per pemain.
            ISessionInfo found = null;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Await(Find(code), result => found = result, "cari session dengan kode");
            Assert.IsNotNull(found, "Session tidak ditemukan dengan kode " + code);
            Assert.AreEqual(session.Id, found.Id);

            Select(Read<TableSelectable>(lobby, "leaveButton"));
            Assert.IsNull(Session);
            Assert.IsTrue(Button("Create Room").gameObject.activeInHierarchy, "Keluar room kembali ke menu utama.");
            yield return Until(() => !manager.IsListening, "NGO berhenti setelah keluar room");
            // Host keluar menghapus session; Lobby butuh beberapa detik sampai query tidak menemukannya lagi.
            float deadline = Time.realtimeSinceStartup + 40;
            do
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Session masih bisa dicari setelah host keluar room.");
                yield return new WaitForSecondsRealtime(2);
                yield return Await(Find(code), result => found = result, "cari session setelah keluar");
            } while (found != null);

            // Kode yang sudah ditutup ditolak dengan pesan di meja, tanpa crash dan tanpa NGO menyala.
            yield return new WaitForSecondsRealtime(1.5f);
            Select(Button("Join Room"));
            var digits = Read<IList>(lobby, "digitButtons");
            foreach (char c in code) Select((TableSelectable)digits[c - '0']);
            Select(Read<TableSelectable>(lobby, "joinButton"));
            yield return Until(() => Caption.Contains(Strings.CodeNotFound.Format(OnlineRoom.FormatCode(code))), "pesan kode tidak ditemukan", 30);
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
            yield return Until(() => Read<string>(voice, "activeChannel") == channel || Caption.Contains(Strings.VoiceUnavailable.Text), "masuk channel Vivox", 60);
            Assert.AreEqual(channel, Read<string>(voice, "activeChannel"), "Vivox gagal: " + voice.GetType().GetProperty("LastError").GetValue(voice) + " | " + Caption);
            var service = VivoxService.Instance;
            Assert.IsTrue(service.IsLoggedIn);
            Assert.IsTrue(service.ActiveChannels.ContainsKey(channel), "Channel room aktif di Vivox.");
            Assert.AreEqual(director.Settings.VoiceMuted, service.IsInputDeviceMuted, "Mute mikrofon di Settings diterapkan ke Vivox.");

            Select(Read<TableSelectable>(lobby, "leaveButton"));
            Assert.IsFalse(director.VoiceRoomActive);
            yield return Until(() => Read<string>(voice, "activeChannel") == null && !service.IsLoggedIn, "keluar Vivox bersama room", 30);
        }

        static void RequireInternet()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
                Assert.Ignore("Tidak ada jaringan: tes Multiplayer Services/Vivox dilewati.");
            Assert.IsNotEmpty(Application.cloudProjectId, "Project UGS belum ter-link (BOB-040).");
        }

        IEnumerator CreateRoom()
        {
            Assert.IsTrue(Button("Create Room").interactable, "Buat Room aktif kalau NetworkManager, meja online dan project UGS siap.");
            Select(Button("Create Room"));
            yield return Until(() => Code != null || !Read<bool>(lobby, "busy"), "Buat Room selesai", 60);
            Assert.IsNotNull(Code, "Buat Room gagal: " + Caption);
        }

        // Pencarian kode milik lobby (dipakai Buat Room dan Gabung Room), bukan salinan query di tes.
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
            Assert.IsTrue(target.gameObject.activeInHierarchy && target.isActiveAndEnabled && target.interactable, target.name + " tidak interaktif");
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
