using System;
using System.Collections;
using System.Linq;
using BiteOrBetray.Core;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// BOB-063 runtime soak: ten offline matches in a row, alternating Play again and Back to menu -> Play vs Bots with
    /// a different bot count (5, 2, 4, 1, 3). Default item and draw rules, so bots go through the normal item paths.
    /// On every results screen and every menu, scene objects, TextMesh and AudioSource counts must equal the first
    /// results screen (pad items and the candy subtracted), and runtime materials/clips must not grow; spectator
    /// copies are a per-seat cache bounded by the seat renderers. Whole-frame Editor GC is never zero, so per-frame GC
    /// while waiting for the human is split into the director's share (enabled minus disabled frames), which must stay near zero.
    /// </summary>
    public partial class OfflineMatchTests
    {
        const int SoakMatches = 10, GcFrames = 120;
        const double DirectorGcLimit = 2048;
        static readonly int[] SoakBots = { 5, 2, 4, 1, 3 };

        sealed class SoakCounts
        {
            public int objects, textMeshes, audioSources, materials, spectatorMaterials, clips;

            public override string ToString() => $"{objects} objects, {textMeshes} TextMesh, {audioSources} AudioSource, "
                + $"{materials} materials (+{spectatorMaterials} spectator), {clips} clips";
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Soak_ten_matches_with_play_again_and_menu_keep_counts_flat_and_no_gc_while_waiting()
        {
            var defaults = new RuleConfig();
            director.rules.ItemsPerRound = defaults.ItemsPerRound;
            director.rules.RandomDraw = defaults.RandomDraw;
            // TurnTimeLimitSeconds stays 0 from the fixture: the GC windows wait for input without a timeout.
            int spectatorLimit = SpectatorMaterialLimit();
            SoakCounts first = null;
            for (int match = 0; match < SoakMatches; match++)
            {
                string at = $"Match {match + 1} ({SoakBots[match / 2]} bots, {(match % 2 == 0 ? "from menu" : "Play again")}): ";
                yield return StartSoakMatch(match);
                yield return new WaitForSeconds(5); // d20/round popups are gone before measuring
                Assert.IsTrue(HumanReady, at + "waiting for the human");
                double withDirector = 0, withoutDirector = 0;
                using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
                {
                    Assert.IsTrue(gc.Valid, "GC counter unavailable; do not report a false zero.");
                    yield return SampleGc(gc, v => withDirector = v);
                    director.enabled = false;
                    yield return SampleGc(gc, v => withoutDirector = v);
                    director.enabled = true;
                }
                Assert.IsTrue(HumanReady, at + "still waiting for the human after the GC windows");
                TestContext.WriteLine(FormattableString.Invariant(
                    $"{at}GC/frame while waiting {withDirector:0} B, director disabled {withoutDirector:0} B, director share {withDirector - withoutDirector:0} B"));
                Assert.LessOrEqual(withDirector - withoutDirector, DirectorGcLimit, at + "director allocates every frame while waiting for input");

                yield return FinishMatch();
                AssertResults();
                var counts = default(SoakCounts);
                yield return MeasureSettled(c => counts = c);
                TestContext.WriteLine(at + "results: " + counts);
                if (first == null) first = counts;
                AssertFlat(first, counts, at + "results");
                Assert.LessOrEqual(counts.spectatorMaterials, spectatorLimit, at + "spectator materials are copied again instead of cached per seat");

                if (match % 2 == 0) continue; // the next match is Play again
                int menuRequests = 0;
                Action counted = () => menuRequests++;
                director.MenuRequested += counted;
                Select(Button("Back to menu"));
                director.MenuRequested -= counted;
                Assert.AreEqual(1, menuRequests, at + "Back to menu");
                Assert.IsNull(Engine, at + "Back to menu stops the match");
                Assert.IsTrue(Button("Play vs Bots").interactable, at + "main menu");
                yield return MeasureSettled(c => counts = c);
                TestContext.WriteLine(at + "menu: " + counts);
                AssertFlat(first, counts, at + "menu");
            }
        }

        IEnumerator StartSoakMatch(int match)
        {
            var previous = Engine;
            if (match % 2 == 0)
            {
                Select(Button("Play vs Bots"));
                SetBotCount(SoakBots[match / 2]);
                director.seed = HumanFirstSeed(100 + match * 100) - Read<int>(director, "gameIndex");
                bool onboardingDone = Read<bool>(director, "onboardingDone");
                Select(Button("Start"));
                if (!onboardingDone)
                {
                    yield return Until(() => Button("Skip").interactable, "onboarding before the first soak match");
                    Select(Button("Skip"));
                }
            }
            else
            {
                director.seed = HumanFirstSeed(100 + match * 100) - Read<int>(director, "gameIndex");
                Select(Button("Play again"));
            }
            yield return Until(() => Engine != null && Engine != previous, $"match {match + 1} engine");
            Assert.AreEqual(SoakBots[match / 2], Engine.Players.Count(p => p.IsBot), $"match {match + 1} bot count");
            yield return Until(() => HumanReady, $"first human turn of match {match + 1}");
        }

        static IEnumerator SampleGc(ProfilerRecorder gc, Action<double> average)
        {
            long sum = 0;
            int samples = 0;
            for (int i = 0; i < GcFrames; i++)
            {
                yield return null;
                if (gc.Count == 0) continue;
                sum += gc.LastValue;
                samples++;
            }
            Assert.Greater(samples, GcFrames / 2, "GC counter returned too few samples.");
            average((double)sum / samples);
        }

        // Popups expire and deferred Destroy calls finish before counting.
        IEnumerator MeasureSettled(Action<SoakCounts> result)
        {
            yield return Until(() => director.GetComponentsInChildren<TextMesh>(true).All(t => t.name != "Popup"), "popups expire");
            yield return null;
            yield return null;
            result(CountObjects());
        }

        SoakCounts CountObjects()
        {
            // Pad items and the candy depend on the match state, not on leaks; everything else must be identical.
            var transient = new System.Collections.Generic.HashSet<Transform>();
            foreach (var prop in Props) transient.UnionWith(PropButton(prop).GetComponentsInChildren<Transform>(true));
            var candy = Read<GameObject>(director, "candyObject");
            if (candy != null) transient.UnionWith(candy.GetComponentsInChildren<Transform>(true));
            var roots = scene.GetRootGameObjects();
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            return new SoakCounts
            {
                objects = roots.Sum(r => r.GetComponentsInChildren<Transform>(true).Count(t => !transient.Contains(t))),
                textMeshes = roots.Sum(r => r.GetComponentsInChildren<TextMesh>(true).Length),
                audioSources = roots.Sum(r => r.GetComponentsInChildren<AudioSource>(true).Length),
                materials = materials.Count(m => !m.name.EndsWith(" Spectator")),
                spectatorMaterials = materials.Count(m => m.name.EndsWith(" Spectator")),
                clips = Resources.FindObjectsOfTypeAll<AudioClip>().Length
            };
        }

        static void AssertFlat(SoakCounts first, SoakCounts current, string at)
        {
            Assert.AreEqual(first.objects, current.objects, at + ": scene objects changed. First " + first + "; now " + current);
            Assert.AreEqual(first.textMeshes, current.textMeshes, at + ": TextMesh count changed");
            Assert.AreEqual(first.audioSources, current.audioSources, at + ": AudioSource count changed");
            Assert.LessOrEqual(current.materials, first.materials, at + ": runtime materials leak");
            Assert.LessOrEqual(current.clips, first.clips, at + ": runtime audio clips leak");
        }

        // A KO look copies each material slot of a seat once (SpectatorAppearance); more copies than slots is a leak.
        int SpectatorMaterialLimit()
        {
            int slots = 0;
            foreach (var seat in Read<IList>(director, "allSeats"))
                foreach (var field in new[] { "character", "avatar" })
                {
                    var root = Read<Component>(seat, field);
                    if (root != null) slots += root.GetComponentsInChildren<Renderer>(true).Sum(r => r.sharedMaterials.Length);
                }
            Assert.Greater(slots, 0, "Seats have renderers to fade.");
            return slots;
        }
    }
}
