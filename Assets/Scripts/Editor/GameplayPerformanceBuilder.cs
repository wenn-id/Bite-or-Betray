using System;
using System.IO;
using System.Linq;
using BiteOrBetray.Table3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BiteOrBetray.EditorTools
{
    /// <summary>Optimizations saved by Unity, without editing scene YAML or source model assets.</summary>
    public static class GameplayPerformanceBuilder
    {
        const string SourceScene = "Assets/BiteOrBetray/Scenes/CandyParlour3D.unity";
        const string MeshFolder = "Assets/BiteOrBetray/PerformanceMeshes";
        const string GeometryName = "Performance Geometry";

        [MenuItem("Bite or Betray/Quest Perf Test/0. Prepare Gameplay")]
        public static void PrepareScenes()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Prepare performance in Edit Mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (!Application.isBatchMode && SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save modified scenes before preparing performance.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single(g => g.name == "BoB_Art3D").transform;
                int before = Draws(root), castersBefore = Casters(root);
                int sdkDraws = HideReplacedRigVisuals(root);
                bool movedSeats = ArtSceneBuilder.ConfigureSeatedFraming(root);
                Optimize(root, movedSeats);
                Directory.CreateDirectory("Logs/ci");
                File.WriteAllText("Logs/ci/perf-prepare.txt",
                    $"BoB_Art3D before/after: {before}/{Draws(root)} color slots, {castersBefore}/{Casters(root)} shadow slots.\n" +
                    $"SDK visuals replaced by local avatar: {sdkDraws} color slots hidden (including dynamic hand meshes); sensors/anchors preserved.\n" +
                    "Character/decor shadows disabled. Skinned slots with matching materials combined without changing vertices/blendshapes; furniture grouped by material/lightmap/layer/probe; colliders and characters preserved.\n");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                QuestPerfTestBuilder.BuildScene();
            }
            finally
            {
                // Batchmode CI starts without a saved scene; an empty setup cannot be restored.
                if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        static int Draws(Transform root) => root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff).Sum(r => r.sharedMaterials.Length);

        static int Casters(Transform root) => root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff && r.shadowCastingMode != ShadowCastingMode.Off)
            .Sum(r => r.sharedMaterials.Length);

        static int HideReplacedRigVisuals(Transform root)
        {
            var tracking = root.GetComponentInChildren<LocalAvatarTracking>(true);
            if (tracking == null || !tracking.isActiveAndEnabled || tracking.avatar == null || tracking.tableInput == null || tracking.headTarget == null)
                throw new InvalidOperationException("Local avatar replacing SDK visuals not configured yet.");
            var rig = tracking.headTarget.root;
            if (!rig.name.Contains("Camera Rig")) throw new InvalidOperationException("Local avatar tracking rig not found.");
            int draws = Draws(rig);
            tracking.controllerRenderers = rig.GetComponentsInChildren<Renderer>(true);
            if (PrefabUtility.IsPartOfPrefabInstance(tracking)) PrefabUtility.RecordPrefabInstancePropertyModifications(tracking);
            // The SDK may re-enable renderers; forceRenderingOff does not disable OVRHand/skeleton/input.
            foreach (var renderer in tracking.controllerRenderers)
            {
                renderer.enabled = false;
                renderer.forceRenderingOff = true;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                RecordOverride(renderer);
            }
            return draws;
        }

        static void Optimize(Transform root, bool rebuildFurniture)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                RecordOverride(renderer);
            }
            MergeSkinMaterials(root);
            // Idempotent: sources remain for colliders/bounds; only source renderers are disabled.
            var previous = root.Find(GeometryName);
            if (previous != null && !rebuildFurniture) return;
            // Chairs/pads are already combined in world coordinates; also move the geometry that is actually rendered.
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var room = root.Find("Room");
            var table = root.Find("Table");
            if (room == null || table == null) throw new InvalidOperationException("Gameplay Room/Table not found.");
            var candidates = root.GetComponentsInChildren<MeshRenderer>(true).Where(r =>
                (r.enabled || rebuildFurniture) && r.gameObject.activeInHierarchy &&
                (r.transform.IsChildOf(room) || r.transform.IsChildOf(table) ||
                 r.GetComponentsInParent<Transform>().Any(t => t.name == "Chair" || t.name == "ItemMat")))
                .Where(r => r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null &&
                    r.sharedMaterials.Length == r.GetComponent<MeshFilter>().sharedMesh.subMeshCount &&
                    r.sharedMaterials.All(m => m != null && m.renderQueue < 3000) && r.realtimeLightmapIndex < 0).ToArray();
            var container = new GameObject(GeometryName).transform;
            container.SetParent(root, false);
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/BiteOrBetray", "PerformanceMeshes");
            // One actual mesh/submesh per group, rather than an estimated static batching discount in the audit.
            var parts = candidates.SelectMany(r => r.sharedMaterials.Select((m, sub) => new { renderer = r, material = m, sub }));
            var groups = parts.GroupBy(p => (p.material, p.renderer.lightmapIndex, p.renderer.receiveGI,
                p.renderer.lightProbeUsage, p.renderer.reflectionProbeUsage, p.renderer.receiveShadows,
                p.renderer.gameObject.layer, p.renderer.renderingLayerMask));
            int index = 0;
            foreach (var group in groups)
            {
                var sources = group.ToArray();
                var first = sources[0].renderer;
                var mesh = new Mesh { name = "Furniture_" + index, indexFormat = IndexFormat.UInt32 };
                bool baked = first.lightmapIndex >= 0 && first.lightmapIndex < LightmapSettings.lightmaps.Length;
                mesh.CombineMeshes(sources.Select(p => new CombineInstance {
                    mesh = p.renderer.GetComponent<MeshFilter>().sharedMesh, subMeshIndex = p.sub,
                    transform = container.worldToLocalMatrix * p.renderer.transform.localToWorldMatrix,
                    lightmapScaleOffset = p.renderer.lightmapScaleOffset
                }).ToArray(), true, true, baked);
                long sourceIndices = sources.Sum(p => (long)p.renderer.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(p.sub));
                if (mesh.subMeshCount != 1 || mesh.vertexCount == 0 || (long)mesh.GetIndexCount(0) != sourceIndices)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                    throw new InvalidOperationException("Furniture merging lost geometry; sources remain enabled.");
                }
                string path = MeshFolder + "/Furniture_" + index++ + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
                EditorUtility.SetDirty(saved);
                var go = new GameObject(saved.name);
                go.transform.SetParent(container, false);
                go.layer = first.gameObject.layer;
                go.AddComponent<MeshFilter>().sharedMesh = saved;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = group.Key.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = first.receiveShadows;
                renderer.receiveGI = first.receiveGI;
                renderer.lightmapIndex = first.lightmapIndex;
                renderer.lightmapScaleOffset = new Vector4(1, 1, 0, 0);
                renderer.lightProbeUsage = first.lightProbeUsage;
                renderer.reflectionProbeUsage = first.reflectionProbeUsage;
                renderer.renderingLayerMask = first.renderingLayerMask;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            foreach (var source in candidates)
            {
                source.enabled = false;
                RecordOverride(source);
            }
        }

        static void MergeSkinMaterials(Transform root)
        {
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int index = 0; index < skins.Length; index++)
            {
                var skin = skins[index];
                var source = skin.sharedMesh;
                var materials = skin.sharedMaterials;
                if (source == null || !source.isReadable || source.subMeshCount != materials.Length ||
                    materials.Any(m => m == null || m.renderQueue >= 3000)) continue;
                var groups = Enumerable.Range(0, materials.Length).GroupBy(i => materials[i]).ToArray();
                if (groups.Length == materials.Length || Enumerable.Range(0, source.subMeshCount)
                    .Any(i => source.GetTopology(i) != MeshTopology.Triangles)) continue;
                if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/BiteOrBetray", "PerformanceMeshes");
                // The copy preserves bone weights, bindposes, UVs, bounds and all blendshape frames.
                var mesh = UnityEngine.Object.Instantiate(source);
                mesh.name = "Skin_" + index;
                mesh.subMeshCount = groups.Length;
                for (int sub = 0; sub < groups.Length; sub++)
                    mesh.SetIndices(groups[sub].SelectMany(i => source.GetIndices(i)).ToArray(), MeshTopology.Triangles, sub, false);
                long before = Enumerable.Range(0, source.subMeshCount).Sum(i => (long)source.GetIndexCount(i));
                long after = Enumerable.Range(0, mesh.subMeshCount).Sum(i => (long)mesh.GetIndexCount(i));
                if (before != after || source.vertexCount != mesh.vertexCount || source.blendShapeCount != mesh.blendShapeCount)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                    throw new InvalidOperationException("Skinned slot merging lost geometry/animation.");
                }
                string path = MeshFolder + "/Skin_" + index + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
                EditorUtility.SetDirty(saved);
                skin.sharedMesh = saved;
                skin.sharedMaterials = groups.Select(g => g.Key).ToArray();
                RecordOverride(skin);
            }
        }

        static void RecordOverride(Renderer renderer)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }
}
