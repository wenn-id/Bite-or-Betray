import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Logs/ChibiRig';report={}
for name in ['Blond','Silver','Mint','Navy','Peach','Charcoal','HandLeft','HandRight']:
 bpy.ops.wm.open_mainfile(filepath=str(OUT/(name+'_Rig.blend')))
 obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
 if name.startswith('Hand'):
  arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');grip=bpy.data.objects['CandyGrip'];open_tip=arm.pose.bones['Finger_Middle_2'].tail.copy()
  for b in arm.pose.bones:
   if 'curlAxis' in b.bone:b.rotation_mode='AXIS_ANGLE';b.rotation_axis_angle=(math.radians(b.bone['curlDegrees']),*b.bone['curlAxis'])
  bpy.context.view_layer.update();closed_tip=arm.pose.bones['Finger_Middle_2'].tail.copy()
  a=(open_tip-grip.location).length;b=(closed_tip-grip.location).length
  assert b<a*.8,(name,a,b)
  for v in obj.data.vertices:assert abs(sum(g.weight for g in v.groups)-1)<.0001
  report[name]={'finger_joints':10,'open_tip_to_candy':a,'closed_tip_to_candy':b}
 else:
  keys=obj.data.shape_keys.key_blocks;basis=keys[0];assert all(k in keys for k in ['Blink','Open','Chew'])
  group=obj.vertex_groups['ProtectedHair'].index;hair=[v.index for v in obj.data.vertices if any(g.group==group for g in v.groups)]
  largest=0
  for k in keys[1:]:
   assert all(math.isfinite(c) for p in k.data for c in p.co)
   for i in hair:largest=max(largest,(k.data[i].co-basis.data[i].co).length)
  assert largest<1e-7,(name,largest)
  report[name]={'blendshapes':[k.name for k in keys[1:]],'protected_hair_vertices':len(hair),'max_hair_displacement':largest}
(OUT/'native-validation.json').write_text(json.dumps(report,indent=2));print('NATIVE_RIG_VALIDATION_PASSED',report)
