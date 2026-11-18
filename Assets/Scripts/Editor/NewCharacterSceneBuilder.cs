using System;
using System.IO;
using System.Linq;
using BiteOrBetray.Art3D;
using BiteOrBetray.Table3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BiteOrBetray.EditorTools
{
    public static class NewCharacterSceneBuilder
    {
        const string Folder = "Assets/BiteOrBetray/NewCharacters";
        const string Scene = "Assets/BiteOrBetray/Scenes/CandyParlour3D.unity";
        const float PlayerVisualScale = 1.15f;
        const float PlayerHandScale = 1.58f;
        const float PlayerHandRestX = .27f;
        static readonly string[] Models = { "Blond", "Silver", "Mint", "Navy", "Peach", "Charcoal", "Reaper", "HandLeft", "HandRight", "ServingCloche" };
        static readonly string[] Bots = { "Yellow", "Blue", "Red", "Mint", "Peach" };

        [MenuItem("Bite or Betray/Apply New Chibi Characters")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            foreach (string name in Models) PrepareModel(name);
            var scene = EditorSceneManager.OpenScene(Scene);
            var root = GameObject.Find("BoB_Art3D").transform;
            var previousGeometry = root.Find("Performance Geometry");
            if (previousGeometry != null) UnityEngine.Object.DestroyImmediate(previousGeometry.gameObject);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.GetComponentsInParent<Transform>(true).Any(t => t.name == "Table" || t.name == "Chair" || t.name == "ItemMat" || t.name == "Room")) r.enabled = true;
            var seats = root.Find("Seats");
            var mats = root.Cast<Transform>().Where(t => t.name == "ItemMat").ToArray();
            var pads = Enumerable.Range(0, 6).Select(i => mats.OrderBy(m => (m.position - seats.Find("Seat_" + i).position).sqrMagnitude).First()).ToArray();
            if (pads.Distinct().Count() != 6) throw new InvalidOperationException("Seat pad mapping is ambiguous.");
            for (int i = 0; i < 6; i++)
            {
                var seat = seats.Find("Seat_" + i);
                float angle = i * 60 * Mathf.Deg2Rad;
                var direction = new Vector3(-Mathf.Sin(angle), 0, -Mathf.Cos(angle));
                seat.SetLocalPositionAndRotation(direction * 1.62f, Quaternion.LookRotation(-direction));
                pads[i].position = direction * 1.13f + Vector3.up * .765f;
                pads[i].rotation = seat.rotation * Quaternion.Euler(-90, 0, 0);
                // Keep the imported pad's authored axis correction.
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(pads[i].gameObject);
                if (source != null) pads[i].rotation = seat.rotation * source.transform.localRotation;
                var anchor = seat.Find("HeadAnchor");anchor.localPosition = new Vector3(0, 1.27f, 0);
                foreach (Transform child in seat.Cast<Transform>().ToArray())
                    if (child.name != "HeadAnchor" && child.name != "Chair") UnityEngine.Object.DestroyImmediate(child.gameObject);
                CreatePlayer(i == 0 ? "Local Avatar" : "Bot_" + Bots[i - 1], Models[i], seat, i == 0);
                var upholstery = new GameObject("Chair upholstery").transform;upholstery.SetParent(seat, false);
                var velvet = Material("ChairVelvet", new Color(.28f, .035f, .065f));
                Primitive("Padded chair back", PrimitiveType.Sphere, upholstery, new Vector3(0, .82f, -.16f), new Vector3(.48f, .65f, .12f), velvet);
                Primitive("Padded chair seat", PrimitiveType.Sphere, upholstery, new Vector3(0, .47f, -.02f), new Vector3(.48f, .085f, .44f), velvet);
            }
            var table = root.Find("Table");
            if (table.Find("TableModel") == null)
            {
                table.name = "TableModel";
                var wrapper = new GameObject("Table").transform;wrapper.SetParent(root, false);
                table.SetParent(wrapper, false);wrapper.localScale = new Vector3(1.82f, 1, 1.82f);
            }
            var oldWaiter = root.Find("Waiter");if (oldWaiter != null) UnityEngine.Object.DestroyImmediate(oldWaiter.gameObject);
            var waiter = new GameObject("Waiter").transform;waiter.SetParent(root, false);
            waiter.localPosition = new Vector3(2.25f, .08f, 2.15f);waiter.rotation = Quaternion.LookRotation(new Vector3(-waiter.position.x, 0, -waiter.position.z));
            var waiterHead = new GameObject("Head").transform;waiterHead.SetParent(waiter, false);
            AddModel("Reaper", waiterHead);waiterHead.localScale = Vector3.one * 1.42f;
            AddCloche(waiterHead);
            var animator = waiter.gameObject.AddComponent<CharacterAnimator>();animator.idleStrength = .15f;animator.lookStrength = .1f;animator.bodyPivotHeight = .6f;
            var oldDispenser = root.Find("Dispenser");if (oldDispenser != null) UnityEngine.Object.DestroyImmediate(oldDispenser.gameObject);
            BuildDish(root);
            var tray = root.Find("CandyTray");if (tray != null) tray.localPosition = new Vector3(0, .77f, -.48f);
            foreach (string color in new[] { "Yellow", "Blue", "Red" })
            { var candy = root.Find("Candy_" + color);if (candy != null) candy.localPosition = new Vector3(0, .815f, -.48f); }
            if (root.Find("Chibi Table Layout") == null) new GameObject("Chibi Table Layout").transform.SetParent(root, false);
            var preview = UnityEngine.Object.FindFirstObjectByType<ArtPreviewControls>(FindObjectsInactive.Include);
            preview.tableOverview = true;preview.GetComponent<Camera>().fieldOfView = 52;
            preview.SetTableOverview();
            var director = UnityEngine.Object.FindFirstObjectByType<CandyTableDirector>();
            director.botNames = new[] { "Silver", "Mint", "Navy", "Peach", "Charcoal" };
            var tracking = root.GetComponentInChildren<LocalAvatarTracking>(true);
            if (tracking == null) throw new InvalidOperationException("Local tracking was not installed.");
            var rig = scene.GetRootGameObjects().First(g => g.name.Contains("Camera Rig"));
            rig.transform.position = seats.Find("Seat_0").position + Vector3.up * 1.27f;
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true)) { r.enabled = false;r.forceRenderingOff = true; }
            if (root.Find("New Interior Layout") != null) NewInteriorSceneBuilder.ApplyToScene();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/NewCharacters");
            Capture(preview.GetComponent<Camera>(), "Logs/NewCharacters/overview.png");
            QuestPerfTestBuilder.BuildScene();
            EditorSceneManager.OpenScene(Scene);
            Debug.Log("NEW_CHARACTERS_APPLIED: six unique heads, twelve gloves, floating reaper, expanded round table and low candy dish.");
        }

        static void PrepareModel(string name)
        {
            string path = Folder + "/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new FileNotFoundException(path);
            importer.importAnimation = false;importer.isReadable = true;importer.importBlendShapes = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var original in model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
            {
                string materialName = original.name;
                string texturePath = Folder + "/" + materialName + "_BaseColor.png";
                var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (textureImporter == null) throw new FileNotFoundException(texturePath);
                textureImporter.maxTextureSize = materialName == "ParlourProps" ? 512 : 4096;
                textureImporter.textureCompression = TextureImporterCompression.Compressed;
                if (materialName == "ParlourProps")
                {
                    var android = textureImporter.GetPlatformTextureSettings("Android");
                    android.overridden = true;android.maxTextureSize = 512;android.format = TextureImporterFormat.ASTC_6x6;
                    textureImporter.SetPlatformTextureSettings(android);
                }
                textureImporter.SaveAndReimport();
                var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + materialName + ".mat");
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material, Folder + "/" + materialName + ".mat"); }
                bool face = Array.IndexOf(Models, name) < 6;
                material.shader = Shader.Find(face ? "BiteOrBetray/ChibiFace" : "Universal Render Pipeline/Lit");
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));material.SetColor("_BaseColor", Color.white);
                if (face)
                {
                    foreach (string expression in new[] { "Blink", "Mouth" })
                    {
                        string expressionPath = Folder + "/" + name + "_" + expression + ".png";
                        var expressionImporter = (TextureImporter)AssetImporter.GetAtPath(expressionPath);
                        expressionImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                        expressionImporter.alphaIsTransparency = true;expressionImporter.maxTextureSize = 2048;
                        expressionImporter.textureCompression = TextureImporterCompression.Compressed;expressionImporter.SaveAndReimport();
                        material.SetTexture("_" + expression + "Map", AssetDatabase.LoadAssetAtPath<Texture2D>(expressionPath));
                    }
                    material.SetFloat("_FaceBlink", 0);material.SetFloat("_FaceMouth", 0);
                }
                else { material.SetFloat("_Smoothness", .24f);material.SetFloat("_Metallic", 0); }
                material.enableInstancing = true;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), original.name), material);
            }
            importer.SaveAndReimport();
        }

        static GameObject AddModel(string model, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + model + ".fbx"), parent);
            go.transform.localPosition = Vector3.zero;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off;r.lightProbeUsage = LightProbeUsage.BlendProbes; }
            return go;
        }

        static void CreatePlayer(string name, string model, Transform seat, bool local)
        {
            var root = new GameObject(name);root.transform.SetParent(seat, false);
            var head = new GameObject("Head").transform;head.SetParent(root.transform, false);head.localPosition = new Vector3(0, 1.27f, 0);head.localScale = Vector3.one * PlayerVisualScale;
            var headModel = AddModel(model, head);
            var face = headModel.GetComponentInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < face.sharedMesh.blendShapeCount; i++) face.SetBlendShapeWeight(i, 0);
            face.gameObject.AddComponent<ChibiFace>();
            AddMouthAnchor(head);
            var eye = new GameObject("Avatar Eye Reference").transform;eye.SetParent(head, false);eye.localPosition = new Vector3(0, -.13f, .19f);
            var left = Hand(root.transform, "Left", -PlayerHandRestX);var right = Hand(root.transform, "Right", PlayerHandRestX);
            var anim = root.AddComponent<CharacterAnimator>();anim.bodyPivotHeight = 1.1f;anim.neckDrop = .03f;anim.idleStrength = .3f;anim.lookStrength = .35f;anim.trackingDrivenBody = local;
            var hands = root.AddComponent<ChibiHands>();hands.character = anim;hands.left = BindHand(left);hands.right = BindHand(right);
            if (!local) return;
            var avatar = root.AddComponent<SeatedAvatar>();avatar.seat = seat;avatar.head = head;avatar.eyeReference = eye;
            avatar.left = hands.left;avatar.right = hands.right;hands.avatar = avatar;
            avatar.headRenderers = headModel.GetComponentsInChildren<Renderer>();
            var reactions = root.AddComponent<AvatarReactionController>();reactions.face = anim;
            var remote = root.AddComponent<RemoteAvatarRepresentation>();remote.avatar = avatar;remote.enabled = false;
            var rig = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name.Contains("Camera Rig"));
            var target = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == "CenterEyeAnchor");
            var tracking = root.AddComponent<LocalAvatarTracking>();tracking.avatar = avatar;tracking.headTarget = target;tracking.firstPersonCamera = target.GetComponent<Camera>();
            tracking.tableInput = UnityEngine.Object.FindFirstObjectByType<VRTableInput>();hands.input = tracking.tableInput;tracking.controllerRenderers = rig.GetComponentsInChildren<Renderer>(true);
        }

        static Transform Hand(Transform parent, string side, float x)
        {
            var hand = new GameObject("Arm_" + side).transform;hand.SetParent(parent, false);hand.localPosition = new Vector3(x, .83f, .35f);
            var visual = AddModel("Hand" + side, hand);
            var markers = visual.GetComponentsInChildren<Transform>();
            var palm = markers.Single(t => t.name == "PalmCenter");
            var normal = markers.Single(t => t.name == "PalmNormal");
            var forward = markers.Single(t => t.name == "FingerForward");
            var fingersDirection = hand.InverseTransformDirection(forward.position - palm.position);
            var palmDirection = hand.InverseTransformDirection(normal.position - palm.position);
            visual.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(fingersDirection, -palmDirection)) * visual.transform.localRotation;
            visual.transform.localPosition -= hand.InverseTransformVector(palm.position - hand.position);
            hand.localScale = Vector3.one * PlayerHandScale;
            return hand;
        }

        static SeatedAvatar.Hand BindHand(Transform root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            var palm = all.Single(t => t.name == "PalmCenter");
            var palmNormal = all.Single(t => t.name == "PalmNormal");
            var fingers = all.Where(t => t.name.StartsWith("Finger_")).OrderBy(t => t.name).ToArray();
            if (fingers.Length != 15) throw new InvalidOperationException("Glove needs fifteen finger joints: " + root.name);
            var axes = new Vector3[fingers.Length];var angles = new float[fingers.Length];var index = new bool[fingers.Length];
            for (int i = 0; i < fingers.Length; i++)
            {
                var joint = fingers[i];bool first = joint.name.EndsWith("_1");
                Vector3 direction = joint.GetChild(0).position - joint.position;
                var inward = (palmNormal.position - palm.position).normalized;
                if (joint.name.Contains("Thumb") && first)
                {
                    var target = all.Single(t => t.name == "CandyGrip").position - joint.position;
                    axes[i] = joint.InverseTransformDirection(Vector3.Cross(direction, target)).normalized;
                }
                else axes[i] = joint.InverseTransformDirection(Vector3.Cross(direction, inward)).normalized;
                int segment = joint.name[joint.name.Length - 1] - '1';
                angles[i] = joint.name.Contains("Thumb") ? new[] { 45f, 30f, 25f }[segment] : new[] { 40f, 60f, 35f }[segment];
                index[i] = joint.name.Contains("Index");
            }
            return new SeatedAvatar.Hand { hand = root, restPosition = root.localPosition, fingers = fingers, fingerAxes = axes, fingerAngles = angles,
                indexFingers = index, handRotationOffset = root.localRotation, gripAnchor = all.Single(t => t.name == "CandyGrip") };
        }

        static void AddCloche(Transform waiterHead)
        {
            var old = waiterHead.Find("Serving Cloche");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var tray = new GameObject("Serving Cloche").transform;tray.SetParent(waiterHead, false);
            tray.localPosition = new Vector3(0, .84f, .23f);tray.localScale = Vector3.one * .93f;
            AddModel("ServingCloche", tray);
        }

        [MenuItem("Bite or Betray/Polish Hands, Waiter and Ceiling")]
        public static void ApplyPresentationPolish()
        {
            AssetDatabase.Refresh();
            foreach (string name in new[] { "HandLeft", "HandRight", "ServingCloche", "Reaper" }) PrepareModel(name);
            var scene = EditorSceneManager.OpenScene(Scene);
            var root = GameObject.Find("BoB_Art3D").transform;
            RefreshGloves(root);
            AddCloche(root.Find("Waiter/Head"));
            NewInteriorSceneBuilder.ApplyToScene();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            QuestPerfTestBuilder.BuildScene();EditorSceneManager.OpenScene(Scene);
            Debug.Log("PRESENTATION_POLISH_APPLIED: rounded gloves, calibrated wrists, serving cloche and coffered ceiling.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Bite or Betray/Apply Glove Revision")]
        public static void ApplyGloveRevision()
        {
            AssetDatabase.Refresh();
            foreach (string name in new[] { "HandLeft", "HandRight" }) PrepareModel(name);
            var scene = EditorSceneManager.OpenScene(Scene);
            var root = GameObject.Find("BoB_Art3D").transform;
            RefreshGloves(root);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            QuestPerfTestBuilder.BuildScene();EditorSceneManager.OpenScene(Scene);
            Debug.Log("GLOVE_REVISION_APPLIED: SDK joint topology, palm-centered tracking and fifteen finger joints per hand.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void AddMouthAnchor(Transform head)
        {
            var skin = head.GetComponentInChildren<SkinnedMeshRenderer>();
            var map = (Texture2D)skin.sharedMaterial.GetTexture("_MouthMap");
            var target = RenderTexture.GetTemporary(map.width, map.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(map.width, map.height, TextureFormat.RGBA32, false, true);
            var mesh = new Mesh();
            try
            {
                Graphics.Blit(map, target);RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, map.width, map.height), 0, 0);pixels.Apply();
                skin.BakeMesh(mesh);var vertices = mesh.vertices;var uv = mesh.uv;var triangles = mesh.triangles;
                var areas = new float[vertices.Length];
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    float area = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).magnitude / 6;
                    areas[a] += area;areas[b] += area;areas[c] += area;
                }
                Vector3 center = Vector3.zero;float weight = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var point = head.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));
                    float alpha = pixels.GetPixelBilinear(uv[i].x, uv[i].y).a;
                    if (alpha < .5f || point.z <= 0) continue;
                    float sample = alpha * areas[i];
                    center += point * sample;weight += sample;
                }
                if (weight <= 1e-8f) throw new InvalidOperationException("No authored mouth pixels on " + skin.name);
                var marker = head.Find("MouthAnchor");
                if (marker == null) { marker = new GameObject("MouthAnchor").transform;marker.SetParent(head, false); }
                marker.localPosition = center / weight;
                Debug.Log($"MOUTH_ANCHOR: {skin.name} local={marker.localPosition:F4}, surface weight={weight:F6}");
            }
            finally
            {
                RenderTexture.active = previous;RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [MenuItem("Bite or Betray/Calibrate Chibi Mouths")]
        public static void CalibrateMouths()
        {
            var scene = EditorSceneManager.OpenScene(Scene);
            var players = GameObject.Find("BoB_Art3D/Seats").GetComponentsInChildren<ChibiHands>(true);
            if (players.Length != 6) throw new InvalidOperationException("Expected six chibi players.");
            foreach (var player in players) AddMouthAnchor(player.transform.Find("Head"));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            QuestPerfTestBuilder.BuildScene();EditorSceneManager.OpenScene(Scene);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void RefreshGloves(Transform root)
        {
            foreach (Transform seat in root.Find("Seats"))
            {
                var hands = seat.GetComponentInChildren<ChibiHands>(true);
                UnityEngine.Object.DestroyImmediate(hands.left.hand.gameObject);
                UnityEngine.Object.DestroyImmediate(hands.right.hand.gameObject);
                hands.left = BindHand(Hand(hands.transform, "Left", -PlayerHandRestX));
                hands.right = BindHand(Hand(hands.transform, "Right", PlayerHandRestX));
                var avatar = hands.GetComponent<SeatedAvatar>();
                if (avatar != null) { avatar.left = hands.left;avatar.right = hands.right; }
                var tracking = hands.GetComponent<LocalAvatarTracking>();
                if (tracking != null) tracking.controllerWristOffset = Vector3.zero;
            }
        }

        [MenuItem("Bite or Betray/Enlarge All Player Hands")]
        public static void ApplyEnlargedPlayerHands()
        {
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var root = GameObject.Find("BoB_Art3D").transform;
            foreach (Transform seat in root.Find("Seats"))
            {
                var hands = seat.GetComponentInChildren<ChibiHands>(true);
                if (hands == null) continue;
                UpdateHandScaleAndRest(hands.left, -PlayerHandRestX);
                UpdateHandScaleAndRest(hands.right, PlayerHandRestX);
                EditorUtility.SetDirty(hands);
                var avatar = hands.GetComponent<SeatedAvatar>();
                if (avatar != null)
                {
                    UpdateHandScaleAndRest(avatar.left, -PlayerHandRestX);
                    UpdateHandScaleAndRest(avatar.right, PlayerHandRestX);
                    EditorUtility.SetDirty(avatar);
                }
            }
            GameplayPerformanceBuilder.Optimize(root, false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            QuestPerfTestBuilder.BuildScene();
            QuestPerfTestBuilder.Audit();
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            Debug.Log($"ENLARGED_PLAYER_HANDS_APPLIED: scale={PlayerHandScale:F2}, restX=+/-{PlayerHandRestX:F2}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void UpdateHandScaleAndRest(SeatedAvatar.Hand h, float restX)
        {
            if (h.hand == null) return;
            h.hand.localScale = Vector3.one * PlayerHandScale;
            var lp = h.hand.localPosition;
            lp.x = restX;
            h.hand.localPosition = lp;
            h.restPosition = lp;
            EditorUtility.SetDirty(h.hand);
        }

        static Material Material(string name, Color color, float metallic = 0)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m, path); }
            m.color = color;m.SetFloat("_Metallic", metallic);m.SetFloat("_Smoothness", .38f);return m;
        }
        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);go.name = name;go.transform.SetParent(parent, false);go.transform.localPosition = pos;go.transform.localScale = scale;go.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void BuildDish(Transform root)
        {
            var dish = new GameObject("Dispenser").transform;dish.SetParent(root, false);dish.localPosition = new Vector3(0, .765f, .08f);
            var gold = Material("DishGold", new Color(.74f, .43f, .12f), .55f);var teal = Material("DishTeal", new Color(.025f, .19f, .18f));
            Primitive("Gold low rim", PrimitiveType.Cylinder, dish, new Vector3(0, .018f, 0), new Vector3(.94f, .018f, .53f), gold);
            Primitive("Three compartment dish", PrimitiveType.Cylinder, dish, new Vector3(0, .036f, 0), new Vector3(.91f, .012f, .50f), teal);
            for (int i = -1; i <= 1; i += 2) Primitive("Divider", PrimitiveType.Cube, dish, new Vector3(i * .155f, .058f, 0), new Vector3(.012f, .045f, .4f), gold);
            var colors = new[] { new Color(.85f, .025f, .075f), new Color(.025f, .22f, .95f), new Color(1, .68f, .025f) };
            for (int c = 0; c < 3; c++)
            {
                var material = Material("DishCandy" + c, colors[c]);
                for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++)
                { float x = (c - 1) * .29f + (col - 1.5f) * .052f;float z = (row - 1.5f) * .075f;if (x * x / .19f + z * z / .045f > .85f) continue;
                    Primitive("Candy " + c, PrimitiveType.Sphere, dish, new Vector3(x, .071f + ((row + col) % 2) * .007f, z), Vector3.one * .052f, material); }
            }
            var spawn = new GameObject("CandySpawnPoint").transform;spawn.SetParent(dish, false);spawn.localPosition = new Vector3(0, .115f, 0);
        }
        static void Capture(Camera camera, string path)
        {
            var rt = RenderTexture.GetTemporary(1600, 1000, 24);var before = RenderTexture.active;
            try
            {
                camera.aspect = 1.6f;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });RenderTexture.active = rt;
                var tex = new Texture2D(1600, 1000, TextureFormat.RGB24, false);tex.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);tex.Apply();File.WriteAllBytes(path, tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
            }
            finally { RenderTexture.active = before;RenderTexture.ReleaseTemporary(rt); }
        }
    }
}
