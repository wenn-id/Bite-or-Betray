using System.Collections;
using System.IO;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests.PlayMode
{
    public class NewInteriorTests : IPrebuildSetup, IPostBuildCleanup
    {
        public void Setup() => new ChibiLayoutTests().Setup();
        public void Cleanup() => new ChibiLayoutTests().Cleanup();

        [UnityTest]
        public IEnumerator New_interior_preserves_seats_table_height_colliders_and_imported_furniture()
        {
            yield return EditorXR.Stop();yield return SceneManager.LoadSceneAsync("CandyParlour3D");
            for (int i = 0; i < 60; i++) yield return null;
            var root = GameObject.Find("BoB_Art3D").transform;
            Assert.IsNotNull(root.Find("New Interior Layout"));
            Assert.IsFalse(root.Find("Legacy Room").gameObject.activeSelf);
            var room = root.Find("Room");Assert.IsNotNull(room);
#if UNITY_EDITOR
            var mergedRoom = root.Find("Performance Geometry");Assert.IsNotNull(mergedRoom);
            var visibleRoom = root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && !r.forceRenderingOff &&
                (r.transform.IsChildOf(room) || r.transform.IsChildOf(mergedRoom))).ToArray();
            Assert.IsNotEmpty(visibleRoom);
            Assert.IsTrue(visibleRoom.All(r => UnityEditor.GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, UnityEditor.StaticEditorFlags.BatchingStatic)),
                "The budget audit must check rendered furniture batches, not the disabled editable source meshes.");
#endif
            Assert.AreEqual(6, room.GetComponentsInChildren<Light>().Length);
            Assert.AreEqual(6, root.Find("Seats").childCount);
            Assert.AreEqual(6, root.Find("Seats").GetComponentsInChildren<Transform>().Count(t => t.name == "Interior Chair"));
            Assert.IsFalse(root.Find("Table/TableModel").gameObject.activeSelf);
            var table = root.Find("Table/Interior Table").GetComponentInChildren<Renderer>().bounds;
            Assert.AreEqual(.760f, table.max.y, .004f);
            Assert.AreEqual(3.24f, table.size.x, .015f);Assert.AreEqual(3.24f, table.size.z, .015f);
            var tableMesh = root.Find("Table/Interior Table").GetComponentInChildren<MeshFilter>();
            int tableTriangles = tableMesh.sharedMesh.triangles.Length / 3;
            Assert.LessOrEqual(tableTriangles, 3500);
            Assert.AreEqual(1, tableMesh.GetComponent<Renderer>().sharedMaterials.Length);
            var tableCollider = tableMesh.gameObject.AddComponent<MeshCollider>();tableCollider.sharedMesh = tableMesh.sharedMesh;
            foreach (float x in new[] { -1f, -.5f, 0, .5f, 1f }) foreach (float z in new[] { -1f, -.5f, 0, .5f, 1f })
            {
                Assert.IsTrue(tableCollider.Raycast(new Ray(new Vector3(x, 1.0f, z), Vector3.down), out var hit, .5f), "Playing surface has a hole");
                Assert.AreEqual(.758f, hit.point.y, .002f, "Playing surface is uneven");
                Assert.Greater(hit.normal.y, .999f);
            }
            Object.Destroy(tableCollider);
            int chairTriangles = 0;
            foreach (Transform seat in root.Find("Seats"))
            {
                var chair = seat.Find("Chair/Interior Chair");var mesh = chair.GetComponentInChildren<MeshFilter>();
                int triangles = mesh.sharedMesh.triangles.Length / 3;chairTriangles += triangles;
                Assert.LessOrEqual(triangles, 2000);Assert.AreEqual(1, mesh.GetComponent<Renderer>().sharedMaterials.Length);
                var bounds = mesh.GetComponent<Renderer>().bounds;
                Assert.AreEqual(1.18f, bounds.size.y, .005f);Assert.AreEqual(chair.position.y, bounds.min.y, .005f);
                var collider = mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh = mesh.sharedMesh;
                foreach (float height in new[] { .68f, .90f, 1.04f })
                    Assert.IsTrue(collider.Raycast(new Ray(chair.TransformPoint(new Vector3(0, height, .60f)), -chair.forward), out _, 1), "Chair back cushion has a hole");
                Object.Destroy(collider);
            }
            Assert.LessOrEqual(tableTriangles + chairTriangles, 15500, "Table and six chairs exceeded the Quest furniture budget");
            foreach (var renderer in new[] { tableMesh.GetComponent<Renderer>(), root.Find("Seats/Seat_0/Chair/Interior Chair").GetComponentInChildren<Renderer>() })
            {
                var texture = (Texture2D)renderer.sharedMaterial.GetTexture("_BaseMap");
                Assert.LessOrEqual(texture.width, 1024);Assert.LessOrEqual(texture.height, 1024);
                Assert.Greater(texture.mipmapCount, 1);Assert.Less(renderer.sharedMaterial.renderQueue, 3000);
#if UNITY_EDITOR
                var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(texture));
                var android = importer.GetPlatformTextureSettings("Android");
                Assert.IsTrue(android.overridden);Assert.AreEqual(UnityEditor.TextureImporterFormat.ASTC_6x6, android.format);
#endif
            }
            var floorRoot = room.Find("Interior Checker Floor");Assert.IsNotNull(floorRoot);
            Assert.AreEqual(1, floorRoot.GetComponentsInChildren<MeshFilter>().Length, "Floor returned to separate modules");
            Assert.IsNull(floorRoot.Find("Floor grout backing"));
            foreach (string model in new[] { "Floor", "Rug" })
            {
                var prop = model == "Floor" ? floorRoot.Find("Interior Floor") : room.Find("Interior Rug");
                var mesh = prop.GetComponentInChildren<MeshFilter>();var renderer = mesh.GetComponent<Renderer>();var bounds = renderer.bounds;
                Assert.LessOrEqual(mesh.sharedMesh.triangles.Length / 3, model == "Floor" ? 12 : 300);
                Assert.AreEqual(model == "Floor" ? 7.4f : 4.85f, bounds.size.x, .003f);
                Assert.AreEqual(model == "Floor" ? 7.4f : 4.50f, bounds.size.z, .003f);
                Assert.AreEqual(model == "Floor" ? 0 : .011f, bounds.max.y, .001f);
                Assert.AreEqual(1, renderer.sharedMaterials.Length);Assert.Less(renderer.sharedMaterial.renderQueue, 3000);
                var texture = (Texture2D)renderer.sharedMaterial.GetTexture("_BaseMap");
                int limit = model == "Floor" ? 512 : 1024;
                Assert.LessOrEqual(texture.width, limit);Assert.LessOrEqual(texture.height, limit);Assert.Greater(texture.mipmapCount, 1);
                Assert.AreEqual(model == "Floor" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, texture.wrapMode);
                Assert.AreEqual(FilterMode.Trilinear, texture.filterMode);
#if UNITY_EDITOR
                var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(texture));
                var android = importer.GetPlatformTextureSettings("Android");
                Assert.IsTrue(android.overridden);Assert.AreEqual(limit, android.maxTextureSize);
                Assert.AreEqual(UnityEditor.TextureImporterFormat.ASTC_6x6, android.format);
