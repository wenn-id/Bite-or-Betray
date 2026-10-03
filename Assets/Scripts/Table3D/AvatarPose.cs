using System;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    // Coordinates are relative to a seat, never to the local XR tracking space.
    [Serializable]
    public struct AvatarPose
    {
        public Pose head, leftHand, rightHand;
        public bool headTracked, leftTracked, rightTracked;
        public float leftGrip, rightGrip, leftIndex, rightIndex;
        public uint sequence;

        public bool IsValid => Valid(head) && Valid(leftHand) && Valid(rightHand)
            && Finite(leftGrip) && Finite(rightGrip) && Finite(leftIndex) && Finite(rightIndex);

        static bool Valid(Pose p)
        {
            var q = p.rotation;
            float norm = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            return Finite(p.position.x) && Finite(p.position.y) && Finite(p.position.z)
                && p.position.sqrMagnitude < 16 && Finite(norm) && Mathf.Abs(norm - 1) < .02f;
        }

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        public static AvatarPose Lerp(in AvatarPose a, in AvatarPose b, float t)
        {
            var result = b;
            result.head = Blend(a.head, b.head, t);
            result.leftHand = Blend(a.leftHand, b.leftHand, t);
            result.rightHand = Blend(a.rightHand, b.rightHand, t);
            result.leftGrip = Mathf.Lerp(a.leftGrip, b.leftGrip, t);
            result.rightGrip = Mathf.Lerp(a.rightGrip, b.rightGrip, t);
            result.leftIndex = Mathf.Lerp(a.leftIndex, b.leftIndex, t);
            result.rightIndex = Mathf.Lerp(a.rightIndex, b.rightIndex, t);
            return result;
        }

        static Pose Blend(Pose a, Pose b, float t) => new Pose(Vector3.Lerp(a.position, b.position, t), Quaternion.Slerp(a.rotation, b.rotation, t));
    }
}
