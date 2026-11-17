using System;
using System.Collections;
using System.Linq;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BiteOrBetray.Tests.PlayMode
{
    public partial class OfflineMatchTests
    {
        [UnityTest]
        public IEnumerator Music_loops_all_four_tracks_and_survives_volume_and_component_changes()
        {
            Time.timeScale = 1;
            var audio = director.GetComponent<GameplayAudio>();
            var music = Read<AudioSource>(audio, "musicSource");
            var effects = Read<AudioSource[]>(audio, "sources");
            Assert.AreEqual(4, audio.roundMusic.Length);
            Assert.AreEqual(4, audio.roundMusic.Distinct().Count());
            Assert.IsTrue(music.loop);
            Assert.AreEqual(0, music.spatialBlend);
            bool output = music.isPlaying;
            TestContext.WriteLine("Audio playback device present: " + output);
            for (int round = 1; round <= 4; round++)
            {
                audio.SetRoundMusic(round);
                Assert.AreSame(audio.roundMusic[round - 1], music.clip);
                Assert.Greater(music.clip.length, 20);
                Assert.AreEqual(audio.musicVolume, music.volume, .0001f);
                if (!output) continue;
                music.time = music.clip.length - .25f;
                yield return new WaitForSecondsRealtime(.65f);
                Assert.IsTrue(music.isPlaying, "Music stopped instead of looping in round " + round);
                Assert.Less(music.time, 1.5f, "Music did not wrap around its endpoint.");
                int sample = music.timeSamples;
                audio.PlayMusic();
                Assert.GreaterOrEqual(music.timeSamples, sample, "Repeated PlayMusic restarts the same track.");
            }
            audio.SetMasterVolume(0);
            Assert.AreEqual(0, AudioListener.volume);
            audio.SetMasterVolume(.42f);
            Assert.AreEqual(.42f, AudioListener.volume, .0001f);
            foreach (GameplaySound sound in Enum.GetValues(typeof(GameplaySound)))
            {
                audio.Play(sound);
                var clip = Read<AudioClip[]>(audio, "clips")[(int)sound];
                Assert.IsNotNull(clip);
                Assert.IsTrue(effects.Any(s => s.clip == clip && Mathf.Abs(s.volume - audio.volume) < .0001f));
            }
            audio.StopEffects();
            Assert.IsTrue(effects.All(s => !s.isPlaying));
            if (output) Assert.IsTrue(music.isPlaying, "Stopping effects also stopped the music.");
            for (int i = 0; i < 3; i++)
            {
                audio.enabled = false;
                Assert.IsFalse(music.isPlaying);
                audio.enabled = true;
                yield return null;
                if (output) Assert.IsTrue(music.isPlaying, "Music did not resume on enable.");
                Assert.AreEqual(5, audio.GetComponents<AudioSource>().Length);
            }
            audio.StopAll();
            Assert.IsFalse(music.isPlaying);
            audio.PlayMusic();
            if (output) Assert.IsTrue(music.isPlaying);
        }

        [UnityTest]
        public IEnumerator Live_round_transitions_change_music_and_menu_restores_first_track()
        {
            yield return StartMatch(1);
            var audio = director.GetComponent<GameplayAudio>();
            var music = Read<AudioSource>(audio, "musicSource");
            for (int round = 1; round <= 5; round++)
            {
                Assert.AreEqual(round, Engine.RoundNumber);
                Assert.AreSame(audio.roundMusic[(round - 1) % 4], music.clip, "Wrong soundtrack for live round " + round);
                if (round == 5) break;
                Engine.DebugSetPool(new[] { new Candy(CandyColor.Yellow, true) });
                Select(Candy);
                int nextRound = round + 1;
                yield return Until(() => Engine.RoundNumber == nextRound && HumanReady, "round music transition " + nextRound);
            }
            director.ReturnToMenu();
            yield return null;
            Assert.AreSame(audio.roundMusic[0], music.clip, "Returning to menu retains the last round's music.");
        }
    }
}
