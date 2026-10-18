using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BiteOrBetray.Art3D;
using BiteOrBetray.Core;
using BiteOrBetray.Table3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BiteOrBetray.EditorTools
{
    /// <summary>
    /// Rekam video Devpost dari Editor tanpa headset (BOB-045). Satu klik membuka CandyParlour3D, menjalankan Play desktop,
    /// menggerakkan kamera preview tiap frame lalu menyimpan JPG 1920x1080 ke Recordings/ (di luar Assets) dengan
    /// Time.captureFramerate 30, jadi video tetap 30 fps walaupun render lebih lambat. Selesai: Play berhenti sendiri,
    /// buat_video.cmd/.sh berisi perintah ffmpeg ke MP4 dan perintahnya disalin ke clipboard. Audio tidak ikut direkam.
    /// Pengaturan director diubah di memori saja dan dikembalikan setelah Play; scene tidak disimpan.
    /// </summary>
    [InitializeOnLoad]
    public static class GameplayRecorder
    {
        public enum Shot { Pertandingan, Menu, CloseUpBot, MejaAtas }

        const string Menu = "Bite or Betray/Rekam Video/";
        const string ShotKey = "BoB.Recorder.Shot", FolderKey = "BoB.Recorder.Folder", BackupKey = "BoB.Recorder.Director";
        public const int Width = 1920, Height = 1080, Fps = 30;
        // Batas aman kalau pertandingan tidak pernah selesai (detik waktu game).
        const float MaxSeconds = 20 * 60;
        static readonly PropertyInfo StateProperty = typeof(CandyTableDirector).GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic);

        static Shot shot;
        static string folder;
        static bool exitWhenDone, oldMute;
        static int oldCaptureFramerate;
        static float elapsed, overAt;
        static CandyTableDirector director;
        static Camera camera;
        static Vector3 seat, center;
        static Transform lastActor;
        static RenderTexture target;
        static Texture2D texture;

        public static bool Recording { get; private set; }
        public static int Frames { get; private set; }
        public static string Folder => folder;

        static GameplayRecorder()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem(Menu + "Pertandingan dari Kursi Pemain (1 klik)")]
        static void RecordMatch() => Record(Shot.Pertandingan);

        [MenuItem(Menu + "Shot Menu Utama")]
        static void RecordMenu() => Record(Shot.Menu);

        [MenuItem(Menu + "Shot Close-up Bot Bereaksi")]
        static void RecordCloseUp() => Record(Shot.CloseUpBot);

        [MenuItem(Menu + "Shot Meja dari Atas")]
        static void RecordTopDown() => Record(Shot.MejaAtas);

        [MenuItem(Menu + "Pertandingan dari Kursi Pemain (1 klik)", true)]
        [MenuItem(Menu + "Shot Menu Utama", true)]
        [MenuItem(Menu + "Shot Close-up Bot Bereaksi", true)]
        [MenuItem(Menu + "Shot Meja dari Atas", true)]
        static bool CanRecord() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Menu + "Buka Folder Rekaman")]
        static void OpenFolder()
        {
            string root = Path.Combine(ProjectRoot, "Recordings");
            Directory.CreateDirectory(root);
            EditorUtility.RevealInFinder(root);
        }

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        static void Record(Shot next)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Hentikan Play dulu.");
            if (ArtSceneBuilder.PlayInVR)
                throw new InvalidOperationException("Matikan Bite or Betray > Play Pakai VR dan Meta XR Simulator dulu: rekaman memakai kamera preview desktop.");
            if (EditorSceneManager.GetActiveScene().path != ArtSceneBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ArtSceneBuilder.ScenePath);
            }
            var d = UnityEngine.Object.FindFirstObjectByType<CandyTableDirector>();
            if (d == null) throw new InvalidOperationException("CandyTableDirector tidak ada di scene. Jalankan Pasang Autoplay Meja 3D dulu.");
            // Perubahan di memori ikut masuk Play; setelah Play nilai asli dipulihkan dari salinan ini.
            SessionState.SetString(BackupKey, EditorJsonUtility.ToJson(d));
            Configure(d, next.ToString());
            string path = Path.Combine(ProjectRoot, "Recordings", $"{DateTime.Now:yyyyMMdd_HHmmss}_{next}");
            SessionState.SetString(ShotKey, next.ToString());
            SessionState.SetString(FolderKey, path);
            Debug.Log($"[Bite or Betray] Rekam {next} {Width}x{Height} {Fps} fps ke {path}. Play berhenti sendiri setelah selesai.");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// Atur director sebelum Start: Menu memakai kursi manusia dan menu utama, shot lain semua bot bermain sendiri
        /// (kursi pemain kosong, kamera di sana). Pertandingan hanya satu game; telemetry rekaman tidak dicatat.
        /// </summary>
        public static void Configure(CandyTableDirector d, string shotName)
        {
            var s = (Shot)Enum.Parse(typeof(Shot), shotName);
            bool menu = s == Shot.Menu;
            d.autoStart = true;
            d.humanPlayer = menu;
            d.mainMenu = menu;
            if (!menu) d.roomSetup = d.onboarding = false;
            d.loop = s != Shot.Pertandingan;
            d.recordTelemetry = false;
            d.showLog = false;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                string requested = SessionState.GetString(ShotKey, "");
                if (requested == "") return;
                SessionState.EraseString(ShotKey);
                try { Begin(requested, SessionState.GetString(FolderKey, ""), true); }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    EditorApplication.ExitPlaymode();
                }
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Stop();
                SetTick(false);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                string backup = SessionState.GetString(BackupKey, "");
                var d = UnityEngine.Object.FindFirstObjectByType<CandyTableDirector>();
                if (backup != "" && d != null) EditorJsonUtility.FromJsonOverwrite(backup, d);
                SessionState.EraseString(BackupKey);
                string path = SessionState.GetString(FolderKey, "");
                SessionState.EraseString(FolderKey);
                if (path != "" && Directory.Exists(path)) EditorUtility.RevealInFinder(Path.Combine(path, "buat_video.cmd"));
            }
        }

        /// <summary>
        /// Mulai merekam di Play yang sedang berjalan (director sudah diatur lewat Configure). exitPlayMode: hentikan Play
        /// saat selesai (menu); tes PlayMode memanggil Stop sendiri.
        /// </summary>
        public static void Begin(string shotName, string outputFolder, bool exitPlayMode)
        {
            if (Recording) throw new InvalidOperationException("Rekaman lain masih berjalan.");
            if (VRTableInput.XRActive) throw new InvalidOperationException("XR aktif; rekaman butuh Play desktop tanpa headset.");
            director = UnityEngine.Object.FindFirstObjectByType<CandyTableDirector>();
            camera = Camera.main;
            if (director == null || camera == null) throw new InvalidOperationException("Rekaman butuh CandyTableDirector dan kamera preview desktop (Camera.main).");
            var root = director.artRoot != null ? director.artRoot : GameObject.Find("BoB_Art3D")?.transform;
            var seat0 = root != null ? root.Find("Seats/Seat_0") : null;
            if (seat0 == null) throw new InvalidOperationException("BoB_Art3D/Seats/Seat_0 tidak ditemukan.");
            // Sama dengan PlaceDesktopCamera director: mata di HeadAnchor kursi pemain, pusat meja di dispenser.
            seat = (seat0.Find("HeadAnchor") ?? seat0).position;
            var dispenser = root.Find("Dispenser");
            center = dispenser != null ? dispenser.position : root.position + Vector3.up * .76f;

            shot = (Shot)Enum.Parse(typeof(Shot), shotName);
            folder = outputFolder;
            Directory.CreateDirectory(folder);
            exitWhenDone = exitPlayMode;
            elapsed = 0;
            overAt = -1;
            lastActor = null;
            Frames = 0;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            oldCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = Fps;
            // Waktu game diperlambat captureFramerate, jadi suaranya tidak cocok; bisukan selama merekam.
            oldMute = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = true;
            Recording = true;
            SetTick(true);
            Pose(snap: true);
        }

        /// <summary>Hentikan rekaman (juga saat Play dihentikan manual) dan tulis skrip ffmpeg untuk frame yang sudah ada.</summary>
        public static void Stop()
        {
            if (!Recording) return;
            Recording = false;
            Time.captureFramerate = oldCaptureFramerate;
            EditorUtility.audioMasterMute = oldMute;
            if (camera != null) camera.ResetAspect();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
            director = null;
            camera = null;
            string command = WriteScripts();
            EditorGUIUtility.systemCopyBuffer = command;
            Debug.Log($"[Bite or Betray] Rekaman {shot}: {Frames} frame ({Frames / (float)Fps:0.0} detik) di {folder}.\n"
                + "Jalankan buat_video.cmd (Windows) atau buat_video.sh, atau perintah ini (sudah disalin ke clipboard):\n" + command);
            // Saat Play sedang dihentikan (ExitingPlayMode) isPlayingOrWillChangePlaymode sudah false.
            if (exitWhenDone && EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }

        // Tick ikut player loop (sekali per frame, setelah LateUpdate) supaya tiap frame game tepat satu gambar.
        // Dilepas saat keluar Play, bukan dari dalam Tick; setelah Stop, Tick tidak melakukan apa-apa.
        static void SetTick(bool on)
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < loop.subSystemList.Length; i++)
            {
                if (loop.subSystemList[i].type != typeof(PostLateUpdate)) continue;
                var systems = loop.subSystemList[i].subSystemList.Where(s => s.type != typeof(GameplayRecorder)).ToList();
                if (on) systems.Add(new PlayerLoopSystem { type = typeof(GameplayRecorder), updateDelegate = Tick });
                loop.subSystemList[i].subSystemList = systems.ToArray();
            }
            PlayerLoop.SetPlayerLoop(loop);
        }

        static void Tick()
        {
            if (!Recording) return;
            if (!EditorApplication.isPlaying || camera == null || director == null)
            {
                Stop();
                return;
            }
            elapsed += Time.deltaTime;
            Pose(snap: false);
            Capture();
            var state = StateProperty?.GetValue(director) as ITableState;
            if (state != null && state.IsOver && overAt < 0) overAt = elapsed;
            // Pertandingan: sampai pemenang + 5 detik layar akhir. Preset lain: durasi tetap.
            float duration = shot == Shot.Menu ? 8 : shot == Shot.CloseUpBot ? 45 : shot == Shot.MejaAtas ? 30 : MaxSeconds;
            if ((shot == Shot.Pertandingan && overAt >= 0 && elapsed - overAt >= 5) || elapsed >= duration) Stop();
        }

        static void Pose(bool snap)
        {
            var state = StateProperty?.GetValue(director) as ITableState;
            var actor = state == null ? null : Head(state.IsOver ? state.Winner : state.Current);
            if (actor != null) lastActor = actor;
            float t = elapsed;
            // Goyang napas kecil supaya kamera tidak terasa terkunci tripod.
            var drift = Quaternion.Euler(Mathf.Sin(t * .9f) * .6f, Mathf.Sin(t * .6f) * .8f, 0);
            Vector3 position;
            Quaternion rotation;
            float rate;
            switch (shot)
            {
                case Shot.Menu:
                {
                    // Panel menu ada di tepi meja depan pemain; dorong maju pelan 12 cm.
                    Vector3 panel = Vector3.Lerp(seat, center, .5f);
                    panel.y = center.y + .12f;
                    position = seat + (panel - seat).normalized * .12f * Mathf.SmoothStep(0, 1, t / 8);
                    rotation = Quaternion.LookRotation(panel - position) * drift;
                    rate = 4;
                    break;
                }
                case Shot.CloseUpBot:
                {
                    var head = lastActor != null ? lastActor : Head(null);
                    if (head == null) goto default;
                    Vector3 toCenter = center - head.position;
                    toCenter.y = 0;
                    toCenter.Normalize();
                    position = head.position + toCenter * .7f + Vector3.Cross(Vector3.up, toCenter) * .12f + Vector3.up * .03f;
                    rotation = Quaternion.LookRotation(head.position - Vector3.up * .04f - position);
                    rate = 2.5f;
                    break;
                }
                case Shot.MejaAtas:
                {
                    Vector3 away = seat - center;
                    away.y = 0;
                    position = center + Vector3.up * 1.9f + Quaternion.Euler(0, t * 6, 0) * away.normalized * .6f;
                    rotation = Quaternion.LookRotation(center - position);
                    rate = 4;
                    break;
                }
                default:
                {
                    // Dari kursi pemain ke pusat meja (seperti PlaceDesktopCamera), menoleh sedikit ke bot yang beraksi.
                    Vector3 look = (center + Vector3.up * .25f - seat).normalized;
                    if (actor != null) look = Vector3.Slerp(look, (actor.position - seat).normalized, .35f);
                    position = seat;
                    rotation = Quaternion.LookRotation(look) * drift;
                    rate = 2.2f;
                    break;
                }
            }
            float k = snap ? 1 : 1 - Mathf.Exp(-rate * Time.deltaTime);
            var transform = camera.transform;
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, k), Quaternion.Slerp(transform.rotation, rotation, k));
        }

        /// <summary>Kepala karakter bot pemain ini (nama bot -> warna kursi Bot_*). null: bot pertama yang terlihat.</summary>
        static Transform Head(PlayerState player)
        {
            var colors = ArtSceneBuilder.BotColors;
            for (int i = 0; i < colors.Length; i++)
            {
                string name = director.botNames != null && i < director.botNames.Length && !string.IsNullOrWhiteSpace(director.botNames[i])
                    ? director.botNames[i] : colors[i];
                if (player != null && player.Name != name) continue;
                var character = CharacterAnimator.All.FirstOrDefault(c => c != null && c.name == "Bot_" + colors[i] && c.isActiveAndEnabled);
                if (character != null) return character.Head != null ? character.Head : character.transform;
            }
            return null;
        }

        static void Capture()
        {
            // SingleCameraRequest seperti Render Preview Scene 3D; Game view tetap memakai kamera yang sama.
            camera.aspect = Width / (float)Height;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            camera.ResetAspect();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(folder, $"frame_{Frames:D6}.jpg"), texture.EncodeToJPG(95));
            Frames++;
        }

        /// <summary>Tulis buat_video.cmd/.sh di folder rekaman (MP4 H.264 di sebelah frame). Hasil: perintah dengan path lengkap.</summary>
        static string WriteScripts()
        {
            string name = Path.GetFileName(folder) + ".mp4";
            const string Encode = "-c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -movflags +faststart";
            File.WriteAllText(Path.Combine(folder, "buat_video.cmd"),
                $"@echo off\r\ncd /d \"%~dp0\"\r\nffmpeg -y -framerate {Fps} -i frame_%%06d.jpg {Encode} \"{name}\"\r\n");
            File.WriteAllText(Path.Combine(folder, "buat_video.sh"),
                $"#!/bin/sh\ncd \"$(dirname \"$0\")\"\nffmpeg -y -framerate {Fps} -i frame_%06d.jpg {Encode} \"{name}\"\n");
            return $"ffmpeg -y -framerate {Fps} -i \"{Path.Combine(folder, "frame_%06d.jpg")}\" {Encode} \"{Path.Combine(folder, name)}\"";
        }
    }
}
