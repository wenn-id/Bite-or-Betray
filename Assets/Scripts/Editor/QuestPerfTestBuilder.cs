using System;
using System.IO;
using System.Linq;
using BiteOrBetray.PerfTest;
using BiteOrBetray.Table3D;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiteOrBetray.EditorTools
{
    /// <summary>
    /// Quest performance test scene: a copy of CandyParlour3D with the same gameplay characters, components, and lightmaps,
    /// plus QuestPerfMonitor. Run from the menu or batchmode:
    /// Unity -batchmode -buildTarget Android -executeMethod BiteOrBetray.EditorTools.QuestPerfTestBuilder.BatchAll
    /// </summary>
    public static class QuestPerfTestBuilder
    {
        const string SourceScene = "Assets/BiteOrBetray/Scenes/CandyParlour3D.unity";
        const string TestScene = "Assets/BiteOrBetray/Scenes/QuestPerfTest.unity";
        const string ApkPath = "Builds/BiteOrBetray_QuestPerfTest.apk";
        const string ResultPath = "Temp/QuestPerfTest.result.txt";

        [MenuItem("Bite or Betray/Quest Perf Test/1. Build Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build QuestPerfTest in Edit Mode.");
            Directory.CreateDirectory("Temp");
            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            var src = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(src, TestScene, true);
            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);

            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "BoB_Art3D")
                       ?? throw new InvalidOperationException("BoB_Art3D not found in the scene.");
            var seats = root.transform.Find("Seats") ?? throw new InvalidOperationException("Seats not found.");
            var director = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CandyTableDirector>(true)).Single();
            // Enter the match immediately; keep the gameplay characters, animators, local rig, rules, and human seat.
            director.autoStart = true;
            director.mainMenu = false;
            director.roomSetup = false;
            director.onboarding = false;
            // results board on the table, in front of the player's seat (Seat_0)
            var seat0 = seats.GetChild(0);
            var toCenter = (-seat0.position); toCenter.y = 0; toCenter.Normalize();
            var board = new GameObject("PerfTest Board");
            board.transform.position = seat0.position + toCenter * .78f + Vector3.up * .80f;
            board.transform.rotation = Quaternion.LookRotation(toCenter) * Quaternion.Euler(50, 0, 0);
            var text = board.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            board.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            board.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            text.fontSize = 48; text.characterSize = .0045f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Left;
            text.color = new Color(.18f, .1f, .06f);
            text.text = "Quest perf test - waiting...";
            var monitor = board.AddComponent<QuestPerfMonitor>();
            monitor.label = text;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log($"Scene {TestScene}: characters and components copied from {SourceScene}; menu/setup/onboarding skipped.");
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
            // Old scenes may still contain PerfHero; always build the APK from a fresh gameplay copy.
            BuildScene();
            BuildCurrentSceneApk();
        }

        static void BuildCurrentSceneApk()
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
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("APK build failed, see the log.");
        }

        public static void BatchAll()
        {
            try { Directory.CreateDirectory("Temp"); File.WriteAllText(ResultPath, ""); BuildScene(); Capture(); BuildCurrentSceneApk(); Log("DONE"); }
            catch (Exception e) { Log("FAILED " + e); EditorApplication.Exit(1); }
        }

        // ---------------- budget audit (without a headset) ----------------
        const int TriangleBudget = 250000;
        const int DrawBudget = 100;
        const int MaterialBudget = 50;
        const string AuditPath = "Builds/reports/QuestBudgetAudit.txt";

        [MenuItem("Bite or Betray/Quest Perf Test/4. Budget Audit")]
        public static void Audit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run Budget Audit in Edit Mode.");
            Directory.CreateDirectory("Temp");
            Directory.CreateDirectory(Path.GetDirectoryName(AuditPath));
            var sb = new System.Text.StringBuilder();
            bool passed = true;
            sb.AppendLine("== Conservative Quest budget (static estimate, not proof of 72 FPS) ==");
            sb.AppendLine($"Target per view/eye: <= {TriangleBudget:N0} triangles, <= {DrawBudget} draws including shadows, <= {MaterialBudget} materials, <= {DrawBudget} estimated SetPass calls.");
            sb.AppendLine("FOV 110, 8 yaw directions x 3 pitches (-30/0/30) from Seat_0, height 1.15 m; no occlusion/LOD/batching discount; local avatar head hidden from its own seat.");
            sb.AppendLine("One color draw per slot/submesh; count all scene casters once for main light shadows (also fully allocated to each eye). ");
            sb.AppendLine("SetPass = upper bound of one change per draw, NOT a GPU measurement; SRP Batcher/instancing is not assumed to reduce draws.");
            sb.AppendLine("Objects/animations/UI/particles appearing during Play are not covered yet: compare Frame Debugger/Profiler during laptop gameplay.");
            try
            {
                var rp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                if (rp == null) throw new InvalidOperationException("Mobile_RPAsset not imported.");
                sb.AppendLine($"\nMobile RP: scale={rp.renderScale}, MSAA={rp.msaaSampleCount}x, HDR={rp.supportsHDR}, shadow={rp.mainLightShadowmapResolution}/{rp.shadowDistance}m/{rp.shadowCascadeCount} cascade, " +
                              $"addLights={rp.additionalLightsRenderingMode}, additionalShadow={rp.supportsAdditionalLightShadows}, depth={rp.supportsCameraDepthTexture}, opaque={rp.supportsCameraOpaqueTexture}, SRPBatcher={rp.useSRPBatcher}");
                passed &= BudgetCheck(sb, "Conservative URP", Mathf.Approximately(rp.renderScale, 1f) && rp.msaaSampleCount <= 4 &&
                    !rp.supportsHDR && rp.supportsMainLightShadows && rp.mainLightShadowmapResolution <= 512 && rp.shadowDistance <= 6 && rp.shadowCascadeCount == 1 &&
                    rp.additionalLightsRenderingMode != UnityEngine.Rendering.Universal.LightRenderingMode.PerPixel && !rp.supportsAdditionalLightShadows &&
                    !rp.supportsSoftShadows && !rp.supportsCameraDepthTexture && !rp.supportsCameraOpaqueTexture && rp.useSRPBatcher);
                var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
                passed &= BudgetCheck(sb, "Mobile renderer without SSAO/renderer features", renderer != null && !renderer.rendererFeatures.Any(f => f != null && f.isActive));
                for (int q = 0; q < QualitySettings.names.Length; q++)
                {
                    var qrp = QualitySettings.GetRenderPipelineAssetAt(q);
                    sb.AppendLine($"Quality[{q}] {QualitySettings.names[q]}: RP={(qrp ? AssetDatabase.GetAssetPath(qrp) : "default")}");
                    if (QualitySettings.names[q] == "Mobile" || QualitySettings.names[q] == "Meta Quest (Build Profile)")
                        passed &= BudgetCheck(sb, "Quest quality pipeline", (qrp ? qrp : GraphicsSettings.defaultRenderPipeline) == rp);
                }
                bool staticBatching = PlayerSettings.GetStaticBatchingForPlatform(BuildTarget.Android);
                passed &= BudgetCheck(sb, $"Static batching Android={staticBatching}", staticBatching);
                sb.AppendLine("Runtime VRTableInput: OVRManager FFR High + dynamic, eye-tracked off, dynamic resolution off, XR scale 1.0; check while Android XR is active.");
                foreach (var scenePath in new[] { SourceScene, TestScene })
                {
                    // Preview scenes do not close/discard the owner's in-progress scene changes.
                    var scene = EditorSceneManager.OpenPreviewScene(scenePath);
                    try { passed &= AuditScene(scene, sb, rp.supportsMainLightShadows); }
                    catch (Exception e) { passed = false; sb.AppendLine($"FAIL {scenePath}: {e.Message}"); }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                }
            }
            catch (Exception e) { passed = false; sb.AppendLine("FAIL: " + e); }
            sb.AppendLine(passed ? "\nRESULT: PASS static budget" : "\nRESULT: FAIL static budget");
            File.WriteAllText("Temp/QuestBudgetAudit.txt", sb.ToString());
            File.WriteAllText(AuditPath, sb.ToString());
            Log("Audit: " + AuditPath + (passed ? " (PASS)" : " (FAIL)"));
            if (!passed) throw new InvalidOperationException("Quest budget FAILED. Report: " + AuditPath);
        }

        static bool BudgetCheck(System.Text.StringBuilder sb, string label, bool passed)
        {
            sb.AppendLine($"{(passed ? "PASS" : "FAIL")}: {label}");
            return passed;
        }

        static bool AuditScene(UnityEngine.SceneManagement.Scene scene, System.Text.StringBuilder sb, bool mainShadows)
        {
            sb.AppendLine($"\n== {Path.GetFileNameWithoutExtension(scene.path)} ==");
            var roots = scene.GetRootGameObjects();
            var root = roots.FirstOrDefault(g => g.name == "BoB_Art3D");
            var seat0 = root != null ? root.transform.Find("Seats/Seat_0") : null;
            if (seat0 == null) throw new InvalidOperationException("BoB_Art3D/Seats/Seat_0 not found.");
            var allRenderers = roots.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();
            var renderers = allRenderers.Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff).ToArray();
            sb.AppendLine($"Renderers counted={renderers.Length}, disabled/forceRenderingOff={allRenderers.Length - renderers.Length}; active dynamic geometry still fails.");
            // The local avatar head is never drawn by the first-person camera: LocalAvatarTracking sets forceRenderingOff on
            // desktop and moves it to a camera-culled layer in XR (CameraSetupTests). Still included in scene/shadow totals.
            var localHeads = new System.Collections.Generic.HashSet<Renderer>(roots.SelectMany(g => g.GetComponentsInChildren<LocalAvatarTracking>(true))
                .Where(t => t.enabled && t.gameObject.activeInHierarchy && t.avatar != null && t.firstPersonCamera != null)
                .SelectMany(t => t.avatar.headRenderers ?? Array.Empty<Renderer>()).Where(r => r != null));
            var lights = roots.SelectMany(g => g.GetComponentsInChildren<Light>(true))
                .Where(l => l.isActiveAndEnabled && l.lightmapBakeType != LightmapBakeType.Baked).ToArray();
            var shadowLights = lights.Where(l => l.shadows != LightShadows.None).ToArray();
            bool passed = BudgetCheck(sb, $"Realtime lights={lights.Length}, shadows={shadowLights.Length} (at most one directional)",
                shadowLights.Length <= 1 && shadowLights.All(l => l.type == LightType.Directional));
            bool shadows = mainShadows && shadowLights.Length > 0;
            var triangles = new System.Collections.Generic.Dictionary<Renderer, long>();
            var draws = new System.Collections.Generic.Dictionary<Renderer, int>();
            int shadowDraws = 0, unknown = 0;
            long shadowTriangles = 0;
            foreach (var r in renderers)
            {
                var text = r.GetComponent<TextMesh>();
                // TextMesh has no MeshFilter; do not call the sharedMesh binding on a missing component.
                r.TryGetComponent<MeshFilter>(out var filter);
                var mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
                var materials = r.sharedMaterials;
                int d = mesh != null ? Math.Max(mesh.subMeshCount, materials.Length) : materials.Length;
                long t = 0;
                if (text != null) t = (long)text.text.Length * 2;
                else if (mesh == null || mesh.subMeshCount == 0 || r is ParticleSystemRenderer)
                { unknown++; sb.AppendLine($"FAIL unavailable/dynamic geometry: {r.name} ({r.GetType().Name})"); }
                else
                    for (int i = 0; i < d; i++)
                    {
                        int sub = Math.Min(i, mesh.subMeshCount - 1);
                        if (mesh.GetTopology(sub) != MeshTopology.Triangles) { unknown++; continue; }
                        t += (long)mesh.GetIndexCount(sub) / 3;
                    }
                if (materials.Length == 0 || materials.Any(m => m == null || m.shader == null || !m.shader.isSupported))
                { unknown++; sb.AppendLine($"FAIL missing/unsupported material/shader: {r.name}"); }
                triangles[r] = t; draws[r] = d;
                if (shadows && r.shadowCastingMode != ShadowCastingMode.Off)
                { shadowDraws += d; shadowTriangles += t; }
            }
            passed &= BudgetCheck(sb, $"Complete geometry/materials (unknown={unknown})", unknown == 0);
            var room = root.transform.Find("Room");
            var roomRenderers = room != null ? room.GetComponentsInChildren<MeshRenderer>(true) : Array.Empty<MeshRenderer>();
            passed &= BudgetCheck(sb, $"Room BatchingStatic renderers ({roomRenderers.Length})", roomRenderers.Length > 0 &&
                roomRenderers.All(r => GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic)));
            sb.AppendLine($"Whole scene: {triangles.Values.Sum():N0} triangles, {draws.Values.Sum()} color draws; conservative shadows {shadowDraws} draws/{shadowTriangles:N0} triangles.");
            sb.AppendLine($"Local avatar head (hidden from first-person camera): {localHeads.Count(draws.ContainsKey)} renderers/{localHeads.Where(draws.ContainsKey).Sum(r => draws[r])} draws excluded per eye.");
            sb.AppendLine($"Skinned mesh: {renderers.OfType<SkinnedMeshRenderer>().Count()} | vertex: {renderers.OfType<SkinnedMeshRenderer>().Sum(r => r.sharedMesh != null ? (long)r.sharedMesh.vertexCount : 0):N0}");
            var materialsAll = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            sb.AppendLine($"Unique scene materials: {materialsAll.Length}, instancing enabled: {materialsAll.Count(m => m.enableInstancing)} (SkinnedMeshRenderer does not use GPU instancing; SRP/static batching may be preferred).");
            var textures = materialsAll.SelectMany(m => m.GetTexturePropertyNameIDs().Select(m.GetTexture)).Where(t => t != null).Distinct().ToArray();
            sb.AppendLine($"Scene textures: {textures.Length}, estimated Editor memory {textures.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)) / 1048576f:0.0} MB; transparent materials: {materialsAll.Count(m => m.renderQueue >= 3000)}");
            sb.AppendLine("Shader: " + string.Join(", ", materialsAll.Where(m => m.shader != null).Select(m => m.shader.name).Distinct()));
            sb.AppendLine($"Volume post-process: {roots.Sum(g => g.GetComponentsInChildren<Volume>(true).Length)}");
            var go = new GameObject("AuditCam");
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                var cam = go.AddComponent<Camera>(); cam.enabled = false;
                cam.fieldOfView = 110; cam.aspect = 1f; cam.nearClipPlane = .05f; cam.farClipPlane = 30;
                go.transform.position = seat0.position + Vector3.up * 1.15f;
                var forward = Quaternion.LookRotation(new Vector3(0, .85f, 0) - go.transform.position);
                long maxTris = 0; int maxDraws = 0, maxMats = 0;
                foreach (int pitch in new[] { -30, 0, 30 })
                    for (int yaw = 0; yaw < 360; yaw += 45)
                    {
                        go.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * forward * Quaternion.Euler(pitch, 0, 0);
                        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                        var visible = renderers.Where(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !localHeads.Contains(r) && GeometryUtility.TestPlanesAABB(planes, r.bounds)).ToArray();
                        long tris = visible.Sum(r => triangles[r]);
                        int colorDraws = visible.Sum(r => draws[r]), total = colorDraws + shadowDraws;
                        int mats = visible.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count();
                        bool viewPassed = tris <= TriangleBudget && total <= DrawBudget && mats <= MaterialBudget;
                        passed &= viewPassed;
                        sb.AppendLine($"{(viewPassed ? "PASS" : "FAIL")} yaw={yaw}, pitch={pitch}: triangles={tris:N0}/{TriangleBudget}, draws/eye={colorDraws}+{shadowDraws}={total}/{DrawBudget}, materials={mats}/{MaterialBudget}, estimated SetPass<={total}/{DrawBudget}");
                        maxTris = Math.Max(maxTris, tris); maxDraws = Math.Max(maxDraws, total); maxMats = Math.Max(maxMats, mats);
                    }
                sb.AppendLine($"Maximum across views: {maxTris:N0} triangles, {maxDraws} draws/eye, {maxMats} materials, estimated SetPass<={maxDraws}.");
                sb.AppendLine("Largest renderers (whole scene): " + string.Join(", ", triangles.OrderByDescending(p => p.Value).Take(8).Select(p => $"{p.Key.name}={p.Value:N0} tri/{draws[p.Key]} draw")));
                sb.AppendLine("Most draws (whole scene): " + string.Join(", ", draws.OrderByDescending(p => p.Value).Take(12)
                    .Select(p => $"{p.Key.name}={p.Value} color/{(shadows && p.Key.shadowCastingMode != ShadowCastingMode.Off ? p.Value : 0)} shadow")));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            return passed;
        }

        public static void BatchAudit()
        {
            try { File.WriteAllText(ResultPath, ""); Audit(); Log("DONE"); }
            catch (Exception e) { Log("FAILED " + e); EditorApplication.Exit(1); }
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
