import bpy,bmesh,json,sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/BiteOrBetray/NewInterior';OUT.mkdir(parents=True,exist_ok=True)
LOG=ROOT/'Logs/NewInterior';LOG.mkdir(parents=True,exist_ok=True)
SOURCE=Path(r'C:\Users\acer\Downloads\Interior')
# Blender dimensions are width, depth, height. Wall assets face -Y and have their back at Y=0.
ITEMS={
 'Table':('Meja',(3.24,3.24,.760)),
 'Chair':('Kursi',(.60,.62,1.18)),
 'Door':('Pintu',None),
 'Window':('Jendela',None),
 'Rug':('Karpet',(4.85,4.50,.008)),
 'WallPanel':('Panel DInding',(1.40,.14,3.10)),
 'Shelves':('Rak kosong',None),
 'Display':('Rak Display',None),
 'Sconce':('Lampu DInding',None),
 'Picture':('Bingkai Foto',None),
 'Floor':('Modul Lantai',(7.4,7.4,.012)),
}
HEIGHTS={'Chair':1.18,'Door':2.55,'Window':1.85,'Shelves':1.20,'Display':1.20,'Sconce':.52,'Picture':.43}
report=[]
for name,(folder,size) in ITEMS.items():
 if '--only' in sys.argv and name not in sys.argv[sys.argv.index('--only')+1].split(','):continue
 bpy.ops.wm.read_factory_settings(use_empty=True)
 path=next((SOURCE/folder).glob('*_texture.glb'));bpy.ops.import_scene.gltf(filepath=str(path))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
 lo=Vector([min(p[i] for p in points) for i in range(3)]);hi=Vector([max(p[i] for p in points) for i in range(3)])
 dims=hi-lo;scale=Vector([size[i]/dims[i] for i in range(3)]) if size else Vector((1,1,1))*(HEIGHTS[name]/dims.z)
 if name in ['Door','Window','Shelves','Display','Sconce','Picture']:
  scale.y={'Door':.20,'Window':.18,'Shelves':.30,'Display':.26,'Sconce':.23,'Picture':.035}[name]/dims.y
 center=(lo+hi)*.5;center.z=lo.z
 if name in ['Door','Window','WallPanel','Shelves','Display','Sconce','Picture']:center.y=hi.y
 for obj in meshes:
  matrix=obj.matrix_world.copy()
  for v in obj.data.vertices:
   p=matrix@v.co-center;v.co=Vector([p[i]*scale[i] for i in range(3)])
  obj.parent=None;obj.matrix_world.identity();obj.name='Interior'+name
  # Weld UV-seam duplicates before repairing inconsistent triangle winding from reconstruction.
  bm=bmesh.new();bm.from_mesh(obj.data)
  bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
  bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
  bm.to_mesh(obj.data);bm.free()
  obj.data.normals_split_custom_set([(0,0,0)]*len(obj.data.loops))
  obj.data.update()
  normal_data=[n.vector.copy() for n in obj.data.corner_normals]
  # Large planar faces must not inherit the rounded bevel's lighting across a whole tabletop or wall.
  for face in obj.data.polygons:
   axis=2 if name in ['Table','Floor','Rug'] else 1 if name in ['WallPanel','Door','Window','Picture'] else None
   if axis is not None and abs(face.normal[axis])>.98:
    flat=Vector((0,0,0));flat[axis]=1 if face.normal[axis]>0 else -1
    for loop in face.loop_indices:normal_data[loop]=flat
  obj.data.normals_split_custom_set(normal_data)
 if name=='Door':
  for obj in meshes:bpy.data.objects.remove(obj,do_unlink=True)
  from bob_polished_door import build_door
  meshes=build_door()
 if name in ['Window','Shelves','Display']:
  for obj in meshes:bpy.data.objects.remove(obj,do_unlink=True)
  from bob_polished_wall_furniture import build_wall_furniture
  meshes=build_wall_furniture(name)
 if name in ['Table','Chair']:
  for obj in meshes:bpy.data.objects.remove(obj,do_unlink=True)
  from bob_polished_table_chair import build_table_chair
  meshes=build_table_chair(name)
 if name in ['Sconce','Picture']:
  for obj in meshes:bpy.data.objects.remove(obj,do_unlink=True)
  from bob_polished_wall_decor import build_wall_decor
  meshes=build_wall_decor(name)
 if name in ['Floor','Rug']:
  for obj in meshes:bpy.data.objects.remove(obj,do_unlink=True)
  from bob_polished_floor_rug import build_floor_rug
  meshes=build_floor_rug(name)
 materials=[]
 for index,mat in enumerate(dict.fromkeys(m for o in meshes for m in o.data.materials if m)):
  mat.name='Interior'+name+('_'+str(index) if index else '')
  image=next((n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name=='sRGB'),None)
  assert image is not None,(name,'missing color texture')
  image.pack();image.filepath_raw=str(OUT/(mat.name+'_BaseColor.png'));image.file_format='PNG';image.save()
  materials.append(mat.name)
 if name=='Sconce':
  emission=bpy.data.images['InteriorSconceEmission'];emission.filepath_raw=str(OUT/'InteriorSconce_Emission.png');emission.file_format='PNG';emission.save()
 bpy.ops.object.select_all(action='DESELECT')
 for obj in meshes:obj.select_set(True)
 bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_anim=False,mesh_smooth_type='FACE',path_mode='STRIP')
 exported_points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
 actual=[max(v[i] for v in exported_points)-min(v[i] for v in exported_points) for i in range(3)]
 tris=sum(len(p.vertices)-2 for obj in meshes for p in obj.data.polygons)
 assert 0<tris<12000,(name,tris)
 report.append(dict(name=name,source=str(path),triangles=tris,width=actual[0],depth=actual[1],height=actual[2],materials=materials))
 if '--no-preview' in sys.argv:continue
 # Native front render verifies the source orientation and albedo before placement in Unity.
 for obj in meshes:
  for mat in obj.data.materials:
   tex=next(n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name=='sRGB')
   emission=mat.node_tree.nodes.new('ShaderNodeEmission');mat.node_tree.links.new(tex.outputs['Color'],emission.inputs['Color']);mat.node_tree.links.new(emission.outputs[0],next(n for n in mat.node_tree.nodes if n.type=='OUTPUT_MATERIAL').inputs[0])
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1;scene.render.resolution_x=scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.render.film_transparent=True;scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
 target=Vector((0,-actual[1]*.3 if name not in ['Table','Chair','Rug','Floor'] else 0,actual[2]*.5))
 bpy.ops.object.camera_add(location=target+Vector((.20,-1,.28 if name not in ['Table','Rug','Floor'] else .75))*max(actual)*2)
 camera=bpy.context.object;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=max(actual)*1.28;scene.camera=camera
 scene.render.filepath=str(LOG/(name+'-source.png'));bpy.ops.render.render(write_still=True)
previous=json.loads((OUT/'manifest.json').read_text()) if (OUT/'manifest.json').exists() else []
merged={entry['name']:entry for entry in previous};merged.update({entry['name']:entry for entry in report})
(OUT/'manifest.json').write_text(json.dumps(list(merged.values()),indent=2));print('INTERIOR_EXPORT_COMPLETE',json.dumps(report))
