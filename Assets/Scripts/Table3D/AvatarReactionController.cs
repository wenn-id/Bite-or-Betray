using System;
using BiteOrBetray.Art3D;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    // Gameplay -> avatar: the director only calls this API; further expression/audio/VFX systems subscribe to its events.
    [DisallowMultipleComponent]
    public sealed class AvatarReactionController : MonoBehaviour
    {
        public CharacterAnimator face;
        public event Action<CharacterReaction> ReactionPlayed;
        public event Action StatusChanged;

        public bool Paralyzed { get; private set; }
        public bool Defeated { get; private set; }
        /// <summary>0 = healthy, 1 = nearly depleted HP/poisoning (weak, dizzy, nauseous).</summary>
        public float Sickness { get; private set; }

        public void Play(CharacterReaction reaction, Transform target = null)
        {
            if ((int)reaction < (int)CharacterReaction.Eat || (int)reaction > (int)CharacterReaction.Nod) return;
            if (face != null) face.Play(reaction, target);
            ReactionPlayed?.Invoke(reaction);
        }

        public void SetStatus(bool paralyzed, bool defeated)
        {
            if (paralyzed == Paralyzed && defeated == Defeated) return;
            Paralyzed = paralyzed;
            Defeated = defeated;
            if (face != null) { face.paralyzed = paralyzed; face.defeated = defeated; }
            StatusChanged?.Invoke();
        }

        public void SetSickness(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(amount, Sickness)) return;
            Sickness = amount;
            if (face != null) face.sickness = amount;
            StatusChanged?.Invoke();
        }

        public void ResetState()
        {
            Paralyzed = Defeated = false;
            Sickness = 0;
            if (face != null) face.ResetState();
            StatusChanged?.Invoke();
        }
    }
}
