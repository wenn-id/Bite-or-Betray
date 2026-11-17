"""Clean, reproducible replacement for the reconstructed parlour door."""
import bpy,bmesh,math,numpy as np
from mathutils import Vector
from pathlib import Path

def build_door():
 palette=[(.035,.38,.41),(.07,.47,.49),(.025,.27,.30),(.88,.50,.115),(.98,.71,.28),(.34,.68,.85),(.20,.48,.70),(.045,.035,.025)]
 width,height=1024,512;pixels=np.ones((height,width,4),dtype=np.float32)
 for index,color in enumerate(palette):
  yy,xx=np.mgrid[0:256,0:256];grain=(np.sin(xx*.37+np.sin(yy*.035)*.7)+np.sin(xx*.93+yy*.013))*.009 if index<3 else np.zeros_like(xx)
  pixels[(index//4)*256:(index//4+1)*256,(index%4)*256:(index%4+1)*256,:3]=np.clip(np.array(color)[None,None,:]*(1+grain[:,:,None]),0,1)
 atlas=bpy.data.images.new('InteriorDoorAtlas',width=width,height=height,alpha=True);atlas.pixels.foreach_set(pixels.ravel())
 material=bpy.data.materials.new('InteriorDoor');material.use_nodes=True
 tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas
 material.node_tree.links.new(tex.outputs['Color'],material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
 material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.36
 parts=[]

 def finish(obj,color,bevel=0):
  bpy.context.view_layer.objects.active=obj;obj.select_set(True)
  if bevel:
   mod=obj.modifiers.new('Soft crafted edges','BEVEL');mod.width=bevel;mod.segments=1
   bpy.ops.object.modifier_apply(modifier=mod.name)
  obj.data.materials.clear();obj.data.materials.append(material)
  uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
  coords=[v.co for v in obj.data.vertices];lo=Vector([min(v[i] for v in coords) for i in range(3)]);hi=Vector([max(v[i] for v in coords) for i in range(3)])
  for loop in obj.data.loops:
   v=obj.data.vertices[loop.vertex_index].co
   u=(v.x-lo.x)/max(hi.x-lo.x,.001);w=(v.z-lo.z)/max(hi.z-lo.z,.001)
   # Thin details use a single palette texel so mipmaps cannot blend neighboring atlas colors.
   if obj.name!='Solid arched door leaf':u=w=.5
   uv.data[loop.index].uv=((color%4+(8+u*240)/256)/4,(color//4+(8+w*240)/256)/2)
  obj.select_set(False);parts.append(obj);return obj

 def mesh(name,vertices,faces,color,bevel=0):
  data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update()
  bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
  obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);return finish(obj,color,bevel)

 def extrude(name,outline,front,back,color,bevel=0):
  n=len(outline);vertices=[(x,y,z) for y in [front,back] for x,z in outline]
  faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
  return mesh(name,vertices,faces,color,bevel)

 def box(name,center,size,color,bevel=.006):
  bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cube_add(size=1,location=center)
  obj=bpy.context.object;obj.name=name;obj.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(obj,color,bevel)

 def arc_outline(radius,spring,segments=32):
  return [(-radius,0),(radius,0)]+[(radius*math.cos(t),spring+radius*math.sin(t)) for t in np.linspace(0,math.pi,segments+1)]

 def arch_trim(name,outer,inner,spring,front,back,color):
  outer_points=[(-outer,0)]+[(outer*math.cos(t),spring+outer*math.sin(t)) for t in np.linspace(math.pi,0,33)]+[(outer,0)]
  inner_points=[(-inner,0)]+[(inner*math.cos(t),spring+inner*math.sin(t)) for t in np.linspace(math.pi,0,33)]+[(inner,0)]
  n=len(outer_points);vertices=[(x,y,z) for y in [front,back] for contour in [outer_points,inner_points] for x,z in contour]
  faces=[]
  for i in range(n-1):
   faces.extend([(i,i+1,n+i+1,n+i),(2*n+i,3*n+i,3*n+i+1,2*n+i+1),
                 (i,2*n+i,2*n+i+1,i+1),(n+i,n+i+1,3*n+i+1,3*n+i)])
  faces.extend([(0,n,3*n,2*n),(n-1,3*n-1,4*n-1,2*n-1)])
  return mesh(name,vertices,faces,color)

 def disk(name,cx,cz,radius,front,back,color,segments=32):
  return extrude(name,[(cx+radius*math.cos(t),cz+radius*math.sin(t)) for t in np.linspace(0,2*math.pi,segments,endpoint=False)],front,back,color)

 def ring(name,cx,cz,outer,inner,front,back,color,segments=40):
  circles=[[(cx+r*math.cos(t),y,cz+r*math.sin(t)) for t in np.linspace(0,2*math.pi,segments,endpoint=False)] for y in [front,back] for r in [outer,inner]]
  vertices=sum(circles,[]);faces=[];n=segments
  for i in range(n):
   j=(i+1)%n;faces.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
  return mesh(name,vertices,faces,color)

 extrude('Solid arched door leaf',arc_outline(.695,1.705),-.142,-.015,0)
 arch_trim('Continuous arched frame',.845,.695,1.705,-.180,.005,0)
 arch_trim('Inset frame moulding',.726,.700,1.705,-.194,-.178,1)
 arch_trim('Outer frame highlight',.840,.824,1.705,-.190,-.180,1)
 for side in [-1,1]:
  box('Frame foot',(side*.77,-.105,.09),(.15,.21,.18),0)
  box('Spring collar',(side*.77,-.107,1.70),(.16,.22,.055),1,.004)
  box('Lower inset shadow',(side*.302,-.150,.51),(.51,.018,.67),2)
  box('Lower raised panel',(side*.302,-.164,.51),(.45,.022,.61),0,.010)
 box('Door middle rail',(0,-.164,.94),(1.36,.032,.066),1)
 box('Wooden threshold',(0,-.100,.025),(1.45,.20,.05),3)
 for x in [-.46,-.23,0,.23,.46]:
  top=1.705+math.sqrt(.695**2-x*x)-.025
  box('Upper plank groove',(x,-.144,(1.00+top)/2),(.003,.002,top-1.00),2,0)

 cx,cz=-.255,1.865
 disk('Blue porthole glass',cx,cz,.238,-.169,-.150,5)
 # Two blue shades give the glass a readable reflection without transparent geometry.
 for i in [0,2,4]:
  angles=np.linspace(i*math.pi/3,(i+1)*math.pi/3,7)
  outline=[(cx,cz)]+[(cx+.235*math.cos(a),cz+.235*math.sin(a)) for a in angles]
  extrude('Blue glass reflection',outline,-.171,-.169,6)
 ring('Brass porthole rim',cx,cz,.277,.235,-.196,-.160,3)
 ring('Porthole rim highlight',cx,cz,.279,.270,-.199,-.195,4)
 for angle in [0,60,120]:
  bar=box('Window radial muntin',(cx,-.181,cz),(.470,.018,.012),3,.003);bar.rotation_euler.y=math.radians(angle)
 disk('Window center hub',cx,cz,.022,-.197,-.177,4,16)

 outline=[]
 for angle in np.linspace(0,2*math.pi,32,endpoint=False):
  r=.086*(1+.22*math.cos(4*angle));outline.append((.345+r*math.cos(angle),1.87+r*math.sin(angle)))
 extrude('Four petal brass rosette',outline,-.166,-.144,3,.002)
 disk('Rosette button',.345,1.87,.019,-.179,-.163,4,16)
 for z in [.40,1.08,1.68]:
  bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=.014,depth=.115,location=(-.654,-.160,z));hinge=bpy.context.object;hinge.name='Brass hinge';finish(hinge,3)
 plate=box('Handle escutcheon',(.505,-.174,1.145),(.075,.025,.23),3,.012)
 disk('Keyhole',.505,1.09,.010,-.191,-.188,7,12)
 extrude('Keyhole slot',[(.499,1.087),(.511,1.087),(.514,1.062),(.496,1.062)],-.192,-.190,7)
 path=[(.505,-.185,1.18),(.505,-.229,1.18),(.472,-.257,1.18),(.405,-.263,1.18),(.335,-.255,1.18)]
 vertices=[]
 for i,point in enumerate(path):
  tangent=(Vector(path[min(i+1,len(path)-1)])-Vector(path[max(i-1,0)])).normalized()
  across=Vector((0,0,1)).cross(tangent).normalized()
  for a in np.linspace(0,2*math.pi,8,endpoint=False):vertices.append(Vector(point)+across*math.cos(a)*.014+Vector((0,0,math.sin(a)*.014)))
 faces=[tuple(range(7,-1,-1)),tuple(range(32,40))]
 for i in range(4):
  for j in range(8):faces.append((i*8+j,i*8+(j+1)%8,(i+1)*8+(j+1)%8,(i+1)*8+j))
 mesh('Curved brass lever',vertices,faces,4)

 bpy.ops.object.select_all(action='DESELECT')
 for obj in parts:obj.select_set(True)
 bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();door=bpy.context.object;door.name='InteriorDoor'
 # All components use a single padded atlas and one material slot.
 for face in door.data.polygons:face.material_index=0
 door.data.materials.clear();door.data.materials.append(material)
 check=bmesh.new();check.from_mesh(door.data)
 assert not any(edge.is_boundary for edge in check.edges),'Door has an open component'
 check.free()
 assert sum(len(p.vertices)-2 for p in door.data.polygons)<3500,'Door exceeded the detail budget'
 path=Path(__file__).resolve().parents[2]/'ArtSource/NewInterior';path.mkdir(parents=True,exist_ok=True)
 atlas.pack();bpy.ops.wm.save_as_mainfile(filepath=str(path/'PolishedDoor.blend'))
 assert all(math.isfinite(c) for v in door.data.vertices for c in v.co)
 return [door]
