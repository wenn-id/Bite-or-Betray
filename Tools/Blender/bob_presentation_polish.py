"""Rounded, skinned Quest gloves and a reusable serving cloche, with inspection renders."""
import bpy, bmesh, math, json, sys, shutil
from pathlib import Path
from mathutils import Vector, Matrix
sys.path.insert(0, str(Path(__file__).parent))

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/BiteOrBetray/NewCharacters'
SOURCE = ROOT / 'ArtSource/GloveRevision20261116'
SOURCE.mkdir(parents=True, exist_ok=True)
COLORS = [(.97,.94,.84), (.45,.76,.64), (.91,.60,.16), (.10,.31,.29), (.82,.85,.83)]
parts = []

def begin():
    global material, parts
    bpy.ops.wm.read_factory_settings(use_empty=True)
    parts = []
    image = bpy.data.images.new('ParlourPropAtlas', width=320, height=64, alpha=False)
    pixels = []
    for y in range(64):
        for x in range(320): pixels.extend((*COLORS[x//64], 1))
    image.pixels.foreach_set(pixels)
    material = bpy.data.materials.new('ParlourProps'); material.use_nodes = True
    texture = material.node_tree.nodes.new('ShaderNodeTexImage'); texture.image = image
    material.node_tree.links.new(texture.outputs['Color'], material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value = .43
    image.filepath_raw = str(OUT/'ParlourProps_BaseColor.png'); image.file_format = 'PNG'; image.save(); image.pack()

def finish_mesh(obj, color):
    obj.data.materials.clear(); obj.data.materials.append(material)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
    for layer in list(obj.data.uv_layers):
        if layer!=uv:obj.data.uv_layers.remove(layer)
    uv.name='UVMap'
    for face in obj.data.polygons:
        face.use_smooth = True
        for loop in face.loop_indices: uv.data[loop].uv = ((color+.5)/5,.5)
    parts.append(obj)
    return obj

def ellipsoid(name, center, radii, color=0, n=16, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=n, ring_count=rings, location=center)
    obj = bpy.context.object; obj.name = name; obj.scale = radii
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return finish_mesh(obj, color)

def lathe(name, profile, color, n=24):
    vertices = [(r*math.cos(i*math.tau/n),r*math.sin(i*math.tau/n),z) for r,z in profile for i in range(n)]
    faces = [(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(profile)-1) for i in range(n)]
    data=bpy.data.meshes.new(name); data.from_pydata(vertices,[],faces); data.update()
    bm=bmesh.new(); bm.from_mesh(data); bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(data); bm.free()
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj)
    return finish_mesh(obj,color)

def join():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join()
    return bpy.context.object

def render(name, arm=None):
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=24
    scene.render.resolution_x=1100; scene.render.resolution_y=850; scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('Studio'); scene.world.use_nodes=True
    scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.75,.75,.75,1)
    scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.7
    scene.view_settings.view_transform='AgX'; scene.render.film_transparent=False
    target=Vector((0,0,.10)); hand=arm is not None
    bpy.ops.object.camera_add(location=(.29,-.50,.27) if hand else (.80,-1.15,.75))
    camera=bpy.context.object; camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO'; camera.data.ortho_scale=.29 if hand else .8; scene.camera=camera
    for location,power,size in [((-.4,-.6,.8),40,.5),((.5,.2,.6),25,.4)]:
        bpy.ops.object.light_add(type='AREA',location=location); light=bpy.context.object
        light.data.energy=power if hand else power*3; light.data.shape='DISK'; light.data.size=size
        light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
    for value,suffix in ([(.12,'relaxed'),(.85,'grip')] if hand else [(0,'detail')]):
        if arm:
            for bone in arm.pose.bones:
                if 'curlAxis' in bone.bone:
                    bone.rotation_mode='AXIS_ANGLE'
                    weight=(.12 if 'Index' in bone.name else .38) if suffix=='relaxed' else value
                    bone.rotation_axis_angle=(math.radians(bone.bone['curlDegrees'])*weight,*bone.bone['curlAxis'])
        scene.render.filepath=str(SOURCE/(name+'-'+suffix+'.png')); bpy.ops.render.render(write_still=True)
    if arm:
        for bone in arm.pose.bones: bone.rotation_axis_angle=(0,0,1,0)

def save(name,obj,arm=None):
    old=OUT/(name+'.fbx'); backup=SOURCE/('previous-'+name+'.fbx')
    if old.exists() and not backup.exists(): shutil.copy2(old,backup)
    obj.name=name+'Mesh'
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    if arm:
        arm.select_set(True)
        for child in arm.children:
            if child.type=='EMPTY': child.select_set(True)
    triangles=sum(len(p.vertices)-2 for p in obj.data.polygons)
    assert triangles <= (3600 if arm else 1200), (name,triangles)
    bpy.ops.file.pack_all(); bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')))
    bpy.ops.export_scene.fbx(filepath=str(old),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},
        add_leaf_bones=False,use_armature_deform_only=False,use_mesh_modifiers=False,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_anim=False,mesh_smooth_type='FACE',path_mode='STRIP')
    render(name,arm)
    height=max(v.co.z for v in obj.data.vertices)-min(v.co.z for v in obj.data.vertices)
    return {'name':name,'triangles':triangles,'material':'ParlourProps','bones':len(arm.data.bones) if arm else 0,'height':height}

def glove(name):
    begin(); mirror=1 if name=='HandLeft' else -1
    side='Left' if name=='HandLeft' else 'Right'
    source=next((ROOT/'Library/PackageCache').glob('com.meta.xr.sdk.interaction@*/Runtime/Meshes/OpenXR'+side+'Hand.fbx'))
    bpy.ops.import_scene.fbx(filepath=str(source))
    arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=arm
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    bones=arm.data.bones;wrist=bones['XRHand_Wrist'].head_local.copy()
    forward=(bones['XRHand_MiddleTip'].head_local-wrist).normalized()
    across=(bones['XRHand_IndexProximal'].head_local-bones['XRHand_LittleProximal'].head_local)*mirror
    across=(across-forward*across.dot(forward)).normalized();normal=forward.cross(across).normalized()
    basis=Matrix((across,normal,forward))
    transform=Matrix.Diagonal((.88,.94,.78,1))@basis.to_4x4()@Matrix.Translation(-wrist)
    transform.translation.z+=.026
    for vertex in obj.data.vertices:vertex.co=transform@vertex.co
    bpy.context.view_layer.objects.active=arm;bpy.ops.object.mode_set(mode='EDIT')
    for bone in arm.data.edit_bones:bone.transform(transform)
    bpy.ops.object.mode_set(mode='OBJECT')
    names={'XRHand_Wrist':'Wrist','XRHand_Palm':'Palm'}
    for label in ['Thumb','Index','Middle','Ring','Little']:
        joints=['Metacarpal','Proximal','Distal'] if label=='Thumb' else ['Proximal','Intermediate','Distal']
        for i,joint in enumerate(joints):names['XRHand_'+label+joint]='Finger_'+label+'_'+str(i+1)
        names['XRHand_'+label+'Tip']='Tip_'+label
    for group in obj.vertex_groups:group.name=names.get(group.name,group.name)
    for bone in arm.data.bones:bone.name=names.get(bone.name,bone.name)
    # Keep the SDK skin weights and joint topology; only soften the surface into a glove.
    obj.data.update()
    for vertex in obj.data.vertices:vertex.co+=vertex.normal*.0012
    subdivide=obj.modifiers.new('Rounded glove silhouette','SUBSURF');subdivide.levels=1
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=subdivide.name)
    smooth=obj.modifiers.new('Soft glove surface','SMOOTH');smooth.factor=.35;smooth.iterations=2
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=smooth.name)
    decimate=obj.modifiers.new('Glove triangle budget','DECIMATE')
    decimate.ratio=min(1,2800/sum(len(p.vertices)-2 for p in obj.data.polygons));bpy.ops.object.modifier_apply(modifier=decimate.name)
    finish_mesh(obj,0)
    lathe('Mint rolled cuff',[(0,.002),(.031,.003),(.035,.010),(.035,.028),(.031,.037),(.026,.038),(.024,.029),(0,.002)],1,n=24)
    # Five-point cuff emblem, matching the supplied glove reference.
    vertices=[(0,-.038,.020)]
    for i in range(10):
        a=math.pi*.5+i*math.tau/10; radius=.009 if i%2==0 else .0045
        vertices.append((radius*math.cos(a),-.036,.020+radius*math.sin(a)))
    data=bpy.data.meshes.new('Gold cuff star'); data.from_pydata(vertices,[],[(0,i+1,(i+1)%10+1) for i in range(10)])
    star=bpy.data.objects.new('Gold cuff star',data); bpy.context.collection.objects.link(star); finish_mesh(star,2)
    for part in parts:
        if len(part.vertex_groups)==0:
            group=part.vertex_groups.new(name='Wrist');group.add(list(range(len(part.data.vertices))),1,'REPLACE')
    obj=join();obj.parent=arm;arm.name=name+'_Rig'
    center=arm.data.bones['Palm'].head_local.copy();grip=center+Vector((0,.042,.028))
    for label,location in [('PalmCenter',center),('PalmNormal',center+Vector((0,.04,0))),('FingerForward',center+Vector((0,0,.10))),('CandyGrip',grip)]:
        marker=bpy.data.objects.new(label,None);bpy.context.collection.objects.link(marker);marker.parent=arm;marker.location=location
    for bone in arm.data.bones:
        if not bone.name.startswith('Finger_'):continue
        direction=bone.children[0].head_local-bone.head_local
        inward=grip-bone.head_local if 'Thumb_1' in bone.name else Vector((0,1,0))
        axis=direction.cross(inward).normalized()
        bone['curlAxis']=list(bone.matrix_local.to_3x3().inverted()@axis)
        joint=int(bone.name[-1]);bone['curlDegrees']=([45,30,25] if 'Thumb' in bone.name else [40,60,35])[joint-1]
    return save(name,obj,arm)

