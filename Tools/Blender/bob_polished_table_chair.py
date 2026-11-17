"""Low-poly parlour furniture with baked cloth detail and a single atlas per model."""
import bpy, bmesh, math, numpy as np
from mathutils import Vector
from pathlib import Path


def build_table_chair(kind):
    pixels=np.ones((1024,1024,4),dtype=np.float32)
    yy,xx=np.mgrid[0:256,0:256]
    for i,color in enumerate([(.59,.27,.115),(.40,.155,.065),(.91,.58,.22),(.99,.76,.39)]):
        grain=(np.sin(yy*.29+np.sin(xx*.035))+.5*np.sin(yy*.71+xx*.012))*.035 if i<2 else 0
        pixels[:256,i*256:(i+1)*256,:3]=np.clip(np.array(color)*(1+np.asarray(grain)[...,None]),0,1)
    yy,xx=np.mgrid[0:768,0:1024];u=xx/1024;v=yy/768
    if kind=='Table':
        a=(u*13%1-.5)*2;b=(v*13%1-.5)*2;r=np.sqrt(a*a+b*b);angle=np.arctan2(b,a)
        flower=np.exp(-((r-(.42+.13*np.cos(angle*4)))/.035)**2)
        leaf=np.exp(-((r-.18)/.028)**2)
        diamond=np.exp(-((np.abs(a)+np.abs(b)-1.30)/.035)**2)
        pattern=(flower*.22+leaf*.10+diamond*.07)
        fabric=1+.012*np.sin(xx*2.4)*np.sin(yy*2.1)
        pixels[256:,:,:3]=np.clip(np.array((.038,.375,.40))*fabric[...,None]+pattern[...,None]*np.array((.035,.15,.14)),0,1)
    else:
        a=(u+v)*5;b=(u-v)*5
        seam=np.exp(-(np.sin(math.pi*a)/.075)**2)+np.exp(-(np.sin(math.pi*b)/.075)**2)
        puff=np.abs(np.sin(math.pi*a)*np.sin(math.pi*b))**.6
        fabric=.88+.12*puff-.065*seam
        pixels[256:,:,:3]=np.clip(np.array((.51,.075,.165))*fabric[...,None]+seam[...,None]*np.array((.035,.016,.015)),0,1)
    atlas=bpy.data.images.new('Interior'+kind+'Atlas',width=1024,height=1024,alpha=True);atlas.pixels.foreach_set(pixels.ravel())
    material=bpy.data.materials.new('Interior'+kind);material.use_nodes=True
    tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas
    material.node_tree.links.new(tex.outputs['Color'],material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.45
    parts=[];wood_parts=[]

    def mesh(name,vertices,faces,color=0,cloth=False,smooth=False,seat=False):
        data=bpy.data.meshes.new(name);data.from_pydata(vertices,[],faces);data.update()
        bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
        data.materials.append(material);uv=data.uv_layers.new(name='UVMap')
        for face in data.polygons:
            face.use_smooth=smooth
            if kind=='Table' and abs(face.normal.z)>.999:face.use_smooth=False
            for index in face.loop_indices:
                p=data.vertices[data.loops[index].vertex_index].co
                if cloth:
                    if kind=='Table':s,t=(p.x+1.48)/2.96,(p.y+1.48)/2.96
                    elif seat:s,t=(p.x+.29)/.58,(p.y+.30)/.60
                    else:s,t=(p.x+.24)/.48,(p.z-.48)/.70
                    coord=(.035+np.clip(s,0,1)*.93,.27+np.clip(t,0,1)*.71)
                else:
                    # Keep small trim in palette centers; only broad wood faces carry grain.
                    s=.5+(p.x*.16 if color<2 else 0);t=.5+(p.z*.15 if color<2 else 0)
                    coord=((color+np.clip(s,.15,.85))/4,np.clip(t,.15,.85)/4)
                uv.data[index].uv=coord
        obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);parts.append(obj)
        if not cloth and color<2:wood_parts.append(obj)
        return obj

    def lathe(name,profile,color=0,segments=64,center=(0,0),cloth=False,seat=False):
        vertices=[];rings=[]
        for rx,ry,z in profile:
            if rx==0:rings.append([len(vertices)]);vertices.append((center[0],center[1],z))
            else:
                rings.append(list(range(len(vertices),len(vertices)+segments)))
                vertices.extend((center[0]+rx*math.cos(a),center[1]+ry*math.sin(a),z) for a in np.linspace(0,math.tau,segments,endpoint=False))
        faces=[]
        for first,second in zip(rings,rings[1:]+rings[:1]):
            if len(first)==len(second)==1:continue
            for j in range(segments):
                k=(j+1)%segments
                faces.append((first[0],second[j],second[k]) if len(first)==1 else (first[j],second[0],first[k]) if len(second)==1 else (first[j],second[j],second[k],first[k]))
        return mesh(name,vertices,faces,color,cloth,True,seat)

    def leg(name,path,radii,color=0):
        n=8;vertices=[(p[0]+radius*math.cos(a),p[1]+radius*math.sin(a),p[2]) for p,radius in zip(path,radii) for a in np.linspace(0,math.tau,n,endpoint=False)]
        faces=[tuple(range(n-1,-1,-1)),tuple(range((len(path)-1)*n,len(path)*n))]
        for i in range(len(path)-1):
            for j in range(n):faces.append((i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j))
        return mesh(name,vertices,faces,color,smooth=True)

    if kind=='Table':
        lathe('Rounded walnut tabletop',[(0,0,.638),(1.565,1.565,.638),(1.605,1.605,.663),(1.614,1.614,.708),
              (1.610,1.610,.740),(1.593,1.593,.760),(1.495,1.495,.760),(1.475,1.475,.747),(0,0,.747)],segments=96)
        lathe('Teal damask playing surface',[(0,0,.750),(1.478,1.478,.750),(1.478,1.478,.758),(0,0,.758)],segments=96,cloth=True)
        lathe('Inner brass inlay',[(1.482,1.482,.752),(1.491,1.491,.752),(1.491,1.491,.760),(1.482,1.482,.760)],2,64)
        lathe('Outer honey bead',[(1.614,1.614,.697),(1.620,1.620,.704),(1.620,1.620,.713),(1.614,1.614,.719)],2,64)
        for x in [-1,1]:
            for y in [-1,1]:
                leg('Curved tapered table leg',[(x*.99,y*.99,.015),(x*.965,y*.965,.12),(x*.92,y*.92,.39),(x*.94,y*.94,.69)],[.045,.045,.065,.090])
                lathe('Brass table foot',[(0,0,0),(.046,.046,0),(.047,.047,.048),(0,0,.048)],2,12,(x*.99,y*.99))
    elif kind=='Chair':
        # The chair faces -Y; the curved back reclines toward +Y.
        outline=[(-.18,.48),(.18,.48),(.19,.63),(.218,.78),(.249,.90)]
        outline += [(.26*math.cos(a),.96+.22*math.sin(a)) for a in np.linspace(0,math.pi,25)]
        outline += [(-.249,.90),(-.218,.78),(-.19,.63)]
        n=len(outline)
        def back_rings(name,profiles,color=0,cloth=False):
            vertices=[(x*scale,y+(z-.48)*.085,.83+(z-.83)*scale) for scale,y in profiles for x,z in outline]
            faces=[]
            for i in range(len(profiles)):
                j=(i+1)%len(profiles)
                for k in range(n):faces.append((i*n+k,i*n+(k+1)%n,j*n+(k+1)%n,j*n+k))
            return mesh(name,vertices,faces,color,cloth,True)
        back_rings('Sculpted walnut back frame',[(1,.26),(1,.19),(.95,.165),(.83,.165),(.83,.26)])
        back_rings('Fine brass back inlay',[(.925,.160),(.91,.160),(.91,.168),(.925,.168)],2)
        # A center cap closes the padded front and back without a dense quilt mesh.
        vertices=[];rings=[]
        for scale,y in [(.835,.235),(.835,.170),(.73,.125),(.40,.100)]:
            rings.append(list(range(len(vertices),len(vertices)+n)))
            vertices.extend((x*scale,y+(z-.48)*.085,.83+(z-.83)*scale) for x,z in outline)
        faces=[tuple(reversed(rings[0])),tuple(rings[-1])]
        for first,second in zip(rings,rings[1:]):
            for i in range(n):faces.append((first[i],first[(i+1)%n],second[(i+1)%n],second[i]))
        mesh('Burgundy quilted back cushion',vertices,faces,cloth=True,smooth=True)
        lathe('Rounded seat frame',[(0,0,.395),(.28,.29,.395),(.30,.31,.425),(.295,.305,.445),(0,0,.445)],segments=40)
        lathe('Burgundy seat cushion',[(0,0,.435),(.278,.288,.435),(.285,.295,.455),(.26,.275,.488),(0,0,.498)],segments=40,cloth=True,seat=True)
        lathe('Seat brass piping',[(.284,.294,.440),(.289,.299,.446),(.287,.297,.452)],2,32)
        for side in [-1,1]:
            leg('Back frame structural stile',[(side*.16,.21,.425),(side*.17,.235,.53),(side*.19,.245,.65)],[.024,.022,.022])
            for front in [-1,1]:
                path=[(side*.241,front*.242,.012),(side*.225,front*.225,.13),(side*.185,front*.18,.435)]
                leg('Tapered walnut chair leg',path,[.022,.026,.035])
                lathe('Brass chair shoe',[(0,0,0),(.023,.023,0),(.024,.024,.034),(0,0,.034)],2,8,(side*.241,front*.242))
    else:raise ValueError(kind)

    # Exact unions remove buried caps and make the load-bearing wood one continuous solid.
    frame=wood_parts[0];bpy.context.view_layer.objects.active=frame
    for other in wood_parts[1:]:
        modifier=frame.modifiers.new('Continuous wooden joint','BOOLEAN');modifier.operation='UNION';modifier.solver='EXACT';modifier.object=other
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        parts.remove(other);bpy.data.objects.remove(other,do_unlink=True)
    check=bmesh.new();check.from_mesh(frame.data)
    assert not any(edge.is_boundary or not edge.is_manifold for edge in check.edges),kind+' wooden joint is open'
    pending=[next(iter(check.verts))];visited=set(pending)
    while pending:
        for edge in pending.pop().link_edges:
            for vertex in edge.verts:
                if vertex not in visited:visited.add(vertex);pending.append(vertex)
    assert len(visited)==len(check.verts),kind+' wooden frame contains disconnected parts'
    check.free()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();result=bpy.context.object;result.name='Interior'+kind
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    for face in result.data.polygons:face.material_index=0
    result.data.materials.clear();result.data.materials.append(material)
    check=bmesh.new();check.from_mesh(result.data)
    assert not any(edge.is_boundary for edge in check.edges),kind+' has open components'
    check.free()
    triangles=sum(len(p.vertices)-2 for p in result.data.polygons)
    assert triangles <= (3500 if kind=='Table' else 2000),(kind,triangles)
    points=[result.matrix_world@v.co for v in result.data.vertices]
    assert all(math.isfinite(c) for p in points for c in p)
    assert all(0<=c<=1 for uv in result.data.uv_layers.active.data for c in uv.uv)
    assert abs(min(p.z for p in points))<.001
    assert abs(max(p.z for p in points)-(.760 if kind=='Table' else 1.18))<.001
    directory=Path(__file__).resolve().parents[2]/'ArtSource/NewInterior';directory.mkdir(parents=True,exist_ok=True)
    atlas.pack();bpy.ops.wm.save_as_mainfile(filepath=str(directory/('Polished'+kind+'.blend')))
    return [result]
