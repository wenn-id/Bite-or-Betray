using System.Collections.Generic;
using UnityEngine;

namespace BiteOrBetray.Table3D
{
    /// <summary>Keep table-depth cards clear of each other, heads, room text and the dispenser from the player camera.</summary>
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
        static readonly List<Rect> roomText = new List<Rect>(8);

        internal sealed class Obstacle
        {
            readonly Transform root;
            readonly Bounds bounds;
            readonly Renderer renderer;

            public Obstacle(Renderer renderer) { this.renderer = renderer; root = renderer.transform; }
            public Obstacle(Transform root, Bounds bounds, bool isHead = false)
            { this.root = root; this.bounds = bounds; IsHead = isHead; }

            public bool Visible => root != null && root.gameObject.activeInHierarchy
                && (renderer == null || (renderer.enabled && !renderer.forceRenderingOff));
            public bool IsHead { get; }
            public bool IsRoomText => renderer == null && !IsHead;
            public Rect Project(Camera camera)
            {
                var local = renderer != null ? renderer.localBounds : bounds;
                var world = new Bounds(root.TransformPoint(local.center), Vector3.zero);
                for (int i = 0; i < 8; i++) world.Encapsulate(root.TransformPoint(Corner(local, i)));
                if (IsHead) world.Expand(.02f);
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
                return found ? new Obstacle(head, bounds, true) : null;
            }
        }

        internal sealed class Label
        {
            public readonly TextMesh text;
            public readonly MeshRenderer renderer;
            // Object.name allocates on every read; captions are named once by MakeText.
            public readonly float maxWidth;
            public MeshRenderer card;
            public Obstacle head;
            public Vector3 anchor;
            readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            float lastAspect = -1;
            int lastState = -1;
            Bounds textBounds;
            bool textKnown, hasText;

            public Label(TextMesh text, Vector3 anchor)
            {
                this.text = text;
                this.anchor = anchor;
                renderer = text.GetComponent<MeshRenderer>();
                maxWidth = text.name == "Narration" ? .56f : text.name == "Final Results" ? .40f : .30f;
            }

            // TextMesh.text returns a new string on every read. Empty text has an empty mesh, so it is read again
            // only when the mesh bounds change or the label is shown again, and idle frames do not allocate.
            public bool HasText
            {
                get
                {
                    var bounds = renderer.localBounds;
                    if (textKnown && Same(bounds.center, textBounds.center) && Same(bounds.extents, textBounds.extents)) return hasText;
                    textBounds = bounds;
                    textKnown = true;
                    return hasText = !string.IsNullOrEmpty(text.text);
                }
            }

            public void ForgetText() => textKnown = false;

            // Exact and NaN-safe (float.Equals): an empty mesh may report a non-finite AABB, which never equals
            // itself under ==, so an empty label would read its text again on every pass.
            static bool Same(Vector3 a, Vector3 b) => a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z);

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

