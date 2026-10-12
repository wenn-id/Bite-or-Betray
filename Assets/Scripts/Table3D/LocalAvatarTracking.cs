using UnityEngine;
using UnityEngine.XR;

namespace BiteOrBetray.Table3D
{
    [DefaultExecutionOrder(400), DisallowMultipleComponent]
    public sealed class LocalAvatarTracking : MonoBehaviour
    {
        public SeatedAvatar avatar;
        public VRTableInput tableInput;
        public Transform headTarget;
        public Camera firstPersonCamera;
        [Tooltip("Wrist offset in the existing controller anchor's coordinates.")]
        public Vector3 controllerWristOffset = new Vector3(0, -.025f, -.02f);
        public Renderer[] controllerRenderers;
        bool[] originalVisibility;
        int originalCameraMask;
        bool xrWasActive;
        uint sequence;

        void Start()
        {
            avatar.Initialize();
            originalCameraMask = firstPersonCamera != null ? firstPersonCamera.cullingMask : -1;
            originalVisibility = new bool[controllerRenderers.Length];
            for (int i = 0; i < controllerRenderers.Length; i++) originalVisibility[i] = controllerRenderers[i].forceRenderingOff;
            SetDesktopHeadHidden(true);
        }

        // Mode desktop: kamera diletakkan di posisi kepala avatar, jadi kepala lokal disembunyikan total.
        // Mode XR memakai layer kepala lokal supaya hanya kamera first-person yang tidak melihatnya.
        void SetDesktopHeadHidden(bool hidden)
        {
            for (int i = 0; i < avatar.headRenderers.Length; i++)
                if (avatar.headRenderers[i] != null) avatar.headRenderers[i].forceRenderingOff = hidden;
        }

        void LateUpdate()
        {
            bool active = tableInput != null && tableInput.IsXRActive;
            if (active != xrWasActive)
            {
                avatar.SetLocalHeadHidden(firstPersonCamera, active);
                SetDesktopHeadHidden(!active);
                for (int i = 0; i < controllerRenderers.Length; i++) controllerRenderers[i].forceRenderingOff = active || originalVisibility[i];
                xrWasActive = active;
            }
            if (!active || headTarget == null) return;
            var pose = avatar.CurrentPose;
            pose.head = avatar.Relative(headTarget);
            pose.headTracked = headTarget.gameObject.activeInHierarchy;
            pose.leftTracked = ReadHand(XRNode.LeftHand, ref pose.leftHand, out pose.leftGrip, out pose.leftIndex);
            pose.rightTracked = ReadHand(XRNode.RightHand, ref pose.rightHand, out pose.rightGrip, out pose.rightIndex);
            pose.sequence = ++sequence;
            avatar.SetPose(pose);
        }

        bool ReadHand(XRNode node, ref Pose pose, out float grip, out float index)
        {
            if (!tableInput.TryGetTrackedHand(node, out var anchor, out grip, out index)) return false;
            pose = avatar.Relative(anchor);
            pose.position += avatar.seat.InverseTransformVector(anchor.TransformVector(controllerWristOffset));
            return true;
        }

        void OnDisable()
        {
            if (originalVisibility == null) return;
            avatar.SetLocalHeadHidden(firstPersonCamera, false);
            SetDesktopHeadHidden(false);
            if (firstPersonCamera != null) firstPersonCamera.cullingMask = originalCameraMask;
            for (int i = 0; i < controllerRenderers.Length; i++)
                if (controllerRenderers[i] != null) controllerRenderers[i].forceRenderingOff = originalVisibility[i];
            xrWasActive = false;
        }
    }
}
