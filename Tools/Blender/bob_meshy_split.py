"""Model Meshy (GLB bertekstur) -> mesh game low-poly, dipisah jadi Head / Hand_Left / Hand_Right / Body.

Kepala + dua tangan memakai satu atlas tekstur (untuk gaya floating head), badan atlas sendiri. Keempat
bagian tetap menempel kalau dipasang di posisi asal, jadi FBX yang sama bisa dipakai full-body.

    blender -b -P Tools/Blender/bob_meshy_split.py -- --src model.glb --name Cherry [--preview DIR] [--blend X.blend]

Juga membuat Models/<Name>.fbx: seluruh badan (satu mesh, satu atlas) dengan rig bot full-body (Hips, Spine,
Head, Arm_*/Forearm_*/Hand_*, plus Thigh_*/Shin_*) yang rest pose-nya sudah duduk di kursi dengan tangan di meja,
jadi CharacterAnimator menganimasikannya seperti Bot_* lain. --float menambah Models/<Name>_Float.fbx (kepala +
tangan saja di rig floating bot).

Langkah: weld -> potong leher (bidang + flood-fill warna rambut) dan pergelangan (penampang lengan terkecil)
-> tutup lubang potongan -> decimate per bagian -> UV baru (kepala lebih rapat) -> bake warna/normal/kekasaran
dari mesh asli -> export FBX + PNG ke Assets/BiteOrBetray/Art3D/Meshy/<Name>/.
"""
import argparse
import colorsys
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Koordinat dalam satuan GLB asli (Z atas, menghadap -Y). Nilai per model, cek dengan --preview.
CONFIG = {
    "Cherry": dict(
        height=1.60,           # tinggi akhir (m), kaki di z=0
        neck_z=.545,           # bidang potong leher, di bawah dagu dan di atas kerah
        shoulder_z=.40,        # rambut di bawah leher ikut kepala sampai batas ini
        arm_x=.45,             # |x| di luar badan = lengan
        hair=dict(hue=(.035, .10), sat=.5, val=.45),
        tris=dict(Head=10000, Hand=1200, Body=9000),
        head_texel=1.7,        # kerapatan tekstur kepala relatif terhadap tangan di atlas yang sama
        # Versi bot meja: rig floating Bot_Peach (bob_floating.peach) supaya tangan tetap bertumpu di meja.
        float=dict(head=(1.16, (.155, .147, .158), -.045), rig=((.125, -.03, .935), (.172, -.07, .745), (.085, -.37, .79), .98),
                   head_scale=.92, hand_scale=.85),
        # Full-body duduk (bot meja). Titik sendi dalam koordinat GLB; kursi/meja dalam meter (ruang kursi).
        full=dict(tris=22000, texel=2048,
                  pelvis=(0, -.02, -.10), chest=(0, -.03, .42), hip=(.12, -.02, -.12), knee=(.135, -.03, -.50),
                  ankle=(.165, -.02, -.78), shoulder=(.27, -.03, .39), elbow_z=.20,
                  seat_z=.53, back_y=.12, lean=4, head_tilt=12, thigh_splay=.06, wrist=(.18, -.43, .80),
                  skirt_z=.10, lap=.10),   # lap: tebal kain maksimum di atas sumbu paha saat duduk       # di bawah ini hanya |x| > arm_x yang boleh ikut lengan (rok di samping tangan A-pose)
    ),
}
PARTS = ("Head", "Hand_Left", "Hand_Right", "Body")


def log(*a):
    print("[meshy]", *a, flush=True)


# ---------------------------------------------------------------------------------------------------
# Muat dan siapkan mesh asli
# ---------------------------------------------------------------------------------------------------

def load(src):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    obj = next(o for o in bpy.data.objects if o.type == "MESH")
    obj.name = "High"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if obj.data.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    n = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    log(f"weld {n} -> {len(bm.verts)} verts, {len(bm.faces)} tris")
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def textures(obj):
    """Gambar basecolor / metal-rough dari material glTF."""
    nodes = obj.data.materials[0].node_tree.nodes
    base = next(n.image for n in nodes if n.type == "TEX_IMAGE" and any(l.to_socket.name == "Base Color" for l in n.outputs[0].links))
    return base


def vertex_colors(obj, image):
    """Warna basecolor rata-rata per vertex (linear) + HSV (sRGB)."""
    me = obj.data
    w, h = image.size
    px = np.empty(w * h * 4, np.float32)
    image.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)[:, :, :3]
    uv = np.empty(len(me.loops) * 2, np.float32)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    lv = np.empty(len(me.loops), np.int64)
    me.loops.foreach_get("vertex_index", lv)
    col = px[np.clip((uv[:, 1] * h).astype(int), 0, h - 1), np.clip((uv[:, 0] * w).astype(int), 0, w - 1)]
    vc = np.zeros((len(me.vertices), 3))
    cnt = np.zeros(len(me.vertices))
    np.add.at(vc, lv, col)
    np.add.at(cnt, lv, 1)
    vc /= np.maximum(cnt, 1)[:, None]
    srgb = np.where(vc <= .0031308, vc * 12.92, 1.055 * np.power(np.clip(vc, 0, 1), 1 / 2.4) - .055)
    hsv = np.array([colorsys.rgb_to_hsv(*c) for c in srgb])
    return vc, hsv


# ---------------------------------------------------------------------------------------------------
# Potong jadi 4 bagian
# ---------------------------------------------------------------------------------------------------

