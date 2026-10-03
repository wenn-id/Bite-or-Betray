using UnityEngine;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Material bersama untuk TextMesh di dunia 3D. Memakai shader BoBWorldText (Resources) yang aman untuk VR;
    /// font.material bawaan (GUI/Text Shader) menggambar teks bergeser di mode single-pass instanced.
    /// </summary>
    static class WorldText
    {
        static Material material;
        static Font source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Font.textureRebuilt -= OnTextureRebuilt;
            material = null;
            source = null;
        }

        public static Material For(Font font)
        {
            if (material != null && source == font) return material;
            var shader = Resources.Load<Shader>("BoBWorldText");
            material = shader != null ? new Material(shader) { name = "BoB World Text" } : font.material;
            material.mainTexture = font.material.mainTexture;
            source = font;
            Font.textureRebuilt -= OnTextureRebuilt;
            Font.textureRebuilt += OnTextureRebuilt;
            return material;
        }

        // Atlas font dinamis dibuat ulang saat ada karakter baru; ikuti teksturnya.
        static void OnTextureRebuilt(Font font)
        {
            if (font == source && material != null) material.mainTexture = font.material.mainTexture;
        }

        public static TextMesh Create(string name, Transform parent, Font font, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 48;
            text.characterSize = size;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.richText = true;
            text.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = For(font);
            return text;
        }
    }
}
