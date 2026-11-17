using System.Collections;
using System.Linq;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.TestTools;

namespace BiteOrBetray.Tests.PlayMode
{
    public partial class OfflineMatchTests
    {
        [UnityTest]
        public IEnumerator Palm_mesh_follows_controller_translation_pitch_yaw_and_roll_without_offsets()
        {
            var avatar = director.artRoot.GetComponentInChildren<SeatedAvatar>();
            var tracking = avatar.GetComponent<LocalAvatarTracking>();
            tracking.headTarget = Camera.main.transform;tracking.firstPersonCamera = Camera.main;
            Camera.main.transform.position = avatar.seat.position + Vector3.up * 1.27f;
            Camera.main.transform.rotation = Quaternion.LookRotation(new Vector3(0, .93f, 0) - Camera.main.transform.position);
            Camera.main.fieldOfView = 82;
            foreach (var text in director.GetComponentsInChildren<TextMesh>()) text.gameObject.SetActive(false);
            Assert.AreEqual(Vector3.zero, tracking.controllerWristOffset);
            Write(input, "active", true);
            var controllers = new[] { InputSystem.AddDevice<XRController>(), InputSystem.AddDevice<XRController>() };
            try
            {
                var poses = new Transform[2];
                for (int side = 0; side < 2; side++)
                {
                    InputSystem.SetDeviceUsage(controllers[side], side == 0 ? CommonUsages.LeftHand : CommonUsages.RightHand);
                    using (StateEvent.From(controllers[side], out var state))
                    {
                        controllers[side].isTracked.WriteValueIntoEvent(1f, state);
                        InputState.Change(controllers[side], state, InputState.currentUpdateType);
                    }
                    poses[side] = new GameObject("Test grip anchor " + side).transform;
                    poses[side].SetParent(director.transform);
                    Write(Hand(side), "pose", poses[side]);
                }
                int frame = 0;
                foreach (var rotation in new[] { Vector3.zero, new Vector3(35, 0, 0), new Vector3(0, 85, 0), new Vector3(0, 0, 75), new Vector3(-25, -60, -50) })
                {
                    for (int side = 0; side < 2; side++)
                        poses[side].SetPositionAndRotation(avatar.seat.TransformPoint(new Vector3(side == 0 ? -.23f : .23f, .92f, .35f)),
                            avatar.seat.rotation * Quaternion.Euler(rotation * (side == 0 ? -1 : 1)));
                    Invoke(tracking, "LateUpdate");
                    for (int i = 0; i < 20; i++) avatar.ApplyPose(.02f);
                    for (int side = 0; side < 2; side++)
                    {
                        var hand = side == 0 ? avatar.left : avatar.right;
                        var parts = hand.hand.GetComponentsInChildren<Transform>();
                        var center = parts.Single(t => t.name == "PalmCenter");
                        var forward = parts.Single(t => t.name == "FingerForward");
                        var normal = parts.Single(t => t.name == "PalmNormal");
                        var index = parts.Single(t => t.name == "Finger_Index_1");
                        var little = parts.Single(t => t.name == "Finger_Little_1");
                        Assert.Less(Vector3.Distance(center.position, poses[side].position), .002f, "The rendered palm orbits its controller: " + rotation);
                        Assert.Greater(Vector3.Dot((forward.position - center.position).normalized, poses[side].forward), .999f, "Fingers point away from the controller: " + rotation);
                        Assert.Greater(Vector3.Dot((normal.position - center.position).normalized, -poses[side].up), .999f, "Palm flips during controller movement: " + rotation);
                        float thumbSide = Vector3.Dot(index.position - little.position, poses[side].right);
                        Assert.Greater(thumbSide * (side == 0 ? 1 : -1), .015f,
                            "The thumb must face inward on the correct anatomical hand: " + side + " " + rotation);
                    }
                    yield return null;
                    CapturePolish(Camera.main, "controller-pose-" + frame++ + ".png");
                }
                yield return null;
            }
            finally
            {
                foreach (var controller in controllers) InputSystem.RemoveDevice(controller);
                Write(input, "active", false);
            }
        }

        [UnityTest]
        public IEnumerator Both_gloves_close_all_fingertips_toward_the_candy()
        {
            var avatar = director.artRoot.GetComponentInChildren<SeatedAvatar>();
            avatar.GetComponent<LocalAvatarTracking>().enabled = false;
            var pose = avatar.CurrentPose;pose.leftTracked = pose.rightTracked = true;
            pose.leftHand = new Pose(avatar.left.restPosition, Quaternion.identity);
            pose.rightHand = new Pose(avatar.right.restPosition, Quaternion.identity);
            foreach (var hand in new[] { avatar.left, avatar.right })
            {
                pose.leftGrip = pose.rightGrip = pose.leftIndex = pose.rightIndex = 0;
                avatar.SetPose(pose);for (int i = 0; i < 25; i++) avatar.ApplyPose(.02f);
                var tips = hand.hand.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Tip_")).OrderBy(t => t.name).ToArray();
                Assert.AreEqual(5, tips.Length);
                var open = tips.Select(t => Vector3.Distance(t.position, hand.gripAnchor.position)).ToArray();
                var parts = hand.hand.GetComponentsInChildren<Transform>();
                var palm = parts.Single(t => t.name == "PalmCenter");
                var normal = (parts.Single(t => t.name == "PalmNormal").position - palm.position).normalized;
                var middle = parts.Single(t => t.name == "Finger_Middle_1");
                Assert.Greater(Vector3.Dot(parts.Single(t => t.name == "Tip_Middle").position - middle.position, normal), .015f,
                    "An idle controller hand must remain cupped instead of lying flat.");
                pose.leftGrip = pose.rightGrip = pose.leftIndex = pose.rightIndex = 1;
                avatar.SetPose(pose);for (int i = 0; i < 25; i++) avatar.ApplyPose(.02f);
                var closed = tips.Select(t => Vector3.Distance(t.position, hand.gripAnchor.position)).ToArray();
                TestContext.WriteLine(hand.hand.name + " open: " + string.Join(", ", open) + " closed: " + string.Join(", ", closed));
                Assert.Less(closed.Average(), open.Average() * .8f, "Grip does not bring the fingers around the candy.");
                for (int i = 0; i < tips.Length; i++)
                    Assert.Less(closed[i], open[i], tips[i].name + " bends away from the palm.");
            }
            yield return null;
        }
    }
}