def wrist_plane(co, hsv, side, cfg):
    """Pergelangan = penampang tersempit di kulit antara ujung lengan baju dan telapak tangan."""
    p_mask = (side * co[:, 0] > cfg["arm_x"]) & (co[:, 2] < cfg["shoulder_z"] - .1)
    p = co[p_mask]
    c = p.mean(0)
    axis = np.linalg.svd(p - c, full_matrices=False)[2][0]
    if axis[2] > 0:
        axis = -axis                        # mengarah ke ujung jari
    t = (p - c) @ axis
    r = np.linalg.norm((p - c) - np.outer(t, axis), axis=1)
    hh = hsv[p_mask]
    skin = (hh[:, 1] > .15) & (hh[:, 1] < .6) & (hh[:, 0] < .12) & (hh[:, 2] > .5)
    bins = np.linspace(t.min(), t.max(), 41)
    step = bins[1] - bins[0]
    prof = []
    for b in bins:
        m = np.abs(t - b) < step
        if m.sum() > 8:
            prof.append((b, np.percentile(r[m], 80), skin[m].mean()))
    # mulai dari bin kulit pertama setelah lengan baju, cari radius minimum di 45% bagian kulit pertama
    skin_bins = [x for x in prof if x[2] > .9]
    t0, t1 = skin_bins[0][0], skin_bins[-1][0]
    cand = [x for x in skin_bins if x[0] <= t0 + (t1 - t0) * .45]
    tw = min(cand, key=lambda x: x[1])[0]
    point = c + axis * tw
    log(f"wrist side {side:+d}: point {np.round(point, 3)} axis {np.round(axis, 3)}")
    return Vector(point), Vector(axis)


def split(obj, cfg):
    image = textures(obj)
    vc, hsv = vertex_colors(obj, image)
    me = obj.data
    co = np.array([v.co[:] for v in me.vertices])
    wrists = {s: wrist_plane(co, hsv, s, cfg) for s in (-1, 1)}

    bm = bmesh.new()
    bm.from_mesh(me)
    col = bm.verts.layers.float_color.new("CapCol")
    hair_l = bm.verts.layers.int.new("Hair")
    hc = cfg["hair"]
    for v in bm.verts:
        c = vc[v.index]
        v[col] = (*c, 1)
        hh = hsv[v.index]
        v[hair_l] = int(hc["hue"][0] < hh[0] < hc["hue"][1] and hh[1] > hc["sat"] and hh[2] > hc["val"])

    # Bisect menambah vertex baru; warnanya diambil dari tetangga terdekat setelahnya.
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6,
                           plane_co=(0, 0, cfg["neck_z"]), plane_no=(0, 0, 1))
    for s, (wp, wa) in wrists.items():
        near = [f for f in bm.faces if s * f.calc_center_median().x > cfg["arm_x"] and (f.calc_center_median() - wp).length < .2]
        geom = list({e for f in near for e in f.edges}) + near + list({v for f in near for v in f.verts})
        bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=wp, plane_no=wa)
    for v in bm.verts:
        if v[col][3] == 0:
            nb = [e.other_vert(v) for e in v.link_edges if e.other_vert(v)[col][3] > 0]
            if nb:
                v[col] = nb[0][col]
                v[hair_l] = nb[0][hair_l]

    part = bm.faces.layers.int.new("Part")
    head, hand_l, hand_r, body = range(4)
    for f in bm.faces:
        c = f.calc_center_median()
        label = body
        for s, (wp, wa) in wrists.items():
            if s * c.x > cfg["arm_x"] and (c - wp).dot(wa) > 0 and c.z < cfg["shoulder_z"]:
                label = hand_l if s < 0 else hand_r
        if label == body and c.z > cfg["neck_z"]:
            label = head
        f[part] = label

    def is_hair(f):
        return sum(v[hair_l] for v in f.verts) >= 2 and f.calc_center_median().z > cfg["shoulder_z"]

    # Rambut yang menjuntai di bawah leher ikut kepala (flood-fill lewat wajah berwarna rambut).
    stack = [f for f in bm.faces if f[part] == head]
    grown = 0
    while stack:
        f = stack.pop()
        for e in f.edges:
            for g in e.link_faces:
                if g[part] == body and is_hair(g):
                    g[part] = head
                    stack.append(g)
                    grown += 1
    # Pulau lepas (ikal rambut, kancing): ikut label mayoritas, ikal rambut di atas bahu masuk kepala.
    seen = set()
    for f in bm.faces:
        if f.index in seen:
            continue
        comp, st = [], [f]
        seen.add(f.index)
        while st:
            g = st.pop()
            comp.append(g)
            for e in g.edges:
                for h2 in e.link_faces:
                    if h2.index not in seen:
                        seen.add(h2.index)
                        st.append(h2)
        if len(comp) > len(bm.faces) // 2:
            continue
        if np.mean([is_hair(g) for g in comp]) > .5:
            for g in comp:
                g[part] = head
        else:
            labels = np.bincount([g[part] for g in comp], minlength=4)
            for g in comp:
                g[part] = int(labels.argmax())
    log(f"hair flood +{grown} faces")

    cut = bm.edges.layers.int.new("Cut")
    for e in bm.edges:
        e[cut] = int(len({f[part] for f in e.link_faces}) > 1)
    bm.to_mesh(me)
    bm.free()

    pieces = {}
    for i, name in enumerate(PARTS):
        o = obj.copy()
        o.data = obj.data.copy()
        o.name = name + "_High"
        bpy.context.scene.collection.objects.link(o)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        pl = bm.faces.layers.int["Part"]
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f[pl] != i], context="FACES")
        cap(bm)
        bm.to_mesh(o.data)
        bm.free()
        pieces[name] = o
        log(f"{name}: {len(o.data.polygons)} tris")
    obj.name = "Whole_High"           # mesh utuh (dengan atribut Part) untuk versi full-body
    return pieces, image, wrists, obj


