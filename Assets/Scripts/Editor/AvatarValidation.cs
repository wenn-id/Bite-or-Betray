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

        [MenuItem("Bite or Betray/Check Floating Avatar (tracking, remote, GC)")]
        public static void Check()
        {
            OnlineAvatarValidation.Check();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SeatedAvatarBuilder.PrefabPath);
            Require(prefab != null, "prefab not created yet");
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
                    Require(avatar.headRenderers.Length >= 8 && avatar.left.fingers.Length == 10 && avatar.right.fingers.Length == 10, "incomplete head/finger rig");
                }
                // Floating: only head meshes (under the Head bone or HeadSkin) and gloves.
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    Require(renderer.name == "HeadSkin" || renderer.name.StartsWith("Glove_") || renderer.GetComponentsInParent<Transform>(true).Any(t => t.name == "Head"),
                        "body/arm mesh still present: " + renderer.name);
                var first = objects[0].GetComponent<SeatedAvatar>();
                var seat = testSeat.transform;
                restEye = first.CurrentPose.head;
                var samples = new[] {
                    new Vector3(-.19f, .80f, .36f), // idle / hands on the table
                    new Vector3(-.06f, 1.08f, .10f), // candy to mouth
                    new Vector3(-.45f, .93f, .30f), // point at a neighbor
                    new Vector3(-.05f, .80f, .58f), // pick up candy
                    new Vector3(-.40f, 1.40f, 1.20f), // away from the body: unrestricted
                    new Vector3(.24f, .89f, .36f), // crossed hands
                    new Vector3(-.19f, .80f, .36f)
                };
                var heads = new[] {
                    restEye,
                    new Pose(restEye.position + new Vector3(0, -.08f, .14f), Quaternion.Euler(35, 0, 0)), // lean, look down
                    new Pose(restEye.position + new Vector3(.12f, 0, 0), Quaternion.Euler(0, 80, 10)), // look right
                    new Pose(restEye.position + new Vector3(-.10f, .05f, -.05f), Quaternion.Euler(-10, -75, 0)) // look left
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
                        Require(Vector3.Distance(first.left.hand.position, seat.TransformPoint(current.leftHand.position)) < .001f, "left hand does not follow the controller");
                        Require(Vector3.Distance(first.right.hand.position, seat.TransformPoint(current.rightHand.position)) < .001f, "right hand does not follow the controller");
                        Require(Vector3.Distance(first.eyeReference.position, seat.TransformPoint(current.head.position)) < .001f, "head does not follow the headset");
                        Require(Quaternion.Angle(first.eyeReference.rotation, seat.rotation * current.head.rotation) < .1f, "head rotation does not follow the headset");
                        Require(Vector3.Distance(first.left.hand.position, lastHand) < .06f, "hand jumps");
                        lastHand = first.left.hand.position;
                        poses++;
                    }
                }

                var camera = testSeat.AddComponent<Camera>();
                first.SetLocalHeadHidden(camera, true);
                Require(first.headRenderers.All(r => (camera.cullingMask & (1 << r.gameObject.layer)) == 0), "head visible to the local camera");
                Require(objects[0].GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Glove_")).All(r => r.gameObject.layer != first.localHeadLayer), "hands are also hidden");
                first.SetLocalHeadHidden(camera, false);
                Require(first.headRenderers.All(r => r.gameObject.layer == first.gameObject.layer), "remote head hidden");

                // Tracking lost: hold briefly, then smoothly return to the resting position on the table; recover without snapping.
                Vector3 held = first.left.hand.position;
                current.leftTracked = current.rightTracked = false;
                for (int i = 0; i < 20; i++) { first.SetPose(current); first.ApplyPose(1 / 90f); }
                Require(Vector3.Distance(first.left.hand.position, held) < .001f, "hand not held during tracking loss");
                for (int i = 0; i < 400; i++) { first.SetPose(current); first.ApplyPose(1 / 90f); }
                Require(Vector3.Distance(first.left.hand.position, seat.TransformPoint(first.left.restPosition)) < .01f, "hand does not return to rest position");
                current.leftTracked = current.rightTracked = true;
                lastHand = first.left.hand.position;
                for (int i = 0; i < 30; i++)
                {
                    first.SetPose(current); first.ApplyPose(1 / 90f);
                    Require(Vector3.Distance(first.left.hand.position, lastHand) < .1f, "hand snaps when tracking recovers");
                    lastHand = first.left.hand.position;
                }
                Require(Vector3.Distance(first.left.hand.position, seat.TransformPoint(current.leftHand.position)) < .001f, "hand does not return to the controller");

                var remote = objects[1].GetComponent<RemoteAvatarRepresentation>();
                current.sequence = 100;
                Require(remote.ReceivePose(current), "remote rejected a valid pose");
                Require(!remote.ReceivePose(current), "remote accepted a duplicate");
                current.sequence = 99;
                Require(!remote.ReceivePose(current), "remote accepted a stale packet");
                current.sequence = 101;
                var invalid = current; invalid.leftHand.position.x = float.NaN;
                Require(!remote.ReceivePose(invalid), "remote accepted NaN");
                Require(remote.ReceivePose(current), "remote did not recover after an invalid packet");
                remote.ApplyReceivedPose(Time.unscaledTime + 1);
                Require(!remote.avatar.CurrentPose.leftTracked, "remote timeout not handled");
                remote.ResetStream();
                current.sequence = 1;
                Require(remote.ReceivePose(current), "reconnect sequence not reset");

                // Low HP / poisoning is passed through the reaction API, not candy logic.
                var reaction = objects[0].GetComponent<AvatarReactionController>();
                int statusEvents = 0;
                reaction.StatusChanged += () => statusEvents++;
                reaction.SetSickness(.5f);
                reaction.SetSickness(.5f);
                Require(statusEvents == 1 && Mathf.Approximately(reaction.face.sickness, .5f), "sickness status not forwarded to the face");
                reaction.ResetState();
                Require(reaction.Sickness == 0 && reaction.face.sickness == 0, "sickness status reset failed");

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
        // Settings that must not change during builds (this script used to write m_BrgStripping to both).
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
                BuildAndroid(RequestedBuildMode() == "Release");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        // Store the RC request in the repo so the runner uses the same mode as the source commit.
        public static string RequestedBuildMode()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-bobBuildMode");
            const string request = "Docs/Release/build-mode.txt";
            string mode = i >= 0
                ? (i + 1 < args.Length ? args[i + 1] : "")
                : (File.Exists(request) ? File.ReadAllText(request).Trim() : "Development");
            if (mode != "Development" && mode != "Release") throw new ArgumentException("Build mode must be Development or Release, not " + mode);
            return mode;
        }

        /// <summary>
        /// Build the Quest APK from CandyParlour3D. Preserve settings: copy GraphicsSettings.asset and the Build Profile (Meta Quest/active)
        /// before building, then restore them if changed. Save reports to Builds/reports/ (Temp/ is removed when Unity closes).
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
            // Restore settings changed during the build to keep git status clean.
            var restored = guarded.Where(g => !File.ReadAllBytes(g.Key).SequenceEqual(g.Value)).Select(g => g.Key).ToList();
            foreach (string path in restored)
            {
                File.WriteAllBytes(path, guarded[path]);
                if (path.StartsWith("Assets/")) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            var summary = report.summary;
            string result = $"{summary.result}; mode={mode}; errors={summary.totalErrors}; warnings={summary.totalWarnings}; bytes={summary.totalSize}; duration={timer.Elapsed.TotalSeconds:0.0}s; path={output}";
            var text = new System.Text.StringBuilder();
            text.AppendLine("Bite or Betray - Quest build report");
            text.AppendLine($"Result: {result}");
            text.AppendLine($"Commit: {commit}");
            text.AppendLine($"Started: {started:yyyy-MM-dd HH:mm:ss}; finished: {DateTime.Now:yyyy-MM-dd HH:mm:ss}; Unity duration: {summary.totalTime.TotalSeconds:0.0}s");
            text.AppendLine($"Unity: {Application.unityVersion}; scene: {BuildScene}; active profile: {profilePath ?? "-"}");
            long apkBytes = File.Exists(output) ? new FileInfo(output).Length : 0;
            text.AppendLine($"APK: {apkBytes} bytes ({apkBytes / 1048576.0:0.0} MB)");
            if (summary.result == BuildResult.Succeeded && File.Exists(output))
            {
                using var apk = File.OpenRead(output);
                using var sha = System.Security.Cryptography.SHA256.Create();
                text.AppendLine("APK SHA-256: " + BitConverter.ToString(sha.ComputeHash(apk)).Replace("-", "").ToLowerInvariant());
            }
            text.AppendLine($"BRG stripping: global={BrgStripping(GraphicsSettings.GetGraphicsSettings())}, profile={(profile != null ? BrgStripping(AssetDatabase.LoadAllAssetsAtPath(profilePath)) : "-")}");
            text.AppendLine("Settings restored after build: " + (restored.Count > 0 ? string.Join(", ", restored) : "none (unchanged)"));
            text.AppendLine();
            text.AppendLine("== Build steps ==");
            foreach (var step in report.steps.Where(s => s.depth <= 1))
                text.AppendLine($"{new string(' ', step.depth * 2)}{step.name}: {step.duration.TotalSeconds:0.0}s");
            var messages = report.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Take(30).ToList();
            if (messages.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("== Errors ==");
                foreach (var message in messages) text.AppendLine(message.content);
            }
            text.AppendLine();
            text.AppendLine("== Largest assets ==");
            foreach (var asset in report.packedAssets.SelectMany(a => a.contents).GroupBy(c => c.sourceAssetPath)
                         .Select(c => (path: c.Key, bytes: c.Sum(x => (long)x.packedSize))).OrderByDescending(c => c.bytes).Take(25))
                text.AppendLine($"{asset.bytes / 1024.0,10:0.0} KB  {asset.path}");
            text.AppendLine();
            text.Append(ShaderVariantCount.Report());

            Directory.CreateDirectory("Builds/reports");
            string reportPath = $"Builds/reports/BiteOrBetray_Quest_{mode}_{started:yyyyMMdd-HHmmss}_{commit}.txt";
            File.WriteAllText(reportPath, text.ToString());
            Debug.Log($"[Bite or Betray] {result}; report={Path.GetFullPath(reportPath)}");
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

        // Full commit hash, plus "dirty" if import/prepare/build changes the CI working copy.
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
            string hash = Git("rev-parse HEAD");
            if (string.IsNullOrEmpty(hash)) return "nogit";
            return string.IsNullOrEmpty(Git("status --porcelain")) ? hash : hash + "-dirty";
        }
    }

    /// <summary>
    /// Shader variant counts before and after script stripping (URP, Meta XR) during AvatarValidation builds.
    /// Only reads the variant list: ShaderVariantsBefore runs first, ShaderVariantsAfter runs last.
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
            var text = new System.Text.StringBuilder("== Shader variants (before -> after script stripping) ==\n");
            if (counts.Count == 0) text.AppendLine("unavailable (no shaders processed)");
            else
            {
                text.AppendLine($"Total: {counts.Values.Sum(c => c.before)} -> {counts.Values.Sum(c => c.after)} ({counts.Count} shaders)");
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
