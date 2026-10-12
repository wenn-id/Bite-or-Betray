"""
Generator karakter Bite or Betray untuk Blender 5.1.

Setiap karakter disusun dari bentuk SDF (elipsoid, kapsul, kotak bulat) yang dilebur halus, lalu
dipoligon dengan surface nets, di-decimate, dan di-skin ke rig yang dibaca CharacterAnimator di Unity:

    Hips > Spine > Head
                 > Arm_Left > Forearm_Left > Hand_Left   (sisi kanan sama)

Wajah memakai mesh kaku yang menempel di tulang Head: EyeWhite_*, Pupil_*, Lid_* (kelopak), Brow_* (alis),
Nose, dan Mouth (blend shape Open, Grin, Frown). Baju ikut di-skin ke badan; topi, sarung tangan, dan
sepatu menempel kaku di tulangnya.

Koordinat Blender: Z ke atas, karakter menghadap -Y, "Left" = sisi -X (sama dengan model lama).

Pakai:
    blender -b --python Tools/Blender/bob_characters.py -- [--only Bot_Red,Waiter] [--no-export]
        [--preview out.png] [--pose-test]
"""
import json
import math
import os
import sys
import time

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(HERE, "..", ".."))
ART = os.path.join(PROJECT, "Assets", "BiteOrBetray", "Art3D")
MODELS = os.path.join(ART, "Models")

UP = np.array([0., 0., 1.])
FWD = np.array([0., -1., 0.])
SIDES = ((-1, "Left", "L"), (1, "Right", "R"))
# Skala jumlah segitiga hasil decimate (anggaran VR standalone).
TRI_SCALE = .72


def vec(*a):
    return np.array(a, float)


def unit(v):
    v = np.asarray(v, float)
    return v / np.linalg.norm(v)


def rot_x(deg):
    a = math.radians(deg)
    return np.array([[1, 0, 0], [0, math.cos(a), -math.sin(a)], [0, math.sin(a), math.cos(a)]])


def rot_y(deg):
    a = math.radians(deg)
    return np.array([[math.cos(a), 0, math.sin(a)], [0, 1, 0], [-math.sin(a), 0, math.cos(a)]])


def rot_z(deg):
    a = math.radians(deg)
    return np.array([[math.cos(a), -math.sin(a), 0], [math.sin(a), math.cos(a), 0], [0, 0, 1]])


def frame(axis, hint=UP):
    """Matriks rotasi yang kolom z-nya = axis."""
    z = unit(axis)
    x = np.cross(hint, z)
    if np.linalg.norm(x) < 1e-6:
        x = np.cross(vec(1, 0, 0), z)
    x = unit(x)
    return np.stack([x, np.cross(z, x), z], 1)


# ---------------------------------------------------------------------------------------------------
# Primitif SDF (semua vektorial: P berbentuk (N, 3))

def _norm(a):
    return np.sqrt(np.einsum("ij,ij->i", a, a))


def _local(P, c, R):
    Q = P - c
    return Q if R is None else Q @ R


class Prim:
    def bbox(self):
        raise NotImplementedError


class Ellipsoid(Prim):
    def __init__(self, c, r, R=None):
        self.c, self.r, self.R = np.asarray(c, float), np.asarray(r, float), R

    def __call__(self, P):
        q = _local(P, self.c, self.R) / self.r
        k0 = _norm(q)
        k1 = _norm(q / self.r)
        return k0 * (k0 - 1) / np.maximum(k1, 1e-9)

    def bbox(self):
        m = self.r.max()
        return self.c - m, self.c + m


def Sphere(c, r):
    return Ellipsoid(c, (r, r, r))


class Cone(Prim):
    """Kapsul meruncing dari a (jari-jari ra) ke b (jari-jari rb)."""

    def __init__(self, a, b, ra, rb=None):
        self.a, self.b = np.asarray(a, float), np.asarray(b, float)
        self.ra, self.rb = ra, ra if rb is None else rb

    def __call__(self, P):
        a, b, r1, r2 = self.a, self.b, self.ra, self.rb
        ba = b - a
        l2 = ba @ ba
        rr = r1 - r2
        a2 = l2 - rr * rr
        il2 = 1.0 / l2
        pa = P - a
        y = pa @ ba
        z = y - l2
        w = pa * l2 - np.outer(y, ba)
        x2 = np.einsum("ij,ij->i", w, w)
        y2 = y * y * l2
        z2 = z * z * l2
        k = np.sign(rr) * rr * rr * x2
        d_tip = np.sqrt(x2 + z2) * il2 - r2
        d_base = np.sqrt(x2 + y2) * il2 - r1
        d_side = (np.sqrt(np.maximum(x2 * a2 * il2, 0)) + y * rr) * il2 - r1
        return np.where(np.sign(z) * a2 * z2 > k, d_tip, np.where(np.sign(y) * a2 * y2 < k, d_base, d_side))

    def bbox(self):
        r = max(self.ra, self.rb)
        return np.minimum(self.a, self.b) - r, np.maximum(self.a, self.b) + r


class RBox(Prim):
    def __init__(self, c, half, rad, R=None):
        self.c, self.half, self.rad, self.R = np.asarray(c, float), np.asarray(half, float), rad, R

    def __call__(self, P):
        q = np.abs(_local(P, self.c, self.R)) - self.half + self.rad
        return _norm(np.maximum(q, 0)) + np.minimum(q.max(1), 0) - self.rad

    def bbox(self):
        m = np.linalg.norm(self.half)
        return self.c - m, self.c + m


class Torus(Prim):
    """Torus dengan sumbu = kolom z dari R."""

    def __init__(self, c, R_major, r_minor, R=None, squash=1.0):
        self.c, self.Rm, self.rm, self.R, self.squash = np.asarray(c, float), R_major, r_minor, R, squash

    def __call__(self, P):
        q = _local(P, self.c, self.R)
        qx = np.sqrt(q[:, 0] ** 2 + q[:, 1] ** 2) - self.Rm
        return np.sqrt(qx ** 2 + (q[:, 2] / self.squash) ** 2) * min(1, self.squash) - self.rm

    def bbox(self):
        m = self.Rm + self.rm
        return self.c - m, self.c + m


class Cyl(Prim):
    """Silinder bulat: sumbu z lokal, jari-jari r, setengah tinggi h, pembulatan rad."""

    def __init__(self, c, r, h, rad, R=None):
        self.c, self.r, self.h, self.rad, self.R = np.asarray(c, float), r, h, rad, R

    def __call__(self, P):
        q = _local(P, self.c, self.R)
        dx = np.sqrt(q[:, 0] ** 2 + q[:, 1] ** 2) - self.r + self.rad
        dz = np.abs(q[:, 2]) - self.h + self.rad
        return np.sqrt(np.maximum(dx, 0) ** 2 + np.maximum(dz, 0) ** 2) + np.minimum(np.maximum(dx, dz), 0) - self.rad

    def bbox(self):
        m = math.hypot(self.r, self.h)
        return self.c - m, self.c + m


class Wavy(Prim):
    """Bidang yang tepinya bergelombang mengitari sumbu vertikal di axis_xy."""

    def __init__(self, p, n, axis_xy, amp, waves):
        self.p, self.n, self.axis, self.amp, self.waves = np.asarray(p, float), unit(n), axis_xy, amp, waves

    def __call__(self, P):
        a = np.arctan2(P[:, 0] - self.axis[0], P[:, 1] - self.axis[1])
        return (P - self.p) @ self.n + self.amp * np.cos(a * self.waves)

    def bbox(self):
        return np.full(3, -9.), np.full(3, 9.)


class Plane(Prim):
    """Setengah ruang: bagian dalam (negatif) ada di sisi berlawanan normal n."""

    def __init__(self, p, n):
        self.p, self.n = np.asarray(p, float), unit(n)

    def __call__(self, P):
        return (P - self.p) @ self.n

    def bbox(self):
        return np.full(3, -9.), np.full(3, 9.)


