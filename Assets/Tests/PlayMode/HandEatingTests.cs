using System;
using System.Collections;
using System.Collections.Generic;
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
        static Vector3 AuthoredLip(ChibiHands hands)
        {
            var head = hands.transform.Find("Head");
            var skin = head.GetComponentInChildren<SkinnedMeshRenderer>();
            // Independent landmarks from bob_chibi_rig.py's authored mouth artwork, in head coordinates.
            float height = skin.sharedMaterial.name switch {
                "Blond" => -.2455f, "Silver" => -.224f, "Mint" => -.231f,
                "Navy" => -.158f, "Peach" => -.2435f, "Charcoal" => -.239f,
                _ => throw new InvalidOperationException(skin.sharedMaterial.name)
            };
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh);
                var candidates = mesh.vertices.Select(p => head.InverseTransformPoint(skin.transform.TransformPoint(p)))
                    .Where(p => Mathf.Abs(p.x) < .03f && Mathf.Abs(p.y - height) < .025f && p.z > .08f)
                    .OrderBy(p => p.x * p.x + (p.y - height) * (p.y - height)).Take(4).ToArray();
                Assert.IsNotEmpty(candidates, skin.name + " has no lip surface at its authored mouth height.");
                return head.TransformPoint(candidates.Aggregate(Vector3.zero, (sum, p) => sum + p) / candidates.Length);
            }
            finally { Object.Destroy(mesh); }
        }

        static void HandAuditShot(Camera camera, string name)
        {
            CapturePolish(camera, "hand-audit-" + name + ".png");
            var folder = Path.Combine("ArtSource/HandEatAudit20261117/Screens",
                Environment.GetEnvironmentVariable("BOB_HAND_AUDIT_STAGE") ?? "verification");
            Directory.CreateDirectory(folder);
            File.Copy("ArtSource/GloveRevision20261116/InGame/hand-audit-" + name + ".png", Path.Combine(folder, name + ".png"), true);
        }

        [UnityTest]
        public IEnumerator Eating_targets_land_on_all_six_enlarged_lip_surfaces()
        {
            yield return StartMatch(5);
            Time.timeScale = 1;
            var errors = new List<float>();
            foreach (var seat in Read<IList>(director, "allSeats"))
            {
                var actor = Read<SeatedAvatar>(seat, "avatar");
                var character = Read<CharacterAnimator>(seat, "character");
                var hands = (actor != null ? actor.gameObject : character.gameObject).GetComponent<ChibiHands>();
                hands.character.enabled = false;
                var expected = AuthoredLip(hands);
                var actual = (Vector3)Invoke(director, "Mouth", seat);
                float error = Vector3.Distance(expected, actual);errors.Add(error);
                TestContext.WriteLine(hands.name + " lip error=" + error.ToString("F4") + "m");
                var head = hands.transform.Find("Head");var camera = Camera.main;
                camera.transform.SetPositionAndRotation(expected + head.forward * .65f + Vector3.up * .02f,
                    Quaternion.LookRotation(-head.forward));camera.fieldOfView = 44;
                var markers = new List<GameObject>();var materials = new List<Material>();
                foreach (var point in new[] { (actual, Color.red), (expected, Color.green) })
                {
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);markers.Add(marker);
                    marker.transform.position = point.Item1;marker.transform.localScale = Vector3.one * .014f;
                    var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));materials.Add(material);material.color = point.Item2;
                    marker.GetComponent<Renderer>().sharedMaterial = material;
                }
                yield return null;HandAuditShot(camera, "mouth-" + hands.name.Replace(' ', '-'));
                foreach (var marker in markers) Object.Destroy(marker);
                foreach (var material in materials) Object.Destroy(material);
            }
            Assert.Less(errors.Max(), .018f, "The eating endpoint misses the visible mouth after scaling.");
        }

        [UnityTest]
        public IEnumerator Both_visible_grips_gate_mouth_snap_and_consume_only_once()
        {
            yield return StartMatch(1);
            Time.timeScale = 1;director.tempo = 1;
            var avatar = director.artRoot.GetComponentInChildren<SeatedAvatar>();
            avatar.GetComponent<LocalAvatarTracking>().enabled = false;
            var hands = avatar.GetComponent<ChibiHands>();
            var camera = Camera.main;
            camera.transform.SetPositionAndRotation(avatar.eyeReference.position, avatar.seat.rotation);
            float oldCapture = Time.captureDeltaTime;Time.captureDeltaTime = 1f / 60;
            try
            {
                yield return null;
                for (int side = 0; side < 2; side++)
                {
                    yield return Until(() => HumanReady, "human turn for hand " + side);
                    var hand = side == 0 ? avatar.left : avatar.right;
                    var device = Hand(side);Grab(device);
                    var pose = avatar.CurrentPose;pose.headTracked = false;
                    pose.leftTracked = side == 0;pose.rightTracked = side == 1;
                    pose.leftGrip = pose.rightGrip = pose.leftIndex = pose.rightIndex = 1;
                    Vector3 mouth = camera.transform.position - camera.transform.up * .08f + camera.transform.forward * .05f;
                    Vector3 offset = hand.gripAnchor.position - hand.hand.position;
                    var anchor = Read<Transform>(device, "pose");
                    anchor.rotation = hand.hand.rotation;
                    int eaten = Engine.Stats.EatSelf;
                    foreach (bool inside in new[] { false, true })
                    {
                        var wanted = mouth + Vector3.down * (input.mouthDistance + (inside ? -.035f : .005f));
                        anchor.position = wanted - offset;
                        var target = avatar.Relative(anchor);
                        if (side == 0) pose.leftHand = target;else pose.rightHand = target;
                        avatar.SetPose(pose);for (int i = 0; i < 30; i++) avatar.ApplyPose(1f / 60);
                        Invoke(hands, "LateUpdate");
                        Assert.Less(Vector3.Distance(Candy.transform.position, hand.gripAnchor.position), .002f);
                        Write(input, "actedThisFrame", false);
                        Invoke(input, "ProcessHeldCandy", true);
                        if (!inside)
                        {
                            Assert.IsTrue(input.IsHoldingCandy, "Candy eaten while its visible grip was outside the mouth radius: hand " + side);
                            Assert.AreEqual(eaten, Engine.Stats.EatSelf);
                        }
                    }
                    Assert.IsFalse(input.IsHoldingCandy);
                    yield return Until(() => Engine.Stats.EatSelf == eaten + 1 && HumanReady, "one candy eaten from hand " + side);
                    Assert.AreEqual(eaten + 1, Engine.Stats.EatSelf);
                }
            }
            finally { Time.captureDeltaTime = oldCapture; }
        }

        [UnityTest]
        public IEnumerator All_six_eat_animations_keep_pickup_attachment_and_return_the_hand()
        {
            var measurements = new List<(string name, float pickup, float lip, float returned, float returnStep)>();
            float oldCapture = Time.captureDeltaTime;Time.captureDeltaTime = 1f / 60;
            try
            {
                for (int index = 0; index < 6; index++)
                {
                    Time.timeScale = 20;director.tempo = 4;
                    yield return StartMatch(5);
                    Time.timeScale = 1;director.tempo = 1;director.thinkSeconds = new Vector2(30, 30);
                    var seat = Read<IList>(director, "allSeats")[index];
                    var avatar = Read<SeatedAvatar>(seat, "avatar");
                    var character = Read<CharacterAnimator>(seat, "character");
                    var hands = (avatar != null ? avatar.gameObject : character.gameObject).GetComponent<ChibiHands>();
                    var rest = hands.right.hand.localPosition;
                    var candy = Candy.transform;var home = candy.position;
                    if (avatar != null) Select(Candy);
                    else Select((TableSelectable)Read<IDictionary>(director, "botTargets")[seat]);
                    yield return Until(() => Read<bool>(hands, "carrying"), "carry starts for " + hands.name);
                    int attached = 0;float pickup = 0, returnStep = 0;float deadline = Time.realtimeSinceStartup + 5;
                    Vector3 previous = hands.right.hand.position;
                    var camera = Camera.main;var head = hands.transform.Find("Head");
                    var center = (home + AuthoredLip(hands)) * .5f;
                    var cameraPosition = center + head.forward * .8f + head.right * .85f + Vector3.up * .65f;
                    camera.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(center - cameraPosition));
                    camera.fieldOfView = 64;
                    while (Read<bool>(hands, "carrying"))
                    {
                        Assert.Less(Time.realtimeSinceStartup, deadline);
                        yield return null;
                        if (!Read<bool>(hands, "carrying")) returnStep = Vector3.Distance(previous, hands.right.hand.position);
                        previous = hands.right.hand.position;
                        if (!Read<bool>(hands, "attached")) continue;
                        if (attached++ == 0) { pickup = Vector3.Distance(home, candy.position);HandAuditShot(camera, "pickup-" + index); }
                        Assert.Less(Vector3.Distance(candy.position, hands.right.gripAnchor.position), .003f, hands.name + " loses the attached candy");
                        if (attached == 12) HandAuditShot(camera, "carry-" + index);
                    }
                    Assert.Greater(attached, 3);
                    float lip = Vector3.Distance(candy.position, AuthoredLip(hands));
                    HandAuditShot(camera, "mouth-arrival-" + index);
                    for (float t = 0; t < .6f; t += Time.deltaTime)
                    {
                        yield return null;
                        returnStep = Mathf.Max(returnStep, Vector3.Distance(previous, hands.right.hand.position));
                        previous = hands.right.hand.position;
                    }
                    float returned = Vector3.Distance(rest, hands.right.hand.localPosition);
                    HandAuditShot(camera, "returned-" + index);
                    measurements.Add((hands.name, pickup, lip, returned, returnStep));
                    TestContext.WriteLine(hands.name + $" pickup jump={pickup:F4}m, lip error={lip:F4}m, rest error={returned:F4}m, return step={returnStep:F4}m");
                    director.ReturnToMenu();yield return null;
                }
                foreach (var m in measurements)
                {
                    Assert.Less(m.pickup, .025f, m.name + " candy jumps at pickup");
                    Assert.Less(m.lip, .025f, m.name + " candy does not reach its visible mouth");
                    Assert.Less(m.returned, .035f, m.name + " hand remains displaced after eating");
                    Assert.Less(m.returnStep, .09f, m.name + " hand teleports back after eating at 60 simulation FPS");
                }
            }
            finally { Time.captureDeltaTime = oldCapture; }
        }

        [UnityTest]
        public IEnumerator Reacquired_controller_pose_takes_priority_during_eating()
        {
            var avatar = director.artRoot.GetComponentInChildren<SeatedAvatar>();
            avatar.GetComponent<LocalAvatarTracking>().enabled = false;
            var hands = avatar.GetComponent<ChibiHands>();
            foreach (bool afterPickup in new[] { false, true })
            {
                Time.timeScale = 20;director.tempo = 4;
                var pose = avatar.CurrentPose;pose.leftTracked = pose.rightTracked = false;avatar.SetPose(pose);
                yield return StartMatch(1);
                Time.timeScale = 1;director.tempo = 1;
                Select(Candy);
                yield return Until(() => Read<bool>(hands, afterPickup ? "attached" : "carrying"), "auto carry before tracking returns");
                pose = avatar.CurrentPose;pose.rightTracked = true;
                pose.rightHand = new Pose(new Vector3(.3f, 1.03f, .38f), Quaternion.Euler(25, 35, -15));
                avatar.SetPose(pose);
                for (int i = 0; i < 30; i++) avatar.ApplyPose(1f / 60);
                yield return null;
                Assert.IsFalse(Read<bool>(hands, "carrying"), "Auto eating still owns the hand after tracking returns.");
                Assert.Less(Vector3.Distance(avatar.right.hand.position, avatar.seat.TransformPoint(pose.rightHand.position)), .002f);
                director.ReturnToMenu();yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Remote_character_clones_keep_their_own_mouth_calibration()
        {
            yield return StartMatch(5);
            var seats = Read<IList>(director, "seats");
            var expected = seats.Cast<object>().Select(s => Read<CharacterAnimator>(s, "character")?.transform.Find("Head/MouthAnchor")?.localPosition).ToArray();
            var bindings = Enumerable.Range(0, 6).Select(i => new TableSeat("Player " + i, i == 0 ? SeatController.Local : SeatController.Online, (ulong)i)).ToArray();
            int previous = Read<int>(director, "localConfigIndex");Write(director, "localConfigIndex", 0);
            try
            {
                Invoke(director, "ConfigureNetworkAvatars", (object)bindings);
                yield return null;
                for (int i = 1; i < 6; i++)
                {
                    var avatar = Read<SeatedAvatar>(seats[i], "avatar");Assert.IsNotNull(avatar);
                    var anchor = avatar.head.Find("MouthAnchor");Assert.IsNotNull(anchor);
                    Assert.Less(Vector3.Distance(expected[i].Value, anchor.localPosition), .0001f, "Remote mouth inherits the local player's face instead of its own.");
                    Assert.Less(Vector3.Distance((Vector3)Invoke(director, "Mouth", seats[i]), AuthoredLip(avatar.GetComponent<ChibiHands>())), .018f);
                }
            }
            finally { Invoke(director, "RestoreNetworkAvatars");Write(director, "localConfigIndex", previous); }
        }
    }
}
