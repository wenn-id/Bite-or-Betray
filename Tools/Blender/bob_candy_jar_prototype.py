"""Shelf jar proof: opaque baked candy wrap and one shared atlas."""
import bpy,bmesh,math,numpy as np,json
from mathutils import Vector
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/CandyJarPrototype20261115';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
pixels=np.ones((1024,1024,4),dtype=np.float32)
colors=[(.84,.025,.07),(.035,.27,.92),(.99,.67,.025)]
yy,xx=np.mgrid[0:256,0:1024];u=(xx+.5)/1024;v=(yy+.5)/256
rng=np.random.default_rng(82)
for variant,base in enumerate(colors):
    panel=np.zeros((256,1024,3),dtype=np.float32);panel[:]=np.array(base)*.31
    for row in range(7):
        for col in range(16):
            cx=(col+.5+(row%2)*.5)/16+rng.uniform(-.007,.007);cz=(row-.15)/6.0+rng.uniform(-.022,.022)
            rx=rng.uniform(.029,.035);rz=rx*.46/.17
            for shift in [0]+([-1] if cx+rx>1 else [])+([1] if cx-rx<0 else []):
                dx=(u-cx-shift)/rx;dz=(v-cz)/rz;distance=dx*dx+dz*dz
                cover=np.clip((1-distance)*22,0,1)[...,None]
                sphere=.40+.60*np.sqrt(np.clip(1-distance,0,1))
                highlight=np.exp(-(((dx+.32)/.22)**2+((dz-.37)/.24)**2))*.75
                shade=np.clip(np.array(base)*sphere[...,None]+highlight[...,None],0,1)
                panel=panel*(1-cover)+shade*cover
    reflection=np.exp(-((u-.21)/.022)**2)*.13+np.exp(-((u-.70)/.013)**2)*.07
    panel=panel*(1-reflection[...,None])+np.array((.82,.94,.96))*reflection[...,None]
    pixels[variant*256:(variant+1)*256,:,:3]=panel
palette=[(.85,.53,.20),(.94,.87,.73),(.35,.70,.70),(.20,.11,.06),*colors,(.99,.96,.87)]
for i,color in enumerate(palette):pixels[768+(i//4)*128:768+(i//4+1)*128,(i%4)*256:(i%4+1)*256,:3]=color
atlas=bpy.data.images.new('CandyJarSharedAtlas',width=1024,height=1024,alpha=True);atlas.pixels.foreach_set(pixels.ravel())
atlas.filepath_raw=str(OUT/'CandyJarSharedAtlas.png');atlas.file_format='PNG';atlas.save();atlas.pack()
material=bpy.data.materials.new('CandyJarShared');material.use_nodes=True
nodes=material.node_tree.nodes;tex=nodes.new('ShaderNodeTexImage');tex.image=atlas
material.node_tree.links.new(tex.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color'])
nodes.get('Principled BSDF').inputs['Roughness'].default_value=.30
report=[];jars=[]
for variant,label in enumerate(['Red','Blue','Yellow']):
    parts=[]
    def finish(obj,color=0,wrap=False):
        obj.data.materials.clear();obj.data.materials.append(material)
        uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
        for face in obj.data.polygons:
            face.use_smooth=True
            angles=[(math.atan2(obj.data.vertices[obj.data.loops[i].vertex_index].co.y,obj.data.vertices[obj.data.loops[i].vertex_index].co.x)/math.tau)%1 for i in face.loop_indices]
            seam=max(angles)-min(angles)>.5
            for i,a in zip(face.loop_indices,angles):
                point=obj.data.vertices[obj.data.loops[i].vertex_index].co
                if wrap:coord=(a+1 if seam and a<.5 else a,(variant+(6+np.clip((point.z-.018)/.174,0,1)*244)/256)/4)
                else:coord=((color%4+.5)/4,.75+(color//4+.5)/8)
                uv.data[i].uv=coord
        obj.select_set(False);parts.append(obj);return obj
    def lathe(name,profile,color=0,wrap=False,n=16):
        vertices=[];rings=[]
        for r,z in profile:
            rings.append(list(range(len(vertices),len(vertices)+(n if r else 1))))
            vertices.extend([(r*math.cos(a),r*math.sin(a),z) for a in np.linspace(0,math.tau,n,endpoint=False)] if r else [(0,0,z)])
        faces=[]
        for first,second in zip(rings,rings[1:]+rings[:1]):
            if len(first)==len(second)==1:continue
            for j in range(n):
                k=(j+1)%n
                faces.append((first[0],second[j],second[k]) if len(first)==1 else (first[j],second[0],first[k]) if len(second)==1 else (first[j],second[j],second[k],first[k]))
        data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update()
        bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
        obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);return finish(obj,color,wrap)
    lathe('Textured candy body',[(0,.012),(.061,.012),(.075,.028),(.075,.168),(.062,.192),(0,.192)],wrap=True)
    lathe('Ivory jar foot',[(0,0),(.067,0),(.073,.013),(0,.019)],1,n=12)
    lathe('Gold neck collar',[(.061,.190),(.066,.195),(.066,.207),(.061,.207)],0,n=12)
    lathe('Ivory domed lid',[(0,.211),(.080,.211),(.080,.222),(.055,.247),(0,.257)],1)
    lathe('Gold lid bead',[(.078,.211),(.084,.213),(.084,.221),(.078,.221)],0,n=12)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=.0215,location=(0,0,.2785));finish(bpy.context.object,4+variant)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();jar=bpy.context.object;jar.name='CandyJar'+label
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    for face in jar.data.polygons:face.material_index=0
    jar.data.materials.clear();jar.data.materials.append(material)
    check=bmesh.new();check.from_mesh(jar.data);assert all(edge.is_manifold for edge in check.edges);check.free()
    triangles=sum(len(face.vertices)-2 for face in jar.data.polygons);assert triangles<=700
    assert all(math.isfinite(c) for vertex in jar.data.vertices for c in vertex.co)
    assert abs(min(v.co.z for v in jar.data.vertices))<.0001 and abs(max(v.co.z for v in jar.data.vertices)-.30)<.0001
    bpy.ops.export_scene.fbx(filepath=str(OUT/(jar.name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='STRIP')
    report.append(dict(name=jar.name,triangles=triangles,materials=1,height=.30));jars.append(jar)
for jar,x in zip(jars,[-.22,0,.22]):jar.location.x=x
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.use_denoising=True
scene.render.resolution_x=1200;scene.render.resolution_y=750;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Studio');scene.world.color=(.18,.18,.18);scene.view_settings.view_transform='AgX'
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.location.z=-.0005
floor_material=bpy.data.materials.new('Studio White');floor_material.diffuse_color=(.88,.88,.88,1);floor.data.materials.append(floor_material)
for location,power,size in [((-1,-1,2),180,2),((1,.5,1.2),100,1.5)]:
    bpy.ops.object.light_add(type='AREA',location=location);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size
    light.rotation_euler=(Vector((0,0,.15))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(.38,-1.2,.63));camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,.15))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=.87;scene.camera=camera
atlas.pack();bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'CandyJarsPrototype.blend'))
scene.render.filepath=str(OUT/'preview.png');bpy.ops.render.render(write_still=True)
(OUT/'manifest.json').write_text(json.dumps(report,indent=2));print('CANDY_JAR_PROTOTYPE',json.dumps(report))
