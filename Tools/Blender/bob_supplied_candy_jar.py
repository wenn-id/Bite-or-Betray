"""Retexture the supplied Meshy jar and preserve its silhouette at 1,000 triangles."""
import bpy,bmesh,math,json,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/RetexturedCandyJar20261115';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=Path(r'C:\Users\acer\Downloads\Meshy_AI_Object_Glass_cookie_jar_filled_with_20261115_texture.blend')
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
source=next(o for o in bpy.context.scene.objects if o.type=='MESH')
for obj in list(bpy.context.scene.objects):
    if obj!=source:bpy.data.objects.remove(obj,do_unlink=True)
bpy.context.view_layer.objects.active=source;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
points=np.array([v.co for v in source.data.vertices]);lo=points.min(0);hi=points.max(0);center=(lo+hi)/2;center[2]=lo[2]
for vertex in source.data.vertices:vertex.co=Vector((np.array(vertex.co)-center)*(.30/(hi[2]-lo[2])))
original=[v.co.copy() for v in source.data.vertices]
before=sum(len(p.vertices)-2 for p in source.data.polygons)
modifier=source.modifiers.new('Shelf budget','DECIMATE');modifier.ratio=1000/before
bpy.ops.object.modifier_apply(modifier=modifier.name)
points=np.array([v.co for v in source.data.vertices]);lo=points.min(0);hi=points.max(0);center=(lo+hi)/2;center[2]=lo[2]
for vertex in source.data.vertices:vertex.co=Vector((np.array(vertex.co)-center)*(.30/(hi[2]-lo[2])))
source.data.normals_split_custom_set([(0,0,0)]*len(source.data.loops))
check=bmesh.new();check.from_mesh(source.data);bmesh.ops.recalc_face_normals(check,faces=list(check.faces))
assert all(edge.is_manifold for edge in check.edges),'Decimation opened the supplied jar'
check.to_mesh(source.data);check.free()
triangles=sum(len(p.vertices)-2 for p in source.data.polygons);assert triangles<=1000
tree=BVHTree.FromPolygons([v.co for v in source.data.vertices],[list(p.vertices) for p in source.data.polygons])
distances=np.array([tree.find_nearest(p)[3] for p in original])
assert np.percentile(distances,95)<.003 and distances.max()<.006,'Silhouette error exceeded the shelf limit'

pixels=np.ones((1024,1024,4),dtype=np.float32)
colors=[(.88,.018,.045),(.025,.25,.91),(.99,.67,.015)]
yy,xx=np.mgrid[0:256,0:1024];u=(xx+.5)/1024;t=np.clip((yy-8)/240,0,1);fill=np.clip((t-.055)/.595,0,1)
rng=np.random.default_rng(104)
for variant,base in enumerate(colors):
    panel=np.zeros((256,1024,3),dtype=np.float32);panel[:]=np.array(base)*.29
    for row in range(8):
        for col in range(24):
            cx=(col+.5+(row%2)*.5)/24+rng.uniform(-.004,.004);cy=(row-.2)/7+rng.uniform(-.016,.016)
            rx=rng.uniform(.0185,.022);ry=rx*.72/.1785
            for shift in [0]+([-1] if cx+rx>1 else [])+([1] if cx-rx<0 else []):
                dx=(u-cx-shift)/rx;dy=(fill-cy)/ry;distance=dx*dx+dy*dy
                coverage=np.clip((1-distance)*24,0,1)[...,None]
                shade=.32+.68*np.sqrt(np.clip(1-distance,0,1))
                shine=np.exp(-(((dx+.31)/.23)**2+((dy-.35)/.25)**2))*.63
                candy=np.clip(np.array(base)*shade[...,None]+shine[...,None],0,1)
                panel=panel*(1-coverage)+candy*coverage
    reflection=np.exp(-((u-.69)/.016)**2)*.17+np.exp(-((u-.20)/.009)**2)*.10
    panel=panel*(1-reflection[...,None])+np.array((.83,.94,.96))*reflection[...,None]
    def paint(mask,color):
        global panel
        mask=np.clip(mask,0,1)[...,None];panel=panel*(1-mask)+np.array(color)*mask
    ivory=(.93,.86,.72);gold=(.84,.51,.18);dark=(.32,.17,.075)
    paint(t<.055,ivory);paint((t>.035)&(t<.055),gold)
    paint(t>.64,ivory)
    for a,b in [(.672,.716),(.775,.811)]:paint((t>=a)&(t<b),gold)
    paint((t>=.716)&(t<.739),dark)
    # The source clasps sit at +/-X. Narrow gold pads follow their mounting strips.
    width=.10*np.sqrt(np.clip(1-((t-.63)/.22)**2,0,1))
    for center_u in [0,.5]:
        distance=np.abs((u-center_u+.5)%1-.5)
        paint(((width-distance)/.003)*((t>.41)&(t<.82)),gold)
    paint((t>.875)&(t<.900),gold);paint(t>=.900,base)
    pixels[variant*256:(variant+1)*256,:,:3]=np.clip(panel,0,1)
