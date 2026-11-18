using System;
using System.Collections;
using System.Collections.Generic;
using BiteOrBetray.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Low-profile Victorian mechanical "Parlour Candy Roulette" centerpiece (Option 2 + Cara A):
    /// - Outer Walnut & Brass Pin Ring + Front Mechanical Chute + Animated Lever + Attached Golden CandyTray
    ///   combined into 1 single MeshRenderer ("RouletteTurntable") that rotates smoothly to face whichever seat holds the turn.
    /// - Inner 3-Wedge Cream Platter + Brass Spinner Knob + 36 Twisted-Wrapper Bonbon Candies combined into
    ///   1 single MeshRenderer ("Three compartment dish" under Dispenser) that spins on every draw and depletes live
    ///   as CandiesInDispenser decreases.
    /// Max height is 10.6 cm above the table (world y ~ 0.871 m < 0.91 m) so opposite-player sightlines stay 100% clear.
    /// </summary>
    public sealed class ParlourCandyRoulette
    {
        public const int BonbonsPerWedge = 18;
        public const int TotalBonbonSlots = BonbonsPerWedge * 3;
        const float TrayRadiusFromCenter = 0.438f;
        const float CandyRestHeight = 0.033f;
        const float ChuteMouthRadius = 0.318f;
        const float ChuteMouthHeight = 0.038f;

        static readonly Color32 WalnutWood     = new Color32(92, 50, 28, 36);
        static readonly Color32 WalnutDark     = new Color32(54, 29, 16, 24);
        static readonly Color32 CreamPorcelain = new Color32(246, 235, 208, 34);
        static readonly Color32 BrassTrim      = new Color32(212, 158, 62, 232);
        static readonly Color32 BrassGold      = new Color32(244, 194, 84, 250);
        static readonly Color32 TraySatin      = new Color32(236, 202, 124, 185);
        static readonly Color32 SlateDark      = new Color32(28, 32, 36, 20);
        static readonly Color32 RubyBonbon     = new Color32(232, 26, 62, 248);
        static readonly Color32 BlueBonbon     = new Color32(30, 92, 244, 248);
        static readonly Color32 GoldBonbon     = new Color32(248, 178, 22, 248);

        public readonly GameObject turntableObject;
        public readonly GameObject wheelObject;
        public readonly Transform chuteSpawnTransform;

        readonly Transform turntable;
        readonly Transform wheel;
        readonly List<GameObject> hiddenSceneParts = new List<GameObject>();
        readonly Transform movedDice;
        readonly Vector3 originalDiceLocalPos;
        readonly Vector3 originalDispenserLocalPos;
        readonly Transform dispenserRoot;

        Mesh turntableMesh;
        Vector3[] turntableBaseVerts;
        Vector3[] turntableWorkVerts;
        Vector3[] turntableBaseNorms;
        Vector3[] turntableWorkNorms;
        int leverStart, leverCount;
        Vector3 leverPivotLocal;
        float currentLeverAngle;

        Mesh wheelMesh;
        Vector3[] wheelBaseVerts;
        Vector3[] wheelWorkVerts;
        readonly int[] bonbonStart = new int[TotalBonbonSlots];
        readonly int[] bonbonCount = new int[TotalBonbonSlots];
        readonly int[] depletionOrder = new int[TotalBonbonSlots];

        int lastVisibleBonbons = -1;
        int roundBaselinePool = 24;
        float wheelYaw;
        float spinTimer = -1f;
        float spinDuration = 0.55f;
        float spinStartYaw, spinTargetYaw;

        public Vector3 Center => turntable.position;
        public Vector3 TurntableForward => turntable.forward;
        public Vector3 CandyRestWorldPosition => turntable.TransformPoint(new Vector3(0f, CandyRestHeight, TrayRadiusFromCenter));
        public Quaternion CandyRestWorldRotation => turntable.rotation;
        public int VisibleBonbons { get; private set; }

        public ParlourCandyRoulette(Transform directorParent, Transform artRoot, Transform dispenser, Transform legacyTray, Vector3 defaultForward)
        {
            dispenserRoot = dispenser;
            originalDispenserLocalPos = dispenser.localPosition;

            // Hide legacy primitive cylinders/spheres/tray while keeping them in hierarchy (inactive) so scene-asset audits remain intact
            for (int i = 0; i < dispenser.childCount; i++)
            {
                var child = dispenser.GetChild(i).gameObject;
                if (child.name == "CandySpawnPoint") continue;
                if (child.activeSelf)
                {
                    child.SetActive(false);
                    hiddenSceneParts.Add(child);
                }
            }
            if (legacyTray != null && legacyTray.gameObject.activeSelf)
            {
                legacyTray.gameObject.SetActive(false);
                hiddenSceneParts.Add(legacyTray.gameObject);
            }
            var perfGeom = artRoot != null ? artRoot.Find("Performance Geometry") : null;
            if (perfGeom != null)
            {
                foreach (var mr in perfGeom.GetComponentsInChildren<MeshRenderer>())
                {
                    var mat = mr.sharedMaterial;
                    if (mat != null && mat.name.StartsWith("Dish", StringComparison.Ordinal))
                    {
                        mr.gameObject.SetActive(false);
                        hiddenSceneParts.Add(mr.gameObject);
                    }
                }
            }

            // Center Dispenser horizontally on the round table so all 6 seats share identical reach distance
            dispenser.localPosition = new Vector3(0f, originalDispenserLocalPos.y, 0f);

            movedDice = artRoot != null ? artRoot.Find("Dice") : null;
            if (movedDice != null)
            {
                originalDiceLocalPos = movedDice.localPosition;
                movedDice.localPosition = new Vector3(-0.46f, originalDiceLocalPos.y, -0.52f);
            }

            Vector3 flatFwd = new Vector3(defaultForward.x, 0f, defaultForward.z);
            if (flatFwd.sqrMagnitude < 1e-5f) flatFwd = Vector3.back;
            flatFwd.Normalize();

            // 1. Turntable (outer walnut-brass pin drum + front chute + animated lever + attached golden CandyTray)
            turntableObject = new GameObject("RouletteTurntable");
            turntable = turntableObject.transform;
            turntable.SetParent(directorParent, false);
            turntable.SetPositionAndRotation(dispenser.position, Quaternion.LookRotation(flatFwd, Vector3.up));
            BuildTurntableMesh();

            var spawnGo = new GameObject("RouletteChuteMouth");
            chuteSpawnTransform = spawnGo.transform;
            chuteSpawnTransform.SetParent(turntable, false);
            chuteSpawnTransform.localPosition = new Vector3(0f, ChuteMouthHeight, ChuteMouthRadius);

            // 2. Spinning inner 3-wedge platter + brass spinner knob + 36 wrapped bonbons (named "Three compartment dish" for RegressionTests)
            wheelObject = new GameObject("Three compartment dish");
            wheel = wheelObject.transform;
            wheel.SetParent(dispenser, false);
            wheel.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            BuildWheelMesh();
            SetRemainingCandies(TotalBonbonSlots, TotalBonbonSlots);
        }

        public void SetFacingImmediate(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 1e-5f) return;
            turntable.rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        }

        public void StepTowardDirection(Vector3 worldDirection, float dt, float speed = 7.5f)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 1e-5f) return;
            Quaternion target = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            if (Quaternion.Angle(turntable.rotation, target) < 0.05f)
            {
                turntable.rotation = target;
                return;
            }
            turntable.rotation = Quaternion.Slerp(turntable.rotation, target, 1f - Mathf.Exp(-speed * dt));
        }

        public IEnumerator RotateToSeatCoroutine(Vector3 worldDirection, float duration)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 1e-5f) yield break;
            Quaternion startRot = turntable.rotation;
            Quaternion targetRot = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            float angle = Quaternion.Angle(startRot, targetRot);
            if (angle < 1.0f)
            {
                turntable.rotation = targetRot;
                yield break;
            }
            float actualDuration = Mathf.Max(0.05f, duration * Mathf.Clamp(angle / 120f, 0.45f, 1.15f));
            for (float t = 0f; t < actualDuration; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / actualDuration);
                float ease = u * u * (3f - 2f * u);
                turntable.rotation = Quaternion.Slerp(startRot, targetRot, ease);
                yield return null;
            }
            turntable.rotation = targetRot;
        }

        public void TriggerSpinAndDispense(CandyColor dispensedColor, int remainingInPool)
        {
            // Spin the wheel so the matching color wedge sweeps past the front chute (+Z = 0 deg) with 1.5 full turns
            float wedgeAngle = dispensedColor == CandyColor.Red ? -60f
                : dispensedColor == CandyColor.Blue ? -180f : -300f;
            spinStartYaw = wheelYaw;
            float baseTarget = Mathf.Floor(wheelYaw / 360f) * 360f + 540f + wedgeAngle;
            if (baseTarget - spinStartYaw < 360f) baseTarget += 360f;
            spinTargetYaw = baseTarget;
            spinDuration = 0.56f;
            spinTimer = 0f;
            SetRemainingCandies(remainingInPool, Mathf.Max(roundBaselinePool, remainingInPool + 1));
        }

        public void NotifyNewRound(int totalPool)
        {
            roundBaselinePool = Mathf.Max(6, totalPool);
            SetRemainingCandies(totalPool, roundBaselinePool);
        }

        public void SetRemainingCandies(int remaining, int baselinePool = -1)
        {
            if (baselinePool > 0) roundBaselinePool = Mathf.Max(6, baselinePool);
            int targetVisible;
            if (remaining <= 0) targetVisible = 0;
            else
            {
                float ratio = Mathf.Clamp01(remaining / (float)Mathf.Max(1, roundBaselinePool));
                targetVisible = Mathf.Clamp(Mathf.CeilToInt(ratio * TotalBonbonSlots), 1, TotalBonbonSlots);
                if (remaining < TotalBonbonSlots) targetVisible = Mathf.Min(targetVisible, remaining);
            }
            if (targetVisible == lastVisibleBonbons || wheelMesh == null) return;
            lastVisibleBonbons = targetVisible;
            VisibleBonbons = targetVisible;

            for (int rank = 0; rank < TotalBonbonSlots; rank++)
            {
                int slot = depletionOrder[rank];
                int s = bonbonStart[slot], c = bonbonCount[slot];
                bool show = rank < targetVisible;
                if (show)
                {
                    for (int k = 0; k < c; k++) wheelWorkVerts[s + k] = wheelBaseVerts[s + k];
                }
                else
                {
                    for (int k = 0; k < c; k++) wheelWorkVerts[s + k] = Vector3.zero;
                }
            }
            wheelMesh.SetVertices(wheelWorkVerts);
        }

        public void TickAnimation(float dt)
        {
            if (spinTimer < 0f) return;
            spinTimer += dt;
            float u = Mathf.Clamp01(spinTimer / Mathf.Max(0.05f, spinDuration));
            // Cubic ease-out roulette deceleration
            float inv = 1f - u;
            float easeOut = 1f - inv * inv * inv;
            wheelYaw = Mathf.Lerp(spinStartYaw, spinTargetYaw, easeOut);
            wheel.localRotation = Quaternion.Euler(0f, wheelYaw, 0f);

            // Mechanical brass lever pulls down during [0.45 .. 0.85] as the gate releases the candy, then springs back
            float leverPulse = u < 0.40f ? 0f : u < 0.68f ? Mathf.SmoothStep(0f, 1f, (u - 0.40f) / 0.28f)
                : Mathf.SmoothStep(1f, 0f, (u - 0.68f) / 0.32f);
            SetLeverAngle(leverPulse * 38f);

            if (u >= 1f)
            {
                spinTimer = -1f;
                SetLeverAngle(0f);
            }
        }

        void SetLeverAngle(float degrees)
        {
            if (Mathf.Abs(degrees - currentLeverAngle) < 0.1f || turntableMesh == null) return;
            currentLeverAngle = degrees;
            Quaternion q = Quaternion.Euler(degrees, 0f, 0f);
            int end = leverStart + leverCount;
            for (int i = leverStart; i < end; i++)
            {
                turntableWorkVerts[i] = leverPivotLocal + q * (turntableBaseVerts[i] - leverPivotLocal);
                turntableWorkNorms[i] = q * turntableBaseNorms[i];
            }
            turntableMesh.SetVertices(turntableWorkVerts);
            turntableMesh.SetNormals(turntableWorkNorms);
        }

        public void Dispose()
        {
            if (dispenserRoot != null) dispenserRoot.localPosition = originalDispenserLocalPos;
            if (movedDice != null) movedDice.localPosition = originalDiceLocalPos;
            for (int i = 0; i < hiddenSceneParts.Count; i++)
                if (hiddenSceneParts[i] != null) hiddenSceneParts[i].SetActive(true);
            hiddenSceneParts.Clear();
            if (turntableMesh != null) Object.Destroy(turntableMesh);
            if (wheelMesh != null) Object.Destroy(wheelMesh);
            if (turntableObject != null) Object.Destroy(turntableObject);
            if (wheelObject != null) Object.Destroy(wheelObject);
        }

        // ------------------------------------------------------------------ Mesh Builders

        void BuildTurntableMesh()
        {
            var v = new List<Vector3>(1400);
            var n = new List<Vector3>(1400);
            var c = new List<Color32>(1400);
            var t = new List<int>(2800);

            const int segs = 28;
            // Stepped dark walnut drum ring (rOut 0.316 -> rIn 0.236, y = 0..0.052)
            AddRingFrustum(v, n, c, t, segs, 0.320f, 0.314f, 0.000f, 0.014f, WalnutDark);
            AddRingFrustum(v, n, c, t, segs, 0.314f, 0.304f, 0.014f, 0.052f, WalnutWood);
            AddAnnulusTop(v, n, c, t, segs, 0.236f, 0.304f, 0.052f, WalnutWood);
            AddRingInnerWall(v, n, c, t, segs, 0.236f, 0.012f, 0.052f, WalnutDark);

            // Inlaid brushed-brass roulette pin track on top of walnut deck
            AddRingFrustum(v, n, c, t, segs, 0.281f, 0.279f, 0.052f, 0.0555f, BrassTrim);
            AddAnnulusTop(v, n, c, t, segs, 0.263f, 0.279f, 0.0555f, BrassTrim);
            AddRingInnerWall(v, n, c, t, segs, 0.263f, 0.052f, 0.0555f, BrassTrim);

            // 11 brass roulette pins around the track (skipping 0 deg where the front brass release housing sits)
            for (int p = 1; p < 12; p++)
            {
                float a = p * 30f * Mathf.Deg2Rad;
                Vector3 pinBase = new Vector3(Mathf.Sin(a) * 0.271f, 0.0555f, Mathf.Cos(a) * 0.271f);
                AddCylinderY(v, n, c, t, 8, pinBase, 0.0052f, 0.013f, BrassGold);
            }

            // Front mechanical brass release housing (+Z = 0 deg)
            AddBox(v, n, c, t, new Vector3(0f, 0.030f, 0.282f), new Vector3(0.116f, 0.058f, 0.096f), BrassTrim);
            AddBox(v, n, c, t, new Vector3(0f, 0.060f, 0.272f), new Vector3(0.104f, 0.005f, 0.072f), BrassGold);
            // Pointer pin atop the brass release gate pointing inward toward the spinning wheel
            AddCylinderY(v, n, c, t, 8, new Vector3(0f, 0.062f, 0.252f), 0.0055f, 0.011f, BrassGold);
            // Dark chute mouth recess
            AddBox(v, n, c, t, new Vector3(0f, 0.028f, 0.328f), new Vector3(0.064f, 0.030f, 0.012f), SlateDark);

            // Sloped brass delivery chute leading from housing into the attached CandyTray
            AddBox(v, n, c, t, new Vector3(0f, 0.019f, 0.354f), new Vector3(0.068f, 0.006f, 0.062f), BrassGold);
            AddBox(v, n, c, t, new Vector3(-0.036f, 0.028f, 0.354f), new Vector3(0.007f, 0.020f, 0.062f), BrassTrim);
            AddBox(v, n, c, t, new Vector3( 0.036f, 0.028f, 0.354f), new Vector3(0.007f, 0.020f, 0.062f), BrassTrim);

            // Rigid walnut-and-brass bridge tongue bolting the CandyTray to the roulette drum
            AddBox(v, n, c, t, new Vector3(0f, 0.006f, 0.362f), new Vector3(0.092f, 0.010f, 0.076f), WalnutDark);

            // Attached Golden Brushed-Brass CandyTray (centered at z = TrayRadiusFromCenter = 0.438 m)
            Vector3 trayCenter = new Vector3(0f, 0.007f, TrayRadiusFromCenter);
            AddBox(v, n, c, t, trayCenter, new Vector3(0.160f, 0.012f, 0.106f), BrassTrim);
            AddBox(v, n, c, t, new Vector3(0f, 0.0135f, TrayRadiusFromCenter), new Vector3(0.140f, 0.003f, 0.086f), TraySatin);
            // 4 raised beveled brass tray walls
            AddBox(v, n, c, t, new Vector3(0f, 0.020f, TrayRadiusFromCenter + 0.048f), new Vector3(0.162f, 0.016f, 0.010f), BrassGold);
            AddBox(v, n, c, t, new Vector3(0f, 0.020f, TrayRadiusFromCenter - 0.048f), new Vector3(0.162f, 0.016f, 0.010f), BrassGold);
            AddBox(v, n, c, t, new Vector3(-0.076f, 0.020f, TrayRadiusFromCenter), new Vector3(0.010f, 0.016f, 0.086f), BrassGold);
            AddBox(v, n, c, t, new Vector3( 0.076f, 0.020f, TrayRadiusFromCenter), new Vector3(0.010f, 0.016f, 0.086f), BrassGold);

            // Side mechanical lever (animated vertex slice) on the right side of the brass gate
            leverPivotLocal = new Vector3(0.062f, 0.030f, 0.292f);
            AddBox(v, n, c, t, leverPivotLocal, new Vector3(0.016f, 0.014f, 0.014f), BrassTrim);
            leverStart = v.Count;
            AddBox(v, n, c, t, leverPivotLocal + new Vector3(0.014f, 0.000f, 0.000f), new Vector3(0.020f, 0.007f, 0.007f), BrassTrim);
            AddBox(v, n, c, t, leverPivotLocal + new Vector3(0.022f, 0.022f, 0.006f), new Vector3(0.007f, 0.044f, 0.007f), BrassTrim);
            AddCylinderY(v, n, c, t, 8, leverPivotLocal + new Vector3(0.022f, 0.042f, 0.006f), 0.010f, 0.014f, BrassGold);
            leverCount = v.Count - leverStart;

            turntableBaseVerts = v.ToArray();
            turntableWorkVerts = (Vector3[])turntableBaseVerts.Clone();
            turntableBaseNorms = n.ToArray();
            turntableWorkNorms = (Vector3[])turntableBaseNorms.Clone();

            turntableMesh = new Mesh { name = "RouletteTurntable_Mesh" };
            turntableMesh.SetVertices(turntableWorkVerts);
            turntableMesh.SetNormals(turntableWorkNorms);
            turntableMesh.SetColors(c);
            turntableMesh.SetTriangles(t, 0, true);

            turntableObject.AddComponent<MeshFilter>().sharedMesh = turntableMesh;
            var mr = turntableObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ParlourDiegeticProps.SharedMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void BuildWheelMesh()
        {
            var v = new List<Vector3>(3800);
            var n = new List<Vector3>(3800);
            var c = new List<Color32>(3800);
            var t = new List<int>(7800);

            const int segs = 24;
            // Cream porcelain recessed basin + curved inner rim
            AddAnnulusTop(v, n, c, t, segs, 0.052f, 0.226f, 0.016f, CreamPorcelain);
            AddRingInnerWall(v, n, c, t, segs, 0.226f, 0.016f, 0.046f, CreamPorcelain);
            AddAnnulusTop(v, n, c, t, segs, 0.226f, 0.235f, 0.046f, CreamPorcelain);

            // 3 radial cream-and-brass divider walls at 0, 120, 240 deg
            for (int w = 0; w < 3; w++)
            {
                Quaternion q = Quaternion.Euler(0f, w * 120f, 0f);
                AddRotatedBox(v, n, c, t, q, q * new Vector3(0f, 0.030f, 0.140f), new Vector3(0.014f, 0.028f, 0.174f), CreamPorcelain);
                AddRotatedBox(v, n, c, t, q, q * new Vector3(0f, 0.045f, 0.140f), new Vector3(0.008f, 0.003f, 0.172f), BrassTrim);
            }

            // Center dark walnut boss + tiered brushed-brass spinner knob (max height = 0.104 m)
            AddCylinderY(v, n, c, t, 16, new Vector3(0f, 0.016f, 0f), 0.056f, 0.030f, WalnutWood);
            AddCylinderY(v, n, c, t, 16, new Vector3(0f, 0.046f, 0f), 0.043f, 0.008f, BrassTrim);
            AddCylinderY(v, n, c, t, 12, new Vector3(0f, 0.054f, 0f), 0.016f, 0.022f, BrassTrim);
            AddCylinderY(v, n, c, t, 16, new Vector3(0f, 0.076f, 0f), 0.035f, 0.016f, BrassGold);
            AddCylinderY(v, n, c, t, 12, new Vector3(0f, 0.092f, 0f), 0.022f, 0.010f, BrassGold);

            // 54 plump 3D twisted-wrapper bonbon candies (18 per color wedge: 11 base layer + 7 heaped upper layer)
            Color32[] wedgeColors = { RubyBonbon, BlueBonbon, GoldBonbon };
            var rng = new System.Random(20261118);
            int orderIdx = 0;
            for (int i = 0; i < BonbonsPerWedge; i++)
                for (int w = 0; w < 3; w++)
                    depletionOrder[orderIdx++] = w * BonbonsPerWedge + i;

            for (int w = 0; w < 3; w++)
            {
                float wedgeStartDeg = w * 120f + 13f;
                float wedgeSpanDeg = 94f;
                Color32 col = wedgeColors[w];
                for (int i = 0; i < BonbonsPerWedge; i++)
                {
                    int slot = w * BonbonsPerWedge + i;
                    float radius, angDeg, y;
                    if (i < 6)
                    {
                        radius = 0.188f + ((float)rng.NextDouble() - 0.5f) * 0.010f;
                        angDeg = wedgeStartDeg + ((i + 0.5f) / 6f) * wedgeSpanDeg;
                        y = 0.028f;
                    }
                    else if (i < 10)
                    {
                        int k = i - 6;
                        radius = 0.136f + ((float)rng.NextDouble() - 0.5f) * 0.010f;
                        angDeg = wedgeStartDeg + 6f + ((k + 0.5f) / 4f) * (wedgeSpanDeg - 12f);
                        y = 0.028f;
                    }
                    else if (i < 12)
                    {
                        int k = i - 10;
                        radius = 0.092f + ((float)rng.NextDouble() - 0.5f) * 0.008f;
                        angDeg = wedgeStartDeg + 18f + ((k + 0.5f) / 2f) * (wedgeSpanDeg - 36f);
                        y = 0.028f;
                    }
                    else
                    {
                        int k = i - 12;
                        radius = (k < 4 ? 0.164f : 0.118f) + ((float)rng.NextDouble() - 0.5f) * 0.010f;
                        int nArc = k < 4 ? 4 : 2;
                        int idxArc = k < 4 ? k : k - 4;
                        angDeg = wedgeStartDeg + 10f + ((idxArc + 0.5f) / nArc) * (wedgeSpanDeg - 20f);
                        y = 0.045f;
                    }
                    float rad = angDeg * Mathf.Deg2Rad;
                    Vector3 pos = new Vector3(Mathf.Sin(rad) * radius, y, Mathf.Cos(rad) * radius);
                    Quaternion rot = Quaternion.Euler(
                        ((float)rng.NextDouble() - 0.5f) * 16f,
                        angDeg + 90f + ((float)rng.NextDouble() - 0.5f) * 48f,
                        ((float)rng.NextDouble() - 0.5f) * 14f);

                    bonbonStart[slot] = v.Count;
                    AddWrappedBonbon(v, n, c, t, pos, rot, 0.036f, col);
                    bonbonCount[slot] = v.Count - bonbonStart[slot];
                }
            }

            wheelBaseVerts = v.ToArray();
            wheelWorkVerts = (Vector3[])wheelBaseVerts.Clone();

            wheelMesh = new Mesh { name = "RouletteWheel_Mesh" };
            wheelMesh.SetVertices(wheelWorkVerts);
            wheelMesh.SetNormals(n);
            wheelMesh.SetColors(c);
            wheelMesh.SetTriangles(t, 0, true);

            wheelObject.AddComponent<MeshFilter>().sharedMesh = wheelMesh;
            var mr = wheelObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ParlourDiegeticProps.SharedMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ------------------------------------------------------------------ Geometry Helpers

        static void AddQuad(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color32 col)
        {
            int i0 = v.Count;
            Vector3 norm = Vector3.Cross(b - a, d - a).normalized;
            v.Add(a); v.Add(b); v.Add(d); v.Add(e);
            n.Add(norm); n.Add(norm); n.Add(norm); n.Add(norm);
            c.Add(col); c.Add(col); c.Add(col); c.Add(col);
            t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
            t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3);
        }

        static void AddTri(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Vector3 a, Vector3 b, Vector3 d, Color32 col)
        {
            int i0 = v.Count;
            Vector3 norm = Vector3.Cross(b - a, d - a).normalized;
            v.Add(a); v.Add(b); v.Add(d);
            n.Add(norm); n.Add(norm); n.Add(norm);
            c.Add(col); c.Add(col); c.Add(col);
            t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
        }

        static void AddBox(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Vector3 center, Vector3 size, Color32 col) =>
            AddRotatedBox(v, n, c, t, Quaternion.identity, center, size, col);

        static void AddRotatedBox(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Quaternion rot, Vector3 center, Vector3 size, Color32 col)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float sx, float sy, float sz) => center + rot * new Vector3(sx * h.x, sy * h.y, sz * h.z);
            Vector3 p000 = P(-1, -1, -1), p100 = P(1, -1, -1), p101 = P(1, -1, 1), p001 = P(-1, -1, 1);
            Vector3 p010 = P(-1,  1, -1), p110 = P(1,  1, -1), p111 = P(1,  1, 1), p011 = P(-1,  1, 1);
            AddQuad(v, n, c, t, p010, p011, p111, p110, col);
            AddQuad(v, n, c, t, p000, p010, p110, p100, col);
            AddQuad(v, n, c, t, p100, p110, p111, p101, col);
            AddQuad(v, n, c, t, p101, p111, p011, p001, col);
            AddQuad(v, n, c, t, p001, p011, p010, p000, col);
        }

        static void AddRingFrustum(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            int segs, float rBottom, float rTop, float yBottom, float yTop, Color32 col)
        {
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs;
                float a1 = (i + 1) * Mathf.PI * 2f / segs;
                float s0 = Mathf.Sin(a0), c0 = Mathf.Cos(a0);
                float s1 = Mathf.Sin(a1), c1 = Mathf.Cos(a1);
                // Going clockwise (a0 -> a1): b0 -> t0 -> t1 -> b1 has outward normal (+sin, +, +cos)
                Vector3 b0 = new Vector3(s0 * rBottom, yBottom, c0 * rBottom);
                Vector3 b1 = new Vector3(s1 * rBottom, yBottom, c1 * rBottom);
                Vector3 t0 = new Vector3(s0 * rTop, yTop, c0 * rTop);
                Vector3 t1 = new Vector3(s1 * rTop, yTop, c1 * rTop);
                AddQuad(v, n, c, t, b1, t1, t0, b0, col);
            }
        }

        static void AddRingInnerWall(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            int segs, float r, float yBottom, float yTop, Color32 col)
        {
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs;
                float a1 = (i + 1) * Mathf.PI * 2f / segs;
                Vector3 b0 = new Vector3(Mathf.Sin(a0) * r, yBottom, Mathf.Cos(a0) * r);
                Vector3 b1 = new Vector3(Mathf.Sin(a1) * r, yBottom, Mathf.Cos(a1) * r);
                Vector3 t0 = new Vector3(Mathf.Sin(a0) * r, yTop, Mathf.Cos(a0) * r);
                Vector3 t1 = new Vector3(Mathf.Sin(a1) * r, yTop, Mathf.Cos(a1) * r);
                // Inward-facing normal toward center
                AddQuad(v, n, c, t, b0, t0, t1, b1, col);
            }
        }

        static void AddAnnulusTop(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            int segs, float rIn, float rOut, float y, Color32 col)
        {
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs;
                float a1 = (i + 1) * Mathf.PI * 2f / segs;
                Vector3 i0 = new Vector3(Mathf.Sin(a0) * rIn, y, Mathf.Cos(a0) * rIn);
                Vector3 i1 = new Vector3(Mathf.Sin(a1) * rIn, y, Mathf.Cos(a1) * rIn);
                Vector3 o0 = new Vector3(Mathf.Sin(a0) * rOut, y, Mathf.Cos(a0) * rOut);
                Vector3 o1 = new Vector3(Mathf.Sin(a1) * rOut, y, Mathf.Cos(a1) * rOut);
                // Upward-facing (+Y) normal: i0 -> o0 -> o1 -> i1
                AddQuad(v, n, c, t, i0, o0, o1, i1, col);
            }
        }

        static void AddCylinderY(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            int segs, Vector3 bottomCenter, float r, float height, Color32 col)
        {
            float y0 = bottomCenter.y, y1 = bottomCenter.y + height;
            Vector3 topApex = new Vector3(bottomCenter.x, y1 + r * 0.16f, bottomCenter.z);
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs;
                float a1 = (i + 1) * Mathf.PI * 2f / segs;
                Vector3 p00 = new Vector3(bottomCenter.x + Mathf.Sin(a0) * r, y0, bottomCenter.z + Mathf.Cos(a0) * r);
                Vector3 p10 = new Vector3(bottomCenter.x + Mathf.Sin(a1) * r, y0, bottomCenter.z + Mathf.Cos(a1) * r);
                Vector3 p01 = new Vector3(bottomCenter.x + Mathf.Sin(a0) * r, y1, bottomCenter.z + Mathf.Cos(a0) * r);
                Vector3 p11 = new Vector3(bottomCenter.x + Mathf.Sin(a1) * r, y1, bottomCenter.z + Mathf.Cos(a1) * r);
                AddQuad(v, n, c, t, p10, p11, p01, p00, col);
                AddTri(v, n, c, t, topApex, p01, p11, col);
            }
        }

        // Stylized 3D twisted-wrapper bonbon: plump central candy pillow + 2 flared wrapper bow-ends
        static void AddWrappedBonbon(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Vector3 center, Quaternion rot, float size, Color32 col)
        {
            // Plump central candy pillow
            AddRotatedBox(v, n, c, t, rot, center, new Vector3(size * 0.92f, size * 0.56f, size * 0.66f), col);
            AddRotatedBox(v, n, c, t, rot, center + rot * new Vector3(0f, size * 0.12f, 0f),
                new Vector3(size * 0.68f, size * 0.42f, size * 0.48f), col);
            // Left & right flared wrapper twists
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 neck = center + rot * new Vector3(side * size * 0.46f, 0f, 0f);
                Vector3 flareA = center + rot * new Vector3(side * size * 0.86f, size * 0.08f,  size * 0.30f);
                Vector3 flareB = center + rot * new Vector3(side * size * 0.86f, size * 0.08f, -size * 0.30f);
                Vector3 flareBot = center + rot * new Vector3(side * size * 0.80f, -size * 0.10f, 0f);
                if (side > 0)
                {
                    AddTri(v, n, c, t, neck, flareA, flareB, col);
                    AddTri(v, n, c, t, neck, flareB, flareBot, col);
                    AddTri(v, n, c, t, neck, flareBot, flareA, col);
                }
                else
                {
                    AddTri(v, n, c, t, neck, flareB, flareA, col);
                    AddTri(v, n, c, t, neck, flareBot, flareB, col);
                    AddTri(v, n, c, t, neck, flareA, flareBot, col);
                }
            }
        }
    }
}
