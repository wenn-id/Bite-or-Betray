using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BiteOrBetray.EditorTools
{
    /// <summary>
    /// Membuat lingkungan placeholder di scene yang sedang terbuka: lantai, meja bundar, 6 kursi,
    /// dispenser, wadah permen aktif, area item tiap kursi, dan beberapa benda untuk tes grab.
    /// Semua bentuk masih primitif (kubus/silinder). Nanti diganti aset asli.
    /// </summary>
    public static class VRTableBuilder
    {
        const string RootName = "BoB_Table";
        const string MatFolder = "Assets/BiteOrBetray/Materials";

        // Ukuran dalam meter.
        public const float TableHeight = 0.74f;
        public const float TableRadius = 0.8f;
        public const float SeatRadius = 1.2f;
        public const int SeatCount = 6;

        [MenuItem("Bite or Betray/Bangun Meja di Scene Ini")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var old = GameObject.Find(RootName);
            if (old != null)
            {
                if (!EditorUtility.DisplayDialog("Bangun Meja", "Meja lama (BoB_Table) akan dihapus dan dibuat ulang. Lanjut?", "Ya", "Batal")) return;
                Undo.DestroyObjectImmediate(old);
            }

            EnsureFolder("Assets/BiteOrBetray");
            EnsureFolder(MatFolder);
            var matFloor = Mat("BoB_Floor", new Color(0.18f, 0.16f, 0.2f));
            var matWood = Mat("BoB_TableWood", new Color(0.45f, 0.27f, 0.15f));
            var matCloth = Mat("BoB_TableCloth", new Color(0.12f, 0.35f, 0.25f));
            var matMetal = Mat("BoB_Dispenser", new Color(0.75f, 0.75f, 0.8f));
            var matButton = Mat("BoB_Button", new Color(0.9f, 0.2f, 0.2f));
            var matChair = Mat("BoB_Chair", new Color(0.3f, 0.2f, 0.12f));
            var matArea = Mat("BoB_ItemArea", new Color(0.85f, 0.8f, 0.6f));
            var matYellow = Mat("BoB_Candy_Yellow", new Color(1f, 0.85f, 0.15f));
            var matBlue = Mat("BoB_Candy_Blue", new Color(0.25f, 0.55f, 1f));
            var matRed = Mat("BoB_Candy_Red", new Color(0.95f, 0.25f, 0.3f));
            var matTest = Mat("BoB_GrabTest", new Color(0.6f, 0.9f, 0.4f));

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Bangun Meja");

            // Lantai 10 x 10 m
            var floor = Prim(PrimitiveType.Plane, "Floor", root.transform, Vector3.zero, Vector3.one, matFloor);
            floor.isStatic = true;

            // Meja: kaki + papan + taplak
            var table = new GameObject("Table");
            table.transform.SetParent(root.transform, false);
            var leg = Prim(PrimitiveType.Cylinder, "Leg", table.transform, new Vector3(0, TableHeight / 2f, 0),
                new Vector3(0.15f, TableHeight / 2f, 0.15f), matWood);
            var top = Prim(PrimitiveType.Cylinder, "Top", table.transform, new Vector3(0, TableHeight - 0.02f, 0),
                new Vector3(TableRadius * 2f, 0.02f, TableRadius * 2f), matWood);
            var cloth = Prim(PrimitiveType.Cylinder, "Cloth", table.transform, new Vector3(0, TableHeight + 0.001f, 0),
                new Vector3(TableRadius * 1.9f, 0.001f, TableRadius * 1.9f), matCloth);
            ReplaceCapsuleWithMesh(leg);
            ReplaceCapsuleWithMesh(top);
            Object.DestroyImmediate(cloth.GetComponent<Collider>());
            leg.isStatic = top.isStatic = cloth.isStatic = true;

            // Dispenser di tengah meja + tombol menghadap kursi 1
            var dispenser = new GameObject("Dispenser");
            dispenser.transform.SetParent(root.transform, false);
            dispenser.transform.localPosition = new Vector3(0, TableHeight, 0);
            Prim(PrimitiveType.Cube, "Body", dispenser.transform, new Vector3(0, 0.15f, 0), new Vector3(0.18f, 0.3f, 0.18f), matMetal);
            Prim(PrimitiveType.Sphere, "Globe", dispenser.transform, new Vector3(0, 0.37f, 0), new Vector3(0.2f, 0.2f, 0.2f), matMetal);
            Prim(PrimitiveType.Cylinder, "Button", dispenser.transform, new Vector3(0, 0.12f, -0.1f), new Vector3(0.05f, 0.01f, 0.05f), matButton)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);
            var spawn = new GameObject("CandySpawnPoint");
            spawn.transform.SetParent(dispenser.transform, false);
            spawn.transform.localPosition = new Vector3(0, 0.05f, -0.15f);

            // Wadah permen aktif di depan dispenser
            var tray = Prim(PrimitiveType.Cube, "ActiveCandyTray", root.transform, new Vector3(0, TableHeight + 0.01f, -0.28f),
                new Vector3(0.2f, 0.02f, 0.2f), matMetal);
            tray.isStatic = true;

            // 6 kursi melingkar, kursi 0 = posisi pemain lokal (menghadap +Z)
            var seats = new GameObject("Seats");
            seats.transform.SetParent(root.transform, false);
            for (int i = 0; i < SeatCount; i++)
            {
                float a = i * Mathf.PI * 2f / SeatCount;
                var dir = new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a));
                var seat = new GameObject($"Seat_{i}");
                seat.transform.SetParent(seats.transform, false);
                seat.transform.localPosition = dir * SeatRadius;
                seat.transform.localRotation = Quaternion.LookRotation(-dir, Vector3.up);
                Prim(PrimitiveType.Cube, "Chair", seat.transform, new Vector3(0, 0.22f, -0.05f), new Vector3(0.45f, 0.44f, 0.45f), matChair).isStatic = true;
                Prim(PrimitiveType.Cube, "Backrest", seat.transform, new Vector3(0, 0.65f, -0.25f), new Vector3(0.45f, 0.45f, 0.05f), matChair).isStatic = true;

                var area = Prim(PrimitiveType.Cube, "ItemArea", seat.transform, Vector3.zero, new Vector3(0.4f, 0.004f, 0.18f), matArea);
                area.transform.position = dir * (TableRadius - 0.18f) + Vector3.up * (TableHeight + 0.003f);
                area.transform.rotation = seat.transform.rotation;
                Object.DestroyImmediate(area.GetComponent<Collider>());

                var head = new GameObject("HeadAnchor");
                head.transform.SetParent(seat.transform, false);
                head.transform.localPosition = new Vector3(0, 1.15f, 0);
            }

            // Benda untuk tes Milestone 3 (belum bisa di-grab sampai diberi komponen grab)
            var tests = new GameObject("GrabTests");
            tests.transform.SetParent(root.transform, false);
            var basePos = new Vector3(-0.2f, TableHeight + 0.05f, -0.55f);
            Candy("TestCandy_Yellow", tests.transform, basePos, matYellow);
            Candy("TestCandy_Blue", tests.transform, basePos + new Vector3(0.08f, 0, 0), matBlue);
            Candy("TestCandy_Red", tests.transform, basePos + new Vector3(0.16f, 0, 0), matRed);
            var cube = Prim(PrimitiveType.Cube, "GrabTestCube", tests.transform, new Vector3(0.25f, TableHeight + 0.05f, -0.55f),
                Vector3.one * 0.08f, matTest);
            var rb = cube.AddComponent<Rigidbody>();
            rb.mass = 0.2f;

            PlaceCameraRig(seats.transform.GetChild(0));
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("[Bite or Betray] Meja dibuat. Simpan scene dengan Ctrl+S, lalu tekan Play.");
        }

        /// <summary>Tinggi mata pemain duduk (meter) di atas lantai.</summary>
        public const float SeatedEyeHeight = 1.15f;

        [MenuItem("Bite or Betray/Atur Camera Rig ke Kursi 1")]
        public static void PlaceCameraRigMenu()
        {
            var seat0 = GameObject.Find(RootName + "/Seats/Seat_0");
            if (seat0 == null)
            {
                EditorUtility.DisplayDialog("Camera Rig", "Meja belum ada. Jalankan Bite or Betray > Bangun Meja di Scene Ini dulu.", "OK");
                return;
            }
            PlaceCameraRig(seat0.transform);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static void PlaceCameraRig(Transform seat0)
        {
            GameObject rig = null;
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name.Contains("Camera Rig")) rig = go;

            if (rig == null)
            {
                Debug.LogWarning("[Bite or Betray] Camera Rig tidak ditemukan. Tambahkan Building Block Camera Rig, lalu jalankan menu ini lagi.");
                return;
            }
            // Game ini dimainkan sambil duduk: pakai Eye Level, lalu taruh rig di tinggi mata pemain duduk.
            // Dengan begitu tinggi badan pemain tidak berpengaruh, dan simulator juga mulai di posisi yang benar.
            Undo.RecordObject(rig.transform, "Posisikan Camera Rig");
            rig.transform.position = seat0.position + Vector3.up * SeatedEyeHeight;
            rig.transform.rotation = seat0.rotation;
            SetEyeLevelTracking(rig);

            // Kamera bawaan scene bentrok dengan kamera VR di dalam Camera Rig.
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go == rig || go.name != "Main Camera" || go.GetComponent<Camera>() == null) continue;
                Undo.DestroyObjectImmediate(go);
                Debug.Log("[Bite or Betray] Main Camera bawaan dihapus (kamera VR ada di dalam Camera Rig).");
            }
        }

        /// <summary>Set OVRManager.trackingOriginType ke EyeLevel lewat SerializedObject (tanpa referensi langsung ke assembly Meta).</summary>
        static void SetEyeLevelTracking(GameObject rig)
        {
            foreach (var comp in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null || comp.GetType().Name != "OVRManager") continue;
                var so = new SerializedObject(comp);
                var prop = so.FindProperty("_trackingOriginType");
                if (prop == null) break;
                int idx = System.Array.FindIndex(prop.enumNames, n => n.Replace(" ", "") == "EyeLevel");
                if (idx >= 0 && prop.enumValueIndex != idx)
                {
                    prop.enumValueIndex = idx;
                    so.ApplyModifiedProperties();
                    Debug.Log("[Bite or Betray] Tracking Origin diubah ke Eye Level (mode duduk).");
                }
                return;
            }
            Debug.LogWarning("[Bite or Betray] OVRManager tidak ditemukan di Camera Rig. Atur Tracking Origin Type ke Eye Level secara manual.");
        }

        static void Candy(string name, Transform parent, Vector3 pos, Material m)
        {
            var c = Prim(PrimitiveType.Sphere, name, parent, pos, new Vector3(0.035f, 0.025f, 0.035f), m);
            var rb = c.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        /// <summary>Collider kapsul bawaan silinder rusak kalau silindernya gepeng; ganti dengan MeshCollider.</summary>
        static void ReplaceCapsuleWithMesh(GameObject go)
        {
            var cap = go.GetComponent<CapsuleCollider>();
            if (cap != null) Object.DestroyImmediate(cap);
            go.AddComponent<MeshCollider>();
        }

        static Material Mat(string name, Color c)
        {
            string path = $"{MatFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