def cloche():
    begin()
    lathe('Gold serving tray',[(0,0),(.23,0),(.275,.008),(.29,.021),(.29,.032),(.278,.038),(.265,.026),(0,.026)],2,n=24)
    lathe('Teal inset',[(0,.027),(.259,.027),(.259,.031),(0,.031)],3,n=24)
    lathe('Pearl silver dome',[(0,.229),(.055,.226),(.115,.207),(.176,.173),(.217,.124),(.239,.070),(.243,.041),(.235,.035),(0,.035)],4,n=24)
    lathe('Gold dome band',[(.242,.035),(.247,.039),(.247,.050),(.241,.054),(.242,.035)],2,n=24)
    ellipsoid('Gold lid handle',(0,0,.25),(.023,.023,.028),2,n=16,rings=8)
    return save('ServingCloche',join())

requested=sys.argv[sys.argv.index('--only')+1].split(',') if '--only' in sys.argv else ['HandRight','HandLeft','ServingCloche']
report=[glove(name) if name!='ServingCloche' else cloche() for name in requested]
(SOURCE/'manifest.json').write_text(json.dumps(report,indent=2))
manifest_path=OUT/'manifest.json'
manifest={item['name']:item for item in json.loads(manifest_path.read_text())}
for item in report:
    manifest[item['name']]={'name':item['name'],'materials':[{'name':'ParlourProps','texture':'ParlourProps_BaseColor.png'}],
        'triangles':item['triangles'],'height':item['height']}
manifest_path.write_text(json.dumps(list(manifest.values()),indent=2))
print('PRESENTATION_PROPS_READY',json.dumps(report))
