using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace BiteOrBetray.EditorTools
{
    /// <summary>
    /// Install online multiplayer packages (Netcode for GameObjects, Multiplayer Services for Lobby/Relay, Vivox)
    /// with the recommended versions for this editor.
    /// Unity -batchmode -executeMethod BiteOrBetray.EditorTools.PackageSetup.BatchAddNetworking (without -quit).
    /// Batch exits once packages are installed; compilation and csproj generation happen next time the project opens
    /// (editor callbacks disappear after a domain reload, so do not wait for compilation here).
    /// </summary>
    public static class PackageSetup
    {
        static readonly string[] Networking = { "com.unity.netcode.gameobjects", "com.unity.services.multiplayer", "com.unity.services.vivox" };
        static AddAndRemoveRequest request;

        [MenuItem("Bite or Betray/Install Online Packages (NGO, Multiplayer, Vivox)")]
        public static void AddNetworking() => Start(false);

        public static void BatchAddNetworking() => Start(true);

        static void Start(bool exitWhenDone)
        {
            request = Client.AddAndRemove(Networking);
            EditorApplication.update += Wait;

            void Wait()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Wait;
                bool ok = request.Status == StatusCode.Success;
                string report = ok ? "OK\n" + string.Join("\n", request.Result.Select(p => p.packageId)) : "FAILED " + request.Error?.message;
                File.WriteAllText("Logs/PackageSetup.result.txt", report);
                Debug.Log("[Bite or Betray] PackageSetup: " + report);
                if (exitWhenDone) EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
