"""Bake a serving pose into the existing reaper and keep it within a 12K triangle budget."""
import bpy, math, shutil, json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'ArtSource/PresentationPolish20261115'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Logs/ChibiRig/Reaper_Rig.blend'))
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
before=len(obj.data.polygons)
rotation=Matrix.Rotation(math.radians(-70),3,'X')

def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)))
    return t*t*(3-2*t)

changed=0
for vertex in obj.data.vertices:
    p=vertex.co.copy();side=1 if p.x>0 else -1
    weight=smooth(.175,.245,abs(p.x))*(1-smooth(.89,1.03,p.z))
    weight*=smooth(.48,.61,p.z)*(1-smooth(-.03,.055,p.y))
    if weight<=0:continue
    elbow=Vector((side*.24,-.055,.90))
    target=elbow+rotation@(p-elbow)
    target.x-=side*.025*weight
    vertex.co=p.lerp(target,weight);changed+=1
assert changed>500 and len(obj.data.polygons)==before
obj.data.update()
bpy.context.view_layer.objects.active=obj
decimate=obj.modifiers.new('Quest waiter budget','DECIMATE')
decimate.ratio=min(1,12000/sum(len(p.vertices)-2 for p in obj.data.polygons))
decimate.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=decimate.name)
triangles=sum(len(p.vertices)-2 for p in obj.data.polygons)
assert triangles<=12000
destination=ROOT/'Assets/BiteOrBetray/NewCharacters/Reaper.fbx'
backup=OUT/'previous-Reaper.fbx'
if not backup.exists():shutil.copy2(destination,backup)
bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ReaperServing.blend'))
bpy.ops.export_scene.fbx(filepath=str(destination),use_selection=True,object_types={'MESH'},
    add_leaf_bones=False,use_mesh_modifiers=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',axis_up='Y',bake_anim=False,mesh_smooth_type='FACE',path_mode='STRIP')
print('REAPER_SERVING_POSE',changed,'posed vertices;',triangles,'triangles')
manifest_path=ROOT/'Assets/BiteOrBetray/NewCharacters/manifest.json'
manifest=json.loads(manifest_path.read_text())
for item in manifest:
    if item['name']=='Reaper':item['triangles']=triangles
manifest_path.write_text(json.dumps(manifest,indent=2))
