"""Add procedural facial BlendShapes (Shape Keys) and an openable 3D mouth cavity to Meshy character meshes.

    blender -b -P Tools/Blender/bob_meshy_faces.py -- [--only Cherry,...] [--preview DIR]

Splits the mouth seam along each character's exact parabolic lip parting curve z_seam(x) and builds
an interior 3D mouth bag (dark cavity, upper teeth, and tongue mapped to existing pixels in
<Name>_Full_BaseColor.png), then creates 13 facial Shape Keys on <Name>Mesh for all 6 Meshy
characters (Cherry, BlueGentleman, RedGrouch, LavenderWoman, GentleGiant, StripedChef):
  - Blink, EyeWide
  - GazeLeft, GazeRight, GazeUp, GazeDown
  - BrowUp, BrowKnit, BrowSad
  - Open, Chew, Grin, Frown

Zero extra GameObjects, zero extra draw calls, and zero extra materials; driven directly by
CharacterAnimator.cs in LateUpdate.
"""
import argparse
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(ROOT, "Assets", "BiteOrBetray", "Art3D")
MODELS_DIR = os.path.join(ART, "Models")
MESHY_DIR = os.path.join(ART, "Meshy")


def smooth01(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


# Per-character facial landmarks measured from high-res UV texture raycasts in world space (Z-up, facing -Y).
FACE_SPECS = {
    "Cherry": dict(
        subdiv_box=dict(z=(1.17, 1.38), x=0.088, y_max=-0.25, cuts=2),
        y_cut=-0.255,
        uv_dark=(0.1704, 0.4844),
        uv_white=(0.3267, 0.5327),
        uv_tongue=(0.4712, 0.5444),
        eyes=[
            dict(xc=-0.044, rx=0.030, z_bot=1.292, z_top=1.348, z_skin=1.360, lower_frac=0.20, pupil_x=-0.032, pupil_z=1.310),
            dict(xc=+0.044, rx=0.030, z_bot=1.292, z_top=1.348, z_skin=1.360, lower_frac=0.20, pupil_x=+0.030, pupil_z=1.310),
        ],
        brows=[
            dict(xb=-0.045, rxb=0.035, zb=1.368, rzb=0.014, side=-1, lift=0.013),
            dict(xb=+0.045, rxb=0.035, zb=1.368, rzb=0.014, side=+1, lift=0.013),
        ],
        mouth=dict(xm=0.000, rxm=0.036, rx_cut=0.025, zm=1.249, curve=-0.0010, z_chin=1.165, drop=0.025, grin_up=0.012, grin_out=0.006, frown_dn=0.011, teeth=True),
    ),
    "BlueGentleman": dict(
        subdiv_box=dict(z=(1.13, 1.25), x=0.065, y_max=-0.13, cuts=2),
        y_cut=-0.135,
        glasses_z=(1.270, 1.354),
        glasses_y=(-0.1695, -0.1645),
        uv_dark=(0.0361, 0.8247),
        uv_white=(0.3423, 0.9390),
        uv_tongue=(0.8584, 0.9707),
        eyes=[
            dict(xc=-0.032, rx=0.022, z_bot=1.293, z_top=1.347, z_skin=1.354, lower_frac=0.25, pupil_x=-0.025, pupil_z=1.313),
            dict(xc=+0.021, rx=0.022, z_bot=1.293, z_top=1.347, z_skin=1.354, lower_frac=0.25, pupil_x=+0.018, pupil_z=1.315),
        ],
        brows=[
            dict(xb=-0.036, rxb=0.028, zb=1.365, rzb=0.015, side=-1, lift=0.013),
            dict(xb=+0.018, rxb=0.028, zb=1.365, rzb=0.015, side=+1, lift=0.013),
        ],
        mouth=dict(xm=-0.004, rxm=0.031, rx_cut=0.022, zm=1.206, curve=+0.0035, z_chin=1.135, drop=0.025, grin_up=0.012, grin_out=0.005, frown_dn=0.011, teeth=True),
    ),
    "RedGrouch": dict(
        subdiv_box=dict(z=(1.21, 1.48), x=0.095, y_max=-0.43, cuts=2),
        y_cut=-0.43,
        uv_dark=(0.2700, 0.9575),
        uv_white=(0.2666, 0.9526),
        uv_tongue=(0.5034, 0.3643),
        eyes=[
            dict(xc=-0.047, rx=0.034, z_bot=1.403, z_top=1.430, z_skin=1.442, lower_frac=0.68, pupil_x=-0.039, pupil_z=1.414),
            dict(xc=+0.047, rx=0.034, z_bot=1.403, z_top=1.430, z_skin=1.442, lower_frac=0.68, pupil_x=+0.039, pupil_z=1.414),
        ],
        brows=[
            dict(xb=-0.042, rxb=0.040, zb=1.450, rzb=0.020, side=-1, lift=0.014),
            dict(xb=+0.052, rxb=0.040, zb=1.450, rzb=0.020, side=+1, lift=0.014),
        ],
        mouth=dict(xm=+0.006, rxm=0.038, rx_cut=0.025, zm=1.312, curve=-0.0015, z_chin=1.205, drop=0.026, grin_up=0.013, grin_out=0.007, frown_dn=0.012, teeth=True),
    ),
    "LavenderWoman": dict(
        subdiv_box=dict(z=(1.04, 1.26), x=0.088, y_max=-0.115, cuts=2),
        y_cut=-0.115,
        uv_dark=(0.7002, 0.3164),
        uv_white=(0.4668, 0.5171),
        uv_tongue=(0.4810, 0.5830),
        eyes=[
            dict(xc=-0.048, rx=0.034, z_bot=1.150, z_top=1.214, z_skin=1.232, lower_frac=0.22, pupil_x=-0.042, pupil_z=1.175),
            dict(xc=+0.048, rx=0.034, z_bot=1.150, z_top=1.214, z_skin=1.232, lower_frac=0.22, pupil_x=+0.038, pupil_z=1.175),
        ],
        brows=[
            dict(xb=-0.050, rxb=0.036, zb=1.244, rzb=0.012, side=-1, lift=0.012),
            dict(xb=+0.050, rxb=0.036, zb=1.244, rzb=0.012, side=+1, lift=0.012),
        ],
        mouth=dict(xm=0.000, rxm=0.035, rx_cut=0.025, zm=1.103, curve=+0.0020, z_chin=1.048, drop=0.023, grin_up=0.011, grin_out=0.005, frown_dn=0.010, teeth=True),
    ),
    "GentleGiant": dict(
        subdiv_box=dict(z=(1.35, 1.50), x=0.072, y_max=-0.215, cuts=2),
        y_cut=-0.215,
        uv_dark=(0.2275, 0.4141),
        uv_white=(0.2153, 0.9512),
        uv_tongue=(0.7910, 0.3608),
        snap_frontal_uv=dict(u=(0.66, 0.73), v=(0.91, 0.97), x=0.045, z=(1.438, 1.480), y_max=-0.225),
        eyes=[
            dict(xc=-0.031, rx=0.018, z_bot=1.450, z_top=1.469, z_skin=1.475, lower_frac=0.35, pupil_x=-0.030, pupil_z=1.459),
            dict(xc=+0.031, rx=0.018, z_bot=1.450, z_top=1.469, z_skin=1.475, lower_frac=0.35, pupil_x=+0.028, pupil_z=1.459),
        ],
        brows=[
            dict(xb=-0.032, rxb=0.026, zb=1.483, rzb=0.011, side=-1, lift=0.011),
            dict(xb=+0.035, rxb=0.026, zb=1.483, rzb=0.011, side=+1, lift=0.011),
        ],
        # GentleGiant's wide smile crease runs from z=1.405 at x=0 out to z=1.416 at |x|=0.042.
        mouth=dict(xm=+0.002, rxm=0.050, rx_cut=0.041, zm=1.405, curve=+0.0110, z_chin=1.350, drop=0.024, grin_up=0.011, grin_out=0.006, frown_dn=0.010, teeth=True),
    ),
    "StripedChef": dict(
        subdiv_box=dict(z=(1.38, 1.58), x=0.065, y_max=-0.055, cuts=1),
        y_cut=-0.058,
        uv_dark=(0.8809, 0.0146),
        uv_white=(0.9136, 0.0669),
        uv_tongue=(0.4346, 0.8901),
        eyes=[
            dict(xc=-0.028, rx=0.021, z_bot=1.507, z_top=1.538, z_skin=1.555, lower_frac=0.20, pupil_x=-0.021, pupil_z=1.521),
            dict(xc=+0.023, rx=0.020, z_bot=1.507, z_top=1.538, z_skin=1.555, lower_frac=0.20, pupil_x=+0.020, pupil_z=1.521),
        ],
        brows=[
            dict(xb=-0.025, rxb=0.022, zb=1.565, rzb=0.011, side=-1, lift=0.011),
            dict(xb=+0.024, rxb=0.022, zb=1.565, rzb=0.011, side=+1, lift=0.011),
        ],
        # StripedChef's white upper teeth bottom arch is at z=1.4395 (center) curving up to 1.445 at corners.
        mouth=dict(xm=0.000, rxm=0.032, rx_cut=0.021, zm=1.4395, curve=+0.0055, z_chin=1.385, drop=0.023, grin_up=0.011, grin_out=0.005, frown_dn=0.010, mustache_z=(1.448, 1.474), teeth=False),
    ),
}


def seam_z(x, m):
    """Parabolic mouth parting line z_seam(x) following each character's natural smile/frown curve."""
    u = np.clip((x - m["xm"]) / max(m["rx_cut"], 1e-4), -1.5, 1.5)
    return m["zm"] + m.get("curve", 0.0) * (u ** 2)


def split_mouth_and_build_cavity(mesh_obj, spec):
    """Bisect the mouth along the parabolic curve z_seam(x), split the lip seam into upper/lower lips,
    and construct an interior 3D mouth cavity + upper teeth + tongue weighted to the Head bone.

    Returns per-vertex role arrays:
      - lip_role: 0 = regular exterior, 1 = upper lip seam, 2 = lower lip seam, 3 = mouth corner,
                  4 = cavity roof, 5 = cavity floor, 6 = cavity back, 7 = upper teeth, 8 = tongue
    """
    me = mesh_obj.data
    mw = mesh_obj.matrix_world
    mwi = mw.inverted()
    m = spec["mouth"]
    xm, zm, rx_cut = m["xm"], m["zm"], m["rx_cut"]
    curve = m.get("curve", 0.0)
    y_cut = spec["y_cut"]

    head_vg = mesh_obj.vertex_groups.get("Head")
    head_vg_idx = head_vg.index if head_vg else 0

    bm = bmesh.new()
    bm.from_mesh(me)
    bm.transform(mw)

    uv_lay = bm.loops.layers.uv.active
    dvert_lay = bm.verts.layers.deform.verify()

    # 1. Temporarily shear Z by -curve * ((x - xm) / rx_cut)^2 so the parabolic lip curve becomes a flat plane z' = zm
    for v in bm.verts:
        u = max(-1.5, min(1.5, (v.co.x - xm) / rx_cut))
        v.co.z -= curve * (u * u)

    mouth_faces = [
        f for f in bm.faces
        if f.calc_center_median().y <= y_cut + 0.005
        and abs(f.calc_center_median().x - xm) <= rx_cut * 1.35
        and abs(f.calc_center_median().z - zm) <= 0.028
    ]
    mouth_edges = {e for f in mouth_faces for e in f.edges}
    mouth_verts = {v for f in mouth_faces for v in f.verts}
    mouth_geom = list(mouth_faces) + list(mouth_edges) + list(mouth_verts)

    bmesh.ops.bisect_plane(
        bm,
        geom=mouth_geom,
        dist=1e-5,
        plane_co=Vector((xm, 0.0, zm)),
        plane_no=Vector((0.0, 0.0, 1.0)),
        clear_inner=False,
        clear_outer=False,
    )
    non_tris = [f for f in bm.faces if len(f.verts) > 3]
    if non_tris:
        bmesh.ops.triangulate(bm, faces=non_tris)

    # 2. Identify seam edges along z' = zm inside |x - xm| <= rx_cut and split them
    seam_edges = [
        e for e in bm.edges
        if abs(e.verts[0].co.z - zm) < 2e-4
        and abs(e.verts[1].co.z - zm) < 2e-4
        and e.verts[0].co.y <= y_cut
        and e.verts[1].co.y <= y_cut
        and abs(e.verts[0].co.x - xm) <= rx_cut
        and abs(e.verts[1].co.x - xm) <= rx_cut
    ]
    bmesh.ops.split_edges(bm, edges=seam_edges)

    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()

    # Classify exterior vertices near z' = zm (while still in sheared Z space) into upper lip (1), lower lip (2), or corner (3)
    roles = {}
    upper_chain = []
    lower_chain = []

    for v in bm.verts:
        if abs(v.co.z - zm) < 2e-4 and v.co.y <= y_cut and abs(v.co.x - xm) <= rx_cut + 1e-3:
            f_zs = [f.calc_center_median().z - zm for f in v.link_faces]
            if not f_zs:
                continue
            has_up = any(dz > 0 for dz in f_zs)
            has_lo = any(dz < 0 for dz in f_zs)
            if has_up and not has_lo:
                roles[v.index] = 1
                upper_chain.append(v)
            elif has_lo and not has_up:
                roles[v.index] = 2
                lower_chain.append(v)
            elif has_up and has_lo:
                if any(abs(ov.co.z - zm) < 2e-4 and len(e.link_faces) == 1 for e in v.link_edges for ov in e.verts):
                    roles[v.index] = 3
                    upper_chain.append(v)
                    lower_chain.append(v)

    # Un-shear all exterior vertices back to their true world Z coordinates
    for v in bm.verts:
        u = max(-1.5, min(1.5, (v.co.x - xm) / rx_cut))
        v.co.z += curve * (u * u)

    upper_chain.sort(key=lambda v: v.co.x)
    lower_chain.sort(key=lambda v: v.co.x)

    def make_vert(co_vec, role_id):
        v = bm.verts.new(co_vec)
        v[dvert_lay][head_vg_idx] = 1.0
        roles[v] = role_id
        return v

    def make_tri(v0, v1, v2, uv_base):
        try:
            f = bm.faces.new((v0, v1, v2))
        except ValueError:
            return
        u0, v0_uv = uv_base
        for l in f.loops:
            co_v = l.vert.co
            l[uv_lay].uv = (u0 + (co_v.x - xm) * 0.002, v0_uv + (co_v.z - zm) * 0.002)

    def make_quad(v0, v1, v2, v3, uv_base):
        make_tri(v0, v1, v2, uv_base)
        make_tri(v0, v2, v3, uv_base)

    # 3. Build the interior mouth cavity pocket + upper teeth + tongue following z_seam(x)
    # At Basis (Open = 0), keep all cavity/teeth/tongue vertices within zi +/- 0.0003m so they are 100% hidden inside the closed seam!
    K = 13
    xs = np.linspace(xm - rx_cut * 0.90, xm + rx_cut * 0.90, K)
    zs_seam = seam_z(xs, m)
    if len(upper_chain) >= 2:
        uc_x = np.array([v.co.x for v in upper_chain])
        uc_y = np.array([v.co.y for v in upper_chain])
        ys = np.interp(xs, uc_x, uc_y)
    else:
        ys = np.full(K, y_cut - 0.02)

    uv_dark = spec["uv_dark"]
    uv_white = spec["uv_white"]
    uv_tongue = spec["uv_tongue"]
    depth = 0.022

    rim_up = []
    rim_lo = []
    back_up = []
    back_lo = []
    for i in range(K):
        xi = float(xs[i])
        yi = float(ys[i]) + 0.0024
        zi = float(zs_seam[i])
        xb = xm + (xi - xm) * 0.80
        yb = yi + depth
        if i == 0 or i == K - 1:
            vc_r = make_vert(Vector((xi, yi, zi)), 3)
            vc_b = make_vert(Vector((xb, yb, zi)), 6)
            rim_up.append(vc_r)
            rim_lo.append(vc_r)
            back_up.append(vc_b)
            back_lo.append(vc_b)
        else:
            rim_up.append(make_vert(Vector((xi, yi, zi + 0.0002)), 4))
            rim_lo.append(make_vert(Vector((xi, yi, zi - 0.0002)), 5))
            back_up.append(make_vert(Vector((xb, yb, zi + 0.0003)), 4))
            back_lo.append(make_vert(Vector((xb, yb, zi - 0.0003)), 5))

    for i in range(K - 1):
        make_quad(rim_up[i], rim_up[i + 1], back_up[i + 1], back_up[i], uv_dark)
        make_quad(back_lo[i], back_lo[i + 1], rim_lo[i + 1], rim_lo[i], uv_dark)
        make_quad(back_up[i], back_up[i + 1], back_lo[i + 1], back_lo[i], uv_dark)

    # Upper teeth strip following the parabolic upper lip arch z_seam(x)
    if m.get("teeth", True):
        Kt = 11
        xt = np.linspace(xm - rx_cut * 0.78, xm + rx_cut * 0.78, Kt)
        yt = np.interp(xt, xs, ys) + 0.0030
        zt = seam_z(xt, m)
        t_top = [make_vert(Vector((float(xt[j]), float(yt[j]), float(zt[j]) + 0.0002)), 4) for j in range(Kt)]
        t_bot = [make_vert(Vector((float(xt[j]), float(yt[j]) + 0.0002, float(zt[j]) + 0.0001)), 7) for j in range(Kt)]
        t_bck = [make_vert(Vector((float(xt[j]), float(yt[j]) + 0.0020, float(zt[j]) + 0.0002)), 4) for j in range(Kt)]
        for j in range(Kt - 1):
            make_quad(t_top[j], t_top[j + 1], t_bot[j + 1], t_bot[j], uv_white)
            make_quad(t_bot[j], t_bot[j + 1], t_bck[j + 1], t_bck[j], uv_white)

    # Lower tongue dome inside the mouth floor following z_seam(x)
    Kg = 9
    xg = np.linspace(xm - rx_cut * 0.60, xm + rx_cut * 0.60, Kg)
    yg_front = np.interp(xg, xs, ys) + 0.0040
    yg_mid = yg_front + 0.0060
    yg_back = yg_front + 0.0130
    zg = seam_z(xg, m)
    g_front = [make_vert(Vector((float(xg[j]), float(yg_front[j]), float(zg[j]) - 0.0001)), 8) for j in range(Kg)]
    g_mid = [make_vert(Vector((float(xg[j]) * 0.92 + xm * 0.08, float(yg_mid[j]), float(zg[j]))), 8) for j in range(Kg)]
    g_back = [make_vert(Vector((float(xg[j]) * 0.82 + xm * 0.18, float(yg_back[j]), float(zg[j]) - 0.0001)), 8) for j in range(Kg)]
    for j in range(Kg - 1):
        make_quad(g_back[j], g_back[j + 1], g_mid[j + 1], g_mid[j], uv_tongue)
        make_quad(g_mid[j], g_mid[j + 1], g_front[j + 1], g_front[j], uv_tongue)

    bm.verts.ensure_lookup_table()
    role_arr = np.zeros(len(bm.verts), dtype=np.int32)
    for idx, v in enumerate(bm.verts):
        if v in roles:
            role_arr[idx] = roles[v]
        elif idx in roles:
            role_arr[idx] = roles[idx]

    bm.transform(mwi)
    bm.to_mesh(me)
    bm.free()
    return role_arr


def add_facial_blendshapes(name, mesh_obj):
    if name not in FACE_SPECS:
        return
    spec = FACE_SPECS[name]
    me = mesh_obj.data

    bpy.ops.object.select_all(action="DESELECT")
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = mesh_obj

    if me.shape_keys:
        mesh_obj.shape_key_clear()

    # 1. Weld UV-seam split vertices and repair any torn eye triangles that belong to the frontal face UV island
    bm = bmesh.new()
    bm.from_mesh(me)
    if "snap_frontal_uv" in spec:
        sf = spec["snap_frontal_uv"]
        mw = mesh_obj.matrix_world
        bm.transform(mw)
        uv_lay = bm.loops.layers.uv.active
        main_uvs = {}
        for f in bm.faces:
            for l in f.loops:
                uv = l[uv_lay].uv
                if sf["u"][0] <= uv.x <= sf["u"][1] and sf["v"][0] <= uv.y <= sf["v"][1]:
                    key = (round(l.vert.co.x, 4), round(l.vert.co.y, 4), round(l.vert.co.z, 4))
                    main_uvs[key] = (uv.x, uv.y)
        for f in bm.faces:
            c = f.calc_center_median()
            if c.y <= sf["y_max"] and abs(c.x) <= sf["x"] and sf["z"][0] <= c.z <= sf["z"][1]:
                keys = [(round(l.vert.co.x, 4), round(l.vert.co.y, 4), round(l.vert.co.z, 4)) for l in f.loops]
                if all(k in main_uvs for k in keys):
                    for l, k in zip(f.loops, keys):
                        l[uv_lay].uv = main_uvs[k]
        bm.transform(mw.inverted())
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()

    # 2. Selectively subdivide the front face polygons so eyelids and lips deform smoothly
    sb = spec.get("subdiv_box")
    if sb:
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="DESELECT")
        bpy.ops.object.mode_set(mode="OBJECT")
        mw = mesh_obj.matrix_world
        for poly in me.polygons:
            c = mw @ poly.center
            if sb["z"][0] <= c.z <= sb["z"][1] and abs(c.x) <= sb["x"] and c.y <= sb["y_max"]:
                poly.select = True
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.subdivide(number_cuts=sb["cuts"], smoothness=0.0)
        bpy.ops.mesh.quads_convert_to_tris()
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode="OBJECT")

    # 3. Cut the parabolic mouth parting seam and construct the 3D interior mouth cavity, upper teeth, and tongue
    role_arr = split_mouth_and_build_cavity(mesh_obj, spec)

    if me.has_custom_normals:
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    me.shade_smooth()

    mw = mesh_obj.matrix_world
    mwi = mw.inverted()
    co = np.array([mw @ v.co for v in me.vertices], dtype=np.float64)
    x, y, z = co[:, 0], co[:, 1], co[:, 2]
    n_verts = len(co)

    is_interior = role_arr >= 4
    w_front = smooth01(spec["y_cut"] + 0.015, spec["y_cut"] - 0.010, y)
    w_front_ext = w_front * (~is_interior)
    w_no_glasses = np.ones(n_verts, dtype=np.float64)
    if "glasses_z" in spec:
        gz0, gz1 = spec["glasses_z"]
        gy0, gy1 = spec["glasses_y"]
        in_gz = (z >= gz0) & (z <= gz1)
        w_no_glasses[in_gz] = smooth01(gy0, gy1, y[in_gz])

    mesh_obj.shape_key_add(name="Basis", from_mix=False)

    def write_shape(key_name, delta_world):
        sk = mesh_obj.shape_key_add(name=key_name, from_mix=False)
        target_world = co + delta_world
        rot_i = np.array(mwi.to_3x3(), dtype=np.float64)
        trans_i = np.array(mwi.translation, dtype=np.float64)
        target_local = target_world @ rot_i.T + trans_i
        sk.data.foreach_set("co", target_local.astype(np.float32).ravel())

    # --- BLINK ---
    d_blink = np.zeros_like(co)
    for eye in spec["eyes"]:
        xc, rx = eye["xc"], eye["rx"]
        zb, zt, zs = eye["z_bot"], eye["z_top"], eye["z_skin"]
        lf = eye["lower_frac"]
        dx_norm = np.abs(x - xc) / rx
        wx = smooth01(1.38, 0.85, dx_norm) * w_front_ext * w_no_glasses
        z_closed = zb + lf * (zt - zb)
        eye_h = zt - zb

        m_open = (z >= zb) & (z <= zt) & (wx > 0)
        u = (z[m_open] - zb) / eye_h
        z_new = z_closed + (u - lf) * 0.02 * eye_h
        d_blink[m_open, 2] += (z_new - z[m_open]) * wx[m_open]
        d_blink[m_open, 1] += 0.0012 * np.sin(u * math.pi) * wx[m_open]

        dz_top = (z_closed + (1.0 - lf) * 0.02 * eye_h) - zt
        m_up = (z > zt) & (z <= zs) & (wx > 0)
        v = (z[m_up] - zt) / (zs - zt)
        s = smooth01(1.0, 0.0, v)
        d_blink[m_up, 2] += dz_top * s * wx[m_up]
        d_blink[m_up, 1] += -0.0015 * np.sin(v * math.pi) * wx[m_up]

        dz_bot = (z_closed - lf * 0.02 * eye_h) - zb
        zb_low = zb - 0.75 * eye_h
        m_lo = (z >= zb_low) & (z < zb) & (wx > 0)
        vb = (zb - z[m_lo]) / (zb - zb_low)
        sb_w = smooth01(1.0, 0.0, vb)
        d_blink[m_lo, 2] += dz_bot * sb_w * wx[m_lo]
        d_blink[m_lo, 1] += -0.0012 * np.sin(vb * math.pi) * wx[m_lo]

    write_shape("Blink", d_blink)

    # --- EYE WIDE ---
    d_wide = np.zeros_like(co)
    for eye in spec["eyes"]:
        xc, rx = eye["xc"], eye["rx"]
        zb, zt = eye["z_bot"], eye["z_top"]
        eye_h = zt - zb
        wx = smooth01(1.05, 0.45, np.abs(x - xc) / rx) * w_front_ext * w_no_glasses
        w_up = np.exp(-((z - zt) / (0.32 * eye_h)) ** 2)
        w_lo = np.exp(-((z - zb) / (0.28 * eye_h)) ** 2)
        d_wide[:, 2] += (0.09 * eye_h * w_up - 0.06 * eye_h * w_lo) * wx
    write_shape("EyeWide", d_wide)

    # --- GAZE (GazeLeft, GazeRight, GazeUp, GazeDown) ---
    d_gl = np.zeros_like(co)
    d_gr = np.zeros_like(co)
    d_gu = np.zeros_like(co)
    d_gd = np.zeros_like(co)
    for eye in spec["eyes"]:
        px_c = eye["pupil_x"]
        pz_c = eye["pupil_z"]
        rx = eye["rx"] * 0.72
        rz = (eye["z_top"] - eye["z_bot"]) * 0.55
        r2 = ((x - px_c) / rx) ** 2 + ((z - pz_c) / rz) ** 2
        w_iris = (np.clip(1.0 - r2, 0.0, 1.0) ** 1.6) * w_front_ext * w_no_glasses
        shift_x = 0.28 * rx * w_iris
        shift_z = 0.28 * rz * w_iris
        d_gl[:, 0] -= shift_x
        d_gr[:, 0] += shift_x
        d_gu[:, 2] += shift_z
        d_gd[:, 2] -= shift_z
    write_shape("GazeLeft", d_gl)
    write_shape("GazeRight", d_gr)
    write_shape("GazeUp", d_gu)
    write_shape("GazeDown", d_gd)

    # --- BROWS (BrowUp, BrowKnit, BrowSad) ---
    d_bup = np.zeros_like(co)
    d_bknit = np.zeros_like(co)
    d_bsad = np.zeros_like(co)
    for brow in spec["brows"]:
        xb, rxb = brow["xb"], brow["rxb"]
        zb, rzb = brow["zb"], brow["rzb"]
        side = brow["side"]
        lift = brow["lift"]
        u_in = ((x - xb) / rxb) * side
        wx = np.clip(1.0 - ((x - xb) / rxb) ** 2, 0.0, 1.0) ** 1.2 * w_front_ext
        wz = np.exp(-((z - zb) / rzb) ** 2)
        wb = wx * wz

        d_bup[:, 2] += lift * wb

        knit_z = np.clip(0.65 - 0.85 * u_in, -0.25, 1.35)
        knit_x = np.clip(0.8 - 0.6 * u_in, 0.0, 1.2)
        d_bknit[:, 2] -= lift * 0.90 * knit_z * wb
        d_bknit[:, 0] -= side * (lift * 0.40) * knit_x * wb

        sad_z = np.clip(0.35 - 1.15 * u_in, -1.0, 0.9)
        d_bsad[:, 2] += lift * 0.85 * sad_z * wb

    write_shape("BrowUp", d_bup)
    write_shape("BrowKnit", d_bknit)
    write_shape("BrowSad", d_bsad)

    # --- MOUTH (Open, Chew, Grin, Frown) ---
    m = spec["mouth"]
    xm, rxm, rx_cut, zm, z_chin = m["xm"], m["rxm"], m["rx_cut"], m["zm"], m["z_chin"]
    drop = m["drop"]
    z_seam_v = seam_z(x, m)
    dz_seam = z - z_seam_v

    d_open = np.zeros_like(co)
    arch_cut = np.clip(1.0 - ((x - xm) / (rx_cut * 1.02)) ** 2, 0.0, 1.0) ** 1.35
    wx_jaw = np.clip(1.0 - ((x - xm) / (rxm * 1.55)) ** 2, 0.0, 1.0) ** 1.15 * w_front

    # Lower jaw & lower lip (exterior vertices below z_seam_v, or lower lip seam role==2)
    is_lower_ext = (~is_interior) & ((role_arr == 2) | ((dz_seam < -1e-4) & (role_arr != 1) & (role_arr != 3))) & (z >= z_chin - 0.030)
    u_jaw = np.clip((z[is_lower_ext] - (z_chin - 0.030)) / (z_seam_v[is_lower_ext] - (z_chin - 0.030)), 0.0, 1.0)
    w_jaw_z = smooth01(0.0, 0.72, u_jaw)
    w_lip_arch = (u_jaw ** 2) * arch_cut[is_lower_ext] * w_front[is_lower_ext] + (1.0 - u_jaw ** 2) * (wx_jaw[is_lower_ext] * 0.65)
    d_open[is_lower_ext, 2] -= drop * w_jaw_z * w_lip_arch
    d_open[is_lower_ext, 1] += drop * 0.28 * w_jaw_z * w_lip_arch

    # Upper lip (exterior vertices at role==1 or just above z_seam_v) lifts slightly to expose upper teeth
    is_upper_ext = (~is_interior) & ((role_arr == 1) | ((dz_seam > 1e-4) & (dz_seam <= 0.020) & (role_arr != 2) & (role_arr != 3)))
    u_ulip = np.exp(-((np.maximum(0.0, dz_seam[is_upper_ext])) / 0.009) ** 2)
    d_open[is_upper_ext, 2] += drop * 0.22 * u_ulip * arch_cut[is_upper_ext] * w_front[is_upper_ext]
    d_open[is_upper_ext, 1] -= 0.0010 * u_ulip * arch_cut[is_upper_ext] * w_front[is_upper_ext]

    # Mouth corners (role == 3)
    is_corner = role_arr == 3
    d_open[is_corner, 2] -= drop * 0.10 * arch_cut[is_corner]
    d_open[is_corner, 0] -= np.sign(x[is_corner] - xm) * 0.0015

    if "mustache_z" in m:
        mz0, mz1 = m["mustache_z"]
        m_mus = (~is_interior) & (z >= mz0) & (z <= mz1)
        wx_mus = np.clip(1.0 - ((x - xm) / (rxm * 1.35)) ** 2, 0.0, 1.0) * w_front
        d_open[m_mus, 2] += drop * 0.16 * wx_mus[m_mus]
        d_open[m_mus, 1] -= 0.0015 * wx_mus[m_mus]

    # Strict interior ordering multiplied by arch_cut(x) so cavity/teeth/tongue NEVER exceed exterior lips:
    # +0.22 (Ext Upper Lip) > +0.19 (Cavity Roof) > -0.04 (Upper Teeth) > -0.70 (Tongue) > -0.90 (Cavity Floor) > -1.00 (Ext Lower Lip)
    m_r4 = role_arr == 4
    d_open[m_r4, 2] += drop * 0.19 * arch_cut[m_r4]
    m_r5 = role_arr == 5
    d_open[m_r5, 2] -= drop * 0.90 * arch_cut[m_r5]
    d_open[m_r5, 1] += drop * 0.26 * arch_cut[m_r5]
    m_r7 = role_arr == 7
    d_open[m_r7, 2] -= drop * 0.04 * arch_cut[m_r7]
    m_r8 = role_arr == 8
    d_open[m_r8, 2] -= drop * 0.70 * arch_cut[m_r8]
    d_open[m_r8, 1] += drop * 0.22 * arch_cut[m_r8]

    write_shape("Open", d_open)

    # Interior vertices share the exact same horizontal & vertical lip-curve displacements in Chew, Grin, and Frown!
    w_mouth_seam = np.where(is_interior, 1.0, w_front)

    # --- CHEW ---
    d_chew = np.zeros_like(co)
    for side_s in (-1.0, +1.0):
        x_chk = xm + side_s * (rxm * 1.15)
        z_chk = zm + 0.006
        w_chk = (
            np.exp(-((x - x_chk) / (rxm * 0.75)) ** 2)
            * np.exp(-((z - z_chk) / 0.026) ** 2)
            * w_front_ext
        )
        d_chew[:, 0] += side_s * 0.0060 * w_chk
        d_chew[:, 1] -= 0.0050 * w_chk
        d_chew[:, 2] += 0.0035 * w_chk

    w_mouth_zone = (
        np.exp(-((x - xm) / (rxm * 1.25)) ** 2)
        * np.exp(-((dz_seam + 0.006) / 0.030) ** 2)
        * w_mouth_seam
    )
    d_chew[:, 0] += 0.0050 * w_mouth_zone
    d_chew[:, 1] -= 0.0030 * w_mouth_zone * (~is_interior)
    write_shape("Chew", d_chew)

    # --- GRIN ---
    d_grin = np.zeros_like(co)
    dx_m = (x - xm) / rxm
    sgn_x = np.sign(dx_m)
    w_corner_x = np.exp(-((np.abs(dx_m) - 0.80) / 0.45) ** 2) * smooth01(0.10, 0.55, np.abs(dx_m))
    w_corner_z = np.exp(-(dz_seam / 0.018) ** 2)
    w_corner = w_corner_x * w_corner_z * w_mouth_seam
    d_grin[:, 2] += m["grin_up"] * w_corner
    d_grin[:, 0] += sgn_x * m["grin_out"] * w_corner
    d_grin[:, 1] += 0.003 * w_corner * (~is_interior)

    w_center = np.clip(1.0 - dx_m ** 2, 0.0, 1.0) * np.exp(-(dz_seam / 0.015) ** 2) * w_mouth_seam
    d_grin[:, 2] += m["grin_up"] * 0.22 * (dx_m ** 2) * w_center

    for eye in spec["eyes"]:
        xc, rx = eye["xc"], eye["rx"]
        z_cheek = 0.45 * zm + 0.55 * eye["z_bot"]
        w_chk = np.clip(1.0 - ((x - xc) / (rx * 1.20)) ** 2, 0.0, 1.0) * np.exp(-((z - z_cheek) / 0.018) ** 2) * w_front_ext
        d_grin[:, 2] += m["grin_up"] * 0.25 * w_chk
        d_grin[:, 1] -= 0.0020 * w_chk
    write_shape("Grin", d_grin)

    # --- FROWN ---
    d_frown = np.zeros_like(co)
    d_frown[:, 2] -= m["frown_dn"] * w_corner
    d_frown[:, 1] += 0.0025 * w_corner * (~is_interior)
    write_shape("Frown", d_frown)

    n_split_up = int(np.sum(role_arr == 1))
    n_split_lo = int(np.sum(role_arr == 2))
    print(f"[faces] {name}: {len(me.vertices)} verts (lip split {n_split_up}/{n_split_lo}), {len(me.shape_keys.key_blocks) - 1} blendshapes", flush=True)


