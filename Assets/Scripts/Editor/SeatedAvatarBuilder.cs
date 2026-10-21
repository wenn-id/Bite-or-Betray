using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiteOrBetray.EditorTools
{
    [InitializeOnLoad]
    public static class SeatedAvatarBuilder
    {
        const string Art = "Assets/BiteOrBetray/Art3D";
        public const string PrefabPath = Art + "/Prefabs/SeatedRemoteAvatar.prefab";
        static double lastCheck;
        // Bots and waiter using full-body Meshy characters, rather than <name>_Float (Cherry from bob_meshy_split.py).
        static readonly Dictionary<string, string> BotModels = new Dictionary<string, string>
        {
            ["Bot_Peach"] = "Cherry", ["Bot_Blue"] = "BlueGentleman", ["Bot_Red"] = "RedGrouch",
            ["Bot_Yellow"] = "LavenderWoman", ["Bot_Mint"] = "GentleGiant", ["Waiter"] = "StripedChef"   // from Tools/Blender/bob_meshy_rigged.py
        };
        static SeatedAvatarBuilder() => EditorApplication.update += CheckRequests;

        static void CheckRequests()
        {
            if (EditorApplication.timeSinceStartup - lastCheck < 1 || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            lastCheck = EditorApplication.timeSinceStartup;
            if (File.Exists("Temp/BoBAvatarRefresh.request"))
            {
                File.Delete("Temp/BoBAvatarRefresh.request");
                Run(() => EditorSceneManager.OpenScene("Assets/BiteOrBetray/Scenes/CandyParlour3D.unity"));
            }
            if (File.Exists("Temp/BoBAvatarInstall.request"))
            {
                File.Delete("Temp/BoBAvatarInstall.request");
                Run(Install);
            }
            if (File.Exists("Temp/BoBAvatarCheck.request"))
            {
                File.Delete("Temp/BoBAvatarCheck.request");
                Run(AvatarValidation.Check);
            }
            if (File.Exists("Temp/BoBAvatarBuild.request"))
            {
                File.Delete("Temp/BoBAvatarBuild.request");
                Run(AvatarValidation.BuildAndroid);
            }
        }

        static void Run(Action action)
        {
            try { action(); }
            catch (Exception exception) { File.WriteAllText("Temp/BoBAvatar.result.txt", exception.ToString()); Debug.LogException(exception); }
        }

        [MenuItem("Bite or Betray/Install Floating Avatar")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("/CandyParlour3D.unity") || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open CandyParlour3D and stop Play first.");
            EditorSceneManager.SaveScene(scene, "Temp/CandyParlour_BeforeAvatar.unity", true);
            var root = GameObject.Find("BoB_Art3D");
            var seat = root.transform.Find("Seats/Seat_0");
            var director = UnityEngine.Object.FindAnyObjectByType<CandyTableDirector>();
            var rig = scene.GetRootGameObjects().FirstOrDefault(g => g.name.Contains("Camera Rig"));
            if (seat == null || director == null || rig == null) throw new InvalidOperationException("Seat_0, director, or Camera Rig not found.");
            var anchors = rig.GetComponentsInChildren<Transform>(true);
            var eye = anchors.First(t => t.name == "CenterEyeAnchor");
            var firstPersonCamera = eye.GetComponent<Camera>();
            var input = director.GetComponent<VRTableInput>();
            if (input == null) input = director.gameObject.AddComponent<VRTableInput>();
            Directory.CreateDirectory(Art + "/Prefabs");
            AssetDatabase.Refresh();
            EnsureMaterials();
            EnsureMeshyMaterials();
            int layer = HeadLayer();
            var template = CreateAvatar(layer);
            PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
            UnityEngine.Object.DestroyImmediate(template);
            // Reinstall the local avatar to pick up the latest prefab structure.
            foreach (var name in new[] { "Local Seated Avatar", "Local Avatar" })
            {
                var existing = seat.Find(name);
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            {
                var local = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), seat);
                local.name = "Local Avatar";
                local.transform.localPosition = Vector3.zero;
                local.transform.localRotation = Quaternion.identity;
                var avatar = local.GetComponent<SeatedAvatar>();
                avatar.seat = seat;
                local.GetComponent<RemoteAvatarRepresentation>().enabled = false;
                var tracking = local.AddComponent<LocalAvatarTracking>();
                tracking.avatar = avatar; tracking.tableInput = input;
                tracking.headTarget = eye; tracking.firstPersonCamera = firstPersonCamera;
                // Controller helpers are visual children; their anchors and input remain active.
                tracking.controllerRenderers = rig.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.GetComponentsInParent<Transform>(true).Any(t => t.name.Contains("Controller"))).ToArray();
            }
            // Bots and waiter: floating models with the same rig, so CharacterAnimator animations still work.
            foreach (string name in new[] { "Bot_Yellow", "Bot_Blue", "Bot_Red", "Bot_Mint", "Bot_Peach", "Waiter" })
            {
                var old = root.GetComponentsInChildren<CharacterAnimator>(true).First(c => c.name == name);
                var parent = old.transform.parent;
                var position = old.transform.localPosition;
                var rotation = old.transform.localRotation;
                int sibling = old.transform.GetSiblingIndex();
                string settings = EditorJsonUtility.ToJson(old);
                var replacement = Model(BotModels.TryGetValue(name, out var model) ? model : name + "_Float", parent);
                replacement.name = old.name;
                replacement.transform.SetLocalPositionAndRotation(position, rotation);
                replacement.transform.SetSiblingIndex(sibling);
                EditorJsonUtility.FromJsonOverwrite(settings, replacement.AddComponent<CharacterAnimator>());
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AvatarValidation.Check();
            Debug.Log("[Bite or Betray] Floating avatar installed; gameplay and room lightmaps preserved.");
        }

        static GameObject CreateAvatar(int layer)
        {
            var root = new GameObject("Seated Remote Avatar");
            var model = Model("Avatar_Float", root.transform);
            var face = model.AddComponent<CharacterAnimator>();
            face.trackingDrivenBody = true;
            var avatar = root.AddComponent<SeatedAvatar>();
            avatar.seat = root.transform;
            avatar.head = Part(model.transform, "Head");
            avatar.localHeadLayer = layer;
            var eye = new GameObject("Avatar Eye Reference").transform;
            eye.SetParent(avatar.head, false);
            eye.position = (Part(model.transform, "EyeWhite_L").position + Part(model.transform, "EyeWhite_R").position) * .5f + root.transform.forward * .005f;
            eye.rotation = root.transform.rotation;
            avatar.eyeReference = eye;
            var armA = Part(model.transform, "Arm_Left");
            var armB = Part(model.transform, "Arm_Right");
            bool aIsLeft = Vector3.Dot(armA.position - root.transform.position, root.transform.right) < 0;
            avatar.left = ConfigureHand(model.transform, aIsLeft ? "Left" : "Right", root.transform, -1);
            avatar.right = ConfigureHand(model.transform, aIsLeft ? "Right" : "Left", root.transform, 1);
            avatar.headRenderers = avatar.head.GetComponentsInChildren<Renderer>(true)
                .Concat(model.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "HeadSkin")).Distinct().ToArray();
            root.AddComponent<RemoteAvatarRepresentation>().avatar = avatar;
            root.AddComponent<AvatarReactionController>().face = face;
            return root;
        }

        // New art_manifest colors (skin, hair, iris) have no Unity materials yet: create shared URP Lit materials.
        static void EnsureMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string manifest = File.ReadAllText(Art + "/art_manifest.json");
            foreach (Match match in Regex.Matches(manifest, @"""([^""]+)"":\s*""(#[0-9a-fA-F]{6})"""))
            {
                string name = match.Groups[1].Value;
                string path = Art + "/Materials/BoB3D_" + name + ".mat";
                ColorUtility.TryParseHtmlString(match.Groups[2].Value, out Color color);
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null)
                {
                    // Colors follow the manifest; preserve gloss and textures from GraphicsPolish.
                    if (name != "Glass" && existing.GetColor("_BaseColor") != color) { existing.SetColor("_BaseColor", color); EditorUtility.SetDirty(existing); }
                    continue;
                }
                var mat = new Material(shader) { name = "BoB3D_" + name, enableInstancing = true };
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", GraphicsPolish.Smoothness(name));
                AssetDatabase.CreateAsset(mat, path);
                if (name.StartsWith("Iris"))
                {
                    // The iris uses the glazed version like the other eyes (Model() selects it automatically).
                    var glazed = new Material(mat) { name = mat.name + "_Glazed" };
                    glazed.SetFloat("_Smoothness", .78f);
                    AssetDatabase.CreateAsset(glazed, Art + "/Graphics/" + glazed.name + ".mat");
                }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Unity -batchmode -executeMethod BiteOrBetray.EditorTools.SeatedAvatarBuilder.BatchInstall (Unity closed).</summary>
        public static void BatchInstall()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/BiteOrBetray/Scenes/CandyParlour3D.unity");
                Install();
                BatchPoses();
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/BoBAvatar.result.txt", exception.ToString());
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Render character poses; copy to Logs/BoBPreview because Temp/ is removed when batch Unity exits.</summary>
        public static void BatchPoses()
        {
            if (!EditorSceneManager.GetActiveScene().path.EndsWith("/CandyParlour3D.unity"))
                EditorSceneManager.OpenScene("Assets/BiteOrBetray/Scenes/CandyParlour3D.unity");
            ArtSceneBuilder.RenderCharacterPoses();
            Directory.CreateDirectory("Logs/BoBPreview");
            foreach (string file in Directory.GetFiles("Temp", "BoB*.png").Concat(Directory.GetFiles("Temp", "BoB*.txt")))
                File.Copy(file, Path.Combine("Logs/BoBPreview", Path.GetFileName(file)), true);
        }

        /// <summary>Resolve Meshy albedo imports before preparing scenes for tests, screenshots and builds.</summary>
        public static void PrepareMeshyMaterials()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Prepare Meshy materials in Edit Mode.");
            foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { Art + "/Materials" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Full.mat")))
            {
                string atlas = Path.GetFileNameWithoutExtension(path);
                string model = atlas.Substring(0, atlas.Length - "_Full".Length);
                string texturePath = Art + "/Meshy/" + model + "/" + atlas + "_BaseColor.png";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material.HasProperty("_BaseMap") || !material.HasProperty("_MainTex"))
                    throw new InvalidOperationException("Meshy material must use URP Lit: " + path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                string restore = null;
                if (texture == null || material.GetTexture("_BaseMap") != texture || material.GetTexture("_MainTex") != texture)
                {
                    // A corrupt checkout would only fail to import again: restore it from Git LFS first.
                    if (texture == null) restore = RestoreFromGitLfs(texturePath);
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    material = AssetDatabase.LoadAssetAtPath<Material>(path);
                }
                if (texture == null)
                    throw new InvalidOperationException("Meshy base color texture could not be imported: " + texturePath + " (" + DescribePng(texturePath) + (restore != null ? "; " + restore : "") + ")");
                // URP validation copies _BaseMap to _MainTex. Resolve the texture first so an unresolved
                // import cannot persist a null legacy slot. Asset repair belongs here, never in gameplay/tests.
                foreach (string property in new[] { "_BaseMap", "_MainTex" })
                    if (material.GetTexture(property) != texture)
                    {
                        material.SetTexture(property, texture);
                        EditorUtility.SetDirty(material);
                    }
                AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        // Why a PNG did not import: an LFS pointer, or a corrupt checkout with the right size whose chunk CRC fails
        // (seen on the CI laptop; the runner checks every LFS file against its object id before Unity starts).
        static string DescribePng(string path)
        {
            if (!File.Exists(path)) return "file missing";
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 200 && System.Text.Encoding.ASCII.GetString(data).StartsWith("version https://git-lfs"))
                return "Git LFS pointer, run git lfs pull";
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (data.Length < 8 || !data.Take(8).SequenceEqual(signature)) return data.Length + " bytes without a PNG signature";
            for (int at = 8; at + 12 <= data.Length;)
            {
                long length = (uint)(data[at] << 24 | data[at + 1] << 16 | data[at + 2] << 8 | data[at + 3]);
                if (at + 12 + length > data.Length) return $"{data.Length} bytes, PNG chunk at byte {at} runs past the end";
                string type = System.Text.Encoding.ASCII.GetString(data, at + 4, 4);
                int end = at + 8 + (int)length;
                uint stored = (uint)(data[end] << 24 | data[end + 1] << 16 | data[end + 2] << 8 | data[end + 3]);
                if (Crc32(data, at + 4, end) != stored)
                    return $"{data.Length} bytes, PNG chunk {type} at byte {at} fails its CRC: corrupt file, restore it from Git LFS";
                if (type == "IEND") return data.Length + " bytes, PNG chunks intact";
                at = end + 4;
            }
            return data.Length + " bytes, PNG without IEND";
        }

        // The CI laptop kept a RedGrouch_Full_BaseColor.png with the right size and a bad IDAT chunk through every run:
        // git trusts its stat cache and git-lfs smudges a local object without hashing it, so checkout -f and lfs pull
        // never replace it, and the runner on the laptop is a copy that only changes when the owner reinstalls it.
        // In CI 585a9cc checkout -f and lfs pull run from Unity wrote nothing after the file was deleted (Unity then also
        // deleted its .meta). So the verified LFS object is copied over the file here without the smudge filter: a corrupt
        // local object is deleted and fetched again (downloads are verified), and the file is only replaced by an object
        // that hashes to its pointer oid. Returns null when the file already matches.
        static string RestoreFromGitLfs(string path)
        {
            string failure = null;
            string Git(string arguments)
            {
                try
                {
                    var start = new System.Diagnostics.ProcessStartInfo("git", arguments) {
                        RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
                    };
                    // Batch Unity has nobody to answer a credential prompt: fail with a message instead of hanging.
                    start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                    start.EnvironmentVariables["GCM_INTERACTIVE"] = "never";
                    using var git = System.Diagnostics.Process.Start(start);
                    var error = git.StandardError.ReadToEndAsync();
                    string text = git.StandardOutput.ReadToEnd().Trim();
                    git.WaitForExit();
                    if (git.ExitCode == 0) return text;
                    failure = $"git {arguments} exit {git.ExitCode}: {error.Result.Trim().Replace('\n', ' ')}";
                }
                catch (Exception exception) { failure = "git could not run: " + exception.Message; }
                return null;
            }
            var pointer = Regex.Match(Git($"cat-file blob \":{path}\"") ?? "", @"oid sha256:([0-9a-f]{64})");
            if (!pointer.Success) return "no Git LFS pointer in the index to restore from" + (failure != null ? " (" + failure + ")" : "");
            string oid = pointer.Groups[1].Value;
            if (Sha256(path) == oid) return null;
            string objectPath = Path.Combine(Git("rev-parse --git-common-dir") ?? ".git", "lfs", "objects", oid.Substring(0, 2), oid.Substring(2, 2), oid);
            bool badObject = File.Exists(objectPath) && Sha256(objectPath) != oid;
            if (badObject)
            {
                File.SetAttributes(objectPath, FileAttributes.Normal);
                File.Delete(objectPath);
            }
            bool fetched = !File.Exists(objectPath) && Git($"lfs fetch --include=\"{path}\"") != null;
            if (Sha256(objectPath) == oid)
            {
                if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
                File.Copy(objectPath, path, true);
            }
            string result = (badObject ? "local Git LFS object was corrupt and was deleted, " : "")
                + (fetched ? "object fetched again, " : "")
                + (Sha256(path) == oid ? "file restored from the verified Git LFS object"
                    : "file still does not match Git LFS object " + oid.Substring(0, 12) + (File.Exists(objectPath) ? "" : ", object not in " + objectPath)
                        + (failure != null ? "; " + failure : ""));
            Debug.LogWarning($"{path} did not match its Git LFS object {oid.Substring(0, 12)}: {result}.");
            return result;
        }

        static string Sha256(string path)
        {
            if (!File.Exists(path)) return "";
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
        }

        static uint Crc32(byte[] data, int start, int end)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = start; i < end; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xEDB88320u & (0u - (crc & 1)));
            }
            return ~crc;
        }

        // Meshy characters (Art3D/Meshy/<Name>/<Atlas>_BaseColor|Normal|MetallicSmoothness.png): URP Lit materials
        // named like the FBX atlas, Android 1024 ASTC textures. Double-sided because Meshy hair uses sheets.
        static void EnsureMeshyMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (string basePath in AssetDatabase.FindAssets("_BaseColor t:Texture2D", new[] { Art + "/Meshy" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_BaseColor.png")))
            {
                string atlas = Path.GetFileNameWithoutExtension(basePath).Replace("_BaseColor", "");
                string dir = basePath.Substring(0, basePath.LastIndexOf('/'));
                var baseMap = MeshyTexture(basePath, TextureImporterType.Default, true);
                var normal = MeshyTexture(dir + "/" + atlas + "_Normal.png", TextureImporterType.NormalMap, false);
                var mask = MeshyTexture(dir + "/" + atlas + "_MetallicSmoothness.png", TextureImporterType.Default, false);
                string path = Art + "/Materials/" + atlas + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader) { name = atlas };
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                mat.enableInstancing = true;
                mat.SetColor("_BaseColor", Color.white);
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetTexture("_BumpMap", normal);
                mat.SetTexture("_MetallicGlossMap", mask);
                mat.SetFloat("_Metallic", 0);
                mat.SetFloat("_Smoothness", 1);
                mat.SetFloat("_SmoothnessTextureChannel", 0);
                mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.doubleSidedGI = true;
                mat.EnableKeyword("_NORMALMAP");
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        static Texture2D MeshyTexture(string path, TextureImporterType type, bool srgb)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Meshy texture not imported yet: " + path);
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                ?? throw new InvalidOperationException("Meshy texture could not be imported: " + path);
        }

        // Hand resting position (relative to the seat): above the table, slightly in front of the table edge.
        static readonly Vector3 RestHand = new Vector3(.16f, .79f, .40f);

        static SeatedAvatar.Hand ConfigureHand(Transform model, string suffix, Transform root, float side)
        {
            var arm = new SeatedAvatar.Hand { hand = Part(model, "Hand_" + suffix), restPosition = Vector3.Scale(RestHand, new Vector3(side, 1, 1)) };
            Vector3 forward = (arm.hand.position - Part(model, "Forearm_" + suffix).position).normalized;
            arm.handRotationOffset = Quaternion.Inverse(Quaternion.LookRotation(forward, root.up)) * arm.hand.rotation;
            arm.fingers = model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Finger_") && t.name.EndsWith("_" + suffix)).ToArray();
            arm.fingerAxes = arm.fingers.Select(t => t.InverseTransformDirection(Vector3.Cross(forward, -root.up)).normalized).ToArray();
            arm.indexFingers = arm.fingers.Select(t => t.name.Contains("_Index_")).ToArray();
            return arm;
        }

        static GameObject Model(string name, Transform parent)
        {
            string path = Art + "/Models/" + name + ".fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidOperationException("Model has not finished importing: " + path);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.isReadable = false;
            importer.meshOptimizationFlags = MeshOptimizationFlags.Everything;
            foreach (var mat in source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Materials/" + mat.name + ".mat");
                if (material == null) throw new InvalidOperationException("Unknown material: " + mat.name);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mat.name), material);
            }
            importer.SaveAndReimport();
            source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    skin.updateWhenOffscreen = false;
                    skin.quality = SkinQuality.Bone4;
                    // Tracked arms can leave the imported rest bounds; keep culling conservative.
                    skin.localBounds = new Bounds(new Vector3(0, 0, 1), Vector3.one * 3);
                }
                if (renderer.name.StartsWith("EyeWhite") || renderer.name.StartsWith("Pupil"))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => AssetDatabase.LoadAssetAtPath<Material>(Art + "/Graphics/" + m.name + "_Glazed.mat") ?? m).ToArray();
            }
            return model;
        }

        public static Transform Part(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static int HeadLayer()
        {
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = manager.FindProperty("layers");
            for (int i = 8; i < 32; i++) if (layers.GetArrayElementAtIndex(i).stringValue == "Local Avatar Head") return i;
            for (int i = 30; i >= 8; i--)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = "Local Avatar Head";
                    manager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            throw new InvalidOperationException("No free layer for the local head.");
        }
    }
}
