using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BiteOrBetray.Art3D
{
    public enum CharacterReaction { Eat, Yummy, Yuck, Shocked, Hurt, Give, Cheer, Laugh, Wave, Nod }

    /// <summary>
    /// Procedural animation for Bot_* and Waiter characters: idle, blinking, looking, gameplay reactions, and status (thinking,
    /// paralyzed, defeated). All motion is additive in LateUpdate, so other scripts can safely move characters.
    ///
    /// Rigged models (Tools/Blender/bob_characters.py) use Spine, Head, Arm_*, Forearm_*, and Hand_* bones,
    /// plus Lid_* (eyelids), Brow_* (eyebrows), Pupil_* (glances), and Mouth blendshapes (Open, Grin, Frown).
    /// Legacy unrigged models (Head + empty Arm_* objects) remain supported.
    /// Runs before default-order scripts so table labels (CandyTableDirector.LateUpdate) are arranged around this frame's pose.
    /// </summary>
    [DefaultExecutionOrder(-10), DisallowMultipleComponent]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        public static readonly List<CharacterAnimator> All = new List<CharacterAnimator>();

        [Header("Idle")]
        [Range(0, 2)] public float idleStrength = 1;
        [Tooltip("Body pivot height above the model pivot (meters). Only for models without a Spine bone.")]
        public float bodyPivotHeight = .45f;
        [Tooltip("Distance from head pivot to neck (meters). Only for unrigged models; the Head bone already pivots at the neck.")]
        public float neckDrop = .14f;

        [Header("Looking")]
        [Tooltip("Empty = main camera (player).")]
        public Transform lookTarget;
        public bool lookAround = true;
        [Range(0, 1)] public float lookStrength = 1;

        /// <summary>If set, the character keeps watching this target (e.g. the player whose turn it is).</summary>
        [System.NonSerialized] public Transform attention;

        [Header("Blinking")]
        public Vector2 blinkInterval = new Vector2(2.5f, 6f);

        [Header("Status")]
        public bool thinking;
        public bool paralyzed;
        public bool defeated;
        [Tooltip("0 = healthy, 1 = nearly depleted HP/poisoning: weak, dizzy, nauseous.")]
        [Range(0, 1)] public float sickness;
        [Tooltip("XR avatar: only animate the face; tracking/IK controls head, torso, and arms.")]
        public bool trackingDrivenBody;

        struct Pose
        {
            public float bodyUp, bodyPitch, bodyYaw, bodyRoll, squash;
            public float headPitch, headYaw, headRoll;
            public float leftRaise, rightRaise, leftSpread, rightSpread, leftBend, rightBend;
            public float eyeClose, eyeWide;
            public float mouthOpen, grin, frown, browUp, browKnit;
            public float gazeYaw, gazePitch;
        }

        sealed class Joint
        {
            public readonly Transform transform;
            Vector3 lastPosition, lastScale = Vector3.one;
            Quaternion lastRotation = Quaternion.identity;

            public Joint(Transform transform) { this.transform = transform; }

            public void Apply(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                transform.localPosition += position - lastPosition;
                transform.localRotation = rotation * Quaternion.Inverse(lastRotation) * transform.localRotation;
                transform.localScale = Vector3.Scale(transform.localScale, new Vector3(
                    scale.x / lastScale.x, scale.y / lastScale.y, scale.z / lastScale.z));
                lastPosition = position;
                lastRotation = rotation;
                lastScale = scale;
            }

            public void Reset() => Apply(Vector3.zero, Quaternion.identity, Vector3.one);
        }

        sealed class Arm
        {
            public Joint joint, forearm;
            public Vector3 raiseAxis, spreadAxis, bendAxis;
            public float side;
        }

        sealed class Eye
        {
            public Joint joint;
            public int upAxis;
        }

        sealed class Lid
        {
            public Joint joint;
            public Vector3 axis;
        }

        sealed class Brow
        {
            public Joint joint;
            public Vector3 lift, axis;
        }

        sealed class Pupil
        {
            public Joint joint;
            public Vector3 up, right;
        }

        sealed class Active
        {
            public CharacterReaction type;
            public Transform target;
            public float time, duration, fade = 1;
            public bool fading;
        }

        // Eyelid angle from rest to fully closed (matching the model generator).
        const float LidCloseAngle = 100;

        readonly List<Active> active = new List<Active>();
        readonly List<Eye> eyes = new List<Eye>();
        readonly List<Lid> lids = new List<Lid>();
        readonly List<Brow> brows = new List<Brow>();
        readonly List<Pupil> pupils = new List<Pupil>();
        Joint body, spine, head;
        Arm leftArm, rightArm;
        Transform face;
        SkinnedMeshRenderer mouth;
        int mouthOpenIndex = -1, mouthGrinIndex = -1, mouthFrownIndex = -1;
        bool ready;

        // Axes in root parent (body), spine parent, and head parent space.
        Vector3 upP, fwdP, rightP, upPv, hipP;
        Vector3 upS, fwdS, rightS;
        Vector3 upH, fwdH, rightH, neckH;
        int squashAxis;

        float speed, phase, idleAmp;
        float thinkW, paralyzedW, defeatedW, sickW;
        float blinkTimer, blinkTime = -1;
        bool doubleBlink;
        float lookYaw, lookPitch, lookYawVelocity, lookPitchVelocity, glanceTimer;
        float gazeYaw, gazePitch, gazeYawVelocity, gazePitchVelocity, saccadeTimer;
        Vector2 saccade;
        Transform glance;
        bool glanceForward;

        /// <summary>Face point for other characters to look at (nose if present, otherwise head pivot).</summary>
        public Transform Head => face != null ? face : head?.transform;

        public bool Rigged => spine != null;

        public void Play(CharacterReaction reaction, Transform target = null)
        {
            foreach (var a in active) a.fading = true;
            active.Add(new Active { type = reaction, target = target, duration = Duration(reaction) });
        }

        /// <summary>Stop all reactions and statuses, e.g. when starting a new game.</summary>
        public void ResetState()
        {
            active.Clear();
            thinking = paralyzed = defeated = false;
            sickness = 0;
            attention = null;
        }

        public static void PlayAll(CharacterReaction reaction, Transform target = null)
        {
            foreach (var c in All) c.Play(reaction, target);
        }

        static float Duration(CharacterReaction r)
        {
            switch (r)
            {
                case CharacterReaction.Eat: return 1.9f;
                case CharacterReaction.Yummy: return 1.5f;
                case CharacterReaction.Yuck: return 1.7f;
                case CharacterReaction.Shocked: return 1.3f;
                case CharacterReaction.Hurt: return .9f;
                case CharacterReaction.Give: return 1.6f;
                case CharacterReaction.Cheer: return 2.2f;
                case CharacterReaction.Laugh: return 1.8f;
                case CharacterReaction.Wave: return 2f;
                default: return 1f;
            }
        }

        [ContextMenu("Play/Eat")] void PlayEat() => Play(CharacterReaction.Eat);
        [ContextMenu("Play/Yummy")] void PlayYummy() => Play(CharacterReaction.Yummy);
        [ContextMenu("Play/Yuck")] void PlayYuck() => Play(CharacterReaction.Yuck);
        [ContextMenu("Play/Shocked")] void PlayShocked() => Play(CharacterReaction.Shocked);
        [ContextMenu("Play/Hurt")] void PlayHurt() => Play(CharacterReaction.Hurt);
        [ContextMenu("Play/Give")] void PlayGive() => Play(CharacterReaction.Give, PlayerTarget());
        [ContextMenu("Play/Cheer")] void PlayCheer() => Play(CharacterReaction.Cheer);
        [ContextMenu("Play/Laugh")] void PlayLaugh() => Play(CharacterReaction.Laugh);
        [ContextMenu("Play/Wave")] void PlayWave() => Play(CharacterReaction.Wave);
        [ContextMenu("Play/Nod")] void PlayNod() => Play(CharacterReaction.Nod);

        void OnEnable() => All.Add(this);

        void OnDisable()
        {
            All.Remove(this);
            RestorePose();
        }

        void Start()
        {
            if (ready) return;
            ready = Setup();
            if (!ready)
            {
                Debug.LogWarning($"[Bite or Betray] {name}: Head part not found, animation disabled.", this);
                enabled = false;
            }
        }

        void RestorePose()
        {
            if (!ready) return;
            body.Reset();
            spine?.Reset();
            head.Reset();
            foreach (var arm in new[] { leftArm, rightArm })
            {
                arm?.joint.Reset();
                arm?.forearm?.Reset();
            }
            foreach (var e in eyes) e.joint.Reset();
            foreach (var l in lids) l.joint.Reset();
            foreach (var b in brows) b.joint.Reset();
            foreach (var p in pupils) p.joint.Reset();
            SetMouth(0, 0, 0);
        }

        bool Setup()
        {
            var headT = FindPart(transform, "Head");
            if (headT == null) return false;
            var spineT = FindPart(transform, "Spine");
            if (spineT != null && GetComponentInChildren<SkinnedMeshRenderer>(true) == null) spineT = null;
            Vector3 up = Vector3.up;
            var nose = FindChild(headT, "Nose");
            Vector3 fwd = Vector3.ProjectOnPlane(nose != null ? nose.position - headT.position : transform.forward, up);
            if (fwd.sqrMagnitude < 1e-8f) fwd = Vector3.ProjectOnPlane(transform.forward, up);
            fwd.Normalize();
            Vector3 right = Vector3.Cross(up, fwd);

            body = new Joint(transform);
            upP = ParentDirection(transform, up).normalized;
            fwdP = ParentDirection(transform, fwd).normalized;
            rightP = Vector3.Cross(upP, fwdP).normalized;
            upPv = ParentVector(transform, up);
            squashAxis = DominantAxis(transform.InverseTransformDirection(up));

            if (spineT != null)
            {
                // Upper body leans from the waist; hips and legs stay on the chair.
                spine = new Joint(spineT);
                upS = ParentDirection(spineT, up).normalized;
                fwdS = ParentDirection(spineT, fwd).normalized;
                rightS = Vector3.Cross(upS, fwdS).normalized;
                squashAxis = DominantAxis(spineT.InverseTransformDirection(up));
                hipP = Vector3.zero;
                neckH = Vector3.zero;
                face = nose;
            }
            else hipP = upPv * bodyPivotHeight;

            head = new Joint(headT);
            upH = ParentDirection(headT, up).normalized;
            fwdH = ParentDirection(headT, fwd).normalized;
            rightH = Vector3.Cross(upH, fwdH).normalized;
            if (spine == null) neckH = ParentVector(headT, Vector3.down * neckDrop);

            var armA = MakeArm(FindPart(transform, "Arm_Left"), up, fwd, right);
            var armB = MakeArm(FindPart(transform, "Arm_Right"), up, fwd, right);
            // Determine arm side by position rather than name, so Give always uses the hand nearest the target.
            if (armA != null && armB != null && armA.side > armB.side) (armA, armB) = (armB, armA);
            leftArm = armA;
            rightArm = armB;

            foreach (var t in headT.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Lid"))
                    lids.Add(new Lid { joint = new Joint(t), axis = SignedAxis(t, right, fwd, -up) });
                else if (t.name.StartsWith("Brow"))
                {
                    float side = Vector3.Dot(t.position - headT.position, right) >= 0 ? 1 : -1;
                    // Positive rotation lowers the inner brow tip (frowning).
                    brows.Add(new Brow { joint = new Joint(t), lift = ParentVector(t, up), axis = SignedAxis(t, fwd, -right * side, -up) });
                }
                else if (t.name.StartsWith("Mouth") && t.TryGetComponent(out SkinnedMeshRenderer smr) && smr.sharedMesh != null)
                {
                    mouth = smr;
                    mouthOpenIndex = BlendShape(smr.sharedMesh, "Open");
                    mouthGrinIndex = BlendShape(smr.sharedMesh, "Grin");
                    mouthFrownIndex = BlendShape(smr.sharedMesh, "Frown");
                }
            }
            bool hasLids = lids.Count > 0;
            foreach (var t in headT.GetComponentsInChildren<Transform>(true))
            {
                if (hasLids && t.name.StartsWith("Pupil"))
                    pupils.Add(new Pupil { joint = new Joint(t), up = ParentDirection(t, up).normalized, right = ParentDirection(t, right).normalized });
                else if (!hasLids && (t.name.StartsWith("EyeWhite") || t.name.StartsWith("Pupil") || t.name.StartsWith("EyeSpark")))
                    eyes.Add(new Eye { joint = new Joint(t), upAxis = DominantAxis(t.InverseTransformDirection(up)) });
            }

            // Each character has its own timing so they do not move together like robots.
            var rng = new System.Random(StableHash(name));
            speed = Mathf.Lerp(.85f, 1.2f, (float)rng.NextDouble());
            phase = (float)rng.NextDouble() * 100;
            idleAmp = Mathf.Lerp(.8f, 1.2f, (float)rng.NextDouble());
            blinkTimer = Random.Range(.5f, blinkInterval.y);
            glanceTimer = Random.Range(.5f, 2f);
            saccadeTimer = Random.Range(.3f, 1.5f);
            return true;
        }

        Arm MakeArm(Transform arm, Vector3 up, Vector3 fwd, Vector3 right)
        {
            if (arm == null) return null;
            var forearmT = FindChild(arm, "Forearm");
            Vector3 center = arm.position + fwd * .1f - up * .1f;
            if (forearmT != null) center = forearmT.position;
            else
            {
                var renderers = arm.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    center = bounds.center;
                }
            }
            Vector3 dir = center - arm.position;
            float side = Vector3.Dot(arm.position - transform.position, right) >= 0 ? 1 : -1;
            var result = new Arm {
                joint = new Joint(arm), side = side,
                // Positive angle = hand raised / opening outward.
                raiseAxis = SignedAxis(arm, Vector3.Cross(dir, up), dir, up),
                spreadAxis = SignedAxis(arm, Vector3.Cross(dir, right * side), dir, right * side)
            };
            if (forearmT != null)
            {
                var handT = FindChild(forearmT, "Hand");
                Vector3 hand = handT != null && handT != forearmT ? handT.position : forearmT.position + fwd * .2f;
                Vector3 upper = forearmT.position - arm.position, lower = hand - forearmT.position;
                Vector3 n = Vector3.Cross(upper, lower);
                if (n.sqrMagnitude < 1e-8f) n = right;
                // Positive rotation = elbow bending (hand approaching the shoulder).
                var test = forearmT.position + Quaternion.AngleAxis(15, n) * lower;
                if (Vector3.Distance(test, arm.position) > Vector3.Distance(hand, arm.position)) n = -n;
                result.forearm = new Joint(forearmT);
                result.bendAxis = ParentDirection(forearmT, n).normalized;
            }
            return result;
        }

        /// <summary>Rotation axis (in t's parent space), with sign chosen so positive rotation moves
        /// the probe vector toward toward.</summary>
        static Vector3 SignedAxis(Transform t, Vector3 axis, Vector3 probe, Vector3 toward)
        {
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(probe, toward);
            axis.Normalize();
            if (Vector3.Dot(Quaternion.AngleAxis(10, axis) * probe - probe, toward) < 0) axis = -axis;
            return ParentDirection(t, axis).normalized;
        }

        static int BlendShape(Mesh mesh, string key)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string n = mesh.GetBlendShapeName(i);
                if (n == key || n.EndsWith("." + key)) return i;
            }
            return -1;
        }

        void LateUpdate()
        {
            if (!ready) return;
            Step(Time.deltaTime, Time.time);
        }

        void Step(float dt, float time)
        {
            thinkW = Mathf.MoveTowards(thinkW, thinking ? 1 : 0, dt * 3);
            paralyzedW = Mathf.MoveTowards(paralyzedW, paralyzed ? 1 : 0, dt * 4);
            defeatedW = Mathf.MoveTowards(defeatedW, defeated ? 1 : 0, dt * 1.5f);
            sickW = Mathf.MoveTowards(sickW, Mathf.Clamp01(sickness), dt);

            var p = new Pose();
            Idle(ref p, time * speed + phase, idleStrength * idleAmp * (1 - paralyzedW) * (1 - .7f * defeatedW) * (1 - .4f * sickW));
            States(ref p, time);

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var a = active[i];
                a.time += dt;
                if (a.fading) a.fade = Mathf.MoveTowards(a.fade, 0, dt / .2f);
                if (a.time >= a.duration || a.fade <= 0) { active.RemoveAt(i); continue; }
                Reaction(ref p, a);
            }

            Look(ref p, dt);
            p.eyeClose = Mathf.Max(p.eyeClose, Blink(dt));
            Apply(p);
        }

        void Idle(ref Pose p, float t, float k)
        {
            float breath = Mathf.Sin(t * 2.1f);
            p.bodyUp += breath * (Rigged ? .002f : .005f) * k;
            p.squash += breath * .012f * k;
            p.bodyRoll += Mathf.Sin(t * .53f) * 1.4f * k;
            p.bodyYaw += Mathf.Sin(t * .31f + 1) * 2f * k;
            p.bodyPitch += Mathf.Sin(t * .41f + 3) * 1.2f * k;
            p.headRoll += Mathf.Sin(t * .71f + 2) * 3.5f * k;
            p.headPitch += Mathf.Sin(t * .93f + .5f) * 2.5f * k;
            p.leftRaise += Mathf.Sin(t * 1.1f) * 3 * k;
            p.rightRaise += Mathf.Sin(t * 1.1f + 1.7f) * 3 * k;
            p.leftBend += Mathf.Sin(t * .8f + .4f) * 4 * k;
            p.rightBend += Mathf.Sin(t * .7f + 2.2f) * 4 * k;
            p.browUp += Mathf.Max(0, Mathf.Sin(t * .37f + 4)) * .25f * k;
        }

        void States(ref Pose p, float t)
        {
            if (thinkW > 0)
            {
                // Hand to chin, eyes glance upward to the side, brows furrow.
                float w = Smooth(0, 1, thinkW);
                p.headRoll += 12 * w;
                p.headPitch -= 6 * w;
                p.headYaw += Mathf.Sin(t * .6f) * 10 * w;
                p.rightRaise += (Rigged ? 48 : 75 + Mathf.Sin(t * 5) * 3) * w;
                p.rightBend += (70 + Mathf.Sin(t * 5) * 4) * w;
                p.rightSpread -= 18 * w;
                p.gazePitch -= 14 * w;
                p.gazeYaw += (12 + Mathf.Sin(t * .9f) * 6) * w;
                p.browKnit += .6f * w;
                p.frown += .35f * w;
            }
            if (paralyzedW > 0)
            {
                float w = Smooth(0, 1, paralyzedW);
                p.bodyRoll += (Mathf.PerlinNoise(t * 25, 0) - .5f) * 3 * w;
                p.bodyUp += (Mathf.PerlinNoise(t * 30, 7) - .5f) * .006f * w;
                p.headRoll += (Mathf.PerlinNoise(t * 28, 13) - .5f) * 4 * w;
                p.leftSpread += 12 * w;
                p.rightSpread += 12 * w;
                p.leftBend -= 15 * w;
                p.rightBend -= 15 * w;
                p.eyeWide += .4f * w;
                p.mouthOpen += .4f * w;
                p.browUp += .9f * w;
            }
            if (sickW > 0)
            {
                // Weak, dizzy, nauseous: body sways slowly, head droops, eyes are heavy and rolling.
                float w = Smooth(0, 1, sickW);
                p.bodyRoll += Mathf.Sin(t * 1.3f) * 4 * w;
                p.bodyYaw += Mathf.Sin(t * .9f + 1) * 3 * w;
                p.bodyPitch += 6 * w;
                p.bodyUp -= .015f * w;
                p.headRoll += Mathf.Sin(t * 1.3f + .6f) * 7 * w;
                p.headPitch += 10 * w;
                p.leftRaise -= 8 * w;
                p.rightRaise -= 8 * w;
                p.eyeClose += .35f * w;
                p.frown += .5f * w;
                p.browKnit -= .4f * w;
                p.mouthOpen += (.06f + .08f * Mathf.Max(0, Mathf.Sin(t * 2.3f))) * w;
                p.gazeYaw += Mathf.Sin(t * 1.7f) * 8 * w;
                p.gazePitch += Mathf.Cos(t * 1.7f) * 5 * w;
            }
            if (defeatedW > 0)
            {
                float w = Smooth(0, 1, defeatedW);
                p.headPitch += 24 * w;
                p.bodyPitch += 9 * w;
                p.bodyUp -= .03f * w;
                p.leftRaise -= 12 * w;
                p.rightRaise -= 12 * w;
                p.eyeClose += .6f * w;
                p.frown += .8f * w;
                p.browKnit -= .7f * w;
                p.gazePitch += 10 * w;
            }
        }

        void Reaction(ref Pose p, Active a)
        {
            float u = a.time / a.duration, t = a.time, w;
            switch (a.type)
            {
                case CharacterReaction.Eat:
                    // Hand brings candy to the mouth, mouth opens, then chews.
                    w = Window(u, .12f, .25f) * a.fade;
                    float reach = Bump(u, 0, .55f) * a.fade;
                    float bite = Bump(u, .22f, .42f) * a.fade;
                    float chew = Smooth(.38f, .48f, u) * (1 - Smooth(.82f, 1, u)) * a.fade;
                    p.rightRaise += (Rigged ? 34 : 70) * reach;
                    p.rightBend += 72 * reach;
                    p.rightSpread -= 12 * reach;
                    p.bodyPitch += 7 * w;
                    p.headPitch += 5 * w + Mathf.Sin(t * 17) * 4 * chew;
                    p.squash += Mathf.Sin(t * 17) * .015f * chew;
                    p.mouthOpen += .85f * bite + (.5f + .5f * Mathf.Sin(t * 17)) * .4f * chew;
                    p.browUp += .5f * bite;
                    p.eyeClose += .3f * chew;
                    break;
                case CharacterReaction.Yummy:
                    w = Window(u, .1f, .3f) * a.fade;
                    p.bodyUp += Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3)) * .035f * w;
                    p.squash += Mathf.Sin(u * Mathf.PI * 6) * .05f * w;
                    p.headRoll += Mathf.Sin(t * 9) * 10 * w;
                    p.leftRaise += 40 * w;
                    p.rightRaise += 40 * w;
                    p.leftSpread += 30 * w;
                    p.rightSpread += 30 * w;
                    p.leftBend += 30 * w;
                    p.rightBend += 30 * w;
                    p.eyeClose += .65f * w;
                    p.grin += w;
                    p.mouthOpen += (.25f + .15f * Mathf.Sin(t * 12)) * w;
                    p.browUp += .6f * w;
                    break;
                case CharacterReaction.Yuck:
                    w = Window(u, .08f, .3f) * a.fade;
                    p.headYaw += Mathf.Sin(t * 22) * 14 * w;
                    p.bodyPitch -= 6 * w;
                    p.squash -= .05f * w;
                    p.bodyRoll += Mathf.Sin(t * 31) * 1.5f * w;
                    p.leftRaise += 20 * w;
                    p.rightRaise += 20 * w;
                    p.leftSpread += 25 * w;
                    p.rightSpread += 25 * w;
                    p.eyeClose += .45f * w;
                    p.frown += w;
                    p.mouthOpen += .3f * w;
                    p.browKnit += .9f * w;
                    break;
                case CharacterReaction.Shocked:
                    w = Window(u, .05f, .45f) * a.fade;
                    float jump = Bump(u, 0, .35f) * a.fade;
                    p.bodyUp += .05f * jump;
                    p.squash += .08f * jump;
                    p.bodyPitch -= 6 * w;
                    p.headPitch -= 10 * w;
                    p.leftRaise += 100 * w;
                    p.rightRaise += 100 * w;
                    p.leftSpread += 50 * w;
                    p.rightSpread += 50 * w;
                    p.leftBend += 20 * w;
                    p.rightBend += 20 * w;
                    p.eyeWide += .5f * w;
                    p.mouthOpen += w;
                    p.browUp += w;
                    break;
                case CharacterReaction.Hurt:
                    w = Window(u, .05f, .5f) * a.fade;
                    float hit = Bump(u, 0, .4f) * a.fade;
                    p.bodyPitch -= 8 * hit;
                    p.squash -= .09f * hit;
                    p.bodyRoll += Mathf.Sin(t * 40) * 2 * w;
                    p.headPitch += 8 * w;
                    p.eyeClose += hit;
                    p.frown += .9f * w;
                    p.mouthOpen += .35f * hit;
                    p.browKnit += .8f * w;
                    break;
                case CharacterReaction.Give:
                    w = Window(u, .15f, .3f) * a.fade;
                    float yaw = a.target != null ? YawTo(a.target.position) : 0;
                    p.bodyYaw += Mathf.Clamp(yaw, -60, 60) * .5f * w;
                    p.bodyPitch += 10 * w;
                    p.headPitch += 6 * w;
                    p.grin += .35f * w;
                    // The hand nearest the target reaches out (elbow straightened).
                    if (leftArm != null && yaw * leftArm.side > 5) { p.leftRaise += 65 * w; p.leftSpread -= 8 * w; p.leftBend -= 30 * w; }
                    else { p.rightRaise += 65 * w; p.rightSpread -= 8 * w; p.rightBend -= 30 * w; }
                    break;
                case CharacterReaction.Cheer:
                    w = Window(u, .08f, .2f) * a.fade;
                    p.bodyUp += Mathf.Abs(Mathf.Sin(u * Mathf.PI * 4)) * .06f * w;
                    p.squash += Mathf.Sin(u * Mathf.PI * 8) * .06f * w;
                    p.leftRaise += (120 + Mathf.Sin(t * 12) * 12) * w;
                    p.rightRaise += (120 + Mathf.Sin(t * 12 + 1.5f) * 12) * w;
                    // Spread sets the hands' sideways reach; at 55 the outer hand of the end seats left the desktop seat frame (CI f31ae01).
                    p.leftSpread += 40 * w;
                    p.rightSpread += 40 * w;
                    p.leftBend -= 25 * w;
                    p.rightBend -= 25 * w;
                    p.headPitch -= 12 * w;
                    p.eyeClose += .55f * w;
                    p.grin += w;
                    p.mouthOpen += (.55f + .2f * Mathf.Sin(t * 12)) * w;
                    p.browUp += .8f * w;
                    break;
                case CharacterReaction.Laugh:
                    w = Window(u, .1f, .25f) * a.fade;
                    p.bodyUp += Mathf.Abs(Mathf.Sin(t * 15)) * .012f * w;
                    p.headPitch += (-15 + Mathf.Sin(t * 15) * 3) * w;
                    p.bodyPitch -= 4 * w;
                    p.leftRaise += 25 * w;
                    p.rightRaise += 25 * w;
                    p.leftSpread += 15 * w;
                    p.rightSpread += 15 * w;
                    p.leftBend += 25 * w;
                    p.rightBend += 25 * w;
                    p.eyeClose += .85f * w;
                    p.grin += w;
                    p.mouthOpen += (.45f + .3f * Mathf.Sin(t * 15)) * w;
                    p.browUp += .4f * w;
                    break;
                case CharacterReaction.Wave:
                    w = Window(u, .12f, .25f) * a.fade;
                    p.rightRaise += 115 * w;
                    p.rightSpread += (Rigged ? 35 : 45 + Mathf.Sin(t * 10) * 20) * w;
                    p.rightBend += (Rigged ? 55 + Mathf.Sin(t * 10) * 28 : 0) * w;
                    p.headRoll += 8 * w;
                    p.bodyRoll -= 3 * w;
                    p.eyeClose += .3f * w;
                    p.grin += .7f * w;
                    p.mouthOpen += .2f * w;
                    p.browUp += .5f * w;
                    break;
                case CharacterReaction.Nod:
                    w = Window(u, .1f, .2f) * a.fade;
                    p.headPitch += Mathf.Sin(u * Mathf.PI * 4) * 10 * w;
                    p.grin += .3f * w;
                    break;
            }
        }

        void Look(ref Pose p, float dt)
        {
            float weight = lookStrength * (1 - .7f * thinkW) * (1 - paralyzedW) * (1 - .8f * defeatedW);
            Transform target = null;
            foreach (var a in active)
                if (a.target != null && !a.fading) target = a.target;
            if (target == null) target = attention;
            if (target == null)
            {
                if (lookAround)
                {
                    glanceTimer -= dt;
                    if (glanceTimer <= 0) PickGlance();
                    target = glanceForward ? null : glance;
                }
                else target = PlayerTarget();
            }

            float yaw = 0, pitch = 0;
            if (target != null)
            {
                yaw = YawTo(target.position);
                if (Mathf.Abs(yaw) > 110) yaw = 0;
                Vector3 dir = ParentDirection(transform, target.position - head.transform.position);
                pitch = -(90 - Vector3.Angle(dir, upP));
            }
            yaw *= weight;
            pitch *= weight;
            lookYaw = Mathf.SmoothDamp(lookYaw, Mathf.Clamp(yaw, -70, 70), ref lookYawVelocity, .3f, Mathf.Infinity, dt);
            lookPitch = Mathf.SmoothDamp(lookPitch, Mathf.Clamp(pitch, -25, 30), ref lookPitchVelocity, .3f, Mathf.Infinity, dt);
            p.bodyYaw += lookYaw * .25f;
            p.headYaw += lookYaw * .75f;
            p.headPitch += lookPitch;

            // Eyes lead the head: pupils cover the remaining angle before the head catches up, plus small random glances.
            saccadeTimer -= dt;
            if (saccadeTimer <= 0)
            {
                saccadeTimer = Random.Range(.35f, 1.8f);
                saccade = Random.insideUnitCircle * 3.5f;
            }
            float yawGoal = Mathf.Clamp(yaw - lookYaw + saccade.x + p.gazeYaw, -28, 28);
            float pitchGoal = Mathf.Clamp(pitch - lookPitch + saccade.y + p.gazePitch, -20, 20);
            gazeYaw = Mathf.SmoothDamp(gazeYaw, yawGoal, ref gazeYawVelocity, .05f, Mathf.Infinity, dt);
            gazePitch = Mathf.SmoothDamp(gazePitch, pitchGoal, ref gazePitchVelocity, .05f, Mathf.Infinity, dt);
            p.gazeYaw = gazeYaw;
            p.gazePitch = gazePitch;
        }

        void PickGlance()
        {
            glanceTimer = Random.Range(2f, 5.5f);
            glanceForward = false;
            float roll = Random.value;
            if (roll < .5f) glance = PlayerTarget();
            else if (roll < .8f && All.Count > 1)
            {
                var other = All[Random.Range(0, All.Count)];
                glance = other != this ? other.Head : PlayerTarget();
            }
            else glanceForward = true;
        }

        Transform PlayerTarget()
        {
            if (lookTarget != null) return lookTarget;
            var camera = Camera.main;
            return camera != null ? camera.transform : null;
        }

        float Blink(float dt)
        {
            if (blinkTime < 0)
            {
                blinkTimer -= dt;
                if (blinkTimer > 0) return 0;
                blinkTime = 0;
                doubleBlink = Random.value < .2f;
            }
            blinkTime += dt;
            const float length = .14f;
            if (blinkTime < length) return Bump(blinkTime, 0, length);
            blinkTime = -1;
            blinkTimer = doubleBlink ? .12f : Random.Range(blinkInterval.x, blinkInterval.y);
            doubleBlink = false;
            return 0;
        }

        void Apply(in Pose p)
        {
            if (!trackingDrivenBody)
            {
            var bodyRotation = Quaternion.AngleAxis(p.bodyYaw, upP) * Quaternion.AngleAxis(p.bodyPitch, rightP)
                * Quaternion.AngleAxis(p.bodyRoll, fwdP);
            float s = Mathf.Clamp(1 + p.squash, .6f, 1.4f);
            var squash = Vector3.one * (1 / Mathf.Sqrt(s));
            squash[squashAxis] = s;
            if (spine != null)
            {
                body.Apply(upPv * p.bodyUp, Quaternion.identity, Vector3.one);
                var spineRotation = Quaternion.AngleAxis(p.bodyYaw, upS) * Quaternion.AngleAxis(p.bodyPitch, rightS)
                    * Quaternion.AngleAxis(p.bodyRoll, fwdS);
                spine.Apply(Vector3.zero, spineRotation, squash);
            }
            else body.Apply(upPv * p.bodyUp + hipP - bodyRotation * hipP, bodyRotation, squash);

            var headRotation = Quaternion.AngleAxis(p.headYaw, upH) * Quaternion.AngleAxis(p.headPitch, rightH)
                * Quaternion.AngleAxis(p.headRoll, fwdH);
            head.Apply(neckH - headRotation * neckH, headRotation, Vector3.one);

            ApplyArm(leftArm, p.leftRaise, p.leftSpread, p.leftBend);
            ApplyArm(rightArm, p.rightRaise, p.rightSpread, p.rightBend);
            }

            float close = Mathf.Clamp01(p.eyeClose), wide = Mathf.Max(0, p.eyeWide) * (1 - close);
            foreach (var e in eyes)
            {
                var scale = Vector3.one * (1 + wide);
                scale[e.upAxis] = Mathf.Lerp(1, .08f, close) * (1 + wide);
                e.joint.Apply(Vector3.zero, Quaternion.identity, scale);
            }
            foreach (var l in lids)
                l.joint.Apply(Vector3.zero, Quaternion.AngleAxis(close * LidCloseAngle - wide * 30, l.axis), Vector3.one);
            foreach (var pu in pupils)
                pu.joint.Apply(Vector3.zero, Quaternion.AngleAxis(p.gazeYaw, pu.up) * Quaternion.AngleAxis(p.gazePitch, pu.right), Vector3.one);

            float browUp = Mathf.Clamp(p.browUp, -1, 1.2f), knit = Mathf.Clamp(p.browKnit, -1, 1);
            foreach (var b in brows)
                b.joint.Apply(b.lift * (.011f * browUp - .004f * Mathf.Max(0, knit)), Quaternion.AngleAxis(knit * 16, b.axis), Vector3.one);

            SetMouth(Mathf.Clamp01(p.mouthOpen), Mathf.Clamp01(p.grin), Mathf.Clamp01(p.frown - p.grin * .5f));
        }

        void SetMouth(float open, float grin, float frown)
        {
            if (mouth == null) return;
            if (mouthOpenIndex >= 0) mouth.SetBlendShapeWeight(mouthOpenIndex, open * 100);
            if (mouthGrinIndex >= 0) mouth.SetBlendShapeWeight(mouthGrinIndex, grin * 100);
            if (mouthFrownIndex >= 0) mouth.SetBlendShapeWeight(mouthFrownIndex, frown * 100);
        }

        static void ApplyArm(Arm arm, float raise, float spread, float bend)
        {
            if (arm == null) return;
            arm.joint.Apply(Vector3.zero, Quaternion.AngleAxis(raise, arm.raiseAxis) * Quaternion.AngleAxis(spread, arm.spreadAxis), Vector3.one);
            arm.forearm?.Apply(Vector3.zero, Quaternion.AngleAxis(Mathf.Clamp(bend, -40, 120), arm.bendAxis), Vector3.one);
        }

        float YawTo(Vector3 world)
        {
            Vector3 dir = Vector3.ProjectOnPlane(ParentDirection(transform, world - head.transform.position), upP);
            return dir.sqrMagnitude < 1e-8f ? 0 : Vector3.SignedAngle(fwdP, dir, upP);
        }

