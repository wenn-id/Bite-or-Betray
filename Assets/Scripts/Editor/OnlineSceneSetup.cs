using System;
using BiteOrBetray.Table3D;
using BiteOrBetray.Table3D.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BiteOrBetray.EditorTools
{
    /// <summary>
    /// Pasang objek online (BOB-035) di scene yang terbuka: NetworkManager + UnityTransport dengan lobby Sessions, dan
    /// NetworkObject + NetworkTableSession milik meja. Aman dijalankan ulang; yang sudah ada tidak digandakan.
    /// Online tetap butuh project UGS yang ter-link (BOB-040) supaya tombol Buat/Gabung Room aktif.
    /// </summary>
    public static class OnlineSceneSetup
    {
        const string NetworkName = "BoB_Network";
        const string TableName = "BoB_TableSession";

        [MenuItem("Bite or Betray/Pasang Online di Scene")]
        static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var director = UnityEngine.Object.FindFirstObjectByType<CandyTableDirector>();
            if (director == null) throw new InvalidOperationException("Pasang Autoplay Meja 3D dulu (CandyTableDirector tidak ditemukan).");
            bool wasDirty = scene.isDirty;
            int added = 0;

            var manager = UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
            if (manager == null)
            {
                manager = new GameObject(NetworkName).AddComponent<NetworkManager>();
                added++;
            }
            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();
            var transport = manager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = manager.gameObject.AddComponent<UnityTransport>();
                added++;
            }
            if (manager.NetworkConfig.NetworkTransport == null) manager.NetworkConfig.NetworkTransport = transport;
            // Klien menyamakan scene dengan host; NetworkTableSession adalah objek scene, bukan prefab.
            manager.NetworkConfig.EnableSceneManagement = true;

            var table = UnityEngine.Object.FindFirstObjectByType<NetworkTableSession>();
            if (table == null)
            {
                table = new GameObject(TableName).AddComponent<NetworkTableSession>();
                added++;
            }
            table.director = director;

            var lobby = UnityEngine.Object.FindFirstObjectByType<SessionRoomLobby>();
            if (lobby == null)
            {
                lobby = manager.gameObject.AddComponent<SessionRoomLobby>();
                added++;
            }
            lobby.table = table;

            foreach (var o in new UnityEngine.Object[] { manager, transport, table, lobby }) EditorUtility.SetDirty(o);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Bite or Betray] Online dipasang di {scene.path}: {added} komponen baru"
                + (wasDirty ? " (scene belum disimpan)" : "") + ". Link project UGS (Project Settings > Services) supaya Buat/Gabung Room aktif.");
        }
    }
}
