"""Anime/chibi floating head + floating hands for every character (bots, waiter, player avatar).

Style: chibi head without a neck, shared natural skin tone, hair made of pointed locks,
oval anime eyes (gradient iris, highlights, thick lashes with tails), thin brows, tiny nose, "D" mouth.
Run: blender --factory-startup -b --python Tools/Blender/bob_floating.py [-- --preview path.png] [--only red,blue]
"""
import os
import sys
import json
import math
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(__file__))
import bob_characters as art
from bob_characters import (Character, Cone, Ellipsoid, Plane, Shape, Sphere, Torus, Xf, Cyl, Piece,
                            vec, unit, rot_x, SIDES, UP, cap, tris_of, polygonize, make_object, material)

SKIN = "Skin"


# ---------------------------------------------------------------------------------------------------
# Hands and rig

def hand_shape(wrist, direction, side, scale=1):
    f = unit(direction)
    down = unit(-UP - f * (-UP @ f))
    lat = np.cross(f, -down)
    point = lambda a, b, d: wrist + (f * a + down * b + lat * d) * scale
    suffix = "Left" if side < 0 else "Right"
    shape = Shape().add(Ellipsoid(point(.043, 0, 0), vec(.04, .042, .02) * scale, np.stack([lat, f, -down], 1)), "Hand_" + suffix)
    for name, lateral, length in (("Index", -.027 * side, .107), ("Middle", -.009 * side, .116),
                                 ("Ring", .009 * side, .108), ("Little", .026 * side, .093)):
        a, b, c = point(.065, 0, lateral), point(.065 + (length - .065) * .55, .003, lateral), point(length, .006, lateral)
        shape.add(Cone(a, b, .0105 * scale, .0095 * scale), f"Finger_{name}_Proximal_" + suffix, .005)
        shape.add(Cone(b, c, .0095 * scale, .0085 * scale), f"Finger_{name}_Distal_" + suffix, .004)
    a, b, c = point(.025, 0, -.031 * side), point(.048, .002, -.048 * side), point(.074, .005, -.051 * side)
    shape.add(Cone(a, b, .013 * scale, .011 * scale), "Finger_Thumb_Proximal_" + suffix, .009)
    shape.add(Cone(b, c, .011 * scale, .009 * scale), "Finger_Thumb_Distal_" + suffix, .005)
    # Puffy glove cuffs, like the cartoon hands in the reference.
    shape.add(Cone(point(-.026, 0, 0), point(.008, 0, 0), .036 * scale, .033 * scale), "Hand_" + suffix, .010)
    return shape


def add_fingers(c):
    for side, suffix, _ in SIDES:
        wrist = c.bone_pos("Hand_" + suffix)
        shape = hand_shape(wrist, wrist - c.bone_pos("Forearm_" + suffix), side, c.hands.get("scale", 1))
        for part in shape.parts:
            if part.bone.startswith("Finger_"):
                parent = "Hand_" + suffix if "Proximal" in part.bone else part.bone.replace("Distal", "Proximal")
                c.bone(part.bone, part.prim.a, part.prim.b, parent)


def rig(c, shoulder, elbow, wrist, scale=1.0):
    """Bones only (no body/arm meshes). Arms use the legacy design positions so bot hands still
    rest on the table and CharacterAnimator animations stay unchanged."""
    cz, (rx, ry, rz), y = c.head
    pivot = cz - rz * .45
    c.bone("Hips", (0, -.02, .60), (0, -.02, .68))
    c.bone("Spine", (0, -.02, .68), (0, y, pivot), "Hips")
    c.bone("Head", (0, y, pivot), (0, y, cz + rz), "Spine")
    for s, side, _ in SIDES:
        m = vec(s, 1, 1)
        sh, el, wr = vec(*shoulder) * m, vec(*elbow) * m, vec(*wrist) * m
        c.bone("Arm_" + side, sh, el, "Spine")
        c.bone("Forearm_" + side, el, wr, "Arm_" + side)
        c.bone("Hand_" + side, wr, wr + unit(wr - el) * .07, "Forearm_" + side)
    c.hands = dict(scale=scale)
    add_fingers(c)


# ---------------------------------------------------------------------------------------------------
# Geometry helpers

def orient_out(V, T, ref):
    """Flip triangles whose normals face the reference (ref = one point, or one point per vertex)."""
    a, b, c = V[T[:, 0]], V[T[:, 1]], V[T[:, 2]]
    n = np.cross(b - a, c - a)
    centroid = (a + b + c) / 3
    r = ref if np.ndim(ref) == 1 else ref[T].mean(1)
    flip = np.einsum("ij,ij->i", n, centroid - r) < 0
    T = T.copy()
    T[flip] = T[flip][:, ::-1]
    return T


