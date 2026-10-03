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

        public void SetHover(bool value)
        {
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
