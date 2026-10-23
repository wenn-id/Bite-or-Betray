using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// BOB-CI (CI 669ee58): the BOB-060 room-text pass in WorldTextLayout.Arrange looped forever when a seat label's head
    /// was entirely behind the camera (its projection has NaN max/center), which hung the PlayMode and Trailer runs until
    /// the runner killed Unity. These cases call Arrange directly on a small rig, without the gameplay scene.
    /// BOB-CI (CI d79e91c): the director allocated ~5.7 KB per frame while waiting for input, mostly because Arrange read
    /// TextMesh.text and Object.name (new strings on every read) and GetComponent of a missing TableSelectable.
    /// </summary>
    public class WorldTextLayoutTests
    {
        static readonly Type Layout = typeof(CandyTableDirector).Assembly.GetType("BiteOrBetray.Table3D.WorldTextLayout", true);
        static readonly Type ObstacleType = Layout.GetNestedType("Obstacle", BindingFlags.NonPublic);
        static readonly Type LabelType = Layout.GetNestedType("Label", BindingFlags.NonPublic);
        static readonly Vector3 Eye = new Vector3(0, 1.2f, 0);
        readonly List<Object> created = new List<Object>();
        Camera camera;
        Transform room;

        [SetUp]
        public void CreateRig()
        {
            camera = Track(new GameObject("Layout Test Camera")).AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(Eye, Quaternion.identity);
            camera.fieldOfView = 60;
            camera.nearClipPlane = .05f;
            camera.aspect = 16f / 9;
            room = Track(new GameObject("Layout Test Room")).transform;
        }

        [TearDown]
        public void DestroyRig()
        {
            foreach (var o in created) if (o != null) Object.Destroy(o);
            created.Clear();
        }

        [UnityTest]
        public IEnumerator Seat_label_with_head_behind_camera_stays_at_its_anchor()
        {
            var head = Track(new GameObject("Head")).transform;
            head.position = Eye + Vector3.back;
            var anchor = Eye + new Vector3(0, .3f, 2);
            var text = Text("Label Mint", anchor);
            yield return null;
            var labels = Labels((text, anchor, Obstacle(head, new Bounds(Vector3.zero, new Vector3(.2f, .25f, .2f)), true)));
            // All room text behind the camera too: with the NaN head every pass used to raise the label again.
            var obstacles = Obstacles(Obstacle(room, new Bounds(new Vector3(0, 2.7f, -3), new Vector3(3, .7f, .02f))),
                Obstacle(room, new Bounds(new Vector3(2.5f, 1.5f, -3), new Vector3(1.3f, .1f, .01f))));

            Arrange(labels, obstacles);

            Assert.That(Vector3.Distance(text.transform.position, anchor), Is.LessThan(1e-4f),
                "A label whose head cannot be projected keeps its anchor: " + text.transform.position);
        }

        [UnityTest]
        public IEnumerator Seat_label_rises_over_room_text_into_the_clear_band_below_the_next_text()
        {
            var head = Track(new GameObject("Head")).transform;
            head.position = Eye + new Vector3(0, 0, 2);
            var anchor = head.position + Vector3.up * .33f;
            var text = Text("Label Mint", anchor);
            yield return null;
            var labels = Labels((text, anchor, Obstacle(head, new Bounds(Vector3.zero, new Vector3(.2f, .25f, .2f)), true)));
            // The sign starts just above the head top, so the label must clear it; the higher tag caps the band.
            var sign = new Bounds(Eye + new Vector3(0, .25f, 2.5f), new Vector3(1, .1f, .02f));
            var tag = new Bounds(Eye + new Vector3(0, .8f, 2.5f), new Vector3(1, .2f, .02f));
            var obstacles = Obstacles(Obstacle(room, tag), Obstacle(room, sign));

            Arrange(labels, obstacles);

            var label = Project(text.GetComponent<MeshRenderer>().bounds);
            var signRect = Project(sign);
            var tagRect = Project(tag);
            Assert.IsTrue(float.IsFinite(label.yMin) && float.IsFinite(label.yMax), "label rect " + label);
            Assert.Greater(label.yMin, signRect.yMax, $"Label {label} must be above the sign {signRect}.");
            Assert.LessOrEqual(label.yMax, tagRect.yMin + .002f, $"Label {label} must stay below the next room text {tagRect}.");
            Assert.That(label.center.x, Is.EqualTo(.5f).Within(.01f), "The label stays over its own head.");
        }

        [UnityTest]
        public IEnumerator Card_follows_text_emptiness_including_text_changed_while_hidden()
        {
            var text = Text("Narration", Eye + new Vector3(0, 0, 2));
            var card = Card(text);
            yield return null;
            var labels = Labels((text, text.transform.position, null));
            var obstacles = Obstacles();

            Arrange(labels, obstacles);
            Assert.IsTrue(card.activeSelf, "A label with text shows its card.");
            text.text = "";
            Arrange(labels, obstacles);
            Assert.IsFalse(card.activeSelf, "Emptied text hides the card in the same pass.");
            text.text = "Mint wins";
            Arrange(labels, obstacles);
            Assert.IsTrue(card.activeSelf, "New text shows the card again.");
            text.text = "Mint loses";
            Arrange(labels, obstacles);
            Assert.IsTrue(card.activeSelf, "Changed text keeps the card.");

            text.gameObject.SetActive(false);
            Arrange(labels, obstacles);
            text.text = "";
            text.gameObject.SetActive(true);
            Arrange(labels, obstacles);
            Assert.IsFalse(card.activeSelf, "Text emptied while the label was hidden is read again when it is shown.");
        }

        [UnityTest]
        public IEnumerator Arrange_does_not_allocate_while_texts_are_unchanged()
        {
            var head = Track(new GameObject("Head")).transform;
            head.position = Eye + new Vector3(0, 0, 2);
            var anchor = head.position + Vector3.up * .33f;
            var seat = Text("Label Mint", anchor);
            Card(seat);
            var caption = Text("Narration", Eye + new Vector3(.4f, -.3f, 2));
            Card(caption);
            var hidden = Text("Popup", Eye + new Vector3(-.4f, -.3f, 2));
            hidden.text = "";
            yield return null;
            var labels = Labels((seat, anchor, Obstacle(head, new Bounds(Vector3.zero, new Vector3(.2f, .25f, .2f)), true)),
                (caption, caption.transform.position, null), (hidden, hidden.transform.position, null));
            var obstacles = Obstacles(Obstacle(room, new Bounds(Eye + new Vector3(0, .8f, 2.5f), new Vector3(1, .2f, .02f))));
            var arrange = (Action)typeof(WorldTextLayoutTests).GetMethod(nameof(Bind), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(labels.GetType(), obstacles.GetType()).Invoke(null, new object[] { camera, labels, obstacles });
            arrange(); // the first pass reads each text once

            Assert.That(() => { var control = new object[8]; }, Is.AllocatingGCMemory(), "GC.Alloc recorder must see a known allocation.");
            Assert.That(() => { for (int i = 0; i < 10; i++) arrange(); }, Is.Not.AllocatingGCMemory(),
                "WorldTextLayout.Arrange runs every LateUpdate and must not allocate while the texts stay the same.");
            Assert.IsTrue(seat.transform.Find("Card").gameObject.activeSelf, "The seat card stays visible.");
        }

        // Typed delegate so the measured calls carry no reflection allocations.
        static Action Bind<TLabels, TObstacles>(Camera camera, TLabels labels, TObstacles obstacles)
        {
            var method = Layout.GetMethod("Arrange", BindingFlags.Public | BindingFlags.Static);
            var arrange = (Action<Camera, TLabels, TObstacles, TextMesh[]>)Delegate.CreateDelegate(
                typeof(Action<Camera, TLabels, TObstacles, TextMesh[]>), method);
            return () => arrange(camera, labels, obstacles, null);
        }

        GameObject Card(TextMesh text)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(quad.GetComponent<Collider>());
            quad.name = "Card";
            quad.transform.SetParent(text.transform, false);
            return quad;
        }

        T Track<T>(T o) where T : Object { created.Add(o); return o; }

        TextMesh Text(string name, Vector3 position)
        {
            var go = Track(new GameObject(name));
            go.transform.position = position;
            var text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.fontSize = 48;
            text.characterSize = .0065f;
            text.anchor = TextAnchor.MiddleCenter;
            text.text = "Mint";
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return text;
        }

        static object Obstacle(Transform root, Bounds bounds, bool isHead = false) =>
            Activator.CreateInstance(ObstacleType, root, bounds, isHead);

        static IList Labels(params (TextMesh text, Vector3 anchor, object head)[] entries)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(LabelType));
            foreach (var (text, anchor, head) in entries)
            {
                var label = Activator.CreateInstance(LabelType, text, anchor);
                LabelType.GetField("head").SetValue(label, head);
                var card = text.transform.Find("Card");
                if (card != null) LabelType.GetField("card").SetValue(label, card.GetComponent<MeshRenderer>());
                list.Add(label);
            }
            return list;
        }

        static IList Obstacles(params object[] obstacles)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ObstacleType));
            foreach (var obstacle in obstacles) list.Add(obstacle);
            return list;
        }

        void Arrange(IList labels, IList obstacles) =>
            Layout.GetMethod("Arrange", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { camera, labels, obstacles, null });

        Rect Project(Bounds bounds)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector2 point = camera.WorldToViewportPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
