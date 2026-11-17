using UnityEngine;

namespace BiteOrBetray.Table3D
{
    public enum GameplaySound { Pickup, EatGood, EatBad, Give, UseItem, Damage, Elimination, Win, Waiter, MenuSelect }

    /// <summary>Table/menu/music audio hub; loads studio WAV clips from Resources/Audio or synthesizes rich parlour jazz/SFX cues. Source: Docs/Autopilot/LOG.md (BOB-018/033).</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayAudio : MonoBehaviour
    {
        [Header("Replacement clips (auto-loaded from Resources/Audio/BoB_* if unset)")]
        public AudioClip pickup, eatGood, eatBad, give, useItem, damage, elimination, win;
        [Range(0, 1)] public float volume = .65f;
        public AudioClip waiter, menuSelect, backgroundMusic;
        public AudioClip[] roundMusic;
        [Range(0, 1)] public float musicVolume = .24f;

        static readonly string[] RoundMusicResources =
            { "Audio/BoB_Music", "Audio/BoB_Music_Round2", "Audio/BoB_Music_Round3", "Audio/BoB_Music_Round4" };

        const int SampleRate = 24000;
        readonly AudioSource[] sources = new AudioSource[4];
        readonly AudioClip[] generated = new AudioClip[(int)GameplaySound.MenuSelect + 1];
        readonly AudioClip[] generatedRoundMusic = new AudioClip[4];
        AudioClip[] clips;
        AudioSource musicSource;
        bool musicRequested;
        int nextSource;

        void Awake()
        {
            clips = new[] { pickup, eatGood, eatBad, give, useItem, damage, elimination, win, waiter, menuSelect };
            // Fixed pool, created once; 2D so every seat hears cues at the same volume.
            for (int i = 0; i < sources.Length; i++) sources[i] = MakeSource(false);
            musicSource = MakeSource(true);

            LoadOrSynthesize(GameplaySound.Pickup, () => SynthPickup());
            LoadOrSynthesize(GameplaySound.EatGood, () => SynthEat(true));
            LoadOrSynthesize(GameplaySound.EatBad, () => SynthEat(false));
            LoadOrSynthesize(GameplaySound.Give, () => SynthGive());
            LoadOrSynthesize(GameplaySound.UseItem, () => SynthUseItem());
            LoadOrSynthesize(GameplaySound.Damage, () => SynthDamage());
            LoadOrSynthesize(GameplaySound.Elimination, () => SynthElimination());
            LoadOrSynthesize(GameplaySound.Win, () => SynthWin());
            LoadOrSynthesize(GameplaySound.Waiter, () => SynthWaiter());
            LoadOrSynthesize(GameplaySound.MenuSelect, () => SynthMenuSelect());

            pickup = clips[(int)GameplaySound.Pickup];
            eatGood = clips[(int)GameplaySound.EatGood];
            eatBad = clips[(int)GameplaySound.EatBad];
            give = clips[(int)GameplaySound.Give];
            useItem = clips[(int)GameplaySound.UseItem];
            damage = clips[(int)GameplaySound.Damage];
            elimination = clips[(int)GameplaySound.Elimination];
            win = clips[(int)GameplaySound.Win];
            waiter = clips[(int)GameplaySound.Waiter];
            menuSelect = clips[(int)GameplaySound.MenuSelect];

            if (roundMusic == null || roundMusic.Length == 0)
                roundMusic = new AudioClip[RoundMusicResources.Length];
            if (backgroundMusic != null && roundMusic[0] == null)
                roundMusic[0] = backgroundMusic;
            for (int i = 0; i < roundMusic.Length; i++)
            {
                if (roundMusic[i] != null) continue;
                string resPath = i < RoundMusicResources.Length ? RoundMusicResources[i] : RoundMusicResources[0];
                roundMusic[i] = Resources.Load<AudioClip>(resPath);
                if (roundMusic[i] == null && i < generatedRoundMusic.Length)
                    generatedRoundMusic[i] = roundMusic[i] = SynthParlourJazzLoop(i);
            }
            if (backgroundMusic == null)
                backgroundMusic = roundMusic[0];
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

        void LoadOrSynthesize(GameplaySound sound, System.Func<AudioClip> synthFallback)
        {
            int index = (int)sound;
            if (clips[index] != null) return;
            var res = Resources.Load<AudioClip>("Audio/BoB_" + sound);
            if (res != null)
            {
                clips[index] = res;
                return;
            }
            generated[index] = clips[index] = synthFallback();
        }

        static float MidiToFreq(int midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        static void AddVibeNote(float[] buf, float startSec, int midi, float dur, float amp)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(startSec * SampleRate));
            int n = Mathf.Min(Mathf.CeilToInt((dur + .08f) * SampleRate), buf.Length - i0);
            float f = MidiToFreq(midi);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Clamp01(t / .003f) * Mathf.Exp(-t / (dur * .85f + .04f));
                float mod = 1.2f * Mathf.Exp(-t / .02f) * Mathf.Sin(2f * Mathf.PI * (f * 4f) * t);
                float trem = 1f - .18f * (.5f - .5f * Mathf.Cos(2f * Mathf.PI * 6f * t));
                buf[i0 + i] += amp * env * trem * (.82f * Mathf.Sin(2f * Mathf.PI * f * t + mod) + .18f * Mathf.Sin(4f * Mathf.PI * f * t));
            }
        }

