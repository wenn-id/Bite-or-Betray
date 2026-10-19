using System;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Objek di meja yang bisa dipilih pemain (permen, bot, item). Pointer desktop (mouse) memanggil SetHover/Select;
    /// interaksi VR nanti cukup memanggil method yang sama.
    /// </summary>
    public sealed class TableSelectable : MonoBehaviour
    {
        public enum Kind { Candy, Bot, Item, Button }

        public Kind kind;
        public string tooltip;
        public bool interactable;
        public Action onSelect;
        [Tooltip("Objek yang membesar saat disorot. Kosong = objek ini sendiri.")]
        public Transform highlight;

        Vector3 restScale;
        bool hovered;
        public bool IsHovered => hovered;
        static RaycastHit[] rayHits = new RaycastHit[64];

        /// <summary>Pilih target interaktif terdekat; collider dekorasi dan target nonaktif tidak menghalangi.</summary>
        public static TableSelectable Raycast(Ray ray, float maxDistance, out RaycastHit hit,
            TableSelectable ignored = null, bool botsOnly = false)
        {
            int count;
            // Hasil NonAlloc tidak berurutan. Kalau penuh, ulangi dengan buffer lebih besar agar target terdekat
            // tidak terlewat; kapasitas disimpan untuk ray berikutnya, tanpa alokasi selama kapasitas cukup.
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
            // Warna/bingkai tombol berubah lewat kartu; pembesaran membuat label tetangganya tertutup.
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
