using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiteOrBetray.EditorTools
{
    /// <summary>
    /// Cozy Candy Parlour lighting upgrade & single-atlas static GI baker for Meta Quest 3:
    /// 1. Enables secondary UV2 lightmap coordinates on NewInterior FBX models.
    /// 2. Authors warm indoor chandelier spotlighting, 6 baked amber wall sconces, cool twilight window rims,
    ///    a 62-probe LightProbeGroup, and a 128px box-projected ReflectionProbe.
    /// 3. Bakes everything into ONE single 2048x2048 Non-Directional Subtractive Lightmap (lightmapIndex = 0)
    ///    so GameplayPerformanceBuilder merges static furniture with 0 extra draw-call splits.
    /// 4. Rebuilds Performance Geometry, syncs QuestPerfTest.unity, and runs QuestPerfTestBuilder.Audit().
    /// </summary>
    public static class ParlourLightingBaker
    {
        const string InteriorFolder = "Assets/BiteOrBetray/NewInterior";
        const string SourceScene = "Assets/BiteOrBetray/Scenes/CandyParlour3D.unity";
        const string SceneDataFolder = "Assets/BiteOrBetray/Scenes/CandyParlour3D";
        const string LightingAssetPath = "Assets/BiteOrBetray/Scenes/CandyParlour3D_Lighting.lighting";
        const string ReflectionCubemapPath = SceneDataFolder + "/ParlourReflection.exr";

        static readonly string[] InteriorModels =
        {
            "Table", "Chair", "Door", "Window", "Rug", "Shelves", "Display",
            "Sconce", "Picture", "Floor", "CandyJarRed", "CandyJarBlue", "CandyJarYellow"
        };

        [MenuItem("Bite or Betray/Bake Cozy Parlour Lighting")]
        public static void BakeMenu() => RunBakeInternal(exitOnComplete: false);

        public static void BakeAndOptimizeBatch()
        {
            try
            {
                RunBakeInternal(exitOnComplete: true);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        static void RunBakeInternal(bool exitOnComplete)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run Bake Cozy Parlour Lighting in Edit Mode.");

            EnsureSecondaryUVsAndMaterials();

            var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single(g => g.name == "BoB_Art3D").transform;

            RestoreSourceFurnitureForBake(root);
            EnsureUnwrappedCubeMeshesForRoom(root.Find("Room"));
            ConfigureGiFlagsAndScales(root);
            var reflectionProbe = AuthorParlourLightingRig(root);
            var lightingSettings = ConfigureLightingSettingsAsset();

            float[] candidateResolutions = { 5.5f, 4.2f };
            bool singleAtlas = false;
            foreach (float res in candidateResolutions)
            {
                lightingSettings.lightmapResolution = res;
                EditorUtility.SetDirty(lightingSettings);
                Lightmapping.lightingSettings = lightingSettings;
                Debug.Log($"[ParlourLightingBaker] Baking CandyParlour3D GI at {res} texels/unit...");
                if (!Lightmapping.Bake())
                    throw new InvalidOperationException("Lightmapping.Bake() failed.");

                int count = LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0;
                Debug.Log($"[ParlourLightingBaker] Bake finished: lightmap count = {count} (res={res}).");
                if (count == 1)
                {
                    singleAtlas = true;
                    break;
                }
            }
            if (!singleAtlas)
                throw new InvalidOperationException($"Expected exactly 1 lightmap atlas to prevent draw splits, got {LightmapSettings.lightmaps?.Length ?? 0}.");

            if (!AssetDatabase.IsValidFolder(SceneDataFolder))
                AssetDatabase.CreateFolder("Assets/BiteOrBetray/Scenes", "CandyParlour3D");

            if (reflectionProbe != null)
            {
                if (Lightmapping.BakeReflectionProbe(reflectionProbe, ReflectionCubemapPath))
                {
                    AssetDatabase.ImportAsset(ReflectionCubemapPath, ImportAssetOptions.ForceUpdate);
                    var cubemap = AssetDatabase.LoadAssetAtPath<Texture>(ReflectionCubemapPath);
                    if (cubemap != null)
                    {
                        reflectionProbe.mode = ReflectionProbeMode.Custom;
                        reflectionProbe.customBakedTexture = cubemap;
                        EditorUtility.SetDirty(reflectionProbe);
                    }
                }
            }

            ConfigureBakedTextureCompression();

            // Clear temporary bake shadow flags so runtime shadowDraws == 0, sconce LightShadows == None (NewInteriorTests), and receiveShadows is uniform
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (PrefabUtility.IsPartOfPrefabInstance(r))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            var roomTransform = root.Find("Room");
            if (roomTransform != null)
            {
                foreach (var l in roomTransform.GetComponentsInChildren<Light>(true))
                    if (l.name == "Warm lantern fill") l.shadows = LightShadows.None;
            }

            GameplayPerformanceBuilder.Optimize(root, true);

            var perfGeom = root.Find("Performance Geometry");
            int mergedGroups = perfGeom != null ? perfGeom.childCount : 0;
            Debug.Log($"[ParlourLightingBaker] Combined static furniture into {mergedGroups} draw groups.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            QuestPerfTestBuilder.BuildScene();
            QuestPerfTestBuilder.Audit();

            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            Debug.Log("COZY_PARLOUR_LIGHTING_BAKED: 1x 2048 lightmap, 62 light probes, 128px reflection probe, Quest audit PASS.");

            if (exitOnComplete && Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static void EnsureSecondaryUVsAndMaterials()
        {
            bool reimported = false;
            foreach (string model in InteriorModels)
            {
                string path = $"{InteriorFolder}/{model}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                if (!importer.generateSecondaryUV || importer.secondaryUVPackMargin < 8)
                {
                    importer.generateSecondaryUV = true;
                    importer.secondaryUVPackMargin = 8;
                    importer.secondaryUVMinLightmapResolution = 16;
                    importer.SaveAndReimport();
                    reimported = true;
                }
            }

            // Enrich smoothness/metallic ONLY on NewInterior materials (leave NewCharacters untouched for asset-hash tests)
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { InteriorFolder }))
            {
                string matPath = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null || mat.shader == null) continue;
                string n = mat.name;
                bool changed = false;
                if (n.Contains("Table") && mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", 0.40f);
                    changed = true;
                }
                else if (n.Contains("Chair") && mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", 0.32f);
                    changed = true;
                }
                else if (n.Contains("Floor") && mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", 0.24f);
                    changed = true;
                }
                else if (n.Contains("CandyJar") && mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", 0.60f);
                    changed = true;
                }
                else if (n == "WallGoldLine")
                {
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.45f);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.56f);
                    changed = true;
                }
                else if (n == "WallWoodRail" && mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", 0.36f);
                    changed = true;
                }
                else if (n == "CeilingCream" && mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", new Color(0.66f, 0.58f, 0.49f));
                    changed = true;
                }
                else if (n.Contains("Sconce") && mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", new Color(1.55f, 1.02f, 0.42f));
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    mat.EnableKeyword("_EMISSION");
                    changed = true;
                }
                if (changed) EditorUtility.SetDirty(mat);
            }
            if (reimported) AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }

        static void RestoreSourceFurnitureForBake(Transform root)
        {
            var prevGeom = root.Find("Performance Geometry");
            if (prevGeom != null) UnityEngine.Object.DestroyImmediate(prevGeom.gameObject);

            var room = root.Find("Room");
            var table = root.Find("Table");
            var dispenser = root.Find("Dispenser");
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                bool isCandidate = (room != null && r.transform.IsChildOf(room))
                    || (table != null && r.transform.IsChildOf(table))
                    || (dispenser != null && r.transform.IsChildOf(dispenser))
                    || r.GetComponentsInParent<Transform>().Any(t => t.name == "Chair" || t.name == "ItemMat");
                if (isCandidate)
                {
                    r.enabled = true;
                    if (PrefabUtility.IsPartOfPrefabInstance(r))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
            }
        }

        static void EnsureUnwrappedCubeMeshesForRoom(Transform room)
        {
            if (room == null) return;
            if (!AssetDatabase.IsValidFolder("Assets/BiteOrBetray/PerformanceMeshes"))
                AssetDatabase.CreateFolder("Assets/BiteOrBetray", "PerformanceMeshes");

            var builtinCube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            const string unwrappedPath = "Assets/BiteOrBetray/PerformanceMeshes/UnwrappedWallCube.asset";
            var unwrapped = AssetDatabase.LoadAssetAtPath<Mesh>(unwrappedPath);
            if (unwrapped == null)
            {
                unwrapped = UnityEngine.Object.Instantiate(builtinCube);
                unwrapped.name = "UnwrappedWallCube";
                UnwrapParam.SetDefaults(out var param);
                param.packMargin = 0.03f;
                Unwrapping.GenerateSecondaryUVSet(unwrapped, param);
                AssetDatabase.CreateAsset(unwrapped, unwrappedPath);
            }

            foreach (var mf in room.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.gameObject.activeInHierarchy || mf.name == "Ceiling plaster") continue;
                if (mf.sharedMesh == builtinCube)
                {
                    mf.sharedMesh = unwrapped;
                    EditorUtility.SetDirty(mf);
                }
            }
        }

        static void ConfigureGiFlagsAndScales(Transform root)
        {
            var room = root.Find("Room");
            var table = root.Find("Table");
            var dispenser = root.Find("Dispenser");

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;

                if (r is MeshRenderer mr)
                {
                    bool inDispenser = dispenser != null && mr.transform.IsChildOf(dispenser);
                    bool inItemMat = mr.GetComponentsInParent<Transform>().Any(t => t.name == "ItemMat");
                    bool inRoom = room != null && mr.transform.IsChildOf(room);
                    bool inTable = table != null && mr.transform.IsChildOf(table);
                    bool inChair = mr.GetComponentsInParent<Transform>().Any(t => t.name == "Chair");
                    bool isUnlit = mr.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name.Contains("Unlit"));

                    if (inDispenser || isUnlit)
                    {
                        GameObjectUtility.SetStaticEditorFlags(mr.gameObject, StaticEditorFlags.BatchingStatic);
                        mr.receiveGI = ReceiveGI.LightProbes;
                        mr.lightProbeUsage = LightProbeUsage.Off;
                        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        mr.shadowCastingMode = ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                        SetScaleInLightmap(mr, 0f);
                    }
                    else if (inItemMat)
                    {
                        GameObjectUtility.SetStaticEditorFlags(mr.gameObject, StaticEditorFlags.BatchingStatic);
                        mr.receiveGI = ReceiveGI.LightProbes;
                        mr.lightProbeUsage = LightProbeUsage.BlendProbes;
                        mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                        mr.shadowCastingMode = ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                        SetScaleInLightmap(mr, 0f);
                    }
                    else if (inRoom || inTable || inChair)
                    {
                        GameObjectUtility.SetStaticEditorFlags(mr.gameObject,
                            StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
                        mr.receiveGI = ReceiveGI.Lightmaps;
                        mr.lightProbeUsage = LightProbeUsage.Off;
                        mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                        mr.receiveShadows = true;

                        // Shell surfaces and emissive wall sconces do not cast self-occluding shadows during bake
                        bool noBakeShadowCast = mr.GetComponentsInParent<Transform>().Any(t =>
                            t.name == "Coffered Ceiling" || t.name.StartsWith("Continuous wall", StringComparison.Ordinal) ||
                            t.name == "Wall corner" || t.name == "Interior Checker Floor" || t.name == "Interior Rug" ||
                            t.name == "Interior Sconce");
                        mr.shadowCastingMode = noBakeShadowCast ? ShadowCastingMode.Off : ShadowCastingMode.On;

                        float scale = ComputeLightmapScale(mr.transform, inTable, inChair);
                        SetScaleInLightmap(mr, scale);
                    }
                    else
                    {
                        // Dynamic rigid meshes (Candies, Items, Dice, Cloche)
                        GameObjectUtility.SetStaticEditorFlags(mr.gameObject, 0);
                        mr.lightProbeUsage = LightProbeUsage.BlendProbes;
                        mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                        mr.shadowCastingMode = ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                    }
                }
                else if (r is SkinnedMeshRenderer smr)
                {
                    smr.lightProbeUsage = LightProbeUsage.BlendProbes;
                    smr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                    smr.shadowCastingMode = ShadowCastingMode.Off;
                    smr.receiveShadows = false;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(r))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
        }

        static float ComputeLightmapScale(Transform t, bool inTable, bool inChair)
        {
            if (inTable) return 1.0f;
            if (inChair) return 0.45f;
            string n = t.name;
            if (t.GetComponentsInParent<Transform>().Any(p => p.name.StartsWith("Interior CandyJar", StringComparison.Ordinal)))
                return 0.18f;
            if (t.GetComponentsInParent<Transform>().Any(p => p.name == "Coffered Ceiling" || p.name == "Wall corner"))
                return 0.22f;
            if (n == "Peach plaster" || n == "Teal wainscot" || t.GetComponentsInParent<Transform>().Any(p => p.name == "Interior Checker Floor" || p.name == "Interior Rug"))
                return 1.0f;
            if (n.Contains("rail") || n.Contains("inlay") || n.Contains("baseboard") || n.Contains("Cornice"))
                return 0.22f;
            return 0.50f;
        }

        static void SetScaleInLightmap(MeshRenderer mr, float scale)
        {
            var so = new SerializedObject(mr);
            var prop = so.FindProperty("m_ScaleInLightmap");
            if (prop != null) prop.floatValue = scale;
            var stitch = so.FindProperty("m_StitchLightmapSeams");
            if (stitch != null) stitch.boolValue = scale >= 0.4f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static ReflectionProbe AuthorParlourLightingRig(Transform root)
        {
            var oldRig = root.Find("Parlour Lighting");
            if (oldRig != null) UnityEngine.Object.DestroyImmediate(oldRig.gameObject);

            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.40f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.33f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.15f, 0.16f);
            RenderSettings.subtractiveShadowColor = new Color(0.24f, 0.20f, 0.26f);

            // Main Directional Light (Mixed Subtractive: straight-down studio overhead angle eliminates any two-tone corner seam across the 4 walls
            // while giving all 6 Chibi seats symmetric warm key lighting)
            var sun = UnityEngine.Object.FindObjectsByType<Light>().First(l => l.type == LightType.Directional);
            sun.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            sun.color = new Color(1.0f, 0.92f, 0.80f);
            sun.intensity = 0.48f;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.shadows = LightShadows.Hard;
            sun.shadowBias = 0.06f;
            sun.shadowNormalBias = 0.10f;
            SetLightSoftAngle(sun, 6.5f);
            RenderSettings.sun = sun;

            // Convert 6 Wall Sconces from invisible 0.06 realtime point lights -> glowing baked golden lanterns (0 runtime cost!)
            var room = root.Find("Room");
            if (room != null)
            {
                foreach (var l in room.GetComponentsInChildren<Light>(true))
                {
                    if (l.name != "Warm lantern fill") continue;
                    l.transform.localPosition = new Vector3(0f, 0.24f, 0.34f);
                    l.type = LightType.Point;
                    l.lightmapBakeType = LightmapBakeType.Baked;
                    l.color = new Color(1.0f, 0.76f, 0.32f);
                    l.intensity = 5.20f;
                    l.range = 2.55f;
                    l.shadows = LightShadows.Soft;
                    SetLightShadowRadius(l, 0.06f);
                }
            }

            var rig = new GameObject("Parlour Lighting").transform;
            rig.SetParent(root, false);

            // 1. Primary Baked Overhead Table Chandelier Spotlight (focused VIP golden pool over Roulette + player mats)
            var chandelier = CreateLight(rig, "Table Chandelier Spot", LightType.Spot, LightmapBakeType.Baked,
                new Vector3(0f, 3.26f, 0f), Quaternion.Euler(90f, 0f, 0f),
                new Color(1.0f, 0.91f, 0.72f), intensity: 4.10f, range: 5.5f, shadows: LightShadows.Soft);
            chandelier.spotAngle = 76f;
            chandelier.innerSpotAngle = 26f;
            SetLightShadowRadius(chandelier, 0.08f);

            // 2. Wide Baked Parlour Canopy Fill (keeps peach walls warm & inviting while vignetting outer corners)
            var canopy = CreateLight(rig, "Parlour Canopy Fill", LightType.Spot, LightmapBakeType.Baked,
                new Vector3(0f, 3.30f, 0f), Quaternion.Euler(90f, 0f, 0f),
                new Color(1.0f, 0.87f, 0.72f), intensity: 1.85f, range: 7.8f, shadows: LightShadows.Soft);
            canopy.spotAngle = 160f;
            canopy.innerSpotAngle = 88f;
            SetLightShadowRadius(canopy, 0.10f);

            // 3. Subtle Baked Evening Cool Rim Lights inside the two Arched Parlour Windows
            var winLeft = CreateLight(rig, "Window Twilight Left", LightType.Spot, LightmapBakeType.Baked,
                new Vector3(-3.34f, 1.48f, -0.55f), Quaternion.Euler(14f, 90f, 0f),
                new Color(0.56f, 0.75f, 1.0f), intensity: 0.90f, range: 3.8f, shadows: LightShadows.Soft);
            winLeft.spotAngle = 112f;
            winLeft.innerSpotAngle = 48f;

            var winRight = CreateLight(rig, "Window Twilight Right", LightType.Spot, LightmapBakeType.Baked,
                new Vector3(3.34f, 1.48f, -2.05f), Quaternion.Euler(14f, -90f, 0f),
                new Color(0.56f, 0.75f, 1.0f), intensity: 0.90f, range: 3.8f, shadows: LightShadows.Soft);
            winRight.spotAngle = 112f;
            winRight.innerSpotAngle = 48f;

            // 4. Single Realtime Per-Vertex Table Fill Point Light (0 shadows, gives Chibi hands/candies/items warm overhead lift)
            CreateLight(rig, "Table Warm PerVertex Fill", LightType.Point, LightmapBakeType.Realtime,
                new Vector3(0f, 2.05f, 0f), Quaternion.identity,
                new Color(1.0f, 0.89f, 0.70f), intensity: 0.30f, range: 3.8f, shadows: LightShadows.None);

            // 5. 62-Point LightProbeGroup sampling Chibi seats, tabletop, and Waiter corner
            var probeGo = new GameObject("Parlour Light Probes");
            probeGo.transform.SetParent(rig, false);
            var lpg = probeGo.AddComponent<LightProbeGroup>();
            lpg.probePositions = BuildProbePositions();

            // 6. 128px Box-Projected Parlour ReflectionProbe
            var reflGo = new GameObject("Parlour Reflection Probe");
            reflGo.transform.SetParent(rig, false);
            reflGo.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            var rp = reflGo.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Baked;
            rp.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            rp.boxProjection = true;
            rp.size = new Vector3(7.6f, 3.8f, 7.6f);
            rp.center = new Vector3(0f, 0.20f, 0f);
            rp.resolution = 128;
            rp.hdr = false;
            rp.intensity = 0.80f;
            rp.nearClipPlane = 0.2f;
            rp.farClipPlane = 12f;
            return rp;
        }

        static Light CreateLight(Transform parent, string name, LightType type, LightmapBakeType bakeType,
            Vector3 localPos, Quaternion localRot, Color color, float intensity, float range, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPos, localRot);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.lightmapBakeType = bakeType;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows;
            return l;
        }

        static Vector3[] BuildProbePositions()
        {
            var pts = new List<Vector3>(64)
            {
                // Center column above Roulette
                new Vector3(0f, 0.88f, 0f),
                new Vector3(0f, 1.55f, 0f),
            };
            // Inner table ring (6 mats / candies)
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                float s = -Mathf.Sin(a), c = -Mathf.Cos(a);
                pts.Add(new Vector3(s * 1.05f, 0.86f, c * 1.05f));
                pts.Add(new Vector3(s * 1.05f, 1.45f, c * 1.05f));
            }
            // Seat ring (6 Chibi heads, hands, and under-chair bounce)
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                float s = -Mathf.Sin(a), c = -Mathf.Cos(a);
                pts.Add(new Vector3(s * 1.68f, 0.45f, c * 1.68f));
                pts.Add(new Vector3(s * 1.68f, 1.05f, c * 1.68f));
                pts.Add(new Vector3(s * 1.68f, 1.55f, c * 1.68f));
                pts.Add(new Vector3(s * 1.68f, 2.05f, c * 1.68f));
            }
            // Outer room & Waiter ring (8 directions x 3 heights = 24 probes -> total = 2 + 12 + 24 + 24 = 62 probes)
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                float s = Mathf.Sin(a), c = Mathf.Cos(a);
                pts.Add(new Vector3(s * 2.85f, 0.40f, c * 2.85f));
                pts.Add(new Vector3(s * 2.85f, 1.45f, c * 2.85f));
                pts.Add(new Vector3(s * 2.85f, 2.55f, c * 2.85f));
            }
            return pts.ToArray();
        }

        static LightingSettings ConfigureLightingSettingsAsset()
        {
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingAssetPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "CandyParlour3D_Lighting" };
                AssetDatabase.CreateAsset(ls, LightingAssetPath);
            }
            ls.lightmapper = LightingSettings.Lightmapper.UnityComputeGPU;
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.mixedBakeMode = MixedLightingMode.Subtractive;
            ls.directionalityMode = LightmapsMode.NonDirectional;
            ls.lightmapMaxSize = 2048;
            ls.lightmapResolution = 10f;
            ls.lightmapPadding = 4;
            ls.directSampleCount = 24;
            ls.indirectSampleCount = 96;
            ls.environmentSampleCount = 64;
            ls.maxBounces = 3;
            ls.ao = true;
            ls.aoMaxDistance = 0.48f;
            ls.aoExponentIndirect = 1.35f;
            ls.aoExponentDirect = 0.35f;
            ls.filteringMode = LightingSettings.FilterMode.Auto;
            ls.lightmapCompression = LightmapCompression.NormalQuality;
            EditorUtility.SetDirty(ls);
            Lightmapping.lightingSettings = ls;
            return ls;
        }

        static void ConfigureBakedTextureCompression()
        {
            if (!AssetDatabase.IsValidFolder(SceneDataFolder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture", new[] { SceneDataFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool isRefl = path.EndsWith("ParlourReflection.exr", StringComparison.OrdinalIgnoreCase);
                ti.textureCompression = TextureImporterCompression.Compressed;
                var android = ti.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = isRefl ? 128 : 2048;
                android.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(android);
                ti.SaveAndReimport();
            }
        }

        static void SetLightSoftAngle(Light l, float degrees)
        {
            var so = new SerializedObject(l);
            var p = so.FindProperty("m_LightShadowCasterMode");
            var a = so.FindProperty("m_BakedShadowAngle");
            if (a != null) a.floatValue = degrees;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLightShadowRadius(Light l, float radius)
        {
            var so = new SerializedObject(l);
            var r = so.FindProperty("m_BakedShadowRadius");
            if (r != null) r.floatValue = radius;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