def cap(bm):
    """Tutup lubang bekas potongan dengan kipas segitiga ke titik tengah (material slot 1)."""
    cut = bm.edges.layers.int["Cut"]
    col = bm.verts.layers.float_color["CapCol"]
    edges = {e for e in bm.edges if e.is_boundary and e[cut]}
    while edges:
        e0 = edges.pop()
        chain = [e0]
        # kumpulkan rantai tepi yang tersambung
        frontier = list(e0.verts)
        while frontier:
            v = frontier.pop()
            for e in v.link_edges:
                if e in edges:
                    edges.discard(e)
                    chain.append(e)
                    frontier.extend(e.verts)
        if len(chain) < 3:
            continue
        verts = list({v for e in chain for v in e.verts})
        center = sum((v.co for v in verts), Vector()) / len(verts)
        cv = bm.verts.new(center)
        cv[col] = tuple(np.median([v[col][:] for v in verts], axis=0))
        for e in chain:
            # urutan vertex mengikuti arah loop wajah tetangga supaya normal tutup menghadap keluar
            loop = e.link_loops[0]
            a, b = loop.vert, loop.link_loop_next.vert
            try:
                f = bm.faces.new((b, a, cv))
                f.material_index = 1
            except ValueError:
                pass


# ---------------------------------------------------------------------------------------------------
# Low-poly, UV, bake
# ---------------------------------------------------------------------------------------------------

def normalize(objs, height):
    pts = np.array([o.matrix_world @ v.co for o in objs for v in o.data.vertices])
    lo, hi = pts.min(0), pts.max(0)
    s = height / (hi[2] - lo[2])
    off = Vector((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))
    for o in objs:
        for v in o.data.vertices:
            v.co = (v.co + off) * s
    return s, off