def render_preview(name, mesh_obj, preview_dir):
    os.makedirs(preview_dir, exist_ok=True)
    mat = mesh_obj.data.materials[0]
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    for n in list(nodes):
        if n.type in ("TEX_IMAGE", "NORMAL_MAP"):
            nodes.remove(n)
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    tex_node = nodes.new("ShaderNodeTexImage")
    tex_node.image = bpy.data.images.load(os.path.join(MESHY_DIR, name, name + "_Full_BaseColor.png"))
    links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])
    norm_path = os.path.join(MESHY_DIR, name, name + "_Full_Normal.png")
    if os.path.exists(norm_path):
        n_tex = nodes.new("ShaderNodeTexImage")
        n_tex.image = bpy.data.images.load(norm_path)
        n_tex.image.colorspace_settings.name = "Non-Color"
        n_map = nodes.new("ShaderNodeNormalMap")
        links.new(n_tex.outputs["Color"], n_map.inputs["Color"])
        links.new(n_map.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.85

    scene = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    scene.render.engine = next(e for e in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT") if e in engines)
    scene.render.resolution_x = 320
    scene.render.resolution_y = 360
    if not scene.world:
        scene.world = bpy.data.worlds.new("w")
    bg = scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.16, 0.17, 0.20, 1.0)
        bg.inputs[1].default_value = 0.9

    spec = FACE_SPECS[name]
    z_mid = 0.5 * (spec["mouth"]["zm"] + spec["brows"][0]["zb"])
    x_mid = spec["mouth"]["xm"]
    span = (spec["brows"][0]["zb"] - spec["mouth"]["z_chin"]) * 2.15

    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = span
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = Vector((x_mid, -2.0, z_mid))
    cam.rotation_euler = Vector((0, 1, 0)).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.2
    sun.rotation_euler = Vector((0.2, 0.85, -0.35)).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(sun)

    scene_poses = [
        ("Neutral", {}),
        ("Bite (Open)", {"Open": 1.0, "BrowUp": 0.45}),
        ("Chew (Kunyah)", {"Chew": 1.0, "Open": 0.28, "Grin": 0.35, "BrowUp": 0.2}),
        ("Yummy (Good)", {"Grin": 1.0, "Open": 0.65, "BrowUp": 0.55}),
        ("Yuck (Bad)", {"Frown": 1.0, "BrowKnit": 1.0, "Open": 0.35}),
        ("Shocked", {"Open": 1.0, "EyeWide": 0.85, "BrowUp": 1.0}),
    ]
    keys = mesh_obj.data.shape_keys.key_blocks
    for idx, (_, weights) in enumerate(scene_poses):
        for kb in keys:
            if kb.name != "Basis":
                kb.value = weights.get(kb.name, 0.0)
        bpy.context.view_layer.update()
        scene.render.filepath = os.path.join(preview_dir, f"expr_{name}_{idx}.png")
        bpy.ops.render.render(write_still=True)
    for kb in keys:
        if kb.name != "Basis":
            kb.value = 0.0
    bpy.data.objects.remove(cam)
    bpy.data.objects.remove(sun)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--only")
    ap.add_argument("--preview")
    args = ap.parse_args(argv)

    for name in FACE_SPECS:
        if args.only and name not in args.only.split(","):
            continue
        fbx_path = os.path.join(MODELS_DIR, name + ".fbx")
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=fbx_path)
        arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
        mesh_obj = next(o for o in bpy.data.objects if o.type == "MESH")
        orig_mesh_data_name = mesh_obj.data.name

        add_facial_blendshapes(name, mesh_obj)

        arm.name = name
        arm.data.name = name + "_Rig"
        mesh_obj.name = name + "Mesh"
        mesh_obj.data.name = orig_mesh_data_name

        bpy.ops.object.select_all(action="DESELECT")
        arm.select_set(True)
        mesh_obj.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.export_scene.fbx(
            filepath=fbx_path, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
            bake_anim=False, use_armature_deform_only=False, mesh_smooth_type="FACE", use_mesh_modifiers=False,
            use_custom_props=False, primary_bone_axis="Y", secondary_bone_axis="X", path_mode="STRIP")
        print(f"[faces] exported {fbx_path}", flush=True)

        if args.preview:
            render_preview(name, mesh_obj, args.preview)


if __name__ == "__main__":
    main()
