using System.Collections.Generic;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    /// <summary>Main menu settings: defaults, limits, and save/load across sessions via ISettingsStore.</summary>
    public class GameSettingsTests
    {
        sealed class MemoryStore : ISettingsStore
        {
            public readonly Dictionary<string, int> values = new Dictionary<string, int>();
            public int saves;

            public int GetInt(string key, int fallback) => values.TryGetValue(key, out int v) ? v : fallback;
            public void SetInt(string key, int value) => values[key] = value;
            public void Save() => saves++;
        }

        [Test]
        public void EmptyStore_UsesDefaults()
        {
            var settings = GameSettings.Load(new MemoryStore());
            Assert.AreEqual(GameSettings.DefaultVolume, settings.Volume);
            Assert.IsFalse(settings.LeftHanded);
            Assert.AreEqual(MouthSnap.Normal, settings.MouthSnap);
            Assert.AreEqual(.15f, settings.MouthDistance, 1e-5f);
            Assert.AreEqual(GameSettings.DefaultVolume, settings.VoiceVolume);
            Assert.IsFalse(settings.VoiceMuted);
            Assert.AreEqual(Language.English, settings.Language, "Default English for the Devpost judges.");
        }

        [Test]
        public void SaveThenLoad_KeepsValuesAcrossSessions()
        {
            var store = new MemoryStore();
            var settings = GameSettings.Load(store);
            settings.ChangeVolume(-3);
            settings.LeftHanded = true;
            settings.CycleMouthSnap();
            settings.ChangeVoiceVolume(-1);
            settings.VoiceMuted = true;
            settings.CycleLanguage();
            settings.Save(store);
            Assert.AreEqual(1, store.saves);

            // New session: a new object from the same store.
            var next = GameSettings.Load(store);
            Assert.AreEqual(50, next.Volume);
            Assert.IsTrue(next.LeftHanded);
            Assert.AreEqual(MouthSnap.High, next.MouthSnap);
            Assert.Greater(next.MouthDistance, .15f);
            Assert.AreEqual(70, next.VoiceVolume);
            Assert.IsTrue(next.VoiceMuted);
            Assert.AreEqual(Language.Indonesian, next.Language);
        }

        [Test]
        public void Language_CyclesEnglishIndonesianAndLoadsUnknownAsEnglish()
        {
            var settings = new GameSettings();
            settings.CycleLanguage();
            Assert.AreEqual(Language.Indonesian, settings.Language);
            settings.CycleLanguage();
            Assert.AreEqual(Language.English, settings.Language);

            var store = new MemoryStore();
            store.SetInt(GameSettings.LanguageKey, 1);
            Assert.AreEqual(Language.Indonesian, GameSettings.Load(store).Language);
            foreach (int corrupt in new[] { -1, 2, 99 })
            {
                store.SetInt(GameSettings.LanguageKey, corrupt);
                Assert.AreEqual(Language.English, GameSettings.Load(store).Language, "value " + corrupt);
            }
        }

        [Test]
        public void Volume_StepsAndClamps()
        {
            var settings = new GameSettings();
            settings.ChangeVolume(5);
            Assert.AreEqual(100, settings.Volume);
            settings.ChangeVolume(-20);
            Assert.AreEqual(0, settings.Volume);
            Assert.AreEqual(0f, settings.Volume01);
            settings.Volume = 85;
            settings.ChangeVolume(1);
            Assert.AreEqual(90, settings.Volume);
        }

        [Test]
        public void VoiceVolume_StepsClampAndStaySeparateFromMusicAndMicrophone()
        {
            var settings = new GameSettings { VoiceMuted = true };
            settings.ChangeVoiceVolume(5);
            Assert.AreEqual(100, settings.VoiceVolume);
            settings.ChangeVoiceVolume(-20);
            Assert.AreEqual(0, settings.VoiceVolume);
            Assert.IsTrue(settings.VoiceMuted);
            settings.VoiceVolume = 85;
            settings.ChangeVoiceVolume(1);
            Assert.AreEqual(90, settings.VoiceVolume);
            Assert.AreEqual(GameSettings.DefaultVolume, settings.Volume);
            settings.Volume = 0;
            Assert.AreEqual(90, settings.VoiceVolume);
        }

        [TestCase(-200, 0, -50)]
        [TestCase(0, 0, -50)]
        [TestCase(50, 50, -25)]
        [TestCase(80, 80, -10)]
        [TestCase(100, 100, 0)]
        [TestCase(250, 100, 0)]
        public void VoiceVolume_LoadClampsAndMapsToVivoxWithoutBoost(int stored, int percent, int output)
        {
            var store = new MemoryStore();
            store.SetInt(GameSettings.VoiceVolumeKey, stored);
            var settings = GameSettings.Load(store);
            Assert.AreEqual(percent, settings.VoiceVolume);
            Assert.AreEqual(output, settings.VoiceOutputVolume);
        }

        [Test]
        public void ExistingSettings_WithoutVoiceKeysKeepVoiceEnabled()
        {
            var store = new MemoryStore();
            store.SetInt(GameSettings.VolumeKey, 0);
            store.SetInt(GameSettings.LeftHandedKey, 1);
            var settings = GameSettings.Load(store);
            Assert.IsFalse(settings.VoiceMuted);
            Assert.AreEqual(GameSettings.DefaultVolume, settings.VoiceVolume);
            Assert.AreEqual(0, settings.Volume);
            Assert.IsTrue(settings.LeftHanded);
        }

        [Test]
        public void MouthSnap_CyclesLowNormalHigh()
        {
            var settings = new GameSettings();
            settings.CycleMouthSnap();
            Assert.AreEqual(MouthSnap.High, settings.MouthSnap);
            settings.CycleMouthSnap();
            Assert.AreEqual(MouthSnap.Low, settings.MouthSnap);
            Assert.Less(settings.MouthDistance, .15f);
            settings.CycleMouthSnap();
            Assert.AreEqual(MouthSnap.Normal, settings.MouthSnap);
        }

        [Test]
        public void CorruptStore_ClampedToValidRange()
        {
            var store = new MemoryStore();
            store.SetInt(GameSettings.VolumeKey, 250);
            store.SetInt(GameSettings.LeftHandedKey, 7);
            store.SetInt(GameSettings.MouthSnapKey, -4);
            var settings = GameSettings.Load(store);
            Assert.AreEqual(100, settings.Volume);
            Assert.IsTrue(settings.LeftHanded);
            Assert.AreEqual(MouthSnap.Low, settings.MouthSnap);

            store.SetInt(GameSettings.MouthSnapKey, 99);
            Assert.AreEqual(MouthSnap.High, GameSettings.Load(store).MouthSnap);
        }
    }
}