def tube(path, width, thick, normals, segs=6, tip=True):
    """Sweep elliptical cross-sections along a path; normals = outward direction (determines the flat side)."""
    path = np.asarray(path, float)
    n = len(path)
    tang = np.gradient(path, axis=0)
    tang /= np.linalg.norm(tang, axis=1, keepdims=True)
    V, ref, T = [], [], []
    rings = n - 1 if tip else n
    for i in range(rings):
        t = tang[i]
        nn = unit(normals[i] - t * (normals[i] @ t))
        b = np.cross(t, nn)
        for k in range(segs):
            a = 2 * math.pi * k / segs
            V.append(path[i] + b * width[i] * math.cos(a) + nn * thick[i] * math.sin(a))
            ref.append(path[i])
    for i in range(rings - 1):
        for k in range(segs):
            k2 = (k + 1) % segs
            p, q = i * segs, (i + 1) * segs
            T += [(p + k, q + k, q + k2), (p + k, q + k2, p + k2)]
    V.append(path[0]); ref.append(path[0] + tang[0] * .01)
    root = len(V) - 1
    for k in range(segs):
        T.append((root, (k + 1) % segs, k))
    if tip:
        last = (rings - 1) * segs
        V.append(path[-1]); ref.append(path[-2])
        end = len(V) - 1
        for k in range(segs):
            T.append((last + k, last + (k + 1) % segs, end))
    else:
        last = (rings - 1) * segs
        V.append(path[-1]); ref.append(path[-1] - tang[-1] * .01)
        end = len(V) - 1
        for k in range(segs):
            T.append((last + k, last + (k + 1) % segs, end))
    V, T = np.array(V), np.array(T, np.int32)
    return V, orient_out(V, T, np.array(ref))


class Scalp:
    """Hair reference ellipsoid: locks are designed by azimuth (0 = front, + = right) and elevation."""

    def __init__(self, center, radii):
        self.c, self.r = vec(*center), vec(*radii)

    def point(self, az, el, off=0.0):
        a, e = math.radians(az), math.radians(el)
        d = vec(math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e))
        return self.c + d * (self.r + off), unit(d / (self.r + off))

    def lock(self, root, tip, width, thick=None, n=10, lift=.004, bulge=.01, tip_out=0.0, curl=0.0):
        """One pointed lock from root (az, el) to tip (az, el). tip_out moves the tip away from the head,
        curl bends the tip sideways."""
        thick = thick or width * .38
        P, N, W, H = [], [], [], []
        for i in range(n):
            t = i / (n - 1)
            az = root[0] + (tip[0] - root[0]) * t + curl * t ** 3
            el = root[1] + (tip[1] - root[1]) * t
            off = lift + bulge * math.sin(math.pi * min(1, t * 1.15)) + tip_out * t ** 2
            p, nn = self.point(az, el, off)
            P.append(p); N.append(nn)
            shape = (1 - t) ** .75
            W.append(max(width * (.6 + .4 * math.sin(math.pi * (.25 + .75 * t))) * shape, .0008))
            H.append(max(thick * shape, .0006))
        return tube(P, W, H, N)


def child_object(c, name, pieces, parent):
    """Mesh object parented to another object (eye), verts in parent local space."""
    V, T, mats, M = art.combine(pieces)
    me = bpy.data.meshes.new(name)
    me.from_pydata(V.tolist(), [], T.tolist())
    for m in mats:
        me.materials.append(material(m))
    me.polygons.foreach_set("material_index", M)
    me.shade_smooth()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = Matrix.Identity(4)
    c.stats[name] = len(T)
    return ob


# ---------------------------------------------------------------------------------------------------
# Anime face