#if UNITY_EDITOR
        /// <summary>Pose a reaction at a given time without Play mode, for Editor preview renders.
        /// Call EditorRestore afterward.</summary>
        public void EditorPose(CharacterReaction? reaction, float seconds, bool think = false, bool paralyze = false, bool defeat = false)
        {
            if (!ready) ready = Setup();
            if (!ready) return;
            RestorePose();
            active.Clear();
            thinking = think;
            paralyzed = paralyze;
            defeated = defeat;
            thinkW = think ? 1 : 0;
            paralyzedW = paralyze ? 1 : 0;
            defeatedW = defeat ? 1 : 0;
            blinkTimer = 99;
            blinkTime = -1;
            glanceTimer = 99;
            glanceForward = true;
            saccadeTimer = 99;
            saccade = Vector2.zero;
            lookYaw = lookPitch = gazeYaw = gazePitch = 0;
            if (reaction.HasValue) Play(reaction.Value, PlayerTarget());
            const float dt = 1 / 60f;
            for (float t = 0; t < seconds; t += dt) Step(dt, phase + t);
            if (seconds <= 0) Step(0, phase);
        }

        public void EditorRestore()
        {
            RestorePose();
            active.Clear();
            thinking = paralyzed = defeated = false;
            sickness = 0;
            thinkW = paralyzedW = defeatedW = sickW = 0;
        }