def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(.5 + .5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


def smax(a, b, k):
    return -smin(-a, -b, k)


class Part:
    def __init__(self, prim, bone=None, k=0.0, op="add"):
        self.prim, self.bone, self.k, self.op = prim, bone, k, op


class Shape:
    """Gabungan bagian SDF. bone: nama tulang, atau (bawah, atas, z0, z1) untuk peralihan menurut tinggi."""

    def __init__(self, dilate=0.0, displace=None):
        self.parts, self.dilate, self.displace = [], dilate, displace

    def add(self, prim, bone=None, k=0.0):
        self.parts.append(Part(prim, bone, k, "add"))
        return self

    def sub(self, prim, k=0.0):
        self.parts.append(Part(prim, None, k, "sub"))
        return self

    def inter(self, prim, k=0.0):
        self.parts.append(Part(prim, None, k, "int"))
        return self

    def extend(self, parts):
        self.parts.extend(parts)
        return self

    def __call__(self, P):
        d = None
        for p in self.parts:
            v = p.prim(P)
            if d is None:
                d = v
            elif p.op == "add":
                d = smin(d, v, p.k)
            elif p.op == "sub":
                d = smax(d, -v, p.k)
            else:
                d = smax(d, v, p.k)
        d = d - self.dilate
        if self.displace is not None:
            d = d + self.displace(P)
        return d

    def bbox(self):
        lo, hi = np.full(3, 1e9), np.full(3, -1e9)
        for p in self.parts:
            if p.op == "add":
                a, b = p.prim.bbox()
                lo, hi = np.minimum(lo, a), np.maximum(hi, b)
        for p in self.parts:
            if p.op == "int" and not isinstance(p.prim, (Plane, Wavy)):
                a, b = p.prim.bbox()
                lo, hi = np.maximum(lo, a), np.minimum(hi, b)
        return lo - self.dilate - .01, hi + self.dilate + .01


# ---------------------------------------------------------------------------------------------------
# Poligonisasi

def grad(f, P, e=5e-4):
    g = np.empty_like(P)
    for i in range(3):
        d = np.zeros(3)
        d[i] = e
        g[:, i] = f(P + d) - f(P - d)
    return g / (2 * e)


def surface_nets(f, lo, hi, h):
    n = np.ceil((hi - lo) / h).astype(int) + 1
    axes = [lo[i] + np.arange(n[i]) * h for i in range(3)]
    G = np.stack(np.meshgrid(*axes, indexing="ij"), -1).reshape(-1, 3)
    F = np.empty(len(G), np.float32)
    step = 2_000_000
    for s in range(0, len(G), step):
        F[s:s + step] = f(G[s:s + step])
    del G
    F = F.reshape(n)
    offs = [(i, j, k) for i in (0, 1) for j in (0, 1) for k in (0, 1)]
    nc = n - 1
    corner = [F[i:i + nc[0], j:j + nc[1], k:k + nc[2]] for (i, j, k) in offs]
    any_in = np.zeros(nc, bool)
    all_in = np.ones(nc, bool)
    for c in corner:
        any_in |= c < 0
        all_in &= c < 0
    active = any_in & ~all_in
    idx = np.argwhere(active)
    cv = np.stack([c[active] for c in corner], 1)
    O = np.array(offs, float)
    acc = np.zeros((len(idx), 3))
    cnt = np.zeros(len(idx))
    for a in range(8):
        for b in range(a + 1, 8):
            if bin(a ^ b).count("1") != 1:
                continue
            fa, fb = cv[:, a], cv[:, b]
            m = (fa < 0) != (fb < 0)
            t = fa[m] / (fa[m] - fb[m])
            acc[m] += O[a] + t[:, None] * (O[b] - O[a])
            cnt[m] += 1
    V = lo + (idx + acc / cnt[:, None]) * h
    vid = -np.ones(nc, np.int64)
    vid[active] = np.arange(len(idx))
    neg = F < 0
    quads = []
    i, j, k = np.nonzero(neg[:-1, 1:-1, 1:-1] != neg[1:, 1:-1, 1:-1])
    j, k = j + 1, k + 1
    quads.append(np.stack([vid[i, j - 1, k - 1], vid[i, j, k - 1], vid[i, j, k], vid[i, j - 1, k]], 1))
    i, j, k = np.nonzero(neg[1:-1, :-1, 1:-1] != neg[1:-1, 1:, 1:-1])
    i, k = i + 1, k + 1
    quads.append(np.stack([vid[i - 1, j, k - 1], vid[i, j, k - 1], vid[i, j, k], vid[i - 1, j, k]], 1))
    i, j, k = np.nonzero(neg[1:-1, 1:-1, :-1] != neg[1:-1, 1:-1, 1:])
    i, j = i + 1, j + 1
    quads.append(np.stack([vid[i - 1, j - 1, k], vid[i, j - 1, k], vid[i, j, k], vid[i - 1, j, k]], 1))
    return V, np.concatenate(quads)


def project(f, V, iterations=3):
    for _ in range(iterations):
        d = f(V)
        g = grad(f, V)
        V = V - (d / np.maximum(np.einsum("ij,ij->i", g, g), 1e-9))[:, None] * g
    return V


def orient(f, V, Q):
    """Balik quad yang normalnya berlawanan dengan gradien SDF."""
    n = np.cross(V[Q[:, 2]] - V[Q[:, 0]], V[Q[:, 3]] - V[Q[:, 1]])
    c = V[Q].mean(1)
    flip = np.einsum("ij,ij->i", n, grad(f, c)) < 0
    Q = Q.copy()
    Q[flip] = Q[flip][:, ::-1]
    return Q


def decimate(V, F, tris, symmetric=True):
    me = bpy.data.meshes.new("tmp")
    me.from_pydata(V.tolist(), [], F.tolist())
    ob = bpy.data.objects.new("tmp", me)
    bpy.context.scene.collection.objects.link(ob)
    current = sum(len(f) - 2 for f in F) if F.dtype == object else F.shape[0] * (F.shape[1] - 2)
    if tris and current > tris:
        mod = ob.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = tris / current
        mod.use_symmetry = symmetric
        mod.symmetry_axis = "X"
    mod = ob.modifiers.new("Tri", "TRIANGULATE")
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    m = ev.to_mesh()
    m.calc_loop_triangles()
    V2 = np.empty(len(m.vertices) * 3)
    m.vertices.foreach_get("co", V2)
    T = np.empty(len(m.loop_triangles) * 3, np.int32)
    m.loop_triangles.foreach_get("vertices", T)
    ev.to_mesh_clear()
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    return V2.reshape(-1, 3), T.reshape(-1, 3)


def polygonize(shape, h, tris, symmetric=True):
    t0 = time.time()
    lo, hi = shape.bbox()
    V, Q = surface_nets(shape, lo - 2 * h, hi + 2 * h, h)
    if len(Q) == 0:
        raise RuntimeError("Bentuk kosong")
    V = project(shape, V)
    Q = orient(shape, V, Q)
    V, T = decimate(V, Q, int(tris * TRI_SCALE), symmetric)
    log(f"    poligon {len(Q)} quad -> {len(T)} tri ({time.time() - t0:.1f}s)")
    return V, T


# ---------------------------------------------------------------------------------------------------
# Mesh langsung (bola, tudung)

def cap(center, axis, radius, theta_max, rings, segs, thickness=0.0, hint=UP):
    """Tudung bola di sekitar axis sampai sudut theta_max (pi = bola penuh)."""
    R = frame(axis, hint)
    full = theta_max >= math.pi - 1e-6
    verts, faces = [], []

    def ring_layer(r):
        base = len(verts)
        verts.append(center + R[:, 2] * r)
        n_rings = rings - 1 if full else rings
        for i in range(1, n_rings + 1):
            th = theta_max * i / rings
            for j in range(segs):
                ph = 2 * math.pi * j / segs
                d = R[:, 0] * math.sin(th) * math.cos(ph) + R[:, 1] * math.sin(th) * math.sin(ph) + R[:, 2] * math.cos(th)
                verts.append(center + d * r)
        for j in range(segs):
            faces.append((base, base + 1 + j, base + 1 + (j + 1) % segs))
        for i in range(n_rings - 1):
            a, b = base + 1 + i * segs, base + 1 + (i + 1) * segs
            for j in range(segs):
                j2 = (j + 1) % segs
                faces.append((a + j, b + j, b + j2, a + j2))
        if full:
            verts.append(center - R[:, 2] * r)
            last, tip = base + 1 + (n_rings - 1) * segs, len(verts) - 1
            for j in range(segs):
                faces.append((last + j, tip, last + (j + 1) % segs))
        return base, n_rings

    base_o, n = ring_layer(radius)
    if thickness > 0 and not full:
        nf = len(faces)
        base_i, _ = ring_layer(radius - thickness)
        for fi in range(nf, len(faces)):
            faces[fi] = tuple(reversed(faces[fi]))
        ro, ri = base_o + 1 + (n - 1) * segs, base_i + 1 + (n - 1) * segs
        for j in range(segs):
            j2 = (j + 1) % segs
            faces.append((ro + j, ri + j, ri + j2, ro + j2))
    return np.array(verts), faces


def tris_of(faces):
    out = []
    for f in faces:
        if len(f) == 3:
            out.append(f)
        else:
            out.append((f[0], f[1], f[2]))
            out.append((f[0], f[2], f[3]))
    return np.array(out, np.int32)


# ---------------------------------------------------------------------------------------------------
# Material, objek, rig

MANIFEST = json.load(open(os.path.join(ART, "art_manifest.json")))
_materials = {}


def material(name):
    if name in _materials:
        return _materials[name]
    m = bpy.data.materials.new("BoB3D_" + name)
    hexcol = MANIFEST["materials"][name].lstrip("#")
    srgb = [int(hexcol[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4 for c in srgb]
    m.diffuse_color = (*lin, 1)
    bsdf = m.node_tree.nodes.get("Principled BSDF") if m.node_tree else None
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*lin, 1)
        bsdf.inputs["Roughness"].default_value = .45
    _materials[name] = m
    return m


class Piece:
    def __init__(self, V, T, mat):
        self.V, self.T, self.mat = np.asarray(V, float), np.asarray(T, np.int32), mat


def combine(pieces):
    V, T, M, mats = [], [], [], []
    off = 0
    for p in pieces:
        if p.mat not in mats:
            mats.append(p.mat)
        V.append(p.V)
        T.append(p.T + off)
        M.append(np.full(len(p.T), mats.index(p.mat), np.int32))
        off += len(p.V)
    return np.concatenate(V), np.concatenate(T), mats, np.concatenate(M)


def make_object(ch, name, pieces, bone=None, origin=None, skinned=False):
    V, T, mats, M = combine(pieces)
    origin = np.zeros(3) if origin is None else np.asarray(origin, float)
    me = bpy.data.meshes.new(name)
    me.from_pydata((V - origin).tolist(), [], T.tolist())
    for m in mats:
        me.materials.append(material(m))
    me.polygons.foreach_set("material_index", M)
    me.shade_smooth()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = ch.arm
    if bone:
        ob.parent_type = "BONE"
        ob.parent_bone = bone
    bpy.context.view_layer.update()
    ob.matrix_world = Matrix.Translation(Vector(origin))
    if skinned:
        mod = ob.modifiers.new("Armature", "ARMATURE")
        mod.object = ch.arm
    ch.stats[name] = len(T)
    return ob, V


def build_armature(name, bones):
    ad = bpy.data.armatures.new(name + "_Rig")
    arm = bpy.data.objects.new(name, ad)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = {}
    for bname, head, tail, parent in bones:
        b = ad.edit_bones.new(bname)
        b.head, b.tail = Vector(head), Vector(tail)
        if parent:
            b.parent = eb[parent]
        eb[bname] = b
    bpy.ops.object.mode_set(mode="OBJECT")
    ad.display_type = "STICK"
    return arm


def skin_weights(V, parts, names, blend=.02):
    parts = [p for p in parts if p.op == "add" and p.bone]
    D = np.stack([p.prim(V) for p in parts], 1)
    W = np.exp(-(D - D.min(1, keepdims=True)) / blend)
    out = {}
    for i, p in enumerate(parts):
        if isinstance(p.bone, str):
            out.setdefault(p.bone, np.zeros(len(V)))
            out[p.bone] += W[:, i]
        else:
            lo, hi, z0, z1 = p.bone
            s = np.clip((V[:, 2] - z0) / (z1 - z0), 0, 1)
            s = s * s * (3 - 2 * s)
            out.setdefault(lo, np.zeros(len(V)))
            out.setdefault(hi, np.zeros(len(V)))
            out[lo] += W[:, i] * (1 - s)
            out[hi] += W[:, i] * s
    A = np.stack([out.get(n, np.zeros(len(V))) for n in names], 1)
    keep = np.argsort(-A, 1)[:, :3]
    mask = np.zeros_like(A, bool)
    np.put_along_axis(mask, keep, True, 1)
    A = np.where(mask & (A > .02 * A.max(1, keepdims=True)), A, 0)
    A /= A.sum(1, keepdims=True)
    return A


def apply_weights(ob, names, A):
    for j, n in enumerate(names):
        vg = ob.vertex_groups.get(n) or ob.vertex_groups.new(name=n)
        nz = np.nonzero(A[:, j])[0]
        for i in nz:
            vg.add([int(i)], float(A[i, j]), "REPLACE")


# ---------------------------------------------------------------------------------------------------
# Karakter

class Character:
    def __init__(self, name, skin):
        self.name, self.skin = name, skin
        self.body = Shape()
        self.bones = []
        self.garments = []  # (mat, shape, h, tris)
        self.rigid = {}  # nama objek -> (tulang, [(mat, shape, h, tris)])
        self.face_extra = []  # bentuk kaku di kepala yang ikut jadi permukaan wajah (moncong)
        self.face = {}
        self.hands = {}
        self.feet = {}
        self.stats = {}
        self.arm = None
        self.upper, self.fore, self.shoulder = {}, {}, {}

    def bone(self, name, head, tail, parent=None):
        self.bones.append((name, vec(*head), vec(*tail), parent))

    def add(self, prim, bone=None, k=0.0):
        self.body.add(prim, bone, k)

    def attach(self, obj, bone, mat, shape, h=.004, tris=1200):
        self.rigid.setdefault(obj, (bone, []))[1].append((mat, shape, h, tris))

    def bone_pos(self, name, tail=False):
        for b in self.bones:
            if b[0] == name:
                return b[2] if tail else b[1]
        raise KeyError(name)

    # --- anggota badan simetris ---------------------------------------------------------------------
    def arms(self, shoulder, elbow, wrist, r, k=(.014, .012), fore_mid=None):
        """r = (bahu, siku, pergelangan[, tengah lengan bawah])."""
        for s, side, _ in SIDES:
            m = vec(s, 1, 1)
            sh, el, wr = vec(*shoulder) * m, vec(*elbow) * m, vec(*wrist) * m
            self.bone("Arm_" + side, sh, el, "Spine")
            self.bone("Forearm_" + side, el, wr, "Arm_" + side)
            hand_dir = unit(wr - el)
            self.bone("Hand_" + side, wr, wr + hand_dir * .07, "Forearm_" + side)
            self.upper[side] = Cone(sh, el, r[0], r[1])
            self.shoulder[side] = sh
            self.add(self.upper[side], "Arm_" + side, k[0])
            if fore_mid is not None:
                fm = vec(*fore_mid) * m
                self.fore[side] = Shape().add(Cone(el, fm, r[1], r[3])).add(Cone(fm, wr, r[3], r[2]), None, k[1])
            else:
                self.fore[side] = Cone(el, wr, r[1], r[2])
            self.add(self.fore[side], "Forearm_" + side, k[1])

    def legs(self, hip, knee, ankle, r, k=.03):
        for s, side, _ in SIDES:
            m = vec(s, 1, 1)
            self.add(Cone(vec(*hip) * m, vec(*knee) * m, r[0], r[1]), "Hips", k)
            self.add(Cone(vec(*knee) * m, vec(*ankle) * m, r[1], r[2]), "Hips", .01)
        self.feet["ankle"] = vec(*ankle)

    # --- titik di permukaan wajah --------------------------------------------------------------------
    def face_sdf(self):
        s = Shape().extend(self.body.parts)
        for prim in self.face_extra:
            s.add(prim, None, 0.0)
        return s

    def surface(self, x, z, y0=None):
        """Tembak sinar dari depan (-Y) ke wajah di koordinat (x, z). Hasil: titik dan normal."""
        f = self.face_sdf()
        x, z = np.atleast_1d(np.asarray(x, float)), np.atleast_1d(np.asarray(z, float))
        y0 = (self.face["center"][1] - .6) if y0 is None else y0
        o = np.stack([x, np.full_like(x, y0), z], 1)
        t = np.zeros(len(x))
        for _ in range(120):
            d = f(o + t[:, None] * -FWD)
            t += np.maximum(d * .8, 2e-5)
        p = o + t[:, None] * -FWD
        p = project(f, p, 2)
        n = grad(f, p)
        n /= np.linalg.norm(n, axis=1, keepdims=True)
        return p, n

    def on_face(self, x, z, off=0.0):
        p, n = self.surface([x], [z])
        return p[0] + n[0] * off


# ---------------------------------------------------------------------------------------------------
# Bagian-bagian siap pakai

def glove(wrist, forearm_dir, s, scale=1.0, mitten=False, cuff=True):
    """Sarung tangan kartun 4 jari, telapak menghadap bawah, jari ke arah lengan bawah."""
    f = unit(forearm_dir)
    down = unit(-UP - f * (-UP @ f))
    lat = np.cross(f, -down)  # ke sisi +X untuk tangan kanan
    inner = s  # ibu jari menghadap ke tengah badan
    c = lambda a, b, d: wrist + (f * a + down * b + lat * d) * scale
    sh = Shape()
    sh.add(Ellipsoid(c(.05, .002, 0), vec(.046, .05, .024) * scale, np.stack([lat, f, -down], 1)), None)
    if mitten:
        sh.add(Ellipsoid(c(.085, .012, 0), vec(.042, .03, .018) * scale, np.stack([lat, f, -down], 1)), None, .02)
        sh.add(Cone(c(.03, .004, inner * .035), c(.065, .016, inner * .05), .014 * scale, .012 * scale), None, .012)
    else:
        for i, d in enumerate((-.024, 0, .024)):
            ln = (.115, .122, .112)[i]
            sh.add(Cone(c(.07, .004, d), c(ln - .02, .02, d * 1.1), .0125 * scale, .0115 * scale), None, .008)
            sh.add(Cone(c(ln - .02, .02, d * 1.1), c(ln, .035, d * 1.1), .0115 * scale, .0105 * scale), None, .006)
        sh.add(Cone(c(.03, .006, inner * .036), c(.07, .018, inner * .055), .0135 * scale, .012 * scale), None, .01)
    if cuff:
        sh.add(Cone(c(-.03, 0, 0), c(.012, .001, 0), .047 * scale, .041 * scale), None, .012)
    return sh


def shoe(ankle, length=.13, width=.05, height=.045, toe_up=0.0, chunky=False, heel=None):
    toe = ankle + vec(0, -length * .62, -height * .35 + toe_up)
    sh = Shape()
    sh.add(Ellipsoid(toe, vec(width, length * .45, height * .8)), None)
    sh.add(Ellipsoid(ankle + vec(0, .005, -height * .3), vec(width * .9, width * .95, height * .9)), None, .03)
    if chunky:
        sh.add(RBox(ankle + vec(0, -length * .3, -height * .75), vec(width * 1.05, length * .62, height * .32), .015), None, .01)
    return sh


# ---------------------------------------------------------------------------------------------------
# Desain karakter

def lemon():
    """Bot_Yellow: bulat gempal, kaki menggantung, topi bowler, kumis tebal."""
    c = Character("Bot_Yellow", "Yellow")
    c.bone("Hips", (0, -.04, .60), (0, -.04, .68))
    c.bone("Spine", (0, -.05, .68), (0, -.07, .97), "Hips")
    c.bone("Head", (0, -.07, .97), (0, -.08, 1.24), "Spine")
    c.add(Ellipsoid((0, -.07, .80), (.235, .18, .245)), ("Hips", "Spine", .64, .82))
    c.add(Ellipsoid((0, -.085, 1.085), (.165, .15, .145)), "Head", .07)
    c.arms((.19, -.07, .90), (.245, -.17, .76), (.165, -.33, .79), (.054, .047, .04))
    c.legs((.10, -.12, .62), (.12, -.29, .60), (.125, -.33, .43), (.075, .062, .047))
    c.face = dict(center=vec(0, -.085, 1.085), eye_r=.031, eye_dx=.058, eye_z=1.115, embed=.5, lid=26,
                  iris="WoodDark", brow=dict(mat="WoodDark", r=.0095, dz=.048, w=.034, tilt=-.004, arch=.006),
                  nose=dict(mat="Coral", r=(.03, .026, .026), z=1.07), mouth=dict(w=.068, z=1.022, smile=.012, teeth=False),
                  cheeks=dict(mat="Coral", dx=.098, z=1.06, r=(.026, .018)))
    hat = Shape()
    R = rot_x(-10)
    pivot = vec(0, -.085, 1.2)
    hat.add(Xf(Ellipsoid((0, -.085, 1.225), (.122, .118, .078)), pivot, R), None)
    hat.inter(Xf(Plane((0, 0, 1.19), (0, 0, -1)), pivot, R), .005)
    hat.add(Xf(Cyl((0, -.085, 1.193), .172, .007, .006), pivot, R), None, .012)
    c.attach("HeadGear", "Head", "Teal", hat, .004, 1600)
    band = Shape().add(Xf(Cyl((0, -.085, 1.208), .124, .013, .004), pivot, R), None)
    c.attach("HeadGear", "Head", "Coral", band, .004, 500)
    c.mustache = dict(mat="WoodDark", z=1.05, w=.075, r=.017, curl=.01)
    c.bowtie = dict(mat="Red", z=.955, size=1.0)
    c.hands = dict(mat="Milk", scale=1.0)
    c.feet.update(mat="WoodDark", length=.12, width=.05, height=.045, toe_up=.02)
    return c


def taffy():
    """Bot_Blue: tinggi kurus, leher panjang, kacamata bulat, rompi."""
    c = Character("Bot_Blue", "Blue")
    c.bone("Hips", (0, -.01, .60), (0, -.01, .68))
    c.bone("Spine", (0, -.01, .68), (0, -.02, 1.0), "Hips")
    c.bone("Head", (0, -.03, 1.06), (0, -.06, 1.42), "Spine")
    torso = [Ellipsoid((0, 0, .645), (.135, .12, .085)), Cone((0, 0, .66), (0, -.02, .92), .112, .118),
             Ellipsoid((0, -.02, .945), (.15, .10, .07))]
    c.add(torso[0], "Hips")
    c.add(torso[1], ("Hips", "Spine", .64, .78), .04)
    c.add(torso[2], "Spine", .05)
    c.add(Cone((0, -.02, .97), (0, -.04, 1.13), .05, .045), ("Spine", "Head", 1.0, 1.1), .035)
    c.add(Ellipsoid((0, -.055, 1.265), (.122, .116, .145)), "Head", .035)
    c.add(Ellipsoid((0, -.1, 1.19), (.072, .062, .055)), "Head", .05)
    c.arms((.15, -.01, .955), (.215, -.06, .745), (.15, -.37, .79), (.046, .039, .033))
    c.legs((.075, -.06, .61), (.095, -.36, .60), (.10, -.40, .09), (.062, .052, .041))
    c.face = dict(center=vec(0, -.055, 1.265), eye_r=.031, eye_dx=.044, eye_z=1.295, embed=.45, lid=38,
                  iris="WoodDark", brow=dict(mat="Ink", r=.0055, dz=.058, w=.028, tilt=-.003, arch=.006),
                  nose=dict(mat="Blue", r=(.02, .04, .022), z=1.252), mouth=dict(w=.058, z=1.198, smile=.009, teeth=True),
                  cheeks=dict(mat="Coral", dx=.078, z=1.235, r=(.018, .013)))
    shirt = Shape()
    shirt.add(Shape(dilate=.005).extend([Part(p, None, .04) for p in torso]).inter(Plane((0, 0, .66), (0, 0, -1)), .006))
    for side in ("Left", "Right"):
        sleeve = Shape(dilate=.006).add(c.upper[side]).inter(Sphere(c.shoulder[side], .13), .008)
        shirt.add(sleeve, None, .012)
    c.garments.append(("Milk", shirt, .005, 2600))
    vest = Shape(dilate=.011).extend([Part(p, None, .04) for p in torso])
    vest.inter(RBox((0, -.02, .80), (.3, .3, .17), .0), .006)
    vest.sub(Cone((0, -.14, 1.0), (0, -.14, .86), .05, .0), .01)
    c.garments.append(("Teal", vest, .005, 2200))
    hair = Shape()
    hair.add(Cone((0, -.04, 1.39), (0, -.10, 1.435), .032, .012), None)
    hair.add(Cone((0, -.02, 1.40), (-.045, -.03, 1.45), .026, .01), None, .02)
    hair.add(Cone((0, -.02, 1.40), (.04, -.01, 1.445), .024, .009), None, .02)
    hair.add(Ellipsoid((0, -.03, 1.385), (.08, .075, .032)), None, .02)
    c.attach("HeadGear", "Head", "TealDark", hair, .003, 1200)
    c.glasses = dict(mat="Gold", r=1.32, wire=.0045)
    c.hands = dict(mat="Milk", scale=.92)
    c.feet.update(mat="WoodDark", length=.16, width=.045, height=.04)
    return c


def gummy():
    """Bot_Red: beruang gummy gempal, telinga bulat, moncong, syal."""
    c = Character("Bot_Red", "Red")
    c.bone("Hips", (0, -.03, .60), (0, -.03, .68))
    c.bone("Spine", (0, -.03, .68), (0, -.04, 1.0), "Hips")
    c.bone("Head", (0, -.045, 1.0), (0, -.05, 1.26), "Spine")
    belly = Ellipsoid((0, -.045, .745), (.19, .165, .18))
    c.add(belly, ("Hips", "Spine", .64, .82))
    c.add(Ellipsoid((0, -.03, .90), (.175, .135, .12)), "Spine", .06)
    c.add(Ellipsoid((0, -.05, 1.115), (.155, .14, .135)), "Head", .05)
    for s in (-1, 1):
        c.add(Sphere((s * .112, -.035, 1.228), .048), "Head", .018)
    c.arms((.165, -.03, .93), (.225, -.11, .765), (.165, -.36, .795), (.062, .055, .048))
    c.legs((.10, -.08, .63), (.115, -.28, .62), (.12, -.30, .13), (.085, .072, .056))
    muzzle = Ellipsoid((0, -.178, 1.072), (.07, .045, .052))
    c.face_extra.append(muzzle)
    c.attach("HeadGear", "Head", "Cream", Shape().add(muzzle), .003, 900)
    ears = Shape()
    for s in (-1, 1):
        ears.add(Ellipsoid((s * .114, -.072, 1.232), (.027, .012, .027)), None)
    c.attach("HeadGear", "Head", "Coral", ears, .0025, 500)
    patch = Shape(dilate=.004).add(belly)
    patch.inter(Ellipsoid((0, -.22, .76), (.12, .12, .13)), .01)
    c.garments.append(("Cream", patch, .004, 900))
    scarf = Shape()
    scarf.add(Torus((0, -.035, 1.0), .118, .03, frame(rot_x(-8) @ UP), squash=.8), None)
    scarf.add(Xf(Ellipsoid((.055, -.165, .945), (.032, .013, .058)), vec(.055, -.165, .99), rot_y(-14) @ rot_x(-12)), None, .02)
    c.attach("ChestGear", "Spine", "Teal", scarf, .004, 1400)
    c.face = dict(center=vec(0, -.05, 1.115), eye_r=.024, eye_dx=.058, eye_z=1.14, embed=.5, lid=24,
                  iris="Ink", brow=dict(mat="Berry", r=.007, dz=.04, w=.024, tilt=.0, arch=.005),
                  nose=dict(mat="Ink", r=(.024, .016, .017), z=1.095), mouth=dict(w=.05, z=1.05, smile=.007, teeth=False),
                  cheeks=dict(mat="Coral", dx=.1, z=1.09, r=(.022, .015)))
    c.hands = dict(mat="Red", scale=1.1, mitten=True, cuff=False)
    c.feet.update(mat="Red", length=.12, width=.058, height=.05)
    return c


def block():
    """Bot_Mint: kotak kekar, rahang persegi, topi pet, suspender."""
    c = Character("Bot_Mint", "Mint")
    c.bone("Hips", (0, -.02, .60), (0, -.02, .68))
    c.bone("Spine", (0, -.02, .70), (0, -.04, 1.03), "Hips")
    c.bone("Head", (0, -.045, 1.05), (0, -.06, 1.30), "Spine")
    torso = [RBox((0, -.02, .67), (.165, .125, .075), .06), RBox((0, -.035, .86), (.20, .125, .13), .085),
             RBox((0, -.035, .975), (.235, .12, .06), .06)]
    c.add(torso[0], "Hips")
    c.add(torso[1], ("Hips", "Spine", .66, .80), .05)
    c.add(torso[2], "Spine", .05)
    c.add(Cone((0, -.04, 1.0), (0, -.05, 1.09), .075, .07), ("Spine", "Head", 1.02, 1.07), .03)
    c.add(RBox((0, -.055, 1.175), (.125, .115, .11), .075), "Head", .03)
    c.add(RBox((0, -.07, 1.115), (.132, .11, .06), .06), "Head", .04)
    c.arms((.23, -.035, .95), (.275, -.09, .745), (.19, -.38, .79), (.058, .05, .05, .068), fore_mid=(.24, -.24, .765))
    c.legs((.10, -.07, .62), (.115, -.31, .61), (.125, -.34, .11), (.08, .07, .06))
    straps = Shape(dilate=.006).extend([Part(p, None, .05) for p in torso])
    straps.inter(Shape().add(RBox((.085, -.035, .87), (.019, .3, .18), .004)).add(RBox((-.085, -.035, .87), (.019, .3, .18), .004)), .004)
    c.garments.append(("Teal", straps, .004, 1600))
    belt = Shape(dilate=.008).extend([Part(p, None, .05) for p in torso[:2]])
    belt.inter(RBox((0, -.03, .705), (.4, .4, .022), .004), .004)
    c.garments.append(("WoodDark", belt, .004, 1400))
    c.attach("ChestGear", "Spine", "Gold", Shape().add(RBox((0, -.158, .705), (.026, .008, .02), .005)), .002, 300)
    cap_ = Shape()
    cap_.add(Ellipsoid((0, -.05, 1.262), (.138, .132, .048)), None)
    cap_.add(Xf(Ellipsoid((0, -.175, 1.252), (.095, .07, .011)), vec(0, -.12, 1.26), rot_x(-8)), None, .015)
    c.attach("HeadGear", "Head", "WoodDark", cap_, .004, 1300)
    c.face = dict(center=vec(0, -.055, 1.16), eye_r=.024, eye_dx=.054, eye_z=1.185, embed=.5, lid=18,
                  iris="WoodDark", brow=dict(mat="Ink", r=.011, dz=.035, w=.032, tilt=-.006, arch=.0),
                  nose=dict(mat="Peach", r=(.034, .03, .03), z=1.15), mouth=dict(w=.088, z=1.093, smile=.01, teeth=True),
                  cheeks=None)
    c.hands = dict(mat="Milk", scale=1.25)
    c.feet.update(mat="WoodDark", length=.15, width=.062, height=.055, chunky=True)
    return c


def cupcake():
    """Bot_Peach: badan pir, rok bungkus cupcake berlipit, sanggul frosting, ceri."""
    c = Character("Bot_Peach", "Peach")
    c.bone("Hips", (0, -.02, .60), (0, -.02, .68))
    c.bone("Spine", (0, -.02, .72), (0, -.03, .97), "Hips")
    c.bone("Head", (0, -.04, 1.0), (0, -.05, 1.27), "Spine")
    hips = Ellipsoid((0, -.02, .672), (.20, .165, .11))
    waist = Ellipsoid((0, -.025, .81), (.115, .095, .09))
    c.add(hips, "Hips")
    c.add(waist, ("Hips", "Spine", .72, .84), .07)
    c.add(Ellipsoid((0, -.03, .92), (.13, .095, .075)), "Spine", .05)
    c.add(Cone((0, -.03, .96), (0, -.04, 1.04), .036, .034), ("Spine", "Head", .98, 1.02), .025)
    c.add(Ellipsoid((0, -.05, 1.13), (.135, .125, .13)), "Head", .03)
    c.arms((.125, -.03, .935), (.172, -.07, .745), (.085, -.37, .79), (.036, .031, .027))
    thighs = []
    for s in (-1, 1):
        thighs.append(Cone((s * .075, -.08, .62), (s * .08, -.29, .61), .062, .05))
    c.legs((.075, -.08, .62), (.08, -.29, .61), (.075, -.31, .10), (.062, .05, .032))

    def pleats(P):
        a = np.arctan2(P[:, 0], P[:, 1] + .03)
        return .005 * np.sin(a * 16)
    skirt = Shape(dilate=.014, displace=pleats).add(hips).add(waist, None, .07)
    for t in thighs:
        skirt.add(t, None, .08)
    skirt.inter(RBox((0, -.03, .70), (.4, .4, .1), .0), .008)
    skirt.inter(Plane((0, -.3, 0), (0, -1, 0)), .02)
    c.garments.append(("Berry", skirt, .003, 5000))
    chest = c.body.parts[2].prim
    top = Shape()
    top.add(Shape(dilate=.006).add(waist).add(chest, None, .05).inter(RBox((0, -.03, .86), (.3, .3, .115), 0), .006))
    for side in ("Left", "Right"):
        top.add(Shape(dilate=.012).add(c.upper[side]).inter(Sphere(c.shoulder[side], .075), .01), None, .012)
    c.garments.append(("Red", top, .004, 2400))
    hair = Shape()
    hair.add(Ellipsoid((0, -.038, 1.142), (.146, .137, .142)), None)
    hair.inter(Wavy((0, -.05, 1.175), unit((0, -.45, -1)), (0, -.04), .009, 9), .006)
    c.attach("HeadGear", "Head", "Milk", hair, .004, 1400)
    bun = Shape()
    bun.add(Torus((0, -.035, 1.282), .066, .033), None)
    bun.add(Torus((0, -.035, 1.33), .042, .027), None, .015)
    bun.add(Sphere((0, -.035, 1.362), .026), None, .015)
    c.attach("HeadGear", "Head", "Milk", bun, .003, 1500)
    c.attach("HeadGear", "Head", "Red", Shape().add(Sphere((0, -.035, 1.402), .025)), .002, 500)
    c.attach("HeadGear", "Head", "WoodDark", Shape().add(Cone((0, -.035, 1.42), (.012, -.01, 1.462), .0045, .003)), .0015, 200)
    c.face = dict(center=vec(0, -.05, 1.13), eye_r=.035, eye_dx=.05, eye_z=1.14, embed=.45, lid=40,
                  iris="Teal", lashes=True, brow=dict(mat="WoodDark", r=.0042, dz=.054, w=.026, tilt=-.003, arch=.007),
                  nose=dict(mat="Coral", r=(.014, .012, .012), z=1.112), mouth=dict(w=.045, z=1.072, smile=.009, teeth=False),
                  cheeks=dict(mat="Coral", dx=.083, z=1.095, r=(.03, .02)))
    c.hands = dict(mat="Milk", scale=.82)
    c.feet.update(mat="Red", length=.1, width=.038, height=.032)
    return c


def chef():
    """Waiter: berdiri, perut bulat, topi koki tinggi, kumis melengkung, rompi dan celemek."""
    c = Character("Waiter", "Lavender")
    c.bone("Hips", (0, 0, .86), (0, 0, .95))
    c.bone("Spine", (0, 0, .95), (0, 0, 1.28), "Hips")
    c.bone("Head", (0, -.005, 1.30), (0, -.02, 1.57), "Spine")
    pelvis = Ellipsoid((0, 0, .90), (.16, .13, .10))
    belly = Ellipsoid((0, -.02, 1.02), (.178, .168, .17))
    chest = Ellipsoid((0, 0, 1.20), (.165, .12, .11))
    c.add(pelvis, "Hips")
    c.add(belly, ("Hips", "Spine", .92, 1.06), .06)
    c.add(chest, "Spine", .06)
    c.add(Cone((0, 0, 1.26), (0, -.01, 1.32), .055, .05), ("Spine", "Head", 1.28, 1.32), .03)
    c.add(Ellipsoid((0, -.02, 1.43), (.14, .132, .14)), "Head", .035)
    c.arms((.175, 0, 1.27), (.22, 0, 1.03), (.10, -.19, .95), (.05, .043, .036))
    legs = []
    for s in (-1, 1):
        legs += [Cone((s * .085, 0, .86), (s * .09, -.015, .48), .068, .055), Cone((s * .09, -.015, .48), (s * .09, 0, .09), .055, .044)]
    c.legs((.085, 0, .86), (.09, -.015, .48), (.09, 0, .09), (.068, .055, .044))
    pants = Shape(dilate=.007).add(pelvis)
    for l in legs:
        pants.add(l, None, .03)
    pants.inter(RBox((0, 0, .48), (.4, .4, .44), 0), .006)
    c.garments.append(("Ink", pants, .005, 2600))
    shirt = Shape()
    shirt.add(Shape(dilate=.005).add(belly).add(chest, None, .06).inter(Plane((0, 0, .9), (0, 0, -1)), .006))
    for side in ("Left", "Right"):
        shirt.add(Shape(dilate=.005).add(c.upper[side]).add(c.fore[side], None, .012), None, .015)
    c.garments.append(("Milk", shirt, .005, 3600))
    vest = Shape(dilate=.012).add(belly).add(chest, None, .06)
    vest.inter(RBox((0, 0, 1.11), (.3, .3, .15), 0), .006)
    vest.sub(Cone((0, -.17, 1.28), (0, -.17, 1.12), .055, .0), .01)
    c.garments.append(("Teal", vest, .005, 2400))
    apron = Shape(dilate=.02).add(belly).add(pelvis, None, .06)
    for l in legs[::2]:
        apron.add(l, None, .06)
    apron.inter(RBox((0, -.2, .80), (.4, .2, .23), 0), .01)
    c.garments.append(("Cream", apron, .005, 2000))
    hat = Shape()
    hat.add(Cyl((0, -.02, 1.548), .104, .03, .01), None)
    for p, r in (((.055, -.02, 1.645), .068), ((-.055, -.02, 1.645), .068), ((0, -.07, 1.65), .066),
                 ((0, .03, 1.65), .066), ((0, -.02, 1.69), .075)):
        hat.add(Sphere(p, r), None, .03)
    hat.add(Cyl((0, -.02, 1.6), .095, .04, .01), None, .02)
    c.attach("HeadGear", "Head", "Milk", hat, .004, 1800)
    c.face = dict(center=vec(0, -.02, 1.43), eye_r=.029, eye_dx=.05, eye_z=1.455, embed=.5, lid=32,
                  iris="WoodDark", brow=dict(mat="Ink", r=.0065, dz=.046, w=.028, tilt=-.002, arch=.008),
                  nose=dict(mat="Berry", r=(.027, .031, .025), z=1.42), mouth=dict(w=.056, z=1.362, smile=.01, teeth=False),
                  cheeks=dict(mat="Coral", dx=.088, z=1.395, r=(.021, .015)))
    c.mustache = dict(mat="Ink", z=1.39, w=.075, r=.012, curl=.022)
    c.bowtie = dict(mat="Red", z=1.29, size=.9)
    c.hands = dict(mat="Milk", scale=.95)
    c.feet.update(mat="WoodDark", length=.15, width=.048, height=.045, ground=True)
    return c


class Xf(Prim):
    """Putar sebuah primitif dengan matriks R di sekitar titik pivot."""

    def __init__(self, prim, pivot, R):
        self.prim, self.pivot, self.R = prim, np.asarray(pivot, float), np.asarray(R, float)

    def __call__(self, P):
        return self.prim((P - self.pivot) @ self.R + self.pivot)

    def bbox(self):
        lo, hi = self.prim.bbox()
        corners = np.array([[x, y, z] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])])
        w = (corners - self.pivot) @ self.R.T + self.pivot
        return w.min(0), w.max(0)