def patch(axis, r, ax, ay, cx=0.0, cy=0.0, rings=5, segs=28, band=None):
    """Elliptical patch on a sphere of radius r (half-angles ax, ay in degrees, center cx, cy).
    band(beta_norm, rho) -> material name per triangle."""
    lat = unit(np.cross(UP, axis))
    up = unit(np.cross(axis, lat))

    def d(alpha, beta):
        a, b = math.radians(alpha), math.radians(beta)
        return unit(axis * math.cos(a) * math.cos(b) + lat * math.sin(a) * math.cos(b) + up * math.sin(b)) * r

    V, T = [d(cx, cy)], []
    for i in range(1, rings + 1):
        rho = i / rings
        for j in range(segs):
            ps = 2 * math.pi * j / segs
            V.append(d(cx + rho * ax * math.cos(ps), cy + rho * ay * math.sin(ps)))
    for j in range(segs):
        T.append((0, 1 + j, 1 + (j + 1) % segs))
    for i in range(rings - 1):
        a, b = 1 + i * segs, 1 + (i + 1) * segs
        for j in range(segs):
            j2 = (j + 1) % segs
            T += [(a + j, b + j, b + j2), (a + j, b + j2, a + j2)]
    V, T = np.array(V), np.array(T, np.int32)
    T = orient_out(V, T, np.zeros(3))
    if band is None:
        return [(V, T, None)]
    cent = V[T].mean(1) / r
    beta = np.degrees(np.arcsin(np.clip(cent @ up, -1, 1)))
    alpha = np.degrees(np.arctan2(cent @ lat, cent @ axis))
    rho = np.sqrt(((alpha - cx) / ax) ** 2 + ((beta - cy) / ay) ** 2)
    keys = [band((beta[k] - cy) / ay, rho[k]) for k in range(len(T))]
    return [(V, T[np.array([k == m for k in keys])], m) for m in dict.fromkeys(keys)]


def anime_eyes(c):
    """Vertically stretched white sphere (EyeWhite_*); iris/pupil (Pupil_*) and eyelids (Lid_*) are its children,
    so CharacterAnimator glances and blinks stay attached to the oval eye surface."""
    fc = c.face
    r, stretch = fc["eye_r"], fc["stretch"]
    S = np.diag([1, 1, stretch])
    iris, dark, light = fc["iris"], fc["iris"] + "Dark", fc["iris"] + "Light"
    for s, side, short in SIDES:
        p, n = c.surface([s * fc["eye_dx"]], [fc["eye_z"]])
        ec = p[0] - n[0] * r * fc["embed"]
        axis = unit(vec(s * .16, -1, -.06))
        fwd = unit(axis - UP * (axis @ UP))
        lat = unit(np.cross(UP, fwd))

        def rim(phi):
            """Angle from the viewing axis where the eyeball enters the skin (eyelid edge)."""
            for th in np.radians(np.arange(4, 100, 1.5)):
                v = (axis * math.cos(th) + (lat * math.cos(phi) + UP * math.sin(phi)) * math.sin(th)) * r
                if c.body((ec + S @ v)[None])[0] < 0:
                    return th
            return math.radians(95)

        # White sphere + thin lower lashes following the lower rim.
        wv, wf = cap(np.zeros(3), axis, r, math.pi, 14, 28)
        white = [Piece(wv, tris_of(wf), "Milk")]
        lower = []
        for phi in np.radians(np.linspace(228, 312, 9)):
            th = rim(phi) - math.radians(4)
            lower.append((axis * math.cos(th) + (lat * math.cos(phi) + UP * math.sin(phi)) * math.sin(th)) * r * 1.03)
        lower = np.array(lower)
        lw = np.array([.0003, .0006, .0009, .001, .001, .001, .0009, .0006, .0003])
        v, t = tube(lower, lw, lw * .7, [unit(q) for q in lower], segs=5, tip=False)
        white.append(Piece(v, t, "Ink"))
        eye, _ = make_object(c, "EyeWhite_" + short, white, "Head")
        bpy.context.view_layer.update()
        eye.matrix_world = Matrix.Translation(Vector(ec)) @ Matrix.Diagonal((1, 1, stretch, 1))

        # Gradient oval iris: dark at the top and edges, base color at the center, light at the bottom.
        k = 1 + .025
        band = lambda b, rho: dark if (b > .3 or rho > .86) else (light if b < -.42 else iris)
        ip = [Piece(V * k, T, m) for V, T, m in patch(axis, r, 41, 50, band=band) if len(T)]
        ip += [Piece(V * (k + .025), T, "Ink") for V, T, _ in patch(axis, r, 18, 25, cy=2)]
        # Large upper-left and small lower-right highlights, matching in both eyes (one light source).
        ip += [Piece(V * (k + .05), T, "Milk") for V, T, _ in patch(axis, r, 11, 12, cx=-14, cy=18, rings=3, segs=16)]
        ip += [Piece(V * (k + .05), T, "Milk") for V, T, _ in patch(axis, r, 6, 6, cx=13, cy=-20, rings=2, segs=12)]
        child_object(c, "Pupil_" + short, ip, eye)

        # Upper lid: edge slightly below the upper rim; thick lash line with a tail attached to the edge.
        beta = rim(math.pi / 2) - math.radians(fc.get("lid_drop", 15))
        pole = UP * math.cos(beta) - fwd * math.sin(beta)
        kv, kf = cap(np.zeros(3), pole, r * 1.06, math.pi / 2, 7, 24, thickness=r * .08, hint=fwd)
        lid = [Piece(kv, tris_of(kf), SKIN)]
        edge = fwd * math.cos(beta) + UP * math.sin(beta)
        arc = np.linspace(-1.3, 1.35, 13) * s
        pts = np.array([(edge * math.cos(a) + lat * math.sin(a)) * r * 1.075 for a in arc])
        tail = pts[-1] + unit(UP * .5 + lat * s + edge * .3) * r * .4
        pts = np.vstack([pts, tail])
        u = np.linspace(0, 1, len(pts))
        w = fc["lash"] * (.3 + .7 * np.sin(np.pi * np.clip(u * .8 + .12, 0, 1)))
        w[-1] = .0005
        v, t = tube(pts, w, w * .55, [unit(q) for q in pts], segs=6, tip=False)
        lid.append(Piece(v, t, "Ink"))
        if fc.get("lashes"):
            for a, ln in ((1.0, .45), (1.25, .55)):
                root = (edge * math.cos(a * s) + lat * math.sin(a * s)) * r * 1.08
                tipp = root + unit(UP * .5 + edge * .4 + lat * s * .7) * r * ln
                q = np.array([root, (root + tipp) / 2 + UP * r * .05, tipp])
                v, t = tube(q, np.array([.0018, .0012, .0004]), np.array([.0012, .0008, .0003]), [unit(x) for x in q], segs=5)
                lid.append(Piece(v, t, "Ink"))
        child_object(c, "Lid_" + short, lid, eye)


