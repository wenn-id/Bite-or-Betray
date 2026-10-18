using System;

namespace BiteOrBetray.Core
{
    /// <summary>Tempat simpan pengaturan antar sesi. Di game: PlayerPrefs; di tes: kamus biasa.</summary>
    public interface ISettingsStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void Save();
    }

    /// <summary>Toleransi permen ke mulut saat makan di VR: makin tinggi, makin jauh permen sudah "masuk" mulut.</summary>
    public enum MouthSnap { Low, Normal, High }

    /// <summary>
    /// Pengaturan pemain dari menu utama: volume, voice, tangan dominan, dan snap ke mulut. Nilai dari penyimpanan yang rusak
    /// atau di luar batas dijepit ke rentang yang sah, jadi game tidak pernah memakai nilai aneh.
    /// </summary>
    public sealed class GameSettings
    {
        public const string VolumeKey = "bob.settings.volume";
        public const string LeftHandedKey = "bob.settings.leftHanded";
        public const string MouthSnapKey = "bob.settings.mouthSnap";
        public const string VoiceVolumeKey = "bob.settings.voiceVolume";
        public const string VoiceMutedKey = "bob.settings.voiceMuted";

        public const int VolumeStep = 10;
        public const int DefaultVolume = 80;

        int volume = DefaultVolume;
        int voiceVolume = DefaultVolume;
        MouthSnap mouthSnap = MouthSnap.Normal;

        /// <summary>Volume utama dalam persen, 0-100.</summary>
        public int Volume
        {
            get => volume;
            set => volume = Math.Max(0, Math.Min(100, value));
        }

        public float Volume01 => volume / 100f;

        /// <summary>Volume dengar voice terpisah dari musik/SFX; nol benar-benar membisukan keluaran Vivox.</summary>
        public int VoiceVolume
        {
            get => voiceVolume;
            set => voiceVolume = Math.Max(0, Math.Min(100, value));
        }

        /// <summary>Mute mikrofon sendiri; tetap bisa mendengar pemain lain, termasuk saat menjadi spectator.</summary>
        public bool VoiceMuted { get; set; }

        // Vivox memakai -50..50, dengan 0 sebagai volume normal. UI 100% tidak menambah gain.
        public int VoiceOutputVolume => (voiceVolume - 100) / 2;

        public void ChangeVoiceVolume(int steps) => VoiceVolume = (voiceVolume + steps * VolumeStep) / VolumeStep * VolumeStep;

        /// <summary>True = tangan kiri dominan: kalau kedua tangan beraksi di frame yang sama, tangan kiri yang menang.</summary>
        public bool LeftHanded { get; set; }

        public MouthSnap MouthSnap
        {
            get => mouthSnap;
            set => mouthSnap = value < MouthSnap.Low ? MouthSnap.Low : value > MouthSnap.High ? MouthSnap.High : value;
        }

        /// <summary>Jarak permen ke mulut (meter) supaya dihitung dimakan. Normal = nilai lama VRTableInput (0,15 m).</summary>
        public float MouthDistance => mouthSnap == MouthSnap.Low ? .1f : mouthSnap == MouthSnap.High ? .22f : .15f;

        public void ChangeVolume(int steps) => Volume = (volume + steps * VolumeStep) / VolumeStep * VolumeStep;

        /// <summary>Low > Normal > High > Low.</summary>
        public void CycleMouthSnap() => mouthSnap = mouthSnap == MouthSnap.High ? MouthSnap.Low : mouthSnap + 1;

        /// <summary>Baca dari penyimpanan; kunci yang belum ada memakai nilai bawaan.</summary>
        public static GameSettings Load(ISettingsStore store)
        {
            var settings = new GameSettings();
            if (store == null) return settings;
            settings.Volume = store.GetInt(VolumeKey, DefaultVolume);
            settings.LeftHanded = store.GetInt(LeftHandedKey, 0) != 0;
            settings.MouthSnap = (MouthSnap)store.GetInt(MouthSnapKey, (int)MouthSnap.Normal);
            settings.VoiceVolume = store.GetInt(VoiceVolumeKey, DefaultVolume);
            settings.VoiceMuted = store.GetInt(VoiceMutedKey, 0) != 0;
            return settings;
        }

        public void Save(ISettingsStore store)
        {
            if (store == null) return;
            store.SetInt(VolumeKey, volume);
            store.SetInt(LeftHandedKey, LeftHanded ? 1 : 0);
            store.SetInt(MouthSnapKey, (int)mouthSnap);
            store.SetInt(VoiceVolumeKey, voiceVolume);
            store.SetInt(VoiceMutedKey, VoiceMuted ? 1 : 0);
            store.Save();
        }
    }
}