def high_materials(pieces, base):
    """Material bake: slot 0 = tekstur Meshy asli, slot 1 = warna tutup dari atribut CapCol."""
    src = next(iter(pieces.values())).data.materials[0]
    cap_m = bpy.data.materials.new("Cap")
    nt = cap_m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    a = nt.nodes.new("ShaderNodeAttribute")
    a.attribute_name = "CapCol"
    nt.links.new(a.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = .85
    for o in pieces.values():
        o.data.materials.clear()
        o.data.materials.append(src)
        o.data.materials.append(cap_m)


def make_low(high, target):
    o = high.copy()
    o.data = high.data.copy()
    o.name = high.name.replace("_High", "")
    bpy.context.scene.collection.objects.link(o)
    me = o.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    for name in ("CapCol", "Hair", "Part", "Cut"):
        if name in me.attributes:
            me.attributes.remove(me.attributes[name])
    me.materials.clear()
    select(o)
    mod = o.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = min(1.0, target / len(me.polygons))
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    me.shade_smooth()
    log(f"{o.name}: {len(high.data.polygons)} -> {len(me.polygons)} tris")
    return o


def select(*objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def unwrap(objs, scales):
    """Smart UV per atlas; pulau objek pertama (kepala) diperbesar sebelum packing."""
    select(*objs)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.002, area_weight=0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for o, s in zip(objs, scales):
        uv = o.data.uv_layers.active.data
        a = np.empty(len(uv) * 2, np.float32)
        uv.foreach_get("uv", a)
        uv.foreach_set("uv", a * s)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=.003)
    bpy.ops.object.mode_set(mode="OBJECT")


def bake_atlas(name, lows, highs, sizes, out_dir):
    """Bake semua low di satu atlas: low digabung sementara supaya margin tidak saling menimpa."""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = "CUDA"
        prefs.get_devices()
        for d in prefs.devices:
            d.use = d.type == "CUDA"
        scene.cycles.device = "GPU"
    except Exception:
        scene.cycles.device = "CPU"

    mat = bpy.data.materials.new(name)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    images = {}
    for kind, size, cs in (("BaseColor", sizes[0], "sRGB"), ("Normal", sizes[1], "Non-Color"), ("Roughness", sizes[2], "Non-Color")):
        img = bpy.data.images.new(f"{name}_{kind}", size, size, alpha=False)
        img.colorspace_settings.name = cs
        images[kind] = img
    target = nodes.new("ShaderNodeTexImage")
    nodes.active = target
    for o in lows:
        o.data.materials.clear()
        o.data.materials.append(mat)

    # gabung sementara: low jadi satu objek aktif, high jadi satu objek sumber
    tmp_low = [o.copy() for o in lows]
    tmp_high = [o.copy() for o in highs]
    for o in tmp_low + tmp_high:
        o.data = o.data.copy()
        scene.collection.objects.link(o)
    select(*tmp_low)
    bpy.ops.object.join()
    low = bpy.context.view_layer.objects.active
    select(*tmp_high)
    bpy.ops.object.join()
    high = bpy.context.view_layer.objects.active

    bake = scene.render.bake
    bake.use_selected_to_active = True
    bake.cage_extrusion = .02
    bake.max_ray_distance = .06
    bake.margin = 8
    bake.use_clear = True
    select(low, high)
    bpy.context.view_layer.objects.active = low
    for kind, pass_type in (("BaseColor", "DIFFUSE"), ("Normal", "NORMAL"), ("Roughness", "ROUGHNESS")):
        target.image = images[kind]
        if pass_type == "DIFFUSE":
            bake.use_pass_direct = bake.use_pass_indirect = False
            bake.use_pass_color = True
        log(f"bake {name} {kind}")
        bpy.ops.object.bake(type=pass_type)
    for o in (low, high):
        bpy.data.objects.remove(o)
    nodes.remove(target)

    # URP Lit: metallic di R, smoothness di A
    rough = images["Roughness"]
    px = np.empty(rough.size[0] * rough.size[1] * 4, np.float32)
    rough.pixels.foreach_get(px)
    px = px.reshape(-1, 4)
    ms = bpy.data.images.new(f"{name}_MetallicSmoothness", *rough.size, alpha=True)
    ms.colorspace_settings.name = "Non-Color"
    out = np.zeros_like(px)
    out[:, 3] = 1 - px[:, 0]
    ms.pixels.foreach_set(out.ravel())
    images["MetallicSmoothness"] = ms

    for kind, img in images.items():
        if kind == "Roughness":
            continue
        img.filepath_raw = os.path.join(out_dir, img.name + ".png")
        img.file_format = "PNG"
        img.save()
        log(f"   {img.filepath_raw} {img.size[0]}x{img.size[1]}")

    bsdf = nodes["Principled BSDF"]

    def tex(img):
        n = nodes.new("ShaderNodeTexImage")
        n.image = img
        return n
    links.new(tex(images["BaseColor"]).outputs["Color"], bsdf.inputs["Base Color"])
    links.new(tex(rough).outputs["Color"], bsdf.inputs["Roughness"])
    nmap = nodes.new("ShaderNodeNormalMap")
    links.new(tex(images["Normal"]).outputs["Color"], nmap.inputs["Color"])
    links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Metallic"].default_value = 0
    return mat


def set_origin(o, point):
    off = Vector(point) - o.location
    for v in o.data.vertices:
        v.co -= off
    o.location += off


def origins(lows, highs):
    """Pivot kepala di tengah potongan leher, tangan di tengah pergelangan, badan tetap di kaki."""
    for name, o in lows.items():
        if name == "Body":
            continue
        me = highs[name].data
        flags = np.zeros(len(me.edges), np.int32)
        me.attributes["Cut"].data.foreach_get("value", flags)
        idx = {i for e, f in zip(me.edges, flags) if f for i in e.vertices}
        set_origin(o, np.array([me.vertices[i].co[:] for i in idx]).mean(0))


def baked_copy(src, name):
    """Salinan objek dengan transform dunia sudah masuk ke vertex (objek di identitas)."""
    o = src.copy()
    o.data = src.data.copy()
    o.name = name
    bpy.context.scene.collection.objects.link(o)
    o.data.transform(src.matrix_world)
    o.matrix_world = Matrix.Identity(4)
    return o


def frame(f, n):
    f = Vector(f).normalized()
    n = (Vector(n) - f * f.dot(Vector(n))).normalized()
    return Matrix((f, n, f.cross(n))).transposed()


def build_float(lows, name, cfg):
    """Kepala + tangan Meshy di rig floating bot, export ke Models/<Name>_Float.fbx."""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import bob_characters as art
    import bob_floating as fl
    f = cfg["float"]
    c = art.Character(name + "_Float", fl.SKIN)
    c.head = f["head"]
    fl.rig(c, *f["rig"])
    c.arm = art.build_armature(c.name, c.bones)
    cz, r, y = c.head

    # Kepala kaku di tulang Head; pivot leher Meshy diletakkan di dagu kepala chibi.
    head = baked_copy(lows["Head"], "HeadSkin")
    pivot = lows["Head"].matrix_world.translation
    k = f["head_scale"]
    head.data.transform(Matrix.Translation((0, y, cz - r[2] * .9)) @ Matrix.Scale(k, 4) @ Matrix.Translation(-pivot))
    head.parent = c.arm
    head.parent_type = "BONE"
    head.parent_bone = "Head"
    bpy.context.view_layer.update()
    head.matrix_world = Matrix.Identity(4)

    # Tangan: jari searah lengan bawah rig, telapak menghadap meja, di-skin penuh ke Hand_* seperti Glove_*.
    for s, side, short in art.SIDES:
        src = lows["Hand_" + side]
        hand = baked_copy(src, "Glove_" + short)
        wrist = src.matrix_world.translation
        pts = np.array([v.co[:] for v in hand.data.vertices]) - np.array(wrist)
        axis = np.linalg.svd(pts, full_matrices=False)[2][0]
        if axis @ pts.mean(0) < 0:
            axis = -axis
        wr, el = Vector(c.bone_pos("Hand_" + side)), Vector(c.bone_pos("Forearm_" + side))
        rot = frame(wr - el, (0, 0, -1)) @ frame(axis, (-s, 0, 0)).inverted()
        hand.data.transform(Matrix.Translation(wr) @ rot.to_4x4() @ Matrix.Scale(f["hand_scale"], 4) @ Matrix.Translation(-wrist))
        hand.parent = c.arm
        hand.matrix_world = Matrix.Identity(4)
        group = hand.vertex_groups.new(name="Hand_" + side)
        group.add(range(len(hand.data.vertices)), 1.0, "REPLACE")
        hand.modifiers.new("Armature", "ARMATURE").object = c.arm

    # Sama dengan bob_floating.finish: tulang Hips dibuang, Spine jadi akar.
    bpy.context.view_layer.objects.active = c.arm
    bpy.ops.object.mode_set(mode="EDIT")
    c.arm.data.edit_bones["Spine"].parent = None
    c.arm.data.edit_bones.remove(c.arm.data.edit_bones["Hips"])
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in c.arm.children:
        bb = np.array([o.matrix_world @ Vector(b) for b in o.bound_box])
        log(f"float {o.name}: {len(o.data.polygons)} tris, min {np.round(bb.min(0), 3)} max {np.round(bb.max(0), 3)}")
    art.export(c)
    return c


# ---------------------------------------------------------------------------------------------------
# Full-body duduk
# ---------------------------------------------------------------------------------------------------

def seg_dist(p, a, b):
    ab = b - a
    t = np.clip(((p - a) @ ab) / (ab @ ab), 0, 1)
    return np.linalg.norm(p - (a + np.outer(t, ab)), axis=1), t


def smooth01(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def full_weights(co, part, edges, J, neck, arm_x, skirt_z):
    """Bobot kulit: kepala/tangan kaku dari label potongan, badan dari jarak ke ruas tulang + penghalusan."""
    names = list(J)
    idx = {n: i for i, n in enumerate(names)}
    W = np.zeros((len(co), len(names)))
    body = part == 3
    p = co[body]
    D = np.full((len(p), len(names)), 9.0)
    for n, (a, b) in J.items():
        if n == "Head" or n.startswith("Hand"):
            continue
        d, _ = seg_dist(p, a, b)
        # Ruas paha/lengan tidak menarik daging di atas pangkalnya (pantat, ketiak).
        if n.startswith(("Thigh", "Arm")):
            along = ((p - a) @ (b - a)) / np.linalg.norm(b - a) ** 2
            d = d + (1 - smooth01(-.08, .12, along)) * .25
        if n.startswith(("Arm", "Forearm")):
            d = d + ((np.abs(p[:, 0]) < arm_x) & (p[:, 2] < skirt_z)) * .5
        D[:, idx[n]] = d
    # Panggul: ruas horizontal di antara kedua sendi paha juga milik Hips.
    hd, _ = seg_dist(p, J["Thigh_Left"][0], J["Thigh_Right"][0])
    D[:, idx["Hips"]] = np.minimum(D[:, idx["Hips"]], hd + .02)
    W[body] = np.exp(-(D - D.min(1, keepdims=True)) / .03)
    W[part == 0, idx["Head"]] = 1
    W[part == 1, idx["Hand_Left"]] = 1
    W[part == 2, idx["Hand_Right"]] = 1
    # Leher: kulit badan tepat di bawah potongan ikut sedikit ke kepala.
    near = body & (np.linalg.norm((co - neck)[:, :2], axis=1) < .12) & (co[:, 2] > neck[2] - .06)
    k = smooth01(neck[2] - .06, neck[2], co[near, 2])
    W[near] *= (1 - k)[:, None]
    W[near, idx["Head"]] += k
    W /= W.sum(1, keepdims=True)
    # Penghalusan Laplacian (badan saja; kepala dan tangan tetap kaku).
    free = body & ~near
    cnt = np.zeros(len(co))
    np.add.at(cnt, edges[:, 0], 1)
    np.add.at(cnt, edges[:, 1], 1)
    for _ in range(6):
        acc = np.zeros_like(W)
        np.add.at(acc, edges[:, 0], W[edges[:, 1]])
        np.add.at(acc, edges[:, 1], W[edges[:, 0]])
        avg = acc / np.maximum(cnt, 1)[:, None]
        W[free] = W[free] * .4 + avg[free] * .6
    # Maks 4 pengaruh per vertex.
    order = np.argsort(-W, axis=1)
    W[np.arange(len(W))[:, None], order[:, 4:]] = 0
    W /= W.sum(1, keepdims=True)
    return names, W


def drape_lap(low, part, W, names, target, lap):
    """Rok yang mengembang ikut terangkat saat paha diputar ke depan: kain di atas paha dipadatkan ke paha
    (seperti kain jatuh) supaya pangkuan tidak menembus meja."""
    me = low.data
    P = np.array([v.co[:] for v in me.vertices])
    for sgn, side in ((-1, "Left"), (1, "Right")):
        h, k = (np.asarray(x) for x in target["Thigh_" + side])
        u = (k - h) / np.linalg.norm(k - h)
        w = W[:, names.index("Thigh_" + side)] + W[:, names.index("Shin_" + side)]
        sel = (part == 3) & (w > .05) & (sgn * P[:, 0] >= 0)
        t = np.clip((P[sel] - h) @ u, 0, None)
        up = P[sel, 2] - (h + np.outer(t, u))[:, 2]
        excess = np.maximum(up - lap, 0)
        P[sel, 2] -= excess * .8 * np.clip(w[sel] * 1.5, 0, 1)
    me.vertices.foreach_set("co", P.ravel())
    me.update()


def two_bone(s_pos, w_pos, l1, l2, pole):
    d = w_pos - s_pos
    dist = min(np.linalg.norm(d), (l1 + l2) * .999)
    d = d / np.linalg.norm(d)
    w_pos = s_pos + d * dist
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(l1 * l1 - a * a, 0))
    pole = pole - d * (pole @ d)
    pole /= np.linalg.norm(pole)
    return s_pos + d * a + pole * h, w_pos


def build_full(whole, head_high, wrists, scale, offset, cfg, name, out_dir, preview_dir):
    """Satu mesh utuh + rig bot full-body; rest pose = duduk di kursi dengan tangan di atas meja."""
    from mathutils.bvhtree import BVHTree
    f = cfg["full"]

    def N(p):
        return np.array((Vector(tuple(p)) + offset) * scale)

    low = make_low(whole, f["tris"])
    low.name = name + "Mesh"
    # Label bagian per vertex dari wajah terdekat di mesh asli (0 kepala, 1/2 tangan, 3 badan).
    bvh = BVHTree.FromObject(whole, bpy.context.evaluated_depsgraph_get())
    parts = np.zeros(len(whole.data.polygons), np.int32)
    whole.data.attributes["Part"].data.foreach_get("value", parts)
    co = np.array([v.co[:] for v in low.data.vertices])
    part = np.array([parts[bvh.find_nearest(Vector(c))[2]] for c in co])

    # UV: pulau kepala diperbesar sebelum packing supaya wajah/rambut lebih tajam.
    unwrap([low], [1])
    me = low.data
    uv = np.empty(len(me.loops) * 2, np.float32)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    lv = np.empty(len(me.loops), np.int64)
    me.loops.foreach_get("vertex_index", lv)
    uv[part[lv] == 0] *= cfg["head_texel"]
    me.uv_layers.active.data.foreach_set("uv", uv.ravel())
    select(low)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=.003)
    bpy.ops.object.mode_set(mode="OBJECT")
    bake_atlas(f"{name}_Full", [low], [whole], (f["texel"], f["texel"], 1024), out_dir)

    # Titik sendi A-pose (meter, kaki di z=0).
    hm = head_high.data
    flags = np.zeros(len(hm.edges), np.int32)
    hm.attributes["Cut"].data.foreach_get("value", flags)
    ring = {i for e, fl in zip(hm.edges, flags) if fl for i in e.vertices}
    neck = np.array([hm.vertices[i].co[:] for i in ring]).mean(0)
    top = co[part == 0][:, 2].max()
    pelvis, chest = N(f["pelvis"]), N(f["chest"])
    J = {"Hips": (pelvis, pelvis + np.array([0, 0, .1]))}
    J["Spine"] = (J["Hips"][1], chest)
    J["Head"] = (neck, np.array([neck[0], neck[1], top]))
    for sgn, side in ((-1, "Left"), (1, "Right")):
        m = np.array([sgn, 1, 1])
        wp, wa = wrists[sgn]
        el = N(wp + wa * ((f["elbow_z"] - wp.z) / wa.z))
        J["Arm_" + side] = (N(np.array(f["shoulder"]) * m), el)
        J["Forearm_" + side] = (el, N(wp))
        J["Hand_" + side] = (N(wp), N(wp) + np.array(wa) * .08)
        hip, knee, ankle = (N(np.array(f[k]) * m) for k in ("hip", "knee", "ankle"))
        J["Thigh_" + side] = (hip, knee)
        J["Shin_" + side] = (knee, ankle)

    edges = np.empty(len(me.edges) * 2, np.int64)
    me.edges.foreach_get("vertices", edges)
    limit = N((cfg["arm_x"], 0, f["skirt_z"]))
    names, W = full_weights(co, part, edges.reshape(-1, 2), J, neck, abs(limit[0]), limit[2])
    for i, n in enumerate(names):
        g = low.vertex_groups.new(name=n)
        for vi in np.nonzero(W[:, i] > 1e-4)[0]:
            g.add([int(vi)], float(W[vi, i]), "REPLACE")

    parent = {"Hips": None, "Spine": "Hips", "Head": "Spine"}
    for side in ("Left", "Right"):
        parent.update({"Arm_" + side: "Spine", "Forearm_" + side: "Arm_" + side, "Hand_" + side: "Forearm_" + side,
                       "Thigh_" + side: "Hips", "Shin_" + side: "Thigh_" + side})
    ad = bpy.data.armatures.new(name + "_Rig")
    arm = bpy.data.objects.new(name, ad)
    bpy.context.scene.collection.objects.link(arm)
    select(arm)
    bpy.ops.object.mode_set(mode="EDIT")
    for n, (a, b) in J.items():
        eb = ad.edit_bones.new(n)
        eb.head, eb.tail = Vector(a), Vector(b)
    for n, pn in parent.items():
        if pn:
            ad.edit_bones[n].parent = ad.edit_bones[pn]
    bpy.ops.object.mode_set(mode="OBJECT")
    low.parent = arm
    low.modifiers.new("Armature", "ARMATURE").object = arm

    # Pose duduk: panggul di dudukan, punggung dekat sandaran, paha ke depan, betis turun, tangan di meja.
    thigh_r = .085
    torso = (part == 3) & (np.abs(co[:, 0]) < .2) & (co[:, 2] > pelvis[2]) & (co[:, 2] < chest[2])
    body_back = co[torso][:, 1].max() - pelvis[1]
    seat_pelvis = np.array([0, f["back_y"] - .015 - body_back, f["seat_z"] + thigh_r + (pelvis[2] - J["Thigh_Left"][0][2])])
    lean = Matrix.Rotation(math.radians(f["lean"]), 3, "X")       # positif = condong ke depan (-Y)
    nod = Matrix.Rotation(math.radians(f["lean"] + f["head_tilt"]), 3, "X")

    def to_seat(p):
        return np.array(lean @ Vector(tuple(p - pelvis))) + seat_pelvis

    target = {n: [to_seat(J[n][0]), to_seat(J[n][1])] for n in ("Hips", "Spine")}
    # Wajah Meshy agak menengadah: kepala ditundukkan sedikit di sendi leher.
    target["Head"] = [to_seat(J["Head"][0]), to_seat(J["Head"][0]) + np.array(nod @ Vector(tuple(J["Head"][1] - J["Head"][0])))]
    for sgn, side in ((-1, "Left"), (1, "Right")):
        hip = to_seat(J["Thigh_" + side][0])
        l_th = np.linalg.norm(J["Thigh_" + side][1] - J["Thigh_" + side][0])
        l_sh = np.linalg.norm(J["Shin_" + side][1] - J["Shin_" + side][0])
        th = np.array([sgn * f["thigh_splay"], -1, -.04])
        knee = hip + th / np.linalg.norm(th) * l_th
        sd = np.array([0, -.12, -1])
        target["Thigh_" + side] = [hip, knee]
        target["Shin_" + side] = [knee, knee + sd / np.linalg.norm(sd) * l_sh]
        sh = to_seat(J["Arm_" + side][0])
        l1 = np.linalg.norm(J["Arm_" + side][1] - J["Arm_" + side][0])
        l2 = np.linalg.norm(J["Forearm_" + side][1] - J["Forearm_" + side][0])
        el, wr = two_bone(sh, np.array(f["wrist"]) * [sgn, 1, 1], l1, l2, np.array([sgn * 1.0, .4, -1.0]))
        hand = np.array([-sgn * .15, -1, -.05])
        target["Arm_" + side] = [sh, el]
        target["Forearm_" + side] = [el, wr]
        target["Hand_" + side] = [wr, wr + hand / np.linalg.norm(hand) * .08]

    select(arm)
    bpy.ops.object.mode_set(mode="POSE")
    order = ["Hips", "Spine", "Head"] + [p + s for s in ("Left", "Right") for p in ("Arm_", "Forearm_", "Hand_", "Thigh_", "Shin_")]
    for n in order:
        rest = ad.bones[n].matrix_local.to_3x3()
        a, b = J[n]
        ta, tb = target[n]
        rd, td = Vector(tuple(b - a)), Vector(tuple(tb - ta))
        if n.startswith(("Forearm", "Hand")):
            sgn = -1 if n.endswith("Left") else 1
            rot = frame(td, (0, 0, -1)) @ frame(rd, (-sgn, 0, 0)).inverted()
        elif n in ("Hips", "Spine", "Head"):
            rot = nod.copy() if n == "Head" else lean.copy()
        else:
            rot = rd.rotation_difference(td).to_matrix()
        arm.pose.bones[n].matrix = Matrix.Translation(Vector(tuple(ta))) @ (rot @ rest).to_4x4()
        bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")
    select(low)
    bpy.ops.object.modifier_apply(modifier="Armature")
    drape_lap(low, part, W, names, target, f["lap"])
    select(arm)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    low.modifiers.new("Armature", "ARMATURE").object = arm
    log(f"full {low.name}: {len(low.data.polygons)} tris, {len(names)} bones")
    for n in order:
        b = ad.bones[n]
        log(f"   {n:14s} {np.round(b.head_local, 3)} -> {np.round(b.tail_local, 3)}")

    select(arm, low)
    path = os.path.join(ROOT, "Assets", "BiteOrBetray", "Art3D", "Models", name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
        bake_anim=False, use_armature_deform_only=False, mesh_smooth_type="FACE", use_mesh_modifiers=False,
        use_custom_props=False, primary_bone_axis="Y", secondary_bone_axis="X", path_mode="STRIP")
    log(f"export {path}")

    if preview_dir:
        props = []
        for model, loc in (("Chair", (0, 0, 0)), ("Table", (0, -1.2, 0))):
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, "Assets", "BiteOrBetray", "Art3D", "Models", model + ".fbx"))
            for o in set(bpy.data.objects) - before:
                if o.parent is None:
                    o.location = Vector(loc) + o.location
                if o.type == "MESH":
                    props.append(o)
        bpy.context.view_layer.update()
        preview([low] + props, os.path.join(preview_dir, f"{name}_seated"), views=("front", "q", "side", "back"),
                size=(2000, 900), frame_objs=[low])
        for o in props:
            bpy.data.objects.remove(o)
    return arm


