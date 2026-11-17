import bpy,json,math,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent))
from bob_chibi_rig import face_rig,hand_rig,SPECS
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Assets/BiteOrBetray/NewCharacters';OUT.mkdir(parents=True,exist_ok=True)
INPUT=Path(r'C:\Users\acer\Downloads\New Character')
items={'Blond':'Main Blond','Silver':'Silver','Mint':'Mint','Navy':'Navy','Peach':'Peach','Charcoal':'Charcoal cyan','Reaper':'Pelayan','HandRight':'Tangan kanan','HandLeft':'Tangan Kiri'}
report=[]
for name,folder in items.items():
 if "--only" in sys.argv and name not in sys.argv[sys.argv.index("--only")+1].split(","):continue
 bpy.ops.wm.read_factory_settings(use_empty=True)
 path=next((INPUT/folder).glob('*_texture.glb'));bpy.ops.import_scene.gltf(filepath=str(path))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 coords=[o.matrix_world@v.co for o in meshes for v in o.data.vertices];lo=Vector([min(v[i] for v in coords) for i in range(3)]);hi=Vector([max(v[i] for v in coords) for i in range(3)])
 hand=name.startswith('Hand');height=.20 if hand else 1.55 if name=='Reaper' else .60
 scale=height/(hi.z-lo.z);center=(lo+hi)*.5
 if hand or name=='Reaper':center.z=lo.z
 for o in meshes:
  matrix=o.matrix_world.copy()
  for v in o.data.vertices:v.co=(matrix@v.co-center)*scale
  o.parent=None;o.matrix_world.identity();o.name=name+'Mesh'
  if name=='Reaper' and len(o.data.polygons)>50000:
   bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Game mesh','DECIMATE');mod.ratio=40000/len(o.data.polygons);bpy.ops.object.modifier_apply(modifier=mod.name)
 arm=None
 if name in SPECS:
  face_rig(name,meshes[0])
 elif hand:arm=hand_rig(name,meshes[0])
 mats=[]
 for i,m in enumerate(dict.fromkeys(m for o in meshes for m in o.data.materials if m)):
  tex=next((n.image for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name=='sRGB'),None)
  if tex is None:tex=next((n.image for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image),None)
  m.name=name+('_'+str(i) if i else '')
  if tex:
   tex.pack();tex.filepath_raw=str(OUT/(m.name+'_BaseColor.png'));tex.file_format='PNG';tex.save()
  mats.append({'name':m.name,'texture':m.name+'_BaseColor.png' if tex else ''})
 bpy.ops.object.select_all(action='DESELECT')
 for o in meshes:o.select_set(True)
 if arm:
  arm.select_set(True)
  for child in arm.children:
   if child.type=='EMPTY':child.select_set(True)
 bpy.ops.file.pack_all()
 bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Logs/ChibiRig'/(name+'_Rig.blend')))
 bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,use_armature_deform_only=False,use_mesh_modifiers=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_anim=False,mesh_smooth_type='FACE',path_mode='STRIP')
 report.append({'name':name,'materials':mats,'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),'height':height})
previous=json.loads((OUT/'manifest.json').read_text()) if (OUT/'manifest.json').exists() else []
merged={r['name']:r for r in previous};merged.update({r['name']:r for r in report})
(OUT/'manifest.json').write_text(json.dumps(list(merged.values()),indent=2));print('NEW_CHARACTERS_EXPORTED',report)