pixels[768:,:,:3]=(.93,.86,.72)
atlas=bpy.data.images.new('SuppliedCandyJarAtlas',width=1024,height=1024,alpha=True)
atlas.pixels.foreach_set(pixels.ravel());atlas.filepath_raw=str(OUT/'CandyJarShared_BaseColor.png');atlas.file_format='PNG';atlas.save();atlas.pack()
material=bpy.data.materials.new('CandyJarShared');material.use_nodes=True;material.use_backface_culling=True
texture=material.node_tree.nodes.new('ShaderNodeTexImage');texture.image=atlas
material.node_tree.links.new(texture.outputs['Color'],material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.32
source.data.materials.clear();source.data.materials.append(material)
for layer in list(source.data.uv_layers):source.data.uv_layers.remove(layer)
report=[];models=[]
for variant,label in enumerate(['Red','Blue','Yellow']):
    obj=source if variant==0 else source.copy()
    if variant:
        obj.data=source.data.copy();bpy.context.collection.objects.link(obj)
    obj.name='CandyJar'+label
    for layer in list(obj.data.uv_layers):obj.data.uv_layers.remove(layer)
    uv=obj.data.uv_layers.new(name='UVMap')
    for face in obj.data.polygons:
        face.use_smooth=True;face.material_index=0
        angles=[math.atan2(obj.data.vertices[obj.data.loops[i].vertex_index].co.y,obj.data.vertices[obj.data.loops[i].vertex_index].co.x)/math.tau%1 for i in face.loop_indices]
        seam=max(angles)-min(angles)>.5
        for index,a in zip(face.loop_indices,angles):
            z=obj.data.vertices[obj.data.loops[index].vertex_index].co.z/.30
            uv.data[index].uv=(a+1 if seam and a<.5 else a,(variant+(8+z*240)/256)/4)
    assert len(obj.data.materials)==1
    assert all(math.isfinite(c) for vertex in obj.data.vertices for c in vertex.co)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=str(OUT/(obj.name+'.fbx')),use_selection=True,object_types={'MESH'},apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='STRIP')
    bpy.ops.export_scene.gltf(filepath=str(OUT/(obj.name+'.glb')),export_format='GLB',use_selection=True,export_materials='EXPORT')
    report.append(dict(name=obj.name,triangles=triangles,materials=1,height=.30));models.append(obj)
for obj,x in zip(models,[-.29,0,.29]):obj.location.x=x
report_data=dict(source=str(SOURCE),source_triangles=before,optimized_triangles=triangles,p95_surface_error_mm=float(np.percentile(distances,95)*1000),maximum_surface_error_mm=float(distances.max()*1000),variants=report)
(OUT/'manifest.json').write_text(json.dumps(report_data,indent=2))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.use_denoising=True
scene.render.resolution_x=1200;scene.render.resolution_y=750;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Studio');scene.world.color=(.18,.18,.18);scene.view_settings.view_transform='AgX'
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.location.z=-.0005
floor_material=bpy.data.materials.new('Studio White');floor_material.diffuse_color=(.85,.85,.85,1);floor.data.materials.append(floor_material)
for location,power,size in [((-1,-1,2),140,2),((1,.5,1.2),80,1.5)]:
    bpy.ops.object.light_add(type='AREA',location=location);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((0,0,.15))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.4,-1.4,.66));camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,.15))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=1.12;scene.camera=camera
atlas.pack();bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CandyJars_Quest.blend'))
scene.render.filepath=str(OUT/'preview.png');bpy.ops.render.render(write_still=True)
print('SUPPLIED_JAR_COMPLETE',json.dumps(report_data))
