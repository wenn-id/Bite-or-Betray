using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace BiteOrBetray.EditorTools
{
    /// <summary>
    /// Pasang package online multiplayer (Netcode for GameObjects, Multiplayer Services untuk Lobby/Relay, Vivox)
    /// dengan versi yang direkomendasikan untuk editor ini.
    /// Unity -batchmode -executeMethod BiteOrBetray.EditorTools.PackageSetup.BatchAddNetworking (tanpa -quit).
    /// Batch keluar begitu package terpasang; compile dan csproj dibuat saat project dibuka berikutnya
    /// (callback editor hilang setelah domain reload, jadi jangan menunggu compile di sini).
    /// </summary>
    public static class PackageSetup
    {
        static readonly string[] Networking = { "com.unity.netcode.gameobjects", "com.unity.services.multiplayer", "com.unity.services.vivox" };
        static AddAndRemoveRequest request;

        [MenuItem("Bite or Betray/Pasang Package Online (NGO, Multiplayer, Vivox)")]
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
                string report = ok ? "OK\n" + string.Join("\n", request.Result.Select(p => p.packageId)) : "GAGAL " + request.Error?.message;
                File.WriteAllText("Logs/PackageSetup.result.txt", report);
                Debug.Log("[Bite or Betray] PackageSetup: " + report);
                if (exitWhenDone) EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
