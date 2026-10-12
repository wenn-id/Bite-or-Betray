"""Body avatar revision, derived from the existing reference scene."""
import sys
import math
import json
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector, Matrix

HERE = Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import bob_characters as art
import bob_floating as floating

OUT = Path(r'C:\Users\acer\Documents\Codex\2026-10-04\buk\outputs\BiteOrBetray_VisualPrototype')
UNITY = HERE.parent.parent/'Assets/BiteOrBetray/VisualPrototype'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'BiteOrBetray_Cartoon_VisualProof.blend'))
scene = bpy.context.scene
scene.frame_set(1)
arm = bpy.data.objects['Cartoon_Hero']
arm.name = 'Cartoon_Hero_WithBody'
arm.location = (0,0,0)
bpy.context.view_layer.update()
for material in bpy.data.materials:
    if material.name.startswith('Cartoon_'): art._materials[material.name.removeprefix('Cartoon_')] = material
trouser=art._materials['Green'].copy();trouser.name='Cartoon_Trouser'
trouser.diffuse_color=(.024,.045,.031,1)
trouser.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=trouser.diffuse_color
art._materials['Trouser']=trouser
hero = bpy.data.collections['Hero']

def place(obj):
    for collection in list(obj.users_collection): collection.objects.unlink(obj)
    hero.objects.link(obj)

def mesh(name,shape,material,triangles,step=.005,bone=None):
    vertices,tri = art.polygonize(shape,step,triangles,symmetric=False)
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices.tolist(),[],tri.tolist())
    data.materials.append(art._materials[material]); data.shade_smooth()
    obj = bpy.data.objects.new(name,data);hero.objects.link(obj)
    if bone:
        obj.parent=arm;obj.parent_type='BONE';obj.parent_bone=bone
        bpy.context.view_layer.update();obj.matrix_world=Matrix.Identity(4)
    return obj,vertices

c = art.Character(arm.name,'Skin');c.arm=arm
for bone in arm.data.bones:
    c.bone(bone.name,bone.head_local,bone.tail_local,bone.parent.name if bone.parent else None)
c.hands={'scale':1.4}
floating.add_fingers(c)
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT')
hips=arm.data.edit_bones.new('Hips');hips.head=(0,.02,.6);hips.tail=(0,.02,.8)
arm.data.edit_bones['Spine'].parent=hips
for name,head,tail,parent in c.bones:
    if name in arm.data.edit_bones: continue
    b=arm.data.edit_bones.new(name);b.head=head;b.tail=tail;b.parent=arm.data.edit_bones[parent]
for side,suffix,short in art.SIDES:
    for name,head,tail,parent in [
        ('Thigh_'+suffix,(side*.18,0,.64),(side*.18,-.43,.49),'Hips'),
        ('Shin_'+suffix,(side*.18,-.43,.49),(side*.18,-.44,.12),'Thigh_'+suffix),
        ('Foot_'+suffix,(side*.18,-.44,.12),(side*.18,-.59,.10),'Shin_'+suffix)]:
        b=arm.data.edit_bones.new(name);b.head=head;b.tail=tail;b.parent=arm.data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')

for obj in list(hero.objects):
    if obj.name.startswith('Hand_') and obj.type=='MESH': bpy.data.objects.remove(obj,do_unlink=True)
    elif obj.name.startswith('Sleeve cuff '): bpy.data.objects.remove(obj,do_unlink=True)
names=[b.name for b in arm.data.bones]
for side,suffix,short in art.SIDES:
    wrist=c.bone_pos('Hand_'+suffix)
    shape=floating.hand_shape(wrist,wrist-c.bone_pos('Forearm_'+suffix),side,c.hands['scale'])
    obj,vertices=mesh('BodyHand_'+short,shape,'Skin',3600,.0028)
    obj.parent=arm
    obj.modifiers.new('Finger Skin','ARMATURE').object=arm
    art.apply_weights(obj,names,art.skin_weights(vertices,shape.parts,names,.006))
    forward=art.unit(wrist-c.bone_pos('Forearm_'+suffix))
    bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=.072,depth=.035,location=Vector(wrist)-Vector(forward)*.006)
    cuff=bpy.context.object;cuff.name='Sleeve cuff '+suffix
    cuff.rotation_euler=Vector(forward).to_track_quat('Z','Y').to_euler()
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bevel=cuff.modifiers.new('Cuff rim','BEVEL');bevel.width=.002;bevel.segments=2
    cuff.data.materials.append(art._materials['Shirt']);cuff.data.shade_smooth();place(cuff)
    transform=cuff.matrix_world.copy();cuff.parent=arm;cuff.parent_type='BONE';cuff.parent_bone='Forearm_'+suffix
    bpy.context.view_layer.update();cuff.matrix_world=transform
    axis=Vector(np.cross(forward,-art.UP))
    for bone in arm.pose.bones:
        if bone.name.startswith('Finger_') and bone.name.endswith('_'+suffix):
            bone.rotation_mode='AXIS_ANGLE'
            local=bone.bone.matrix_local.to_3x3().inverted()@axis
            local.normalize()
            curl=(30 if 'Thumb' in bone.name else 85 if 'Proximal' in bone.name else 50) if side<0 else 12
            angle=math.radians(curl)
            bone.rotation_axis_angle=(angle,*local)

