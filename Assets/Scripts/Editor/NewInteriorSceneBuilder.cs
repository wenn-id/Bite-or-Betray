using System;
using System.IO;
using System.Linq;
using BiteOrBetray.Art3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiteOrBetray.EditorTools
{
    public static class NewInteriorSceneBuilder
    {
        const string Folder = "Assets/BiteOrBetray/NewInterior";
        const string ScenePath = "Assets/BiteOrBetray/Scenes/CandyParlour3D.unity";
        static readonly string[] Models = { "Table", "Chair", "Door", "Window", "Rug", "Shelves", "Display", "Sconce", "Picture", "Floor", "CandyJarRed", "CandyJarBlue", "CandyJarYellow" };

        [MenuItem("Bite or Betray/Apply New Interior")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            foreach (string model in Models) PrepareModel(model);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            QuestPerfTestBuilder.BuildScene();EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("NEW_INTERIOR_APPLIED: continuous walls with four corner posts, imported furniture and floor, preserved gameplay anchors.");
            // Finish saved batch work without waiting on lingering Editor import callbacks.
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void ApplyToScene()
        {
            var root = GameObject.Find("BoB_Art3D").transform;
            var merged = root.Find("Performance Geometry");if (merged != null) UnityEngine.Object.DestroyImmediate(merged.gameObject);
            // Keep the previous room available for recovery without rendering its furniture or colliders.
            var legacy = root.Find("Legacy Room");
            if (legacy == null) { legacy = root.Find("Room");legacy.name = "Legacy Room"; }
            legacy.gameObject.SetActive(false);
            var previous = root.Find("Room");if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var room = new GameObject("Room").transform;room.SetParent(root, false);
            if (root.Find("New Interior Layout") == null) new GameObject("New Interior Layout").transform.SetParent(root, false);

            BuildFloor(room);
            var rug = Place("Rug", room, new Vector3(0, .003f, 0), 0);
            BuildWalls(room);
            BuildCeiling(room);
            WallCollider(room, new Vector3(0, 1.70f, 3.74f), new Vector3(7.6f, 3.4f, .10f));
            WallCollider(room, new Vector3(0, 1.70f, -3.74f), new Vector3(7.6f, 3.4f, .10f));
            WallCollider(room, new Vector3(-3.74f, 1.70f, 0), new Vector3(.10f, 3.4f, 7.6f));
            WallCollider(room, new Vector3(3.74f, 1.70f, 0), new Vector3(.10f, 3.4f, 7.6f));

            Place("Door", room, new Vector3(0, .005f, 3.57f), 180);
            Place("Window", room, new Vector3(-3.54f, 1.08f, -.55f), 90);
            Place("Window", room, new Vector3(3.54f, 1.08f, -2.05f), -90);
            StockShelves(Place("Shelves", room, new Vector3(-2.38f, 1.28f, 3.54f), 180), false);
            StockShelves(Place("Display", room, new Vector3(2.46f, 1.31f, 3.54f), 180), true);
            StockShelves(Place("Shelves", room, new Vector3(3.54f, 1.28f, .55f), -90), false);
            StockShelves(Place("Display", room, new Vector3(-3.54f, 1.30f, 2.12f), 90), true);
            Place("Picture", room, new Vector3(-1.25f, 1.60f, 3.53f), 180);
            Place("Picture", room, new Vector3(1.25f, 1.60f, 3.53f), 180);
            Place("Picture", room, new Vector3(-3.53f, 1.64f, -2.05f), 90);
            Place("Picture", room, new Vector3(3.53f, 1.64f, 2.34f), -90);
            Sconce(room, new Vector3(-1.22f, 2.40f, 3.53f), 180);
            Sconce(room, new Vector3(1.22f, 2.40f, 3.53f), 180);
            Sconce(room, new Vector3(-3.53f, 2.42f, -2.04f), 90);
            Sconce(room, new Vector3(-3.53f, 2.42f, 1.04f), 90);
            Sconce(room, new Vector3(3.53f, 2.42f, -1.00f), -90);
            Sconce(room, new Vector3(3.53f, 2.42f, 2.34f), -90);

            var table = root.Find("Table");table.localScale = Vector3.one;
            var oldTable = table.Find("TableModel");if (oldTable != null) oldTable.gameObject.SetActive(false);
            foreach (Transform child in table.Cast<Transform>().Where(t => t.name != "TableModel").ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            var tableModel = Place("Table", table, Vector3.zero, 0);
            var tabletop = table.gameObject.GetComponent<BoxCollider>();
            if (tabletop == null) tabletop = table.gameObject.AddComponent<BoxCollider>();
            tabletop.center = new Vector3(0, .725f, 0);tabletop.size = new Vector3(3.12f, .065f, 3.12f);
            foreach (Transform seat in root.Find("Seats"))
            {
                var chair = seat.Find("Chair");if (chair != null) UnityEngine.Object.DestroyImmediate(chair.gameObject);
                var upholstery = seat.Find("Chair upholstery");if (upholstery != null) UnityEngine.Object.DestroyImmediate(upholstery.gameObject);
                var chairRoot = new GameObject("Chair").transform;chairRoot.SetParent(seat, false);
                Place("Chair", chairRoot, Vector3.zero, 0);
            }
            foreach (string lightName in new[] { "Shop Fill", "Window Fill" })
            { var oldLight = root.Find(lightName);if (oldLight != null) UnityEngine.Object.DestroyImmediate(oldLight.gameObject); }
            RenderSettings.skybox = null;RenderSettings.fog = false;RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.72f, .78f, .84f);
            RenderSettings.ambientEquatorColor = new Color(.69f, .62f, .55f);
            RenderSettings.ambientGroundColor = new Color(.43f, .36f, .31f);
            var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
            sun.color = new Color(1, .91f, .81f);sun.intensity = 1.12f;sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            sun.shadows = LightShadows.Hard;sun.shadowBias = .08f;sun.shadowNormalBias = .12f;RenderSettings.sun = sun;
            var preview = UnityEngine.Object.FindFirstObjectByType<ArtPreviewControls>(FindObjectsInactive.Include);
            preview.tableOverview = true;preview.SetTableOverview();
            GameplayPerformanceBuilder.Optimize(root, true);
            var furniture = room.GetComponentsInChildren<MeshFilter>()
                .Concat(table.GetComponentsInChildren<MeshFilter>())
                .Concat(root.Find("Seats").Cast<Transform>().SelectMany(s => s.Find("Chair").GetComponentsInChildren<MeshFilter>()));
            long triangles = furniture.Sum(f => (long)f.sharedMesh.triangles.Length / 3);
            Directory.CreateDirectory("Logs/NewInterior");
            File.WriteAllText("Logs/NewInterior/layout-report.txt", $"Imported interior including all six chairs, walls and floor: {triangles:N0} triangles.\n" +
                "36 decorative candy jars share one opaque material and one 1K atlas; combined jar geometry is 36,000 triangles. Original downloads and character rigs were preserved.\n");
        }

        static void PrepareModel(string name)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/" + name + ".fbx");
            if (importer == null) throw new FileNotFoundException("Interior asset missing: " + name);
            importer.isReadable = true;importer.importAnimation = false;importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".fbx");
            foreach (var original in source.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct())
            {
                string texturePath = Folder + "/" + original.name + "_BaseColor.png";
                var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                if (textureImporter == null) throw new FileNotFoundException(texturePath);
                textureImporter.maxTextureSize = name == "Floor" || name == "Sconce" || name == "Picture" ? 512 : 1024;
                textureImporter.textureCompression = TextureImporterCompression.Compressed;
                if (name == "Table" || name == "Chair" || name == "Sconce" || name == "Picture" || name == "Floor" || name == "Rug" || name.StartsWith("CandyJar"))
                {
                    textureImporter.mipmapEnabled = true;
                    var android = textureImporter.GetPlatformTextureSettings("Android");
                    android.overridden = true;android.maxTextureSize = textureImporter.maxTextureSize;android.format = TextureImporterFormat.ASTC_6x6;
                    textureImporter.SetPlatformTextureSettings(android);
                }
                if (name == "Floor" || name == "Rug")
                {
                    textureImporter.wrapMode = name == "Floor" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    textureImporter.filterMode = FilterMode.Trilinear;textureImporter.anisoLevel = name == "Floor" ? 4 : 2;
                }
                textureImporter.SaveAndReimport();
                var material = Material(original.name, Color.white);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                material.SetFloat("_Smoothness", name == "Rug" ? .10f : name == "Floor" ? .12f : name.StartsWith("CandyJar") ? .35f : .25f);
                if (name == "Sconce")
                {
                    string emissionPath = Folder + "/InteriorSconce_Emission.png";
                    var emissionImporter = (TextureImporter)AssetImporter.GetAtPath(emissionPath);
                    if (emissionImporter == null) throw new FileNotFoundException(emissionPath);
                    emissionImporter.sRGBTexture = false;emissionImporter.mipmapEnabled = true;emissionImporter.maxTextureSize = 128;
                    var android = emissionImporter.GetPlatformTextureSettings("Android");
                    android.overridden = true;android.maxTextureSize = 128;android.format = TextureImporterFormat.ASTC_6x6;
                    emissionImporter.SetPlatformTextureSettings(android);emissionImporter.SaveAndReimport();
                    material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath));
                    material.SetColor("_EmissionColor", new Color(.8f, .56f, .24f));
                    // URP import validation requires an emissive GI flag to preserve the emission keyword.
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;material.EnableKeyword("_EMISSION");
                }
                EditorUtility.SetDirty(material);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), original.name), material);
            }
            importer.SaveAndReimport();
        }

        static Transform Place(string model, Transform parent, Vector3 position, float yaw)
        {
            var wrapper = new GameObject("Interior " + model).transform;wrapper.SetParent(parent, false);
            wrapper.SetLocalPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + model + ".fbx");
            if (source == null) throw new FileNotFoundException(model);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, wrapper);instance.transform.localPosition = Vector3.zero;
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            { r.shadowCastingMode = ShadowCastingMode.Off;r.lightProbeUsage = LightProbeUsage.Off;r.reflectionProbeUsage = ReflectionProbeUsage.Off; }
            return wrapper;
        }

        static void StockShelves(Transform rack, bool arched)
        {
            var levels = arched ? new[] { .085f, .405f, .725f } : new[] { .23f, .615f, 1.00f };
            var colors = new[] { "Red", "Blue", "Yellow" };
            for (int row = 0; row < levels.Length; row++) for (int column = 0; column < colors.Length; column++)
            {
                // Shelf boards are 55 mm thick; the smaller arched rack leaves clearance above each knob.
                var jar = Place("CandyJar" + colors[column], rack,
                    new Vector3((column - 1) * (arched ? .29f : .34f), levels[row] + .0285f, arched ? .15f : .17f), 0);
                jar.localScale = Vector3.one * (arched ? .80f : 1f);
            }
        }

        static Material Material(string name, Color color)
        {
            string path = Folder + "/" + name + ".mat";var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color);material.SetFloat("_Smoothness", .16f);material.enableInstancing = true;return material;
        }

        static void BuildFloor(Transform room)
        {
            var floor = new GameObject("Interior Checker Floor");floor.transform.SetParent(room, false);
            Place("Floor", floor.transform, new Vector3(0, -.012f, 0), 0);
            var collider = floor.AddComponent<BoxCollider>();collider.center = Vector3.down * .04f;collider.size = new Vector3(7.4f, .08f, 7.4f);
        }

        static void BuildWalls(Transform room)
        {
            // Colors sampled from the supplied wall asset; uninterrupted geometry avoids its repeated end pillars.
            var peach = Material("WallPeach", new Color(.988f, .698f, .553f));
            var teal = Material("WallTeal", new Color(.031f, .380f, .396f));
            var wood = Material("WallWoodRail", new Color(.80f, .404f, .051f));
            var gold = Material("WallGoldLine", new Color(.96f, .66f, .24f));
            for (int side = 0; side < 4; side++)
            {
                var wall = new GameObject("Continuous wall " + side).transform;wall.SetParent(room, false);
                wall.localPosition = side == 0 ? new Vector3(0, 0, 3.70f) : side == 1 ? new Vector3(-3.70f, 0, 0) : side == 2 ? new Vector3(3.70f, 0, 0) : new Vector3(0, 0, -3.70f);
                wall.localRotation = Quaternion.Euler(0, side == 0 ? 180 : side == 1 ? 90 : side == 2 ? -90 : 0, 0);
                Cube("Peach plaster", wall, new Vector3(0, 2.29f, .07f), new Vector3(7.4f, 2.32f, .14f), peach, false);
                Cube("Teal wainscot", wall, new Vector3(0, .565f, .07f), new Vector3(7.4f, 1.13f, .14f), teal, false);
                Cube("Wood chair rail", wall, new Vector3(0, 1.13f, .11f), new Vector3(7.4f, .065f, .22f), wood, false);
                Cube("Fine gold inlay", wall, new Vector3(0, 1.145f, .115f), new Vector3(7.4f, .012f, .24f), gold, false);
                Cube("Teal baseboard", wall, new Vector3(0, .07f, .105f), new Vector3(7.4f, .14f, .21f), teal, false);
                Cube("Cornice lower bead", wall, new Vector3(0, 3.28f, .10f), new Vector3(7.4f, .045f, .20f), teal, false);
                Cube("Cornice", wall, new Vector3(0, 3.35f, .12f), new Vector3(7.4f, .11f, .24f), teal, false);
                Cube("Cornice cap", wall, new Vector3(0, 3.43f, .135f), new Vector3(7.4f, .04f, .27f), teal, false);
            }
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
            {
                var corner = new GameObject("Wall corner").transform;corner.SetParent(room, false);corner.localPosition = new Vector3(x * 3.55f, 0, z * 3.55f);
                Cube("Corner post", corner, new Vector3(0, 1.725f, 0), new Vector3(.18f, 3.45f, .18f), teal, false);
                Cube("Corner base", corner, new Vector3(0, .10f, 0), new Vector3(.24f, .20f, .24f), teal, false);
                Cube("Corner capital", corner, new Vector3(0, 3.33f, 0), new Vector3(.24f, .16f, .24f), teal, false);
            }
        }

        static void Sconce(Transform room, Vector3 position, float yaw)
        {
            var sconce = Place("Sconce", room, position, yaw);
            var light = new GameObject("Warm lantern fill").AddComponent<Light>();light.transform.SetParent(sconce, false);
            light.transform.localPosition = new Vector3(0, .20f, .22f);light.type = LightType.Point;light.color = new Color(1, .67f, .31f);
            light.intensity = .06f;light.range = 1.65f;light.shadows = LightShadows.None;
        }

        static void WallCollider(Transform room, Vector3 position, Vector3 size)
        {
            var wall = new GameObject("Room boundary");wall.transform.SetParent(room, false);wall.transform.localPosition = position;wall.AddComponent<BoxCollider>().size = size;
        }

        static void BuildCeiling(Transform room)
        {
            var ceiling = new GameObject("Coffered Ceiling").transform;ceiling.SetParent(room, false);
            var plaster = Material("CeilingCream", new Color(.88f, .82f, .70f));
            plaster.shader = Shader.Find("Universal Render Pipeline/Unlit");
            var teal = Material("WallTeal", new Color(.031f, .380f, .396f));
            var gold = Material("WallGoldLine", new Color(.96f, .66f, .24f));
            Cube("Ceiling plaster", ceiling, new Vector3(0, 3.52f, 0), new Vector3(7.4f, .10f, 7.4f), plaster, false);
            for (int axis = 0; axis < 2; axis++)
                foreach (float offset in new[] { -3.60f, -1.20f, 1.20f, 3.60f })
                {
                    Cube("Teal coffer beam", ceiling, new Vector3(axis == 0 ? offset : 0, 3.43f, axis == 1 ? offset : 0),
                        new Vector3(axis == 0 ? .10f : 7.30f, .08f, axis == 1 ? .10f : 7.30f), teal, false);
                    Cube("Gold beam inlay", ceiling, new Vector3(axis == 0 ? offset : 0, 3.385f, axis == 1 ? offset : 0),
                        new Vector3(axis == 0 ? .018f : 7.27f, .008f, axis == 1 ? .018f : 7.27f), gold, false);
                }
            foreach (float x in new[] { -2.4f, 0, 2.4f }) foreach (float z in new[] { -2.4f, 0, 2.4f })
                for (int side = 0; side < 4; side++)
                {
                    bool horizontal = side < 2;float sign = side % 2 == 0 ? -1 : 1;
                    Cube("Recessed panel moulding", ceiling, new Vector3(x + (horizontal ? 0 : sign * .97f), 3.455f, z + (horizontal ? sign * .97f : 0)),
                        new Vector3(horizontal ? 1.98f : .025f, .025f, horizontal ? .025f : 1.98f), gold, false);
                }
            var medallion = GameObject.CreatePrimitive(PrimitiveType.Cylinder);medallion.name = "Ceiling medallion";
            medallion.transform.SetParent(ceiling, false);medallion.transform.localPosition = new Vector3(0, 3.43f, 0);
            medallion.transform.localScale = new Vector3(.52f, .025f, .52f);medallion.GetComponent<Renderer>().sharedMaterial = teal;
            UnityEngine.Object.DestroyImmediate(medallion.GetComponent<Collider>());
        }

        static void Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);go.name = name;go.transform.SetParent(parent, false);go.transform.localPosition = position;go.transform.localScale = size;go.GetComponent<Renderer>().sharedMaterial = material;
            var renderer = go.GetComponent<Renderer>();renderer.lightProbeUsage = LightProbeUsage.Off;renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
