using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BiteOrBetray.EditorTools
{
    [InitializeOnLoad]
    public static class GraphicsPolish
    {
        const string Art = "Assets/BiteOrBetray/Art3D";
        const string Graphics = Art + "/Graphics";
        const string Result = "Temp/BoBGraphics.result.json";
        // ponytail: these sub-texel decorations use probes; bake charts if their geometry grows.
        static readonly HashSet<string> ProbeDetails = new HashSet<string> {
            "Room_Glow", "Room_Yellow", "ClothStitch", "Room_Mint", "Room_Ink", "Table_Gold", "Room_Red", "Room_Gold"
        };
        static double lastCheck;

        static GraphicsPolish()
        {
            EditorApplication.update += CheckRequests;
            Lightmapping.bakeCompleted += BakeFinished;
        }

        static void CheckRequests()
        {
            if (EditorApplication.timeSinceStartup - lastCheck < 1 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            lastCheck = EditorApplication.timeSinceStartup;
            if (File.Exists("Temp/BoBGraphicsCapture.request"))
            {
                File.Delete("Temp/BoBGraphicsCapture.request");
                EditorApplication.ExecuteMenuItem("Bite or Betray/Render 3D Scene Preview");
            }
            if (!File.Exists("Temp/BoBGraphics.request") || EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) return;
            File.Delete("Temp/BoBGraphics.request");
            try { Apply(); }
            catch (Exception error) { File.WriteAllText(Result, error.ToString()); Debug.LogException(error); }
        }

        [MenuItem("Bite or Betray/Polish Candy Parlour Graphics")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("/CandyParlour3D.unity")) throw new InvalidOperationException("Open CandyParlour3D first.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new InvalidOperationException("Stop Play or baking first.");
            var root = GameObject.Find("BoB_Art3D");
            if (root == null) throw new InvalidOperationException("BoB_Art3D not found.");
            Directory.CreateDirectory(Graphics);
            AssetDatabase.Refresh();
            if (!File.Exists("Temp/CandyParlour_BeforeGraphics.unity"))
                EditorSceneManager.SaveScene(scene, "Temp/CandyParlour_BeforeGraphics.unity", true);
            ConfigurePipelines();
            ConfigureMaterials(root);
            foreach (string model in new[] { "Room", "Table", "Chair" })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Art + "/Models/" + model + ".fbx");
                importer.generateSecondaryUV = true;
                importer.secondaryUVMinLightmapResolution = 24;
                importer.secondaryUVMinObjectScale = .5f;
                importer.SaveAndReimport();
            }
            var lighting = root.transform.Find("Graphics Lighting");
            if (lighting == null)
            {
                lighting = new GameObject("Graphics Lighting").transform;
                lighting.SetParent(root.transform, false);
            }
            ConfigureLighting(root, lighting);
            ConfigureVolume(lighting);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool stationary = renderer.transform.IsChildOf(root.transform.Find("Room")) || renderer.transform.IsChildOf(root.transform.Find("Table"))
                    || renderer.GetComponentsInParent<Transform>().Any(t => t.name == "Chair");
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, stationary ? flags | StaticEditorFlags.ContributeGI : flags & ~StaticEditorFlags.ContributeGI);
                renderer.receiveGI = stationary && !ProbeDetails.Contains(renderer.name) ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
                renderer.shadowCastingMode = renderer.sharedMaterials.Any(m => m != null && m.name == "BoB3D_Glass") ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            foreach (var camera in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)))
            {
                if (camera.name != "Desktop Art Preview") continue;
                camera.allowMSAA = true;
                camera.allowHDR = true;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None;
                data.volumeLayerMask = ~0;
                EditorUtility.SetDirty(camera);
                EditorUtility.SetDirty(data);
            }
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(Graphics + "/CandyParlourLighting.asset");
            if (settings == null) { settings = new LightingSettings(); AssetDatabase.CreateAsset(settings, Graphics + "/CandyParlourLighting.asset"); }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.mixedBakeMode = MixedLightingMode.Shadowmask;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.lightmapResolution = 32;
            settings.lightmapMaxSize = 2048;
            settings.lightmapPadding = 4;
            settings.directSampleCount = 64;
            settings.indirectSampleCount = 256;
            settings.environmentSampleCount = 128;
            settings.maxBounces = 4;
            settings.ao = true;
            settings.aoMaxDistance = .25f;
            settings.aoExponentIndirect = 1;
            settings.aoExponentDirect = .15f;
            Lightmapping.lightingSettings = settings;
            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            SessionState.SetBool("BoB.GraphicsBake", true);
            File.WriteAllText(Result, "{\"status\":\"baking\"}");
            if (!Lightmapping.BakeAsync()) throw new InvalidOperationException("Unity cannot start light baking.");
            Debug.Log("[Bite or Betray] Materials and lighting updated; light baking is running.");
        }

        static void ConfigurePipelines()
        {
            foreach (string name in new[] { "PC", "Mobile" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/" + name + "_RPAsset.asset");
                if (pipeline == null) continue;
                pipeline.msaaSampleCount = 4;
                pipeline.renderScale = 1;
                pipeline.shadowDistance = 12;
                pipeline.mainLightShadowmapResolution = 2048;
                pipeline.shadowCascadeCount = 2;
                pipeline.cascade2Split = .35f;
                pipeline.shadowDepthBias = .3f;
                pipeline.shadowNormalBias = .3f;
                var serialized = new SerializedObject(pipeline);
                serialized.FindProperty("m_SoftShadowsSupported").boolValue = name == "PC";
                // ponytail: native probe sampling avoids Unity 6.6's GRD/Burst build-profile thread error.
                serialized.FindProperty("m_GPUResidentDrawerMode").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                pipeline.supportsHDR = name == "PC";
                pipeline.supportsCameraOpaqueTexture = false;
                EditorUtility.SetDirty(pipeline);
            }
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer == null) return;
            foreach (var feature in renderer.rendererFeatures.Where(f => f != null && f.name == "ScreenSpaceAmbientOcclusion"))
            {
                feature.SetActive(true);
                var settings = new SerializedObject(feature);
                settings.FindProperty("m_Settings.Intensity").floatValue = .22f;
                settings.FindProperty("m_Settings.Radius").floatValue = .12f;
                settings.FindProperty("m_Settings.DirectLightingStrength").floatValue = .1f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
            }
            EditorUtility.SetDirty(renderer);
        }

        static bool HasUVArea(Vector2[] coordinates, int[] triangles)
        {
            if (coordinates.Length == 0 || triangles.Any(i => i >= coordinates.Length)) return false;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector2 a = coordinates[triangles[i + 1]] - coordinates[triangles[i]];
                Vector2 b = coordinates[triangles[i + 2]] - coordinates[triangles[i]];
                if (Mathf.Abs(a.x * b.y - a.y * b.x) > 1e-10f) return true;
            }
            return false;
        }

        /// <summary>Gloss by palette color. Skin and hair are slightly glossy like the reference cartoon characters.</summary>
        internal static float Smoothness(string name) =>
            name == "Gold" ? .72f : name == "Glass" ? .85f : name == "Floor" ? .16f
            : name == "Milk" || name == "Tongue" ? .5f : name == "Teal" || name == "Coral" ? .42f
            : name == "Skin" ? .45f : name.StartsWith("Hair") ? .55f : .3f;

        internal static void ConfigureMaterials(GameObject root)
        {
            Texture2D wood = SurfaceTexture("WoodGrain", true);
            Texture2D ceramic = SurfaceTexture("Ceramic", false);
            foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { Art + "/Materials" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat.name.StartsWith("BoB3D_")) continue;
                string name = mat.name.Replace("BoB3D_", "");
                mat.SetFloat("_Smoothness", Smoothness(name));
                if (name == "Gold") mat.SetFloat("_Metallic", .72f);
                if (name == "Wood" || name == "WoodDark") mat.SetTexture("_BaseMap", wood);
                if (name == "Floor") mat.SetTexture("_BaseMap", ceramic);
                if (name == "Glow")
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", mat.GetColor("_BaseColor") * 2);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                }
                EditorUtility.SetDirty(mat);
            }
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!(renderer.name.Contains("EyeWhite") || renderer.name.Contains("Pupil") || renderer.name.Contains("EyeSpark")
                    || renderer.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("Candy_")))) continue;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source => {
                    if (source.name.EndsWith("_Glazed")) return source;
                    string path = Graphics + "/" + source.name + "_Glazed.mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) { mat = new Material(source); AssetDatabase.CreateAsset(mat, path); }
                    mat.SetFloat("_Smoothness", .78f);
                    EditorUtility.SetDirty(mat);
                    return mat;
                }).ToArray();
            }
        }

        static Texture2D SurfaceTexture(string name, bool grain)
        {
            string path = Graphics + "/" + name + ".png";
            if (!File.Exists(path))
            {
                const int size = 512;
                var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
                var colors = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = x * 2 * Mathf.PI / size, v = y * 2 * Mathf.PI / size;
                        float pattern = grain ? Mathf.Sin(v * 24 + Mathf.Sin(u) * 1.3f + Mathf.Sin(u * 3 + v * 2) * .5f)
                            : Mathf.Sin(u * 19 + Mathf.Cos(v * 11)) * Mathf.Sin(v * 31 + Mathf.Cos(u * 13));
                        float value = (grain ? .91f : .97f) + pattern * (grain ? .065f : .025f);
                        colors[y * size + x] = new Color(value, value, value);
                    }
                texture.SetPixels(colors); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void ConfigureLighting(GameObject root, Transform lighting)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.38f, .49f, .58f);
            RenderSettings.ambientEquatorColor = new Color(.28f, .25f, .20f);
            RenderSettings.ambientGroundColor = new Color(.15f, .13f, .10f);
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.color = new Color(1, .92f, .8f);
                sun.intensity = 1.15f;
                sun.lightmapBakeType = LightmapBakeType.Mixed;
                sun.transform.rotation = Quaternion.Euler(45, -55, 0);
                sun.shadows = LightShadows.Soft;
                sun.shadowBias = .3f;
                sun.shadowNormalBias = .3f;
                sun.GetUniversalAdditionalLightData().usePipelineSettings = false;
            }
            foreach (var light in root.GetComponentsInChildren<Light>())
                if (light.name == "Shop Fill" || light.name == "Window Fill") light.lightmapBakeType = LightmapBakeType.Baked;
            Area("Table Softbox", new Vector3(0, 3, 0), new Vector2(2.5f, 2.5f), new Color(1, .88f, .73f), 3);
            Area("Counter Softbox", new Vector3(0, 3, 2.4f), new Vector2(3.5f, 1), new Color(1, .9f, .79f), 2);
            var probes = lighting.GetComponentInChildren<LightProbeGroup>();
            if (probes == null) probes = new GameObject("Character Light Probes").AddComponent<LightProbeGroup>();
            probes.transform.SetParent(lighting, false);
            var positions = new List<Vector3>();
            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 3; z++)
                    foreach (float y in new[] { .35f, 1.1f, 1.8f, 2.6f }) positions.Add(new Vector3(x * .8f, y, z * .8f));
            probes.probePositions = positions.ToArray();
            var reflection = lighting.GetComponentInChildren<ReflectionProbe>();
            if (reflection == null) reflection = new GameObject("Candy Shop Reflections").AddComponent<ReflectionProbe>();
            reflection.transform.SetParent(lighting, false);
            reflection.transform.localPosition = new Vector3(0, 1.5f, .2f);
            reflection.mode = ReflectionProbeMode.Baked;
            reflection.resolution = 256;
            reflection.size = new Vector3(8, 4, 7.6f);
            reflection.boxProjection = true;
            reflection.blendDistance = .25f;
            reflection.nearClipPlane = .05f;
            reflection.farClipPlane = 15;
            reflection.hdr = true;

            void Area(string name, Vector3 position, Vector2 size, Color color, float intensity)
            {
                var child = lighting.Find(name);
                if (child == null) { child = new GameObject(name).transform; child.SetParent(lighting, false); }
                child.localPosition = position;
                child.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                if (!child.TryGetComponent<Light>(out var light)) light = child.gameObject.AddComponent<Light>();
                light.type = LightType.Rectangle;
                light.areaSize = size;
                light.color = color;
                light.intensity = intensity;
                light.range = 8;
                light.lightmapBakeType = LightmapBakeType.Baked;
            }
        }

        static void ConfigureVolume(Transform lighting)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Graphics + "/CandyParlourLook.asset");
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, Graphics + "/CandyParlourLook.asset"); }
            var tone = Component<Tonemapping>(); tone.mode.Override(TonemappingMode.Neutral);
            var color = Component<ColorAdjustments>();
            color.postExposure.Override(.15f); color.contrast.Override(8); color.saturation.Override(10);
            var bloom = Component<Bloom>(); bloom.threshold.Override(1.2f); bloom.intensity.Override(.12f); bloom.scatter.Override(.45f);
            var volume = lighting.GetComponentInChildren<Volume>();
            if (volume == null) volume = new GameObject("Desktop Color Grade").AddComponent<Volume>();
            volume.transform.SetParent(lighting, false);
            volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;
            EditorUtility.SetDirty(profile);

            T Component<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var component))
                {
                    component = profile.Add<T>(true);
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
                return component;
            }
        }

        static void BakeFinished()
        {
            if (!SessionState.GetBool("BoB.GraphicsBake", false)) return;
            EditorApplication.delayCall += () => {
                try
                {
                    Validate();
                    SessionState.EraseBool("BoB.GraphicsBake");
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                    AssetDatabase.SaveAssets();
                    EditorApplication.ExecuteMenuItem("Bite or Betray/Render 3D Scene Preview");
                    Debug.Log("[Bite or Betray] Graphics polished and light baking passed validation.");
                }
                catch (Exception error) { File.WriteAllText(Result, error.ToString()); Debug.LogException(error); }
            };
        }

        [MenuItem("Bite or Betray/Check Candy Parlour Graphics")]
        public static void Validate()
        {
            var root = GameObject.Find("BoB_Art3D");
            int characters = root.GetComponentsInChildren<CharacterAnimator>(true).Count(c => !c.trackingDrivenBody);
            int directors = UnityEngine.Object.FindObjectsByType<CandyTableDirector>(FindObjectsInactive.Include).Length;
            int maps = LightmapSettings.lightmaps.Length;
            if (characters != 6 || directors != 1 || maps == 0 || LightmapSettings.lightmaps.Any(m => m.lightmapColor == null))
                throw new InvalidOperationException($"Graphics check failed: characters {characters}, directors {directors}, lightmaps {maps}.");
            if (root.GetComponentsInChildren<Renderer>().Any(r => r.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader")))
                throw new InvalidOperationException("A material failed to load.");
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.GetComponent<MeshRenderer>().receiveGI != ReceiveGI.Lightmaps) continue;
                using var snapshot = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                using var uv = new NativeArray<Vector2>(filter.sharedMesh.vertexCount, Allocator.Temp);
                var data = snapshot[0];
                if (data.HasVertexAttribute(VertexAttribute.TexCoord1)) data.GetUVs(1, uv);
                var indices = filter.sharedMesh.indexFormat == IndexFormat.UInt16 ? data.GetIndexData<ushort>().ToArray().Select(i => (int)i).ToArray()
                    : data.GetIndexData<int>().ToArray();
                if (!HasUVArea(uv.ToArray(), indices)) throw new InvalidOperationException("Lightmap UVs still empty: " + filter.name);
            }
            File.WriteAllText(Result, $"{{\"status\":\"complete\",\"characters\":{characters},\"directors\":{directors},\"lightmaps\":{maps},\"pipeline\":\"{AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline)}\"}}");
        }
    }
}
