using System.Collections;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// BOB-043: CandyParlour3D is saved in VR state so the Meta Project Setup Tool does not ask for a camera/AudioListener
    /// required fix. Without a headset, ArtPreviewControls uses the desktop preview camera; on Quest only
    /// CenterEyeAnchor is the MainCamera, with one AudioListener.
    /// </summary>
    public class CameraSetupTests
    {
        const string SceneName = "CandyParlour3D";
        Scene scene;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            scene = SceneManager.GetActiveScene();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            // The OVR rig switched on by the VR test is torn down here too, before the next test.
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-043 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void Editor_without_XR_uses_desktop_preview_camera()
        {
            var preview = Object.FindFirstObjectByType<ArtPreviewControls>();
            Assert.IsNotNull(preview, "Desktop Art Preview missing from " + SceneName);
            Assert.AreSame(preview.GetComponent<Camera>(), Camera.main, "Without XR, the preview camera must be the MainCamera.");
            AssertSingleMainCamera(preview.gameObject);
            AssertSingleListener(preview.gameObject);
            var rig = Rig();
            Assert.IsFalse(rig.activeSelf, "Camera Rig must be off in Play without a headset.");
            var manager = rig.GetComponentInChildren<OVRManager>(true);
            if (manager != null)
                Assert.AreNotSame(manager, OVRManager.instance, "OVRManager ran Awake before ArtPreviewControls turned off the rig.");
        }

        [Test]
        public void VR_only_CenterEyeAnchor_is_main_camera_with_one_AudioListener()
        {
            // The OVR rig runs without a headset here; OVR logs are not what is being tested.
            LogAssert.ignoreFailingMessages = true;
            var preview = Object.FindFirstObjectByType<ArtPreviewControls>();
            Assert.IsNotNull(preview, "Desktop Art Preview missing from " + SceneName);
            ArtPreviewControls.UseCamera(xr: true);
            Assert.IsTrue(Rig().activeInHierarchy, "Camera Rig must be active while XR is running.");
            Assert.IsFalse(preview.gameObject.activeInHierarchy, "Desktop preview must be off while XR is running.");
            var main = Camera.main;
            Assert.IsNotNull(main, "No active MainCamera while XR is running.");
            Assert.AreEqual("CenterEyeAnchor", main.name);
            Assert.IsTrue(main.transform.IsChildOf(Rig().transform), "MainCamera must be inside the Camera Rig.");
            AssertSingleMainCamera(main.gameObject);
            AssertSingleListener(main.gameObject);
        }

        [Test]
        public void Local_avatar_head_is_not_drawn_by_first_person_camera()
        {
            // Budget Audit does not count the local avatar head per eye; both LocalAvatarTracking paths are verified here.
            var tracking = Object.FindFirstObjectByType<LocalAvatarTracking>();
            Assert.IsNotNull(tracking, "No active LocalAvatarTracking in " + SceneName);
            Assert.IsNotNull(tracking.firstPersonCamera, "LocalAvatarTracking has no first-person camera.");
            var heads = tracking.avatar.headRenderers.Where(r => r != null).ToArray();
            Assert.IsNotEmpty(heads, "Local avatar head renderer list is empty.");
            var drawn = heads.Where(r => !r.forceRenderingOff).Select(r => r.name).ToList();
            Assert.IsEmpty(drawn, "Desktop: local head is still drawn: " + string.Join(", ", drawn));
            // When XR is on, LateUpdate calls the same path: the head moves to a layer that the eye camera culls.
            tracking.avatar.SetLocalHeadHidden(tracking.firstPersonCamera, true);
            int mask = tracking.firstPersonCamera.cullingMask;
            drawn = heads.Where(r => (mask & (1 << r.gameObject.layer)) != 0).Select(r => r.name).ToList();
            Assert.IsEmpty(drawn, "XR: local head is still visible to camera " + tracking.firstPersonCamera.name + ": " + string.Join(", ", drawn));
        }

        GameObject Rig()
        {
            var rig = scene.GetRootGameObjects().FirstOrDefault(g => g.name.Contains("Camera Rig"));
            Assert.IsNotNull(rig, "Camera Rig missing from " + SceneName);
            return rig;
        }

        static void AssertSingleMainCamera(GameObject expected)
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(c => c.isActiveAndEnabled && c.CompareTag("MainCamera")).Select(c => c.name).ToList();
            CollectionAssert.AreEqual(new[] { expected.name }, cameras, "Active MainCamera cameras: " + string.Join(", ", cameras));
        }

        static void AssertSingleListener(GameObject expected)
        {
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .Where(l => l.isActiveAndEnabled).ToList();
            Assert.AreEqual(1, listeners.Count, "Active AudioListeners: " + string.Join(", ", listeners.Select(l => l.name)));
            Assert.AreSame(expected, listeners[0].gameObject);
        }
    }
}
