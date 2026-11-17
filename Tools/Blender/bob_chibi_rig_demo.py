import bpy,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Logs/ChibiRig';FRAMES=OUT/'demo';FRAMES.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Blond_Rig.blend'))
head=next(o for o in bpy.context.scene.objects if o.type=='MESH');head.location=(-.12,0,0)
with bpy.data.libraries.load(str(OUT/'HandRight_Rig.blend'),link=False) as (src,dst):dst.objects=[n for n in src.objects if n.startswith('HandRight') or n=='CandyGrip']
for o in dst.objects:
 if o is not None and o.name not in bpy.context.scene.objects:bpy.context.collection.objects.link(o)
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');arm.location=(.30,-.04,-.26);arm.rotation_euler=(0,0,math.pi)
for obj in [o for o in bpy.context.scene.objects if o.type=='MESH']:
 for m in obj.data.materials:
  nodes=m.node_tree.nodes;tex=next((n for n in nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name=='sRGB'),None)
  if tex:
   em=nodes.new('ShaderNodeEmission');m.node_tree.links.new(tex.outputs['Color'],em.inputs['Color']);m.node_tree.links.new(em.outputs[0],next(n for n in nodes if n.type=='OUTPUT_MATERIAL').inputs[0])
bpy.context.view_layer.update();grip=bpy.data.objects['CandyGrip']
bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=.022,location=grip.matrix_world.translation);candy=bpy.context.object;candy.name='Candy held in glove'
m=bpy.data.materials.new('Candy Blue');m.use_nodes=True;m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.012,.15,.8,1);m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.22;candy.data.materials.append(m)
for p in candy.data.polygons:p.use_smooth=True
bpy.ops.object.light_add(type='AREA',location=(-.3,-1,1));light=bpy.context.object;light.data.energy=70;light.data.size=1
bpy.ops.object.camera_add(location=(.04,-2,.02));cam=bpy.context.object;cam.rotation_euler=(Vector((.04,0,.02))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.05
s=bpy.context.scene;s.camera=cam;s.render.engine='CYCLES';s.cycles.samples=4;s.cycles.use_denoising=True;s.render.resolution_x=900;s.render.resolution_y=700;s.render.resolution_percentage=100;s.render.film_transparent=False;s.world=bpy.data.worlds.new('Proof backdrop');s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs[0].default_value=(.085,.12,.15,1);s.world.node_tree.nodes['Background'].inputs[1].default_value=.5;s.view_settings.view_transform='Standard'
for f in range(30):
 blink=1 if f in [7,27] else .5 if f in [6,8,26,28] else 0
 chew=(.5+.5*math.sin((f-12)*1.1)) if 12<=f<=33 else 0
 for k in head.data.shape_keys.key_blocks[1:]:k.value={'Blink':blink,'Open':chew*.7,'Chew':(1-chew)*.75 if f>=12 else 0}.get(k.name,0)
 t=max(0,min(1,(f-3)/9));t=t*t*(3-2*t)
 for bone in arm.pose.bones:
  if 'curlAxis' in bone.bone:bone.rotation_mode='AXIS_ANGLE';bone.rotation_axis_angle=(math.radians(bone.bone['curlDegrees'])*t,*bone.bone['curlAxis'])
 bpy.context.view_layer.update();candy.location=grip.matrix_world.translation
 s.render.filepath=str(FRAMES/('frame-%03d.png'%f));bpy.ops.render.render(write_still=True)
print('RIG_DEMO_RENDERED')
