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
    /// Install online objects (BOB-035) in the open scene: NetworkManager + UnityTransport with a Sessions lobby, and
    /// the table's NetworkObject + NetworkTableSession. Safe to repeat; existing objects are not duplicated.
    /// Online still needs a linked UGS project (BOB-040) to enable Create/Join Room buttons.
    /// </summary>
    public static class OnlineSceneSetup
    {
        const string NetworkName = "BoB_Network";
        const string TableName = "BoB_TableSession";

        [MenuItem("Bite or Betray/Install Online in Scene")]
        static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var director = UnityEngine.Object.FindFirstObjectByType<CandyTableDirector>();
            if (director == null) throw new InvalidOperationException("Install 3D Table Autoplay first (CandyTableDirector not found).");
            bool wasDirty = scene.isDirty;
            int added = Install(director);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Bite or Betray] Online installed in {scene.path}: {added} new components"
                + (wasDirty ? " (scene not saved yet)" : "") + ". Link a UGS project (Project Settings > Services) to enable Create/Join Room.");
        }

        /// <summary>
        /// Install/update online objects in the director's scene without saving; used by the menu above and ArtSceneBuilder.Build
        /// (BOB-046). Reuse existing objects to preserve the in-scene table's GlobalObjectIdHash.
        /// </summary>
        internal static int Install(CandyTableDirector director)
        {
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
            // Clients synchronize scenes with the host; NetworkTableSession is a scene object, not a prefab.
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
            return added;
        }
    }
}
