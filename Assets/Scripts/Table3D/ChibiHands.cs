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
                attached = true;
                for (float t = 0; t < seconds * .6f; t += Time.deltaTime)
                { if (!CanCarry) yield break;carryPosition = Vector3.Lerp(pickup, mouth(), Mathf.SmoothStep(0, 1, t / (seconds * .6f)));yield return null; }
                if (item != null) item.position = carryPosition = mouth();
            }
            finally { carrying = attached = false;candy = null; }
        }
        public void ResetState() { UndoCarryOffset();carrying = attached = false;candy = null;curl = returnWeight = 0; }
        void OnDisable() => ResetState();
    }
}
