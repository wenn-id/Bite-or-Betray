"""Continuous checker floor and opaque woven oval rug with baked ornament."""
import bpy, bmesh, math, numpy as np
from pathlib import Path


def build_floor_rug(kind):
    resolution=512 if kind=='Floor' else 1024
    yy,xx=np.mgrid[0:resolution,0:resolution];u=(xx+.5)/resolution;v=(yy+.5)/resolution
    pixels=np.ones((resolution,resolution,4),dtype=np.float32)
    if kind=='Floor':
        tx=u*2;ty=v*2;parity=(np.floor(tx)+np.floor(ty)).astype(int)%2
        color=np.where(parity[...,None]==0,np.array((.79,.65,.54)),np.array((.38,.30,.28)))
        marble=.009*np.sin(math.tau*(3*u+2*v))+.005*np.sin(math.tau*(11*u-7*v))
        fx=tx%1;fy=ty%1;edge=np.minimum.reduce([fx,1-fx,fy,1-fy])
        bevel=np.exp(-edge*180)*.025
        color=np.clip(color+marble[...,None]-bevel[...,None],0,1)
        grout=np.clip((.009-edge)/.003,0,1)[...,None]
        pixels[:,:,:3]=color*(1-grout)+np.array((.43,.34,.29))*grout
        assert np.allclose(pixels[:,0],pixels[:,-1]) and np.allclose(pixels[0],pixels[-1]),'Floor texture does not tile'
    else:
        x=u*2-1;y=v*2-1;r=np.sqrt(x*x+y*y);theta=np.arctan2(y,x)
        a=(u*16%1-.5)*2;b=(v*16%1-.5)*2
        radius=np.sqrt(a*a+b*b);angle=np.arctan2(b,a)
        damask=np.exp(-((radius-(.42+.12*np.cos(angle*4)))/.045)**2)
        weave=.006*np.sin(xx*2.3)*np.sin(yy*2.1)
        color=np.zeros((resolution,resolution,3),dtype=np.float32)
        color[:]=(.30,.18,.39);color+=damask[...,None]*np.array((.022,.016,.025))+weave[...,None]
        border=np.clip((r-.835)/.004,0,1)[...,None]
        color=color*(1-border)+np.array((.235,.125,.315))*border
        gold=np.array((.84,.65,.34))
        def paint(alpha,shade=1):
            nonlocal color
            alpha=np.clip(alpha,0,1)[...,None];color=color*(1-alpha)+gold*shade*alpha
        for ring,width in [(.991,.006),(.974,.003),(.848,.003),(.837,.002)]:
            paint((width-np.abs(r-ring))/.0018)
        stitches=(.5+.5*np.sin(theta*240+r*120))*.15+.82
        paint(np.clip((r-.982)/.002,0,1),stitches[...,None])
        cell=(theta/math.tau*48+.5)%1-.5;a=cell*(math.tau*.91/48);b=r-.913
        for cx,cy,rx,ry in [(0,.018,.017,.018),(0,-.018,.017,.018),(-.018,0,.018,.017),(.018,0,.018,.017)]:
            paint((1-((a-cx)/rx)**2-((b-cy)/ry)**2)*12)
        paint((1-(a/.008)**2-(b/.008)**2)*12,.83)
        paint(np.clip((.002-np.abs(a))/.001,0,1)*np.clip((b+.045)/.002,0,1)*np.clip((-.014-b)/.002,0,1))
        for side in [-1,1]:
            paint((1-((a-side*.048)/.012)**2-((b+.032)/.007)**2)*12,.92)
        vine=np.exp(-((b+.043+.005*np.cos(theta*48))/.0018)**2)
        paint(vine*.65)
        pixels[:,:,:3]=np.clip(color,0,1)
    atlas=bpy.data.images.new('Interior'+kind+'Atlas',width=resolution,height=resolution,alpha=True)
    atlas.pixels.foreach_set(pixels.ravel())
    material=bpy.data.materials.new('Interior'+kind);material.use_nodes=True
    tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas
    material.node_tree.links.new(tex.outputs['Color'],material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    material.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.8 if kind=='Rug' else .6
    if kind=='Floor':
        vertices=[(x,y,z) for z in [0,.012] for y in [-3.7,3.7] for x in [-3.7,3.7]]
        faces=[(0,2,3,1),(4,5,7,6),(0,1,5,4),(1,3,7,5),(3,2,6,7),(2,0,4,6)]
    else:
        n=64;vertices=[(2.425*math.cos(a),2.25*math.sin(a),z) for z in [0,.008] for a in np.linspace(0,math.tau,n,endpoint=False)]
        faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    data=bpy.data.meshes.new('Interior'+kind);data.from_pydata(vertices,[],faces);data.update()
    check=bmesh.new();check.from_mesh(data);bmesh.ops.recalc_face_normals(check,faces=list(check.faces))
    assert all(edge.is_manifold for edge in check.edges),kind+' is not closed'
    check.to_mesh(data);check.free();data.materials.append(material)
    uv=data.uv_layers.new(name='UVMap')
    for face in data.polygons:
        for loop in face.loop_indices:
            p=data.vertices[data.loops[loop].vertex_index].co
            uv.data[loop].uv=((p.x+3.7)/.74,(p.y+3.7)/.74) if kind=='Floor' else ((p.x+2.425)/4.85,(p.y+2.25)/4.50)
        if face.normal.z>.99:assert all(abs(data.vertices[i].co.z-(.012 if kind=='Floor' else .008))<.00001 for i in face.vertices)
    triangles=sum(len(face.vertices)-2 for face in data.polygons)
    assert triangles <= (12 if kind=='Floor' else 300),(kind,triangles)
    assert all(math.isfinite(c) for vertex in data.vertices for c in vertex.co)
    obj=bpy.data.objects.new('Interior'+kind,data);bpy.context.collection.objects.link(obj)
    directory=Path(__file__).resolve().parents[2]/'ArtSource/NewInterior';directory.mkdir(parents=True,exist_ok=True)
    atlas.pack();bpy.ops.wm.save_as_mainfile(filepath=str(directory/('Polished'+kind+'.blend')))
    return [obj]
