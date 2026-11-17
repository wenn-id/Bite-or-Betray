using System.Collections;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Linq;

namespace BiteOrBetray.Tests.PlayMode
{
    public partial class OfflineMatchTests
    {
        [UnityTest]
        public IEnumerator Desktop_overview_menu_remains_legible_and_clickable()
        {
            var camera = Camera.main;camera.aspect = 16f / 9;
            yield return null;
            foreach (string name in new[] { "Play vs Bots", "Create Room", "Join Room", "Settings" })
            {
                var button = Button(name);
                var renderer = button.transform.Find("Card").GetComponent<Renderer>();
                var bounds = renderer.localBounds;
                var points = Enumerable.Range(0, 8).Select(i => camera.WorldToViewportPoint(renderer.transform.TransformPoint(bounds.center +
                    Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))))).ToArray();
                Assert.Greater(points.Max(p => p.y) - points.Min(p => p.y), .035f, name + " shrinks into an unreadable strip in overview.");
                var collider = button.GetComponent<Collider>();
                Physics.SyncTransforms();
                var ray = new Ray(collider.bounds.center - camera.transform.forward * .3f, camera.transform.forward);
                Assert.AreSame(button, TableSelectable.Raycast(ray, .5f, out _));
            }
            CapturePolish(camera, "overview-menu-readable.png");
            Select(Button("Play vs Bots"));
            Assert.IsTrue(Read<bool>(director, "settingUp"));
        }

        [UnityTest]
        public IEnumerator Returning_to_menu_during_candy_carry_resets_the_hand()
        {
            yield return CancelDuringCarry(false);
        }

        [UnityTest]
        public IEnumerator Restarting_during_candy_carry_resets_the_hand()
        {
            yield return CancelDuringCarry(true);
        }

        IEnumerator CancelDuringCarry(bool restart)
        {
            yield return StartMatch(2);
            Time.timeScale = 1;director.tempo = 1;
            var hands = director.artRoot.GetComponentInChildren<SeatedAvatar>().GetComponent<ChibiHands>();
            Select(Candy);
            yield return Until(() => Read<bool>(hands, "carrying"), "hand picks up the candy");
            if (restart) director.Restart();
            else director.ReturnToMenu();
            yield return null;
            Assert.IsFalse(Read<bool>(hands, "carrying"), "A stopped match leaves the hand locked in its carry animation.");
            Assert.IsFalse(Read<bool>(hands, "attached"));
            Assert.IsNull(Read<Transform>(hands, "candy"));
            if (!restart)
            {
                Assert.IsNull(Engine);
                Assert.IsTrue(Button("Play vs Bots").interactable);
            }
        }
    }
}
