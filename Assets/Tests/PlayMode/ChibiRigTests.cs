using System.Collections;
using System.IO;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    public class ChibiRigTests : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup() => new ChibiLayoutTests().Setup();
        public void Cleanup() => new ChibiLayoutTests().Cleanup();
        static int Shape(Mesh mesh, string name) => Enumerable.Range(0, mesh.blendShapeCount).First(i => mesh.GetBlendShapeName(i) == name || mesh.GetBlendShapeName(i).EndsWith("." + name));
        static float Difference(Vector3[] a, Vector3[] b) => a.Zip(b, Vector3.Distance).Max();
        [UnityTest]
        public IEnumerator All_six_faces_blink_and_chew_and_both_gloves_deform_on_grip()
        {
            yield return EditorXR.Stop();yield return SceneManager.LoadSceneAsync("CandyParlour3D");
            for (int i = 0; i < 60; i++) yield return null;
            var seats = GameObject.Find("BoB_Art3D/Seats").transform;
            var before = new Mesh();var after = new Mesh();
            foreach (Transform seat in seats)
            {
                var anim = seat.GetComponentInChildren<CharacterAnimator>();Assert.IsNotNull(anim);
                var face = seat.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.sharedMesh.blendShapeCount > 0);
                int blink = Shape(face.sharedMesh, "Blink"), open = Shape(face.sharedMesh, "Open"), chew = Shape(face.sharedMesh, "Chew");
                anim.enabled = false;
                for (int i = 0; i < face.sharedMesh.blendShapeCount; i++) face.SetBlendShapeWeight(i, 0);
                face.BakeMesh(before);var neutral = before.vertices;
                face.SetBlendShapeWeight(blink, 100);face.BakeMesh(after);
                Assert.Greater(Difference(neutral, after.vertices), .0005f, seat.name + " eyelid relief does not move");
                Assert.Less(Difference(neutral, after.vertices), .002f, seat.name + " blink tears the face surface");
                Assert.IsNotNull(face.GetComponent<ChibiFace>());
                Assert.AreEqual(1, face.sharedMaterials.Length, "Overlapping face material returned");
                Assert.IsNotNull(face.sharedMaterial.GetTexture("_BlinkMap"));
                yield return null;
                var properties = new MaterialPropertyBlock();face.GetPropertyBlock(properties);
                Assert.AreEqual(1, properties.GetFloat("_FaceBlink"), .001f, "Closed eyelid texture is not driven by blink");
                face.SetBlendShapeWeight(blink, 0);face.SetBlendShapeWeight(open, 80);face.SetBlendShapeWeight(chew, 100);face.BakeMesh(after);
                Assert.Greater(Difference(neutral, after.vertices), .003f, seat.name + " mouth does not chew");
                Assert.Less(Difference(neutral, after.vertices), .02f, seat.name + " mouth distorts the face");
                yield return null;face.GetPropertyBlock(properties);
                Assert.GreaterOrEqual(properties.GetFloat("_FaceMouth"), .79f, "Open mouth artwork is not animated");
                Assert.IsNotNull(face.sharedMaterial.GetTexture("_MouthMap"));
                Assert.IsTrue(after.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)));
                face.SetBlendShapeWeight(open, 0);face.SetBlendShapeWeight(chew, 0);
                anim.enabled = true;anim.Play(CharacterReaction.Eat);
                float peak = 0, blinkPeak = 0;
                anim.blinkInterval = new Vector2(.1f, .2f);
                typeof(CharacterAnimator).GetField("blinkTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(anim, 0f);
                Assert.AreSame(face, typeof(CharacterAnimator).GetField("mouth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(anim), "Animator bound to the wrong face");
                for (float elapsed = 0; elapsed < 1.4f; elapsed += Time.deltaTime) { yield return null;peak = Mathf.Max(peak, face.GetBlendShapeWeight(open));blinkPeak = Mathf.Max(blinkPeak, face.GetBlendShapeWeight(blink)); }
                Assert.Greater(peak, 30, seat.name + " reaction is not wired to mouth rig");
                Assert.Greater(blinkPeak, 80, seat.name + " automatic blink is not wired");
                anim.ResetState();
            }
            var avatar = seats.Find("Seat_0").GetComponentInChildren<SeatedAvatar>();
            var driver = avatar.GetComponent<ChibiHands>();driver.enabled = false;
            var pose = avatar.CurrentPose;pose.leftTracked = pose.rightTracked = true;pose.headTracked = false;
            foreach (var hand in new[] { avatar.left, avatar.right })
            {
                Assert.AreEqual(15, hand.fingers.Length);Assert.IsNotNull(hand.gripAnchor);
                var skin = hand.hand.GetComponentInChildren<SkinnedMeshRenderer>();Assert.IsNotNull(skin, "Glove is not skinned");
                pose.leftGrip = pose.rightGrip = pose.leftIndex = pose.rightIndex = 0;avatar.SetPose(pose);
                for (int i = 0; i < 90; i++) avatar.ApplyPose(1f / 60);
                skin.BakeMesh(before);var neutral = before.vertices;var anchor = hand.gripAnchor.position;
                int distal = System.Array.FindIndex(skin.bones, b => b.name == "Finger_Middle_3");Assert.GreaterOrEqual(distal, 0);
                var weights = skin.sharedMesh.boneWeights;
                int tip = Enumerable.Range(0, weights.Length).Where(i => (weights[i].boneIndex0 == distal && weights[i].weight0 > .5f) || (weights[i].boneIndex1 == distal && weights[i].weight1 > .5f))
                    .OrderByDescending(i => Vector3.Distance(skin.transform.TransformPoint(neutral[i]), hand.hand.position)).First();
                float openDistance = Vector3.Distance(skin.transform.TransformPoint(neutral[tip]), anchor);
                pose.leftGrip = pose.rightGrip = pose.leftIndex = pose.rightIndex = 1;avatar.SetPose(pose);
                for (int i = 0; i < 90; i++) avatar.ApplyPose(1f / 60);
                skin.BakeMesh(after);Assert.Greater(Difference(neutral, after.vertices), .02f, "Fingers do not deform");
                Assert.Less(Vector3.Distance(skin.transform.TransformPoint(after.vertices[tip]), anchor), openDistance * .8f, "Fingers curl away from the candy");
                Assert.Less(Vector3.Distance(anchor, hand.gripAnchor.position), .001f, "Wrist/grip anchor moves while fingers curl");
            }
            pose.leftTracked = pose.rightTracked = false;avatar.SetPose(pose);driver.enabled = true;
            var bot = seats.Find("Seat_3").GetComponentInChildren<ChibiHands>();
            var item = GameObject.CreatePrimitive(PrimitiveType.Sphere);item.transform.localScale = Vector3.one * .035f;
            item.transform.position = bot.right.gripAnchor.position + Vector3.forward * .1f;
            var target = bot.transform.position + Vector3.up * 1.16f;
            var carry = bot.CarryCandy(item.transform, () => target, .7f);int attachedSamples = 0;
            while (carry.MoveNext())
            {
                yield return carry.Current;
                bool attached = (bool)typeof(ChibiHands).GetField("attached", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(bot);
                if (attached) { Assert.Less(Vector3.Distance(item.transform.position, bot.right.gripAnchor.position), .002f);attachedSamples++; }
            }
            Assert.Greater(attachedSamples, 2);Assert.Less(Vector3.Distance(item.transform.position, target), .001f);
            Object.Destroy(item);Object.Destroy(before);Object.Destroy(after);
        }
    }
}
