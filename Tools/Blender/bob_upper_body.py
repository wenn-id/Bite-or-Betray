"""Seated variants of the existing characters; no lower body, articulated five-finger gloves.
Run: blender -b --python Tools/Blender/bob_upper_body.py
"""
import os
import sys
import json
import math
import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(__file__))
import bob_characters as art


def hand_shape(wrist, direction, side, scale=1, mitten=False, cuff=True):
    f = art.unit(direction)
    down = art.unit(-art.UP - f * (-art.UP @ f))
    lat = np.cross(f, -down)
    point = lambda a, b, d: wrist + (f * a + down * b + lat * d) * scale
    shape = art.Shape().add(art.Ellipsoid(point(.043, 0, 0), art.vec(.038, .040, .018) * scale,
        np.stack([lat, f, -down], 1)), "Hand_" + ("Left" if side < 0 else "Right"))
    for name, lateral, length in (("Index", -.027 * side, .107), ("Middle", -.009 * side, .116),
                                 ("Ring", .009 * side, .108), ("Little", .026 * side, .093)):
        a, b, c = point(.065, 0, lateral), point(.065 + (length - .065) * .55, .003, lateral), point(length, .006, lateral)
        shape.add(art.Cone(a, b, .0095 * scale, .0085 * scale), f"Finger_{name}_Proximal_" + ("Left" if side < 0 else "Right"), .005)
        shape.add(art.Cone(b, c, .0085 * scale, .007 * scale), f"Finger_{name}_Distal_" + ("Left" if side < 0 else "Right"), .004)
    a, b, c = point(.025, 0, -.031 * side), point(.048, .002, -.048 * side), point(.074, .005, -.051 * side)
    suffix = "Left" if side < 0 else "Right"
    shape.add(art.Cone(a, b, .012 * scale, .010 * scale), "Finger_Thumb_Proximal_" + suffix, .009)
    shape.add(art.Cone(b, c, .010 * scale, .008 * scale), "Finger_Thumb_Distal_" + suffix, .005)
    shape.add(art.Cone(point(-.020, 0, 0), point(.010, 0, 0), .033 * scale, .032 * scale), "Hand_" + suffix, .010)
    return shape


def add_fingers(character):
    for side, suffix, _ in art.SIDES:
        wrist = character.bone_pos("Hand_" + suffix)
        direction = wrist - character.bone_pos("Forearm_" + suffix)
        shape = hand_shape(wrist, direction, side, character.hands.get("scale", 1))
        for part in shape.parts:
            if not part.bone.startswith("Finger_"):
                continue
            parent = "Hand_" + suffix if "Proximal" in part.bone else part.bone.replace("Distal", "Proximal")
            character.bone(part.bone, part.prim.a, part.prim.b, parent)


def clip_mesh(obj, height, keep_upper):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    local_plane = obj.matrix_world.inverted() @ Vector((0, 0, height))
    normal = obj.matrix_world.to_3x3().transposed() @ Vector((0, 0, 1))
    result = bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
        plane_co=local_plane, plane_no=normal, dist=.00001, clear_inner=keep_upper, clear_outer=not keep_upper)
    edges = [e for e in result["geom_cut"] if isinstance(e, bmesh.types.BMEdge) and e.is_boundary]
    if edges:
        bmesh.ops.holes_fill(bm, edges=edges, sides=0)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def seated(character):
    add_fingers(character)
    art.build(character)
    arm = character.arm
    for obj in list(arm.children_recursive):
        if obj.type != "MESH":
            continue
        if obj.name.startswith("Shoes"):
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        clip_mesh(obj, .70, True)
        if obj.name == "Body":
            head = obj.copy()
            head.data = obj.data.copy()
            bpy.context.scene.collection.objects.link(head)
            head.name = "HeadSkin"
            clip_mesh(head, character.bone_pos("Head")[2] + .025, True)
            clip_mesh(obj, character.bone_pos("Head")[2] + .025, False)
        if obj.name.startswith("Glove_"):
            side = -1 if obj.name.endswith("_L") else 1
            suffix = "Left" if side < 0 else "Right"
            wrist = character.bone_pos("Hand_" + suffix)
            shape = hand_shape(wrist, wrist - character.bone_pos("Forearm_" + suffix), side, character.hands.get("scale", 1))
            vertices = np.array([tuple(obj.matrix_world @ v.co) for v in obj.data.vertices])
            obj.parent_type = "OBJECT"
            obj.matrix_world = Matrix.Identity(4)
            for v, position in zip(obj.data.vertices, vertices):
                v.co = position
            modifier = obj.modifiers.new("Finger Skin", "ARMATURE")
            modifier.object = arm
            names = [b[0] for b in character.bones]
            art.apply_weights(obj, names, art.skin_weights(vertices, shape.parts, names, .006))
    # The old Hips weights become Spine weights; the seated rig has no hip/leg bones.
    for obj in arm.children_recursive:
        if obj.type != "MESH":
            continue
        hips = obj.vertex_groups.get("Hips")
        if hips:
            spine = obj.vertex_groups.get("Spine") or obj.vertex_groups.new(name="Spine")
            for vertex in obj.data.vertices:
                weight = next((g.weight for g in vertex.groups if g.group == hips.index), 0)
                if weight:
                    spine.add([vertex.index], weight, "ADD")
            obj.vertex_groups.remove(hips)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    arm.data.edit_bones["Spine"].parent = None
    arm.data.edit_bones.remove(arm.data.edit_bones["Hips"])
    bpy.ops.object.mode_set(mode="OBJECT")
    art.export(character)
    stats = {"triangles": 0, "bones": len(arm.data.bones), "minimum_height": 10}
    for obj in arm.children_recursive:
        if obj.type == "MESH":
            obj.data.calc_loop_triangles()
            stats["triangles"] += len(obj.data.loop_triangles)
            if obj.data.vertices:
                stats["minimum_height"] = min(stats["minimum_height"], min((obj.matrix_world @ v.co).z for v in obj.data.vertices))
    assert stats["minimum_height"] >= .699 and "Hips" not in arm.data.bones
    return stats


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    art.glove = hand_shape
    stats = {}
    for design in art.DESIGNS:
        character = design()
        if character.name == "Waiter":
            continue
        character.name += "_Upper"
        stats[character.name] = seated(character)
        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
    character = art.taffy()
    character.name = "Avatar_UpperBody"
    character.skin = "Peach"
    character.face["nose"]["mat"] = "Peach"
    character.face["iris"] = "Teal"
    stats[character.name] = seated(character)
    with open(os.path.join(art.ART, "upper_body_manifest.json"), "w") as output:
        json.dump(stats, output, indent=2)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(art.PROJECT, "Tools", "Blender", "Avatar_UpperBody.blend"))
    print("Seated assets:", json.dumps(stats))


if __name__ == "__main__":
    main()