def brows(c):
    fc, b = c.face, c.face["brow"]
    for s, side, short in SIDES:
        t = np.linspace(-1, 1, 7)
        xs = s * (fc["eye_dx"] + t * b["w"])
        zs = fc["eye_z"] + b["dz"] + b["arch"] * (1 - t * t) + b["tilt"] * t
        p, n = c.surface(xs, zs)
        pts = p + n * .003
        wid = b["r"] * np.array([.5, .85, 1, 1, .9, .65, .3])
        v, tt = tube(pts, wid, wid * .45, list(n), segs=6, tip=False)
        make_object(c, "Brow_" + short, [Piece(v, tt, b["mat"])], "Head", pts[3])


def nose(c):
    nz = c.face["nose"]
    p, n = c.surface([0], [nz["z"]])
    nc = p[0] + n[0] * nz["r"][1] * .3
    nv, nt = polygonize(Shape().add(Ellipsoid(nc, nz["r"])), .0012, 300)
    make_object(c, "Nose", [Piece(nv, nt, SKIN)], "Head", nc)


def mustache(c, z, w, rr, curl, mat):
    must = Shape()
    for s in (-1, 1):
        pts = [c.on_face(0, z, rr * .55), c.on_face(s * w * .45, z - .006, rr * .6), c.on_face(s * w * .8, z + curl * .4, rr * .4)]
        tip = pts[2] + vec(s * .012, .006, curl)
        must.add(Cone(pts[0], pts[1], rr, rr * .85), None, .006)
        must.add(Cone(pts[1], pts[2], rr * .85, rr * .45), None, .006)
        must.add(Cone(pts[2], tip, rr * .45, rr * .2), None, .004)
    mv, mt = polygonize(must, .0018, 900)
    return Piece(mv, mt, mat)


def glasses(c, mat="Gold"):
    fc = c.face
    gl, rims = Shape(), []
    rad = fc["eye_r"] * 1.32
    for s in (-1, 1):
        p, n = c.surface([s * fc["eye_dx"]], [fc["eye_z"]])
        rc = p[0] + vec(0, -.016, 0)
        rims.append(rc)
        gl.add(Torus(rc, rad, .0042, art.frame(unit(vec(s * .15, -1, 0)))), None)
        gl.add(Cone(rc + vec(s * rad, .0, .004), vec(s * .14, .02, fc["eye_z"] + .01), .0042, .0042), None, .003)
    mid = (rims[0] + rims[1]) / 2 + vec(0, -.002, .012)
    for s, rc in zip((-1, 1), rims):
        gl.add(Cone(rc + vec(-s * rad * .95, 0, .012), mid, .0042), None, .003)
    gv, gt = polygonize(gl, .0016, 1800)
    return Piece(gv, gt, mat)