        static void AddBassNote(float[] buf, float startSec, int midi, float dur, float amp)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(startSec * SampleRate));
            int n = Mathf.Min(Mathf.CeilToInt((dur + .04f) * SampleRate), buf.Length - i0);
            float f = MidiToFreq(midi);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Clamp01(t / .005f) * Mathf.Exp(-t / (dur * .75f));
                float ph = 2f * Mathf.PI * f * t;
                buf[i0 + i] += amp * env * (.7f * Mathf.Sin(ph) + .35f * Mathf.Sin(2f * ph) * Mathf.Exp(-t / .15f) + .15f * Mathf.Sin(3f * ph) * Mathf.Exp(-t / .08f));
            }
        }

        static void AddCrunch(float[] buf, float startSec, float dur, float pitchShift, float amp, int seed)
        {
            int i0 = Mathf.Max(0, Mathf.RoundToInt(startSec * SampleRate));
            int n = Mathf.Min(Mathf.CeilToInt(dur * SampleRate), buf.Length - i0);
            uint state = (uint)(seed * 747796405 + 2891336453);
            float f1 = 680f * pitchShift, f2 = 2100f * pitchShift;
            for (int i = 0; i < n; i++)
            {
                state = state * 747796405u + 2891336453u;
                float r = ((state >> 8) & 0xFFFF) / 32768f - 1f;
                float t = i / (float)SampleRate;
                float env = Mathf.Clamp01(t / .003f) * Mathf.Exp(-t / (dur * .4f));
                float jaw = Mathf.Sin(2f * Mathf.PI * (170f * pitchShift) * t) * Mathf.Exp(-t / .025f);
                float crack = r * (.6f * Mathf.Sin(2f * Mathf.PI * f1 * t) + .4f * Mathf.Sin(2f * Mathf.PI * f2 * t));
                buf[i0 + i] += amp * env * (.65f * crack + .35f * jaw);
            }
        }

        static AudioClip FinalizeClip(string name, float[] samples, float targetPeak = .85f)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = Mathf.Abs(samples[i]);
                if (a > peak) peak = a;
            }
            if (peak > 1e-5f)
            {
                float scale = targetPeak / peak;
                for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i] * scale, -1f, 1f);
            }
            var clip = AudioClip.Create("BoB " + name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static AudioClip SynthPickup()
        {
            var buf = new float[Mathf.CeilToInt(.24f * SampleRate)];
            AddVibeNote(buf, .00f, 76, .09f, .6f);
            AddVibeNote(buf, .06f, 81, .14f, .8f);
            return FinalizeClip("Pickup", buf);
        }

        static AudioClip SynthGive()
        {
            var buf = new float[Mathf.CeilToInt(.36f * SampleRate)];
            AddVibeNote(buf, .02f, 69, .10f, .55f);
            AddVibeNote(buf, .09f, 72, .11f, .68f);
            AddVibeNote(buf, .16f, 76, .16f, .82f);
            return FinalizeClip("Give", buf);
        }

        static AudioClip SynthEat(bool good)
        {
            var buf = new float[Mathf.CeilToInt(.95f * SampleRate)];
            AddCrunch(buf, .00f, .10f, 1.12f, .85f, 10);
            AddCrunch(buf, .14f, .10f, .95f, .72f, 20);
            AddCrunch(buf, .27f, .10f, 1.04f, .65f, 30);
            if (good)
            {
                int[] notes = { 72, 76, 79, 83, 84 };
                for (int i = 0; i < notes.Length; i++)
                    AddVibeNote(buf, .40f + i * .06f, notes[i], .22f, .75f);
            }
            else
            {
                AddBassNote(buf, .38f, 63, .16f, .70f);
                AddBassNote(buf, .50f, 62, .16f, .72f);
                AddBassNote(buf, .62f, 61, .28f, .80f);
                AddBassNote(buf, .40f, 42, .45f, .75f);
            }
            return FinalizeClip(good ? "EatGood" : "EatBad", buf);
        }

        static AudioClip SynthUseItem()
        {
            var buf = new float[Mathf.CeilToInt(.46f * SampleRate)];
            int[] notes = { 67, 72, 74, 79, 84 };
            for (int i = 0; i < notes.Length; i++)
                AddVibeNote(buf, .02f + i * .05f, notes[i], .18f, .72f);
            return FinalizeClip("UseItem", buf);
        }

        static AudioClip SynthDamage()
        {
            var buf = new float[Mathf.CeilToInt(.40f * SampleRate)];
            AddCrunch(buf, .00f, .08f, 1.4f, .75f, 77);
            AddBassNote(buf, .00f, 48, .28f, .85f);
            AddBassNote(buf, .01f, 42, .32f, .85f);
            AddBassNote(buf, .02f, 36, .35f, .95f);
            return FinalizeClip("Damage", buf);
        }

        static AudioClip SynthElimination()
        {
            var buf = new float[Mathf.CeilToInt(1.20f * SampleRate)];
            int[] chord = { 36, 48, 51, 54, 57 };
            for (int i = 0; i < chord.Length; i++) AddBassNote(buf, .01f, chord[i], .85f, .55f);
            int[] run = { 72, 69, 66, 63, 60, 54, 48 };
            for (int i = 0; i < run.Length; i++) AddVibeNote(buf, .10f + i * .09f, run[i], .22f, .60f);
            return FinalizeClip("Elimination", buf);
        }

        static AudioClip SynthWin()
        {
            var buf = new float[Mathf.CeilToInt(1.50f * SampleRate)];
            int[] melody = { 72, 76, 79, 81, 84 };
            float[] times = { .10f, .26f, .42f, .58f, .74f };
            for (int i = 0; i < melody.Length; i++)
            {
                AddVibeNote(buf, times[i], melody[i], i == 4 ? .65f : .16f, .80f);
                AddBassNote(buf, times[i], melody[i] - 24, i == 4 ? .65f : .16f, .55f);
            }
            return FinalizeClip("Win", buf);
        }

        static AudioClip SynthWaiter()
        {
            var buf = new float[Mathf.CeilToInt(.70f * SampleRate)];
            AddVibeNote(buf, .00f, 84, .55f, .90f);
            AddVibeNote(buf, .00f, 96, .35f, .55f);
            AddVibeNote(buf, .18f, 79, .28f, .50f);
            return FinalizeClip("Waiter", buf);
        }

        static AudioClip SynthMenuSelect()
        {
            var buf = new float[Mathf.CeilToInt(.12f * SampleRate)];
            AddVibeNote(buf, .00f, 81, .06f, .70f);
            AddVibeNote(buf, .03f, 88, .07f, .80f);
            return FinalizeClip("MenuSelect", buf);
        }

        static AudioClip SynthParlourJazzLoop(int roundVariant = 0)
        {
            // 16-second (8-bar @ 120 BPM) mischievous parlour swing fallback loop, transposed per round variant
            var buf = new float[16 * SampleRate];
            int[] transpose = { 0, 2, 5, 7 };
            int semi = transpose[Mathf.Clamp(roundVariant, 0, transpose.Length - 1)];
            int[,] bassBars = {
                { 48, 51, 55, 57 }, { 41, 44, 48, 50 }, { 43, 47, 50, 53 }, { 48, 55, 51, 48 },
                { 48, 51, 55, 57 }, { 44, 48, 51, 54 }, { 43, 47, 50, 43 }, { 48, 51, 43, 47 }
            };
            for (int bar = 0; bar < 8; bar++)
            {
                for (int b = 0; b < 4; b++)
                    AddBassNote(buf, bar * 2f + b * .5f, bassBars[bar, b] + semi, .42f, .48f);
                AddVibeNote(buf, bar * 2f + .5f, 60 + semi, .18f, .28f);
                AddVibeNote(buf, bar * 2f + 1.32f, 63 + semi, .18f, .26f);
            }
            (float t, int m, float d)[] mel = {
                (0f, 72, .38f), (.5f, 75, .32f), (.82f, 74, .22f), (1f, 72, .32f), (1.32f, 69, .30f), (1.5f, 67, .42f),
                (2f, 68, .38f), (2.5f, 72, .32f), (2.82f, 75, .22f), (3f, 74, .60f),
                (4f, 71, .32f), (4.32f, 72, .22f), (4.5f, 74, .32f), (5f, 77, .38f), (5.5f, 71, .38f),
                (6f, 72, .80f), (7f, 79, .24f), (7.32f, 75, .24f), (7.5f, 72, .38f),
                (8f, 79, .38f), (8.5f, 75, .32f), (9f, 74, .32f), (9.5f, 79, .42f),
                (10f, 78, .20f), (10.15f, 79, .42f), (11f, 72, .42f), (11.5f, 68, .38f),
                (12f, 71, .32f), (12.5f, 74, .32f), (13f, 77, .32f), (13.5f, 79, .42f),
                (14f, 72, .80f), (15f, 67, .28f), (15.32f, 71, .24f), (15.5f, 74, .35f)
            };
            foreach (var n in mel) AddVibeNote(buf, n.t, n.m + semi, n.d, .52f);
            return FinalizeClip(roundVariant == 0 ? "Music" : "Music Round " + (roundVariant + 1), buf, .75f);
        }

        // One master volume from Settings also applies to active cues and looping music.
        public void SetMasterVolume(float value) => AudioListener.volume = Mathf.Clamp01(value);

        /// <summary>Switch the looping soundtrack based on the current round number (Round 1 / Menu = index 0, Round 2 = index 1, etc.).</summary>
        public void SetRoundMusic(int roundNumber)
        {
            if (roundMusic == null || roundMusic.Length == 0) return;
            int index = roundNumber <= 1 ? 0 : (roundNumber - 1) % roundMusic.Length;
            var target = roundMusic[index] != null ? roundMusic[index] : roundMusic[0];
            if (target == null) return;
            backgroundMusic = target;
            if (musicRequested) PlayMusic();
        }

        public void PlayMusic()
        {
            musicRequested = true;
            // CI Editor batch: the old guard (only isActiveAndEnabled/isPlaying) rejected Start calls, leaving the clip empty.
            // A source reporting isPlaying without this music clip must still be assigned and restarted.
            if (musicSource == null) return;
            bool clipChanged = musicSource.clip != backgroundMusic;
            musicSource.clip = backgroundMusic;
            musicSource.volume = musicVolume;
            if (!enabled || !gameObject.activeInHierarchy) return;
            if (musicSource.isPlaying && !clipChanged) return;
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
            foreach (var clip in generatedRoundMusic) if (clip != null) Destroy(clip);
        }
    }
}
