using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BiteOrBetray.Art3D;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    public class ChibiVisualTests : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup() => new ChibiLayoutTests().Setup();
        public void Cleanup() => new ChibiLayoutTests().Cleanup();

        [UnityTest]
        public IEnumerator Capture_all_faces_at_rest_blink_and_chew_from_two_angles()
        {
            yield return EditorXR.Stop();yield return SceneManager.LoadSceneAsync("CandyParlour3D");
            for (int i = 0; i < 45; i++) yield return null;
            var camera = new GameObject("Face proof camera").AddComponent<Camera>();
            camera.cullingMask = 1 << 30;camera.orthographic = true;camera.orthographicSize = .35f;
            camera.clearFlags = CameraClearFlags.SolidColor;camera.backgroundColor = new Color(.075f, .10f, .12f);
            var target = RenderTexture.GetTemporary(420, 420, 24);camera.targetTexture = target;
            var panel = new Texture2D(420, 420, TextureFormat.RGB24, false);
            var atlas = new Texture2D(2520, 1260, TextureFormat.RGB24, false);
            var seats = GameObject.Find("BoB_Art3D/Seats").transform;
            for (int angle = 0; angle < 2; angle++)
            {
                camera.transform.position = angle == 0 ? new Vector3(0, 0, 2) : new Vector3(.9f, .06f, 2);
                camera.transform.LookAt(Vector3.zero);
                for (int index = 0; index < 6; index++)
                {
                    var source = seats.Find("Seat_" + index).GetComponentsInChildren<Transform>().First(t => t.name == "Head");
                    var head = Object.Instantiate(source.gameObject);head.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    foreach (var t in head.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
                    var face = head.GetComponentInChildren<SkinnedMeshRenderer>();face.enabled = true;face.forceRenderingOff = false;
                    Assert.Less(face.sharedMesh.vertexCount, 20000, "Face contains overlay geometry");
                    Assert.AreEqual(face.sharedMesh.vertexCount, face.sharedMesh.uv2.Length, "Missing smooth face surface for eyelid lighting");
                    var neutral = face.sharedMesh.vertices;
                    Assert.IsTrue(neutral.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)));
                    for (int pose = 0; pose < 3; pose++)
                    {
                        for (int i = 0; i < face.sharedMesh.blendShapeCount; i++)
                        {
                            string key = face.sharedMesh.GetBlendShapeName(i).Split('.')[^1];
                            face.SetBlendShapeWeight(i, pose == 1 && key == "Blink" ? 100 : pose == 2 && (key == "Open" || key == "Chew") ? 100 : 0);
                        }
                        yield return null;
                        camera.Render();var previous = RenderTexture.active;RenderTexture.active = target;
                        panel.ReadPixels(new Rect(0, 0, 420, 420), 0, 0);panel.Apply();RenderTexture.active = previous;
                        atlas.SetPixels(index * 420, (2 - pose) * 420, 420, 420, panel.GetPixels());
                    }
                    Object.Destroy(head);yield return null;
                }
                atlas.Apply();Directory.CreateDirectory("Logs/NewCharacters");
                File.WriteAllBytes("Logs/NewCharacters/faces-" + (angle == 0 ? "front" : "three-quarter") + ".png", atlas.EncodeToPNG());
            }
            camera.targetTexture = null;RenderTexture.ReleaseTemporary(target);
            Object.Destroy(camera.gameObject);Object.Destroy(panel);Object.Destroy(atlas);
        }
    }
}