def build_head(c, hair, extras=()):
    """Replacement for art.build: scalp, eyes, brows, nose, mouth, blush, hair + accessories, gloves."""
    c.arm = art.build_armature(c.name, c.bones)
    hv, ht = polygonize(c.body, .004, 7000)
    make_object(c, "HeadSkin", [Piece(hv, ht, SKIN)], "Head")
    anime_eyes(c)
    brows(c)
    nose(c)
    art.build_mouth(c)
    gear = list(extras) + list(hair)
    ck = c.face["cheeks"]
    for s in (-1, 1):
        dv, dt = art.decal(c, s * ck["dx"], ck["z"], ck["r"][0], ck["r"][1], rings=4, segs=20)
        gear.append(Piece(dv, dt, "Blush"))
    make_object(c, "HeadGear", gear, "Head")
    for s, side, short in SIDES:
        wr, el = c.bone_pos("Hand_" + side), c.bone_pos("Forearm_" + side)
        gv, gt = polygonize(hand_shape(wr, wr - el, s, c.hands["scale"]), .0028, 1100, symmetric=False)
        make_object(c, "Glove_" + short, [Piece(gv, gt, "Milk")], "Hand_" + side, wr)


# ---------------------------------------------------------------------------------------------------
# Chibi head

def chibi(name, cz=1.16, r=(.158, .15, .162), y=-.045, jaw=1.0, ears=False):
    """Large forehead/cranium, small lower face, plump cheeks, slightly pointed chin."""
    c = Character(name, SKIN)
    rx, ry, rz = r
    c.add(Ellipsoid((0, y + .006, cz + .022), (rx, ry, rz * .9)), "Head")
    c.add(Ellipsoid((0, y - .016, cz - rz * .34), (rx * .8, ry * .84, rz * .6)), "Head", .06)
    c.add(Ellipsoid((0, y - ry * .42, cz - rz * .7), (rx * .28 * jaw, ry * .34, rz * .2)), "Head", .05)
    for s in (-1, 1):
        c.add(Ellipsoid((s * rx * .48, y - ry * .5, cz - rz * .42), (.05, .045, .042)), "Head", .04)
        if ears:
            c.add(Ellipsoid((s * rx * .94, y + .012, cz - .03), (.018, .024, .032)), "Head", .014)
    c.head = (cz, r, y)
    c.scalp = Scalp((0, y + .006, cz + .022), (rx, ry, rz * .9))
    return c


def face(c, iris, brow, eye=1.0, lashes=False, smile=.012, open_=.018, mouth_w=.05):
    cz, (rx, ry, rz), y = c.head
    c.face = dict(center=vec(0, y, cz), eye_r=.043 * eye, eye_dx=rx * .4, eye_z=cz - .028, embed=.56, stretch=1.24,
                  iris=iris, lashes=lashes, lash=.0062,
                  brow=dict(mat=brow, r=.0042, dz=.07 * eye, w=.03, tilt=.004, arch=.007),
                  nose=dict(r=(.0075, .006, .0065), z=cz - .085),
                  mouth=dict(w=mouth_w, z=cz - .11, smile=smile, teeth=True, rest_open=open_, mats=["MouthDark", "Milk", "Tongue"]),
                  cheeks=dict(dx=rx * .56, z=cz - .085, r=(.026, .016)))


def hair_cap(c, front=.08, back=-.07, dilate=.006, h=.004, tris=2400):
    """Base hair layer beneath locks so the scalp does not show through gaps."""
    cz, (rx, ry, rz), y = c.head
    n = unit((0, -(front - back), -2 * ry))
    s = Shape(dilate=dilate).add(Ellipsoid((0, y + .006, cz + .022), (rx, ry, rz * .9)))
    s.inter(Plane((0, y, cz + .022 + (front + back) / 2), n), .012)
    return polygonize(s, h, tris)


def bangs(c, n, span, root_el, tip_el, width, jitter=(0, 8, 3, 10, 1, 7, 4, 9, 2), part=0.0, curl=0.0, tip_out=.006):
    """Bangs: locks from the crown down to the forehead, with varied lengths."""
    out = []
    for i in range(n):
        az = -span + 2 * span * i / max(1, n - 1)
        out.append(c.scalp.lock((az * .55 + part, root_el), (az, tip_el + jitter[i % len(jitter)]), width, lift=.003,
                                bulge=.008, tip_out=tip_out, curl=curl * (1 if az >= part else -1)))
    return out


def ring_locks(c, n, az0, az1, root_el, tip_el, width, tip_out=.01, bulge=.012, curl=0.0):
    """Side/back locks hanging downward."""
    out = []
    for i in range(n):
        az = az0 + (az1 - az0) * i / max(1, n - 1)
        out.append(c.scalp.lock((az, root_el), (az + curl, tip_el + (i % 3) * 4), width, bulge=bulge, tip_out=tip_out))
    return out


def hair_pieces(locks, cap_mesh, mat):
    return [Piece(V, T, mat) for V, T in locks] + [Piece(cap_mesh[0], cap_mesh[1], mat)]


