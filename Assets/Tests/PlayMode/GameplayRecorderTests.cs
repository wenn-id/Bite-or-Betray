#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BiteOrBetray.Art3D;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// BOB-045: Editor recorder in the real scene without a headset. An all-bot match is recorded from the player seat
    /// until a winner (1920x1080 JPG frames + make_video.cmd/.sh containing ffmpeg); other presets record a few frames.
    /// Call the Editor assembly through reflection because the PlayMode asmdef can also be built into a player.
    /// </summary>
    public class GameplayRecorderTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Type Recorder = Type.GetType("BiteOrBetray.EditorTools.GameplayRecorder, BiteOrBetray.Editor", true);
        CandyTableDirector director;
        Scene scene;
        string shot, folder;
        float oldTimeScale;
        bool oldRunInBackground;

        static bool Recording => (bool)Recorder.GetProperty("Recording").GetValue(null);
        static int Frames => (int)Recorder.GetProperty("Frames").GetValue(null);
        Vector3 Seat => (director.artRoot.Find("Seats/Seat_0/HeadAnchor") ?? director.artRoot.Find("Seats/Seat_0")).position;

        [SetUp]
        public void Remember()
        {
            oldTimeScale = Time.timeScale;
            oldRunInBackground = Application.runInBackground;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= Configure;
            Call("Stop");
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("BOB-045 Cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = oldTimeScale;
            Application.runInBackground = oldRunInBackground;
            if (folder != null && Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator Pertandingan_dari_kursi_pemain_sampai_pemenang_menyimpan_frame_1080p_dan_perintah_ffmpeg()
        {
            yield return Begin("Match");
            Assert.IsNull(Read<object>(director, "human"), "Rekaman pertandingan dimainkan bot saja; kursi pemain dipakai kamera.");
            director.tempo = 4;
            director.thinkSeconds = Vector2.zero;
            Time.timeScale = 4;
            var camera = Camera.main.transform;
            Vector3 start = camera.forward;
            float maxTurn = 0, maxMove = 0, deadline = Time.realtimeSinceStartup + 840;
            while (Recording)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Rekaman pertandingan tidak selesai.");
                maxTurn = Mathf.Max(maxTurn, Vector3.Angle(start, camera.forward));
                maxMove = Mathf.Max(maxMove, Vector3.Distance(Seat, camera.position));
                yield return null;
            }
            var engine = Read<GameEngine>(director, "engine");
            Assert.IsNotNull(engine, "Autoplay harus memulai pertandingan tanpa menu.");
            Assert.IsTrue(engine.IsOver, "Rekaman berhenti sendiri setelah ada pemenang.");
            Assert.IsNotNull(engine.Winner);
            Assert.Less(maxMove, .01f, "Kamera tetap di kursi pemain.");
            Assert.Greater(maxTurn, 5, "Kamera menoleh ke bot yang sedang beraksi.");
            Assert.Less(maxTurn, 45, "Menoleh sedikit saja, tidak memutar penuh.");
            AssertOutput();
        }

        [UnityTest]
        public IEnumerator Shot_menu_utama_dari_kursi_pemain()
        {
            yield return RecordShot("Menu");
            Assert.IsNotNull(Read<object>(director, "human"), "Shot menu butuh kursi manusia.");
            Assert.IsTrue(Read<TableSelectable>(director, "playBotsButton").gameObject.activeInHierarchy, "Menu utama harus terbuka.");
            Assert.Less(Vector3.Distance(Seat, Camera.main.transform.position), .15f);
        }

        [UnityTest]
        public IEnumerator Shot_close_up_bot_bereaksi()
        {
            yield return RecordShot("CloseUpBot");
            Vector3 camera = Camera.main.transform.position;
            float nearest = CharacterAnimator.All.Where(c => c.name.StartsWith("Bot_") && c.Head != null)
                .Min(c => Vector3.Distance(c.Head.position, camera));
            Assert.Less(nearest, 1, "Kamera close-up harus dekat kepala bot.");
        }

        [UnityTest]
        public IEnumerator Shot_meja_dari_atas()
        {
            yield return RecordShot("TopDown");
            var camera = Camera.main.transform;
            Assert.Greater(camera.position.y, director.artRoot.Find("Dispenser").position.y + 1.5f);
            Assert.Less(camera.forward.y, -.8f, "Kamera harus menghadap ke bawah ke meja.");
        }

        [UnityTest]
        public IEnumerator Seat_recording_writes_English_video_scripts_and_preserves_player_camera()
        {
            yield return RecordShot("Seat");
            Assert.IsNotNull(Read<object>(director, "human"), "Seat recording keeps the human seat.");
            Assert.Less(Vector3.Distance(Seat, Camera.main.transform.position), .15f);
            CollectionAssert.AreEquivalent(new[] { "make_video.cmd", "make_video.sh" },
                Directory.GetFiles(folder, "make_video.*").Select(Path.GetFileName));
        }

        IEnumerator RecordShot(string name)
        {
            yield return Begin(name);
            float deadline = Time.realtimeSinceStartup + 60;
            while (Frames < 45)
            {
                Assert.IsTrue(Recording, "Preset berhenti sebelum 45 frame.");
                Assert.Less(Time.realtimeSinceStartup, deadline, "Frame preset tidak tersimpan.");
                yield return null;
            }
            Call("Stop");
            AssertOutput();
        }

        IEnumerator Begin(string name)
        {
            shot = name;
            folder = Path.Combine(Application.temporaryCachePath, "BoBRecorderTest_" + name);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Time.timeScale = 1;
            Application.runInBackground = true;
            yield return EditorXR.Stop();
            SceneManager.sceneLoaded += Configure;
            yield return SceneManager.LoadSceneAsync("CandyParlour3D", LoadSceneMode.Single);
            SceneManager.sceneLoaded -= Configure;
            scene = SceneManager.GetActiveScene();
            Assert.IsNotNull(director, "Director wajib berasal dari scene gameplay.");
            float deadline = Time.realtimeSinceStartup + 20;
            while (!Read<bool>(director, "ready"))
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "director siap");
                yield return null;
            }
            Call("Begin", shot, folder, false);
            Assert.IsTrue(Recording);
            Assert.AreEqual(30, Time.captureFramerate);
        }

        // sceneLoaded jalan setelah Awake dan sebelum Start, sama seperti menu yang mengatur director sebelum Play.
        void Configure(Scene loaded, LoadSceneMode mode)
        {
            director = Object.FindFirstObjectByType<CandyTableDirector>();
            if (director != null) Call("Configure", director, shot);
        }

        void AssertOutput()
        {
            Assert.IsFalse(Recording);
            Assert.AreEqual(0, Time.captureFramerate, "captureFramerate harus dikembalikan.");
            int frames = Frames;
            var files = Directory.GetFiles(folder, "frame_*.jpg").Select(Path.GetFileName).OrderBy(f => f).ToArray();
            Assert.Greater(frames, 0);
            Assert.AreEqual(frames, files.Length);
            Assert.AreEqual("frame_000000.jpg", files[0]);
            Assert.AreEqual($"frame_{frames - 1:D6}.jpg", files[files.Length - 1]);
            var image = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(image.LoadImage(File.ReadAllBytes(Path.Combine(folder, files[files.Length - 1]))));
                Assert.AreEqual(1920, image.width);
                Assert.AreEqual(1080, image.height);
                var pixels = image.GetPixels32();
                int colors = Enumerable.Range(0, 400).Select(i => pixels[i * (pixels.Length / 400)])
                    .Select(c => (c.r / 16, c.g / 16, c.b / 16)).Distinct().Count();
                Assert.Greater(colors, 10, "Frame kosong/satu warna.");
            }
            finally { Object.Destroy(image); }
            string name = Path.GetFileName(folder) + ".mp4";
            StringAssert.Contains($"ffmpeg -y -framerate 30 -i frame_%%06d.jpg", File.ReadAllText(Path.Combine(folder, "make_video.cmd")));
            StringAssert.Contains(name, File.ReadAllText(Path.Combine(folder, "make_video.cmd")));
            StringAssert.Contains($"ffmpeg -y -framerate 30 -i frame_%06d.jpg", File.ReadAllText(Path.Combine(folder, "make_video.sh")));
        }

        static object Call(string method, params object[] args) =>
            Recorder.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
    }
}
#endif
