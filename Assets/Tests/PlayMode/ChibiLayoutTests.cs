using System.Collections;
using System.IO;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    public class ChibiLayoutTests : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup()
        {
#if UNITY_EDITOR
            var xr = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (xr != null)
            {
                if (!UnityEditor.SessionState.GetBool("ChibiTestXRStored", false)) UnityEditor.SessionState.SetBool("ChibiTestXRStartup", xr.InitManagerOnStart);
                UnityEditor.SessionState.SetBool("ChibiTestXRStored", true);xr.InitManagerOnStart = false;
            }
#endif
        }
        public void Cleanup()
        {
#if UNITY_EDITOR
            var xr = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (xr != null) xr.InitManagerOnStart = UnityEditor.SessionState.GetBool("ChibiTestXRStartup", true);
            UnityEditor.SessionState.EraseBool("ChibiTestXRStored");
#endif
        }
        [UnityTest]
        public IEnumerator New_characters_are_visible_around_low_dish_and_local_head_survives_camera_changes()
        {
            yield return EditorXR.Stop();
            yield return SceneManager.LoadSceneAsync("CandyParlour3D");
            for (int i = 0; i < 90; i++) yield return null;
            var root = GameObject.Find("BoB_Art3D").transform;
            Assert.IsNotNull(root.Find("Chibi Table Layout"));
            var camera = Camera.main;Assert.IsNotNull(camera);
            var preview = camera.GetComponent<ArtPreviewControls>();Assert.IsTrue(preview.tableOverview);
            var seats = root.Find("Seats");Assert.AreEqual(6, seats.childCount);
            foreach (Transform seat in seats)
            {
                Assert.AreEqual(1.62f, new Vector2(seat.localPosition.x, seat.localPosition.z).magnitude, .002f);
                var head = seat.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                var animation = head.GetComponentInParent<CharacterAnimator>();
                animation.enabled = false;
                var inward = Vector3.ProjectOnPlane(root.position - head.position, Vector3.up).normalized;
                Assert.Greater(Vector3.Dot(head.forward, inward), .999f, seat.name + " head does not face the center");
                animation.enabled = true;
                var point = camera.WorldToViewportPoint(head.position);
                Assert.That(point.x, Is.InRange(.06f, .94f), seat.name);
                Assert.That(point.y, Is.InRange(.06f, .94f), seat.name);
                Assert.Greater(point.z, 0);
                foreach (var hand in new[] { seat.GetComponentInChildren<ChibiHands>().left, seat.GetComponentInChildren<ChibiHands>().right })
                {
                    var first = hand.fingers.Single(t => t.name == "Finger_Middle_1");
                    var second = hand.fingers.Single(t => t.name == "Finger_Middle_2");
                    Assert.Greater(Vector3.Dot((second.position - first.position).normalized, seat.forward), .65f, seat.name + " fingers point away from the table");
                }
            }
            var waiterBounds = root.Find("Waiter").GetComponentInChildren<Renderer>().bounds;
            Assert.Greater(waiterBounds.size.y, 2.1f, "Waiter is too small");
            var local = seats.Find("Seat_0").GetComponentInChildren<SeatedAvatar>();Assert.IsNotNull(local);
            Assert.IsTrue(local.headRenderers.All(r => r.enabled && !r.forceRenderingOff), "Main character hidden in overview");
            var dish = root.Find("Dispenser");var bounds = new Bounds(dish.position, Vector3.zero);
            foreach (var r in dish.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            Assert.Less(bounds.max.y, .91f, "Center dish blocks the opposite player");
            Assert.IsTrue(Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials).All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader"));
            Capture(camera, "game-menu.png");
            var old = camera.transform.position;var rotation = camera.transform.rotation;
            camera.transform.position = local.head.position;
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(local.headRenderers.All(r => r.forceRenderingOff), "First person camera sees inside the local head");
            camera.transform.SetPositionAndRotation(old, rotation);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(local.headRenderers.All(r => !r.forceRenderingOff), "Local head does not return in overview");
            var director = Object.FindFirstObjectByType<CandyTableDirector>();director.onboarding = false;director.recordTelemetry = false;
            director.OpenRoomSetup();director.Restart();
            for (int i = 0; i < 5; i++) yield return null;
            var skip = (TableSelectable)typeof(CandyTableDirector).GetField("skipButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(director);
            if (skip != null && skip.interactable) skip.Select();
            for (int i = 0; i < 180; i++) yield return null;
            Assert.IsNotNull(typeof(CandyTableDirector).GetField("engine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(director), "Match did not start");
            Capture(camera, "gameplay.png");
            // Also capture the authored table without transient game cards.
            var texts = director.GetComponentsInChildren<TextMesh>(true).Select(t => t.gameObject).ToArray();
            var active = texts.Select(t => t.activeSelf).ToArray();
            for (int i = 0; i < texts.Length; i++) texts[i].SetActive(false);
            Capture(camera, "table-clean.png");
            for (int i = 0; i < texts.Length; i++) texts[i].SetActive(active[i]);
        }
        static void Capture(Camera camera, string name)
        {
            Directory.CreateDirectory("Logs/NewCharacters");
            var rt = RenderTexture.GetTemporary(1600, 1000, 24);var old = RenderTexture.active;var target = camera.targetTexture;
            try
            {
                camera.targetTexture = rt;camera.Render();RenderTexture.active = rt;
                var tex = new Texture2D(1600, 1000, TextureFormat.RGB24, false);tex.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);tex.Apply();File.WriteAllBytes("Logs/NewCharacters/" + name, tex.EncodeToPNG());Object.Destroy(tex);
            }
            finally { camera.targetTexture = target;RenderTexture.active = old;RenderTexture.ReleaseTemporary(rt); }
        }
    }
}
