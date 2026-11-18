using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiteOrBetray.Table3D
{
    /// <summary>
    /// Procedural combined-mesh 3D diegetic tabletop props for the Candy Parlour:
    /// - TabletopBatch: combines ParlourChalkboard + all 6 SugarHeartStrips into ONE MeshRenderer and ONE Mesh
    ///   (1 single draw call total across the entire table, keeping worst-case 6-seat views under Quest 3's 100 draws/eye cap)
    ///   with 0 B per-frame GC allocation on idle frames.
    /// - BrassMenuLectern: 1 combined tilted walnut-and-brass stand at the player's table edge during menus/lobbies/results.
    /// </summary>
    public static class ParlourDiegeticProps
    {
        public const int MaxHeartSlots = 8;

        static readonly Color32 WalnutWood   = new Color32(88, 48, 27, 32);
        static readonly Color32 WalnutDark   = new Color32(52, 28, 16, 24);
        static readonly Color32 CreamVelvet  = new Color32(246, 234, 206, 22);
        static readonly Color32 DefeatVelvet = new Color32(118, 114, 122, 18);
        static readonly Color32 BrassTrim    = new Color32(214, 158, 62, 228);
        static readonly Color32 CupcakeIce   = new Color32(58, 190, 248, 248);
        static readonly Color32 ParalyzeViol = new Color32(166, 96, 238, 240);
        static readonly Color32 DefeatSlate  = new Color32(78, 74, 84, 56);
        static readonly Color32 TurnGold     = new Color32(255, 218, 76, 255);
        static readonly Color32 TurnIdle     = new Color32(98, 76, 52, 64);
        static readonly Color32 RubyHeart    = new Color32(240, 30, 70, 252);
        static readonly Color32 AshHeart     = new Color32(68, 56, 66, 36);
        static readonly Color32 SlateBack    = new Color32(27, 36, 38, 28);

        static Material sharedMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => sharedMaterial = null;

        public static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial != null) return sharedMaterial;
                var shader = Resources.Load<Shader>("BoBDiegeticProp") ?? Shader.Find("BiteOrBetray/DiegeticProp")
                    ?? Shader.Find("Universal Render Pipeline/Lit");
                sharedMaterial = new Material(shader) { name = "BoB Diegetic Parlour Props", enableInstancing = true };
                return sharedMaterial;
            }
        }

        public sealed class TabletopBatch
        {
            public readonly GameObject gameObject;
            public readonly MeshRenderer renderer;
            readonly Transform batchTransform;
            readonly List<Vector3> buildVerts = new List<Vector3>(4096);
            readonly List<Vector3> buildNorms = new List<Vector3>(4096);
            readonly List<Color32> buildCols = new List<Color32>(4096);
            readonly List<int> buildTris = new List<int>(8192);
            readonly List<SugarHeartStrip> strips = new List<SugarHeartStrip>(8);

            Mesh mesh;
            Vector3[] baseVertices;
            Vector3[] workVertices;
            Vector3[] workNormals;
            Color32[] workColors;

            int easelStart, easelCount;
            bool easelVisible = true;
            internal bool dirtyVertices, dirtyColors;

            public TabletopBatch(Transform parent)
            {
                gameObject = new GameObject("ParlourChalkboard");
                batchTransform = gameObject.transform;
                batchTransform.SetParent(parent, false);
                batchTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                gameObject.AddComponent<MeshFilter>();
                renderer = gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = SharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                gameObject.SetActive(false);
            }

            public GameObject AddChalkboardEasel(Vector3 worldPos, Quaternion worldRot)
            {
                var lv = new List<Vector3>(96);
                var ln = new List<Vector3>(96);
                var lc = new List<Color32>(96);
                var lt = new List<int>(180);

                AddBox(lv, ln, lc, lt, new Vector3(0f, 0.004f, 0f), new Vector3(0.31f, 0.008f, 0.075f), WalnutDark);
                AddBox(lv, ln, lc, lt, new Vector3(0f, 0.009f, -0.028f), new Vector3(0.29f, 0.005f, 0.010f), BrassTrim);
                AddBox(lv, ln, lc, lt, new Vector3(0f, 0.012f, 0f), new Vector3(0.33f, 0.007f, 0.165f), WalnutWood);
                AddFrameRim(lv, ln, lc, lt, new Vector3(0f, 0.016f, 0f), 0.334f, 0.169f, 0.008f, 0.004f, BrassTrim);
                AddBox(lv, ln, lc, lt, new Vector3(0f, 0.0155f, 0f), new Vector3(0.314f, 0.003f, 0.148f), SlateBack);

                easelStart = buildVerts.Count;
                easelCount = lv.Count;
                Vector3 localPos = batchTransform.InverseTransformPoint(worldPos);
                Quaternion localRot = Quaternion.Inverse(batchTransform.rotation) * worldRot;

                for (int i = 0; i < lt.Count; i++) buildTris.Add(easelStart + lt[i]);
                for (int i = 0; i < easelCount; i++)
                {
                    buildVerts.Add(localPos + localRot * lv[i]);
                    buildNorms.Add(localRot * ln[i]);
                    buildCols.Add(lc[i]);
                }
                return gameObject;
            }

            public SugarHeartStrip AddHeartStrip(string seatName, Vector3 worldPos, Quaternion worldRot)
            {
                var strip = new SugarHeartStrip(this, buildVerts, buildNorms, buildCols, buildTris, worldPos, worldRot);
                strips.Add(strip);
                return strip;
            }

            public void Build()
            {
                baseVertices = buildVerts.ToArray();
                workVertices = (Vector3[])baseVertices.Clone();
                workNormals = buildNorms.ToArray();
                workColors = buildCols.ToArray();

                easelVisible = false;
                for (int i = 0; i < easelCount; i++) workVertices[easelStart + i] = Vector3.zero;
                for (int s = 0; s < strips.Count; s++) strips[s].CollapseImmediate();

                mesh = new Mesh { name = "ParlourTabletopProps_Mesh" };
                mesh.SetVertices(workVertices);
                mesh.SetNormals(workNormals);
                mesh.SetColors(workColors);
                mesh.SetTriangles(buildTris, 0, true);
                gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;

                buildVerts.Clear();
                buildNorms.Clear();
                buildCols.Clear();
                buildTris.Clear();
            }

            public void SetEaselVisible(bool visible)
            {
                if (easelVisible == visible || workVertices == null) return;
                easelVisible = visible;
                int end = easelStart + easelCount;
                if (visible)
                {
                    for (int i = easelStart; i < end; i++) workVertices[i] = baseVertices[i];
                }
                else
                {
                    for (int i = easelStart; i < end; i++) workVertices[i] = Vector3.zero;
                }
                dirtyVertices = true;
            }

            public void Flush()
            {
                if (mesh == null) return;
                bool anyVisible = easelVisible;
                if (!anyVisible)
                {
                    for (int i = 0; i < strips.Count; i++)
                        if (strips[i].Visible) { anyVisible = true; break; }
                }
                if (gameObject.activeSelf != anyVisible) gameObject.SetActive(anyVisible);
                if (!anyVisible) return;

                if (dirtyVertices)
                {
                    dirtyVertices = false;
                    mesh.SetVertices(workVertices);
                    mesh.SetNormals(workNormals);
                    mesh.RecalculateBounds();
                }
                if (dirtyColors)
                {
                    dirtyColors = false;
                    mesh.SetColors(workColors);
                }
            }

            public void Dispose()
            {
                if (mesh != null) Object.Destroy(mesh);
                mesh = null;
            }

            internal Vector3 ToLocalPos(Vector3 worldPos) => batchTransform.InverseTransformPoint(worldPos);
            internal Quaternion ToLocalRot(Quaternion worldRot) => Quaternion.Inverse(batchTransform.rotation) * worldRot;
            internal Vector3[] WorkVertices => workVertices;
            internal Vector3[] WorkNormals => workNormals;
            internal Color32[] WorkColors => workColors;
        }

        public sealed class SugarHeartStrip
        {
            public GameObject gameObject => batch.gameObject;
            public MeshRenderer renderer => batch.renderer;

            readonly TabletopBatch batch;
            readonly int batchOffset, vertexCount;
            readonly Vector3[] templateVerts;
            readonly Vector3[] templateNorms;
            readonly Vector3[] shapedLocalVerts;
            readonly int velvetStart, velvetCount;
            readonly int rimStart, rimCount;
            readonly int starStart, starCount;
            readonly int[] heartStart = new int[MaxHeartSlots];
            readonly int[] heartCount = new int[MaxHeartSlots];
            readonly Vector3[] heartCenters = new Vector3[MaxHeartSlots];

            Vector3 lastWorldPos;
            Quaternion lastWorldRot;
            Vector3 localPos;
            Quaternion localRot;
            (bool visible, int hp, int maxHp, bool turn, bool cupcake, bool paralyzed, bool eliminated) lastState;
            bool initialized;

            public bool Visible => lastState.visible;
            public int ActiveHearts { get; private set; }
            public int MaxHearts { get; private set; }
            public bool TurnLit { get; private set; }
            public bool CupcakeLit { get; private set; }
            public bool ParalyzedLit { get; private set; }

            internal SugarHeartStrip(TabletopBatch batch,
                List<Vector3> bVerts, List<Vector3> bNorms, List<Color32> bCols, List<int> bTris,
                Vector3 worldPos, Quaternion worldRot)
            {
                this.batch = batch;
                var verts = new List<Vector3>(640);
                var norms = new List<Vector3>(640);
                var cols = new List<Color32>(640);
                var tris = new List<int>(1280);

                // 1. Chunky walnut plinth (30.0 cm wide x 1.1 cm tall x 6.6 cm deep)
                AddBox(verts, norms, cols, tris, new Vector3(0f, 0.0055f, 0f), new Vector3(0.300f, 0.011f, 0.066f), WalnutWood);

                // 2. High-contrast warm cream velvet inlay bed so ruby & dark-socket hearts pop across the table
                velvetStart = verts.Count;
                AddBox(verts, norms, cols, tris, new Vector3(0.025f, 0.0116f, 0f), new Vector3(0.228f, 0.003f, 0.050f), CreamVelvet);
                velvetCount = verts.Count - velvetStart;

                // Dark medallion badge boss on the left so the gold turn star pops crisply
                AddBox(verts, norms, cols, tris, new Vector3(-0.114f, 0.0118f, 0f), new Vector3(0.044f, 0.0035f, 0.050f), WalnutDark);

                // 3. Bold beveled status/brass collar rim around tray perimeter
                rimStart = verts.Count;
                AddFrameRim(verts, norms, cols, tris, new Vector3(0f, 0.0125f, 0f), 0.312f, 0.074f, 0.0085f, 0.0065f, BrassTrim);
                rimCount = verts.Count - rimStart;

                // 4. Left 6-point turn medallion star (4.1 cm diameter)
                starStart = verts.Count;
                AddStarMedallion(verts, norms, cols, tris, new Vector3(-0.114f, 0.0138f, 0f), 0.0205f, 0.0096f, 0.0095f, TurnIdle);
                starCount = verts.Count - starStart;

                // 5. Up to 8 plump 3D beveled candy hearts (3.94 cm wide x 1.5 cm thick)
                for (int i = 0; i < MaxHeartSlots; i++)
                {
                    heartStart[i] = verts.Count;
                    Vector3 hc = new Vector3(i * 0.046f, 0.0132f, 0f);
                    heartCenters[i] = hc;
                    AddPlumpCandyHeart(verts, norms, cols, tris, hc, 0.0205f, 0.0065f, 0.0148f, RubyHeart);
                    heartCount[i] = verts.Count - heartStart[i];
                }

                templateVerts = verts.ToArray();
                templateNorms = norms.ToArray();
                shapedLocalVerts = (Vector3[])templateVerts.Clone();
                vertexCount = templateVerts.Length;
                batchOffset = bVerts.Count;

                lastWorldPos = worldPos;
                lastWorldRot = worldRot;
                localPos = batch.ToLocalPos(worldPos);
                localRot = batch.ToLocalRot(worldRot);

                for (int i = 0; i < tris.Count; i++) bTris.Add(batchOffset + tris[i]);
                for (int i = 0; i < vertexCount; i++)
                {
                    bVerts.Add(localPos + localRot * templateVerts[i]);
                    bNorms.Add(localRot * templateNorms[i]);
                    bCols.Add(cols[i]);
                }
            }

            internal void CollapseImmediate()
            {
                var wv = batch.WorkVertices;
                int end = batchOffset + vertexCount;
                for (int i = batchOffset; i < end; i++) wv[i] = Vector3.zero;
            }

            public void SetTransform(Vector3 worldPos, Quaternion worldRot)
            {
                if ((worldPos - lastWorldPos).sqrMagnitude < 1e-7f && Quaternion.Angle(worldRot, lastWorldRot) < 0.05f) return;
                lastWorldPos = worldPos;
                lastWorldRot = worldRot;
                localPos = batch.ToLocalPos(worldPos);
                localRot = batch.ToLocalRot(worldRot);
                if (lastState.visible && batch.WorkVertices != null)
                {
                    WriteWorldSlice(lastState.maxHp);
                    batch.dirtyVertices = true;
                }
            }

            public void UpdateState(bool visible, int hp, int maxHp, bool turn, bool cupcake, bool paralyzed, bool eliminated)
            {
                maxHp = Mathf.Clamp(maxHp, 0, MaxHeartSlots);
                hp = Mathf.Clamp(hp, 0, maxHp);
                var next = (visible, hp, maxHp, turn, cupcake, paralyzed, eliminated);
                if (initialized && lastState == next) return;

                bool visChanged = !initialized || lastState.visible != visible;
                bool maxChanged = !initialized || lastState.maxHp != maxHp;
                initialized = true;
                lastState = next;

                ActiveHearts = visible && !eliminated ? hp : 0;
                MaxHearts = visible ? maxHp : 0;
                TurnLit = visible && !eliminated && turn;
                CupcakeLit = visible && !eliminated && cupcake;
                ParalyzedLit = visible && !eliminated && paralyzed;

                var wv = batch.WorkVertices;
                var wc = batch.WorkColors;
                if (wv == null || wc == null) return;

                if (!visible)
                {
                    if (visChanged)
                    {
                        CollapseImmediate();
                        batch.dirtyVertices = true;
                    }
                    return;
                }

                if (visChanged || maxChanged)
                {
                    ShapeHeartSlots(maxHp);
                    WriteWorldSlice(maxHp);
                    batch.dirtyVertices = true;
                }

                Color32 rimColor = eliminated ? DefeatSlate : cupcake ? CupcakeIce : paralyzed ? ParalyzeViol : BrassTrim;
                FillRange(wc, batchOffset + velvetStart, velvetCount, eliminated ? DefeatVelvet : CreamVelvet);
                FillRange(wc, batchOffset + rimStart, rimCount, rimColor);
                FillRange(wc, batchOffset + starStart, starCount, TurnLit ? TurnGold : TurnIdle);

                for (int i = 0; i < maxHp; i++)
                {
                    bool alive = !eliminated && i < hp;
                    Color32 hc = alive ? (cupcake && i == hp - 1 ? CupcakeIce : RubyHeart) : AshHeart;
                    FillRange(wc, batchOffset + heartStart[i], heartCount[i], hc);
                }
                batch.dirtyColors = true;
            }

            void ShapeHeartSlots(int maxHp)
            {
                // Center hearts cleanly across the 22.8 cm cream inlay bed (x in [-0.089 .. +0.139], center = +0.025)
                float scale = maxHp <= 5 ? 1f : 5f / maxHp;
                float step = maxHp <= 4 ? 0.050f : maxHp == 5 ? 0.044f : 0.184f / Mathf.Max(1, maxHp - 1);
                float span = Mathf.Max(0, maxHp - 1) * step;
                float startX = 0.025f - span * 0.5f;
                for (int i = 0; i < MaxHeartSlots; i++)
                {
                    int s = heartStart[i], c = heartCount[i];
                    Vector3 srcCenter = heartCenters[i];
                    if (i < maxHp)
                    {
                        Vector3 dstCenter = new Vector3(startX + i * step, srcCenter.y, srcCenter.z);
                        for (int k = 0; k < c; k++)
                        {
                            Vector3 rel = templateVerts[s + k] - srcCenter;
                            shapedLocalVerts[s + k] = dstCenter + new Vector3(rel.x * scale, rel.y, rel.z * scale);
                        }
                    }
                }
            }

            void WriteWorldSlice(int maxHp)
            {
                var wv = batch.WorkVertices;
                var wn = batch.WorkNormals;
                int activeEnd = maxHp < MaxHeartSlots ? heartStart[maxHp] : vertexCount;
                for (int i = 0; i < activeEnd; i++)
                {
                    wv[batchOffset + i] = localPos + localRot * shapedLocalVerts[i];
                    wn[batchOffset + i] = localRot * templateNorms[i];
                }
                for (int i = activeEnd; i < vertexCount; i++)
                    wv[batchOffset + i] = Vector3.zero;
            }

            static void FillRange(Color32[] arr, int start, int count, Color32 value)
            {
                int end = start + count;
                for (int i = start; i < end; i++) arr[i] = value;
            }
        }

        public static GameObject CreateMenuLectern(Transform parent, Vector3 position, Quaternion rotation)
        {
            var verts = new List<Vector3>(112);
            var norms = new List<Vector3>(112);
            var cols = new List<Color32>(112);
            var tris = new List<int>(210);

            AddBox(verts, norms, cols, tris, new Vector3(0f, 0.005f, 0f), new Vector3(0.38f, 0.010f, 0.24f), WalnutWood);
            AddFrameRim(verts, norms, cols, tris, new Vector3(0f, 0.011f, 0f), 0.392f, 0.252f, 0.010f, 0.005f, BrassTrim);
            AddBox(verts, norms, cols, tris, new Vector3(0f, 0.015f, -0.118f), new Vector3(0.35f, 0.009f, 0.012f), BrassTrim);
            AddBox(verts, norms, cols, tris, new Vector3(0f, 0.0105f, 0f), new Vector3(0.012f, 0.003f, 0.232f), WalnutDark);

            var mesh = new Mesh { name = "BrassMenuLectern_Mesh" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0, true);

            var go = new GameObject("BrassMenuLectern");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SharedMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        // ------------------------------------------------------------------ Low-Poly Faceted Geometry Primitives

        static void AddQuad(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color32 col)
        {
            int i0 = v.Count;
            Vector3 norm = Vector3.Cross(b - a, d - a).normalized;
            v.Add(a); v.Add(b); v.Add(d); v.Add(e);
            n.Add(norm); n.Add(norm); n.Add(norm); n.Add(norm);
            c.Add(col); c.Add(col); c.Add(col); c.Add(col);
            t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
            t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3);
        }

        static void AddTri(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Vector3 a, Vector3 b, Vector3 d, Color32 col)
        {
            int i0 = v.Count;
            Vector3 norm = Vector3.Cross(b - a, d - a).normalized;
            v.Add(a); v.Add(b); v.Add(d);
            n.Add(norm); n.Add(norm); n.Add(norm);
            c.Add(col); c.Add(col); c.Add(col);
            t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
        }

        static void AddBox(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Vector3 center, Vector3 size, Color32 col)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p100 = center + new Vector3( h.x, -h.y, -h.z);
            Vector3 p101 = center + new Vector3( h.x, -h.y,  h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y,  h.z);
            Vector3 p010 = center + new Vector3(-h.x,  h.y, -h.z);
            Vector3 p110 = center + new Vector3( h.x,  h.y, -h.z);
            Vector3 p111 = center + new Vector3( h.x,  h.y,  h.z);
            Vector3 p011 = center + new Vector3(-h.x,  h.y,  h.z);

            AddQuad(v, n, c, t, p010, p011, p111, p110, col);
            AddQuad(v, n, c, t, p000, p010, p110, p100, col);
            AddQuad(v, n, c, t, p100, p110, p111, p101, col);
            AddQuad(v, n, c, t, p101, p111, p011, p001, col);
            AddQuad(v, n, c, t, p001, p011, p010, p000, col);
        }

        static void AddFrameRim(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Vector3 center, float width, float depth, float barThickness, float height, Color32 col)
        {
            float hx = width * 0.5f, hz = depth * 0.5f, b = barThickness;
            AddBox(v, n, c, t, center + new Vector3(0f, 0f, hz - b * 0.5f), new Vector3(width, height, b), col);
            AddBox(v, n, c, t, center + new Vector3(0f, 0f, -hz + b * 0.5f), new Vector3(width, height, b), col);
            AddBox(v, n, c, t, center + new Vector3(-hx + b * 0.5f, 0f, 0f), new Vector3(b, height, depth - b * 2f), col);
            AddBox(v, n, c, t, center + new Vector3( hx - b * 0.5f, 0f, 0f), new Vector3(b, height, depth - b * 2f), col);
        }

        static void AddStarMedallion(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Vector3 center, float outerR, float innerR, float apexH, Color32 col)
        {
            const int pts = 12;
            Vector3 apex = center + Vector3.up * apexH;
            for (int i = 0; i < pts; i++)
            {
                float a0 = i * Mathf.PI * 2f / pts;
                float a1 = (i + 1) * Mathf.PI * 2f / pts;
                float r0 = (i % 2 == 0) ? outerR : innerR;
                float r1 = ((i + 1) % 2 == 0) ? outerR : innerR;
                Vector3 p0 = center + new Vector3(Mathf.Sin(a0) * r0, 0f, Mathf.Cos(a0) * r0);
                Vector3 p1 = center + new Vector3(Mathf.Sin(a1) * r1, 0f, Mathf.Cos(a1) * r1);
                AddTri(v, n, c, t, apex, p0, p1, col);
            }
        }

        static readonly Vector2[] HeartContour =
        {
            new Vector2( 0.00f,  0.52f),
            new Vector2( 0.48f,  0.96f),
            new Vector2( 0.96f,  0.62f),
            new Vector2( 0.88f,  0.08f),
            new Vector2( 0.48f, -0.46f),
            new Vector2( 0.00f, -1.00f),
            new Vector2(-0.48f, -0.46f),
            new Vector2(-0.88f,  0.08f),
            new Vector2(-0.96f,  0.62f),
            new Vector2(-0.48f,  0.96f),
        };

        static void AddPlumpCandyHeart(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
            Vector3 center, float radius, float wallHeight, float crownHeight, Color32 col)
        {
            Vector3 apex = center + new Vector3(0f, crownHeight, 0.02f * radius);
            int count = HeartContour.Length;
            for (int i = 0; i < count; i++)
            {
                Vector2 q0 = HeartContour[i] * radius;
                Vector2 q1 = HeartContour[(i + 1) % count] * radius;
                Vector3 b0 = center + new Vector3(q0.x, 0f, q0.y);
                Vector3 b1 = center + new Vector3(q1.x, 0f, q1.y);
                Vector3 s0 = center + new Vector3(q0.x * 0.88f, wallHeight, q0.y * 0.88f);
                Vector3 s1 = center + new Vector3(q1.x * 0.88f, wallHeight, q1.y * 0.88f);
                AddQuad(v, n, c, t, b0, b1, s1, s0, col);
                AddTri(v, n, c, t, apex, s0, s1, col);
            }
        }
    }
}