#endif
                var collider = mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh = mesh.sharedMesh;
                var samples = model == "Floor" ? new[] { -3.69f, -2f, -1.2333f, 0, 1.2333f, 2f, 3.69f } : new[] { -1.5f, -.75f, 0, .75f, 1.5f };
                foreach (float x in samples) foreach (float z in samples)
                {
                    Assert.IsTrue(collider.Raycast(new Ray(new Vector3(x, .2f, z), Vector3.down), out var hit, .3f), model + " surface has a gap");
                    Assert.AreEqual(model == "Floor" ? 0 : .011f, hit.point.y, .001f);Assert.Greater(hit.normal.y, .999f);
                }
                Object.Destroy(collider);
            }
            Assert.AreEqual(5, room.GetComponentsInChildren<BoxCollider>().Length);
            var walls = room.Cast<Transform>().Where(t => t.name.StartsWith("Continuous wall ")).ToArray();
            Assert.AreEqual(4, walls.Length);
            Assert.AreEqual(4, room.Cast<Transform>().Count(t => t.name == "Wall corner"));
            Assert.IsFalse(room.GetComponentsInChildren<Transform>().Any(t => t.name == "Interior WallPanel"), "Repeated wall end pillars returned");
            foreach (var wall in walls)
            {
                var surface = wall.Find("Peach plaster").GetComponent<Renderer>().bounds;
                Assert.AreEqual(7.4f, Mathf.Max(surface.size.x, surface.size.z), .001f, "Wall is not one continuous span");
            }
            var geometry = root.Find("Performance Geometry");Assert.IsNotNull(geometry);
            var materials = geometry.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m.name).ToArray();
            foreach (string model in new[] { "Table", "Chair", "Door", "Window", "Rug", "Shelves", "Display", "Sconce", "Picture", "Floor" })
                CollectionAssert.Contains(materials, "Interior" + model, "Missing imported interior model: " + model);
            CollectionAssert.Contains(materials, "WallPeach");CollectionAssert.Contains(materials, "WallTeal");
            var door = room.Find("Interior Door");var doorMesh = door.GetComponentInChildren<MeshFilter>();
            Assert.Less(doorMesh.sharedMesh.triangles.Length / 3, 3500);
            var doorCollider = doorMesh.gameObject.AddComponent<MeshCollider>();doorCollider.sharedMesh = doorMesh.sharedMesh;
            foreach (float x in new[] { -.54f, -.30f, 0, .30f, .54f }) foreach (float height in new[] { .25f, .51f, .75f })
                Assert.IsTrue(doorCollider.Raycast(new Ray(door.TransformPoint(new Vector3(x, height, .6f)), -door.forward), out _, 1), "Door panel has a hole");
            Object.Destroy(doorCollider);
            foreach (string model in new[] { "Window", "Shelves", "Display" })
            {
                var furniture = room.Find("Interior " + model);var mesh = furniture.GetComponentInChildren<MeshFilter>();
                Assert.Less(mesh.sharedMesh.triangles.Length / 3, 3500, model + " exceeded its geometry budget");
                Assert.AreEqual(1, mesh.GetComponent<Renderer>().sharedMaterials.Length);
                Assert.AreEqual(model == "Window" ? 1.85f : 1.20f, mesh.GetComponent<Renderer>().bounds.size.y, .005f);
                Assert.AreEqual(furniture.position.y, mesh.GetComponent<Renderer>().bounds.min.y, .005f, model + " base origin shifted");
                var collider = mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh = mesh.sharedMesh;
                if (model == "Window")
                    foreach (float x in new[] { -.60f, -.20f, .20f, .60f }) foreach (float height in new[] { .30f, .70f, 1.10f })
                        Assert.IsTrue(collider.Raycast(new Ray(furniture.TransformPoint(new Vector3(x, height, .6f)), -furniture.forward), out _, 1), "Window pane has a hole");
                else
                    foreach (float height in model == "Shelves" ? new[] { .23f, .615f, 1.00f } : new[] { .085f, .405f, .725f })
                        foreach (float x in new[] { -.40f, 0, .40f })
                        {
                            Assert.IsTrue(collider.Raycast(new Ray(furniture.TransformPoint(new Vector3(x, height + .09f, .17f)), -furniture.up), out var hit, .15f), model + " shelf has a hole at " + x + ", " + height);
                            Assert.Greater(Vector3.Dot(hit.normal, furniture.up), .99f, model + " shelf is not flat");
                        }
                Object.Destroy(collider);
            }
            foreach (string model in new[] { "Sconce", "Picture" })
            {
                var props = room.Cast<Transform>().Where(t => t.name == "Interior " + model).ToArray();
                Assert.AreEqual(model == "Sconce" ? 6 : 4, props.Length);
                foreach (var prop in props)
                {
                    var mesh = prop.GetComponentInChildren<MeshFilter>();var renderer = mesh.GetComponent<Renderer>();
                    Assert.LessOrEqual(mesh.sharedMesh.triangles.Length / 3, model == "Sconce" ? 1100 : 200);
                    Assert.AreEqual(model == "Sconce" ? .52f : .43f, renderer.bounds.size.y, .003f);
                    Assert.AreEqual(prop.position.y, renderer.bounds.min.y, .003f);
                    Assert.AreEqual(1, renderer.sharedMaterials.Length);Assert.Less(renderer.sharedMaterial.renderQueue, 3000);
                    var texture = (Texture2D)renderer.sharedMaterial.GetTexture("_BaseMap");
                    Assert.LessOrEqual(texture.width, 512);Assert.LessOrEqual(texture.height, 512);Assert.Greater(texture.mipmapCount, 1);
#if UNITY_EDITOR
                    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(texture));
                    var android = importer.GetPlatformTextureSettings("Android");
                    Assert.IsTrue(android.overridden);Assert.AreEqual(512, android.maxTextureSize);
                    Assert.AreEqual(UnityEditor.TextureImporterFormat.ASTC_6x6, android.format);
#endif
                    if (model == "Sconce")
                    {
                        Assert.IsTrue(renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"));
                        var emissionMask = (Texture2D)renderer.sharedMaterial.GetTexture("_EmissionMap");Assert.IsNotNull(emissionMask);Assert.LessOrEqual(emissionMask.width, 128);
                        var light = prop.GetComponentInChildren<Light>();Assert.IsNotNull(light);
                        Assert.AreEqual(LightShadows.None, light.shadows);
                    }
                    else
                    {
                        var collider = mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh = mesh.sharedMesh;
                        foreach (float x in new[] { -.10f, 0, .10f }) foreach (float height in new[] { .10f, .21f, .32f })
                            Assert.IsTrue(collider.Raycast(new Ray(prop.TransformPoint(new Vector3(x, height, .10f)), -prop.forward), out _, .2f), "Floral print has a hole");
                        Object.Destroy(collider);
                    }
                }
            }
            var stockedRacks = room.Cast<Transform>().Where(t => t.name == "Interior Shelves" || t.name == "Interior Display").ToArray();
            Assert.AreEqual(4, stockedRacks.Length);
            Material sharedJarMaterial = null;int totalJars = 0;
            foreach (var rack in stockedRacks)
            {
                bool arched = rack.name == "Interior Display";
                var jars = rack.Cast<Transform>().Where(t => t.name.StartsWith("Interior CandyJar")).ToArray();
                Assert.AreEqual(9, jars.Length);totalJars += jars.Length;
                foreach (string color in new[] { "Red", "Blue", "Yellow" }) Assert.AreEqual(3, jars.Count(t => t.name == "Interior CandyJar" + color));
                var rackMesh = rack.GetComponentInChildren<MeshFilter>();var rackCollider = rackMesh.gameObject.AddComponent<MeshCollider>();rackCollider.sharedMesh = rackMesh.sharedMesh;
                var jarBounds = new System.Collections.Generic.List<Bounds>();
                foreach (var jar in jars)
                {
                    var mesh = jar.GetComponentInChildren<MeshFilter>();var renderer = mesh.GetComponent<Renderer>();
                    Assert.AreEqual(1000, mesh.sharedMesh.triangles.Length / 3);Assert.AreEqual(1, renderer.sharedMaterials.Length);
                    if (sharedJarMaterial == null) sharedJarMaterial = renderer.sharedMaterial;
                    Assert.AreSame(sharedJarMaterial, renderer.sharedMaterial);Assert.Less(sharedJarMaterial.renderQueue, 3000);
                    Assert.IsFalse(renderer.enabled, "Jar escaped static material merging");Assert.AreEqual(0, jar.GetComponentsInChildren<Collider>().Length);
                    Assert.AreEqual(arched ? .24f : .30f, renderer.bounds.size.y, .002f);
                    Assert.IsTrue(rackCollider.Raycast(new Ray(jar.position + Vector3.up * .01f, Vector3.down), out var shelfHit, .05f), "Jar is not over a shelf");
                    Assert.AreEqual(.001f, renderer.bounds.min.y - shelfHit.point.y, .0015f, "Jar floats or clips into the board");
                    if (rackCollider.Raycast(new Ray(jar.position + Vector3.up * (renderer.bounds.size.y + .002f), Vector3.up), out var above, .40f))
                        Assert.Greater(above.distance, .01f, "Jar knob is too close to the board above");
                    var bounds = new Bounds(rack.InverseTransformPoint(jar.position), Vector3.zero);
                    foreach (var vertex in mesh.sharedMesh.vertices)
                    {
                        var local = rack.InverseTransformPoint(mesh.transform.TransformPoint(vertex));bounds.Encapsulate(local);
                        Assert.That(local.z, Is.InRange(arched ? .04f : .03f, arched ? .261f : .301f), "Jar extends beyond shelf depth");
                        Assert.LessOrEqual(Mathf.Abs(local.x), arched ? .486f : .55f);
                        if (arched && local.y > .65f) Assert.Less(new Vector2(local.x, local.y - .65f).magnitude, .487f, "Jar clips through the arch");
                    }
                    foreach (var previousJar in jarBounds) Assert.IsFalse(bounds.Intersects(previousJar), "Decorative jars overlap");
                    jarBounds.Add(bounds);
                }
                Object.Destroy(rackCollider);
            }
            Assert.AreEqual(36, totalJars);Assert.IsNotNull(sharedJarMaterial);
            var jarTexture = (Texture2D)sharedJarMaterial.GetTexture("_BaseMap");Assert.AreEqual(1024, jarTexture.width);Assert.Greater(jarTexture.mipmapCount, 1);