DESIGNS = [lemon, taffy, gummy, block, cupcake, chef]


# ---------------------------------------------------------------------------------------------------
# Pembangun

def log(msg):
    print(msg, flush=True)


def build(c):
    log(f"== {c.name}")
    c.arm = build_armature(c.name, c.bones)
    fc = c.face
    head_bone = "Head"

    # Badan + baju (di-skin)
    V, T = polygonize(c.body, .006, 6500)
    # Kulit yang tertutup penuh baju dibuang: tidak bisa tembus saat bergerak, dan segitiganya berkurang.
    covered = np.zeros(len(V), bool)
    for _, shape, _, _ in c.garments:
        covered |= shape(V) < -.006
    T = T[~covered[T].all(1)]
    used = np.unique(T)
    remap = np.full(len(V), -1, np.int64)
    remap[used] = np.arange(len(used))
    V, T = V[used], remap[T].astype(np.int32)
    pieces = [Piece(V, T, c.skin)]
    for mat, shape, h, tris in c.garments:
        gv, gt = polygonize(shape, h, tris)
        pieces.append(Piece(gv, gt, mat))
    body, _ = make_object(c, "Body", pieces, skinned=True)
    # Baju memakai bobot titik kulit di bawahnya, jadi lapisannya bergerak bersama dan tidak tembus.
    names = [b[0] for b in c.bones]
    A = np.concatenate([skin_weights(p.V if i == 0 else project(c.body, p.V, 4), c.body.parts, names)
                        for i, p in enumerate(pieces)])
    apply_weights(body, names, A)

    # Wajah
    center = fc["center"]
    eyes = []
    for s, side, short in SIDES:
        p, n = c.surface([s * fc["eye_dx"]], [fc["eye_z"]])
        r = fc["eye_r"]
        ec = p[0] - n[0] * r * fc["embed"]
        look = vec(0, center[1] - 1.2, fc["eye_z"] - .05)
        axis = unit(look - ec)
        eyes.append((s, side, short, ec, axis))
        wv, wf = cap(ec, axis, r, math.pi, 12, 20)
        make_object(c, "EyeWhite_" + short, [Piece(wv, tris_of(wf), "Milk")], head_bone, ec)
        # Lapisan iris/pupil sedikit di luar bola putih supaya faset putih tidak menembus (mata besar = faset besar).
        lift = fc.get("iris_lift", .006)
        iv, i_f = cap(ec, axis, r * (1 + lift), math.radians(fc.get("iris_deg", 38)), 4, 24)
        pv, pf = cap(ec, axis, r * (1 + lift * 2), math.radians(fc.get("pupil_deg", 19)), 3, 18)
        spark_c = ec + unit(axis + UP * .32 + vec(.2, 0, 0)) * r * (1 + lift * 1.7)
        sv, sf = cap(spark_c, axis, r * fc.get("spark", .16), math.pi, 4, fc.get("spark_segs", 8))
        pupil_pieces = [Piece(iv, tris_of(i_f), fc["iris"]), Piece(pv, tris_of(pf), "Ink"), Piece(sv, tris_of(sf), "Milk")]
        if fc.get("spark2"):
            # Kilau kedua yang lebih kecil di bawah: mata kartun terlihat basah dan hidup.
            s2 = ec + unit(axis - UP * .22 - vec(.16, 0, 0)) * r * (1 + lift * 1.7)
            v2, f2 = cap(s2, axis, r * fc["spark2"], math.pi, 3, fc.get("spark_segs", 8))
            pupil_pieces.append(Piece(v2, tris_of(f2), "Milk"))
        make_object(c, "Pupil_" + short, pupil_pieces, head_bone, ec)
        # Kelopak: setengah kulit bola, tepi depannya di elevasi `lid` derajat.
        beta = math.radians(fc["lid"])
        fwd_e = unit(axis - UP * (axis @ UP))
        pole = UP * math.cos(beta) - fwd_e * math.sin(beta)
        lv, lf = cap(ec, pole, r * 1.075, math.pi / 2, 7, 22, thickness=r * .1, hint=fwd_e)
        lid_pieces = [Piece(lv, tris_of(lf), c.skin)]
        if fc.get("lashes"):
            lash = Shape()
            edge_dir = fwd_e * math.cos(beta) + UP * math.sin(beta)
            lat = unit(np.cross(UP, fwd_e))
            for i, a in enumerate((.35, .7, 1.05)):
                a *= s
                root = ec + (edge_dir * math.cos(a) + lat * math.sin(a)) * r * 1.07
                tip = root + unit(UP * .5 + edge_dir * .6 + lat * s * (.4 + i * .35)) * r * (.28 + i * .05)
                lash.add(Cone(root, tip, .0026, .0011), None, .0015)
            lv2, lt2 = polygonize(lash, .0012, 300, symmetric=False)
            lid_pieces.append(Piece(lv2, lt2, "Ink"))
        if fc.get("lash_line"):
            # Garis bulu mata tebal di tepi kelopak; ikut bergerak saat berkedip.
            edge_dir = fwd_e * math.cos(beta) + UP * math.sin(beta)
            lat = unit(np.cross(UP, fwd_e))
            line = Shape()
            arc = np.linspace(-1.45, 1.45, 9)
            pts = [ec + (edge_dir * math.cos(a) + lat * math.sin(a)) * r * 1.085 for a in arc]
            w = fc["lash_line"]
            for i in range(len(pts) - 1):
                taper = 1 - abs(arc[i] + arc[i + 1]) / 2 / 1.45 * .6
                line.add(Cone(pts[i], pts[i + 1], w * taper, w * taper), None, .002)
            # Ekor bulu mata di sudut luar.
            outer = pts[-1] if s > 0 else pts[0]
            line.add(Cone(outer, outer + unit(UP * .6 + lat * s + edge_dir * .3) * r * .35, w * .7, w * .25), None, .002)
            lv3, lt3 = polygonize(line, .0012, 500, symmetric=False)
            lid_pieces.append(Piece(lv3, lt3, "Ink"))
        make_object(c, "Lid_" + short, lid_pieces, head_bone, ec)

    # Alis
    b = fc["brow"]
    for s, side, short in SIDES:
        t = np.linspace(-1, 1, 5)
        xs = s * (fc["eye_dx"] + t * b["w"])
        zs = fc["eye_z"] + b["dz"] + b["arch"] * (1 - t * t) + b["tilt"] * t
        p, n = c.surface(xs, zs)
        pts = p + n * (b["r"] * .9)
        radii = b["r"] * np.array([.75, .95, 1, .9, .6])
        brow = Shape()
        for i in range(4):
            brow.add(Cone(pts[i], pts[i + 1], radii[i], radii[i + 1]), None, .002)
        bv, bt = polygonize(brow, .0015, 300, symmetric=False)
        make_object(c, "Brow_" + short, [Piece(bv, bt, b["mat"])], head_bone, pts[2])

    # Hidung
    nz = fc["nose"]
    p, n = c.surface([0], [nz["z"]])
    nc_ = p[0] + n[0] * nz["r"][1] * .45
    nv, nt = polygonize(Shape().add(Ellipsoid(nc_, nz["r"])), .0025, 500)
    make_object(c, "Nose", [Piece(nv, nt, nz["mat"])], head_bone, nc_)

    # Mulut dengan blend shape
    build_mouth(c)

    # Detail wajah kaku: pipi, kumis, kacamata, topi, dll.
    head_pieces = []
    if fc.get("cheeks"):
        ck = fc["cheeks"]
        for s in (-1, 1):
            dv, dt = decal(c, s * ck["dx"], ck["z"], ck["r"][0], ck["r"][1])
            head_pieces.append(Piece(dv, dt, ck["mat"]))
    if getattr(c, "mustache", None):
        m = c.mustache
        must = Shape()
        for s in (-1, 1):
            pts = [c.on_face(0, m["z"], m["r"] * .55), c.on_face(s * m["w"] * .45, m["z"] - .006, m["r"] * .6),
                   c.on_face(s * m["w"] * .8, m["z"] + m["curl"] * .4, m["r"] * .4)]
            tip = pts[2] + vec(s * .012, .006, m["curl"])
            must.add(Cone(pts[0], pts[1], m["r"], m["r"] * .85), None, .006)
            must.add(Cone(pts[1], pts[2], m["r"] * .85, m["r"] * .45), None, .006)
            must.add(Cone(pts[2], tip, m["r"] * .45, m["r"] * .2), None, .004)
        mv, mt = polygonize(must, .002, 900)
        head_pieces.append(Piece(mv, mt, m["mat"]))
    if getattr(c, "glasses", None):
        g = c.glasses
        gl = Shape()
        rims = []
        for s, side, short, ec, axis in eyes:
            rc = ec + axis * fc["eye_r"] * 1.1
            rims.append(rc)
            gl.add(Torus(rc, fc["eye_r"] * g["r"], g["wire"], frame(axis)), None)
            temple_a = rc + vec(s * fc["eye_r"] * g["r"], 0, .004)
            temple_b = vec(s * .1, 0, fc["eye_z"] + .006)
            gl.add(Cone(temple_a, temple_b, g["wire"], g["wire"]), None, .003)
        mid = (rims[0] + rims[1]) / 2 + vec(0, -.004, .006)
        gl.add(Cone(rims[0] + vec(fc["eye_r"] * g["r"] * .95, 0, .004), mid, g["wire"]), None, .003)
        gl.add(Cone(mid, rims[1] + vec(-fc["eye_r"] * g["r"] * .95, 0, .004), g["wire"]), None, .003)
        gv, gt = polygonize(gl, .0016, 1600)
        head_pieces.append(Piece(gv, gt, g["mat"]))

    rigid_groups = dict(c.rigid)
    for obj, (bone, parts) in rigid_groups.items():
        ps = head_pieces if obj == "HeadGear" else []
        for mat, shape, h, tris in parts:
            v, t = polygonize(shape, h, tris)
            ps.append(Piece(v, t, mat))
        make_object(c, obj, ps, bone)
        if obj == "HeadGear":
            head_pieces = None
    if head_pieces:
        make_object(c, "HeadGear", head_pieces, head_bone)

    if getattr(c, "bowtie", None):
        bt = c.bowtie
        p = c.surface([0], [bt["z"]], y0=-1.0)[0][0]
        k = bt["size"]
        bow = Shape()
        knot = p + vec(0, -.012 * k, 0)
        for s in (-1, 1):
            bow.add(Cone(knot, knot + vec(s * .045 * k, .004, .0), .012 * k, .026 * k), None, .006)
        bow.add(Sphere(knot + vec(0, -.004, 0), .014 * k), None, .004)
        bv, btr = polygonize(bow, .0022, 700)
        make_object(c, "Bowtie", [Piece(bv, btr, bt["mat"])], "Spine")

    # Sarung tangan
    hd = c.hands
    for s, side, short in SIDES:
        wr = c.bone_pos("Hand_" + side)
        el = c.bone_pos("Forearm_" + side)
        g = glove(wr, wr - el, s, hd.get("scale", 1), hd.get("mitten", False), hd.get("cuff", True))
        gv, gt = polygonize(g, .0028, 1100, symmetric=False)
        make_object(c, "Glove_" + short, [Piece(gv, gt, hd["mat"])], "Hand_" + side, wr)

    # Sepatu
    ft = c.feet
    shoes = Shape()
    for s in (-1, 1):
        a = ft["ankle"] * vec(s, 1, 1)
        if ft.get("ground"):
            a = vec(a[0], a[1], ft["height"] * .9)
        shoes.add(shoe(a, ft["length"], ft["width"], ft["height"], ft.get("toe_up", 0), ft.get("chunky", False)), None)
    sv, st = polygonize(shoes, .003, 1600)
    make_object(c, "Shoes", [Piece(sv, st, ft["mat"])], "Hips")
    total = sum(c.stats.values())
    log(f"   total {total} tri: " + ", ".join(f"{k} {v}" for k, v in c.stats.items()))
    return c


