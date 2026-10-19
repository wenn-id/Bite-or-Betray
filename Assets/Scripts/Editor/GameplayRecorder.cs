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
    /// Record Devpost video from the Editor without a headset (BOB-045). One click opens CandyParlour3D, starts desktop Play,
    /// moves the preview camera each frame, and saves 1920x1080 JPGs to Recordings/ (outside Assets) with
    /// Time.captureFramerate 30, so video stays at 30 fps even when rendering is slower. On completion, Play stops automatically,
    /// make_video.cmd/.sh contain the ffmpeg MP4 command, and the command is copied to the clipboard. Audio is not recorded.
    /// Director settings change only in memory and are restored after Play; the scene is not saved.
    /// Seat (BOB-052): player seat camera like Match, but the human plays/selects menus and recording
    /// only stops via Stop; used by automated trailer clips (TrailerClipTests) that drive the menus and table.
    /// </summary>
    [InitializeOnLoad]
    public static class GameplayRecorder
    {
        public enum Shot { Match, Menu, CloseUpBot, TopDown, Seat }

        const string Menu = "Bite or Betray/Record Video/";
        const string ShotKey = "BoB.Recorder.Shot", FolderKey = "BoB.Recorder.Folder", BackupKey = "BoB.Recorder.Director";
        public const int Width = 1920, Height = 1080, Fps = 30;
        // Safety limit if the match never finishes (game time seconds).
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

        [MenuItem(Menu + "Match from Player Seat (1 click)")]
        static void RecordMatch() => Record(Shot.Match);

        [MenuItem(Menu + "Main Menu Shot")]
        static void RecordMenu() => Record(Shot.Menu);

        [MenuItem(Menu + "Reacting Bot Close-up Shot")]
        static void RecordCloseUp() => Record(Shot.CloseUpBot);

        [MenuItem(Menu + "Top-down Table Shot")]
        static void RecordTopDown() => Record(Shot.TopDown);

        [MenuItem(Menu + "Match from Player Seat (1 click)", true)]
        [MenuItem(Menu + "Main Menu Shot", true)]
        [MenuItem(Menu + "Reacting Bot Close-up Shot", true)]
        [MenuItem(Menu + "Top-down Table Shot", true)]
        static bool CanRecord() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Menu + "Open Recordings Folder")]
        static void OpenFolder()
        {
            string root = Path.Combine(ProjectRoot, "Recordings");
            Directory.CreateDirectory(root);
            EditorUtility.RevealInFinder(root);
        }

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        static void Record(Shot next)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            if (ArtSceneBuilder.PlayInVR)
                throw new InvalidOperationException("Disable Bite or Betray > Play in VR and Meta XR Simulator first: recording uses the desktop preview camera.");
            if (EditorSceneManager.GetActiveScene().path != ArtSceneBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ArtSceneBuilder.ScenePath);
            }
            var d = UnityEngine.Object.FindFirstObjectByType<CandyTableDirector>();
            if (d == null) throw new InvalidOperationException("CandyTableDirector not found in the scene. Run Install 3D Table Autoplay first.");
            // In-memory changes carry into Play; restore the original values from this copy afterward.
            SessionState.SetString(BackupKey, EditorJsonUtility.ToJson(d));
            Configure(d, next.ToString());
            string path = Path.Combine(ProjectRoot, "Recordings", $"{DateTime.Now:yyyyMMdd_HHmmss}_{next}");
            SessionState.SetString(ShotKey, next.ToString());
            SessionState.SetString(FolderKey, path);
            Debug.Log($"[Bite or Betray] Recording {next} {Width}x{Height} {Fps} fps to {path}. Play stops automatically when finished.");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// Configure the director before Start: Menu and Seat use the human seat and main menu; other shots have all bots play
        /// automatically (the player seat stays empty, with the camera there). Match and Seat run just one game (results stay visible);
        /// recording telemetry is disabled.
        /// </summary>
        public static void Configure(CandyTableDirector d, string shotName)
        {
            var s = (Shot)Enum.Parse(typeof(Shot), shotName);
            bool menu = s == Shot.Menu || s == Shot.Seat;
            d.autoStart = true;
            d.humanPlayer = menu;
            d.mainMenu = menu;
            if (!menu) d.roomSetup = d.onboarding = false;
            d.loop = s != Shot.Match && s != Shot.Seat;
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
                if (path != "" && Directory.Exists(path)) EditorUtility.RevealInFinder(Path.Combine(path, "make_video.cmd"));
            }
        }

        /// <summary>
        /// Start recording in an active Play session (director already set up through Configure). exitPlayMode: stop Play
        /// when finished (menu); PlayMode tests call Stop themselves.
        /// </summary>
        public static void Begin(string shotName, string outputFolder, bool exitPlayMode)
        {
            if (Recording) throw new InvalidOperationException("Another recording is still running.");
            if (VRTableInput.XRActive) throw new InvalidOperationException("XR active; recording needs desktop Play without a headset.");
            director = UnityEngine.Object.FindFirstObjectByType<CandyTableDirector>();
            camera = Camera.main;
            if (director == null || camera == null) throw new InvalidOperationException("Recording needs CandyTableDirector and a desktop preview camera (Camera.main).");
            var root = director.artRoot != null ? director.artRoot : GameObject.Find("BoB_Art3D")?.transform;
            var seat0 = root != null ? root.Find("Seats/Seat_0") : null;
            if (seat0 == null) throw new InvalidOperationException("BoB_Art3D/Seats/Seat_0 not found.");
            // Same as the director's PlaceDesktopCamera: eyes at the player seat's HeadAnchor, table center at the dispenser.
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
            // captureFramerate slows game time, so audio would not match; mute during recording.
            oldMute = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = true;
            Recording = true;
            SetTick(true);
            Pose(snap: true);
        }

        /// <summary>Stop recording (also when Play stops manually) and write ffmpeg scripts for existing frames.</summary>
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
            Debug.Log($"[Bite or Betray] Recorded {shot}: {Frames} frames ({Frames / (float)Fps:0.0} seconds) in {folder}.\n"
                + "Run make_video.cmd (Windows) or make_video.sh, or this command (already copied to the clipboard):\n" + command);
            // When Play is stopping (ExitingPlayMode), isPlayingOrWillChangePlaymode is already false.
            if (exitWhenDone && EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        }

        // Tick follows the player loop (once per frame, after LateUpdate) so each game frame produces exactly one image.
        // Remove when leaving Play, not inside Tick; after Stop, Tick does nothing.
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
            // Match: until a winner + 5 seconds of results. Seat: until Stop (MaxSeconds limit). Other presets: fixed duration.
            float duration = shot == Shot.Menu ? 8 : shot == Shot.CloseUpBot ? 45 : shot == Shot.TopDown ? 30 : MaxSeconds;
            if ((shot == Shot.Match && overAt >= 0 && elapsed - overAt >= 5) || elapsed >= duration) Stop();
        }

        static void Pose(bool snap)
        {
            var state = StateProperty?.GetValue(director) as ITableState;
            var actor = state == null ? null : Head(state.IsOver ? state.Winner : state.Current);
            if (actor != null) lastActor = actor;
            float t = elapsed;
            // Small breathing drift keeps the camera from feeling locked to a tripod.
            var drift = Quaternion.Euler(Mathf.Sin(t * .9f) * .6f, Mathf.Sin(t * .6f) * .8f, 0);
            Vector3 position;
            Quaternion rotation;
            float rate;
            switch (shot)
            {
                case Shot.Menu:
                {
                    // The menu panel is at the table edge in front of the player; slowly push forward 12 cm.
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
                case Shot.TopDown:
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
                    // Match/Seat: from the player's seat toward the table center (like PlaceDesktopCamera), looking slightly toward
                    // the acting bot. Menu/lobby have no state yet, so use the same direction as the menu screenshot.
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

        /// <summary>This player's bot character head (bot name -> Bot_* seat color). null: first visible bot.</summary>
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
            // SingleCameraRequest as in Render Preview Scene 3D; Game view keeps using the same camera.
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

        /// <summary>Write make_video.cmd/.sh in the recording folder (H.264 MP4 beside the frames). Returns the command with full paths.</summary>
        static string WriteScripts()
        {
            string name = Path.GetFileName(folder) + ".mp4";
            const string Encode = "-c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -movflags +faststart";
            File.WriteAllText(Path.Combine(folder, "make_video.cmd"),
                $"@echo off\r\ncd /d \"%~dp0\"\r\nffmpeg -y -framerate {Fps} -i frame_%%06d.jpg {Encode} \"{name}\"\r\n");
            File.WriteAllText(Path.Combine(folder, "make_video.sh"),
                $"#!/bin/sh\ncd \"$(dirname \"$0\")\"\nffmpeg -y -framerate {Fps} -i frame_%06d.jpg {Encode} \"{name}\"\n");
            return $"ffmpeg -y -framerate {Fps} -i \"{Path.Combine(folder, "frame_%06d.jpg")}\" {Encode} \"{Path.Combine(folder, name)}\"";
        }
    }
}
