import bpy,sys,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'ArtSource/NewCharacters/Projections';OUT.mkdir(parents=True,exist_ok=True);INPUT=Path(r'C:\Users\acer\Downloads\New Character')
items={'Blond':'Main Blond','Silver':'Silver','Mint':'Mint','Navy':'Navy','Peach':'Peach','Charcoal':'Charcoal cyan','HandRight':'Tangan kanan','HandLeft':'Tangan Kiri'}
for name,folder in items.items():
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(next((INPUT/folder).glob('*_texture.glb'))))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];coords=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
 lo=Vector([min(v[i] for v in coords) for i in range(3)]);hi=Vector([max(v[i] for v in coords) for i in range(3)]);center=(hi+lo)/2;h=.2 if name.startswith('Hand') else .6
 if name.startswith('Hand'):center.z=lo.z
 for o in meshes:
  matrix=o.matrix_world.copy()
  for v in o.data.vertices:v.co=(matrix@v.co-center)*h/(hi.z-lo.z)
  o.parent=None;o.matrix_world.identity()
  for mat in o.data.materials:
   nodes=mat.node_tree.nodes;tex=next(n for n in nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name=='sRGB');em=nodes.new('ShaderNodeEmission');mat.node_tree.links.new(tex.outputs['Color'],em.inputs['Color']);mat.node_tree.links.new(em.outputs[0],next(n for n in nodes if n.type=='OUTPUT_MATERIAL').inputs[0])
 s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=1;s.render.resolution_x=s.render.resolution_y=800;s.render.resolution_percentage=100;s.render.film_transparent=True;s.view_settings.view_transform='Standard';s.view_settings.look='None'
 bpy.ops.object.camera_add(location=(0,-2,.1 if h==.2 else 0));c=bpy.context.object;c.rotation_euler=(Vector((0,0,.1 if h==.2 else 0))-c.location).to_track_quat('-Z','Y').to_euler();c.data.type='ORTHO';c.data.ortho_scale=.24 if h==.2 else .72;s.camera=c
 s.render.filepath=str(OUT/(name+'-front.png'));bpy.ops.render.render(write_still=True)
 print('PROBE',name,[(o.name,len(o.data.vertices)) for o in meshes])
