using System;
using System.Collections;
using BiteOrBetray.Art3D;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    [DefaultExecutionOrder(600)]
    public sealed class ChibiHands : MonoBehaviour
    {
        public SeatedAvatar.Hand left, right;
        public CharacterAnimator character;
        public SeatedAvatar avatar;
        public VRTableInput input;
        Quaternion[] leftRest, rightRest;
        Transform candy;
        Vector3 carryPosition, carryOffset;
        bool carrying, attached;
        float curl, returnWeight;
        public bool CanCarry => avatar == null || (!avatar.CurrentPose.leftTracked && !avatar.CurrentPose.rightTracked);
        void Awake() { leftRest = Rest(left);rightRest = Rest(right); }
        static Quaternion[] Rest(SeatedAvatar.Hand hand)
        {
            var result = new Quaternion[hand.fingers.Length];
            for (int i = 0; i < result.Length; i++) result[i] = hand.fingers[i].localRotation;
            return result;
        }
        void UndoCarryOffset()
        {
            // Tracked avatars replace the hand pose every LateUpdate; bots retain additive joint positions.
            if (avatar == null && right.hand != null) right.hand.localPosition -= carryOffset;
            carryOffset = Vector3.zero;
        }
        void LateUpdate()
        {
            UndoCarryOffset();
            float target = carrying ? 1 : character != null ? character.GripWeight : 0;
            curl = Mathf.MoveTowards(curl, target, Time.deltaTime * 9);
            if (avatar == null || !avatar.CurrentPose.leftTracked) Apply(left, leftRest, target * .35f);
            if (avatar == null || !avatar.CurrentPose.rightTracked) Apply(right, rightRest, curl);
            if (input != null && avatar != null)
            {
                if (avatar.CurrentPose.leftTracked) input.AlignHeldCandy(UnityEngine.XR.XRNode.LeftHand, left.gripAnchor.position);
                if (avatar.CurrentPose.rightTracked) input.AlignHeldCandy(UnityEngine.XR.XRNode.RightHand, right.gripAnchor.position);
            }
            if (!CanCarry) returnWeight = 0;
            else returnWeight = carrying ? 1 : Mathf.MoveTowards(returnWeight, 0, Time.deltaTime * 6);
            if (returnWeight <= 0 || right.gripAnchor == null) return;
            var rest = right.hand.localPosition;
            right.hand.position += (carryPosition - right.gripAnchor.position) * returnWeight;
            carryOffset = right.hand.localPosition - rest;
            if (attached && candy != null) candy.position = right.gripAnchor.position;
        }
        public static void Apply(SeatedAvatar.Hand hand, Quaternion[] rest, float value)
        {
            for (int i = 0; i < hand.fingers.Length; i++)
                hand.fingers[i].localRotation = rest[i] * Quaternion.AngleAxis(Mathf.Lerp(hand.indexFingers[i] ? .12f : .38f, 1, value) * hand.fingerAngles[i], hand.fingerAxes[i]);
        }
        public IEnumerator CarryCandy(Transform item, Func<Vector3> mouth, float seconds)
        {
            if (right.gripAnchor == null || item == null) yield break;
            candy = item;carrying = true;attached = false;
            Vector3 from = right.gripAnchor.position, pickup = item.position;
            try
            {
                for (float t = 0; t < seconds * .4f; t += Time.deltaTime)
                { if (!CanCarry) yield break;carryPosition = Vector3.Lerp(from, pickup, Mathf.SmoothStep(0, 1, t / (seconds * .4f)));yield return null; }
                carryPosition = pickup;
                yield return null;
                attached = true;
                for (float t = 0; t < seconds * .6f; t += Time.deltaTime)
                { if (!CanCarry) yield break;carryPosition = Vector3.Lerp(pickup, mouth(), Mathf.SmoothStep(0, 1, t / (seconds * .6f)));yield return null; }
                if (item != null) item.position = carryPosition = mouth();
            }
            finally { carrying = attached = false;candy = null; }
        }
        public IEnumerator ThrowCandy(Transform item, Func<Vector3> targetMouth, float seconds)
        {
            if (item == null) yield break;
            Quaternion startRot = item.rotation;
            Vector3 pickup = item.position;
            Vector3 initialGoal = targetMouth();
            Vector3 throwDir = initialGoal - pickup;
            throwDir.y = 0f;
            float dist = throwDir.magnitude;
            throwDir = dist > 1e-4f ? throwDir / dist : Vector3.forward;

            // Phase 1: Giver's hand reaches into giver's own CandyTray, grabs the candy, and flicks forward to release
            Vector3 releasePos = pickup + throwDir * .20f + Vector3.up * .14f;
            if (right.gripAnchor != null && CanCarry)
            {
                candy = item;
                carrying = true;
                attached = false;
                Vector3 from = right.gripAnchor.position;
                Vector3 windup = pickup - throwDir * .04f + Vector3.up * .07f;
                try
                {
                    float grabDur = seconds * .26f;
                    for (float t = 0f; t < grabDur; t += Time.deltaTime)
                    {
                        if (!CanCarry || item == null) break;
                        carryPosition = Vector3.Lerp(from, pickup, Mathf.SmoothStep(0f, 1f, t / grabDur));
                        yield return null;
                    }
                    if (CanCarry && item != null)
                    {
                        carryPosition = pickup;
                        yield return null;
                        attached = true;
                        float flickDur = seconds * .22f;
                        for (float t = 0f; t < flickDur; t += Time.deltaTime)
                        {
                            if (!CanCarry || item == null) break;
                            float u = Mathf.Clamp01(t / flickDur);
                            carryPosition = u < .42f
                                ? Vector3.Lerp(pickup, windup, Mathf.SmoothStep(0f, 1f, u / .42f))
                                : Vector3.Lerp(windup, releasePos, Mathf.SmoothStep(0f, 1f, (u - .42f) / .58f));
                            yield return null;
                        }
                        releasePos = right.gripAnchor.position;
                    }
                }
                finally
                {
                    carrying = attached = false;
                    candy = null;
                }
            }
            else
            {
                releasePos = item.position;
            }

            // Phase 2: Candy flies from giver's release point in a parabolic spinning arc straight into target's mouth
            float flightDur = Mathf.Max(.12f, seconds * .52f);
            float arcHeight = Mathf.Clamp(dist * .19f, .15f, .32f);
            Vector3 spinAxis = Vector3.Cross(Vector3.up, throwDir);
            if (spinAxis.sqrMagnitude < 1e-4f) spinAxis = Vector3.right;
            for (float t = 0f; t < flightDur; t += Time.deltaTime)
            {
                if (item == null) yield break;
                float u = Mathf.Clamp01(t / flightDur);
                float ease = u * (2f - u);
                Vector3 goal = targetMouth();
                item.position = Vector3.Lerp(releasePos, goal, ease) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * arcHeight);
                item.rotation = Quaternion.AngleAxis(u * 540f, spinAxis) * startRot;
                yield return null;
            }
            if (item != null)
            {
                item.position = targetMouth();
                item.rotation = startRot;
            }
        }
        public void ResetState() { UndoCarryOffset();carrying = attached = false;candy = null;curl = returnWeight = 0; }
        void OnDisable() => ResetState();
    }
}
