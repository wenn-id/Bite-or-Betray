using System.Collections.Generic;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    /// <summary>Keep table-depth cards clear of each other, heads and the dispenser from the player camera.</summary>
    static class WorldTextLayout
    {
        public static readonly Color Cream = new Color(1, .95f, .81f);
        static readonly Color Ink = new Color(.12f, .22f, .17f);
        static readonly Color Muted = new Color(.30f, .34f, .29f);
        static readonly int StateId = Shader.PropertyToID("_State");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");
        const float Gap = .008f;
        // Leave the top of the view clear for the room sign.
        static readonly Rect Area = Rect.MinMaxRect(.025f, .035f, .975f, .82f);
        static readonly List<Rect> occupied = new List<Rect>(64);

        internal sealed class Obstacle
        {
            readonly Transform root;
            readonly Bounds bounds;
            readonly Renderer renderer;

            public Obstacle(Renderer renderer) { this.renderer = renderer; root = renderer.transform; }
            Obstacle(Transform root, Bounds bounds) { this.root = root; this.bounds = bounds; }

            public bool Visible => root != null && root.gameObject.activeInHierarchy
                && (renderer == null || (renderer.enabled && !renderer.forceRenderingOff));
            public Rect Project(Camera camera)
            {
                var local = renderer != null ? renderer.localBounds : bounds;
                var world = new Bounds(root.TransformPoint(local.center), Vector3.zero);
                for (int i = 0; i < 8; i++) world.Encapsulate(root.TransformPoint(Corner(local, i)));
                if (renderer == null) world.Expand(.02f);
                return WorldTextLayout.Project(camera, null, world);
            }

            // Full-body skins have deliberately loose culling bounds. Measure the posed mesh once,
            // above the neck, then keep that volume attached to the animated head bone.
            public static Obstacle Head(Transform character)
            {
                Transform head = null;
                foreach (var part in character.GetComponentsInChildren<Transform>(true))
                    if (part.name == "Head" || part.name.StartsWith("Head.")) { head = part; break; }
                if (head == null) return null;
                var bounds = new Bounds();
                bool found = false;
                var mesh = new Mesh();
                try
                {
                    foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
                    {
                        Vector3[] vertices;
                        if (renderer is SkinnedMeshRenderer skin) { skin.BakeMesh(mesh); vertices = mesh.vertices; }
                        else
                        {
                            var local = renderer.localBounds;
                            vertices = new Vector3[8];
                            for (int i = 0; i < 8; i++) vertices[i] = Corner(local, i);
                        }
                        foreach (var vertex in vertices)
                        {
                            var world = renderer.transform.TransformPoint(vertex);
                            if (world.y < head.position.y - .02f) continue;
                            var point = head.InverseTransformPoint(world);
                            if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                            else bounds.Encapsulate(point);
                        }
                    }
                }
                finally { Object.Destroy(mesh); }
                return found ? new Obstacle(head, bounds) : null;
            }
        }

        internal sealed class Label
        {
            public readonly TextMesh text;
            public readonly MeshRenderer renderer;
            public MeshRenderer card;
            public Vector3 anchor;
            readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            float lastAspect = -1;
            int lastState = -1;

            public Label(TextMesh text, Vector3 anchor)
            {
                this.text = text;
                this.anchor = anchor;
                renderer = text.GetComponent<MeshRenderer>();
            }

            public void Style(float aspect, TableSelectable selectable)
            {
                int state = selectable == null ? 0 : !selectable.interactable ? 3 : selectable.IsHovered ? 2 : 1;
                if (selectable != null) text.color = state == 1 ? Cream : state == 3 ? Muted : Ink;
                if (card == null || (state == lastState && Mathf.Abs(aspect - lastAspect) < .001f)) return;
                lastState = state;
                lastAspect = aspect;
                properties.SetFloat(StateId, state);
                properties.SetFloat(AspectId, aspect);
                card.SetPropertyBlock(properties);
            }
        }

        public static void Arrange(Camera camera, List<Label> labels, List<Obstacle> obstacles)
        {
            occupied.Clear();
            foreach (var obstacle in obstacles)
            {
                if (!obstacle.Visible) continue;
                var rect = obstacle.Project(camera);
                if (rect.Overlaps(new Rect(0, 0, 1, 1))) occupied.Add(rect);
            }
            for (int i = 0; i < labels.Count; i++)
            {
                var entry = labels[i];
                if (entry.text == null) { labels.RemoveAt(i--); continue; }
                var text = entry.text;
                var t = text.transform;
                t.position = entry.anchor;
                t.rotation = camera.transform.rotation;
                float distance = Vector3.Distance(t.position, camera.transform.position);
                t.localScale = Vector3.one * Mathf.Clamp(distance / 1.1f, 1, 1.4f);
                bool visible = text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(text.text);
                if (entry.card != null) entry.card.gameObject.SetActive(visible);
                if (!visible) continue;
                var bounds = entry.renderer.localBounds;
                var size = new Vector2(bounds.size.x + .04f, bounds.size.y + .025f);
                if (entry.card != null)
                {
                    entry.card.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, .003f);
                    entry.card.transform.localScale = new Vector3(size.x, size.y, 1);
                }
                if (text.TryGetComponent(out BoxCollider box))
                {
                    box.center = new Vector3(bounds.center.x, bounds.center.y, 0);
                    box.size = new Vector3(size.x, size.y, .02f);
                }
                entry.Style(size.x / Mathf.Max(.001f, size.y), text.GetComponent<TableSelectable>());
                if (camera.WorldToViewportPoint(t.position).z <= camera.nearClipPlane) continue;
                var rect = Project(camera, entry);
                if (!rect.Overlaps(new Rect(0, 0, 1, 1))) continue;
                float maxWidth = text.name == "Narasi" ? .56f : text.name == "Hasil Akhir" ? .40f : .30f;
                float fit = Mathf.Min(1, maxWidth / Mathf.Max(.001f, rect.width), .34f / Mathf.Max(.001f, rect.height));
                t.localScale *= fit;
                rect = Project(camera, entry);
                Vector2 center;
                for (int attempt = 0; !FindSpace(rect, out center) && attempt < 8; attempt++)
                {
                    t.localScale *= .9f;
                    rect = Project(camera, entry);
                }
                var depth = camera.WorldToViewportPoint(t.position).z;
                t.position += camera.ViewportToWorldPoint(new Vector3(center.x, center.y, depth))
                    - camera.ViewportToWorldPoint(new Vector3(rect.center.x, rect.center.y, depth));
                occupied.Add(Project(camera, entry));
            }
        }

        static bool FindSpace(Rect rect, out Vector2 best)
        {
            Vector2 desired = rect.center;
            best = desired;
            float score = float.PositiveInfinity;
            // Try the edges of reserved regions and placed cards; prefer the closest point to the anchor.
            for (int x = -1; x < occupied.Count * 2; x++)
                for (int y = -1; y < occupied.Count * 2; y++)
                {
                    float cx = x < 0 ? desired.x : x % 2 == 0 ? occupied[x / 2].xMin - rect.width / 2 - Gap
                        : occupied[x / 2].xMax + rect.width / 2 + Gap;
                    float cy = y < 0 ? desired.y : y % 2 == 0 ? occupied[y / 2].yMin - rect.height / 2 - Gap
                        : occupied[y / 2].yMax + rect.height / 2 + Gap;
                    var center = new Vector2(Mathf.Clamp(cx, Area.xMin + rect.width / 2, Area.xMax - rect.width / 2),
                        Mathf.Clamp(cy, Area.yMin + rect.height / 2, Area.yMax - rect.height / 2));
                    float cost = (center - desired).sqrMagnitude;
                    if (cost >= score) continue;
                    var candidate = new Rect(center - rect.size / 2, rect.size);
                    bool clear = true;
                    foreach (var other in occupied)
                        if (candidate.Overlaps(Rect.MinMaxRect(other.xMin - Gap / 2, other.yMin - Gap / 2,
                            other.xMax + Gap / 2, other.yMax + Gap / 2))) { clear = false; break; }
                    if (clear) { best = center; score = cost; }
                }
            return !float.IsPositiveInfinity(score);
        }

        static Rect Project(Camera camera, Label entry)
        {
            var renderer = entry.card != null ? entry.card : entry.renderer;
            return Project(camera, renderer.transform, renderer.localBounds);
        }

        static Vector3 Corner(Bounds bounds, int i) => bounds.center + Vector3.Scale(bounds.extents,
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));

        static Rect Project(Camera camera, Transform transform, Bounds bounds)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                var point = camera.WorldToViewportPoint(transform != null ? transform.TransformPoint(Corner(bounds, i)) : Corner(bounds, i));
                if (point.z <= camera.nearClipPlane) continue;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