        public static void Arrange(Camera camera, List<Label> labels, List<Obstacle> obstacles, TextMesh[] column = null)
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
                bool active = text.gameObject.activeInHierarchy;
                if (!active) entry.ForgetText();
                bool visible = active && entry.HasText;
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
                // TryGetComponent: GetComponent of a missing component allocates in the Editor.
                text.TryGetComponent(out TableSelectable selectable);
                entry.Style(size.x / Mathf.Max(.001f, size.y), selectable);
            }
            // Seat labels keep their owner's horizontal position; the free-space solver must never swap seats.
            foreach (var entry in labels)
            {
                if (!Visible(camera, entry) || entry.head == null) continue;
                var head = entry.head.Project(camera);
                // A head entirely behind the camera has no screen position to place the label over.
                if (!Finite(head)) continue;
                float width = .22f;
                foreach (var other in labels)
                {
                    if (other == entry || other.head == null || !Visible(camera, other)) continue;
                    var otherHead = other.head.Project(camera);
                    if (Finite(otherHead)) width = Mathf.Min(width, Mathf.Abs(otherHead.center.x - head.center.x) - Gap * 2);
                }
                Fit(camera, entry, Mathf.Max(.04f, width), .20f);
                var rect = Project(camera, entry);
                float bottom = head.yMax + Gap;
                foreach (var obstacle in obstacles)
                {
                    if (!obstacle.Visible || obstacle.IsRoomText) continue;
                    var other = obstacle.Project(camera);
                    if (other.xMin < head.center.x + rect.width / 2 && other.xMax > head.center.x - rect.width / 2)
                        bottom = Mathf.Max(bottom, other.yMax + Gap);
                }
                // Fit below overhead room text; if the head already reaches it, use the next clear band above it.
                // Each sign/tag can raise the label once and is then dropped, so the search always ends.
                roomText.Clear();
                foreach (var obstacle in obstacles)
                {
                    if (!obstacle.Visible || !obstacle.IsRoomText) continue;
                    var other = obstacle.Project(camera);
                    if (Finite(other) && other.xMin < head.center.x + rect.width / 2 && other.xMax > head.center.x - rect.width / 2)
                        roomText.Add(other);
                }
                float top;
                bool raised;
                do
                {
                    top = .98f;
                    raised = false;
                    for (int k = roomText.Count - 1; k >= 0; k--)
                    {
                        var other = roomText[k];
                        if (other.yMax + Gap <= bottom) continue;
                        if (other.yMin - Gap > bottom + .01f) top = Mathf.Min(top, other.yMin - Gap);
                        else { bottom = other.yMax + Gap; roomText.RemoveAt(k); raised = true; }
                    }
                } while (raised);
                Fit(camera, entry, Mathf.Max(.04f, width), Mathf.Max(.001f, top - bottom));
                rect = Project(camera, entry);
                Move(camera, entry, new Vector2(head.center.x, bottom + rect.height / 2));
                occupied.Add(Project(camera, entry));
            }
            if (column != null) ArrangeColumn(camera, labels, obstacles, column);
            foreach (var entry in labels)
            {
                if (!Visible(camera, entry) || entry.head != null || InColumn(entry, column)) continue;
                var text = entry.text;
                var t = text.transform;
                var rect = Project(camera, entry);
                if (!rect.Overlaps(new Rect(0, 0, 1, 1))) continue;
                float fit = Mathf.Min(1, entry.maxWidth / Mathf.Max(.001f, rect.width), .34f / Mathf.Max(.001f, rect.height));
                t.localScale *= fit;
                rect = Project(camera, entry);
                Vector2 center;
                for (int attempt = 0; !FindSpace(rect, out center) && attempt < 8; attempt++)
                {
                    t.localScale *= .9f;
                    rect = Project(camera, entry);
                }
                Move(camera, entry, center);
                occupied.Add(Project(camera, entry));
            }
        }

        static bool Visible(Camera camera, Label entry) => entry.text != null && entry.text.gameObject.activeInHierarchy
            && entry.HasText && camera.WorldToViewportPoint(entry.text.transform.position).z > camera.nearClipPlane;

        // Project() of bounds with every corner behind the camera is MinMaxRect(+inf, +inf, -inf, -inf): NaN max/center.
        internal static bool Finite(Rect rect) => float.IsFinite(rect.xMin) && float.IsFinite(rect.yMin)
            && float.IsFinite(rect.xMax) && float.IsFinite(rect.yMax);

        static bool InColumn(Label entry, TextMesh[] column)
        {
            if (column == null) return false;
            foreach (var text in column) if (entry.text == text) return true;
            return false;
        }

        static void Fit(Camera camera, Label entry, float width, float height)
        {
            var rect = Project(camera, entry);
            entry.text.transform.localScale *= Mathf.Min(1, width / Mathf.Max(.001f, rect.width), height / Mathf.Max(.001f, rect.height));
        }

        static void Move(Camera camera, Label entry, Vector2 center)
        {
            var t = entry.text.transform;
            var rect = Project(camera, entry);
            float depth = camera.WorldToViewportPoint(t.position).z;
            t.position += camera.ViewportToWorldPoint(new Vector3(center.x, center.y, depth))
                - camera.ViewportToWorldPoint(new Vector3(rect.center.x, rect.center.y, depth));
        }

        // The menu is one foreground column, including the hint. Only the complete group is fitted to the view.
        static void ArrangeColumn(Camera camera, List<Label> labels, List<Obstacle> obstacles, TextMesh[] column)
        {
            const float width = .32f, bottom = .07f;
            float top = .76f, height = 0;
            foreach (var obstacle in obstacles)
            {
                if (!obstacle.Visible || !obstacle.IsHead) continue;
                var rect = obstacle.Project(camera);
                if (rect.xMin < .5f + width / 2 && rect.xMax > .5f - width / 2) top = Mathf.Min(top, rect.yMin - Gap);
            }
            foreach (var text in column)
                foreach (var entry in labels)
                {
                    if (entry.text != text || !Visible(camera, entry)) continue;
                    Fit(camera, entry, width, .18f);
                    height += Project(camera, entry).height + Gap;
                }
            float fit = Mathf.Min(1, Mathf.Max(.05f, top - bottom - Gap * column.Length) / Mathf.Max(.001f, height - Gap * column.Length));
            float y = top;
            foreach (var text in column)
                foreach (var entry in labels)
                {
                    if (entry.text != text || !Visible(camera, entry)) continue;
                    entry.text.transform.localScale *= fit;
                    var rect = Project(camera, entry);
                    // All rows share a background width even when their captions have different lengths.
                    if (entry.card != null)
                    {
                        var scale = entry.card.transform.localScale;
                        scale.x *= width / Mathf.Max(.001f, rect.width);
                        entry.card.transform.localScale = scale;
                        text.TryGetComponent(out TableSelectable selectable);
                        entry.Style(scale.x / scale.y, selectable);
                        if (text.TryGetComponent(out BoxCollider box))
                            box.size = new Vector3(scale.x, scale.y, .02f);
                        rect = Project(camera, entry);
                    }
                    Move(camera, entry, new Vector2(.5f, y - rect.height / 2));
                    y -= rect.height + Gap;
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
