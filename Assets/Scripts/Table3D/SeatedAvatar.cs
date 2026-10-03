using System;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public sealed class SeatedAvatar : MonoBehaviour
    {
        [Serializable]
        public sealed class Arm
        {
            public Transform shoulder, elbow, hand;
            public Quaternion handRotationOffset = Quaternion.identity;
            public Transform[] fingers;
            public Vector3[] fingerAxes;
            public bool[] indexFingers;
            [NonSerialized] public float upperLength, lowerLength;
            internal Quaternion[] fingerRest;
            internal Vector3 bend, restHand, filteredHand;
            internal Quaternion handRotation;
            internal float missingTime, reacquire, grip, index;
            internal bool wasTracked;
        }

        public Transform seat, spine, head, eyeReference;
        public Arm left = new Arm(), right = new Arm();
        public Renderer[] headRenderers;
        [Header("Seated calibration")]
        [Range(0, 60)] public float torsoDeadZone = 25;
        [Range(30, 100)] public float torsoYawLimit = 70;
        [Range(.1f, 10)] public float torsoFollowSpeed = 3;
        public Vector3 tableCenter;
        public float tableHeight = .759f, tableRadius = .84f;
        public float trackingLossHold = .35f, reacquireSeconds = .12f;
        [Range(0, 31)] public int localHeadLayer = 30;

        AvatarPose pose;
        Quaternion spineRest;
        Vector3 spinePosition;
        float torsoYaw;
        bool initialized;

        public AvatarPose CurrentPose => pose;
        public bool SetPose(in AvatarPose value)
        {
            if (!value.IsValid) return false;
            pose = value;
            return true;
        }

        public void Initialize()
        {
            if (initialized) return;
            if (seat == null) seat = transform.parent != null ? transform.parent : transform;
            if (spine == null || head == null || eyeReference == null) throw new InvalidOperationException("Avatar rig belum dikonfigurasi.");
            spineRest = spine.localRotation;
            spinePosition = spine.localPosition;
            Cache(left, -1); Cache(right, 1);
            pose = new AvatarPose {
                head = Relative(eyeReference), leftHand = new Pose(left.restHand, Quaternion.identity),
                rightHand = new Pose(right.restHand, Quaternion.identity)
            };
            initialized = true;
        }

        void Cache(Arm arm, float side)
        {
            if (arm.shoulder == null || arm.elbow == null || arm.hand == null) throw new InvalidOperationException("Rantai lengan avatar belum lengkap.");
            arm.upperLength = Vector3.Distance(arm.shoulder.position, arm.elbow.position);
            arm.lowerLength = Vector3.Distance(arm.elbow.position, arm.hand.position);
            if (arm.upperLength < .05f || arm.lowerLength < .05f) throw new InvalidOperationException("Panjang tulang lengan avatar tidak valid.");
            arm.bend = seat.TransformDirection(new Vector3(side * .65f, -.7f, -.2f)).normalized;
            arm.restHand = seat.InverseTransformPoint(arm.hand.position);
            arm.filteredHand = arm.hand.position;
            arm.handRotation = arm.hand.rotation;
            arm.fingerRest = new Quaternion[arm.fingers.Length];
            for (int i = 0; i < arm.fingers.Length; i++) arm.fingerRest[i] = arm.fingers[i].localRotation;
        }

        public Pose Relative(Transform target) => new Pose(seat.InverseTransformPoint(target.position), Quaternion.Inverse(seat.rotation) * target.rotation);
        Pose World(Pose target) => new Pose(seat.TransformPoint(target.position), seat.rotation * target.rotation);

        void Awake() => Initialize();
        void LateUpdate() => ApplyPose(Time.unscaledDeltaTime);

        public void SetLocalHeadHidden(Camera camera, bool hidden)
        {
            for (int i = 0; i < headRenderers.Length; i++) headRenderers[i].gameObject.layer = hidden ? localHeadLayer : gameObject.layer;
            if (camera != null)
                camera.cullingMask = hidden ? camera.cullingMask & ~(1 << localHeadLayer) : camera.cullingMask | (1 << localHeadLayer);
        }

        public void ApplyPose(float dt)
        {
            Initialize();
            dt = Mathf.Clamp(dt, 0, .1f);
            Pose headPose = World(pose.head);
            if (pose.headTracked)
            {
                Vector3 forward = pose.head.rotation * Vector3.forward;
                if (forward.x * forward.x + forward.z * forward.z > .02f)
                {
                    float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                    float error = Mathf.DeltaAngle(torsoYaw, yaw);
                    float desired = torsoYaw + Mathf.Sign(error) * Mathf.Max(0, Mathf.Abs(error) - torsoDeadZone);
                    torsoYaw = Mathf.LerpAngle(torsoYaw, Mathf.Clamp(desired, -torsoYawLimit, torsoYawLimit), 1 - Mathf.Exp(-torsoFollowSpeed * dt));
                }
            }
            spine.localPosition = spinePosition;
            Vector3 yawAxis = spine.parent != null ? spine.parent.InverseTransformDirection(seat.up) : seat.up;
            spine.localRotation = Quaternion.AngleAxis(torsoYaw, yawAxis) * spineRest;
            if (pose.headTracked)
            {
                head.rotation = headPose.rotation * Quaternion.Inverse(eyeReference.localRotation);
                head.position = headPose.position - head.TransformVector(eyeReference.localPosition);
            }
            ApplyArm(left, World(pose.leftHand), pose.leftTracked, pose.leftGrip, pose.leftIndex, -1, dt);
            ApplyArm(right, World(pose.rightHand), pose.rightTracked, pose.rightGrip, pose.rightIndex, 1, dt);
        }

        void ApplyArm(Arm arm, Pose target, bool tracked, float grip, float index, float side, float dt)
        {
            if (tracked)
            {
                if (!arm.wasTracked) arm.reacquire = 0;
                arm.reacquire = Mathf.Min(1, arm.reacquire + dt / Mathf.Max(.01f, reacquireSeconds));
                arm.filteredHand = Vector3.Lerp(arm.filteredHand, target.position, arm.reacquire);
                arm.handRotation = Quaternion.Slerp(arm.handRotation, target.rotation * arm.handRotationOffset, arm.reacquire);
                arm.missingTime = 0;
            }
            else
            {
                arm.missingTime += dt;
                if (arm.missingTime > trackingLossHold)
                {
                    arm.filteredHand = Vector3.Lerp(arm.filteredHand, seat.TransformPoint(arm.restHand), 1 - Mathf.Exp(-5 * dt));
                    grip = index = 0;
                }
            }
            arm.wasTracked = tracked;
            Vector3 shoulder = arm.shoulder.position;
            Vector3 direction = arm.filteredHand - shoulder;
            if (direction.sqrMagnitude < 1e-8f) direction = seat.forward;
            direction.Normalize();
            Vector3 pole = Vector3.ProjectOnPlane(seat.TransformDirection(new Vector3(side * .65f, -.7f, -.2f)), direction);
            Vector3 wrist = shoulder + direction * Mathf.Clamp(Vector3.Distance(shoulder, arm.filteredHand),
                Mathf.Abs(arm.upperLength - arm.lowerLength) + .005f, arm.upperLength + arm.lowerLength - .005f);
            Vector3 preliminary = Elbow(shoulder, wrist, arm.upperLength, arm.lowerLength, pole);
            float radius = Vector2.Distance(new Vector2(preliminary.x, preliminary.z), new Vector2(tableCenter.x, tableCenter.z));
            float tableWeight = Mathf.SmoothStep(0, 1, Mathf.Clamp01((tableRadius + .08f - radius) / .12f))
                * Mathf.SmoothStep(0, 1, Mathf.Clamp01((tableHeight + .065f - preliminary.y) / .12f));
            pole = Vector3.Lerp(pole.normalized, Vector3.ProjectOnPlane(seat.up, direction).normalized, tableWeight);
            arm.bend = Vector3.Lerp(arm.bend, pole, 1 - Mathf.Exp(-12 * dt));
            Vector3 elbow = Elbow(shoulder, wrist, arm.upperLength, arm.lowerLength, arm.bend);
            arm.shoulder.rotation = Quaternion.FromToRotation(arm.elbow.position - shoulder, elbow - shoulder) * arm.shoulder.rotation;
            arm.elbow.rotation = Quaternion.FromToRotation(arm.hand.position - arm.elbow.position, wrist - arm.elbow.position) * arm.elbow.rotation;
            arm.hand.rotation = arm.handRotation;
            arm.grip = Mathf.Lerp(arm.grip, Mathf.Clamp01(grip), 1 - Mathf.Exp(-20 * dt));
            arm.index = Mathf.Lerp(arm.index, Mathf.Clamp01(index), 1 - Mathf.Exp(-20 * dt));
            for (int i = 0; i < arm.fingers.Length; i++)
                arm.fingers[i].localRotation = arm.fingerRest[i] * Quaternion.AngleAxis((arm.indexFingers[i] ? arm.index : arm.grip) * 80, arm.fingerAxes[i]);
        }

        public static Vector3 Elbow(Vector3 shoulder, Vector3 wrist, float upper, float lower, Vector3 bend)
        {
            Vector3 delta = wrist - shoulder;
            float distance = Mathf.Max(.0001f, delta.magnitude);
            Vector3 axis = delta / distance;
            float along = Mathf.Clamp((upper * upper - lower * lower + distance * distance) / (2 * distance), -upper, upper);
            Vector3 radial = Vector3.ProjectOnPlane(bend, axis);
            if (radial.sqrMagnitude < 1e-8f) radial = Vector3.ProjectOnPlane(Vector3.down, axis);
            if (radial.sqrMagnitude < 1e-8f) radial = Vector3.ProjectOnPlane(Vector3.right, axis);
            return shoulder + axis * along + radial.normalized * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
        }
    }
}