#if UNITY_EDITOR
            var jarImporter = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(jarTexture));
            var jarAndroid = jarImporter.GetPlatformTextureSettings("Android");Assert.IsTrue(jarAndroid.overridden);
            Assert.AreEqual(1024, jarAndroid.maxTextureSize);Assert.AreEqual(UnityEditor.TextureImporterFormat.ASTC_6x6, jarAndroid.format);
#endif
            var mergedJars = geometry.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<Renderer>().sharedMaterial == sharedJarMaterial).ToArray();
            Assert.AreEqual(4, mergedJars.Length);
            foreach (var batch in mergedJars)
            {
#if UNITY_EDITOR
                // Unity may replace sharedMesh with an unreadable static buffer during Play.
                var savedBatch = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BiteOrBetray/PerformanceMeshes/" + batch.name + ".asset");
                Assert.IsNotNull(savedBatch);Assert.AreEqual(9000, (int)savedBatch.GetIndexCount(0) / 3);Assert.AreEqual(1, savedBatch.subMeshCount);
#endif
                var bounds = batch.GetComponent<Renderer>().bounds;
                Assert.Less(Mathf.Max(bounds.size.x, bounds.size.z), 1.2f, "Jar batch spans multiple racks");
            }
            foreach (var anim in root.GetComponentsInChildren<CharacterAnimator>()) anim.enabled = false;
            var director = Object.FindFirstObjectByType<CandyTableDirector>();
            foreach (var text in director.GetComponentsInChildren<TextMesh>(true)) text.gameObject.SetActive(false);
            var camera = Camera.main;Assert.IsNotNull(camera);
            var before = camera.transform.position;var rotation = camera.transform.rotation;float authoredFov = camera.fieldOfView;
            Capture(camera, "overview.png");
            camera.fieldOfView = 50;
            camera.transform.SetPositionAndRotation(new Vector3(-2.38f, 2.05f, 1.75f), Quaternion.LookRotation(new Vector3(-2.38f, 1.95f, 3.40f) - new Vector3(-2.38f, 2.05f, 1.75f)));
            Capture(camera, "jar-rack.png");camera.fieldOfView = authoredFov;
            camera.transform.SetPositionAndRotation(new Vector3(-2.75f, 2.65f, -2.80f), Quaternion.LookRotation(new Vector3(0, .95f, .20f) - new Vector3(-2.75f, 2.65f, -2.80f)));
            Capture(camera, "reverse-angle.png");
            camera.transform.SetPositionAndRotation(root.Find("Seats/Seat_0").position + Vector3.up * 1.27f, Quaternion.LookRotation(Vector3.forward));
            camera.fieldOfView = 85;
            yield return null;yield return null;
            Capture(camera, "player-seat.png");
            camera.transform.SetPositionAndRotation(before, rotation);
            int mask = camera.cullingMask;var background = camera.backgroundColor;
            camera.cullingMask = 1 << 29;camera.backgroundColor = new Color(.18f, .20f, .22f);camera.fieldOfView = 48;
            foreach (string model in new[] { "Door", "Window", "Shelves", "Display", "Table", "Chair", "Sconce", "Picture", "Floor", "Rug", "CandyJarRed", "CandyJarBlue", "CandyJarYellow" })
            {
                var source = model == "Table" ? root.Find("Table/Interior Table") : model == "Chair" ? root.Find("Seats/Seat_0/Chair/Interior Chair") : model == "Floor" ? floorRoot.Find("Interior Floor") : model.StartsWith("CandyJar") ? stockedRacks[0].Find("Interior " + model) : room.Find("Interior " + model);
                var preview = Object.Instantiate(source.gameObject);preview.transform.SetPositionAndRotation(new Vector3(0, 8, 0), Quaternion.identity);
                foreach (var part in preview.GetComponentsInChildren<Transform>()) part.gameObject.layer = 29;
                foreach (var renderer in preview.GetComponentsInChildren<Renderer>()) { renderer.enabled = true;renderer.forceRenderingOff = false; }
                var bounds = preview.GetComponentInChildren<Renderer>().bounds;float size = Mathf.Max(bounds.size.x, bounds.size.y);
                var position = new Vector3(size * .24f, 8 + bounds.size.y * .68f, size * 1.45f);
                if (model == "Table") position = new Vector3(1.5f, 11.5f, 4.2f);
                if (model == "Floor") position = new Vector3(2.5f, 15.5f, 8.5f);
                if (model == "Rug") position = new Vector3(1.0f, 12.8f, 5.6f);
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(new Vector3(0, 8 + bounds.size.y * .50f, 0) - position));
                Capture(camera, model.ToLowerInvariant() + "-detail.png");
                if (model == "Table" || model == "Chair")
                {
                    var jointPosition = model == "Table" ? new Vector3(2.8f, 8.32f, 3.4f) : new Vector3(.75f, 8.28f, 1.45f);
                    var jointTarget = new Vector3(0, model == "Table" ? 8.45f : 8.56f, 0);
                    camera.transform.SetPositionAndRotation(jointPosition, Quaternion.LookRotation(jointTarget - jointPosition));
                    Capture(camera, model.ToLowerInvariant() + "-joints.png");
                }
                Object.DestroyImmediate(preview);
            }
            camera.cullingMask = mask;camera.backgroundColor = background;camera.transform.SetPositionAndRotation(before, rotation);
        }

        static void Capture(Camera camera, string name)
        {
            Directory.CreateDirectory("Logs/NewInterior");
            var target = RenderTexture.GetTemporary(1600, 1000, 24);var old = RenderTexture.active;var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;camera.Render();RenderTexture.active = target;
                var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);image.Apply();File.WriteAllBytes("Logs/NewInterior/" + name, image.EncodeToPNG());Object.Destroy(image);
            }
            finally { camera.targetTexture = previous;RenderTexture.active = old;RenderTexture.ReleaseTemporary(target); }
        }
    }
}