def sdf(shape, mat, h=.003, tris=1400):
    v, t = polygonize(shape, h, tris)
    return Piece(v, t, mat)


# ---------------------------------------------------------------------------------------------------
# Designs

def red():
    """Bot_Red: girl with pink hair, pointed bangs, long cheek strands, high bun, ahoge."""
    c = chibi("Bot_Red_Float")
    cz, r, y = c.head
    rig(c, (.165, -.03, .93), (.225, -.11, .765), (.165, -.36, .795), 1.12)
    face(c, "IrisViolet", "HairPinkDark", eye=1.06, lashes=True)
    sc = c.scalp
    locks = bangs(c, 9, 62, 72, 16, .036, part=-12, curl=6)
    for s in (-1, 1):
        locks += [sc.lock((s * 70, 40), (s * 82, -48), .04, bulge=.006, tip_out=.02),
                  sc.lock((s * 58, 46), (s * 68, -30), .034, bulge=.006, tip_out=.012)]
    locks += ring_locks(c, 9, 100, 260, 55, -35, .058, tip_out=.006)
    locks.append(sc.lock((5, 86), (-25, 89), .012, lift=.01, bulge=.03, tip_out=.05, n=8))  # ahoge
    hair = hair_pieces(locks, hair_cap(c, front=.07, back=-.12), "HairPink")
    hair.append(sdf(Shape().add(Sphere((0, y + .055, cz + .2), .062))
                    .add(Torus((0, y + .055, cz + .2), .045, .022, art.frame(unit((0, -.3, 1)))), None, .02), "HairPink"))
    hair.append(sdf(Shape().add(Torus((0, y + .045, cz + .15), .04, .011, art.frame(unit((0, .35, 1))))), "Coral", .002, 400))
    build_head(c, hair)
    return c


def blue():
    """Bot_Blue: boy with upward-swept spiky blue hair, round glasses."""
    c = chibi("Bot_Blue_Float", r=(.152, .146, .166), y=-.04, ears=True)
    rig(c, (.15, -.01, .955), (.215, -.06, .745), (.15, -.37, .79), 1.08)
    face(c, "IrisBlue", "HairBlueDark", smile=.014, open_=.02)
    sc = c.scalp
    locks = []
    for i, az in enumerate((-45, -25, -8, 10, 28, 46)):
        locks.append(sc.lock((az * .5, 50), (az + 12, 62 + (i % 2) * 8), .05, bulge=.015, tip_out=.06 + (i % 3) * .012, n=9))
    locks += bangs(c, 7, 45, 72, 17, .034, jitter=(0, 7, 2, 9, 3, 6, 1), curl=7, tip_out=.012)
    locks += ring_locks(c, 8, 70, 290, 50, -20, .055, tip_out=.02, curl=6)
    for az in (120, 150, 180, 210, 240):
        locks.append(sc.lock((az, 75), (az, 78), .05, bulge=.02, tip_out=.05, n=8))
    build_head(c, hair_pieces(locks, hair_cap(c, front=.1, back=-.05), "HairBlue"), [glasses(c)])
    return c


def yellow():
    """Bot_Yellow: friendly man, yellow bowler hat, thick mustache, spiky brown side hair."""
    c = chibi("Bot_Yellow_Float", r=(.165, .152, .158), y=-.06, jaw=1.2, ears=True)
    cz, r, y = c.head
    rig(c, (.19, -.07, .90), (.245, -.17, .76), (.165, -.33, .79), 1.12)
    face(c, "IrisBrown", "HairBrownDark", eye=.94, smile=.01, open_=.012, mouth_w=.052)
    locks = ring_locks(c, 12, 62, 298, 22, -28, .045, tip_out=.012, curl=8)
    locks += bangs(c, 5, 30, 40, 26, .03, jitter=(0, 4, 1, 5, 2), tip_out=.01)
    hair = hair_pieces(locks, hair_cap(c, front=.07, back=-.08, tris=2000), "HairBrown")
    R, pivot = rot_x(-8), vec(0, y, cz + .13)
    hat = Shape().add(Xf(Ellipsoid((0, y + .005, cz + .16), (.14, .135, .092)), pivot, R), None)
    hat.inter(Xf(Plane((0, 0, cz + .12), (0, 0, -1)), pivot, R), .005)
    hat.add(Xf(Cyl((0, y + .005, cz + .122), .195, .008, .007), pivot, R), None, .014)
    extras = [sdf(hat, "Yellow", .004, 1800), sdf(Shape().add(Xf(Cyl((0, y + .005, cz + .138), .142, .014, .004), pivot, R)), "Teal", .004, 600),
              mustache(c, cz - .097, .06, .0085, .012, "HairBrown")]
    build_head(c, hair, extras)
    return c


