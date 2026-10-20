using System.Collections;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Management;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// Laptop CI uses the Meta XR Simulator as the OpenXR runtime, so XR Management starts XR when the Editor enters
    /// PlayMode. Suites with test-driven device samples stop the XR loader before the scene loads: VRTableInput picks
    /// VR mode in Awake, and the OVR rig that starts along with it writes logs when the scene is unloaded.
    /// XR is not restarted; XR Management itself deinitializes the loader when the Editor exits PlayMode.
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
                Assert.Less(Time.realtimeSinceStartup, deadline, "Could not stop Editor XR for the headless suite.");
                yield return null;
            }
        }
    }
}
