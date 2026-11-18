using System.Collections;
using System.Collections.Generic;
using BiteOrBetray.Art3D;
using BiteOrBetray.Core;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Option B: Animates the parlour Waiter (Grim Reaper with golden ServingCloche).
    /// Steps forward and opens the cloche lid when serving each new round, stalks behind vulnerable
    /// (low-HP) or active players across the back arc of the parlour, and reacts live to table outcomes.
    /// Zero extra draw calls: deforms only the upper lid components of the existing ServingCloche mesh.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class ParlourWaiterController : MonoBehaviour
    {
        public static readonly Vector3 HomeSpot = new Vector3(2.25f, .08f, 2.15f);
        static readonly Vector3 ServeSpot = new Vector3(1.58f, .08f, 1.82f);
        static readonly Vector3 LeftWingSpot = new Vector3(-1.58f, .08f, 1.98f);
        static readonly Vector3 CenterLeftSpot = new Vector3(-1.32f, .08f, 2.08f);
        static readonly Vector3 CenterRightSpot = new Vector3(1.32f, .08f, 2.08f);
        static readonly Vector3 RightWingSpot = new Vector3(1.88f, .08f, 1.95f);

        const float StalkLowHpThreshold = 2f;

        CharacterAnimator animator;
        Vector3 tableCenter, homeLocalPos, currentBaseLocalPos, targetLocalPos, moveVelocity;
        Quaternion homeLocalRot, prevAppliedBaseRot;
        Vector3 lookWorldPos;
        bool bound, inGameplay;
        float appliedHoverY, activeTempo = 1f;

        MeshFilter clocheFilter;
        Mesh runtimeClocheMesh;
        Vector3[] clocheBaseVerts, clocheBaseNorms, clocheWorkVerts, clocheWorkNorms;
        bool[] clocheIsLidVertex;
        Vector3 clocheLocalUp, clocheLocalBack, clocheLocalRight, clocheHingePivot;
        float currentClocheOpen, targetClocheOpen;
        Coroutine clochePulseCoroutine;

        public bool IsServing { get; private set; }
        public float ClocheOpenAmount => currentClocheOpen;
        public int StalkedSeatIndex { get; private set; } = -1;

        public static Transform EnsureNoseAnchor(Transform waiterRoot)
        {
            if (waiterRoot == null) return null;
            var head = waiterRoot.Find("Head");
            if (head == null) return null;
            var nose = head.Find("Nose");
            if (nose == null)
            {
                nose = new GameObject("Nose").transform;
                nose.SetParent(head, false);
                nose.localPosition = new Vector3(0f, 1.32f, .12f);
                nose.localRotation = Quaternion.identity;
            }
            return nose;
        }

        public void Bind(CharacterAnimator waiterAnimator, Vector3 center)
        {
            animator = waiterAnimator;
            tableCenter = center;
            animator?.RefreshFaceAnchor(EnsureNoseAnchor(transform));
            if (!bound)
            {
                homeLocalPos = transform.localPosition;
                homeLocalRot = transform.localRotation;
                currentBaseLocalPos = homeLocalPos;
                targetLocalPos = homeLocalPos;
                prevAppliedBaseRot = homeLocalRot;
                lookWorldPos = tableCenter + Vector3.up * 1.15f;
                SetupClocheMesh();
                bound = true;
            }
            else
            {
                ResetToHome();
            }
        }

        void SetupClocheMesh()
        {
            var cloche = transform.Find("Head/Serving Cloche");
            clocheFilter = cloche != null ? cloche.GetComponentInChildren<MeshFilter>(true) : null;
            if (clocheFilter == null || clocheFilter.sharedMesh == null) return;

            var src = clocheFilter.sharedMesh;
            runtimeClocheMesh = Instantiate(src);
            runtimeClocheMesh.name = "ServingCloche_Animated";
            clocheFilter.sharedMesh = runtimeClocheMesh;

            clocheBaseVerts = runtimeClocheMesh.vertices;
            clocheBaseNorms = runtimeClocheMesh.normals;
            if (clocheBaseNorms == null || clocheBaseNorms.Length != clocheBaseVerts.Length)
            {
                runtimeClocheMesh.RecalculateNormals();
                clocheBaseNorms = runtimeClocheMesh.normals;
            }
            clocheWorkVerts = (Vector3[])clocheBaseVerts.Clone();
            clocheWorkNorms = (Vector3[])clocheBaseNorms.Clone();

            clocheLocalUp = clocheFilter.transform.InverseTransformDirection(Vector3.up).normalized;
            clocheLocalRight = clocheFilter.transform.InverseTransformDirection(transform.right).normalized;
            clocheLocalBack = clocheFilter.transform.InverseTransformDirection(-transform.forward).normalized;

            int n = clocheBaseVerts.Length;
            var spatialIds = new Dictionary<(int, int, int), int>(n);
            var vertToSpatial = new int[n];
            int spatialCount = 0;
            float globalMinH = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                Vector3 v = clocheBaseVerts[i];
                globalMinH = Mathf.Min(globalMinH, Vector3.Dot(v, clocheLocalUp));
                var key = (Mathf.RoundToInt(v.x * 2000f), Mathf.RoundToInt(v.y * 2000f), Mathf.RoundToInt(v.z * 2000f));
                if (!spatialIds.TryGetValue(key, out int id))
                {
                    id = spatialCount++;
                    spatialIds[key] = id;
                }
                vertToSpatial[i] = id;
            }

            var adj = new List<int>[spatialCount];
            for (int i = 0; i < spatialCount; i++) adj[i] = new List<int>(6);
            int[] tris = runtimeClocheMesh.triangles;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = vertToSpatial[tris[t]];
                int b = vertToSpatial[tris[t + 1]];
                int c = vertToSpatial[tris[t + 2]];
                AddEdge(adj, a, b);
                AddEdge(adj, b, c);
                AddEdge(adj, c, a);
            }

            var compOfSpatial = new int[spatialCount];
            for (int i = 0; i < spatialCount; i++) compOfSpatial[i] = -1;
            var compMinH = new List<float>();
            var queue = new Queue<int>();
            int compCount = 0;
            for (int s = 0; s < spatialCount; s++)
            {
                if (compOfSpatial[s] >= 0) continue;
                int cid = compCount++;
                compOfSpatial[s] = cid;
                compMinH.Add(float.PositiveInfinity);
                queue.Enqueue(s);
                while (queue.Count > 0)
                {
                    int u = queue.Dequeue();
                    foreach (int w in adj[u])
                    {
                        if (compOfSpatial[w] >= 0) continue;
                        compOfSpatial[w] = cid;
                        queue.Enqueue(w);
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                int cid = compOfSpatial[vertToSpatial[i]];
                float h = Vector3.Dot(clocheBaseVerts[i], clocheLocalUp);
                if (h < compMinH[cid]) compMinH[cid] = h;
            }

            clocheIsLidVertex = new bool[n];
            Vector3 lidSum = Vector3.zero;
            int lidCount = 0;
            for (int i = 0; i < n; i++)
            {
                int cid = compOfSpatial[vertToSpatial[i]];
                if (compMinH[cid] - globalMinH >= .033f)
                {
                    clocheIsLidVertex[i] = true;
                    lidSum += clocheBaseVerts[i];
                    lidCount++;
                }
            }
            if (lidCount > 0)
                clocheHingePivot = (lidSum / lidCount) + clocheLocalBack * .14f - clocheLocalUp * .06f;
        }

        static void AddEdge(List<int>[] adj, int a, int b)
        {
            if (a == b) return;
            if (!adj[a].Contains(b)) adj[a].Add(b);
            if (!adj[b].Contains(a)) adj[b].Add(a);
        }

        void SetClocheOpen(float open01)
        {
            open01 = Mathf.Clamp01(open01);
            if (Mathf.Abs(open01 - currentClocheOpen) < .001f && runtimeClocheMesh != null) return;
            currentClocheOpen = open01;
            if (runtimeClocheMesh == null || clocheBaseVerts == null) return;

            if (open01 <= .0005f)
            {
                System.Array.Copy(clocheBaseVerts, clocheWorkVerts, clocheBaseVerts.Length);
                System.Array.Copy(clocheBaseNorms, clocheWorkNorms, clocheBaseNorms.Length);
            }
            else
            {
                float eased = open01 * open01 * (3f - 2f * open01);
                Vector3 lift = clocheLocalUp * (eased * .19f) + clocheLocalBack * (eased * .045f);
                Quaternion tilt = Quaternion.AngleAxis(-eased * 22f, clocheLocalRight);
                for (int i = 0; i < clocheBaseVerts.Length; i++)
                {
                    if (!clocheIsLidVertex[i]) continue;
                    clocheWorkVerts[i] = clocheHingePivot + tilt * (clocheBaseVerts[i] - clocheHingePivot) + lift;
                    clocheWorkNorms[i] = tilt * clocheBaseNorms[i];
                }
            }
            runtimeClocheMesh.SetVertices(clocheWorkVerts);
            runtimeClocheMesh.SetNormals(clocheWorkNorms);
            runtimeClocheMesh.RecalculateBounds();
        }

        public void SetGameplayActive(bool active)
        {
            inGameplay = active;
            if (!active)
            {
                targetLocalPos = homeLocalPos;
                lookWorldPos = tableCenter + Vector3.up * 1.15f;
                StalkedSeatIndex = -1;
                if (!IsServing) targetClocheOpen = 0f;
            }
        }

        public IEnumerator ServeRoundRoutine(float tempo)
        {
            if (!bound) yield break;
            IsServing = true;
            inGameplay = true;
            activeTempo = Mathf.Clamp(tempo, 1f, 8f);
            targetLocalPos = ServeSpot;
            lookWorldPos = tableCenter + Vector3.up * .85f;
            if (animator != null) animator.Play(CharacterReaction.Wave);

            float tScale = Mathf.Max(.05f, tempo);
            float openDur = .45f / tScale;
            for (float t = 0f; t < openDur; t += Time.deltaTime)
            {
                targetClocheOpen = Mathf.Clamp01(t / openDur);
                yield return null;
            }
            targetClocheOpen = 1f;
            yield return new WaitForSeconds(1.45f / tScale);

            float closeDur = .42f / tScale;
            for (float t = 0f; t < closeDur; t += Time.deltaTime)
            {
                targetClocheOpen = 1f - Mathf.Clamp01(t / closeDur);
                yield return null;
            }
            targetClocheOpen = 0f;
            IsServing = false;
            activeTempo = 1f;
        }

        internal void UpdateStalkTarget(IReadOnlyList<CandyTableDirector.Seat> seats, CandyTableDirector.Seat activeSeat)
        {
            if (!bound) return;
            inGameplay = true;

            CandyTableDirector.Seat prey = null;
            int lowestHp = int.MaxValue;
            if (seats != null)
            {
                for (int i = 0; i < seats.Count; i++)
                {
                    var s = seats[i];
                    if (s?.player == null || !s.player.IsAlive) continue;
                    if (s.player.Hp <= StalkLowHpThreshold && (s.player.Hp < lowestHp || (s.player.Hp == lowestHp && s == activeSeat)))
                    {
                        lowestHp = s.player.Hp;
                        prey = s;
                    }
                }
            }

            CandyTableDirector.Seat focus = prey ?? activeSeat;
            if (focus == null)
            {
                StalkedSeatIndex = -1;
                if (!IsServing) targetLocalPos = homeLocalPos;
                if (animator != null) animator.attention = null;
                return;
            }

            StalkedSeatIndex = focus.player != null ? focus.player.Seat : (focus.IsHuman ? -1 : focus.index);
            if (animator != null) animator.attention = focus.Head;
            lookWorldPos = focus.Head != null ? focus.Head.position : tableCenter + Vector3.up * 1.15f;

            if (!IsServing)
            {
                targetLocalPos = WaypointForSeat(focus);
                if (clochePulseCoroutine == null) targetClocheOpen = prey != null ? .22f : 0f;
            }
        }

        Vector3 WaypointForSeat(CandyTableDirector.Seat s)
        {
            if (s == null || s.IsHuman) return homeLocalPos;
            Transform src = s.mat != null ? s.mat : s.character != null ? s.character.transform : null;
            if (src == null) return homeLocalPos;
            float x = transform.parent != null ? transform.parent.InverseTransformPoint(src.position).x : src.position.x;
            if (x < -.75f) return LeftWingSpot;
            if (x < -.15f) return CenterLeftSpot;
            if (x < .45f) return CenterRightSpot;
            return RightWingSpot;
        }

        public void ReactToGoodCandy(Transform eaterHead)
        {
            if (animator == null) return;
            animator.attention = eaterHead;
            if (eaterHead != null) lookWorldPos = eaterHead.position;
            animator.Play(CharacterReaction.Nod);
        }

        public void ReactToBadCandy(Transform victimHead)
        {
            if (animator == null) return;
            animator.attention = victimHead;
            if (victimHead != null) lookWorldPos = victimHead.position;
            animator.Play(CharacterReaction.Laugh);
            PulseCloche(.45f, .9f);
        }

        public void ReactToItem(ItemType type, Transform actorHead)
        {
            if (animator == null) return;
            animator.attention = actorHead;
            if (actorHead != null) lookWorldPos = actorHead.position;
            bool dramatic = type == ItemType.HammerCrush || type == ItemType.GrapplingHook || type == ItemType.CloverCoin;
            animator.Play(dramatic ? CharacterReaction.Shocked : CharacterReaction.Nod);
        }

        public void ReactToElimination(Transform victimHead)
        {
            if (animator == null) return;
            animator.attention = victimHead;
            if (victimHead != null) lookWorldPos = victimHead.position;
            animator.Play(CharacterReaction.Cheer);
            PulseCloche(.85f, 1.3f);
        }

        void PulseCloche(float amount, float holdSeconds)
        {
            if (clochePulseCoroutine != null) StopCoroutine(clochePulseCoroutine);
            clochePulseCoroutine = StartCoroutine(ClochePulseRoutine(amount, holdSeconds));
        }

        IEnumerator ClochePulseRoutine(float amount, float holdSeconds)
        {
            if (!IsServing) targetClocheOpen = Mathf.Max(targetClocheOpen, amount);
            yield return new WaitForSeconds(holdSeconds);
            if (!IsServing) targetClocheOpen = StalkedSeatIndex >= 0 && inGameplay ? .18f : 0f;
            clochePulseCoroutine = null;
        }

        public void ResetToHome()
        {
            StopAllCoroutines();
            clochePulseCoroutine = null;
            IsServing = false;
            inGameplay = false;
            StalkedSeatIndex = -1;
            targetClocheOpen = 0f;
            SetClocheOpen(0f);
            if (!bound) return;

            transform.localPosition -= new Vector3(0f, appliedHoverY, 0f);
            transform.localPosition += homeLocalPos - currentBaseLocalPos;
            currentBaseLocalPos = targetLocalPos = homeLocalPos;
            moveVelocity = Vector3.zero;
            appliedHoverY = 0f;

            Quaternion deltaRot = homeLocalRot * Quaternion.Inverse(prevAppliedBaseRot);
            transform.localRotation = deltaRot * transform.localRotation;
            prevAppliedBaseRot = homeLocalRot;
            lookWorldPos = tableCenter + Vector3.up * 1.15f;
        }

        void Update()
        {
            if (!bound) return;

            float smooth = (IsServing ? .38f : .72f) / activeTempo;
            float maxSpeed = (IsServing ? 1.85f : 1.10f) * activeTempo;
            Vector3 nextBase = Vector3.SmoothDamp(currentBaseLocalPos, targetLocalPos, ref moveVelocity, smooth, maxSpeed, Time.deltaTime);
            float hoverY = inGameplay ? Mathf.Sin(Time.time * 2.2f) * .018f : 0f;

            transform.localPosition += (nextBase - currentBaseLocalPos) + new Vector3(0f, hoverY - appliedHoverY, 0f);
            currentBaseLocalPos = nextBase;
            appliedHoverY = hoverY;

            Quaternion desiredBaseRot = homeLocalRot;
            if (inGameplay)
            {
                Vector3 toTarget = lookWorldPos - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 1e-4f)
                {
                    Quaternion desiredWorld = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    Quaternion desiredLocal = transform.parent != null
                        ? Quaternion.Inverse(transform.parent.rotation) * desiredWorld
                        : desiredWorld;
                    // Blend 70% toward lookWorldPos from homeLocalRot so Waiter never turns his back away from Seat_0.
                    float dotFront = Vector3.Dot(homeLocalRot * Vector3.forward, desiredLocal * Vector3.forward);
                    if (dotFront > -.25f) desiredBaseRot = desiredLocal;
                }
            }
            Quaternion smoothBaseRot = Quaternion.Slerp(prevAppliedBaseRot, desiredBaseRot, 1f - Mathf.Exp(-4.5f * Time.deltaTime));
            Quaternion deltaRot = smoothBaseRot * Quaternion.Inverse(prevAppliedBaseRot);
            transform.localRotation = deltaRot * transform.localRotation;
            prevAppliedBaseRot = smoothBaseRot;

            float nextCloche = Mathf.MoveTowards(currentClocheOpen, targetClocheOpen, Time.deltaTime * 3.2f);
            SetClocheOpen(nextCloche);
        }

        public void Dispose()
        {
            if (runtimeClocheMesh != null)
            {
                Destroy(runtimeClocheMesh);
                runtimeClocheMesh = null;
            }
        }

        void OnDestroy() => Dispose();
    }
}
