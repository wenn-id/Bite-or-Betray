"""Karakter Meshy yang sudah di-rig (Meshy auto-rig / Mixamo) -> bot meja atau pelayan Bite or Betray.

    blender -b -P Tools/Blender/bob_meshy_rigged.py -- --dir <folder GLB/FBX> [--only RedGrouch,...] [--preview DIR]

Per karakter: buang objek bantu, skala ke tinggi desain, ganti nama tulang ke konvensi project (Hips, Spine, Chest*,
Neck, Head, Shoulder_*/Arm_*/Forearm_*/Hand_*, Thigh_*/Shin_*/Foot_*; Left = sisi -X seperti bob_characters),
pose duduk di kursi dengan tangan di meja (atau berdiri untuk pelayan) lalu jadikan rest pose, export
Models/<Name>.fbx dan tekstur URP (BaseColor, Normal, MetallicSmoothness) ke Art3D/Meshy/<Name>/.
Skin weight bawaan rig dipakai apa adanya.
"""
import argparse
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bob_meshy_split as ms  # noqa: E402

ROOT = ms.ROOT
ART = os.path.join(ROOT, "Assets", "BiteOrBetray", "Art3D")

# Kursi/meja dalam ruang kursi (meter, menghadap -Y): sama dengan Cherry (bob_meshy_split CONFIG["Cherry"]["full"]).
SEAT = dict(seat_z=.53, back_y=.12, lean=4, thigh_splay=.06, wrist=(.18, -.43, .80))
# Batas segitiga per karakter (owner tidak punya Quest, jadi margin performa dibuat besar). Skin weight ikut
# terinterpolasi oleh decimate; UV dipertahankan, jadi tekstur Meshy tetap dipakai apa adanya.
TRIS = 20000
CAST = {
    "GentleGiant": dict(src="Meshy_AI_The_Gentle_Giant_All_Animations.glb", height=1.90),   # A-pose ulang 01_Stylized_Gentleman
    "BlueGentleman": dict(src="02_Blue_Gentleman.fbx", height=1.82),
    "StripedChef": dict(src="03_Striped_Chef.glb", height=1.80, pose="stand"),
    # Badan kecil: tangan diletakkan lebih dekat dan badan lebih condong supaya lengan tidak terjulur kaku.
    "LavenderWoman": dict(src="04_Lavender_Woman.glb", height=1.55, seat=dict(lean=9, wrist=(.16, -.37, .79))),
    "RedGrouch": dict(src="05_Red_Grouch.glb", height=1.62),
}


def log(*a):
    print("[rigged]", *a, flush=True)


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path)
    else:
        bpy.ops.import_scene.gltf(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)]
    for o in list(bpy.data.objects):
        if o.type == "MESH" and o not in meshes:
            bpy.data.objects.remove(o)              # Icosphere bantu dari Meshy
    for o in [arm] + meshes:
        if o.animation_data:
            o.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    for pb in arm.pose.bones:
        pb.matrix_basis.identity()
    return arm, meshes


def apply_scale(arm, meshes, height):
    pts = np.array([o.matrix_world @ v.co for o in meshes for v in o.data.vertices])
    k = height / (pts[:, 2].max() - pts[:, 2].min())
    arm.scale = arm.scale * k
    ms.select(arm, *meshes)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return k


