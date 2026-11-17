import bpy,bmesh,numpy as np,sys,math,json
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
import bob_meshy_faces as faces

SPECS={
 'Blond':dict(eyes=[(-.087,-.203,-.100),(.087,-.203,-.100)],rx=.080,mz=-.232,mr=.035,original_open=(-.265,-.226)),
 'Silver':dict(eyes=[(-.087,-.193,-.099),(.087,-.193,-.099)],rx=.080,mz=-.224,mr=.028),
 'Mint':dict(eyes=[(-.086,-.191,-.079),(.086,-.191,-.079)],rx=.085,mz=-.211,mr=.031,original_open=(-.260,-.202)),
 'Navy':dict(eyes=[(-.087,-.135,-.019),(.087,-.135,-.019)],rx=.087,mz=-.158,mr=.029),
 'Peach':dict(eyes=[(-.086,-.204,-.101),(.086,-.204,-.101)],rx=.080,mz=-.232,mr=.034,original_open=(-.260,-.227)),
 'Charcoal':dict(eyes=[(-.090,-.205,-.101),(.090,-.205,-.101)],rx=.082,mz=-.239,mr=.030),
}

def sample_colors(mesh,image):
 pixels=np.array(image.pixels[:],dtype=np.float32).reshape(image.size[1],image.size[0],4)
 uv=mesh.uv_layers.active.data
 co=np.array([uv[l].uv[:] for l in range(len(mesh.loops))]);xx=np.clip((co[:,0]*(image.size[0]-1)).astype(int),0,image.size[0]-1);yy=np.clip((co[:,1]*(image.size[1]-1)).astype(int),0,image.size[1]-1)
 cs=pixels[yy,xx,:3];sums=np.zeros((len(mesh.vertices),3));counts=np.zeros(len(mesh.vertices))
 for l,c in zip(mesh.loops,cs):sums[l.vertex_index]+=c;counts[l.vertex_index]+=1
 return sums/np.maximum(counts[:,None],1)

def hair_mask(name,mesh,image):
 c=sample_colors(mesh,image);c=np.where(c<=.0031308,c*12.92,1.055*np.maximum(c,0)**(1/2.4)-.055);r,g,b=c.T
 if name=='Blond':mask=(r-b>.08)&(g-b>.43*(r-b))
 elif name=='Mint':mask=(g>r*1.03)&(g>b*1.05)
 elif name=='Peach':mask=(r>.35)&(g<r*.64)
 else:mask=(b>g*1.06)&(b>r*.91)
 if name=='Charcoal':mask|=(g>r*1.4)&(b>r*1.4)
 z=np.array([v.co.z for v in mesh.vertices]);mask&=z>min(e[1] for e in SPECS[name]['eyes'])-.02
 return mask

def face_rig(name,obj):
 from bob_chibi_face_textures import expression_textures
 spec=SPECS[name];me=obj.data
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.000001);bm.to_mesh(me);bm.free()
 image=next(n.image for n in me.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name=='sRGB')
 co=np.array([v.co[:] for v in me.vertices]);x,y,z=co.T
 protected,front=expression_textures(name,obj,image,spec)
 obj.shape_key_add(name='Basis')
 mz=np.mean(spec['original_open']) if 'original_open' in spec else spec['mz']
 mouth=np.exp(-(x/(spec['mr']*2))**4-((z-mz)/.060)**4)*front*(~protected)
 for label in ['Blink','Open','Chew','Grin','Frown']:
  key=obj.shape_key_add(name=label);target=co.copy()
  if label=='Blink':
   for cx,zb,zt in spec['eyes']:
    w=np.exp(-((x-cx)/spec['rx'])**4-((z-(zb+zt)/2)/((zt-zb)*.65))**4)*front*(~protected)
    target[:,1]+=.0015*w
  elif label=='Open':target[:,2]-=.009*mouth*np.clip((mz+.018-z)/.045,0,1)
  elif label=='Chew':
   target[:,0]+=x*.055*mouth;target[:,2]+=.004*mouth;target[:,1]-=.002*mouth
  else:target[:,2]+=(.004 if label=='Grin' else -.004)*mouth*np.clip(np.abs(x)/spec['mr'],0,1)
  for v,p in zip(key.data,target):v.co=p
  key.value=0
  assert np.isfinite(target).all()
 group=obj.vertex_groups.new(name='ProtectedHair');group.add(np.nonzero(protected)[0].tolist(),1,'REPLACE')
 print('FACE_RIG',name,len(me.vertices),'protected',int(protected.sum()))