def decal(c, cx, cz, rx, rz, rings=4, segs=18, off=.0012):
    pts2 = [(cx, cz)]
    for i in range(1, rings + 1):
        t = i / rings
        for j in range(segs):
            a = 2 * math.pi * j / segs
            pts2.append((cx + math.cos(a) * rx * t, cz + math.sin(a) * rz * t))
    pts2 = np.array(pts2)
    p, n = c.surface(pts2[:, 0], pts2[:, 1])
    V = p + n * off
    F = []
    for j in range(segs):
        F.append((0, 1 + (j + 1) % segs, 1 + j))
    for i in range(rings - 1):
        a, b = 1 + i * segs, 1 + (i + 1) * segs
        for j in range(segs):
            j2 = (j + 1) % segs
            F.append((a + j, a + j2, b + j2, b + j))
    return V, tris_of(F)


def build_mouth(c):
    """Mulut = kisi (u, v) di permukaan wajah. Bentuk dasar senyum tipis; blend shape Open, Grin, Frown."""
    m = c.face["mouth"]
    nu, nv = 19, 7
    U = np.linspace(-1, 1, nu)
    Vv = np.linspace(0, 1, nv)

    def layout(width, smile, opening, drop=.8):
        hw = m["w"] * .5 * width
        x = np.repeat(U[:, None], nv, 1) * hw
        base = m["z"] + smile * U[:, None] ** 2 - smile * .35
        o = opening * (1 - U[:, None] ** 2) ** .75
        top = base + o * (1 - drop)
        bot = base - o * drop
        z = top + (bot - top) * Vv[None, :]
        return x.ravel(), z.ravel()

    shapes = {
        "Basis": layout(1.0, m["smile"], m.get("rest_open", .0045)),
        "Open": layout(.72, m["smile"] * .3, .042, .78),
        "Grin": layout(1.22, m["smile"] * 1.6 + .006, .026, .85),
        "Frown": layout(.85, -m["smile"] * 1.1, .006),
    }
    pos = {}
    for k, (x, z) in shapes.items():
        p, n = c.surface(x, z)
        pos[k] = p + n * .0016
    F, M = [], []
    teeth, tongue = m.get("teeth"), True
    mats = m.get("mats", ["Ink", "Milk", "Coral"])
    for i in range(nu - 1):
        for j in range(nv - 1):
            a = i * nv + j
            F.append((a, a + nv, a + nv + 1))
            F.append((a, a + nv + 1, a + 1))
            u = (U[i] + U[i + 1]) / 2
            v = (Vv[j] + Vv[j + 1]) / 2
            mi = 0
            if teeth and v < .34 and abs(u) < .2:
                mi = 1
            elif tongue and v > .6 and abs(u) < .5:
                mi = 2
            M += [mi, mi]
    T = np.array(F, np.int32)
    origin = pos["Basis"][(nu // 2) * nv + nv // 2]
    me = bpy.data.meshes.new("Mouth")
    me.from_pydata((pos["Basis"] - origin).tolist(), [], T.tolist())
    for name in mats:
        me.materials.append(material(name))
    me.polygons.foreach_set("material_index", np.array(M, np.int32))
    me.shade_smooth()
    ob = bpy.data.objects.new("Mouth", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = c.arm
    ob.parent_type = "BONE"
    ob.parent_bone = "Head"
    bpy.context.view_layer.update()
    ob.matrix_world = Matrix.Translation(Vector(origin))
    ob.shape_key_add(name="Basis", from_mix=False)
    for k in ("Open", "Grin", "Frown"):
        key = ob.shape_key_add(name=k, from_mix=False)
        key.data.foreach_set("co", (pos[k] - origin).ravel())
        key.value = 0.0
    c.stats["Mouth"] = len(T)


# ---------------------------------------------------------------------------------------------------
# Export dan preview

def export(c):
    bpy.ops.object.select_all(action="DESELECT")
    c.arm.select_set(True)
    for o in c.arm.children_recursive:
        o.select_set(True)
    bpy.context.view_layer.objects.active = c.arm
    path = os.path.join(MODELS, c.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
        bake_anim=False, use_armature_deform_only=False, mesh_smooth_type="FACE", use_mesh_modifiers=True,
        use_custom_props=False, primary_bone_axis="Y", secondary_bone_axis="X")
    log(f"   export {path}")


def pose_test(c, strength=1.0):
    """Pose contoh untuk memeriksa deformasi: condong, menoleh, tangan ke mulut."""
    pb = c.arm.pose.bones
    pb["Spine"].rotation_mode = "XYZ"
    pb["Spine"].rotation_euler = (math.radians(-10 * strength), 0, math.radians(12 * strength))
    pb["Head"].rotation_mode = "XYZ"
    pb["Head"].rotation_euler = (math.radians(10), math.radians(15), math.radians(-20))
    for name, ang in (("Arm_Right", (-60, 0, 0)), ("Forearm_Right", (-45, 0, 0)), ("Arm_Left", (0, 0, 30))):
        pb[name].rotation_mode = "XYZ"
        pb[name].rotation_euler = tuple(math.radians(a) for a in ang)
    for o in c.arm.children:
        if o.name == "Mouth" and o.data.shape_keys:
            o.data.shape_keys.key_blocks["Open"].value = .7


def preview(chars, path, pose=False):
    scene = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in engines else engines[0]
    chair_path = os.path.join(MODELS, "Chair.fbx")
    spacing = .78
    for i, c in enumerate(chars):
        x = (i - (len(chars) - 1) / 2) * spacing
        c.arm.location = (x, 0, 0)
        if pose:
            pose_test(c)
        if c.name != "Waiter":
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=chair_path)
            for o in set(bpy.data.objects) - before:
                if o.parent is None:
                    o.location = (x, 0, 0)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    for rot, energy in (((math.radians(50), 0, math.radians(-30)), 3.5), ((math.radians(70), 0, math.radians(140)), 1.2)):
        sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
        sun.data.energy = energy
        sun.rotation_euler = rot
        scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("World")
    scene.world = world
    bg = world.node_tree.nodes.get("Background") if world.node_tree else None
    if bg:
        bg.inputs[0].default_value = (.55, .6, .62, 1)
        bg.inputs[1].default_value = .8
    floor = bpy.data.meshes.new("Floor")
    floor.from_pydata([(-6, -6, 0), (6, -6, 0), (6, 6, 0), (-6, 6, 0)], [], [(0, 1, 2, 3)])
    fo = bpy.data.objects.new("Floor", floor)
    fo.data.materials.append(material("Floor"))
    scene.collection.objects.link(fo)
    base, ext = os.path.splitext(path)
    width = spacing * len(chars)
    views = [("", (0, -max(3.2, width * 1.45), 1.15), (87, 0, 0), 50, (1900, 900))]
    views.append(("_34", (width * .55, -width * .95, 1.6), (80, 0, 30), 40, (1900, 900)))
    for i, c in enumerate(chars):
        hc = np.array(c.face["center"]) + np.array([c.arm.location.x, 0, 0])
        views.append((f"_face{i}", (hc[0] + .25, hc[1] - .55, hc[2] + .03), (88, 0, 24), 50, (800, 800)))
    for suffix, loc, rot, lens, res in views:
        cam.location = loc
        cam.rotation_euler = tuple(math.radians(a) for a in rot)
        cam.data.lens = lens
        scene.render.resolution_x, scene.render.resolution_y = res
        scene.render.filepath = base + suffix + ext
        bpy.ops.render.render(write_still=True)
    log(f"preview {path}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    if "--only" in argv:
        only = set(argv[argv.index("--only") + 1].split(","))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    chars = []
    for design in DESIGNS:
        c = design()
        if only and c.name not in only:
            continue
        build(c)
        if "--no-export" not in argv:
            export(c)
        # Bebaskan nama objek supaya karakter berikutnya tidak mendapat akhiran .001 di FBX-nya.
        for o in c.arm.children_recursive:
            o.name = c.name + "." + o.name
        chars.append(c)
    if "--preview" in argv:
        preview(chars, argv[argv.index("--preview") + 1], "--pose-test" in argv)


if __name__ == "__main__":
    main()