def decimate(meshes, target):
    """Kurangi segitiga sebelum pose (modifier Decimate di atas Armature, lalu di-apply).

    GLB Meshy menyimpan vertex terpisah di setiap sambungan UV; tanpa dilas dulu, collapse membuat wajah/rambut
    bolong. UV adalah data per-loop, jadi tetap utuh setelah vertex dilas."""
    for o in meshes:
        o.data.calc_loop_triangles()
        n = len(o.data.loop_triangles)
        if n <= target:
            continue
        bm = bmesh.new()
        bm.from_mesh(o.data)
        before = len(bm.verts)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bm.to_mesh(o.data)
        bm.free()
        if o.data.has_custom_normals:
            ms.select(o)
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
        o.data.shade_smooth()
        log(f"   weld {o.name}: {before} -> {len(o.data.vertices)} verts")
        ms.select(o)
        mod = o.modifiers.new("Decimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = target / n
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_move_to_index(modifier=mod.name, index=0)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        o.data.calc_loop_triangles()
        log(f"   decimate {o.name}: {n} -> {len(o.data.loop_triangles)} tris")


def rename(arm):
    """Nama tulang project. Sisi ditentukan dari posisi (Left = -X), bukan dari nama asli."""
    bones = arm.data.bones
    base = {b.name: b.name.split(":")[-1] for b in bones}

    def side(b):
        return "Left" if b.head_local.x < 0 else "Right"

    head = next(b for b in bones if base[b.name].lower() == "head")
    hips = next(b for b in bones if base[b.name].lower() == "hips")
    chain = []
    b = head.parent
    while b and b != hips:
        chain.append(b)
        b = b.parent
    chain.reverse()                                  # Hips -> ... -> Neck
    names = {hips.name: "Hips", head.name: "Head"}
    spine = [c for c in chain if "neck" not in base[c.name].lower()]
    for i, c in enumerate(spine):
        names[c.name] = "Spine" if i == 0 else f"Chest{i}"
    for c in chain:
        if "neck" in base[c.name].lower():
            names[c.name] = "Neck"
    table = (("shoulder", "Shoulder"), ("forearm", "Forearm"), ("arm", "Arm"), ("upleg", "Thigh"),
             ("leg", "Shin"), ("foot", "Foot"), ("toebase", "Toe"))
    for b in bones:
        if b.name in names:
            continue
        n = base[b.name].lower().replace("left", "").replace("right", "")
        if base[b.name].lower().startswith(("left", "right")) and n in dict(table):
            names[b.name] = f"{dict(table)[n]}_{side(b)}"
        elif base[b.name].lower().startswith(("left", "right")) and n == "hand":
            names[b.name] = f"Hand_{side(b)}"
    # Sisanya (ujung jari, ujung kepala/kaki, headfront, tulang tambahan): nama netral yang tidak bentrok dengan
    # awalan yang dicari CharacterAnimator (Head, Spine, Arm_, Hand, Forearm).
    for b in bones:
        if b.name not in names:
            names[b.name] = "X_" + base[b.name].replace("Head", "Hd").replace("Hand", "Hnd").replace("head", "hd")
    # Dua tahap supaya tidak bentrok (mis. Spine02 -> Spine saat "Spine" asli masih ada). Vertex group ikut berganti.
    for i, old in enumerate(list(names)):
        arm.data.bones[old].name = f"__tmp{i}"
    for i, new in enumerate(names.values()):
        arm.data.bones[f"__tmp{i}"].name = new
    need = {"Hips", "Spine", "Head"} | {f"{p}_{s}" for p in ("Arm", "Forearm", "Hand", "Thigh", "Shin") for s in ("Left", "Right")}
    missing = need - {b.name for b in arm.data.bones}
    assert not missing, missing
    for o in arm.children:
        if o.type == "MESH":
            stale = [g.name for g in o.vertex_groups if g.name.startswith("__tmp") or ":" in g.name]
            assert not stale, stale


def bone_line(arm, n):
    b = arm.data.bones[n]
    return np.array(b.head_local), np.array(b.tail_local)


def joint(arm, n):
    return np.array(arm.data.bones[n].head_local)


def pose_char(arm, meshes, standing, seat_cfg=None):
    """Target pose dalam ruang armature, lalu dijadikan rest pose."""
    SEAT_ = dict(SEAT, **(seat_cfg or {}))
    co = np.array([v.co[:] for o in meshes for v in o.data.vertices])
    lean = Matrix.Rotation(math.radians(0 if standing else SEAT_["lean"]), 3, "X")
    hips = joint(arm, "Hips")
    target = {}
    if standing:
        offset = np.zeros(3)
        to_body = lambda p: np.asarray(p)
    else:
        hip_l, hip_r = joint(arm, "Thigh_Left"), joint(arm, "Thigh_Right")
        hip_c = (hip_l + hip_r) / 2
        knee = joint(arm, "Shin_Left")
        # Tebal paha dari mesh di sekitar tengah paha (untuk tinggi duduk).
        mid = (hip_l + knee) / 2
        ring = co[np.abs(co[:, 2] - mid[2]) < .03]
        ring = ring[ring[:, 0] < 0]
        thigh_r = float(np.clip(np.percentile(np.abs(ring[:, 1] - mid[1]), 85), .05, .14)) if len(ring) else .08
        chest = joint(arm, "Neck")
        torso = co[(co[:, 2] > hip_c[2]) & (co[:, 2] < chest[2]) & (np.abs(co[:, 0]) < .22)]
        back = torso[:, 1].max() - hips[1]
        seat_hips = np.array([0, SEAT_["back_y"] - .015 - back, SEAT_["seat_z"] + thigh_r + (hips[2] - hip_c[2])])

        def to_body(p):
            return np.array(lean @ Vector(tuple(np.asarray(p) - hips))) + seat_hips

    # Badan (Hips, Spine, Chest*, Neck, Head, Shoulder_*) ikut translasi + condong.
    upper = [b.name for b in arm.data.bones if b.name in ("Hips", "Neck", "Head") or b.name.startswith(("Spine", "Chest", "Shoulder"))]
    for n in upper:
        a, b = bone_line(arm, n)
        target[n] = (to_body(a), to_body(b), "lean")
    for sgn, side in ((-1, "Left"), (1, "Right")):
        sh = to_body(joint(arm, "Arm_" + side))
        l1 = np.linalg.norm(joint(arm, "Forearm_" + side) - joint(arm, "Arm_" + side))
        l2 = np.linalg.norm(joint(arm, "Hand_" + side) - joint(arm, "Forearm_" + side))
        hand_len = np.linalg.norm(np.subtract(*bone_line(arm, "Hand_" + side)[::-1]))
        if standing:
            # Pelayan: lengan turun santai, siku sedikit ditekuk, tangan di depan pinggang.
            wr_t = sh + np.array([sgn * .05, -.22, -(l1 + l2) * .78])
            pole = np.array([sgn * .4, 1.0, -.2])
            hand_dir = np.array([-sgn * .2, -1, -.3])
        else:
            wr_t = np.array(SEAT_["wrist"]) * [sgn, 1, 1]
            pole = np.array([sgn * 1.0, .4, -1.0])
            hand_dir = np.array([-sgn * .15, -1, -.05])
        el, wr = ms.two_bone(sh, wr_t, l1, l2, pole)
        target["Arm_" + side] = (sh, el, "min")
        target["Forearm_" + side] = (el, wr, "palm")
        target["Hand_" + side] = (wr, wr + hand_dir / np.linalg.norm(hand_dir) * hand_len, "palm")
        if standing:
            continue
        hip = to_body(joint(arm, "Thigh_" + side))
        l_th = np.linalg.norm(joint(arm, "Shin_" + side) - joint(arm, "Thigh_" + side))
        l_sh = np.linalg.norm(joint(arm, "Foot_" + side) - joint(arm, "Shin_" + side))
        th = np.array([sgn * SEAT_["thigh_splay"], -1, -.04])
        kn = hip + th / np.linalg.norm(th) * l_th
        foot_h = joint(arm, "Foot_" + side)[2]       # tinggi pergelangan kaki dari lantai saat berdiri
        sd = np.array([0, -.12, -1])
        sd = sd / np.linalg.norm(sd)
        if kn[2] + sd[2] * l_sh < foot_h:            # kaki panjang: betis dimiringkan ke depan supaya tidak tembus lantai
            dz = kn[2] - foot_h
            sd = np.array([0, -math.sqrt(max(l_sh ** 2 - dz ** 2, 0)), -dz]) / l_sh
        an = kn + sd * l_sh
        target["Thigh_" + side] = (hip, kn, "min")
        target["Shin_" + side] = (kn, an, "min")
        target["Foot_" + side] = (an, None, "keep")    # telapak tetap datar seperti saat berdiri

    ms.select(arm)
    bpy.ops.object.mode_set(mode="POSE")
    order = [b.name for b in arm.data.bones if b.name in target]
    order.sort(key=lambda n: len(arm.data.bones[n].parent_recursive))
    for n in order:
        bone = arm.data.bones[n]
        rest = bone.matrix_local.to_3x3()
        a, b = bone_line(arm, n)
        ta, tb, mode = target[n]
        if mode == "lean":
            rot = lean.copy()
        elif mode == "keep":
            rot = Matrix.Identity(3)
        elif mode == "palm":
            sgn = -1 if n.endswith("Left") else 1
            rot = ms.frame(Vector(tuple(tb - ta)), (0, 0, -1)) @ ms.frame(Vector(tuple(b - a)), (-sgn, 0, 0)).inverted()
        else:
            rot = Vector(tuple(b - a)).rotation_difference(Vector(tuple(tb - ta))).to_matrix()
        arm.pose.bones[n].matrix = Matrix.Translation(Vector(tuple(ta))) @ (rot @ rest).to_4x4()
        bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in meshes:
        ms.select(o)
        mod = next(m for m in o.modifiers if m.type == "ARMATURE")
        bpy.ops.object.modifier_apply(modifier=mod.name)
    ms.select(arm)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in meshes:
        o.modifiers.new("Armature", "ARMATURE").object = arm


def textures(name, meshes, out_dir):
    """Material glTF -> PNG gaya URP + satu material bernama <Name>_Full (dipetakan EnsureMeshyMaterials)."""
    os.makedirs(out_dir, exist_ok=True)
    mat = meshes[0].data.materials[0]
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")

    def upstream_image(socket):
        stack = [l.from_node for l in socket.links]
        while stack:
            n = stack.pop()
            if n.type == "TEX_IMAGE":
                return n.image
            stack += [l.from_node for i in n.inputs for l in i.links]
        return None

    base = upstream_image(bsdf.inputs["Base Color"])
    normal = upstream_image(bsdf.inputs["Normal"])
    mr = upstream_image(bsdf.inputs["Roughness"]) or upstream_image(bsdf.inputs["Metallic"])
    assert base is not None, "tekstur base color tidak ditemukan di " + mat.name
    atlas = name + "_Full"

    def save(img, kind, pixels=None):
        w, h = img.size
        out = bpy.data.images.new(f"{atlas}_{kind}", w, h, alpha=kind == "MetallicSmoothness")
        if pixels is None:
            pixels = np.empty(w * h * 4, np.float32)
            img.pixels.foreach_get(pixels)
        if kind != "BaseColor":
            out.colorspace_settings.name = "Non-Color"
        out.pixels.foreach_set(pixels)
        out.filepath_raw = os.path.join(out_dir, out.name + ".png")
        out.file_format = "PNG"
        out.save()
        log(f"   {out.filepath_raw} {w}x{h}")

    base.colorspace_settings.name = "sRGB"
    save(base, "BaseColor")
    if normal:
        normal.colorspace_settings.name = "Non-Color"
        save(normal, "Normal")
    if mr:
        mr.colorspace_settings.name = "Non-Color"
        w, h = mr.size
        px = np.empty(w * h * 4, np.float32)
        mr.pixels.foreach_get(px)
        px = px.reshape(-1, 4)
        out = np.zeros_like(px)
        out[:, 0] = px[:, 2]                       # glTF: G = roughness, B = metallic
        out[:, 3] = 1 - px[:, 1]
        save(mr, "MetallicSmoothness", out.ravel())
    for o in meshes:
        o.data.materials.clear()
        o.data.materials.append(mat)
    mat.name = atlas


def export(name, arm, meshes):
    arm.name = name
    arm.data.name = name + "_Rig"
    for i, o in enumerate(meshes):
        o.name = name + "Mesh" + (str(i) if i else "")
    ms.select(arm, *meshes)
    path = os.path.join(ART, "Models", name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
        bake_anim=False, use_armature_deform_only=False, mesh_smooth_type="FACE", use_mesh_modifiers=False,
        use_custom_props=False, primary_bone_axis="Y", secondary_bone_axis="X", path_mode="STRIP")
    tris = 0
    for o in meshes:
        o.data.calc_loop_triangles()
        tris += len(o.data.loop_triangles)
    log(f"export {path}: {tris} tris, {len(arm.data.bones)} bones")


def preview(name, arm, meshes, out, standing):
    props = []
    for model, loc in ((("Chair", (0, 0, 0)), ("Table", (0, -1.2, 0))) if not standing else (("Table", (0, -1.6, 0)),)):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(ART, "Models", model + ".fbx"))
        for o in set(bpy.data.objects) - before:
            if o.parent is None:
                o.location = Vector(loc) + o.location
            if o.type == "MESH":
                props.append(o)
    bpy.context.view_layer.update()
    ms.preview(meshes + props, os.path.join(out, name + "_posed"), views=("front", "q", "side", "back"), size=(2000, 900),
               frame_objs=meshes)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--only")
    ap.add_argument("--preview")
    args = ap.parse_args(argv)
    for name, c in CAST.items():
        if args.only and name not in args.only.split(","):
            continue
        log(f"== {name} ({c['src']})")
        arm, meshes = load(os.path.join(args.dir, c["src"]))
        apply_scale(arm, meshes, c["height"])
        decimate(meshes, c.get("tris", TRIS))
        rename(arm)
        standing = c.get("pose") == "stand"
        pose_char(arm, meshes, standing, c.get("seat"))
        textures(name, meshes, os.path.join(ART, "Meshy", name))
        export(name, arm, meshes)
        if args.preview:
            preview(name, arm, meshes, args.preview, standing)


if __name__ == "__main__":
    main()