# Landmarks in normalized Blender space: cuff bottom z=0, tips near z=.2.
FINGERS_RIGHT=[('Little',(-.047,.113),(-.055,.136),(-.064,.157)),('Ring',(-.026,.132),(-.031,.161),(-.035,.187)),('Middle',(.001,.134),(.003,.168),(.005,.196)),('Index',(.026,.126),(.032,.158),(.039,.184)),('Thumb',(.033,.079),(.052,.099),(.071,.116))]
FINGERS_LEFT=[('Little',(.047,.112),(.055,.137),(.064,.157)),('Ring',(.028,.130),(.034,.161),(.040,.186)),('Middle',(.001,.133),(.002,.166),(.004,.195)),('Index',(-.024,.124),(-.031,.156),(-.037,.182)),('Thumb',(-.036,.080),(-.054,.099),(-.069,.115))]

def hand_rig(name,obj):
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 # Weld glTF seam duplicates so finger weights agree across UV islands.
 bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.00001);bm.to_mesh(obj.data);bm.free()
 landmarks=FINGERS_LEFT if name=='HandLeft' else FINGERS_RIGHT
 arm_data=bpy.data.armatures.new(name+'_Skeleton');arm=bpy.data.objects.new(name+'_Rig',arm_data);bpy.context.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True);obj.select_set(False);bpy.ops.object.mode_set(mode='EDIT')
 root=arm_data.edit_bones.new('Palm');root.head=(0,0,0);root.tail=(0,0,.075)
 for label,base,joint,tip in landmarks:
  first=arm_data.edit_bones.new('Finger_'+label+'_1');first.head=(base[0],0,base[1]);first.tail=(joint[0],0,joint[1]);first.parent=root
  second=arm_data.edit_bones.new('Finger_'+label+'_2');second.head=first.tail;second.tail=(tip[0],0,tip[1]);second.parent=first;second.use_connect=True
 bpy.ops.object.mode_set(mode='OBJECT');groups={b.name:obj.vertex_groups.new(name=b.name) for b in arm.data.bones}
 for v in obj.data.vertices:
  x,y,z=v.co;weights={};best=None
  for label,base,joint,tip in landmarks:
   a=Vector(base);b=Vector(tip);p=Vector((x,z));t=max(0,min(1,(p-a).dot(b-a)/(b-a).length_squared));dist=(p-(a+t*(b-a))).length
   # Thumb is lateral to the palm; its influence must not cross into index or cuff.
   eligible=z>base[1]-.012 and (label!='Thumb' or (x>base[0]-.006 if name=='HandRight' else x<base[0]+.006))
   if eligible and (best is None or dist<best[0]):best=(dist,label,base,joint,tip)
  if best is None or z<.065:weights={'Palm':1}
  else:
   dist,label,base,joint,tip=best
   start=float(faces.smooth01(base[1]-.014,base[1]+.007,z))
   p=Vector((x,z));a=Vector(base);j=Vector(joint);axis=(Vector(tip)-a).normalized();along=(p-j).dot(axis)
   second=float(faces.smooth01(-.009,.009,along))
   weights={'Palm':1-start,'Finger_'+label+'_1':start*(1-second),'Finger_'+label+'_2':start*second}
  for label,w in weights.items():
   if w>1e-7:groups[label].add([v.index],w,'REPLACE')
  assert abs(sum(weights.values())-1)<1e-5
 mod=obj.modifiers.new('Glove skin','ARMATURE');mod.object=arm;obj.parent=arm
 # These vectors are exported as bone properties for the Unity binding.
 for b in arm.data.bones:
  if not b.name.startswith('Finger_'):continue
  d=b.tail_local-b.head_local;axis=Vector((-d.z,0,d.x)).normalized();
  if 'Thumb_1' in b.name:axis=d.cross(Vector((-.02 if name=='HandRight' else .02,.03,.02))).normalized()
  axis_local=b.matrix_local.to_3x3().inverted()@axis
  b['curlAxis']=list(axis_local);b['curlDegrees']=65.0 if 'Thumb_1' in b.name else 50.0 if b.name.endswith('_1') else 65.0
 grip=bpy.data.objects.new('CandyGrip',None);bpy.context.collection.objects.link(grip);grip.parent=arm;grip.location=(0,.032,.113)
 return arm
