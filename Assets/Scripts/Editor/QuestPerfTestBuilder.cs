using System;
using System.IO;
using System.Linq;
using BiteOrBetray.PerfTest;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiteOrBetray.EditorTools
{
    /// <summary>
    /// Scene tes performa Quest: salinan CandyParlour3D (ruangan + lightmap yang sama) dengan
    /// 5 hero full-body duduk + 1 hero pelayan berdiri, plus QuestPerfMonitor. Bisa dari menu atau batchmode:
    /// Unity -batchmode -buildTarget Android -executeMethod BiteOrBetray.EditorTools.QuestPerfTestBuilder.BatchAll
    /// </summary>
    public static class QuestPerfTestBuilder
    {
        const string HeroDir = "Assets/BiteOrBetray/Art3D/Hero/";
        const string HeroFbx = HeroDir + "Hero_Quest.fbx";
        const string SourceScene = "Assets/BiteOrBetray/Scenes/CandyParlour3D.unity";
        const string TestScene = "Assets/BiteOrBetray/Scenes/QuestPerfTest.unity";
        const string ApkPath = "Builds/BiteOrBetray_QuestPerfTest.apk";
        const string ResultPath = "Temp/QuestPerfTest.result.txt";

        [MenuItem("Bite or Betray/Quest Perf Test/1. Build Scene")]
        public static void BuildScene()
        {
            ConfigureAssets();
            var material = HeroMaterial();
            var sit = Controller("HeroSit", "SitIdle");
            var stand = Controller("HeroStand", "StandIdle");

            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            var src = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(src, TestScene, true);
            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);

            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "BoB_Art3D")
                       ?? throw new InvalidOperationException("BoB_Art3D tidak ada di scene.");
            var seats = root.transform.Find("Seats") ?? throw new InvalidOperationException("Seats tidak ada.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HeroFbx) ?? throw new InvalidOperationException("Hero_Quest.fbx belum ter-import.");

            var renderers = new System.Collections.Generic.List<Renderer>();
            var animators = new System.Collections.Generic.List<Animator>();
            GameObject Spawn(Transform parent, Vector3 pos, Quaternion rot, RuntimeAnimatorController ctrl, string name)
            {
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                hero.name = name;
                hero.transform.SetPositionAndRotation(pos, rot);
                foreach (var smr in hero.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    smr.sharedMaterial = material;
                    smr.quality = SkinQuality.Bone4;
                    smr.updateWhenOffscreen = false;
                    smr.skinnedMotionVectors = false;
                    smr.lightProbeUsage = LightProbeUsage.BlendProbes;
                    smr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                    smr.shadowCastingMode = ShadowCastingMode.On;
                    renderers.Add(smr);
                }
                var anim = hero.GetComponent<Animator>(); if (anim == null) anim = hero.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animators.Add(anim);
                return hero;
            }
            // 5 kursi lawan: sembunyikan bot kepala-melayang (scriptnya tetap hidup), ganti hero duduk
            for (int i = 1; i < seats.childCount; i++)
            {
                var seat = seats.GetChild(i);
                foreach (Transform child in seat)
                    if (child.name.StartsWith("Bot_"))
                        foreach (var r in child.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                Spawn(seat, seat.position, seat.rotation, sit, "PerfHero_Seat" + i);
            }
            var waiter = root.transform.Find("Waiter");
            if (waiter != null)
            {
                foreach (var r in waiter.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var p = waiter.position; var look = new Vector3(-p.x, 0, -p.z);
                Spawn(root.transform, new Vector3(p.x, 0, p.z), Quaternion.LookRotation(look), stand, "PerfHero_Waiter");
            }
            // papan hasil di meja, di depan kursi pemain (Seat_0)
            var seat0 = seats.GetChild(0);
            var toCenter = (-seat0.position); toCenter.y = 0; toCenter.Normalize();
            var board = new GameObject("PerfTest Board");
            board.transform.position = seat0.position + toCenter * .78f + Vector3.up * .80f;
            board.transform.rotation = Quaternion.LookRotation(toCenter) * Quaternion.Euler(50, 0, 0);
            var text = board.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            board.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 48; text.characterSize = .0045f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Left;
            text.color = new Color(.18f, .1f, .06f);
            text.text = "Quest perf test - menunggu...";
            var monitor = board.AddComponent<QuestPerfMonitor>();
            monitor.label = text;
            monitor.heroRenderers = renderers.ToArray();
            monitor.heroAnimators = animators.ToArray();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log($"Scene {TestScene}: {renderers.Count} hero renderer, {animators.Count} animator, " +
                $"{renderers.Sum(r => ((SkinnedMeshRenderer)r).sharedMesh.triangles.Length / 3)} segitiga hero");
        }

        [MenuItem("Bite or Betray/Quest Perf Test/2. Capture Preview")]
        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);
            var seat0 = GameObject.Find("BoB_Art3D/Seats/Seat_0").transform;
            var go = new GameObject("PreviewCam"); var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 85; cam.nearClipPlane = .05f;
            var eye = seat0.position + Vector3.up * 1.15f;
            go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(new Vector3(0, .85f, 0) - eye + Vector3.up * .15f));
            Shot(cam, "Temp/QuestPerfTest_PlayerView.png", 1600, 900);
            go.transform.SetPositionAndRotation(new Vector3(0, 2.6f, -2.6f), Quaternion.Euler(38, 0, 0));
            cam.fieldOfView = 60;
            Shot(cam, "Temp/QuestPerfTest_Overview.png", 1600, 900);
            UnityEngine.Object.DestroyImmediate(go);
            Log("Preview: Temp/QuestPerfTest_PlayerView.png, Temp/QuestPerfTest_Overview.png");
        }

        [MenuItem("Bite or Betray/Quest Perf Test/3. Build APK")]
        public static void BuildApk()
        {
            PlayerSettings.enableFrameTimingStats = true;
            AssetDatabase.SaveAssets();
            string output = Path.GetFullPath(ApkPath);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { TestScene }, target = BuildTarget.Android, locationPathName = output, options = BuildOptions.None
            });
            Log($"APK {report.summary.result}; errors={report.summary.totalErrors}; bytes={report.summary.totalSize}; path={output}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build APK gagal, lihat log.");
        }

        public static void BatchAll()
        {
            try { File.WriteAllText(ResultPath, ""); BuildScene(); Capture(); BuildApk(); Log("DONE"); }
            catch (Exception e) { Log("FAILED " + e); EditorApplication.Exit(1); }
        }

        // ---------------- budget audit (tanpa headset) ----------------
        [MenuItem("Bite or Betray/Quest Perf Test/4. Budget Audit")]
        public static void Audit()
        {
            var sb = new System.Text.StringBuilder();
            var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            sb.AppendLine("== Render settings ==");
            sb.AppendLine($"Default RP asset: {(rp ? AssetDatabase.GetAssetPath(rp) : "none")}");
            for (int q = 0; q < QualitySettings.names.Length; q++)
            {
                var qrp = QualitySettings.GetRenderPipelineAssetAt(q);
                sb.AppendLine($"Quality[{q}] {QualitySettings.names[q]}: RP={(qrp ? AssetDatabase.GetAssetPath(qrp) + $" (exists)" : "NULL -> pakai default")}");
            }
            foreach (var path in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset").Select(AssetDatabase.GUIDToAssetPath))
            {
                var a = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(path);
                sb.AppendLine($"  {path}: renderScale={a.renderScale}, MSAA={a.msaaSampleCount}x, HDR={a.supportsHDR}, mainShadows={a.supportsMainLightShadows} res={a.mainLightShadowmapResolution}, " +
                              $"addLights={a.additionalLightsRenderingMode}, softShadows={a.supportsSoftShadows}, depthTex={a.supportsCameraDepthTexture}, opaqueTex={a.supportsCameraOpaqueTexture}, SRPBatcher={a.useSRPBatcher}");
            }
            foreach (var scenePath in new[] { SourceScene, TestScene })
            {
                if (!File.Exists(scenePath)) continue;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var seat0 = GameObject.Find("BoB_Art3D/Seats/Seat_0").transform;
                var go = new GameObject("AuditCam"); var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 100; cam.aspect = 1f; cam.nearClipPlane = .05f; cam.farClipPlane = 30;
                var eye = seat0.position + Vector3.up * 1.15f;
                go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(new Vector3(0, .85f, 0) - eye));
                var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                int visible = 0, draws = 0, tris = 0, shadowDraws = 0, transparent = 0, skinned = 0, skinnedVerts = 0, realtimeLights = 0;
                var mats = new System.Collections.Generic.HashSet<Material>();
                var shaders = new System.Collections.Generic.HashSet<Shader>();
                var textures = new System.Collections.Generic.HashSet<Texture>();
                int allTris = 0, allDraws = 0;
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!r.enabled || r is ParticleSystemRenderer) continue;
                    Mesh mesh = null;
                    if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                    else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                    int t = mesh != null ? (int)Enumerable.Range(0, mesh.subMeshCount).Sum(i => (long)mesh.GetIndexCount(i) / 3) : 2;
                    int d = r.sharedMaterials.Length;
                    allTris += t; allDraws += d;
                    if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                    visible++; draws += d; tris += t;
                    if (r.shadowCastingMode != ShadowCastingMode.Off) shadowDraws += d;
                    if (r is SkinnedMeshRenderer s2) { skinned++; skinnedVerts += s2.sharedMesh.vertexCount; }
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        mats.Add(m); shaders.Add(m.shader);
                        if (m.renderQueue >= 3000) transparent++;
                        foreach (var id in m.GetTexturePropertyNameIDs()) { var tx = m.GetTexture(id); if (tx != null) textures.Add(tx); }
                    }
                }
                foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.enabled && l.lightmapBakeType != LightmapBakeType.Baked) realtimeLights++;
                long texBytes = textures.Sum(t => (long)UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
                var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
                UnityEngine.Object.DestroyImmediate(go);
                sb.AppendLine($"\n== {Path.GetFileNameWithoutExtension(scenePath)} (dilihat dari kursi pemain, FOV 100) ==");
                sb.AppendLine($"Renderer terlihat: {visible} | draw call (submesh): {draws} | +shadow pass: {shadowDraws} | segitiga terlihat: {tris:N0} (seluruh scene {allTris:N0}, {allDraws} draw)");
                sb.AppendLine($"Material unik: {mats.Count} | shader unik: {shaders.Count} | material transparan: {transparent}");
                sb.AppendLine($"Skinned mesh: {skinned} ({skinnedVerts:N0} vertex di-skin per frame) | lampu non-baked: {realtimeLights} | Volume post-process: {volumes.Length}");
                sb.AppendLine($"Tekstur dipakai yang terlihat: {textures.Count}, memori (editor, perkiraan): {texBytes / 1048576f:0.0} MB");
                sb.AppendLine("Shader: " + string.Join(", ", shaders.Select(s => s.name).Distinct()));
            }
            File.WriteAllText("Temp/QuestBudgetAudit.txt", sb.ToString());
            Log("Audit: Temp/QuestBudgetAudit.txt");
        }

        public static void BatchAudit()
        {
            try { File.WriteAllText(ResultPath, ""); Audit(); Log("DONE"); }
            catch (Exception e) { Log("FAILED " + e); EditorApplication.Exit(1); }
        }

        // ---------------- assets ----------------
        static void ConfigureAssets()
        {
            AssetDatabase.Refresh();
            foreach (var (file, normal, size) in new[] { ("HeroQ_BaseColor.png", false, 2048), ("HeroQ_Normal.png", true, 1024), ("HeroQ_Roughness.png", false, 512) })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(HeroDir + file);
                if (ti == null) continue;
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !normal && !file.Contains("Roughness");
                ti.maxTextureSize = size; ti.mipmapEnabled = true;
                var android = ti.GetPlatformTextureSettings("Android");
                android.overridden = true; android.maxTextureSize = size; android.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(android);
                ti.SaveAndReimport();
            }
            var mi = (ModelImporter)AssetImporter.GetAtPath(HeroFbx) ?? throw new InvalidOperationException("Hero_Quest.fbx tidak ditemukan.");
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = true;
            mi.importBlendShapes = false;
            mi.importCameras = false; mi.importLights = false;
            mi.optimizeGameObjects = true;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips) { c.loopTime = true; c.name = c.name.Contains("Sit") ? "SitIdle" : c.name.Contains("Stand") ? "StandIdle" : c.name; }
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
        }

        static Material HeroMaterial()
        {
            string path = HeroDir + "HeroQ.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(HeroDir + "HeroQ_BaseColor.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(HeroDir + "HeroQ_Normal.png"));
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Metallic", 0); mat.SetFloat("_Smoothness", .32f);
            mat.enableInstancing = false;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static AnimatorController Controller(string name, string clipName)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(HeroFbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName && !c.name.StartsWith("__preview"))
                       ?? throw new InvalidOperationException($"Clip {clipName} tidak ada di Hero_Quest.fbx");
            string path = HeroDir + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
        }

        static void Shot(Camera cam, string path, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
        }

        static void Log(string msg)
        {
            Debug.Log("[QuestPerfTest] " + msg);
            File.AppendAllText(ResultPath, msg + "\n");
        }
    }
}
