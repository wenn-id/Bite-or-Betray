"""Clean arched window and candy-shop shelving, facing Blender -Y."""
import bpy, bmesh, math, numpy as np
from mathutils import Vector
from pathlib import Path


def build_wall_furniture(kind):
    palette = [(.035,.38,.41), (.08,.48,.49), (.025,.27,.30), (.66,.32,.12),
               (.86,.51,.22), (.96,.68,.28), (.25,.07,.12), (.49,.22,.10)]
    pixels = np.ones((1024,1024,4), dtype=np.float32)
    yy, xx = np.mgrid[0:256,0:256]
    for i, color in enumerate(palette):
        grain = .025*np.sin(yy*.21 + np.sin(xx*.018)*.8) if i in [3,4,7] else 0
        pixels[(i//4)*256:(i//4+1)*256,(i%4)*256:(i%4+1)*256,:3] = np.clip(np.array(color)*(1+np.asarray(grain)[...,None]),0,1)
    # Opaque painted view avoids layered transparent panes on mobile VR.
    yy, xx = np.mgrid[0:512,0:1024]; x=xx/1024; z=yy/512
    sky = np.zeros((512,1024,3),dtype=np.float32)
    sky[:] = (.63,.86,.98); sky += z[...,None]*np.array((.13,.09,.02))
    rng=np.random.default_rng(38)
    for row in range(5):
        for col in range(10):
            cx=(col-.2+rng.uniform(-.18,.18))/9; cz=(row-.3)/5.7+rng.uniform(-.04,.04)
            radius=rng.uniform(.055,.085); distance=((x-cx)/radius)**2+((z-cz)/(radius*.95))**2
            coverage=np.clip((1-distance)*18,0,1)[...,None]
            shade=.68+.32*np.sqrt(np.clip(1-distance,0,1))
            color=np.array([(.99,.57,.77),(.88,.64,.97),(.60,.85,.97),(.99,.81,.63)][(row+col)%4])
            ball=color*shade[...,None]
            highlight=np.exp(-(((x-cx+radius*.25)/(radius*.28))**2+((z-cz-radius*.32)/(radius*.28))**2))
            ball=ball*(1-highlight[...,None]*.55)+highlight[...,None]*.55
            sky=sky*(1-coverage)+ball*coverage
    reflection=np.clip(1-np.abs((x*.7-z)-.14)/.055,0,1)*.15
    sky=sky*(1-reflection[...,None])+reflection[...,None]
    pixels[512:,:,:3]=np.clip(sky,0,1)
    atlas=bpy.data.images.new('Interior'+kind+'Atlas',width=1024,height=1024,alpha=True)
    atlas.pixels.foreach_set(pixels.ravel())
    material=bpy.data.materials.new('Interior'+kind); material.use_nodes=True
    tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas
    material.node_tree.links.new(tex.outputs['Color'],material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.4
    parts=[]

    def finish(obj,color,bevel=0,wood=False,glass=False):
        bpy.context.view_layer.objects.active=obj
        if bevel:
            mod=obj.modifiers.new('Rounded crafted edge','BEVEL');mod.width=bevel;mod.segments=2
            bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.data.materials.clear();obj.data.materials.append(material)
        uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
        for face in obj.data.polygons:
            for index in face.loop_indices:
                v=obj.matrix_world@obj.data.vertices[obj.data.loops[index].vertex_index].co
                if glass:
                    coord=(.04+.92*(v.x+.80)/1.60,.51+.48*(v.z-.10)/1.68)
                else:
                    u=.5+.26*v.x if wood else .5
                    w=.5+.26*v.y if wood and abs(face.normal.z)>.8 else .5
                    coord=((color%4+u)/4,(color//4+w)/4)
                uv.data[index].uv=coord
        obj.select_set(False);parts.append(obj);return obj

    def mesh(name,vertices,faces,color,bevel=0,wood=False,glass=False):
        data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update()
        bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
        obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
        return finish(obj,color,bevel,wood,glass)

    def extrude(name,outline,front,back,color,bevel=0,glass=False):
        n=len(outline)
        return mesh(name,[(x,y,z) for y in [front,back] for x,z in outline],
                    [tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],color,bevel,glass=glass)

    def box(name,center,size,color,bevel=.004,wood=False):
        bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cube_add(size=1,location=center)
        obj=bpy.context.object;obj.name=name;obj.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        return finish(obj,color,bevel,wood)

    def arch_outline(radius,spring,bottom=0):
        return [(-radius,bottom),(radius,bottom)]+[(radius*math.cos(t),spring+radius*math.sin(t)) for t in np.linspace(0,math.pi,33)]

    def arch(name,outer,inner,spring,bottom,front,back,color):
        contours=[[(-r,bottom)]+[(r*math.cos(t),spring+r*math.sin(t)) for t in np.linspace(math.pi,0,33)]+[(r,bottom)] for r in [outer,inner]]
        n=len(contours[0]);vertices=[(x,y,z) for y in [front,back] for contour in contours for x,z in contour];faces=[]
        for i in range(n-1):
            faces.extend([(i,i+1,n+i+1,n+i),(2*n+i,3*n+i,3*n+i+1,2*n+i+1),
                          (i,2*n+i,2*n+i+1,i+1),(n+i,n+i+1,3*n+i+1,3*n+i)])
        faces.extend([(0,n,3*n,2*n),(n-1,3*n-1,4*n-1,2*n-1)])
        return mesh(name,vertices,faces,color)

    def shelf(z,width,depth):
        box('Rounded oak shelf',(0,-depth/2,z),(width,depth,.055),3,.018,True)
        box('Honey shelf edge',(0,-depth+.005,z-.007),(width-.02,.025,.027),4,.009)
        box('Fine brass shelf bead',(0,-depth-.008,z-.020),(width-.05,.008,.007),5,.002)

    if kind=='Window':
        extrude('Unbroken painted glass',arch_outline(.79, .98,.09),-.055,-.02,0,glass=True)
        arch('Teal arched surround',.87,.79,.98,.065,-.135,0,0)
        arch('Outer moulding',.864,.846,.98,.065,-.145,-.130,1)
        arch('Inner carved bead',.803,.789,.98,.065,-.149,-.134,2)
        arch('Fine gold glazing bead',.789,.780,.98,.09,-.097,-.080,5)
        for x in [-.40,0,.40]:
            top=.98+math.sqrt(.78**2-x*x)
            box('Plum window mullion',(x,-.090,(top+.10)/2),(.032,.041,top-.10),6,.005)
        for z in [.51,.96,1.39]:
            half=.78 if z<=.98 else math.sqrt(.78**2-(z-.98)**2)
            box('Plum window crossbar',(0,-.092,z),(half*2,.043,.031),6,.005)
        box('Lower window rail',(0,-.09,.108),(1.58,.060,.045),6,.005)
        box('Oak window sill',(0,-.115,.045),(1.85,.27,.090),3,.022,True)
        box('Sill honey edge',(0,-.249,.039),(1.80,.018,.035),4,.008)
        box('Sill apron',(0,-.10,.013),(1.69,.14,.026),0,.007)
    elif kind=='Shelves':
        for side in [-1,1]:
            box('Teal upright',(side*.565,-.047,.555),(.065,.094,1.06),0,.010)
            for z,h in [(.04,.08),(1.08,.06)]:box('Post collar',(side*.565,-.055,z),(.095,.11,h),1,.008)
            bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=.045,location=(side*.565,-.05,1.155))
            for face in bpy.context.object.data.polygons:face.use_smooth=True
            finish(bpy.context.object,5)
        for z in [.23,.615,1.00]:
            shelf(z,1.30,.30)
            for side in [-1,1]:
                outline=[(-.065,z-.025),(-.270,z-.025),(-.254,z-.060),(-.205,z-.080),(-.158,z-.110),(-.126,z-.155),(-.110,z-.215),(-.065,z-.215)]
                n=len(outline);x=side*.535
                mesh('Curved brass shelf bracket',[(a,y,h) for a in [x-.016,x+.016] for y,h in outline],
                     [tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],5,.004)
    elif kind=='Display':
        extrude('Solid arched teal backing',arch_outline(.51,.65,.035),-.035,0,0)
        arch('Arched display frame',.55,.495,.65,.025,-.115,0,0)
        arch('Frame highlight',.544,.529,.65,.025,-.126,-.114,1)
        arch('Inset brass bead',.502,.493,.65,.040,-.133,-.124,5)
        for z in [.085,.405,.725]:shelf(z,1.12 if z<.7 else 1.04,.26)
        for side in [-1,1]:
            box('Display frame foot',(side*.521,-.06,.04),(.09,.13,.08),1,.008)
        for x in [-.30,-.10,.10,.30]:
            top=.65+math.sqrt(.49**2-x*x)-.02
            box('Backing plank joint',(x,-.036,(top+.06)/2),(.002,.002,top-.06),2,0)
        outline=[]
        for a in np.linspace(0,math.tau,32,endpoint=False):
            r=.039*(1+.20*math.cos(4*a));outline.append((r*math.cos(a),1.118+r*math.sin(a)))
        extrude('Clover crest',outline,-.142,-.123,5,.001)
    else:
        raise ValueError(kind)

    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();result=bpy.context.object;result.name='Interior'+kind
    # Placement resets the FBX root position, so geometry must carry the full base-centered transform.
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    for face in result.data.polygons:face.material_index=0
    result.data.materials.clear();result.data.materials.append(material)
    check=bmesh.new();check.from_mesh(result.data)
    assert not any(edge.is_boundary for edge in check.edges),kind+' has open components'
    check.free()
    assert sum(len(p.vertices)-2 for p in result.data.polygons)<3500,kind+' exceeds budget'
    assert all(math.isfinite(c) for v in result.data.vertices for c in v.co)
    assert all(0<=c<=1 for uv in result.data.uv_layers.active.data for c in uv.uv)
    points=[result.matrix_world@v.co for v in result.data.vertices]
    expected_height=1.85 if kind=='Window' else 1.20
    assert abs(min(p.z for p in points))<.001,kind+' origin is not at its base'
    assert abs(max(p.z for p in points)-expected_height)<.001,kind+' height changed'
    directory=Path(__file__).resolve().parents[2]/'ArtSource/NewInterior';directory.mkdir(parents=True,exist_ok=True)
    atlas.pack();bpy.ops.wm.save_as_mainfile(filepath=str(directory/('Polished'+kind+'.blend')))
    return [result]
