using System.Collections;
using System.Linq;
using BiteOrBetray.Art3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// BOB-043: CandyParlour3D disimpan dalam keadaan VR supaya Project Setup Tool Meta tidak meminta required fix
    /// kamera/AudioListener. Tanpa headset, ArtPreviewControls memakai kamera preview desktop; di Quest hanya
    /// CenterEyeAnchor yang menjadi MainCamera dengan satu AudioListener.
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
            // Rig OVR yang dinyalakan tes VR ikut dibongkar di sini, sebelum tes berikutnya.
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-043 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void Editor_tanpa_XR_memakai_kamera_preview_desktop()
        {
            var preview = Object.FindFirstObjectByType<ArtPreviewControls>();
            Assert.IsNotNull(preview, "Desktop Art Preview tidak ada di " + SceneName);
            Assert.AreSame(preview.GetComponent<Camera>(), Camera.main, "Tanpa XR, kamera preview harus menjadi MainCamera.");
            AssertSingleMainCamera(preview.gameObject);
            AssertSingleListener(preview.gameObject);
            var rig = Rig();
            Assert.IsFalse(rig.activeSelf, "Camera Rig harus mati saat Play tanpa headset.");
            var manager = rig.GetComponentInChildren<OVRManager>(true);
            if (manager != null)
                Assert.AreNotSame(manager, OVRManager.instance, "OVRManager sempat Awake sebelum ArtPreviewControls mematikan rig.");
        }

        [Test]
        public void VR_hanya_CenterEyeAnchor_sebagai_kamera_utama_dengan_satu_AudioListener()
        {
            // Rig OVR menyala tanpa headset di sini; log OVR bukan bagian yang diuji.
            LogAssert.ignoreFailingMessages = true;
            var preview = Object.FindFirstObjectByType<ArtPreviewControls>();
            Assert.IsNotNull(preview, "Desktop Art Preview tidak ada di " + SceneName);
            ArtPreviewControls.UseCamera(xr: true);
            Assert.IsTrue(Rig().activeInHierarchy, "Camera Rig harus aktif saat XR berjalan.");
            Assert.IsFalse(preview.gameObject.activeInHierarchy, "Preview desktop harus mati saat XR berjalan.");
            var main = Camera.main;
            Assert.IsNotNull(main, "Tidak ada MainCamera aktif saat XR berjalan.");
            Assert.AreEqual("CenterEyeAnchor", main.name);
            Assert.IsTrue(main.transform.IsChildOf(Rig().transform), "MainCamera harus berada di dalam Camera Rig.");
            AssertSingleMainCamera(main.gameObject);
            AssertSingleListener(main.gameObject);
        }

        GameObject Rig()
        {
            var rig = scene.GetRootGameObjects().FirstOrDefault(g => g.name.Contains("Camera Rig"));
            Assert.IsNotNull(rig, "Camera Rig tidak ada di " + SceneName);
            return rig;
        }

        static void AssertSingleMainCamera(GameObject expected)
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(c => c.isActiveAndEnabled && c.CompareTag("MainCamera")).Select(c => c.name).ToList();
            CollectionAssert.AreEqual(new[] { expected.name }, cameras, "Kamera MainCamera aktif: " + string.Join(", ", cameras));
        }

        static void AssertSingleListener(GameObject expected)
        {
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .Where(l => l.isActiveAndEnabled).ToList();
            Assert.AreEqual(1, listeners.Count, "AudioListener aktif: " + string.Join(", ", listeners.Select(l => l.name)));
            Assert.AreSame(expected, listeners[0].gameObject);
        }
    }
}
