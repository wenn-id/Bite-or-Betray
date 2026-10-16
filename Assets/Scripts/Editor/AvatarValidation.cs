using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Build.Profile;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiteOrBetray.EditorTools
{
    public static class AvatarValidation
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Avatar check: " + message);
        }

        [MenuItem("Bite or Betray/Cek Floating Avatar (tracking, remote, GC)")]
        public static void Check()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SeatedAvatarBuilder.PrefabPath);
            Require(prefab != null, "prefab belum dibuat");
            var testSeat = new GameObject("Avatar Validation Seat") { hideFlags = HideFlags.HideAndDontSave };
            testSeat.transform.rotation = Quaternion.Euler(0, 60, 0);
            var objects = new GameObject[8];
            int poses = 0, triangles = 0;
            long allocated;
            try
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    objects[i] = (GameObject)PrefabUtility.InstantiatePrefab(prefab, testSeat.transform);
                    var avatar = objects[i].GetComponent<SeatedAvatar>();
                    avatar.seat = testSeat.transform;
                    avatar.Initialize();
                    objects[i].GetComponentInChildren<CharacterAnimator>().enabled = false;
                    Require(avatar.headRenderers.Length >= 8 && avatar.left.fingers.Length == 10 && avatar.right.fingers.Length == 10, "head/finger rig tidak lengkap");
                }
                // Floating: hanya mesh kepala (di bawah tulang Head atau HeadSkin) dan sarung tangan.
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    Require(renderer.name == "HeadSkin" || renderer.name.StartsWith("Glove_") || renderer.GetComponentsInParent<Transform>(true).Any(t => t.name == "Head"),
                        "masih ada mesh badan/lengan: " + renderer.name);
                var first = objects[0].GetComponent<SeatedAvatar>();
                var seat = testSeat.transform;
                restEye = first.CurrentPose.head;
                var samples = new[] {
                    new Vector3(-.19f, .80f, .36f), // idle / tangan di meja
                    new Vector3(-.06f, 1.08f, .10f), // candy ke mulut
                    new Vector3(-.45f, .93f, .30f), // menunjuk tetangga
                    new Vector3(-.05f, .80f, .58f), // ambil candy
                    new Vector3(-.40f, 1.40f, 1.20f), // jauh dari badan: tidak dibatasi
                    new Vector3(.24f, .89f, .36f), // tangan bersilang
                    new Vector3(-.19f, .80f, .36f)
                };
                var heads = new[] {
                    restEye,
                    new Pose(restEye.position + new Vector3(0, -.08f, .14f), Quaternion.Euler(35, 0, 0)), // condong, lihat bawah
                    new Pose(restEye.position + new Vector3(.12f, 0, 0), Quaternion.Euler(0, 80, 10)), // lihat kanan
                    new Pose(restEye.position + new Vector3(-.10f, .05f, -.05f), Quaternion.Euler(-10, -75, 0)) // lihat kiri
                };
                AvatarPose current = TestPose(samples[0], heads[0]);
                first.SetPose(current);
                for (int warm = 0; warm < 100; warm++) first.ApplyPose(1 / 90f);
                Vector3 lastHand = first.left.hand.position;
                for (int s = 0; s < samples.Length; s++)
                {
                    var destination = TestPose(samples[s], heads[s % heads.Length]);
                    for (int frame = 0; frame < 120; frame++)
                    {
                        current = AvatarPose.Lerp(current, destination, .035f);
                        first.SetPose(current); first.ApplyPose(1 / 90f);
                        Require(Vector3.Distance(first.left.hand.position, seat.TransformPoint(current.leftHand.position)) < .001f, "tangan kiri tidak mengikuti controller");
                        Require(Vector3.Distance(first.right.hand.position, seat.TransformPoint(current.rightHand.position)) < .001f, "tangan kanan tidak mengikuti controller");
                        Require(Vector3.Distance(first.eyeReference.position, seat.TransformPoint(current.head.position)) < .001f, "kepala tidak mengikuti headset");
                        Require(Quaternion.Angle(first.eyeReference.rotation, seat.rotation * current.head.rotation) < .1f, "rotasi kepala tidak mengikuti headset");
                        Require(Vector3.Distance(first.left.hand.position, lastHand) < .06f, "tangan melompat");
                        lastHand = first.left.hand.position;
                        poses++;
                    }
                }

                var camera = testSeat.AddComponent<Camera>();
                first.SetLocalHeadHidden(camera, true);
                Require(first.headRenderers.All(r => (camera.cullingMask & (1 << r.gameObject.layer)) == 0), "head terlihat di kamera lokal");
                Require(objects[0].GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Glove_")).All(r => r.gameObject.layer != first.localHeadLayer), "tangan ikut tersembunyi");
                first.SetLocalHeadHidden(camera, false);
                Require(first.headRenderers.All(r => r.gameObject.layer == first.gameObject.layer), "head remote tersembunyi");

                // Tracking hilang: tahan sebentar, lalu kembali halus ke posisi istirahat di meja; pulih tanpa snapping.
                Vector3 held = first.left.hand.position;
                current.leftTracked = current.rightTracked = false;
                for (int i = 0; i < 20; i++) { first.SetPose(current); first.ApplyPose(1 / 90f); }
                Require(Vector3.Distance(first.left.hand.position, held) < .001f, "tangan tidak ditahan saat tracking hilang");
                for (int i = 0; i < 400; i++) { first.SetPose(current); first.ApplyPose(1 / 90f); }
                Require(Vector3.Distance(first.left.hand.position, seat.TransformPoint(first.left.restPosition)) < .01f, "tangan tidak kembali ke posisi istirahat");
                current.leftTracked = current.rightTracked = true;
                lastHand = first.left.hand.position;
                for (int i = 0; i < 30; i++)
                {
                    first.SetPose(current); first.ApplyPose(1 / 90f);
                    Require(Vector3.Distance(first.left.hand.position, lastHand) < .1f, "tangan snapping saat tracking pulih");
                    lastHand = first.left.hand.position;
                }
                Require(Vector3.Distance(first.left.hand.position, seat.TransformPoint(current.leftHand.position)) < .001f, "tangan tidak pulih ke controller");

                var remote = objects[1].GetComponent<RemoteAvatarRepresentation>();
                current.sequence = 100;
                Require(remote.ReceivePose(current), "remote menolak pose valid");
                Require(!remote.ReceivePose(current), "remote menerima duplikat");
                current.sequence = 99;
                Require(!remote.ReceivePose(current), "remote menerima paket lama");
                current.sequence = 101;
                var invalid = current; invalid.leftHand.position.x = float.NaN;
                Require(!remote.ReceivePose(invalid), "remote menerima NaN");
                Require(remote.ReceivePose(current), "remote tidak pulih setelah paket invalid");
                remote.ApplyReceivedPose(Time.unscaledTime + 1);
                Require(!remote.avatar.CurrentPose.leftTracked, "timeout remote tidak ditangani");
                remote.ResetStream();
                current.sequence = 1;
                Require(remote.ReceivePose(current), "reconnect sequence tidak di-reset");

                // HP rendah / keracunan diteruskan lewat API reaction, bukan dari logic candy.
                var reaction = objects[0].GetComponent<AvatarReactionController>();
                int statusEvents = 0;
                reaction.StatusChanged += () => statusEvents++;
                reaction.SetSickness(.5f);
                reaction.SetSickness(.5f);
                Require(statusEvents == 1 && Mathf.Approximately(reaction.face.sickness, .5f), "status sakit tidak diteruskan ke wajah");
                reaction.ResetState();
                Require(reaction.Sickness == 0 && reaction.face.sickness == 0, "reset status sakit gagal");

                var avatars = objects.Select(o => o.GetComponent<SeatedAvatar>()).ToArray();
                foreach (var avatar in avatars) avatar.SetPose(current);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 0; frame < 1000; frame++)
                    for (int i = 0; i < avatars.Length; i++) avatars[i].ApplyPose(1 / 90f);
                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(allocated == 0, "managed allocations in pose: " + allocated);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(testSeat); }
            string report = $"PASS: {poses} interpolated poses; head follow (yaw/down/lean), hands follow (table, mouth, point, far reach, crossed), head-only meshes, local head layer, tracking loss/recovery, remote sequence/NaN/timeout/reconnect, low-HP status, 8 avatars.\nManaged allocations: {allocated} bytes across 8,000 avatar frames.\nAvatar triangles: {triangles}.\n";
            File.WriteAllText("Temp/BoBAvatarChecks.txt", report);
            File.WriteAllText("Temp/BoBAvatar.result.txt", report);
            Debug.Log("[Bite or Betray] " + report);
        }

        static Pose restEye;

        static AvatarPose TestPose(Vector3 left, Pose head)
        {
            return new AvatarPose {
                head = head,
                leftHand = new Pose(left, Quaternion.identity), rightHand = new Pose(new Vector3(-left.x, left.y, left.z), Quaternion.Euler(0, 0, 20)),
                headTracked = true, leftTracked = true, rightTracked = true, leftGrip = .7f, rightIndex = .6f
            };
        }

        const string BuildScene = "Assets/BiteOrBetray/Scenes/CandyParlour3D.unity";
        // Setting yang tidak boleh berubah karena build (dulu skrip ini ikut menulis m_BrgStripping ke keduanya).
        static readonly string[] GuardedSettings = { "ProjectSettings/GraphicsSettings.asset", "Assets/Settings/Build Profiles/Meta Quest.asset" };

        [MenuItem("Bite or Betray/Build Quest APK/Development")]
        public static void BuildAndroid() => BuildAndroid(false);

        [MenuItem("Bite or Betray/Build Quest APK/Release")]
        public static void BuildAndroidRelease() => BuildAndroid(true);

        /// <summary>Unity -batchmode -quit -buildTarget Android -executeMethod BiteOrBetray.EditorTools.AvatarValidation.BatchBuild -bobBuildMode Release|Development</summary>
        public static void BatchBuild()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-bobBuildMode");
                string mode = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Development";
                if (mode != "Development" && mode != "Release") throw new ArgumentException("-bobBuildMode harus Development atau Release, bukan " + mode);
                BuildAndroid(mode == "Release");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Build APK Quest dari CandyParlour3D. Tidak mengubah setting: GraphicsSettings.asset dan Build Profile (Meta Quest/aktif)
        /// disalin sebelum build lalu ditulis ulang kalau berubah. Laporan disimpan ke Builds/reports/ (Temp/ hilang saat Unity ditutup).
        /// </summary>
        public static void BuildAndroid(bool release)
        {
            string mode = release ? "Release" : "Development";
            var started = DateTime.Now;
            string commit = GitCommit();
            var profile = BuildProfile.GetActiveBuildProfile();
            string profilePath = profile != null ? AssetDatabase.GetAssetPath(profile) : null;
            var guarded = GuardedSettings.Append(profilePath).Where(p => p != null && File.Exists(p)).Distinct().ToDictionary(p => p, File.ReadAllBytes);
            string output = Path.GetFullPath($"Builds/BiteOrBetray_Quest_{mode}.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var timer = System.Diagnostics.Stopwatch.StartNew();
            BuildReport report;
            ShaderVariantCount.Start();
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { BuildScene },
                    target = BuildTarget.Android, locationPathName = output, options = release ? BuildOptions.None : BuildOptions.Development
                });
            }
            finally { ShaderVariantCount.active = false; }
            timer.Stop();
            // Pulihkan setting yang ikut berubah selama build supaya git status bersih.
            var restored = guarded.Where(g => !File.ReadAllBytes(g.Key).SequenceEqual(g.Value)).Select(g => g.Key).ToList();
            foreach (string path in restored)
            {
                File.WriteAllBytes(path, guarded[path]);
                if (path.StartsWith("Assets/")) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            var summary = report.summary;
            string result = $"{summary.result}; mode={mode}; errors={summary.totalErrors}; warnings={summary.totalWarnings}; bytes={summary.totalSize}; durasi={timer.Elapsed.TotalSeconds:0.0}s; path={output}";
            var text = new System.Text.StringBuilder();
            text.AppendLine("Bite or Betray - laporan build Quest");
            text.AppendLine($"Hasil: {result}");
            text.AppendLine($"Commit: {commit}");
            text.AppendLine($"Mulai: {started:yyyy-MM-dd HH:mm:ss}; selesai: {DateTime.Now:yyyy-MM-dd HH:mm:ss}; durasi Unity: {summary.totalTime.TotalSeconds:0.0}s");
            text.AppendLine($"Unity: {Application.unityVersion}; scene: {BuildScene}; profil aktif: {profilePath ?? "-"}");
            long apkBytes = File.Exists(output) ? new FileInfo(output).Length : 0;
            text.AppendLine($"APK: {apkBytes} bytes ({apkBytes / 1048576.0:0.0} MB)");
            text.AppendLine($"BRG stripping: global={BrgStripping(GraphicsSettings.GetGraphicsSettings())}, profil={(profile != null ? BrgStripping(AssetDatabase.LoadAllAssetsAtPath(profilePath)) : "-")}");
            text.AppendLine("Setting dipulihkan setelah build: " + (restored.Count > 0 ? string.Join(", ", restored) : "tidak ada (tidak berubah)"));
            text.AppendLine();
            text.AppendLine("== Langkah build ==");
            foreach (var step in report.steps.Where(s => s.depth <= 1))
                text.AppendLine($"{new string(' ', step.depth * 2)}{step.name}: {step.duration.TotalSeconds:0.0}s");
            var messages = report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Take(30).ToList();
            if (messages.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("== Error ==");
                foreach (var message in messages) text.AppendLine(message.content);
            }
            text.AppendLine();
            text.AppendLine("== Aset terbesar ==");
            foreach (var asset in report.packedAssets.SelectMany(a => a.contents).GroupBy(c => c.sourceAssetPath)
                         .Select(c => (path: c.Key, bytes: c.Sum(x => (long)x.packedSize))).OrderByDescending(c => c.bytes).Take(25))
                text.AppendLine($"{asset.bytes / 1024.0,10:0.0} KB  {asset.path}");
            text.AppendLine();
            text.Append(ShaderVariantCount.Report());

            Directory.CreateDirectory("Builds/reports");
            string reportPath = $"Builds/reports/BiteOrBetray_Quest_{mode}_{started:yyyyMMdd-HHmmss}_{commit}.txt";
            File.WriteAllText(reportPath, text.ToString());
            Debug.Log($"[Bite or Betray] {result}; laporan={Path.GetFullPath(reportPath)}");
            Require(summary.result == BuildResult.Succeeded, result);
        }

        static string BrgStripping(params UnityEngine.Object[] assets)
        {
            foreach (var asset in assets)
            {
                var stripping = asset != null ? new SerializedObject(asset).FindProperty("m_BrgStripping") : null;
                if (stripping != null) return ((BatchRendererGroupStrippingMode)stripping.intValue).ToString();
            }
            return "-";
        }

        // Hash commit pendek, ditambah "dirty" kalau ada perubahan yang belum di-commit.
        static string GitCommit()
        {
            string Git(string arguments)
            {
                try
                {
                    using var git = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments) {
                        RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                    });
                    string text = git.StandardOutput.ReadToEnd().Trim();
                    git.WaitForExit();
                    return git.ExitCode == 0 ? text : null;
                }
                catch (Exception) { return null; }
            }
            string hash = Git("rev-parse --short HEAD");
            if (string.IsNullOrEmpty(hash)) return "nogit";
            return string.IsNullOrEmpty(Git("status --porcelain")) ? hash : hash + "-dirty";
        }
    }

    /// <summary>
    /// Jumlah variant shader sebelum dan sesudah stripping skrip (URP, Meta XR) selama build dari AvatarValidation.
    /// Hanya membaca daftar variant: ShaderVariantsBefore dipanggil paling awal, ShaderVariantsAfter paling akhir.
    /// </summary>
    static class ShaderVariantCount
    {
        public static bool active;
        static readonly Dictionary<string, (long before, long after)> counts = new Dictionary<string, (long, long)>();

        public static void Start() { counts.Clear(); active = true; }

        public static void Add(Shader shader, int variants, bool after)
        {
            if (!active) return;
            counts.TryGetValue(shader.name, out var count);
            if (after) count.after += variants; else count.before += variants;
            counts[shader.name] = count;
        }

        public static string Report()
        {
            var text = new System.Text.StringBuilder("== Shader variant (sebelum -> sesudah stripping skrip) ==\n");
            if (counts.Count == 0) text.AppendLine("tidak tersedia (tidak ada shader yang diproses)");
            else
            {
                text.AppendLine($"Total: {counts.Values.Sum(c => c.before)} -> {counts.Values.Sum(c => c.after)} ({counts.Count} shader)");
                foreach (var c in counts.OrderByDescending(c => c.Value.before - c.Value.after).Take(30))
                    text.AppendLine($"{c.Value.before,8} -> {c.Value.after,-8} {c.Key}");
            }
            return text.ToString();
        }
    }

    class ShaderVariantsBefore : IPreprocessShaders
    {
        public int callbackOrder => int.MinValue;
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) => ShaderVariantCount.Add(shader, data.Count, false);
    }

    class ShaderVariantsAfter : IPreprocessShaders
    {
        public int callbackOrder => int.MaxValue;
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) => ShaderVariantCount.Add(shader, data.Count, true);
    }
}
