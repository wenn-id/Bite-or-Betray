using UnityEngine;

namespace BiteOrBetray.Table3D
{
    [DefaultExecutionOrder(400), DisallowMultipleComponent]
    public sealed class RemoteAvatarRepresentation : MonoBehaviour
    {
        public SeatedAvatar avatar;
        [Range(.02f, .2f)] public float interpolationSeconds = .06f;
        public float timeoutSeconds = .8f;
        AvatarPose previous, target;
        float receivedAt;
        bool received;

        // Dipanggil di main thread setelah transport memvalidasi pengirim dan kursinya.
        public bool ReceivePose(in AvatarPose value)
        {
            if (!value.IsValid || (received && unchecked((int)(value.sequence - target.sequence)) <= 0)) return false;
            previous = received ? AvatarPose.Lerp(previous, target, Mathf.Clamp01((Time.unscaledTime - receivedAt) / Mathf.Max(.001f, interpolationSeconds))) : value;
            target = value;
            receivedAt = Time.unscaledTime;
            received = true;
            return true;
        }

        void LateUpdate() => ApplyReceivedPose(Time.unscaledTime);
        public void ApplyReceivedPose(float now)
        {
            if (!received) return;
            var pose = AvatarPose.Lerp(previous, target, Mathf.Clamp01((now - receivedAt) / Mathf.Max(.001f, interpolationSeconds)));
            if (now - receivedAt > timeoutSeconds) pose.headTracked = pose.leftTracked = pose.rightTracked = false;
            avatar.SetPose(pose);
        }

        public void ResetStream() => received = false;
        void OnDisable() => ResetStream();
    }
}
