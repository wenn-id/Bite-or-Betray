"""Derive the floating hero from the saved visual proof without changing that proof."""
import sys
import json
import math
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import bob_characters as art
import bob_floating as floating

OUT = Path(r'C:\Users\acer\Documents\Codex\2026-10-04\buk\outputs\BiteOrBetray_VisualPrototype')
UNITY = HERE.parent.parent / 'Assets/BiteOrBetray/VisualPrototype'
bpy.ops.wm.open_mainfile(filepath=str(OUT / 'BiteOrBetray_Cartoon_VisualProof.blend'))
scene = bpy.context.scene
scene.frame_set(1)
old = bpy.data.objects['Cartoon_Hero']
old.location = (0, 0, 0)
bpy.context.view_layer.update()
head = [o for o in old.children_recursive if o.type == 'MESH' and o.parent_type == 'BONE' and o.parent_bone == 'Head']
assert {'HeadSkin', 'Mouth', 'Lid_L', 'Lid_R'} <= {o.name for o in head}
world = {o: o.matrix_world.copy() for o in head}
for o in list(bpy.data.objects):
    if o not in head:
        bpy.data.objects.remove(o, do_unlink=True)
for o in head:
    o.parent = None
    o.matrix_world = world[o]
    if o.data.shape_keys:
        o.data.shape_keys.animation_data_clear()
        for key in o.data.shape_keys.key_blocks:
            key.value = 0
for o in head:
    if o.name.startswith('Lid_'):
        short = o.name[-1]
        eye = next(e for e in head if e.name == 'EyeWhite_'+short)
        x,y,z = eye.matrix_world.translation
        points = []
        for u in np.linspace(-.999,.999,33):
            h = math.sqrt(max(0,1-u*u))
            for t in np.linspace(0,1,9):
                v = -h*(1-t)+h*t
                p = Vector((x+.054*u,y-.030*math.sqrt(max(0,1-u*u-v*v))-.017,z+.033*v))
                points.append(o.matrix_world.inverted() @ p)
        key = o.shape_key_add(name='Blink')
        key.data.foreach_set('co',np.asarray(points).ravel())
        key.value = 0

c = art.Character('Cartoon_Hero_Floating', 'Skin')
c.bone('Spine', (0, 0, .8), (0, 0, 1.24))
c.bone('Head', (0, 0, 1.31), (0, 0, 1.85), 'Spine')
for side, suffix, short in art.SIDES:
    shoulder, elbow, wrist = (side*.28, 0, 1.24), (side*.22, .08, .84), (side*.22, -.24, .90)
    c.bone('Arm_'+suffix, shoulder, elbow, 'Spine')
    c.bone('Forearm_'+suffix, elbow, wrist, 'Arm_'+suffix)
    c.bone('Hand_'+suffix, wrist, np.asarray(wrist) + art.unit(np.asarray(wrist)-elbow)*.07, 'Forearm_'+suffix)
c.hands = {'scale': 1.35}
floating.add_fingers(c)
bpy.ops.object.select_all(action='DESELECT')
c.arm = art.build_armature(c.name, c.bones)
for o in head:
    o.parent = c.arm
    o.parent_type = 'BONE'
    o.parent_bone = 'Head'
    bpy.context.view_layer.update()
    o.matrix_world = world[o]
    if o.name.startswith('Lid_'):
        o.name = 'Expression'+o.name
for material in bpy.data.materials:
    if material.name.startswith('Cartoon_'):
        art._materials[material.name.removeprefix('Cartoon_')] = material
names = [b[0] for b in c.bones]
for side, suffix, short in art.SIDES:
    wrist = c.bone_pos('Hand_'+suffix)
    shape = floating.hand_shape(wrist, wrist-c.bone_pos('Forearm_'+suffix), side, c.hands['scale'])
    vertices, triangles = art.polygonize(shape, .0028, 3600, symmetric=False)
    mesh = bpy.data.meshes.new('Glove_'+short)
    mesh.from_pydata(vertices.tolist(), [], triangles.tolist())
    mesh.materials.append(art._materials['Skin'])
    mesh.shade_smooth()
    obj = bpy.data.objects.new('Glove_'+short, mesh)
    scene.collection.objects.link(obj)
    obj.parent = c.arm
    modifier = obj.modifiers.new('Finger Skin', 'ARMATURE')
    modifier.object = c.arm
    art.apply_weights(obj, names, art.skin_weights(vertices, shape.parts, names, .006))

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(UNITY / 'Cartoon_Hero_Floating_Prototype.fbx'), use_selection=True,
    object_types={'MESH','ARMATURE'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False,
    mesh_smooth_type='FACE', use_mesh_modifiers=True, primary_bone_axis='Y', secondary_bone_axis='X')

def camera(name, position, target):
    obj = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    scene.collection.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    obj.data.lens = 48
    return obj

scene.camera = camera('Floating Avatar Preview', (.32,-2.5,1.72), (0,-.02,1.34))
for name, position, energy, color, size in [
    ('Warm key',(-2,-3,4),450,(1,.85,.69),4),
    ('Soft fill',(2,-1,2),250,(.72,.86,1),3),
    ('Hair rim',(0,2,3),500,(1,.83,.56),2)]:
    light = bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'))
    scene.collection.objects.link(light)
    light.location = position
    light.rotation_euler = (Vector((0,0,1.4))-light.location).to_track_quat('-Z','Y').to_euler()
    light.data.energy, light.data.color, light.data.shape, light.data.size = energy,color,'DISK',size
scene.world.color = (.16,.19,.20)
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.cycles.samples = 48
scene.frame_start, scene.frame_end = 1,72
scene['production_status'] = 'Floating avatar prototype. 28 bones, 20 finger joints; Unity simulated tracking only.'
scene['gameplay_status'] = 'Separate asset. Original gameplay scene and avatar unchanged.'
stats = {'bones':len(c.arm.data.bones),'finger_bones':sum(b.name.startswith('Finger_') for b in c.arm.data.bones),
    'head_meshes':len(head),'meshes':len(head)+2,'shape_keys':sum(len(o.data.shape_keys.key_blocks)-1 for o in head if o.data.shape_keys)}
assert stats['bones']==28 and stats['finger_bones']==20 and stats['shape_keys']==9
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D': area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'BiteOrBetray_Hero_Floating.blend'))
scene.render.filepath = str(OUT / 'BiteOrBetray_Hero_Floating_Blender.png')
bpy.ops.render.render(write_still=True)
(OUT / 'Floating_Blender_Checks.json').write_text(json.dumps(stats,indent=2))
print(json.dumps(stats),flush=True)
