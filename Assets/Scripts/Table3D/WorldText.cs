using System.Text;
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

        // Pertahankan kata dan baris eksplisit; narasi panjang tidak lagi menjadi banner selebar ruangan.
        public static string Wrap(string value, int columns)
        {
            var result = new StringBuilder();
            foreach (var line in value.Split('\n'))
            {
                if (result.Length > 0) result.Append('\n');
                int length = 0;
                foreach (var word in line.Split(' '))
                {
                    if (length > 0 && length + word.Length + 1 > columns) { result.Append('\n'); length = 0; }
                    if (length > 0) { result.Append(' '); length++; }
                    result.Append(word);
                    length += word.Length;
                }
            }
            return result.ToString();
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
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = For(font);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return text;
        }
    }
}
