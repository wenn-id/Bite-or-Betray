using System;
using System.IO;
using System.Linq;
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

        [MenuItem("Bite or Betray/Pasang Seated Upper-Body Avatar")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("/CandyParlour3D.unity") || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Buka CandyParlour3D dan hentikan Play dulu.");
            EditorSceneManager.SaveScene(scene, "Temp/CandyParlour_BeforeAvatar.unity", true);
            var root = GameObject.Find("BoB_Art3D");
            var seat = root.transform.Find("Seats/Seat_0");
            var director = UnityEngine.Object.FindAnyObjectByType<CandyTableDirector>();
            var rig = scene.GetRootGameObjects().FirstOrDefault(g => g.name.Contains("Camera Rig"));
            if (seat == null || director == null || rig == null) throw new InvalidOperationException("Seat_0, director, atau Camera Rig tidak ditemukan.");
            var anchors = rig.GetComponentsInChildren<Transform>(true);
            var eye = anchors.First(t => t.name == "CenterEyeAnchor");
            var firstPersonCamera = eye.GetComponent<Camera>();
            var input = director.GetComponent<VRTableInput>();
            if (input == null) input = director.gameObject.AddComponent<VRTableInput>();
            Directory.CreateDirectory(Art + "/Prefabs");
            AssetDatabase.Refresh();
            int layer = HeadLayer();
            var template = CreateAvatar(layer);
            PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
            UnityEngine.Object.DestroyImmediate(template);
            var existing = seat.Find("Local Seated Avatar");
            if (existing == null)
            {
                var local = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), seat);
                local.name = "Local Seated Avatar";
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
            foreach (string color in new[] { "Yellow", "Blue", "Red", "Mint", "Peach" })
            {
                var old = root.GetComponentsInChildren<CharacterAnimator>(true).First(c => c.name == "Bot_" + color);
                if (Part(old.transform, "Hips") == null) continue;
                var parent = old.transform.parent;
                var position = old.transform.localPosition;
                int sibling = old.transform.GetSiblingIndex();
                string settings = EditorJsonUtility.ToJson(old);
                var replacement = Model("Bot_" + color + "_Upper", parent);
                replacement.name = old.name;
                replacement.transform.localPosition = position;
                replacement.transform.SetSiblingIndex(sibling);
                EditorJsonUtility.FromJsonOverwrite(settings, replacement.AddComponent<CharacterAnimator>());
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AvatarValidation.Check();
            Debug.Log("[Bite or Betray] Avatar duduk terpasang; gameplay dan lightmap ruangan dipertahankan.");
        }

        static GameObject CreateAvatar(int layer)
        {
            var root = new GameObject("Seated Remote Avatar");
            var model = Model("Avatar_UpperBody", root.transform);
            var face = model.AddComponent<CharacterAnimator>();
            face.trackingDrivenBody = true;
            var avatar = root.AddComponent<SeatedAvatar>();
            avatar.seat = root.transform;
            avatar.spine = Part(model.transform, "Spine");
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
            avatar.left = ConfigureArm(model.transform, aIsLeft ? "Left" : "Right", root.transform);
            avatar.right = ConfigureArm(model.transform, aIsLeft ? "Right" : "Left", root.transform);
            avatar.headRenderers = avatar.head.GetComponentsInChildren<Renderer>(true)
                .Concat(model.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "HeadSkin")).Distinct().ToArray();
            root.AddComponent<RemoteAvatarRepresentation>().avatar = avatar;
            root.AddComponent<AvatarReactionController>().face = face;
            return root;
        }

        static SeatedAvatar.Arm ConfigureArm(Transform model, string suffix, Transform root)
        {
            var arm = new SeatedAvatar.Arm {
                shoulder = Part(model, "Arm_" + suffix), elbow = Part(model, "Forearm_" + suffix), hand = Part(model, "Hand_" + suffix)
            };
            Vector3 forward = (arm.hand.position - arm.elbow.position).normalized;
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
            if (source == null) throw new InvalidOperationException("Model belum selesai diimpor: " + path);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.isReadable = false;
            importer.meshOptimizationFlags = MeshOptimizationFlags.Everything;
            foreach (var mat in source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Materials/" + mat.name + ".mat");
                if (material == null) throw new InvalidOperationException("Material tidak dikenal: " + mat.name);
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
            throw new InvalidOperationException("Tidak ada layer kosong untuk kepala local.");
        }
    }
}