#endif

        static Vector3 ParentDirection(Transform t, Vector3 world) =>
            t.parent != null ? t.parent.InverseTransformDirection(world) : world;

        static Vector3 ParentVector(Transform t, Vector3 world) =>
            t.parent != null ? t.parent.InverseTransformVector(world) : world;

        static int DominantAxis(Vector3 v)
        {
            v = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return v.x > v.y && v.x > v.z ? 0 : v.y > v.z ? 1 : 2;
        }

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));

        static float Window(float u, float fadeIn, float fadeOut) => Smooth(0, fadeIn, u) * (1 - Smooth(1 - fadeOut, 1, u));

        static float Bump(float u, float a, float b) => Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(a, b, u)) * Mathf.PI);

        static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in s) h = h * 31 + c;
                return h;
            }
        }

        static Transform FindPart(Transform root, string prefix)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.StartsWith(prefix) && t.GetComponent<Renderer>() == null) return t;
            return null;
        }

        static Transform FindChild(Transform root, string prefix)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.StartsWith(prefix)) return t;
            return null;
        }

        static bool LooksLikeCharacter(Transform t)
        {
            bool head = false, arm = false;
            foreach (Transform child in t)
            {
                if (child.GetComponent<Renderer>() != null) continue;
                head |= child.name.StartsWith("Head");
                arm |= child.name.StartsWith("Arm_");
            }
            if (head && arm) return true;
            // Rigged model: Hips is directly under the root; Head and Arm_* are deeper.
            return t.Find("Hips") != null && FindPart(t, "Head") != null && FindPart(t, "Arm_") != null
                && t.GetComponentInParent<CharacterAnimator>() == null;
        }

        /// <summary>Attach CharacterAnimator to every scene character that lacks one.</summary>
        public static int AttachAll(Scene scene)
        {
            int added = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.GetComponent<CharacterAnimator>() == null && LooksLikeCharacter(t))
                    {
                        t.gameObject.AddComponent<CharacterAnimator>();
                        added++;
                    }
            return added;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        // Scene characters without the component still animate in Play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoAttach()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachAll(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachAll(scene);
    }
}
