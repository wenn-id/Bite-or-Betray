using System;
using UnityEngine;

namespace BiteOrBetray.VisualPrototype
{
    [DefaultExecutionOrder(550), DisallowMultipleComponent]
    public sealed class CartoonAvatarBody : MonoBehaviour
    {
        [Serializable]
        public sealed class Arm
        {
            public Transform shoulder, elbow, wrist;
            internal Vector3 elbowLocal, wristLocal, upperAxis, lowerAxis, pole;
            internal Quaternion upperRest, lowerRest;
            internal float upperLength, lowerLength;
        }

        public Arm left=new Arm(), right=new Arm();
        public float MaxReachCorrection { get; private set; }
        bool ready;

        public void Initialize()
        {
            if (ready) return;
            Cache(left); Cache(right);
            ready=true;
        }

        void Cache(Arm arm)
        {
            if (arm.shoulder==null || arm.elbow==null || arm.wrist==null) throw new InvalidOperationException("Body arm rig is incomplete.");
            var upper=arm.elbow.position-arm.shoulder.position;
            var lower=arm.wrist.position-arm.elbow.position;
            arm.upperLength=upper.magnitude;arm.lowerLength=lower.magnitude;
            arm.upperAxis=arm.shoulder.InverseTransformDirection(upper).normalized;
            arm.lowerAxis=arm.elbow.InverseTransformDirection(lower).normalized;
            arm.elbowLocal=arm.elbow.localPosition;arm.wristLocal=arm.wrist.localPosition;
            arm.upperRest=arm.shoulder.localRotation;arm.lowerRest=arm.elbow.localRotation;
            arm.pole=transform.InverseTransformDirection(upper);
        }

        void LateUpdate() => ApplyArms();

        public void ApplyArms()
        {
            Initialize();
            MaxReachCorrection=Mathf.Max(Solve(left),Solve(right));
        }

        float Solve(Arm arm)
        {
            var target=arm.wrist.position;
            var rotation=arm.wrist.rotation;
            arm.elbow.localPosition=arm.elbowLocal;
            arm.shoulder.localRotation=arm.upperRest;
            arm.elbow.localRotation=arm.lowerRest;
            var start=arm.shoulder.position;
            var delta=target-start;
            var distance=delta.magnitude;
            var direction=distance>.00001f ? delta/distance : transform.forward;
            float reach=Mathf.Clamp(distance,Mathf.Abs(arm.upperLength-arm.lowerLength)+.0001f,arm.upperLength+arm.lowerLength-.0001f);
            var end=start+direction*reach;
            var pole=Vector3.ProjectOnPlane(transform.TransformDirection(arm.pole),direction);
            if (pole.sqrMagnitude<.000001f) pole=Vector3.ProjectOnPlane(-transform.up,direction);
            if (pole.sqrMagnitude<.000001f) pole=Vector3.ProjectOnPlane(transform.right,direction);
            pole.Normalize();
            float along=(arm.upperLength*arm.upperLength-arm.lowerLength*arm.lowerLength+reach*reach)/(2*reach);
            float height=Mathf.Sqrt(Mathf.Max(0,arm.upperLength*arm.upperLength-along*along));
            var elbow=start+direction*along+pole*height;
            arm.shoulder.rotation=Quaternion.FromToRotation(arm.shoulder.TransformDirection(arm.upperAxis),elbow-start)*arm.shoulder.rotation;
            arm.elbow.rotation=Quaternion.FromToRotation(arm.elbow.TransformDirection(arm.lowerAxis),end-arm.elbow.position)*arm.elbow.rotation;
            arm.wrist.localPosition=arm.wristLocal;
            arm.wrist.rotation=rotation;
            return Vector3.Distance(target,arm.wrist.position);
        }
    }
}
