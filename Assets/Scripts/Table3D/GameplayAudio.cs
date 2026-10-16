using UnityEngine;

namespace BiteOrBetray.Table3D
{
    public enum GameplaySound { Pickup, EatGood, EatBad, Give, UseItem, Damage, Elimination, Win }

    /// <summary>Audio aksi meja; klip kosong memakai nada sintetis sementara. Asal dan persetujuan: Docs/Autopilot/LOG.md (BOB-018).</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayAudio : MonoBehaviour
    {
        [Header("Klip pengganti (harus disetujui owner)")]
        public AudioClip pickup, eatGood, eatBad, give, useItem, damage, elimination, win;
        [Range(0, 1)] public float volume = .65f;

        const int SampleRate = 24000;
        readonly AudioSource[] sources = new AudioSource[4];
        readonly AudioClip[] generated = new AudioClip[8];
        AudioClip[] clips;
        int nextSource;

        void Awake()
        {
            clips = new[] { pickup, eatGood, eatBad, give, useItem, damage, elimination, win };
            // Pool tetap, dibuat sekali; 2D supaya semua kursi mendengar cue dengan volume yang sama.
            for (int i = 0; i < sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0;
                source.dopplerLevel = 0;
                sources[i] = source;
            }
            MakePlaceholder(GameplaySound.Pickup, .12f, 660, 880);
            MakePlaceholder(GameplaySound.EatGood, .30f, 523, 659, 784);
            MakePlaceholder(GameplaySound.EatBad, .30f, 294, 233, 185);
            MakePlaceholder(GameplaySound.Give, .18f, 440, 660);
            MakePlaceholder(GameplaySound.UseItem, .24f, 784, 523, 784);
            MakePlaceholder(GameplaySound.Damage, .16f, 147, 98);
            MakePlaceholder(GameplaySound.Elimination, .48f, 220, 165, 110);
            MakePlaceholder(GameplaySound.Win, .64f, 523, 659, 784, 1047);
        }

        // Gelombang sinus asli, tanpa sampel/aset pihak ketiga. Fade tiap nada menghindari klik.
        void MakePlaceholder(GameplaySound sound, float duration, params float[] notes)
        {
            int index = (int)sound;
            if (clips[index] != null) return;
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
            var clip = AudioClip.Create("BoB Placeholder " + sound, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            generated[index] = clips[index] = clip;
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

        public void StopAll()
        {
            foreach (var source in sources) if (source != null) source.Stop();
            nextSource = 0;
        }

        void OnDisable() => StopAll();

        void OnDestroy()
        {
            StopAll();
            foreach (var source in sources) if (source != null) Destroy(source);
            foreach (var clip in generated) if (clip != null) Destroy(clip);
        }
    }
}
