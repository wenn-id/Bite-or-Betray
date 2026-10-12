using System;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using UnityEngine;

namespace BiteOrBetray.VisualPrototype
{
    [DefaultExecutionOrder(600), DisallowMultipleComponent]
    public sealed class CartoonAvatarFace : MonoBehaviour
    {
        public AvatarReactionController reactions;
        public SkinnedMeshRenderer mouth, leftLid, rightLid;
        AvatarReactionController subscribed;
        CharacterReaction reaction;
        float reactionTime, duration, blinkClock;
        bool reacting;
        bool ready;
        int openIndex, grinIndex, frownIndex;
        readonly int[] lidIndices = new int[6];

        void OnEnable() => Bind();
        void OnDisable()
        {
            if (subscribed == null) return;
            subscribed.ReactionPlayed -= Play;
            subscribed.StatusChanged -= StatusChanged;
            subscribed = null;
        }

        void Bind()
        {
            if (subscribed != null || reactions == null) return;
            subscribed = reactions;
            subscribed.ReactionPlayed += Play;
            subscribed.StatusChanged += StatusChanged;
        }

        void Play(CharacterReaction value)
        {
            reaction = value;
            reactionTime = 0;
            duration = value == CharacterReaction.Cheer ? 2.2f : value == CharacterReaction.Eat ? 1.9f : 1.6f;
            reacting = true;
        }

        void StatusChanged()
        {
            if (!reactions.Paralyzed && !reactions.Defeated && reactions.Sickness == 0) reacting = false;
        }

        void LateUpdate() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            Bind();
            if (mouth == null || leftLid == null || rightLid == null || reactions == null) return;
            if (!ready)
            {
                openIndex = Index(mouth,"Open"); grinIndex = Index(mouth,"Grin"); frownIndex = Index(mouth,"Frown");
                lidIndices[0]=Index(leftLid,"Squint"); lidIndices[1]=Index(leftLid,"Wide"); lidIndices[2]=Index(leftLid,"Blink");
                lidIndices[3]=Index(rightLid,"Squint"); lidIndices[4]=Index(rightLid,"Wide"); lidIndices[5]=Index(rightLid,"Blink");
                ready = true;
            }
            dt = Mathf.Clamp(dt, 0, .1f);
            float open = 0, grin = 0, frown = 0, wide = 0, squint = 0;
            if (reacting)
            {
                reactionTime += dt;
                float strength = Mathf.Min(Mathf.Clamp01(reactionTime/.12f), Mathf.Clamp01((duration-reactionTime)/.25f));
                switch (reaction)
                {
                    case CharacterReaction.Eat: open = (.45f+.25f*Mathf.Sin(reactionTime*14))*strength; break;
                    case CharacterReaction.Yuck:
                    case CharacterReaction.Hurt: frown = strength; squint = .75f*strength; break;
                    case CharacterReaction.Shocked: open = .85f*strength; wide = strength; break;
                    case CharacterReaction.Yummy:
                    case CharacterReaction.Cheer:
                    case CharacterReaction.Laugh: open = .3f*strength; grin = .8f*strength; wide = .8f*strength; break;
                    default: grin = .45f*strength; wide = .25f*strength; break;
                }
                if (reactionTime >= duration) reacting = false;
            }
            frown = Mathf.Max(frown, Mathf.Max(reactions.Sickness*.7f, reactions.Defeated ? 1 : 0));
            squint = Mathf.Max(squint, Mathf.Max(reactions.Sickness*.55f, reactions.Defeated ? .85f : 0));
            if (reactions.Paralyzed) { wide = 1; squint = 0; }
            blinkClock = (blinkClock+dt)%4.3f;
            float blink = blinkClock > 4.08f ? Mathf.Sin((blinkClock-4.08f)/.22f*Mathf.PI) : 0;
            mouth.SetBlendShapeWeight(openIndex,open*100); mouth.SetBlendShapeWeight(grinIndex,grin*100); mouth.SetBlendShapeWeight(frownIndex,frown*100);
            SetLid(leftLid,0,squint,wide,blink); SetLid(rightLid,3,squint,wide,blink);
        }

        void SetLid(SkinnedMeshRenderer lid, int offset, float squint, float wide, float blink)
        {
            lid.SetBlendShapeWeight(lidIndices[offset],squint*(1-blink)*100);
            lid.SetBlendShapeWeight(lidIndices[offset+1],wide*(1-blink)*100);
            lid.SetBlendShapeWeight(lidIndices[offset+2],blink*100);
        }

        static int Index(SkinnedMeshRenderer renderer, string name)
        {
            for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                if (renderer.sharedMesh.GetBlendShapeName(i).EndsWith("."+name) || renderer.sharedMesh.GetBlendShapeName(i)==name) return i;
            throw new InvalidOperationException("Missing facial shape: "+renderer.name+"/"+name);
        }
    }
}
