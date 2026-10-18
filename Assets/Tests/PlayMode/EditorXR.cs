using System.Collections;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Management;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// Laptop CI memakai Meta XR Simulator sebagai runtime OpenXR, jadi XR Management menyalakan XR saat Editor masuk
    /// PlayMode. Suite dengan sampel perangkat dari tes mematikan loader XR sebelum scene dimuat: VRTableInput memilih
    /// mode VR di Awake, dan rig OVR yang ikut menyala menulis log saat scene dibongkar.
    /// XR tidak dinyalakan lagi; XR Management sendiri men-deinit loader saat Editor keluar PlayMode.
    /// </summary>
    static class EditorXR
    {
        public static IEnumerator Stop()
        {
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager != null && manager.activeLoader != null)
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }
            float deadline = Time.realtimeSinceStartup + 10;
            while (VRTableInput.XRActive)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "XR Editor tidak bisa dimatikan untuk suite tanpa headset.");
                yield return null;
            }
        }
    }
}
