using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BiteOrBetray.Art3D
{
    public enum CharacterReaction { Eat, Yummy, Yuck, Shocked, Hurt, Give, Cheer, Laugh, Wave, Nod }

    /// <summary>
    /// Animasi prosedural untuk karakter tanpa rig (Bot_* dan Waiter): idle, kedip, menoleh, reaksi gameplay,
    /// dan status (mikir, lumpuh, kalah). Root, Head, Arm_Left/Right, dan mata digerakkan secara aditif di
    /// LateUpdate, jadi tetap aman kalau ada script lain yang memindahkan karakter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        public static readonly List<CharacterAnimator> All = new List<CharacterAnimator>();

        [Header("Idle")]
        [Range(0, 2)] public float idleStrength = 1;
        [Tooltip("Tinggi titik putar badan dari pivot model (meter). Badan condong dari pinggul, bukan dari lantai.")]
        public float bodyPivotHeight = .45f;
        [Tooltip("Jarak pivot kepala ke leher (meter). Kepala menoleh dari leher, bukan dari tengah kepala.")]
        public float neckDrop = .14f;

        [Header("Menoleh")]
        [Tooltip("Kosong = kamera utama (pemain).")]
        public Transform lookTarget;
        public bool lookAround = true;
        [Range(0, 1)] public float lookStrength = 1;

        /// <summary>Kalau diisi, karakter terus memperhatikan target ini (misalnya pemain yang sedang giliran).</summary>
        [System.NonSerialized] public Transform attention;

        [Header("Kedip")]
        public Vector2 blinkInterval = new Vector2(2.5f, 6f);

        [Header("Status")]
        public bool thinking;
        public bool paralyzed;
        public bool defeated;

        struct Pose
        {
            public float bodyUp, bodyPitch, bodyYaw, bodyRoll, squash;
            public float headPitch, headYaw, headRoll;
            public float leftRaise, rightRaise, leftSpread, rightSpread;
            public float eyeClose, eyeWide;
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
            public Joint joint;
            public Vector3 raiseAxis, spreadAxis;
        }

        sealed class Eye
        {
            public Joint joint;
            public int upAxis;
        }

        sealed class Active
        {
            public CharacterReaction type;
            public Transform target;
            public float time, duration, fade = 1;
            public bool fading;
        }

        readonly List<Active> active = new List<Active>();
        readonly List<Eye> eyes = new List<Eye>();
        Joint body, head;
        Arm leftArm, rightArm;
        bool ready;

        // Sumbu di ruang parent root (badan) dan ruang parent kepala.
        Vector3 upP, fwdP, rightP, upPv, hipP;
        Vector3 upH, fwdH, rightH, neckH;
        int squashAxis;

        float speed, phase, idleAmp;
        float thinkW, paralyzedW, defeatedW;
        float blinkTimer, blinkTime = -1;
        bool doubleBlink;
        float lookYaw, lookPitch, lookYawVelocity, lookPitchVelocity, glanceTimer;
        Transform glance;
        bool glanceForward;

        public Transform Head => head?.transform;

        public void Play(CharacterReaction reaction, Transform target = null)
        {
            foreach (var a in active) a.fading = true;
            active.Add(new Active { type = reaction, target = target, duration = Duration(reaction) });
        }

        /// <summary>Hentikan semua reaksi dan status, misalnya saat game baru dimulai.</summary>
        public void ResetState()
        {
            active.Clear();
            thinking = paralyzed = defeated = false;
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
                case CharacterReaction.Eat: return 1.7f;
                case CharacterReaction.Yummy: return 1.5f;
                case CharacterReaction.Yuck: return 1.7f;
                case CharacterReaction.Shocked: return 1.2f;
                case CharacterReaction.Hurt: return .8f;
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
            if (!ready) return;
            body.Reset();
            head.Reset();
            leftArm?.joint.Reset();
            rightArm?.joint.Reset();
            foreach (var e in eyes) e.joint.Reset();
        }

        void Start()
        {
            ready = Setup();
            if (!ready)
            {
                Debug.LogWarning($"[Bite or Betray] {name}: bagian Head tidak ditemukan, animasi dimatikan.", this);
                enabled = false;
            }
        }

        bool Setup()
        {
            var headT = FindPart(transform, "Head");
            if (headT == null) return false;
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
            hipP = upPv * bodyPivotHeight;
            squashAxis = DominantAxis(transform.InverseTransformDirection(up));

            head = new Joint(headT);
            upH = ParentDirection(headT, up).normalized;
            fwdH = ParentDirection(headT, fwd).normalized;
            rightH = Vector3.Cross(upH, fwdH).normalized;
            neckH = ParentVector(headT, Vector3.down * neckDrop);

            leftArm = MakeArm(FindPart(transform, "Arm_Left"), up, fwd, right);
            rightArm = MakeArm(FindPart(transform, "Arm_Right"), up, fwd, right);

            foreach (var t in headT.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("EyeWhite") || t.name.StartsWith("Pupil") || t.name.StartsWith("EyeSpark"))
                    eyes.Add(new Eye { joint = new Joint(t), upAxis = DominantAxis(t.InverseTransformDirection(up)) });

            // Tiap karakter punya tempo sendiri supaya tidak bergerak kompak seperti robot.
            var rng = new System.Random(StableHash(name));
            speed = Mathf.Lerp(.85f, 1.2f, (float)rng.NextDouble());
            phase = (float)rng.NextDouble() * 100;
            idleAmp = Mathf.Lerp(.8f, 1.2f, (float)rng.NextDouble());
            blinkTimer = Random.Range(.5f, blinkInterval.y);
            glanceTimer = Random.Range(.5f, 2f);
            return true;
        }

        Arm MakeArm(Transform arm, Vector3 up, Vector3 fwd, Vector3 right)
        {
            if (arm == null) return null;
            var renderers = arm.GetComponentsInChildren<Renderer>();
            Vector3 center = arm.position + fwd * .1f - up * .1f;
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                center = bounds.center;
            }
            Vector3 dir = center - arm.position;
            float side = Vector3.Dot(arm.position - transform.position, right) >= 0 ? 1 : -1;
            return new Arm {
                joint = new Joint(arm),
                // Sudut positif = tangan naik / membuka ke luar.
                raiseAxis = ParentDirection(arm, Vector3.Cross(dir, up)).normalized,
                spreadAxis = ParentDirection(arm, Vector3.Cross(dir, right * side)).normalized
            };
        }

        void LateUpdate()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            float time = Time.time;
            thinkW = Mathf.MoveTowards(thinkW, thinking ? 1 : 0, dt * 3);
            paralyzedW = Mathf.MoveTowards(paralyzedW, paralyzed ? 1 : 0, dt * 4);
            defeatedW = Mathf.MoveTowards(defeatedW, defeated ? 1 : 0, dt * 1.5f);

            var p = new Pose();
            Idle(ref p, time * speed + phase, idleStrength * idleAmp * (1 - paralyzedW) * (1 - .7f * defeatedW));
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
            p.bodyUp += breath * .005f * k;
            p.squash += breath * .012f * k;
            p.bodyRoll += Mathf.Sin(t * .53f) * 1.4f * k;
            p.bodyYaw += Mathf.Sin(t * .31f + 1) * 2f * k;
            p.headRoll += Mathf.Sin(t * .71f + 2) * 3.5f * k;
            p.headPitch += Mathf.Sin(t * .93f + .5f) * 2.5f * k;
            p.leftRaise += Mathf.Sin(t * 1.1f) * 3 * k;
            p.rightRaise += Mathf.Sin(t * 1.1f + 1.7f) * 3 * k;
        }

        void States(ref Pose p, float t)
        {
            if (thinkW > 0)
            {
                float w = Smooth(0, 1, thinkW);
                p.headRoll += 12 * w;
                p.headPitch -= 6 * w;
                p.headYaw += Mathf.Sin(t * .6f) * 10 * w;
                p.rightRaise += (75 + Mathf.Sin(t * 5) * 3) * w;
                p.rightSpread -= 18 * w;
            }
            if (paralyzedW > 0)
            {
                float w = Smooth(0, 1, paralyzedW);
                p.bodyRoll += (Mathf.PerlinNoise(t * 25, 0) - .5f) * 3 * w;
                p.bodyUp += (Mathf.PerlinNoise(t * 30, 7) - .5f) * .006f * w;
                p.headRoll += (Mathf.PerlinNoise(t * 28, 13) - .5f) * 4 * w;
                p.leftSpread += 12 * w;
                p.rightSpread += 12 * w;
                p.eyeWide += .15f * w;
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
            }
        }

        void Reaction(ref Pose p, Active a)
        {
            float u = a.time / a.duration, t = a.time, w;
            switch (a.type)
            {
                case CharacterReaction.Eat:
                    w = Window(u, .12f, .25f) * a.fade;
                    float reach = Bump(u, 0, .55f) * a.fade;
                    float chew = Smooth(.3f, .4f, u) * (1 - Smooth(.8f, 1, u)) * a.fade;
                    p.rightRaise += 70 * reach;
                    p.rightSpread -= 12 * reach;
                    p.bodyPitch += 7 * w;
                    p.headPitch += 5 * w + Mathf.Sin(t * 17) * 5 * chew;
                    p.squash += Mathf.Sin(t * 17) * .015f * chew;
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
                    p.eyeClose += .65f * w;
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
                    p.eyeWide += .35f * w;
                    break;
                case CharacterReaction.Hurt:
                    w = Window(u, .05f, .5f) * a.fade;
                    float hit = Bump(u, 0, .4f) * a.fade;
                    p.bodyPitch -= 8 * hit;
                    p.squash -= .09f * hit;
                    p.bodyRoll += Mathf.Sin(t * 40) * 2 * w;
                    p.headPitch += 8 * w;
                    p.eyeClose += hit;
                    break;
                case CharacterReaction.Give:
                    w = Window(u, .15f, .3f) * a.fade;
                    float yaw = a.target != null ? YawTo(a.target.position) : 0;
                    p.bodyYaw += Mathf.Clamp(yaw, -60, 60) * .5f * w;
                    p.bodyPitch += 10 * w;
                    p.headPitch += 6 * w;
                    if (yaw < -5) { p.leftRaise += 65 * w; p.leftSpread -= 8 * w; }
                    else { p.rightRaise += 65 * w; p.rightSpread -= 8 * w; }
                    break;
                case CharacterReaction.Cheer:
                    w = Window(u, .08f, .2f) * a.fade;
                    p.bodyUp += Mathf.Abs(Mathf.Sin(u * Mathf.PI * 4)) * .06f * w;
                    p.squash += Mathf.Sin(u * Mathf.PI * 8) * .06f * w;
                    p.leftRaise += (120 + Mathf.Sin(t * 12) * 12) * w;
                    p.rightRaise += (120 + Mathf.Sin(t * 12 + 1.5f) * 12) * w;
                    p.leftSpread += 55 * w;
                    p.rightSpread += 55 * w;
                    p.headPitch -= 12 * w;
                    p.eyeClose += .55f * w;
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
                    p.eyeClose += .85f * w;
                    break;
                case CharacterReaction.Wave:
                    w = Window(u, .12f, .25f) * a.fade;
                    p.rightRaise += 115 * w;
                    p.rightSpread += (45 + Mathf.Sin(t * 10) * 20) * w;
                    p.headRoll += 8 * w;
                    p.bodyRoll -= 3 * w;
                    p.eyeClose += .3f * w;
                    break;
                case CharacterReaction.Nod:
                    w = Window(u, .1f, .2f) * a.fade;
                    p.headPitch += Mathf.Sin(u * Mathf.PI * 4) * 10 * w;
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
            lookYaw = Mathf.SmoothDamp(lookYaw, Mathf.Clamp(yaw, -70, 70) * weight, ref lookYawVelocity, .3f);
            lookPitch = Mathf.SmoothDamp(lookPitch, Mathf.Clamp(pitch, -25, 30) * weight, ref lookPitchVelocity, .3f);
            p.bodyYaw += lookYaw * .25f;
            p.headYaw += lookYaw * .75f;
            p.headPitch += lookPitch;
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
            var bodyRotation = Quaternion.AngleAxis(p.bodyYaw, upP) * Quaternion.AngleAxis(p.bodyPitch, rightP)
                * Quaternion.AngleAxis(p.bodyRoll, fwdP);
            float s = Mathf.Clamp(1 + p.squash, .6f, 1.4f);
            var squash = Vector3.one * (1 / Mathf.Sqrt(s));
            squash[squashAxis] = s;
            body.Apply(upPv * p.bodyUp + hipP - bodyRotation * hipP, bodyRotation, squash);

            var headRotation = Quaternion.AngleAxis(p.headYaw, upH) * Quaternion.AngleAxis(p.headPitch, rightH)
                * Quaternion.AngleAxis(p.headRoll, fwdH);
            head.Apply(neckH - headRotation * neckH, headRotation, Vector3.one);

            ApplyArm(leftArm, p.leftRaise, p.leftSpread);
            ApplyArm(rightArm, p.rightRaise, p.rightSpread);

            float close = Mathf.Clamp01(p.eyeClose), wide = 1 + Mathf.Max(0, p.eyeWide) * (1 - close);
            foreach (var e in eyes)
            {
                var scale = Vector3.one * wide;
                scale[e.upAxis] = Mathf.Lerp(1, .08f, close) * wide;
                e.joint.Apply(Vector3.zero, Quaternion.identity, scale);
            }
        }

        static void ApplyArm(Arm arm, float raise, float spread)
        {
            if (arm == null) return;
            arm.joint.Apply(Vector3.zero, Quaternion.AngleAxis(raise, arm.raiseAxis) * Quaternion.AngleAxis(spread, arm.spreadAxis), Vector3.one);
        }

        float YawTo(Vector3 world)
        {
            Vector3 dir = Vector3.ProjectOnPlane(ParentDirection(transform, world - head.transform.position), upP);
            return dir.sqrMagnitude < 1e-8f ? 0 : Vector3.SignedAngle(fwdP, dir, upP);
        }

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
                if (t.name.StartsWith(prefix)) return t;
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
            return head && arm;
        }

        /// <summary>Pasang CharacterAnimator di semua karakter (Head + Arm_*) di scene yang belum punya.</summary>
        public static int AttachAll(Scene scene)
        {
            int added = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (LooksLikeCharacter(t) && t.GetComponent<CharacterAnimator>() == null)
                    {
                        t.gameObject.AddComponent<CharacterAnimator>();
                        added++;
                    }
            return added;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        // Karakter di scene yang belum dipasangi komponen tetap hidup saat Play.
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
