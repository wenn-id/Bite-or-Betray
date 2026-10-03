using System;
using BiteOrBetray.Art3D;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    [DisallowMultipleComponent]
    public sealed class AvatarReactionController : MonoBehaviour
    {
        public CharacterAnimator face;
        public event Action<CharacterReaction> ReactionPlayed;
        public void Play(CharacterReaction reaction, Transform target = null)
        {
            if ((int)reaction < (int)CharacterReaction.Eat || (int)reaction > (int)CharacterReaction.Nod) return;
            if (face != null) face.Play(reaction, target);
            ReactionPlayed?.Invoke(reaction);
        }
        public void SetStatus(bool paralyzed, bool defeated)
        {
            if (face == null) return;
            face.paralyzed = paralyzed;
            face.defeated = defeated;
        }
        public void ResetState() { if (face != null) face.ResetState(); }
    }
}
