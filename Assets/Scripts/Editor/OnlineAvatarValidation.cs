using System;
using System.IO;
using BiteOrBetray.Table3D;
using BiteOrBetray.Table3D.Net;
using UnityEditor;
using UnityEngine;

namespace BiteOrBetray.EditorTools
{
    public static class OnlineAvatarValidation
    {
        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Online avatar check: " + message);
        }

        [MenuItem("Bite or Betray/Check Online Avatar Codec")]
        public static void Check()
        {
            var source = new AvatarPose {
                head = new Pose(new Vector3(.1234f, 1.2345f, -.0637f), Quaternion.Euler(30, -170, 45)),
                leftHand = new Pose(new Vector3(-.3456f, .7891f, .4567f), Quaternion.Euler(-90, 20, 120)),
                rightHand = new Pose(new Vector3(.2468f, .8543f, -.1723f), Quaternion.Euler(180, 90, 0)),
                headTracked = true, leftTracked = true, rightTracked = false,
                leftGrip = .734f, rightGrip = 1, leftIndex = 0, rightIndex = .412f, sequence = uint.MaxValue
            };
            byte[] bytes = Encode(source);
            Require(bytes.Length == AvatarVisualWire.PoseBytes, "pose size must be 51 bytes");
            var decoded = Decode(bytes);
            Require(decoded.IsValid && decoded.sequence == source.sequence, "sequence round-trip/validation failed");
            CheckTransform(source.head, decoded.head); CheckTransform(source.leftHand, decoded.leftHand); CheckTransform(source.rightHand, decoded.rightHand);
            Require(decoded.headTracked && decoded.leftTracked && !decoded.rightTracked, "tracking flags changed");
            Require(Mathf.Abs(decoded.leftGrip - source.leftGrip) <= 1f / 255 && decoded.rightGrip == 1
                && decoded.leftIndex == 0 && Mathf.Abs(decoded.rightIndex - source.rightIndex) <= 1f / 255, "analog values changed");
            Reject(() => Decode(new byte[AvatarVisualWire.PoseBytes]), "zero rotation accepted");
            Reject(() => Decode(new byte[5]), "truncated packet accepted");
            var flags = (byte[])bytes.Clone(); flags[46] = 8;
            Reject(() => Decode(flags), "illegal flags accepted");
            var invalid = source; invalid.head.position.x = float.NaN;
            Reject(() => Encode(invalid), "NaN accepted");
            invalid = source; invalid.head.position = Vector3.right * 4.1f;
            Reject(() => Encode(invalid), "illegal range accepted");

            var root = new GameObject("Online Avatar Validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var remote = root.AddComponent<RemoteAvatarRepresentation>();
                Require(remote.ReceivePose(decoded), "remote rejected compressed output");
                Require(!remote.ReceivePose(decoded), "remote accepted a duplicate");
                decoded.sequence = 0;
                Require(remote.ReceivePose(decoded), "sequence wrap rejected");
                Require(!remote.ReceivePose(source), "pre-wrap packet accepted");
                remote.ResetStream();
                Require(remote.ReceivePose(source), "new stream did not recover");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("[BoB] PASS avatar codec: 51 bytes, position error <1 mm, rotation <.1 degrees, 8-bit analog, flags, NaN/range, corrupt packets, duplicates, sequence wrap/reset.");
        }

        static byte[] Encode(AvatarPose pose)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            AvatarVisualWire.WritePose(writer, pose);
            return stream.ToArray();
        }

        static AvatarPose Decode(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes, false));
            return AvatarVisualWire.ReadPose(reader);
        }

        static void CheckTransform(Pose source, Pose decoded)
        {
            Require(Vector3.Distance(source.position, decoded.position) < .001f, "position error >=1 mm");
            Require(Quaternion.Angle(source.rotation, decoded.rotation) < .1f, "rotation error >=.1 degrees");
        }

        static void Reject(Action action, string message)
        {
            try { action(); }
            catch (IOException) { return; }
            throw new InvalidOperationException("Online avatar check: " + message);
        }
    }
}
