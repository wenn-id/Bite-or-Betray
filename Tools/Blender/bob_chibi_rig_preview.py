import bpy,sys,math,numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Logs/ChibiRig'
names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['Blond','Silver','HandRight','HandLeft']
for name in names:
 bpy.ops.wm.open_mainfile(filepath=str(OUT/(name+'_Rig.blend')))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 for o in meshes:
  for mat in o.data.materials:
   nodes=mat.node_tree.nodes;tex=next(n for n in nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name=='sRGB');em=nodes.new('ShaderNodeEmission');mat.node_tree.links.new(tex.outputs['Color'],em.inputs['Color']);mat.node_tree.links.new(em.outputs[0],next(n for n in nodes if n.type=='OUTPUT_MATERIAL').inputs[0])
 hand=name.startswith('Hand');s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=1;s.render.resolution_x=s.render.resolution_y=700;s.render.resolution_percentage=100;s.render.film_transparent=True;s.view_settings.view_transform='Standard';s.view_settings.look='None'
 bpy.ops.object.camera_add(location=(.22 if hand else 0,-.45 if hand else -2,.23 if hand else 0));cam=bpy.context.object;target=Vector((0,0,.105 if hand else 0));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=.26 if hand else .72;s.camera=cam
 if hand:
  arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
  for val,label in [(0,'open'),(1,'grip')]:
   for b in arm.pose.bones:
    if 'curlAxis' in b.bone:
     b.rotation_mode='AXIS_ANGLE';b.rotation_axis_angle=(math.radians(b.bone['curlDegrees'])*val,*b.bone['curlAxis'])
   s.render.filepath=str(OUT/(name+'-'+label+'.png'));bpy.ops.render.render(write_still=True)
 else:
  o=meshes[0]
  tex=next(n for n in o.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name=='sRGB')
  original=tex.image;w,h=original.size;base=np.array(original.pixels[:]).reshape(h,w,4)
  for label,values in [('neutral',{}),('blink',{'Blink':1}),('chew',{'Open':.8,'Chew':1})]:
   for k in o.data.shape_keys.key_blocks[1:]:k.value=values.get(k.name,0)
   pixels=base.copy()
   for expression,weight in [('Blink',values.get('Blink',0)),('Mouth',max(values.get('Open',0),values.get('Chew',0)*.7))]:
    if not weight:continue
    overlay=bpy.data.images.load(str(ROOT/'Assets/BiteOrBetray/NewCharacters'/(name+'_'+expression+'.png')))
    rgba=np.array(overlay.pixels[:]).reshape(h,w,4);alpha=rgba[:,:,3:]*weight
    pixels[:,:,:3]=pixels[:,:,:3]*(1-alpha)+rgba[:,:,:3]*alpha
   display=bpy.data.images.new(name+'_'+label,width=w,height=h);display.pixels.foreach_set(pixels.astype(np.float32).ravel());tex.image=display
   s.render.filepath=str(OUT/(name+'-'+label+'.png'));bpy.ops.render.render(write_still=True)