pants=art.Shape().add(art.Ellipsoid((0,.02,.64),(.31,.22,.17)),'Hips')
pants.add(art.Ellipsoid((0,.02,.77),(.29,.19,.15)),'Hips',.045)
for side,suffix,short in art.SIDES:
    pants.add(art.Cone((side*.18,0,.62),(side*.18,-.43,.49),.135,.112),'Thigh_'+suffix,.04)
    pants.add(art.Cone((side*.18,-.43,.49),(side*.18,-.44,.13),.108,.075),'Shin_'+suffix,.025)
    shoe=art.Shape().add(art.RBox((side*.18,-.52,.075),(.103,.166,.07),.04))
    mesh('Shoe_'+suffix,shoe,'Ink',1000,bone='Foot_'+suffix)
obj,vertices=mesh('Seated trousers',pants,'Trouser',6400,.005)
obj.parent=arm;obj.modifiers.new('Leg Skin','ARMATURE').object=arm
art.apply_weights(obj,names,art.skin_weights(vertices,pants.parts,names,.02))

for obj in hero.objects:
    if obj.type!='MESH' or not obj.data.shape_keys: continue
    obj.data.shape_keys.animation_data_clear()
    for key in obj.data.shape_keys.key_blocks:key.value=0
    if obj.name.startswith('Lid_'):
        eye=bpy.data.objects['EyeWhite_'+obj.name[-1]]
        x,y,z=eye.matrix_world.translation
        points=[]
        for u in np.linspace(-.999,.999,33):
            h=math.sqrt(max(0,1-u*u))
            for t in np.linspace(0,1,9):
                v=-h*(1-t)+h*t
                points.append(obj.matrix_world.inverted()@Vector((x+.054*u,y-.030*math.sqrt(max(0,1-u*u-v*v))-.017,z+.033*v)))
        key=obj.shape_key_add(name='Blink');key.data.foreach_set('co',np.asarray(points).ravel());key.value=0
        obj.name='Expression'+obj.name

bpy.ops.object.select_all(action='DESELECT')
for obj in hero.objects:obj.select_set(True)
finger_pose={b.name:tuple(b.rotation_axis_angle) for b in arm.pose.bones if b.name.startswith('Finger_')}
for name in finger_pose:arm.pose.bones[name].rotation_axis_angle=(0,0,1,0)
bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=str(UNITY/'Cartoon_Hero_WithBody_Prototype.fbx'),use_selection=True,
    object_types={'MESH','ARMATURE'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',
    add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE',use_mesh_modifiers=True,primary_bone_axis='Y',secondary_bone_axis='X')
for name,pose in finger_pose.items():arm.pose.bones[name].rotation_axis_angle=pose
bpy.context.view_layer.update()
arm.location=(0,1.52,0)
arm['production_status']='Body avatar revision: connected torso, arms, finger rig and seated legs. Reference pose.'
scene['production_status']='Body avatar is the current visual direction. Floating version is an archived alternative.'
scene.camera=bpy.data.objects['Comparison_Seated']
scene.cycles.samples=32
scene.render.resolution_x=1920;scene.render.resolution_y=1080
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BiteOrBetray_Hero_WithBody.blend'))
scene.render.filepath=str(OUT/'BiteOrBetray_WithBody_Scene_Blender.png');bpy.ops.render.render(write_still=True)
portrait=bpy.data.objects['Hero_Portrait'];portrait.location=(.36,-.10,1.72)
portrait.rotation_euler=(Vector((0,1.42,1.27))-portrait.location).to_track_quat('-Z','Y').to_euler()
portrait.data.lens=42;scene.camera=portrait
scene.render.resolution_x=1100;scene.render.resolution_y=1100
scene.render.filepath=str(OUT/'BiteOrBetray_WithBody_Portrait_Blender.png');bpy.ops.render.render(write_still=True)
for collection in bpy.data.collections:
    if collection.name not in ['Hero','Lighting']: collection.hide_render=True
studio=bpy.data.objects.new('Body silhouette camera',bpy.data.cameras.new('Body silhouette camera'));scene.collection.objects.link(studio)
studio.location=(.6,-1.7,1.4);studio.rotation_euler=(Vector((0,1.4,.97))-studio.location).to_track_quat('-Z','Y').to_euler();studio.data.lens=43
scene.camera=studio;scene.render.resolution_x=1000;scene.render.resolution_y=1200
scene.render.filepath=str(OUT/'BiteOrBetray_WithBody_Full_Blender.png');bpy.ops.render.render(write_still=True)
for collection in bpy.data.collections:collection.hide_render=False
scene.camera=bpy.data.objects['Comparison_Seated'];scene.render.resolution_x=1920;scene.render.resolution_y=1080
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BiteOrBetray_Hero_WithBody.blend'))
stats={'bones':len(arm.data.bones),'finger_bones':sum(b.name.startswith('Finger_') for b in arm.data.bones),
    'body_meshes':[o.name for o in hero.objects if o.type=='MESH' and not (o.parent_type=='BONE' and o.parent_bone=='Head')],
    'shape_keys':sum(len(o.data.shape_keys.key_blocks)-1 for o in hero.objects if o.type=='MESH' and o.data.shape_keys)}
assert stats['bones']==35 and stats['finger_bones']==20 and stats['shape_keys']==9
(OUT/'Body_Blender_Checks.json').write_text(json.dumps(stats,indent=2))
print(json.dumps(stats),flush=True)
