using System;
using System.Collections;
using System.IO;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    public partial class OfflineMatchTests
    {
        [UnityTest]
        public IEnumerator Polished_gloves_cloche_and_ceiling_keep_the_Quest_geometry_budget()
        {
            var root = director.artRoot;
            var candies = root.Find("Dispenser").GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f.name == "Candy 0" || f.name == "Candy 1" || f.name == "Candy 2").ToArray();
            Assert.AreEqual(44, candies.Length);
            Assert.AreEqual(1, candies.Select(f => f.sharedMesh).Distinct().Count());
            foreach (var candy in candies)
            {
                Assert.LessOrEqual(candy.sharedMesh.GetIndexCount(0) / 3, 168);
                Assert.Less(Vector3.Distance(Vector3.one, candy.sharedMesh.bounds.size), .001f);
            }
            int triangles = 0;
            foreach (var hands in root.GetComponentsInChildren<ChibiHands>(true))
                foreach (var hand in new[] { hands.left, hands.right })
                {
                    var skin = hand.hand.GetComponentInChildren<SkinnedMeshRenderer>();
                    int count = (int)skin.sharedMesh.GetIndexCount(0) / 3;
                    Assert.LessOrEqual(count, 3600);triangles += count;
                    Assert.GreaterOrEqual(skin.bones.Length, 16);
                    Assert.AreEqual(15, hand.fingers.Length);
                    Assert.AreEqual(1, skin.sharedMaterials.Length);
                    Assert.LessOrEqual(skin.sharedMaterial.mainTexture.width, 512);
                    var middle = hand.fingers.Single(f => f.name == "Finger_Middle_1");
                    var tip = hand.fingers.Single(f => f.name == "Finger_Middle_2");
                    var center = Vector3.ProjectOnPlane(root.position - hands.transform.position, Vector3.up).normalized;
                    Assert.Greater(Vector3.Dot((tip.position - middle.position).normalized, center), .65f);
                    var baked = new Mesh();skin.BakeMesh(baked);
                    var points = baked.vertices.Select(v => hand.hand.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
                    Assert.Greater(points.Max(v => v.y) - points.Min(v => v.y), .04f, "Glove lost its palm volume.");
                    Object.Destroy(baked);
                }
            Assert.LessOrEqual(triangles, 43200);
            var cloche = root.Find("Waiter/Head/Serving Cloche");
            Assert.IsNotNull(cloche);
            Assert.LessOrEqual(cloche.GetComponentInChildren<MeshFilter>().sharedMesh.GetIndexCount(0) / 3, 1200);
            Assert.Greater(cloche.GetComponentInChildren<Renderer>().bounds.size.x, .60f);
            var ceiling = root.Find("Room/Coffered Ceiling");
            Assert.IsNotNull(ceiling);
            Assert.AreEqual(8, ceiling.Cast<Transform>().Count(t => t.name == "Teal coffer beam"));
            Assert.Less(ceiling.GetComponentsInChildren<MeshFilter>().Sum(m => (long)m.sharedMesh.GetIndexCount(0) / 3), 1200);
            yield return null;
            var camera = Camera.main;
            var waiter = root.Find("Waiter");
            camera.transform.SetPositionAndRotation(waiter.position + waiter.forward * 3 + Vector3.up * 1.65f,
                Quaternion.LookRotation(waiter.position + Vector3.up * 1.15f - (waiter.position + waiter.forward * 3 + Vector3.up * 1.65f)));
            camera.fieldOfView = 48;
            yield return null;CapturePolish(camera, "waiter-cloche.png");
            camera.transform.position = root.Find("Seats/Seat_0").position + Vector3.up * 1.27f;
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, 3.48f, 0) - camera.transform.position);
            camera.fieldOfView = 80;
            yield return null;CapturePolish(camera, "ceiling.png");
        }

        [UnityTest]
        public IEnumerator Tracked_gloves_follow_controller_pose_and_close_without_moving_the_wrist()
        {
            var avatar = director.artRoot.GetComponentInChildren<SeatedAvatar>();
            avatar.GetComponent<LocalAvatarTracking>().enabled = false;
            var pose = avatar.CurrentPose;
            pose.leftTracked = pose.rightTracked = true;
            pose.leftHand = new Pose(new Vector3(-.22f, .90f, .30f), Quaternion.identity);
            pose.rightHand = new Pose(new Vector3(.22f, .90f, .30f), Quaternion.identity);
            pose.leftGrip = pose.rightGrip = pose.leftIndex = pose.rightIndex = 0;
            Assert.IsTrue(avatar.SetPose(pose));
            for (int i = 0; i < 20; i++) avatar.ApplyPose(.02f);
            var rest = avatar.right.fingers.Select(f => f.localRotation).ToArray();
            var wrist = avatar.right.hand.position;
            Assert.Less(Vector3.Distance(wrist, avatar.seat.TransformPoint(pose.rightHand.position)), .001f);
            Assert.Less(Quaternion.Angle(avatar.right.hand.rotation, avatar.seat.rotation * pose.rightHand.rotation * avatar.right.handRotationOffset), .1f);
            pose.leftGrip = pose.rightGrip = pose.leftIndex = pose.rightIndex = 1;
            Assert.IsTrue(avatar.SetPose(pose));
            for (int i = 0; i < 25; i++) avatar.ApplyPose(.02f);
            Assert.Less(Vector3.Distance(wrist, avatar.right.hand.position), .001f);
            Assert.Greater(avatar.right.fingers.Select((f, i) => Quaternion.Angle(rest[i], f.localRotation)).Min(), 15);
            var sample = (GameObject)Invoke(director, "SpawnCandy", CandyColor.Blue, avatar.right.gripAnchor.position);
            sample.transform.SetParent(avatar.right.gripAnchor, true);sample.transform.localPosition = Vector3.zero;
            foreach (var text in director.GetComponentsInChildren<TextMesh>()) text.gameObject.SetActive(false);
            yield return null;
            var camera = Camera.main;
            avatar.SetLocalHeadHidden(camera, true);
            camera.transform.position = avatar.seat.position + Vector3.up * 1.27f;
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, .93f, 0) - camera.transform.position);
            camera.fieldOfView = 82;
            yield return null;CapturePolish(camera, "gloves-gripping.png");
            var position = camera.transform.position;var rotation = camera.transform.rotation;
            camera.transform.position = avatar.right.gripAnchor.position + avatar.seat.forward * .25f + avatar.seat.right * .18f + Vector3.up * .10f;
            camera.transform.rotation = Quaternion.LookRotation(avatar.right.gripAnchor.position - camera.transform.position);
            camera.fieldOfView = 46;
            yield return null;CapturePolish(camera, "glove-candy-detail.png");
            camera.transform.SetPositionAndRotation(position, rotation);camera.fieldOfView = 82;
            Object.Destroy(sample);
            pose.leftGrip = pose.rightGrip = pose.leftIndex = pose.rightIndex = 0;
            avatar.SetPose(pose);
            for (int i = 0; i < 25; i++) avatar.ApplyPose(.02f);
            yield return null;CapturePolish(camera, "gloves-relaxed.png");
        }

        [UnityTest]
        public IEnumerator Gameplay_status_stays_on_the_table_and_replaces_duplicate_effects()
        {
            yield return StartMatch(5);
            Time.timeScale = 1;
            var camera = Camera.main;
            camera.fieldOfView = 82;
            camera.transform.position = director.artRoot.Find("Seats/Seat_0").position + Vector3.up * 1.27f;
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, 1.02f, 0) - camera.transform.position);
            Engine.DebugSetHp(Human, 3);
            yield return null;
            foreach (var label in director.GetComponentsInChildren<TextMesh>().Where(t => t.name.StartsWith("Label ")))
            {
                Assert.Less(label.transform.position.y, .86f, label.name + " moved off the tabletop.");
                StringAssert.Contains("♥", label.text);
                Assert.LessOrEqual(label.GetComponent<Renderer>().bounds.size.x, .31f);
            }
            Assert.IsFalse(director.GetComponentsInChildren<TextMesh>().Any(t => t.name == "Waiter Announcement"));
            var origin = Read<Transform>(Read<object>(director, "human"), "mat").position + Vector3.up * .4f;
            Invoke(director, "Popup", origin, "-1 HP", Color.red, 4f);
            Invoke(director, "Popup", origin, Strings.ParalyzedPopup.Text, new Color(.44f,.25f,.55f), 4f);
            yield return null;
            var feedback = director.GetComponentsInChildren<TextMesh>().Where(t => t.name == "Popup" && t.gameObject.activeInHierarchy).ToArray();
            Assert.IsFalse(feedback.Any(t => t.text == "-1 HP"), "The previous effect in the same local slot must be replaced.");
            Assert.AreEqual(1, feedback.Count(t => t.text == Strings.ParalyzedPopup.Text));
            var caption = Text("Narration");
            Assert.IsFalse(caption.gameObject.activeSelf, "A local effect and narration must share one notice slot.");
            Assert.Less(caption.transform.position.y, .94f);
            var anchor = caption.transform.position;
            camera.transform.rotation = Quaternion.LookRotation(Vector3.up + Vector3.forward);
            yield return null;
            Assert.Less(Vector3.Distance(anchor, caption.transform.position), .001f, "Narration follows the view instead of staying on the table.");
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, 1.02f, 0) - camera.transform.position);
            director.ShowMessage(Strings.YouStillParalyzed.Text);
            yield return null;CapturePolish(camera, "gameplay-player.png");
            Write(director, "captionUntil", Time.unscaledTime - 1);
            var oldMode = Read<object>(director, "mode");
            Write(director, "mode", Activator.CreateInstance(oldMode.GetType()));
            Invoke(director, "LateUpdate");
            Assert.IsEmpty(caption.text, "Finished notifications must not remain on screen.");
            Write(director, "mode", oldMode);
            camera.GetComponent<ArtPreviewControls>().SetTableOverview();
            yield return null;CapturePolish(camera, "gameplay-overview.png");
        }

        static void CapturePolish(Camera camera, string name)
        {
            const string directory = "ArtSource/GloveRevision20261116/InGame";
            Directory.CreateDirectory(directory);
            var target = RenderTexture.GetTemporary(1680, 1050, 24);
            var old = RenderTexture.active;var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;camera.Render();RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);image.Apply();
                File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());Object.Destroy(image);
            }
            finally { camera.targetTexture = previous;RenderTexture.active = old;RenderTexture.ReleaseTemporary(target); }
        }
    }
}
