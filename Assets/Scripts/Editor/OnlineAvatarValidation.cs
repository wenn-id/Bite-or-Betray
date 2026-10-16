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

        [MenuItem("Bite or Betray/Cek Codec Avatar Online")]
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
            Require(bytes.Length == AvatarVisualWire.PoseBytes, "ukuran pose harus 51 byte");
            var decoded = Decode(bytes);
            Require(decoded.IsValid && decoded.sequence == source.sequence, "round-trip sequence/validasi gagal");
            CheckTransform(source.head, decoded.head); CheckTransform(source.leftHand, decoded.leftHand); CheckTransform(source.rightHand, decoded.rightHand);
            Require(decoded.headTracked && decoded.leftTracked && !decoded.rightTracked, "flag tracking berubah");
            Require(Mathf.Abs(decoded.leftGrip - source.leftGrip) <= 1f / 255 && decoded.rightGrip == 1
                && decoded.leftIndex == 0 && Mathf.Abs(decoded.rightIndex - source.rightIndex) <= 1f / 255, "analog berubah");
            Reject(() => Decode(new byte[AvatarVisualWire.PoseBytes]), "rotasi nol diterima");
            Reject(() => Decode(new byte[5]), "paket terpotong diterima");
            var flags = (byte[])bytes.Clone(); flags[46] = 8;
            Reject(() => Decode(flags), "flag ilegal diterima");
            var invalid = source; invalid.head.position.x = float.NaN;
            Reject(() => Encode(invalid), "NaN diterima");
            invalid = source; invalid.head.position = Vector3.right * 4.1f;
            Reject(() => Encode(invalid), "jangkauan ilegal diterima");

            var root = new GameObject("Online Avatar Validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var remote = root.AddComponent<RemoteAvatarRepresentation>();
                Require(remote.ReceivePose(decoded), "remote menolak hasil kompresi");
                Require(!remote.ReceivePose(decoded), "remote menerima duplikat");
                decoded.sequence = 0;
                Require(remote.ReceivePose(decoded), "sequence wrap ditolak");
                Require(!remote.ReceivePose(source), "paket sebelum wrap diterima");
                remote.ResetStream();
                Require(remote.ReceivePose(source), "stream baru tidak pulih");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("[BoB] PASS codec avatar: 51 byte, error posisi <1 mm, rotasi <.1 derajat, analog 8-bit, flag, NaN/jangkauan, paket rusak, duplikat, sequence wrap/reset.");
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
            Require(Vector3.Distance(source.position, decoded.position) < .001f, "error posisi >=1 mm");
            Require(Quaternion.Angle(source.rotation, decoded.rotation) < .1f, "error rotasi >=.1 derajat");
        }

        static void Reject(Action action, string message)
        {
            try { action(); }
            catch (IOException) { return; }
            throw new InvalidOperationException("Online avatar check: " + message);
        }
    }
}
