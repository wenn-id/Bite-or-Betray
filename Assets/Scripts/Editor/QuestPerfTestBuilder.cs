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
    /// Scene tes performa Quest: salinan CandyParlour3D dengan karakter, komponen, dan lightmap gameplay
    /// yang sama, plus QuestPerfMonitor. Bisa dari menu atau batchmode:
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
                throw new InvalidOperationException("Bangun QuestPerfTest di Edit Mode.");
            Directory.CreateDirectory("Temp");
            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            var src = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(src, TestScene, true);
            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);

            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "BoB_Art3D")
                       ?? throw new InvalidOperationException("BoB_Art3D tidak ada di scene.");
            var seats = root.transform.Find("Seats") ?? throw new InvalidOperationException("Seats tidak ada.");
            var director = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CandyTableDirector>(true)).Single();
            // Langsung masuk pertandingan; karakter, animator, rig lokal, aturan, dan kursi manusia tetap dari gameplay.
            director.autoStart = true;
            director.mainMenu = false;
            director.roomSetup = false;
            director.onboarding = false;
            // papan hasil di meja, di depan kursi pemain (Seat_0)
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
            text.text = "Quest perf test - menunggu...";
            var monitor = board.AddComponent<QuestPerfMonitor>();
            monitor.label = text;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log($"Scene {TestScene}: karakter dan komponen disalin dari {SourceScene}; menu/setup/onboarding dilewati.");
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
            // Scene lama masih bisa berisi PerfHero; APK selalu dibangun dari salinan gameplay terbaru.
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
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build APK gagal, lihat log.");
        }

        public static void BatchAll()
        {
            try { Directory.CreateDirectory("Temp"); File.WriteAllText(ResultPath, ""); BuildScene(); Capture(); BuildCurrentSceneApk(); Log("DONE"); }
            catch (Exception e) { Log("FAILED " + e); EditorApplication.Exit(1); }
        }

        // ---------------- budget audit (tanpa headset) ----------------
        const int TriangleBudget = 250000;
        const int DrawBudget = 100;
        const int MaterialBudget = 50;
        const string AuditPath = "Builds/reports/QuestBudgetAudit.txt";

        [MenuItem("Bite or Betray/Quest Perf Test/4. Budget Audit")]
        public static void Audit()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Jalankan Budget Audit di Edit Mode.");
            Directory.CreateDirectory("Temp");
            Directory.CreateDirectory(Path.GetDirectoryName(AuditPath));
            var sb = new System.Text.StringBuilder();
            bool passed = true;
            sb.AppendLine("== Budget Quest konservatif (estimasi statis, bukan bukti 72 FPS) ==");
            sb.AppendLine($"Target per pandangan/mata: <= {TriangleBudget:N0} segitiga, <= {DrawBudget} draw termasuk shadow, <= {MaterialBudget} material, <= {DrawBudget} estimasi SetPass.");
            sb.AppendLine("FOV 110, 8 arah yaw x 3 pitch (-30/0/30) dari Seat_0, tinggi 1,15 m; tanpa occlusion/LOD/batching discount; kepala avatar lokal tidak terlihat dari kursinya sendiri.");
            sb.AppendLine("Satu draw warna per slot/submesh; seluruh caster scene dihitung sekali untuk shadow main light (juga dialokasikan penuh ke tiap mata). ");
            sb.AppendLine("SetPass = batas atas satu pergantian per draw, BUKAN pengukuran GPU; SRP Batcher/instancing tidak diasumsikan mengurangi draw.");
            sb.AppendLine("Objek/animasi/UI/partikel yang muncul saat Play belum tercakup: cocokkan Frame Debugger/Profiler saat gameplay di laptop.");
            try
            {
                var rp = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                if (rp == null) throw new InvalidOperationException("Mobile_RPAsset tidak ter-import.");
                sb.AppendLine($"\nMobile RP: scale={rp.renderScale}, MSAA={rp.msaaSampleCount}x, HDR={rp.supportsHDR}, shadow={rp.mainLightShadowmapResolution}/{rp.shadowDistance}m/{rp.shadowCascadeCount} cascade, " +
                              $"addLights={rp.additionalLightsRenderingMode}, additionalShadow={rp.supportsAdditionalLightShadows}, depth={rp.supportsCameraDepthTexture}, opaque={rp.supportsCameraOpaqueTexture}, SRPBatcher={rp.useSRPBatcher}");
                passed &= BudgetCheck(sb, "URP konservatif", Mathf.Approximately(rp.renderScale, 1f) && rp.msaaSampleCount <= 4 &&
                    !rp.supportsHDR && rp.supportsMainLightShadows && rp.mainLightShadowmapResolution <= 512 && rp.shadowDistance <= 6 && rp.shadowCascadeCount == 1 &&
                    rp.additionalLightsRenderingMode != UnityEngine.Rendering.Universal.LightRenderingMode.PerPixel && !rp.supportsAdditionalLightShadows &&
                    !rp.supportsSoftShadows && !rp.supportsCameraDepthTexture && !rp.supportsCameraOpaqueTexture && rp.useSRPBatcher);
                var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
                passed &= BudgetCheck(sb, "Mobile renderer tanpa SSAO/renderer feature", renderer != null && !renderer.rendererFeatures.Any(f => f != null && f.isActive));
                for (int q = 0; q < QualitySettings.names.Length; q++)
                {
                    var qrp = QualitySettings.GetRenderPipelineAssetAt(q);
                    sb.AppendLine($"Quality[{q}] {QualitySettings.names[q]}: RP={(qrp ? AssetDatabase.GetAssetPath(qrp) : "default")}");
                    if (QualitySettings.names[q] == "Mobile" || QualitySettings.names[q] == "Meta Quest (Build Profile)")
                        passed &= BudgetCheck(sb, "Pipeline kualitas Quest", (qrp ? qrp : GraphicsSettings.defaultRenderPipeline) == rp);
                }
                bool staticBatching = PlayerSettings.GetStaticBatchingForPlatform(BuildTarget.Android);
                passed &= BudgetCheck(sb, $"Static batching Android={staticBatching}", staticBatching);
                sb.AppendLine("Runtime VRTableInput: OVRManager FFR High + dynamic, eye-tracked off, dynamic resolution off, skala XR 1.0; periksa saat Android XR aktif.");
                foreach (var scenePath in new[] { SourceScene, TestScene })
                {
                    // Preview scene tidak menutup/membuang perubahan scene yang sedang dikerjakan owner.
                    var scene = EditorSceneManager.OpenPreviewScene(scenePath);
                    try { passed &= AuditScene(scene, sb, rp.supportsMainLightShadows); }
                    catch (Exception e) { passed = false; sb.AppendLine($"GAGAL {scenePath}: {e.Message}"); }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                }
            }
            catch (Exception e) { passed = false; sb.AppendLine("GAGAL: " + e); }
            sb.AppendLine(passed ? "\nHASIL: LULUS budget statis; verifikasi gameplay Unity masih diperlukan." : "\nHASIL: GAGAL budget statis; lihat batas/objek yang dilaporkan.");
            File.WriteAllText("Temp/QuestBudgetAudit.txt", sb.ToString());
            File.WriteAllText(AuditPath, sb.ToString());
            Log("Audit: " + AuditPath + (passed ? " (LULUS)" : " (GAGAL)"));
            if (!passed) throw new InvalidOperationException("Budget Quest GAGAL. Laporan: " + AuditPath);
        }

        static bool BudgetCheck(System.Text.StringBuilder sb, string label, bool passed)
        {
            sb.AppendLine($"{(passed ? "LULUS" : "GAGAL")}: {label}");
            return passed;
        }

        static bool AuditScene(UnityEngine.SceneManagement.Scene scene, System.Text.StringBuilder sb, bool mainShadows)
        {
            sb.AppendLine($"\n== {Path.GetFileNameWithoutExtension(scene.path)} ==");
            var roots = scene.GetRootGameObjects();
            var root = roots.FirstOrDefault(g => g.name == "BoB_Art3D");
            var seat0 = root != null ? root.transform.Find("Seats/Seat_0") : null;
            if (seat0 == null) throw new InvalidOperationException("BoB_Art3D/Seats/Seat_0 tidak ditemukan.");
            var allRenderers = roots.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();
            var renderers = allRenderers.Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff).ToArray();
            sb.AppendLine($"Renderer dihitung={renderers.Length}, nonaktif/forceRenderingOff={allRenderers.Length - renderers.Length}; geometri dinamis yang masih aktif tetap gagal.");
            // Kepala avatar lokal tidak pernah digambar kamera first-person: LocalAvatarTracking memberi forceRenderingOff di
            // desktop dan memindahkannya ke layer yang di-cull kamera saat XR (CameraSetupTests). Tetap masuk total scene/shadow.
            var localHeads = new System.Collections.Generic.HashSet<Renderer>(roots.SelectMany(g => g.GetComponentsInChildren<LocalAvatarTracking>(true))
                .Where(t => t.enabled && t.gameObject.activeInHierarchy && t.avatar != null && t.firstPersonCamera != null)
                .SelectMany(t => t.avatar.headRenderers ?? Array.Empty<Renderer>()).Where(r => r != null));
            var lights = roots.SelectMany(g => g.GetComponentsInChildren<Light>(true))
                .Where(l => l.isActiveAndEnabled && l.lightmapBakeType != LightmapBakeType.Baked).ToArray();
            var shadowLights = lights.Where(l => l.shadows != LightShadows.None).ToArray();
            bool passed = BudgetCheck(sb, $"Lampu realtime={lights.Length}, shadow={shadowLights.Length} (maks. satu directional)",
                shadowLights.Length <= 1 && shadowLights.All(l => l.type == LightType.Directional));
            bool shadows = mainShadows && shadowLights.Length > 0;
            var triangles = new System.Collections.Generic.Dictionary<Renderer, long>();
            var draws = new System.Collections.Generic.Dictionary<Renderer, int>();
            int shadowDraws = 0, unknown = 0;
            long shadowTriangles = 0;
            foreach (var r in renderers)
            {
                var text = r.GetComponent<TextMesh>();
                // TextMesh tidak memiliki MeshFilter; jangan memanggil binding sharedMesh pada komponen yang hilang.
                r.TryGetComponent<MeshFilter>(out var filter);
                var mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
                var materials = r.sharedMaterials;
                int d = mesh != null ? Math.Max(mesh.subMeshCount, materials.Length) : materials.Length;
                long t = 0;
                if (text != null) t = (long)text.text.Length * 2;
                else if (mesh == null || mesh.subMeshCount == 0 || r is ParticleSystemRenderer)
                { unknown++; sb.AppendLine($"GAGAL geometri tidak tersedia/dinamis: {r.name} ({r.GetType().Name})"); }
                else
                    for (int i = 0; i < d; i++)
                    {
                        int sub = Math.Min(i, mesh.subMeshCount - 1);
                        if (mesh.GetTopology(sub) != MeshTopology.Triangles) { unknown++; continue; }
                        t += (long)mesh.GetIndexCount(sub) / 3;
                    }
                if (materials.Length == 0 || materials.Any(m => m == null || m.shader == null || !m.shader.isSupported))
                { unknown++; sb.AppendLine($"GAGAL material/shader hilang/tidak didukung: {r.name}"); }
                triangles[r] = t; draws[r] = d;
                if (shadows && r.shadowCastingMode != ShadowCastingMode.Off)
                { shadowDraws += d; shadowTriangles += t; }
            }
            passed &= BudgetCheck(sb, $"Geometri/material lengkap (tidak diketahui={unknown})", unknown == 0);
            var room = root.transform.Find("Room");
            var roomRenderers = room != null ? room.GetComponentsInChildren<MeshRenderer>(true) : Array.Empty<MeshRenderer>();
            passed &= BudgetCheck(sb, $"Renderer Room BatchingStatic ({roomRenderers.Length})", roomRenderers.Length > 0 &&
                roomRenderers.All(r => GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic)));
            sb.AppendLine($"Seluruh scene: {triangles.Values.Sum():N0} segitiga, {draws.Values.Sum()} draw warna; shadow konservatif {shadowDraws} draw/{shadowTriangles:N0} segitiga.");
            sb.AppendLine($"Kepala avatar lokal (disembunyikan dari kamera first-person): {localHeads.Count(draws.ContainsKey)} renderer/{localHeads.Where(draws.ContainsKey).Sum(r => draws[r])} draw tidak dihitung per mata.");
            sb.AppendLine($"Skinned mesh: {renderers.OfType<SkinnedMeshRenderer>().Count()} | vertex: {renderers.OfType<SkinnedMeshRenderer>().Sum(r => r.sharedMesh != null ? (long)r.sharedMesh.vertexCount : 0):N0}");
            var materialsAll = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
            sb.AppendLine($"Material scene unik: {materialsAll.Length}, instancing aktif: {materialsAll.Count(m => m.enableInstancing)} (SkinnedMeshRenderer tidak memakai GPU instancing; SRP/static batching dapat diprioritaskan).");
            var textures = materialsAll.SelectMany(m => m.GetTexturePropertyNameIDs().Select(m.GetTexture)).Where(t => t != null).Distinct().ToArray();
            sb.AppendLine($"Tekstur scene: {textures.Length}, memori Editor perkiraan {textures.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)) / 1048576f:0.0} MB; material transparan: {materialsAll.Count(m => m.renderQueue >= 3000)}");
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
                        sb.AppendLine($"{(viewPassed ? "LULUS" : "GAGAL")} yaw={yaw}, pitch={pitch}: segitiga={tris:N0}/{TriangleBudget}, draw/mata={colorDraws}+{shadowDraws}={total}/{DrawBudget}, material={mats}/{MaterialBudget}, estimasi SetPass<={total}/{DrawBudget}");
                        maxTris = Math.Max(maxTris, tris); maxDraws = Math.Max(maxDraws, total); maxMats = Math.Max(maxMats, mats);
                    }
                sb.AppendLine($"Maksimum antar-pandangan: {maxTris:N0} segitiga, {maxDraws} draw/mata, {maxMats} material, estimasi SetPass<={maxDraws}.");
                sb.AppendLine("Renderer terbesar (seluruh scene): " + string.Join(", ", triangles.OrderByDescending(p => p.Value).Take(8).Select(p => $"{p.Key.name}={p.Value:N0} tri/{draws[p.Key]} draw")));
                sb.AppendLine("Draw terbanyak (seluruh scene): " + string.Join(", ", draws.OrderByDescending(p => p.Value).Take(12)
                    .Select(p => $"{p.Key.name}={p.Value} warna/{(shadows && p.Key.shadowCastingMode != ShadowCastingMode.Off ? p.Value : 0)} shadow")));
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
