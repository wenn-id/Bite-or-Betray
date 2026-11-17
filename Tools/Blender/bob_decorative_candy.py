"""Replace dense built-in spheres used by the small, static tabletop candies."""
from pathlib import Path
import bpy

root = Path(__file__).resolve().parents[2]
target = root / 'Assets/BiteOrBetray/NewInterior/DecorativeCandy.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=.5)
obj = bpy.context.object
obj.name = obj.data.name = 'DecorativeCandy'
for face in obj.data.polygons:
    face.use_smooth = True
triangles = sum(len(face.vertices) - 2 for face in obj.data.polygons)
assert triangles == 168, triangles
assert all(abs(vertex.co.length - .5) < 1e-6 for vertex in obj.data.vertices)
bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True, object_types={'MESH'},
    use_mesh_modifiers=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', bake_anim=False, mesh_smooth_type='FACE')
print(f'DECORATIVE_CANDY: {triangles} triangles, unit diameter, {target}')