def mint():
    """Bot_Mint: sporty boy, mint baseball cap, spiky brown hair under the cap, strong brows."""
    c = chibi("Bot_Mint_Float", r=(.162, .15, .16), y=-.05, jaw=1.35, ears=True)
    cz, r, y = c.head
    rig(c, (.23, -.035, .95), (.275, -.09, .745), (.19, -.38, .79), 1.2)
    face(c, "IrisGreen", "HairBrownDark", eye=.95, smile=.013, open_=.016, mouth_w=.056)
    c.face["brow"].update(r=.0075, tilt=.009, arch=.004)
    locks = ring_locks(c, 11, 58, 302, 30, -22, .05, tip_out=.018, curl=-6)
    locks += bangs(c, 7, 42, 40, 30, .032, jitter=(2, 7, 0, 9, 3, 6, 1), tip_out=.015, curl=-5)
    hair = hair_pieces(locks, hair_cap(c, front=.06, back=-.07, tris=2000), "HairBrown")
    cap_ = Shape().add(Ellipsoid((0, y + .008, cz + .1), (.165, .156, .085)), None)
    cap_.inter(Plane((0, 0, cz + .07), (0, 0, -1)), .01)
    cap_.add(Xf(Ellipsoid((0, y - .15, cz + .078), (.115, .08, .012)), vec(0, y - .12, cz + .078), rot_x(-12)), None, .018)
    cap_.add(Sphere((0, y + .008, cz + .183), .014), None, .006)
    build_head(c, hair, [sdf(cap_, "HairMint", .004, 1800)])
    return c


def peach():
    """Bot_Peach: cheerful grandmother, wavy cream hair, bun + cherry, peach headband, curled lashes."""
    c = chibi("Bot_Peach_Float", r=(.155, .147, .158), y=-.045)
    cz, r, y = c.head
    rig(c, (.125, -.03, .935), (.172, -.07, .745), (.085, -.37, .79), .98)
    face(c, "IrisTeal", "HairBrown", lashes=True, smile=.014, open_=.016)
    sc = c.scalp
    locks = []
    for az in np.linspace(-80, 80, 9):
        side = 1 if az > 0 else -1
        locks.append(sc.lock((az, 30), (az * 1.4 + 12 * side, 62), .05, bulge=.016, tip_out=.012, curl=10 * side))
    locks += ring_locks(c, 9, 95, 265, 50, -30, .06, tip_out=.016, curl=10)
    for s in (-1, 1):
        locks.append(sc.lock((s * 75, 20), (s * 95, -35), .045, bulge=.01, tip_out=.03, curl=s * 20))
    hair = hair_pieces(locks, hair_cap(c, front=.09, back=-.12, dilate=.012, tris=2600), "HairCream")
    hair.append(sdf(Shape().add(Torus((0, y + .01, cz + .175), .055, .028)).add(Sphere((0, y + .01, cz + .21), .042), None, .02), "HairCream"))
    band = Shape(dilate=.013).add(Ellipsoid((0, y + .006, cz + .022), (r[0], r[1], r[2] * .9)))
    band.inter(Torus((0, y + .02, cz + .1), .14, .014, art.frame(unit((0, .55, 1)))), .004)
    hair += [sdf(band, "Peach", .003, 900), sdf(Shape().add(Sphere((0, y + .01, cz + .262), .024)), "Red", .002, 400),
             sdf(Shape().add(Cone((0, y + .01, cz + .28), (.012, y + .03, cz + .32), .004, .0028)), "WoodDark", .0015, 200)]
    build_head(c, hair)
    return c


def waiter():
    """Waiter: friendly chef, tall chef hat, spiky brown side hair, curled mustache, floating bow tie."""
    c = chibi("Waiter_Float", cz=1.47, r=(.155, .147, .158), y=-.02, ears=True)
    cz, r, y = c.head
    rig(c, (.175, 0, 1.27), (.22, 0, 1.03), (.10, -.19, .95), 1.05)
    face(c, "IrisBrown", "HairBrownDark", eye=.92, smile=.015, open_=.016, mouth_w=.05)
    locks = ring_locks(c, 12, 60, 300, 20, -26, .045, tip_out=.012, curl=-8)
    hair = hair_pieces(locks, hair_cap(c, front=.06, back=-.08, tris=1800), "HairBrown")
    hat = Shape().add(Cyl((0, y, cz + .125), .118, .035, .01), None)
    for p, rr in (((.06, y, cz + .23), .075), ((-.06, y, cz + .23), .075), ((0, y - .055, cz + .235), .072),
                  ((0, y + .055, cz + .235), .072), ((0, y, cz + .28), .082)):
        hat.add(Sphere(p, rr), None, .03)
    hat.add(Cyl((0, y, cz + .175), .108, .045, .01), None, .02)
    knot = vec(0, y - .07, cz - r[2] - .02)
    bow = Shape()
    for s in (-1, 1):
        bow.add(Cone(knot, knot + vec(s * .055, .004, 0), .014, .033), None, .006)
    bow.add(Sphere(knot + vec(0, -.004, 0), .017), None, .004)
    build_head(c, hair, [sdf(hat, "Milk", .004, 2000), sdf(bow, "Red", .0022, 900), mustache(c, cz - .097, .058, .0075, .022, "HairBrown")])
    return c


