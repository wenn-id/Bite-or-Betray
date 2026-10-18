using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Avatar floating head + floating hands. Kepala persis di pose headset, tangan persis di pose controller;
    /// tidak ada badan/lengan, jadi tidak perlu IK. Pose datang dari LocalAvatarTracking (XR lokal) atau
    /// RemoteAvatarRepresentation (jaringan), selalu relatif ke kursi.
    /// </summary>
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public sealed class SeatedAvatar : MonoBehaviour
    {
        [Serializable]
        public sealed class Hand
        {
            public Transform hand;
            public Quaternion handRotationOffset = Quaternion.identity;
            public Transform[] fingers;
            public Vector3[] fingerAxes;
            public bool[] indexFingers;
            [Tooltip("Posisi istirahat (relatif ke kursi) saat tracking hilang: di atas meja, di depan kursi.")]
            public Vector3 restPosition;
            internal Quaternion[] fingerRest;
            internal Vector3 position;
            internal Quaternion rotation;
            internal float missingTime, reacquire, grip, index;
            internal bool wasTracked;
        }

        public Transform seat, head, eyeReference;
        public Hand left = new Hand(), right = new Hand();
        public Renderer[] headRenderers;
        public float trackingLossHold = .35f, reacquireSeconds = .12f;
        [Range(0, 31)] public int localHeadLayer = 30;

        AvatarPose pose;
        bool initialized;
        SpectatorAppearance spectatorAppearance;

        public void SetSpectator(bool value)
        {
            if (value && spectatorAppearance == null) spectatorAppearance = new SpectatorAppearance(transform);
            spectatorAppearance?.SetSpectator(value);
        }

        void OnDestroy() => spectatorAppearance?.Dispose();

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
            if (head == null || eyeReference == null) throw new InvalidOperationException("Avatar rig belum dikonfigurasi.");
            Cache(left); Cache(right);
            pose = new AvatarPose {
                head = Relative(eyeReference),
                leftHand = new Pose(left.restPosition, Quaternion.identity),
                rightHand = new Pose(right.restPosition, Quaternion.identity)
            };
            initialized = true;
        }

        void Cache(Hand hand)
        {
            if (hand.hand == null) throw new InvalidOperationException("Tangan avatar belum dikonfigurasi.");
            hand.position = seat.TransformPoint(hand.restPosition);
            hand.rotation = seat.rotation * hand.handRotationOffset;
            hand.fingerRest = new Quaternion[hand.fingers.Length];
            for (int i = 0; i < hand.fingers.Length; i++) hand.fingerRest[i] = hand.fingers[i].localRotation;
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
            if (pose.headTracked)
            {
                Pose headPose = World(pose.head);
                head.rotation = headPose.rotation * Quaternion.Inverse(eyeReference.localRotation);
                head.position = headPose.position - head.TransformVector(eyeReference.localPosition);
            }
            ApplyHand(left, World(pose.leftHand), pose.leftTracked, pose.leftGrip, pose.leftIndex, dt);
            ApplyHand(right, World(pose.rightHand), pose.rightTracked, pose.rightGrip, pose.rightIndex, dt);
        }

        void ApplyHand(Hand hand, Pose target, bool tracked, float grip, float index, float dt)
        {
            if (tracked)
            {
                // Tracking punya prioritas: setelah transisi singkat saat reacquire, tangan persis di controller.
                if (!hand.wasTracked) hand.reacquire = 0;
                hand.reacquire = Mathf.Min(1, hand.reacquire + dt / Mathf.Max(.01f, reacquireSeconds));
                hand.position = Vector3.Lerp(hand.position, target.position, hand.reacquire);
                hand.rotation = Quaternion.Slerp(hand.rotation, target.rotation * hand.handRotationOffset, hand.reacquire);
                hand.missingTime = 0;
            }
            else
            {
                hand.missingTime += dt;
                if (hand.missingTime > trackingLossHold)
                {
                    float t = 1 - Mathf.Exp(-5 * dt);
                    hand.position = Vector3.Lerp(hand.position, seat.TransformPoint(hand.restPosition), t);
                    hand.rotation = Quaternion.Slerp(hand.rotation, seat.rotation * hand.handRotationOffset, t);
                    grip = index = 0;
                }
            }
            hand.wasTracked = tracked;
            hand.hand.SetPositionAndRotation(hand.position, hand.rotation);
            hand.grip = Mathf.Lerp(hand.grip, Mathf.Clamp01(grip), 1 - Mathf.Exp(-20 * dt));
            hand.index = Mathf.Lerp(hand.index, Mathf.Clamp01(index), 1 - Mathf.Exp(-20 * dt));
            for (int i = 0; i < hand.fingers.Length; i++)
                hand.fingers[i].localRotation = hand.fingerRest[i] * Quaternion.AngleAxis((hand.indexFingers[i] ? hand.index : hand.grip) * 80, hand.fingerAxes[i]);
        }
    }

    // Dipakai avatar tracking dan karakter bot. Salinan material runtime tidak mengubah aset atau pemain lain.
    internal sealed class SpectatorAppearance : IDisposable
    {
        readonly Renderer[] renderers;
        readonly Material[][] originals, faded;
        readonly ShadowCastingMode[] shadows;
        readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        bool spectator;

        public SpectatorAppearance(Transform root)
        {
            renderers = root.GetComponentsInChildren<Renderer>(true);
            originals = new Material[renderers.Length][];
            faded = new Material[renderers.Length][];
            shadows = new ShadowCastingMode[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originals[i] = renderers[i].sharedMaterials;
                shadows[i] = renderers[i].shadowCastingMode;
                faded[i] = new Material[originals[i].Length];
                for (int j = 0; j < originals[i].Length; j++)
                {
                    var original = originals[i][j];
                    if (original == null) continue;
                    if (!materials.TryGetValue(original, out var material))
                    {
                        material = new Material(original) { name = original.name + " Spectator" };
                        Fade(material);
                        materials.Add(original, material);
                    }
                    faded[i][j] = material;
                }
            }
        }

        static void Fade(Material material)
        {
            // URP membutuhkan mode transparent, blend, keyword, dan queue; alpha saja masih dirender opaque.
            foreach (string property in new[] { "_BaseColor", "_Color" })
                if (material.HasProperty(property))
                {
                    var color = material.GetColor(property);
                    color.a *= .3f;
                    material.SetColor(property, color);
                }
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_AlphaClip", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
        }

        public void SetSpectator(bool value)
        {
            if (spectator == value) return;
            spectator = value;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sharedMaterials = value ? faded[i] : originals[i];
                renderers[i].shadowCastingMode = value ? ShadowCastingMode.Off : shadows[i];
            }
        }

        public void Dispose()
        {
            SetSpectator(false);
            foreach (var material in materials.Values) UnityEngine.Object.Destroy(material);
            materials.Clear();
        }
    }
}