# ---------------------------------------------------------------------------------------------------
# Preview & export
# ---------------------------------------------------------------------------------------------------

def preview(objs, path, views=("front", "q", "back", "side"), size=(1800, 900), flat=False, frame_objs=None):
    scene = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    scene.render.engine = "BLENDER_WORKBENCH" if flat else next(e for e in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT") if e in engines)
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.film_transparent = False
    if not scene.world:
        scene.world = bpy.data.worlds.new("w")
    scene.world.color = (.55, .55, .58)
    hidden = [o for o in scene.objects if o.type == "MESH" and o not in objs]
    for o in hidden:
        o.hide_render = True
    deps = bpy.context.evaluated_depsgraph_get()
    pts = np.array([o.matrix_world @ v.co for o in (frame_objs or objs) for v in o.evaluated_get(deps).data.vertices])
    lo, hi = pts.min(0), pts.max(0)
    c = Vector((lo + hi) / 2)
    ext = max(hi - lo) * 1.15
    dirs = dict(front=(0, -1, 0), back=(0, 1, 0), side=(1, 0, 0), q=(.7, -.7, .1))
    tiles = []
    cam_data = bpy.data.cameras.new("pc")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("pc", cam_data)
    scene.collection.objects.link(cam)
    sun = bpy.data.objects.new("ps", bpy.data.lights.new("ps", "SUN"))
    sun.data.energy = 3.5
    scene.collection.objects.link(sun)
    scene.camera = cam
    w = size[0] // len(views)
    scene.render.resolution_x = w
    cam_data.ortho_scale = ext * max(1, size[1] / w)
    for v in views:
        d = Vector(dirs[v]).normalized()
        cam.location = c + d * 6
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        sun.rotation_euler = (-(d + Vector((.3, 0, .6)))).to_track_quat("-Z", "Y").to_euler()
        p = path + f".{v}.png"
        scene.render.filepath = p
        bpy.ops.render.render(write_still=True)
        tiles.append(p)
    # gabung jadi satu sheet
    imgs = [bpy.data.images.load(p) for p in tiles]
    W, H = imgs[0].size
    sheet = np.zeros((H, W * len(imgs), 4), np.float32)
    for i, im in enumerate(imgs):
        a = np.empty(W * H * 4, np.float32)
        im.pixels.foreach_get(a)
        sheet[:, i * W:(i + 1) * W] = a.reshape(H, W, 4)
    out = bpy.data.images.new("sheet", W * len(imgs), H)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = path + ".png"
    out.file_format = "PNG"
    out.save()
    for p in tiles:
        os.remove(p)
    for o in hidden:
        o.hide_render = False
    bpy.data.objects.remove(cam)
    bpy.data.objects.remove(sun)
    log(f"preview {path}.png")


def export(objs, path):
    select(*objs)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
        bake_anim=False, mesh_smooth_type="FACE", use_mesh_modifiers=True, use_custom_props=False,
        path_mode="STRIP")
    log(f"export {path}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True)
    ap.add_argument("--name", default="Cherry")
    ap.add_argument("--preview")
    ap.add_argument("--blend")
    ap.add_argument("--stage", choices=("split", "all"), default="all")
    ap.add_argument("--float", action="store_true", help="juga buat Models/<Name>_Float.fbx (kepala + tangan saja)")
    args = ap.parse_args(argv)
    cfg = CONFIG[args.name]
    out_dir = os.path.join(ROOT, "Assets", "BiteOrBetray", "Art3D", "Meshy", args.name)

    high = load(args.src)
    pieces, base, wrists, whole = split(high, cfg)
    scale, offset = normalize(list(pieces.values()) + [whole], cfg["height"])
    high_materials(dict(pieces, Whole=whole), base)
    if args.preview:
        tint = dict(Head=(1, .55, .2, 1), Hand_Left=(.2, .5, 1, 1), Hand_Right=(.2, 1, .5, 1), Body=(.75, .75, .75, 1))
        for name, o in pieces.items():
            o.color = tint[name]
        bpy.context.scene.display.shading.color_type = "OBJECT"
        preview(list(pieces.values()), os.path.join(args.preview, f"{args.name}_split_high"), flat=True)
    if args.stage == "split":
        return

    os.makedirs(out_dir, exist_ok=True)
    t = cfg["tris"]
    lows = {n: make_low(pieces[n], t["Head"] if n == "Head" else t["Hand"] if n.startswith("Hand") else t["Body"]) for n in PARTS}
    unwrap([lows["Head"], lows["Hand_Left"], lows["Hand_Right"]], [cfg["head_texel"], 1, 1])
    unwrap([lows["Body"]], [1])
    bake_atlas(f"{args.name}_HeadHands", [lows["Head"], lows["Hand_Left"], lows["Hand_Right"]],
               [pieces["Head"], pieces["Hand_Left"], pieces["Hand_Right"]], (2048, 2048, 1024), out_dir)
    bake_atlas(f"{args.name}_Body", [lows["Body"]], [pieces["Body"]], (2048, 1024, 1024), out_dir)
    origins(lows, pieces)
    for o in pieces.values():
        o.hide_set(True)
        o.hide_render = True
    total = sum(len(o.data.polygons) for o in lows.values())
    floating = total - len(lows["Body"].data.polygons)
    log(f"TOTAL {total} tris (floating head+hands {floating})")
    export(list(lows.values()), os.path.join(out_dir, f"{args.name}_Meshy.fbx"))
    rig = build_float(lows, args.name, cfg) if args.float else None
    full = build_full(whole, pieces["Head"], wrists, scale, offset, cfg, args.name, out_dir, args.preview)
    if args.preview:
        preview(list(lows.values()), os.path.join(args.preview, f"{args.name}_low"))
        preview([lows["Head"], lows["Hand_Left"], lows["Hand_Right"]], os.path.join(args.preview, f"{args.name}_floating"))
        preview([pieces["Head"]], os.path.join(args.preview, f"{args.name}_head_high"), views=("front", "q", "back"), size=(1500, 600))
        preview([lows["Head"]], os.path.join(args.preview, f"{args.name}_head_low"), views=("front", "q", "back"), size=(1500, 600))
        if rig:
            preview(list(rig.arm.children), os.path.join(args.preview, f"{args.name}_float_rig"), views=("front", "q", "side"), size=(1500, 700))
    if args.blend:
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=args.blend)
        log(f"blend {args.blend}")


if __name__ == "__main__":
    main()
