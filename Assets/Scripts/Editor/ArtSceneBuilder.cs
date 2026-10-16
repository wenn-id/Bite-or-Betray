using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BiteOrBetray.EditorTools
{
    [InitializeOnLoad]
    public static class ArtSceneBuilder
    {
        const string Art = "Assets/BiteOrBetray/Art3D";
        const string ScenePath = "Assets/BiteOrBetray/Scenes/CandyParlour3D.unity";
        const string RequestPath = "Temp/BoBArtBuild.request";
        const string ResultPath = "Temp/BoBArtBuild.result.json";
        static readonly string[] ItemNames = { "PlasticFork", "HammerCrush", "HotMilk", "MagicGloves", "Cupcake", "LittlePocket", "GrapplingHook", "CloverCoin" };
        static readonly string[] BotColors = { "Yellow", "Blue", "Red", "Mint", "Peach" };
        static readonly string[] CharacterModels = BotColors.Select(c => "Bot_" + c).Append("Waiter").ToArray();
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static double lastCheck;

        static ArtSceneBuilder()
        {
            EditorApplication.update += CheckRequest;
            EditorApplication.playModeStateChanged += ConfigureDesktopPlay;
        }

        static void ConfigureDesktopPlay(PlayModeStateChange state)
        {
            const string key = "BoB.ArtPreview.XRInit";
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings == null) return;
            if (state == PlayModeStateChange.ExitingEditMode && EditorSceneManager.GetActiveScene().path == ScenePath)
                Debug.Log(PlayInVR ? "[Bite or Betray] Play mode VR" + (SimulatorActive ? " (Meta XR Simulator aktif)" : "") + "."
                    : "[Bite or Betray] Play mode desktop. Untuk VR: nyalakan Meta XR Simulator atau centang Bite or Betray > Play Pakai VR.");
            if (state == PlayModeStateChange.ExitingEditMode && EditorSceneManager.GetActiveScene().path == ScenePath && !PlayInVR)
            {
                SessionState.SetInt(key, settings.InitManagerOnStart ? 1 : 0);
                settings.InitManagerOnStart = false;
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(key, -1) >= 0)
            {
                settings.InitManagerOnStart = SessionState.GetInt(key, 0) == 1;
                SessionState.EraseInt(key);
            }
        }

        const string PlayInVRKey = "BoB.PlayInVR";
        const string PlayInVRMenu = "Bite or Betray/Play Pakai VR (Link atau Simulator)";

        // Default: Play di CandyParlour3D tanpa XR (preview desktop). Centang menu ini untuk tes dengan headset
        // lewat Quest Link atau Meta XR Simulator; kalau headset tidak tersambung, meja tetap jatuh ke mode mouse.
        static bool PlayInVR => EditorPrefs.GetBool(PlayInVRKey) || SimulatorActive;

        // Meta XR Simulator yang aktif menandai dirinya di XR_SELECTED_RUNTIME_JSON (cek yang sama dipakai SDK Meta).
        // Kalau aktif, anggap pemain mau tes VR.
        static bool SimulatorActive =>
            (Environment.GetEnvironmentVariable("XR_SELECTED_RUNTIME_JSON") ?? "").IndexOf("MetaXRSimulator", StringComparison.OrdinalIgnoreCase) >= 0;

        [MenuItem(PlayInVRMenu)]
        static void TogglePlayInVR()
        {
            bool on = !EditorPrefs.GetBool(PlayInVRKey);
            EditorPrefs.SetBool(PlayInVRKey, on);
            Debug.Log(on ? "[Bite or Betray] Play pakai VR: nyalakan Quest Link atau Meta XR Simulator, lalu tekan Play."
                : "[Bite or Betray] Play kembali ke preview desktop (mouse + keyboard).");
        }

        [MenuItem(PlayInVRMenu, true)]
        static bool TogglePlayInVRValidate()
        {
            Menu.SetChecked(PlayInVRMenu, EditorPrefs.GetBool(PlayInVRKey));
            return true;
        }

        static void CheckRequest()
        {
            if (EditorApplication.timeSinceStartup - lastCheck < 1) return;
            lastCheck = EditorApplication.timeSinceStartup;
            if (File.Exists("Temp/BoBArtInspect.request"))
            {
                File.Delete("Temp/BoBArtInspect.request");
                InspectPreview();
            }
            if (File.Exists("Temp/BoBAnimSetup.request") && !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/BoBAnimSetup.request");
                AttachCharacterAnimators();
            }
            if (File.Exists("Temp/BoBDirectorSetup.request") && !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/BoBDirectorSetup.request");
                AttachTableDirector();
            }
            if (File.Exists(CharactersRequest) && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(CharactersRequest);
                try { RefreshCharacters(); }
                catch (Exception error)
                {
                    File.WriteAllText(CharactersResult, JsonUtility.ToJson(new BuildResult { success = false, message = error.ToString() }, true));
                    Debug.LogException(error);
                }
            }
            if (File.Exists("Temp/BoBPoses.request") && !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete("Temp/BoBPoses.request");
                RenderCharacterPoses();
            }
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Models/Waiter.fbx") == null) return;
            File.Delete(RequestPath);
            try { Build(); }
            catch (Exception error)
            {
                File.WriteAllText(ResultPath, JsonUtility.ToJson(new BuildResult { success = false, message = error.ToString() }, true));
                Debug.LogException(error);
            }
        }

        [Serializable]
        class BuildResult { public bool success; public string message; public int models; public int triangles; public int renderers; }

        [Serializable]
        class PreviewState { public bool playing, enabled, keyboard, mouse; public float time; public Vector3 camera; public int heads; }

        [MenuItem("Bite or Betray/Cek Preview Desktop")]
        static void InspectPreview()
        {
            var preview = GameObject.Find("Desktop Art Preview")?.GetComponent<ArtPreviewControls>();
            if (preview == null) throw new InvalidOperationException("Buka CandyParlour3D dulu.");
            File.WriteAllText("Temp/BoBArtInspect.json", JsonUtility.ToJson(new PreviewState {
                playing = EditorApplication.isPlaying, enabled = preview.isActiveAndEnabled,
                keyboard = UnityEngine.InputSystem.Keyboard.current != null,
                mouse = UnityEngine.InputSystem.Mouse.current != null, time = Time.time,
                camera = preview.transform.position,
                heads = preview.artRoot.GetComponentsInChildren<Transform>().Count(t => t.name == "Head" || t.name.StartsWith("Head."))
            }, true));
        }

        const string DirectorName = "BoB_TableDirector";

        static void CreateTableDirector(Transform root)
        {
            // Referensi aset ikut tersimpan supaya shader Unlit tersedia saat build.
            const string materials = "Assets/BiteOrBetray/Materials/";
            var director = new GameObject(DirectorName).AddComponent<CandyTableDirector>();
            director.artRoot = root;
            director.cardMaterial = AssetDatabase.LoadAssetAtPath<Material>(materials + "BoB_TextCard.mat");
            director.gameObject.AddComponent<VRTableInput>().pointerMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(materials + "BoB_VRPointer.mat");
        }

        [MenuItem("Bite or Betray/Pasang Autoplay Meja 3D")]
        static void AttachTableDirector()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var root = GameObject.Find("BoB_Art3D");
            if (root == null) throw new InvalidOperationException("Buka CandyParlour3D dulu.");
            bool wasDirty = scene.isDirty;
            bool added = scene.GetRootGameObjects().All(g => g.GetComponentInChildren<CandyTableDirector>(true) == null);
            if (added)
            {
                CreateTableDirector(root.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            }
            File.WriteAllText("Temp/BoBDirectorSetup.result.json", JsonUtility.ToJson(new BuildResult {
                success = true, message = scene.path + (added && wasDirty ? " (belum disimpan)" : ""), models = added ? 1 : 0 }, true));
            Debug.Log(added ? "[Bite or Betray] Autoplay meja 3D dipasang. Tekan Play untuk menonton bot bermain."
                : "[Bite or Betray] Autoplay meja 3D sudah ada di scene.");
        }

        [MenuItem("Bite or Betray/Pasang Animasi Karakter")]
        static void AttachCharacterAnimators()
        {
            var scene = EditorSceneManager.GetActiveScene();
            bool wasDirty = scene.isDirty;
            int added = CharacterAnimator.AttachAll(scene);
            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            }
            File.WriteAllText("Temp/BoBAnimSetup.result.json", JsonUtility.ToJson(new BuildResult {
                success = true, message = scene.path + (added > 0 && wasDirty ? " (belum disimpan)" : ""), models = added }, true));
            int total = scene.GetRootGameObjects().Sum(g => g.GetComponentsInChildren<CharacterAnimator>(true).Length);
            Debug.Log($"[Bite or Betray] CharacterAnimator dipasang di {added} karakter baru ({total} total di scene).");
        }

        const string CharactersRequest = "Temp/BoBCharacters.request";
        const string CharactersResult = "Temp/BoBCharacters.result.json";

        /// <summary>Ganti karakter di scene dengan FBX terbaru (dari Tools/Blender/bob_characters.py) tanpa
        /// membangun ulang ruangan dan lightmap.</summary>
        [MenuItem("Bite or Betray/Perbarui Karakter 3D")]
        public static void RefreshCharacters()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath);
            }
            var root = GameObject.Find("BoB_Art3D");
            if (root == null) throw new InvalidOperationException("BoB_Art3D tidak ditemukan. Bangun scene 3D dulu.");
            AssetDatabase.Refresh();
            PrepareMaterials();
            RemapModelMaterials(CharacterModels);
            int replaced = 0;
            foreach (string name in CharacterModels)
            {
                var old = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
                if (old == null) continue;
                var parent = old.parent;
                var position = old.localPosition;
                int sibling = old.GetSiblingIndex();
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                var obj = Place(name, parent, position);
                obj.transform.SetSiblingIndex(sibling);
                if (name == "Waiter")
                    obj.transform.localRotation = Quaternion.LookRotation(-obj.transform.position) * obj.transform.localRotation;
                obj.AddComponent<CharacterAnimator>();
                replaced++;
            }
            GraphicsPolish.ConfigureMaterials(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            int triangles = Validate(root);
            File.WriteAllText(CharactersResult, JsonUtility.ToJson(new BuildResult {
                success = true, message = ScenePath, models = replaced, triangles = triangles,
                renderers = root.GetComponentsInChildren<Renderer>().Length }, true));
            Debug.Log($"[Bite or Betray] {replaced} karakter diperbarui dari FBX terbaru.");
            EditorApplication.delayCall += () => { RenderPreview(); RenderCharacterPoses(); };
        }

        /// <summary>Render reaksi karakter tanpa Play mode ke Temp/BoBPose_*.png dan close-up ke Temp/BoBFace_*.png.</summary>
        [MenuItem("Bite or Betray/Render Pose Karakter")]
        internal static void RenderCharacterPoses()
        {
            var go = GameObject.Find("Desktop Art Preview");
            if (go == null) throw new InvalidOperationException("Buka CandyParlour3D dulu.");
            var camera = go.GetComponent<Camera>();
            var characters = UnityEngine.Object.FindObjectsByType<CharacterAnimator>(FindObjectsSortMode.InstanceID);
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float aspect = camera.aspect;
            // Kamera render berada di kursi player: sembunyikan kepala avatar lokal seperti saat bermain.
            var localHeads = UnityEngine.Object.FindObjectsByType<LocalAvatarTracking>(FindObjectsInactive.Exclude)
                .SelectMany(t => t.avatar.headRenderers).Where(r => r != null && !r.forceRenderingOff).ToArray();
            foreach (var r in localHeads) r.forceRenderingOff = true;
            var states = new (string label, CharacterReaction? reaction, float time, bool think, bool paralyze, bool defeat)[] {
                ("Neutral", null, 0, false, false, false), ("Eat", CharacterReaction.Eat, .75f, false, false, false),
                ("Chew", CharacterReaction.Eat, 1.2f, false, false, false), ("Laugh", CharacterReaction.Laugh, .9f, false, false, false),
                ("Shocked", CharacterReaction.Shocked, .45f, false, false, false), ("Yuck", CharacterReaction.Yuck, .8f, false, false, false),
                ("Wave", CharacterReaction.Wave, 1.0f, false, false, false), ("Give", CharacterReaction.Give, .8f, false, false, false),
                ("Think", null, .1f, true, false, false), ("Defeated", null, .1f, false, false, true)
            };
            try
            {
                camera.aspect = 1.6f;
                foreach (var s in states)
                {
                    foreach (var c in characters) c.EditorPose(s.reaction, s.time, s.think, s.paralyze, s.defeat);
                    camera.transform.SetPositionAndRotation(new Vector3(0, 1.15f, -1.2f), Quaternion.LookRotation(new Vector3(0, -.12f, 1.5f)));
                    CaptureCamera(camera, $"Temp/BoBPose_{s.label}.png", 1600, 1000);
                }
                camera.aspect = 1;
                foreach (var c in characters)
                {
                    c.EditorPose(null, 0);
                    var head = c.Head;
                    Vector3 toward = c.name == "Waiter" ? -c.transform.position : -c.transform.parent.position;
                    toward = Vector3.ProjectOnPlane(toward, Vector3.up).normalized;
                    Vector3 eye = head.position + toward * .75f + Vector3.Cross(Vector3.up, toward) * .25f + Vector3.up * .04f;
                    camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(head.position + Vector3.down * .05f - eye));
                    CaptureCamera(camera, $"Temp/BoBFace_{c.name}.png", 700, 700);
                }
            }
            finally
            {
                foreach (var c in characters) c.EditorRestore();
                foreach (var r in localHeads) r.forceRenderingOff = false;
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.aspect = aspect;
            }
            File.WriteAllText("Temp/BoBPoses.result.json", "{\"characters\":" + characters.Length + "}");
            Debug.Log($"[Bite or Betray] Pose {characters.Length} karakter dirender ke Temp/BoBPose_*.png.");
        }

        static void CaptureCamera(Camera camera, string path, int width, int height)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [MenuItem("Bite or Betray/Buka Scene 3D Candy Parlour")]
        static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Bite or Betray/Bangun Scene 3D Candy Parlour")]
        public static void Build()
        {
            PrepareMaterials();
            string[] models = new[] { "Room", "Table", "Chair", "Dispenser", "CandyTray", "ItemMat", "Candy_Yellow", "Candy_Blue", "Candy_Red", "Dice", "Waiter" }
                .Concat(ItemNames).Concat(BotColors.Select(c => "Bot_" + c)).ToArray();
            foreach (string name in models)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Models/" + name + ".fbx") == null)
                    throw new InvalidOperationException("Model belum siap: " + name);
            RemapModelMaterials(models);

            var current = EditorSceneManager.GetActiveScene();
            if (current.isDirty)
                EditorSceneManager.SaveScene(current, "Assets/BiteOrBetray/Scenes/Before3D_WorkingCopy.unity", true);
            if (!File.Exists(ScenePath) && !AssetDatabase.CopyAsset("Assets/BiteOrBetray/Scenes/VR_Sandbox.unity", ScenePath))
                throw new IOException("Tidak bisa menyalin scene sandbox.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "BoB_Art3D" || go.name == "Desktop Art Preview" || go.name == DirectorName) UnityEngine.Object.DestroyImmediate(go);
                else if (go.name == "BoB_Table" || go.name.Contains("Camera Rig")) go.SetActive(false);
            }
            var root = new GameObject("BoB_Art3D");
            var room = Place("Room", root.transform, Vector3.zero);
            room.transform.localRotation = Quaternion.Euler(0, 180, 0) * room.transform.localRotation;
            foreach (var mesh in room.GetComponentsInChildren<MeshFilter>())
                if (!mesh.name.Contains("Glass") && mesh.sharedMesh != null) mesh.gameObject.AddComponent<MeshCollider>();
            var table = Place("Table", root.transform, Vector3.zero);
            foreach (var mesh in table.GetComponentsInChildren<MeshFilter>())
                if (mesh.sharedMesh != null) mesh.gameObject.AddComponent<MeshCollider>();
            var dispenser = Place("Dispenser", root.transform, new Vector3(0, .759f, .08f));
            dispenser.transform.localRotation = Quaternion.Euler(0, 180, 0) * dispenser.transform.localRotation;
            var spawn = new GameObject("CandySpawnPoint");
            spawn.transform.SetParent(dispenser.transform, false);
            spawn.transform.position = dispenser.transform.position + new Vector3(0, .15f, -.06f);
            Place("CandyTray", root.transform, new Vector3(0, .759f, -.28f));
            Place("Candy_Yellow", root.transform, new Vector3(0, .812f, -.28f));
            var seats = new GameObject("Seats");
            seats.transform.SetParent(root.transform, false);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0, -Mathf.Cos(angle));
                var seat = new GameObject("Seat_" + i);
                seat.transform.SetParent(seats.transform, false);
                seat.transform.localPosition = direction * VRTableBuilder.SeatRadius;
                seat.transform.rotation = Quaternion.LookRotation(-direction);
                Place("Chair", seat.transform, Vector3.zero);
                if (i > 0) Place("Bot_" + BotColors[i - 1], seat.transform, Vector3.zero).AddComponent<CharacterAnimator>();
                var anchor = new GameObject("HeadAnchor");
                anchor.transform.SetParent(seat.transform, false);
                anchor.transform.localPosition = Vector3.up * VRTableBuilder.SeatedEyeHeight;
                var mat = Place("ItemMat", root.transform, direction * .59f + Vector3.up * .759f);
                mat.transform.localRotation = Quaternion.LookRotation(-direction) * mat.transform.localRotation;
            }
            var waiter = Place("Waiter", root.transform, new Vector3(1.9f, 0, 2.0f));
            waiter.transform.localRotation = Quaternion.LookRotation(-waiter.transform.position) * waiter.transform.localRotation;
            waiter.AddComponent<CharacterAnimator>().bodyPivotHeight = .6f;
            var items = new GameObject("Item Models");
            items.transform.SetParent(root.transform, false);
            for (int i = 0; i < ItemNames.Length; i++)
            {
                float angle = (i + 1) * Mathf.PI * 2 / 10;
                var prop = Place(ItemNames[i], items.transform, new Vector3(.5f * Mathf.Sin(angle), .773f, -.5f * Mathf.Cos(angle)));
                prop.transform.localRotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0) * prop.transform.localRotation;
                if (ItemNames[i] == "PlasticFork" || ItemNames[i] == "HammerCrush" || ItemNames[i] == "GrapplingHook")
                {
                    prop.transform.Rotate(90, 0, 0, Space.Self);
                    prop.transform.position += Vector3.up * .03f;
                }
                var collider = prop.AddComponent<BoxCollider>();
                Bounds bounds = LocalMeshBounds(prop);
                collider.center = bounds.center;
                collider.size = bounds.size;
            }
            Place("Dice", root.transform, new Vector3(-.32f, .773f, -.43f));
            Place("Candy_Blue", root.transform, new Vector3(-.08f, .79f, -.57f));
            Place("Candy_Red", root.transform, new Vector3(.08f, .79f, -.57f));

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.65f, .72f, .78f);
            RenderSettings.ambientEquatorColor = new Color(.65f, .60f, .53f);
            RenderSettings.ambientGroundColor = new Color(.40f, .36f, .30f);
            RenderSettings.fog = false;
            var sun = scene.GetRootGameObjects().Select(g => g.GetComponent<Light>()).FirstOrDefault(l => l != null);
            if (sun != null)
            {
                sun.color = new Color(1, .88f, .74f);
                sun.intensity = 1.3f;
                sun.transform.rotation = Quaternion.Euler(50, -35, 0);
                sun.shadows = LightShadows.Soft;
                sun.GetUniversalAdditionalLightData().usePipelineSettings = false;
                sun.shadowBias = 1;
                sun.shadowNormalBias = .8f;
                RenderSettings.sun = sun;
            }
            FillLight(root.transform, "Shop Fill", new Vector3(0, 2.8f, 1.7f), new Color(1, .79f, .62f), 1.3f, 5);
            FillLight(root.transform, "Window Fill", new Vector3(-2.8f, 2.2f, -.6f), new Color(.67f, .85f, 1), 1.1f, 5);
            var preview = new GameObject("Desktop Art Preview");
            preview.tag = "MainCamera";
            preview.transform.SetPositionAndRotation(new Vector3(2.7f, 2.2f, -3.2f), Quaternion.LookRotation(new Vector3(-2.7f, -1.1f, 3.9f)));
            var camera = preview.AddComponent<Camera>();
            camera.nearClipPlane = .025f;
            camera.farClipPlane = 30;
            camera.fieldOfView = 65;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.75f, .83f, .84f);
            preview.AddComponent<AudioListener>();
            preview.AddComponent<ArtPreviewControls>().artRoot = root.transform;
            CreateTableDirector(root.transform);
            int triangles = Validate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0, 1, 0), Quaternion.Euler(25, -25, 0), 5);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            AssetDatabase.SaveAssets();
            File.WriteAllText(ResultPath, JsonUtility.ToJson(new BuildResult {
                success = true, message = ScenePath, models = models.Length, triangles = triangles,
                renderers = root.GetComponentsInChildren<Renderer>().Length }, true));
            Debug.Log("[Bite or Betray] Scene 3D siap. Play untuk preview; Space ganti kamera, mouse kanan + WASD untuk melihat ruangan, angka 0-9 / T / P / K untuk animasi karakter.");
            EditorApplication.delayCall += RenderPreview;
        }

        static void PrepareMaterials()
        {
            Materials.Clear();
            if (!AssetDatabase.IsValidFolder(Art + "/Materials")) AssetDatabase.CreateFolder(Art, "Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Shader URP Lit tidak ditemukan.");
            string manifest = File.ReadAllText(Art + "/art_manifest.json");
            foreach (Match match in Regex.Matches(manifest, "\"([^\"]+)\":\\s*\"(#[0-9a-fA-F]{6})\""))
            {
                string name = "BoB3D_" + match.Groups[1].Value;
                ColorUtility.TryParseHtmlString(match.Groups[2].Value, out Color color);
                string path = Art + "/Materials/" + name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader) { name = name }; AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", name.Contains("Gold") ? .5f : .25f);
                mat.SetFloat("_Metallic", name.Contains("Gold") ? .45f : 0);
                mat.enableInstancing = true;
                if (name == "BoB3D_Glass")
                {
                    color.a = .16f;
                    mat.SetColor("_BaseColor", color);
                    mat.SetFloat("_Surface", 1);
                    mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0);
                    mat.SetFloat("_Cull", (float)CullMode.Off);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                }
                if (name == "BoB3D_Glow")
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * 1.5f);
                }
                EditorUtility.SetDirty(mat);
                Materials[name] = mat;
            }
        }

        static GameObject Place(string name, Transform parent, Vector3 position)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Models/" + name + ".fbx");
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            obj.name = name;
            obj.transform.localPosition = position;
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => {
                    if (m == null || !Materials.TryGetValue(m.name, out var replacement))
                        throw new InvalidOperationException("Material model tidak dikenal: " + name + "/" + m?.name);
                    return replacement;
                }).ToArray();
                renderer.gameObject.isStatic = name == "Room" || name == "Table" || name == "Chair";
            }
            return obj;
        }

        static void RemapModelMaterials(IEnumerable<string> models)
        {
            foreach (string name in models)
            {
                string path = Art + "/Models/" + name + ".fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                bool changed = false;
                var external = importer.GetExternalObjectMap();
                foreach (var original in model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct())
                {
                    if (original == null || !Materials.TryGetValue(original.name, out var replacement))
                        throw new InvalidOperationException("Material FBX tidak dikenal: " + name);
                    var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), original.name);
                    if (!external.TryGetValue(identifier, out var mapped) || mapped != replacement)
                    {
                        importer.AddRemap(identifier, replacement);
                        changed = true;
                    }
                }
                if (changed) importer.SaveAndReimport();
            }
        }

        static void FillLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        static Bounds RendererBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [MenuItem("Bite or Betray/Render Preview Scene 3D")]
        static void RenderPreview()
        {
            var go = GameObject.Find("Desktop Art Preview");
            if (go == null) throw new InvalidOperationException("Buka CandyParlour3D dulu.");
            var camera = go.GetComponent<Camera>();
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float aspect = camera.aspect;
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1600, 1000, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(1600, 1000, TextureFormat.RGBA32, false);
            try
            {
                camera.aspect = 1.6f;
                Capture("Temp/BoBArtPreview_Unity.png");
                camera.transform.SetPositionAndRotation(new Vector3(0, 1.15f, -1.2f),
                    Quaternion.LookRotation(new Vector3(0, -.12f, 1.5f)));
                Capture("Temp/BoBArtSeated_Unity.png");
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.aspect = aspect;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }

            void Capture(string path)
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
        }

        static Bounds LocalMeshBounds(GameObject obj)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var mesh in obj.GetComponentsInChildren<MeshFilter>())
            {
                Bounds source = mesh.sharedMesh.bounds;
                Matrix4x4 matrix = obj.transform.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(source.center + Vector3.Scale(source.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        [MenuItem("Bite or Betray/Cek Aset Scene 3D")]
        static void ValidateMenu()
        {
            var root = GameObject.Find("BoB_Art3D");
            if (root == null) throw new InvalidOperationException("Buka CandyParlour3D dulu.");
            Debug.Log("[Bite or Betray] Validasi lulus, " + Validate(root) + " triangles.");
        }

        static int Validate(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0 || renderers.Any(r => r.sharedMaterials.Any(m => m == null || m.shader.name != "Universal Render Pipeline/Lit")))
                throw new InvalidOperationException("Ada mesh atau material yang belum siap.");
            if (root.transform.Find("Seats").childCount != 6 || root.transform.Find("Item Models").childCount != 8)
                throw new InvalidOperationException("Jumlah kursi atau item tidak lengkap.");
            float tableHeight = RendererBounds(root.transform.Find("Table").gameObject).max.y;
            if (tableHeight < .70f || tableHeight > .82f)
                throw new InvalidOperationException("Skala FBX meja tidak dalam meter: " + tableHeight);
            Bounds room = RendererBounds(root.transform.Find("Room").gameObject);
            if (room.size.y < 3.5f || room.size.y > 4.2f || room.size.z < 7)
                throw new InvalidOperationException("Sumbu FBX ruangan tidak benar: " + room.size);
            if (RendererBounds(root.transform.Find("Waiter").gameObject).size.y < 1.2f)
                throw new InvalidOperationException("Orientasi model pelayan tidak tegak.");
            if (root.GetComponentsInChildren<BoxCollider>().Any(c => c.size.x <= 0 || c.size.y <= 0 || c.size.z <= 0))
                throw new InvalidOperationException("Ada collider dengan ukuran tidak valid.");
            return root.GetComponentsInChildren<MeshFilter>().Sum(m =>
                Enumerable.Range(0, m.sharedMesh.subMeshCount).Sum(i => (int)m.sharedMesh.GetIndexCount(i) / 3));
        }
    }
}
