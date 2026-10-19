using System;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Selectable table object (candy, bot, item). Desktop pointer (mouse) calls SetHover/Select;
    /// future VR interaction only needs to call the same methods.
    /// </summary>
    public sealed class TableSelectable : MonoBehaviour
    {
        public enum Kind { Candy, Bot, Item, Button }

        public Kind kind;
        public string tooltip;
        public bool interactable;
        public Action onSelect;
        [Tooltip("Object that grows on hover. Empty = this object itself.")]
        public Transform highlight;

        Vector3 restScale;
        bool hovered;
        public bool IsHovered => hovered;
        static RaycastHit[] rayHits = new RaycastHit[64];

        /// <summary>Select the nearest interactive target; decorative colliders and inactive targets do not block it.</summary>
        public static TableSelectable Raycast(Ray ray, float maxDistance, out RaycastHit hit,
            TableSelectable ignored = null, bool botsOnly = false)
        {
            int count;
            // NonAlloc results are unordered. If full, retry with a larger buffer to avoid missing the nearest
            // target; retain capacity for the next ray, with no allocations while capacity is sufficient.
            while ((count = Physics.RaycastNonAlloc(ray, rayHits, maxDistance, ~0, QueryTriggerInteraction.Collide)) == rayHits.Length)
                Array.Resize(ref rayHits, rayHits.Length * 2);

            TableSelectable nearest = null;
            hit = default;
            for (int i = 0; i < count; i++)
            {
                var candidateHit = rayHits[i];
                var candidate = candidateHit.collider.GetComponentInParent<TableSelectable>();
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.interactable || candidate == ignored) continue;
                if (botsOnly && candidate.kind != Kind.Bot) continue;
                if (nearest != null && candidateHit.distance >= hit.distance) continue;
                nearest = candidate;
                hit = candidateHit;
            }
            return nearest;
        }

        public void SetHover(bool value)
        {
            // Button colors/frames change through the card; enlarging would obscure neighboring labels.
            if (kind == Kind.Button) { hovered = value; return; }
            var target = highlight != null ? highlight : transform;
            if (value == hovered || target == null) return;
            if (value) restScale = target.localScale;
            target.localScale = value ? restScale * 1.15f : restScale;
            hovered = value;
        }

        public void Select()
        {
            if (interactable) onSelect?.Invoke();
        }

        void OnDisable() => SetHover(false);
    }
}