def avatar():
    """Player avatar: pointed anime-style brown bangs + ahoge; own head hidden from the VR camera."""
    c = chibi("Avatar_Float", r=(.152, .146, .162), y=-.04, ears=True)
    rig(c, (.15, -.01, .955), (.215, -.06, .745), (.15, -.37, .79), 1.08)
    face(c, "IrisTeal", "HairChestnutDark", smile=.014, open_=.016)
    sc = c.scalp
    locks = bangs(c, 8, 55, 78, 18, .036, curl=8)
    locks += ring_locks(c, 10, 70, 290, 55, -28, .055, tip_out=.012, curl=8)
    for s in (-1, 1):
        locks.append(sc.lock((s * 66, 40), (s * 78, -22), .035, bulge=.006, tip_out=.012))
    locks.append(sc.lock((-5, 86), (20, 89), .012, lift=.01, bulge=.03, tip_out=.05, n=8))
    build_head(c, hair_pieces(locks, hair_cap(c, front=.07, back=-.08), "HairChestnut"))
    return c


DESIGNS = [yellow, blue, red, mint, peach, waiter, avatar]


# ---------------------------------------------------------------------------------------------------
def finish(c):
    """Weight glove fingers, remove the Hips bone, export FBX."""
    arm = c.arm
    names = [b[0] for b in c.bones]
    for obj in list(arm.children_recursive):
        if obj.type == "MESH" and obj.name.startswith("Glove_"):
            side = -1 if obj.name.endswith("_L") else 1
            suffix = "Left" if side < 0 else "Right"
            wrist = c.bone_pos("Hand_" + suffix)
            shape = hand_shape(wrist, wrist - c.bone_pos("Forearm_" + suffix), side, c.hands["scale"])
            vertices = np.array([tuple(obj.matrix_world @ v.co) for v in obj.data.vertices])
            obj.parent_type = "OBJECT"
            obj.matrix_world = Matrix.Identity(4)
            for v, position in zip(obj.data.vertices, vertices):
                v.co = position
            modifier = obj.modifiers.new("Finger Skin", "ARMATURE")
            modifier.object = arm
            art.apply_weights(obj, names, art.skin_weights(vertices, shape.parts, names, .006))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    arm.data.edit_bones["Spine"].parent = None
    arm.data.edit_bones.remove(arm.data.edit_bones["Hips"])
    bpy.ops.object.mode_set(mode="OBJECT")
    art.export(c)
    stats = {"triangles": 0, "bones": len(arm.data.bones), "meshes": []}
    for obj in arm.children_recursive:
        if obj.type == "MESH":
            obj.data.calc_loop_triangles()
            stats["triangles"] += len(obj.data.loop_triangles)
            stats["meshes"].append(obj.name)
    need = {"HeadSkin", "Nose", "Mouth", "HeadGear", "Glove_L", "Glove_R"} | {f"{p}_{s}" for p in ("EyeWhite", "Pupil", "Lid", "Brow") for s in "LR"}
    assert need <= set(stats["meshes"]), need - set(stats["meshes"])
    return stats


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    only = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None
    stats, chars = {}, []
    for design in DESIGNS:
        if only and design.__name__ not in only:
            continue
        c = design()
        stats[c.name] = finish(c)
        for o in c.arm.children_recursive:
            o.name = c.name + "." + o.name
        if "--preview" in argv:
            chars.append(c)
        else:
            for obj in list(bpy.data.objects):
                bpy.data.objects.remove(obj, do_unlink=True)
    if not only:
        with open(os.path.join(art.ART, "floating_manifest.json"), "w") as output:
            json.dump(stats, output, indent=2)
    if "--preview" in argv:
        art.preview(chars, argv[argv.index("--preview") + 1])
    print("Floating assets:", json.dumps({k: v["triangles"] for k, v in stats.items()}))


if __name__ == "__main__":
    main()
