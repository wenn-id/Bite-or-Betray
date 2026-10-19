using UnityEngine;

namespace BiteOrBetray.Table3D
{
    public enum GameplaySound { Pickup, EatGood, EatBad, Give, UseItem, Damage, Elimination, Win, Waiter, MenuSelect }

    /// <summary>Table/menu/music audio hub; missing clips use temporary synthesized tones. Source: Docs/Autopilot/LOG.md (BOB-018/033).</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayAudio : MonoBehaviour
    {
        [Header("Replacement clips (require owner approval)")]
        public AudioClip pickup, eatGood, eatBad, give, useItem, damage, elimination, win;
        [Range(0, 1)] public float volume = .65f;
        public AudioClip waiter, menuSelect, backgroundMusic;
        [Range(0, 1)] public float musicVolume = .22f;

        const int SampleRate = 24000;
        readonly AudioSource[] sources = new AudioSource[4];
        readonly AudioClip[] generated = new AudioClip[(int)GameplaySound.MenuSelect + 1];
        AudioClip[] clips;
        AudioSource musicSource;
        AudioClip generatedMusic;
        bool musicRequested;
        int nextSource;

        void Awake()
        {
            clips = new[] { pickup, eatGood, eatBad, give, useItem, damage, elimination, win, waiter, menuSelect };
            // Fixed pool, created once; 2D so every seat hears cues at the same volume.
            for (int i = 0; i < sources.Length; i++) sources[i] = MakeSource(false);
            musicSource = MakeSource(true);
            MakePlaceholder(GameplaySound.Pickup, .12f, 660, 880);
            MakePlaceholder(GameplaySound.EatGood, .30f, 523, 659, 784);
            MakePlaceholder(GameplaySound.EatBad, .30f, 294, 233, 185);
            MakePlaceholder(GameplaySound.Give, .18f, 440, 660);
            MakePlaceholder(GameplaySound.UseItem, .24f, 784, 523, 784);
            MakePlaceholder(GameplaySound.Damage, .16f, 147, 98);
            MakePlaceholder(GameplaySound.Elimination, .48f, 220, 165, 110);
            MakePlaceholder(GameplaySound.Win, .64f, 523, 659, 784, 1047);
            MakePlaceholder(GameplaySound.Waiter, .4f, 392, 523, 659);
            MakePlaceholder(GameplaySound.MenuSelect, .08f, 880, 1047);
            if (backgroundMusic == null)
                generatedMusic = backgroundMusic = MakeClip("Music", 8, 262, 330, 392, 330, 440, 392, 330, 294,
                    349, 440, 392, 294, 330, 294, 262, 330);
        }

        AudioSource MakeSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            return source;
        }

        // Original sine waves, without third-party samples/assets. Fade each note to avoid clicks.
        void MakePlaceholder(GameplaySound sound, float duration, params float[] notes)
        {
            int index = (int)sound;
            if (clips[index] != null) return;
            generated[index] = clips[index] = MakeClip(sound.ToString(), duration, notes);
        }

        static AudioClip MakeClip(string name, float duration, params float[] notes)
        {
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            float noteDuration = duration / notes.Length;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)SampleRate;
                int note = Mathf.Min((int)(time / noteDuration), notes.Length - 1);
                float local = time - note * noteDuration;
                float fade = Mathf.Clamp01(local / .008f) * Mathf.Clamp01((noteDuration - local) / .025f);
                samples[i] = .25f * fade * Mathf.Sin(2 * Mathf.PI * notes[note] * local);
            }
            var clip = AudioClip.Create("BoB Placeholder " + name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // One master volume from Settings also applies to active cues and looping music.
        public void SetMasterVolume(float value) => AudioListener.volume = Mathf.Clamp01(value);

        public void PlayMusic()
        {
            musicRequested = true;
            // CI Editor batch: the old guard (only isActiveAndEnabled/isPlaying) rejected Start calls, leaving the clip empty.
            // A source reporting isPlaying without this music clip must still be assigned and restarted.
            if (!enabled || !gameObject.activeInHierarchy || musicSource == null) return;
            if (musicSource.isPlaying && musicSource.clip == backgroundMusic) return;
            musicSource.clip = backgroundMusic;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }

        public void Play(GameplaySound sound)
        {
            int index = (int)sound;
            if (!isActiveAndEnabled || clips == null || index < 0 || index >= clips.Length || clips[index] == null) return;
            int selected = nextSource;
            for (int i = 0; i < sources.Length; i++)
            {
                int candidate = (nextSource + i) % sources.Length;
                if (sources[candidate].isPlaying) continue;
                selected = candidate;
                break;
            }
            var source = sources[selected];
            source.Stop();
            source.clip = clips[index];
            source.volume = volume;
            source.Play();
            nextSource = (selected + 1) % sources.Length;
        }

        public void StopEffects()
        {
            foreach (var source in sources) if (source != null) source.Stop();
            nextSource = 0;
        }

        public void StopAll()
        {
            StopEffects();
            if (musicSource != null) musicSource.Stop();
        }

        void OnEnable() { if (musicRequested) PlayMusic(); }
        void OnDisable() => StopAll();

        void OnDestroy()
        {
            StopAll();
            foreach (var source in sources) if (source != null) Destroy(source);
            if (musicSource != null) Destroy(musicSource);
            foreach (var clip in generated) if (clip != null) Destroy(clip);
            if (generatedMusic != null) Destroy(generatedMusic);
        }
    }
}
