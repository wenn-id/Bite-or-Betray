using System;
using System.IO;
using UnityEngine;

namespace BiteOrBetray.Table3D.Net
{
    // Pose dikuantisasi menjadi 51 byte. Header sesi dan interaksi dikirim terpisah.
    public static class AvatarVisualWire
    {
        public const int PoseBytes = 51;

        public static void WritePose(BinaryWriter writer, in AvatarPose pose)
        {
            if (!pose.IsValid) throw new IOException("Pose tidak valid.");
            writer.Write(pose.sequence);
            WriteTransform(writer, pose.head);
            WriteTransform(writer, pose.leftHand);
            WriteTransform(writer, pose.rightHand);
            writer.Write((byte)((pose.headTracked ? 1 : 0) | (pose.leftTracked ? 2 : 0) | (pose.rightTracked ? 4 : 0)));
            writer.Write(Analog(pose.leftGrip)); writer.Write(Analog(pose.rightGrip));
            writer.Write(Analog(pose.leftIndex)); writer.Write(Analog(pose.rightIndex));
        }

        public static AvatarPose ReadPose(BinaryReader reader)
        {
            var pose = new AvatarPose { sequence = reader.ReadUInt32(), head = ReadTransform(reader),
                leftHand = ReadTransform(reader), rightHand = ReadTransform(reader) };
            byte flags = reader.ReadByte();
            if (flags > 7) throw new IOException("Flag tracking tidak valid.");
            pose.headTracked = (flags & 1) != 0; pose.leftTracked = (flags & 2) != 0; pose.rightTracked = (flags & 4) != 0;
            pose.leftGrip = reader.ReadByte() / 255f; pose.rightGrip = reader.ReadByte() / 255f;
            pose.leftIndex = reader.ReadByte() / 255f; pose.rightIndex = reader.ReadByte() / 255f;
            if (!pose.IsValid) throw new IOException("Pose di luar jangkauan kursi.");
            return pose;
        }

        public static void WriteVector(BinaryWriter writer, Vector3 value)
        {
            writer.Write(Quantize(value.x, 1000)); writer.Write(Quantize(value.y, 1000)); writer.Write(Quantize(value.z, 1000));
        }

        public static Vector3 ReadVector(BinaryReader reader) => new Vector3(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()) / 1000f;

        // Skala lokal dikirim sebagai half (6 byte juga): root FBX dari Blender berskala 100, jauh di luar jangkauan
        // milimeter WriteVector (maks. 32,767), sedangkan permen cadangan berskala 0,05.
        public static void WriteScale(BinaryWriter writer, Vector3 value)
        {
            writer.Write(Half(value.x)); writer.Write(Half(value.y)); writer.Write(Half(value.z));
        }

        public static Vector3 ReadScale(BinaryReader reader)
        {
            var value = new Vector3(Mathf.HalfToFloat(reader.ReadUInt16()), Mathf.HalfToFloat(reader.ReadUInt16()),
                Mathf.HalfToFloat(reader.ReadUInt16()));
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z)) throw new IOException("Skala tidak valid.");
            return value;
        }

        public static void WriteTransform(BinaryWriter writer, Pose pose)
        {
            WriteVector(writer, pose.position);
            var q = pose.rotation.normalized;
            writer.Write(Quantize(q.x, 32767)); writer.Write(Quantize(q.y, 32767));
            writer.Write(Quantize(q.z, 32767)); writer.Write(Quantize(q.w, 32767));
        }

        public static Pose ReadTransform(BinaryReader reader)
        {
            Vector3 position = ReadVector(reader);
            var q = new Quaternion(reader.ReadInt16() / 32767f, reader.ReadInt16() / 32767f,
                reader.ReadInt16() / 32767f, reader.ReadInt16() / 32767f);
            float norm = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (Mathf.Abs(norm - 1) > .02f) throw new IOException("Rotasi tidak valid.");
            return new Pose(position, q.normalized);
        }

        static byte Analog(float value) => (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static ushort Half(float value)
        {
            if (!Finite(value) || Mathf.Abs(value) > 65504) throw new IOException("Skala tidak valid.");
            return Mathf.FloatToHalf(value);
        }
        static short Quantize(float value, float scale)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Mathf.Abs(value * scale) > short.MaxValue)
                throw new IOException("Koordinat tidak valid.");
            return (short)Mathf.RoundToInt(value * scale);
        }
    }
}
