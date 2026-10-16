using System.Collections.Generic;
using BiteOrBetray.Core;
using NUnit.Framework;

namespace BiteOrBetray.Tests
{
    /// <summary>Pengaturan menu utama: nilai bawaan, batas, dan simpan/baca antar sesi lewat ISettingsStore.</summary>
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
        }

        [Test]
        public void SaveThenLoad_KeepsValuesAcrossSessions()
        {
            var store = new MemoryStore();
            var settings = GameSettings.Load(store);
            settings.ChangeVolume(-3);
            settings.LeftHanded = true;
            settings.CycleMouthSnap();
            settings.Save(store);
            Assert.AreEqual(1, store.saves);

            // Sesi baru: objek baru dari penyimpanan yang sama.
            var next = GameSettings.Load(store);
            Assert.AreEqual(50, next.Volume);
            Assert.IsTrue(next.LeftHanded);
            Assert.AreEqual(MouthSnap.High, next.MouthSnap);
            Assert.Greater(next.MouthDistance, .15f);
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
