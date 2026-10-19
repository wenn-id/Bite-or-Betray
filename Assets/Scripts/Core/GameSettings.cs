using System;

namespace BiteOrBetray.Core
{
    /// <summary>Settings storage across sessions. In game: PlayerPrefs; in tests: a plain dictionary.</summary>
    public interface ISettingsStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void Save();
    }

    /// <summary>Candy-to-mouth tolerance when eating in VR: higher values count candy as inside the mouth from farther away.</summary>
    public enum MouthSnap { Low, Normal, High }

    /// <summary>
    /// Player settings from the main menu: volume, voice, dominant hand, mouth snap, and language. Corrupt or out-of-range
    /// stored values are clamped to valid ranges so the game never uses invalid values.
    /// </summary>
    public sealed class GameSettings
    {
        public const string VolumeKey = "bob.settings.volume";
        public const string LeftHandedKey = "bob.settings.leftHanded";
        public const string MouthSnapKey = "bob.settings.mouthSnap";
        public const string VoiceVolumeKey = "bob.settings.voiceVolume";
        public const string VoiceMutedKey = "bob.settings.voiceMuted";
        public const string LanguageKey = "bob.settings.language";

        public const int VolumeStep = 10;
        public const int DefaultVolume = 80;

        int volume = DefaultVolume;
        int voiceVolume = DefaultVolume;
        MouthSnap mouthSnap = MouthSnap.Normal;
        Language language = Language.English;

        /// <summary>Master volume as a percentage, 0-100.</summary>
        public int Volume
        {
            get => volume;
            set => volume = Math.Max(0, Math.Min(100, value));
        }

        public float Volume01 => volume / 100f;

        /// <summary>Voice playback volume separate from music/SFX; zero fully mutes Vivox output.</summary>
        public int VoiceVolume
        {
            get => voiceVolume;
            set => voiceVolume = Math.Max(0, Math.Min(100, value));
        }

        /// <summary>Mute your own microphone; still hear other players, including while spectating.</summary>
        public bool VoiceMuted { get; set; }

        // Vivox uses -50..50, with 0 as normal volume. UI 100% does not increase gain.
        public int VoiceOutputVolume => (voiceVolume - 100) / 2;

        public void ChangeVoiceVolume(int steps) => VoiceVolume = (voiceVolume + steps * VolumeStep) / VolumeStep * VolumeStep;

        /// <summary>True = left hand dominant: if both hands act in the same frame, the left hand wins.</summary>
        public bool LeftHanded { get; set; }

        public MouthSnap MouthSnap
        {
            get => mouthSnap;
            set => mouthSnap = value < MouthSnap.Low ? MouthSnap.Low : value > MouthSnap.High ? MouthSnap.High : value;
        }

        /// <summary>Candy-to-mouth distance (meters) that counts as eaten. Normal = the old VRTableInput value (0.15 m).</summary>
        public float MouthDistance => mouthSnap == MouthSnap.Low ? .1f : mouthSnap == MouthSnap.High ? .22f : .15f;

        /// <summary>Player text language; defaults to English. Unknown stored values fall back to English.</summary>
        public Language Language
        {
            get => language;
            set => language = Enum.IsDefined(typeof(Language), value) ? value : Language.English;
        }

        public void CycleLanguage() => language = Localization.Next(language);

        public void ChangeVolume(int steps) => Volume = (volume + steps * VolumeStep) / VolumeStep * VolumeStep;

        /// <summary>Low > Normal > High > Low.</summary>
        public void CycleMouthSnap() => mouthSnap = mouthSnap == MouthSnap.High ? MouthSnap.Low : mouthSnap + 1;

        /// <summary>Read from storage; missing keys use defaults.</summary>
        public static GameSettings Load(ISettingsStore store)
        {
            var settings = new GameSettings();
            if (store == null) return settings;
            settings.Volume = store.GetInt(VolumeKey, DefaultVolume);
            settings.LeftHanded = store.GetInt(LeftHandedKey, 0) != 0;
            settings.MouthSnap = (MouthSnap)store.GetInt(MouthSnapKey, (int)MouthSnap.Normal);
            settings.VoiceVolume = store.GetInt(VoiceVolumeKey, DefaultVolume);
            settings.VoiceMuted = store.GetInt(VoiceMutedKey, 0) != 0;
            settings.Language = (Language)store.GetInt(LanguageKey, (int)Language.English);
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
            store.SetInt(LanguageKey, (int)language);
            store.Save();
        }
    }
}
