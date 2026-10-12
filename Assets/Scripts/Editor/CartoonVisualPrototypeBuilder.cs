using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BiteOrBetray.EditorTools
{
    [InitializeOnLoad]
    public static class CartoonVisualPrototypeBuilder
    {
        const string Folder = "Assets/BiteOrBetray/VisualPrototype";
        const string Request = "Temp/CartoonVisualPrototype.request";
        const string Output = @"C:\Users\acer\Documents\Codex\2026-10-04\buk\outputs\BiteOrBetray_VisualPrototype";
        static double lastCheck;

        static CartoonVisualPrototypeBuilder() => EditorApplication.update += Check;

        static void Check()
        {
            if (EditorApplication.timeSinceStartup - lastCheck < 1) return;
            lastCheck = EditorApplication.timeSinceStartup;
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            string errorPath = Path.Combine(Output, "Unity_Prototype_Error.txt");
            try
            {
                Build();
                if (File.Exists(errorPath)) File.Delete(errorPath);
            }
            catch (Exception e)
            {
                File.WriteAllText(errorPath, e.ToString());
                Debug.LogException(e);
            }
        }

        [MenuItem("Bite or Betray/Prototype/Bangun Visual Kartun (Scene Terpisah)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Hentikan Play sebelum membuat prototype.");
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Folder + "/Materials");
            Directory.CreateDirectory(Output);
            var original = EditorSceneManager.GetActiveScene();
            var prototype = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("Cartoon Visual Proof - belum terhubung gameplay");
                foreach (string name in new[] { "Room", "Table", "Dispenser", "Inventory", "SeatBlockouts", "PlayerHands" })
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Cartoon_{name}_Prototype.fbx");
                    if (asset == null) throw new FileNotFoundException("FBX prototype belum selesai diimpor: " + name);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, prototype);
                    go.transform.SetParent(root.transform, false);
                }
                var door = root.GetComponentsInChildren<Transform>().First(t => t.name == "Kitchen door");
                float sign = Mathf.Sign(door.position.z);
                if (sign == 0) throw new InvalidOperationException("Orientasi FBX room tidak dapat ditentukan.");
                var heroAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Cartoon_Hero_Prototype.fbx");
                if (heroAsset == null) throw new FileNotFoundException("Hero FBX belum diimpor.");
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(heroAsset, prototype);
                hero.transform.SetParent(root.transform, false);
                hero.transform.position = new Vector3(0, 0, sign * 1.52f);

                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("Shader URP Lit tidak tersedia.");
                var renderers = root.GetComponentsInChildren<Renderer>();
                var materials = renderers.SelectMany(r => r.sharedMaterials).Distinct()
                    .ToDictionary(source => source, source => Remap(source, shader));
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(source => materials[source]).ToArray();
                }
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.47f, .51f, .54f);
                AddLight("Warm key", new Vector3(40, -28, 0), 1.2f, new Color(1, .89f, .74f), true);
                AddLight("Soft fill", new Vector3(34, 143, 0), .45f, new Color(.76f, .88f, 1), false);
                var cam = new GameObject("Prototype comparison camera").AddComponent<Camera>();
                cam.transform.position = new Vector3(0, 1.48f, -sign * 1.83f);
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(0, 1.19f, sign * .45f) - cam.transform.position);
                cam.fieldOfView = 47.5f;
                cam.nearClipPlane = .02f;
                cam.farClipPlane = 30;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(.75f, .78f, .79f);
                cam.GetUniversalAdditionalCameraData().allowXRRendering = false;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                cam.cullingMask = 1 << 29;
                foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 29;
                // The layer isolates the capture from any unsaved objects in the original additive scene.
                EditorSceneManager.SaveScene(prototype, Folder + "/CartoonVisualProof.unity");
                Capture(cam, Path.Combine(Output, "BiteOrBetray_Cartoon_UnityProof.png"));
                var meshes = root.GetComponentsInChildren<MeshFilter>().Select(m => m.sharedMesh)
                    .Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>().Select(m => m.sharedMesh)).Where(m => m != null).ToArray();
                var faceMeshes = hero.GetComponentsInChildren<SkinnedMeshRenderer>();
                File.WriteAllText("Temp/CartoonFaceShapeNames.txt", string.Join("\n", faceMeshes.Select(r =>
                    r.name + ": " + string.Join(", ", Enumerable.Range(0, r.sharedMesh.blendShapeCount).Select(r.sharedMesh.GetBlendShapeName)))));
                int mouthShapes = faceMeshes.Where(r => r.name == "Mouth").Sum(r => r.sharedMesh.blendShapeCount);
                int lidShapes = faceMeshes.Where(r => r.name.StartsWith("Lid_")).Sum(r => r.sharedMesh.blendShapeCount);
                if (mouthShapes != 3 || lidShapes != 4)
                    throw new InvalidOperationException($"Mouth/lid blend shapes: {mouthShapes}/{lidShapes}. " +
                        string.Join(", ", faceMeshes.Select(r => r.name + ":" + r.sharedMesh.blendShapeCount)));
                cam.transform.position = new Vector3(.26f, 1.72f, sign * .29f);
                cam.transform.rotation = Quaternion.LookRotation(new Vector3(0, 1.54f, sign * 1.46f) - cam.transform.position);
                cam.fieldOfView = 41.1f;
                foreach (string mood in new[] { "Smug", "Good", "Bad" })
                {
                    foreach (var renderer in faceMeshes)
                        for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                        {
                            string shape = renderer.sharedMesh.GetBlendShapeName(i).Split('.').Last();
                            float weight = 0;
                            if (mood == "Good")
                            {
                                if (shape == "Open") weight = 30;
                                else if (shape == "Grin" || shape == "Wide") weight = 80;
                            }
                            else if (mood == "Bad")
                            {
                                if (shape == "Frown") weight = 100;
                                else if (shape == "Squint") weight = 75;
                            }
                            renderer.SetBlendShapeWeight(i, weight);
                        }
                    Capture(cam, Path.Combine(Output, "BiteOrBetray_Hero_Unity_" + mood + ".png"), 900, 900, faceMeshes);
                }
                File.WriteAllText(Path.Combine(Output, "Unity_Prototype_Checks.txt"),
                    "Scene: " + prototype.path + "\nStatus: visual prototype only, no gameplay binding.\n" +
                    "Hero revision: 2\nHero imported: yes\nMouth blend shapes: " + mouthShapes + "\nEyelid blend shapes: " + lidShapes + "\nMesh renderers: " + root.GetComponentsInChildren<Renderer>().Length +
                    "\nMesh triangles (before optimization): " + meshes.Sum(m => m.triangles.Length / 3) +
                    "\nFBX room sign: " + sign + "\nOriginal scene preserved: " + original.path + "\nQuest hardware performance: not measured.\n");
                Debug.Log("[Bite or Betray] Visual prototype saved: " + prototype.path);
            }
            finally
            {
                if (original.IsValid()) EditorSceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(prototype, true);
            }
        }

        static Material Remap(Material source, Shader shader)
        {
            if (source == null) throw new InvalidOperationException("Material FBX kosong.");
            string name = source.name.Replace(" (Instance)", "");
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            Color color = source.HasProperty("_Color") ? source.color : Color.white;
            material.SetColor("_BaseColor", color);
            bool candy = name == "Cartoon_Blue" || name == "Cartoon_Red" || name == "Cartoon_Yellow";
            material.SetFloat("_Smoothness", name.Contains("Glass") ? .9f : name.Contains("Gold") ? .6f : candy ? .6f : .2f);
            material.SetFloat("_Metallic", name.Contains("Gold") ? .55f : 0);
            if (name.Contains("Glass"))
            {
                material.SetColor("_BaseColor", new Color(.82f, .94f, .95f, .12f));
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetOverrideTag("RenderType", "Transparent");
            }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static void AddLight(string name, Vector3 rotation, float intensity, Color color, bool shadows)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(rotation);
            light.intensity = intensity;
            light.color = color;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.cullingMask = 1 << 29;
        }

        internal static void Capture(Camera camera, string path, int width = 1280, int height = 720, SkinnedMeshRenderer[] poseMeshes = null)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var baked = new List<(SkinnedMeshRenderer source, GameObject instance, Mesh mesh)>();
            try
            {
                // Editor captures run in one update; bake each pose to avoid reusing the previous skinning frame.
                foreach (var source in poseMeshes ?? Array.Empty<SkinnedMeshRenderer>())
                {
                    if (!source.enabled) continue;
                    var instance = new GameObject("Temporary expression capture");
                    instance.transform.SetParent(source.transform, false);
                    instance.layer = source.gameObject.layer;
                    var mesh = new Mesh();
                    baked.Add((source, instance, mesh));
                    source.BakeMesh(mesh);
                    mesh.RecalculateBounds();
                    if (mesh.bounds.size.sqrMagnitude == 0) throw new InvalidOperationException("Mesh pose kosong: " + source.name);
                    instance.AddComponent<MeshFilter>().sharedMesh = mesh;
                    instance.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
                    source.enabled = false;
                }
                camera.aspect = (float)width / height;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                foreach (var item in baked)
                {
                    item.source.enabled = true;
                    UnityEngine.Object.DestroyImmediate(item.instance);
                    UnityEngine.Object.DestroyImmediate(item.mesh);
                }
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
