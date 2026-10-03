using System;
using System.IO;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using UnityEditor;
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

        [MenuItem("Bite or Betray/Cek Seated Avatar (IK, remote, GC)")]
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
                    Require(SeatedAvatarBuilder.Part(objects[i].transform, "Hips") == null, "masih ada hip bone");
                    Require(avatar.headRenderers.Length >= 8 && avatar.left.fingers.Length == 10 && avatar.right.fingers.Length == 10, "head/finger rig tidak lengkap");
                }
                var first = objects[0].GetComponent<SeatedAvatar>();
                Vector3 spinePosition = first.spine.localPosition;
                restEye = first.CurrentPose.head;
                Vector3 neckRest = first.Relative(first.head).position, hip = first.Relative(first.spine).position;
                var samples = new[] {
                    new Vector3(-.19f, .80f, .36f), // idle / hands on table
                    new Vector3(-.06f, 1.08f, .10f), // candy at mouth
                    new Vector3(-.45f, .93f, .30f), // point to neighbour
                    new Vector3(-.05f, .80f, .58f), // reach for candy
                    new Vector3(-.50f, 1.70f, 2.40f), // unreachable target
                    new Vector3(.24f, .89f, .36f), // crossed hands
                    new Vector3(-.19f, .80f, .36f)
                };
                AvatarPose current = TestPose(samples[0]);
                first.SetPose(current);
                for (int warm = 0; warm < 100; warm++) first.ApplyPose(1 / 90f);
                Vector3 lastElbow = first.left.elbow.position;
                foreach (Vector3 sample in samples)
                {
                    var destination = TestPose(sample);
                    for (int frame = 0; frame < 120; frame++)
                    {
                        current = AvatarPose.Lerp(current, destination, .035f);
                        first.SetPose(current); first.ApplyPose(1 / 90f);
                        CheckArm(first.left); CheckArm(first.right);
                        Require(Vector3.Distance(first.left.elbow.position, lastElbow) < .08f, "elbow popping");
                        lastElbow = first.left.elbow.position;
                        Require(Vector3.Distance(first.eyeReference.position, testSeat.transform.TransformPoint(current.head.position)) < .002f, "head tidak mengikuti pose");
                        Require(Vector3.Distance(first.spine.localPosition, spinePosition) < .0001f, "torso bergeser dari kursi");
                        poses++;
                    }
                }
                current.head.rotation = Quaternion.Euler(0, 90, 0);
                first.SetPose(current); first.ApplyPose(1 / 90f);
                Require(Quaternion.Angle(current.head.rotation, first.Relative(first.spine).rotation) > 15, "torso langsung mengikuti head 1:1");
                // Lihat bawah: kepala berputar di leher, jadi mata ikut turun/maju seperti kepala sungguhan.
                current.head = AroundNeck(neckRest, Quaternion.Euler(70, -70, 0));
                first.SetPose(current);
                for (int i = 0; i < 90; i++) first.ApplyPose(1 / 90f);
                Require(Vector3.Distance(first.eyeReference.position, testSeat.transform.TransformPoint(current.head.position)) < .002f, "head look-down offset");
                Require(first.NeckOffset < .005f, "look-down melepas kepala dari leher");

                // Condong: maju membawa candy ke mulut dan ke samping. Torso ikut, kepala tetap menempel di leher.
                foreach (var leanPose in new[] { Quaternion.Euler(22, 0, 0), Quaternion.Euler(0, 0, 12), Quaternion.Euler(18, 0, -10) })
                {
                    current.head = new Pose(hip + leanPose * (restEye.position - hip), leanPose * restEye.rotation);
                    first.SetPose(current);
                    for (int i = 0; i < 150; i++) { first.ApplyPose(1 / 90f); Require(first.NeckOffset <= first.maxNeckOffset + .001f, "kepala lepas dari leher saat condong"); }
                    Require(Vector3.Distance(first.eyeReference.position, testSeat.transform.TransformPoint(current.head.position)) < .005f, "head tidak mengikuti condong");
                    CheckArm(first.left); CheckArm(first.right);
                }
                // Condong ekstrem/headset terlalu tinggi: torso dibatasi, kepala tidak melayang dari badan.
                foreach (var far in new[] { new Vector3(0, -.05f, .6f), new Vector3(0, .25f, 0), new Vector3(.5f, 0, 0) })
                {
                    current.head = new Pose(restEye.position + far, restEye.rotation);
                    first.SetPose(current);
                    for (int i = 0; i < 150; i++) first.ApplyPose(1 / 90f);
                    Require(first.NeckOffset <= first.maxNeckOffset + .001f, "kepala melayang saat target terlalu jauh");
                    Require(first.LeanAngle <= first.maxForwardLean + .5f, "torso condong melebihi batas");
                }
                current.head = restEye;
                first.SetPose(current);
                for (int i = 0; i < 150; i++) first.ApplyPose(1 / 90f);
                Require(first.LeanAngle < 3, "torso tidak kembali tegak");

                // HP rendah / keracunan diteruskan lewat API reaction, bukan dari logic candy.
                var reaction = objects[0].GetComponent<AvatarReactionController>();
                int statusEvents = 0;
                reaction.StatusChanged += () => statusEvents++;
                reaction.SetSickness(.5f);
                reaction.SetSickness(.5f);
                Require(statusEvents == 1 && Mathf.Approximately(reaction.face.sickness, .5f), "status sakit tidak diteruskan ke wajah");
                reaction.ResetState();
                Require(reaction.Sickness == 0 && reaction.face.sickness == 0, "reset status sakit gagal");
                var camera = testSeat.AddComponent<Camera>();
                first.SetLocalHeadHidden(camera, true);
                Require(first.headRenderers.All(r => (camera.cullingMask & (1 << r.gameObject.layer)) == 0), "head terlihat di kamera lokal");
                first.SetLocalHeadHidden(camera, false);
                Require(first.headRenderers.All(r => r.gameObject.layer == first.gameObject.layer), "head remote tersembunyi");

                current.leftTracked = current.rightTracked = false;
                for (int i = 0; i < 120; i++) { first.SetPose(current); first.ApplyPose(1 / 90f); CheckArm(first.left); }
                current.leftTracked = current.rightTracked = true;
                first.SetPose(current);
                for (int i = 0; i < 30; i++) first.ApplyPose(1 / 90f);
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
                var avatars = objects.Select(o => o.GetComponent<SeatedAvatar>()).ToArray();
                foreach (var avatar in avatars) avatar.SetPose(current);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int frame = 0; frame < 1000; frame++)
                    for (int i = 0; i < avatars.Length; i++) avatars[i].ApplyPose(1 / 90f);
                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(allocated == 0, "managed allocations in pose/IK: " + allocated);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    for (int i = 0; i < mesh.subMeshCount; i++) triangles += (int)mesh.GetIndexCount(i) / 3;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(testSeat); }
            string report = $"PASS: {poses} interpolated poses; idle, table, face, far reach, crossed arms, head yaw/down, lean forward/side/extreme, low-HP status, tracking loss/recovery, remote sequence/NaN/timeout/reconnect, 8 avatars.\nManaged allocations: {allocated} bytes across 8,000 avatar IK frames.\nAvatar triangles: {triangles}.\n";
            File.WriteAllText("Temp/BoBAvatarChecks.txt", report);
            File.WriteAllText("Temp/BoBAvatar.result.txt", report);
            Debug.Log("[Bite or Betray] " + report);
        }

        static Pose restEye;

        static Pose AroundNeck(Vector3 neck, Quaternion rotation) =>
            new Pose(neck + rotation * Quaternion.Inverse(restEye.rotation) * (restEye.position - neck), rotation);

        static AvatarPose TestPose(Vector3 left)
        {
            return new AvatarPose {
                head = restEye,
                leftHand = new Pose(left, Quaternion.identity), rightHand = new Pose(new Vector3(-left.x, left.y, left.z), Quaternion.identity),
                headTracked = true, leftTracked = true, rightTracked = true, leftGrip = .7f, rightIndex = .6f
            };
        }

        static void CheckArm(SeatedAvatar.Arm arm)
        {
            Require(Mathf.Abs(Vector3.Distance(arm.shoulder.position, arm.elbow.position) - arm.upperLength) < .001f, "upper arm stretching");
            Require(Mathf.Abs(Vector3.Distance(arm.elbow.position, arm.hand.position) - arm.lowerLength) < .001f, "forearm stretching");
            Require(!float.IsNaN(arm.elbow.position.x), "IK NaN");
        }

        [MenuItem("Bite or Betray/Build Quest Avatar APK")]
        public static void BuildAndroid()
        {
            var graphics = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            graphics.FindProperty("m_BrgStripping").intValue = (int)BatchRendererGroupStrippingMode.StripAll;
            graphics.ApplyModifiedPropertiesWithoutUndo();
            // The Quest build profile overrides the global stripping setting.
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null)
                foreach (var settings in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(profile)))
                {
                    var data = new SerializedObject(settings);
                    var stripping = data.FindProperty("m_BrgStripping");
                    if (stripping == null) continue;
                    stripping.intValue = (int)BatchRendererGroupStrippingMode.StripAll;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            AssetDatabase.SaveAssets();
            string output = Path.GetFullPath("Builds/BiteOrBetray_SeatedAvatar_Quest.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/BiteOrBetray/Scenes/CandyParlour3D.unity" },
                target = BuildTarget.Android, locationPathName = output, options = BuildOptions.Development
            });
            string result = $"{report.summary.result}; errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}; bytes={report.summary.totalSize}; path={output}";
            File.WriteAllText("Temp/BoBAvatarAndroidBuild.txt", result);
            Require(report.summary.result == BuildResult.Succeeded, result);
            Debug.Log("[Bite or Betray] " + result);
        }
    }
}
