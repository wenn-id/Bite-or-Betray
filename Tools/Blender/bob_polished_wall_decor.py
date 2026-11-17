"""Opaque, single-material lantern and framed floral print for the parlour."""
import bpy, bmesh, math, numpy as np
from mathutils import Vector
from pathlib import Path


def build_wall_decor(kind):
    pixels=np.ones((512,512,4),dtype=np.float32)
    yy,xx=np.mgrid[0:128,0:128]
    palette=[(.67,.35,.115),(.96,.66,.25),(.27,.14,.065),(.99,.83,.48)]
    for i,color in enumerate(palette):
        grain=.025*np.sin(xx*.25+np.sin(yy*.1)) if i==0 else 0
        pixels[:128,i*128:(i+1)*128,:3]=np.array(color)*(1+np.asarray(grain)[...,None])
    yy,xx=np.mgrid[0:384,0:512];u=xx/511;v=yy/383
    if kind=='Picture':
        art=np.zeros((384,512,3),dtype=np.float32);art[:]=(.47,.055,.145)
        x=u*2-1;z=v*2-1
        def paint(mask,color):
            nonlocal art
            alpha=np.clip(mask,0,1)[...,None];art=art*(1-alpha)+np.array(color)*alpha
        def ellipse(cx,cz,rx,rz,color,angle=0):
            a=math.radians(angle);dx=x-cx;dz=z-cz
            distance=((dx*math.cos(a)+dz*math.sin(a))/rx)**2+((-dx*math.sin(a)+dz*math.cos(a))/rz)**2
            paint((1-distance)*65,color)
        paint((.045-np.abs(np.abs(x)/.83+np.abs(z)/.85-1))*90,(.98,.73,.29))
        for side in [-1,1]:
            for top in [-1,1]:
                ellipse(side*.67,top*.65,.08,.20,(.075,.57,.54),side*top*-40)
                ellipse(side*.52,top*.66,.075,.16,(.12,.65,.58),side*top*35)
                ellipse(side*.64,top*.51,.065,.13,(.075,.57,.54),side*top*45)
            for dx,angle in [(-.11,-35),(0,0),(.11,35)]:
                ellipse(dx,side*.43,.043,.105,(.96,.38,.36),side*angle)
        for cx,cz,rx,rz in [(0,.15,.16,.18),(0,-.15,.16,.18),(-.19,0,.24,.12),(.19,0,.24,.12)]:
            ellipse(cx,cz,rx,rz,(1,.94,.77))
        ellipse(0,0,.113,.085,(1,.70,.25))
        pixels[128:,:,:3]=art
    else:
        shine=np.exp(-((u-.23)/.10)**2)*.20+np.exp(-((u-.72)/.045)**2)*.10
        warmth=.80+.16*np.sin(v*math.pi)
        pixels[128:,:,:3]=np.clip(np.array((1,.79,.43))*warmth[...,None]+shine[...,None],0,1)
    atlas=bpy.data.images.new('Interior'+kind+'Atlas',width=512,height=512,alpha=True);atlas.pixels.foreach_set(pixels.ravel())
    material=bpy.data.materials.new('Interior'+kind);material.use_nodes=True
    nodes=material.node_tree.nodes;tex=nodes.new('ShaderNodeTexImage');tex.image=atlas
    material.node_tree.links.new(tex.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color'])
    nodes.get('Principled BSDF').inputs['Roughness'].default_value=.38
    if kind=='Sconce':
        mask=np.ones((128,128,4),dtype=np.float32);mask[:32,:,:3]=0
        emission=bpy.data.images.new('InteriorSconceEmission',width=128,height=128,alpha=True)
        emission.colorspace_settings.name='Non-Color';emission.pixels.foreach_set(mask.ravel())
        node=nodes.new('ShaderNodeTexImage');node.image=emission
        material.node_tree.links.new(node.outputs['Color'],nodes.get('Principled BSDF').inputs['Emission Color'])
        nodes.get('Principled BSDF').inputs['Emission Strength'].default_value=.5
        emission.pack()
    parts=[]

    def mesh(name,vertices,faces,color=0,art=False,glass=False,smooth=False):
        data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update()
        bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
        data.materials.append(material);uv=data.uv_layers.new(name='UVMap')
        for face in data.polygons:
            face.use_smooth=smooth
            for loop in face.loop_indices:
                p=data.vertices[data.loops[loop].vertex_index].co
                if art:coord=(.025+.95*(p.x+.136)/.272,.27+.71*(p.z-.033)/.364)
                elif glass:coord=(.025+.95*(math.atan2(p.y+.145,p.x)+math.pi)/math.tau,.27+.71*(p.z-.07)/.235)
                else:coord=((color+.5)/4,.125)
                uv.data[loop].uv=coord
        obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);parts.append(obj);return obj

    def lathe(name,profile,color=0,n=20,center=(0,-.145),glass=False):
        vertices=[];rings=[]
        for r,z in profile:
            rings.append(list(range(len(vertices),len(vertices)+(n if r else 1))))
            vertices.extend([(center[0]+r*math.cos(a),center[1]+r*math.sin(a),z) for a in np.linspace(0,math.tau,n,endpoint=False)] if r else [(center[0],center[1],z)])
        faces=[]
        for first,second in zip(rings,rings[1:]+rings[:1]):
            if len(first)==len(second)==1:continue
            for j in range(n):
                k=(j+1)%n
                faces.append((first[0],second[j],second[k]) if len(first)==1 else (first[j],second[0],first[k]) if len(second)==1 else (first[j],second[j],second[k],first[k]))
        return mesh(name,vertices,faces,color,glass=glass,smooth=True)

    def tube(name,path,radius,color=1,n=6):
        vertices=[]
        for i,point in enumerate(path):
            tangent=(Vector(path[min(i+1,len(path)-1)])-Vector(path[max(i-1,0)])).normalized()
            axis=Vector((1,0,0)) if abs(tangent.x)<.8 else Vector((0,1,0))
            a=tangent.cross(axis).normalized();b=tangent.cross(a).normalized()
            vertices.extend(Vector(point)+(a*math.cos(t)+b*math.sin(t))*radius for t in np.linspace(0,math.tau,n,endpoint=False))
        faces=[tuple(range(n-1,-1,-1)),tuple(range((len(path)-1)*n,len(path)*n))]
        for i in range(len(path)-1):
            for j in range(n):faces.append((i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j))
        return mesh(name,vertices,faces,color,smooth=True)

    def rectangle(width,height,y):
        r=.004;z=.215
        return [(-width/2+r,y,z-height/2),(width/2-r,y,z-height/2),(width/2,y,z-height/2+r),(width/2,y,z+height/2-r),
                (width/2-r,y,z+height/2),(-width/2+r,y,z+height/2),(-width/2,y,z+height/2-r),(-width/2,y,z-height/2+r)]

    def rect_profile(name,profile,color=0):
        vertices=sum([rectangle(*p) for p in profile],[]);faces=[];n=8
        for i in range(len(profile)):
            j=(i+1)%len(profile)
            for k in range(n):faces.append((i*n+k,i*n+(k+1)%n,j*n+(k+1)%n,j*n+k))
        return mesh(name,vertices,faces,color)

    if kind=='Picture':
        rect_profile('Continuous mitered walnut frame',[(.338,.430,0),(.338,.430,-.020),(.326,.418,-.031),(.280,.372,-.031),(.270,.362,-.025),(.270,.362,0)])
        rect_profile('Inset gold picture bead',[(.285,.377,-.029),(.285,.377,-.035),(.276,.368,-.035),(.276,.368,-.029)],1)
        vertices=rectangle(.272,.364,-.027)+rectangle(.272,.364,-.008);n=8
        mesh('Flat floral print',vertices,[tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],art=True)
    elif kind=='Sconce':
        n=24;vertices=[]
        for scale,y in [(0,-.028),(.92,-.028),(1,-.018),(.94,0),(0,0)]:
            vertices.extend([(0,y,.385)] if scale==0 else [(.070*scale*math.cos(a),y,.385+.105*scale*math.sin(a)) for a in np.linspace(0,math.tau,n,endpoint=False)])
        # Separate fan ends keep the oval plate closed without collapsed edges.
        faces=[(0,1+j,1+(j+1)%n) for j in range(n)]
        for ring in [1,1+n]:
            for j in range(n):faces.append((ring+j,ring+(j+1)%n,ring+n+(j+1)%n,ring+n+j))
        faces += [(1+2*n+j,1+3*n,1+2*n+(j+1)%n) for j in range(n)]
        mesh('Oval brass mounting plate',vertices,faces,1,smooth=True)
        tube('Curled bronze wall bracket',[(0,-.020,.395),(0,-.065,.415),(0,-.075,.475),(0,-.105,.512),(0,-.150,.490),(0,-.170,.450),(0,-.145,.420),(0,-.120,.430),(0,-.123,.455)],.008,2)
        tube('Connected lantern hanger',[(0,-.145,.421),(0,-.145,.325)],.009)
        lathe('Bronze lantern canopy',[(0,.327),(.030,.327),(.058,.297),(.078,.291),(0,.291)],2,16)
        lathe('Gold canopy brim',[(0,.282),(.078,.282),(.078,.291),(0,.291)],1,16)
        lathe('Opaque amber lantern lens',[(0,.070),(.047,.073),(.073,.130),(.075,.210),(.055,.280),(.034,.304),(0,.304)],n=20,glass=True)
        for angle in [0,math.pi/2,math.pi,math.pi*1.5]:
            tube('Bronze glass rib',[(r*math.cos(angle),-.145+r*math.sin(angle),z) for r,z in [(.050,.073),(.077,.130),(.079,.210),(.059,.280)]],.0035,2,4)
        lathe('Brass lower cup',[(0,.037),(.029,.042),(.056,.065),(.064,.078),(.064,.086),(0,.086)],1,16)
        lathe('Brass drop finial',[(0,0),(.012,.007),(.015,.019),(.009,.035),(0,.042)],1,12)
    else:raise ValueError(kind)

    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();result=bpy.context.object;result.name='Interior'+kind
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    if kind=='Sconce':
        maximum=max(v.co.z for v in result.data.vertices)
        for vertex in result.data.vertices:vertex.co.z*=.52/maximum
    for face in result.data.polygons:face.material_index=0
    result.data.materials.clear();result.data.materials.append(material)
    check=bmesh.new();check.from_mesh(result.data)
    assert all(edge.is_manifold for edge in check.edges),kind+' has open components'
    check.free()
    triangles=sum(len(p.vertices)-2 for p in result.data.polygons)
    assert triangles <= (1100 if kind=='Sconce' else 200),(kind,triangles)
    assert all(math.isfinite(c) for v in result.data.vertices for c in v.co)
    assert all(0<=c<=1 for uv in result.data.uv_layers.active.data for c in uv.uv)
    assert abs(min(v.co.z for v in result.data.vertices))<.001
    assert abs(max(v.co.z for v in result.data.vertices)-(.52 if kind=='Sconce' else .43))<.001
    directory=Path(__file__).resolve().parents[2]/'ArtSource/NewInterior';directory.mkdir(parents=True,exist_ok=True)
    atlas.pack();bpy.ops.wm.save_as_mainfile(filepath=str(directory/('Polished'+kind+'.blend')))
    return [result]
