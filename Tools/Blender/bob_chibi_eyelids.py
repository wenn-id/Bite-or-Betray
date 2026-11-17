import bpy,numpy as np,math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from pathlib import Path
from bob_chibi_rig import SPECS,sample_colors,hair_mask

def add_eyelids(name,obj):
 me=obj.data;spec=SPECS[name]
 for key in list(me.shape_keys.key_blocks)[1:]:
  if key.name not in ['Open','Chew','Grin','Frown']:obj.shape_key_remove(key)
 mat=me.materials[0];image=next(n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name=='sRGB')
 cs=sample_colors(me,image);co=np.array([v.co[:] for v in me.vertices]);x,y,z=co.T;r,g,b=cs.T
 mask=(y<-.10)&(np.abs(x)<.19)&(z<.03)&(z>-.28)&(r>g*1.07)&(g>b*1.025)&(g>r*.53)&(b>g*.56)&(~hair_mask(name,me,image))
 def design(a,b):return np.stack([a*0+1,a,a*a,b,b*b],axis=-1)
 X=design(x,z)
 for _ in range(3):
  coeff=np.linalg.lstsq(X[mask],y[mask],rcond=None)[0];res=np.abs(X@coeff-y);mask&=res<.018
 assert mask.sum()>15,(name,mask.sum())
 def surface(a,b):return float(design(np.array(a),np.array(b))@coeff)
 # Extend the existing atlas with a skin swatch; original artwork and UV islands are preserved.
 oldw,h=image.size;tile=512;pix=np.array(image.pixels[:],dtype=np.float32).reshape(h,oldw,4)
 tone=cs[mask & (np.abs(x)<.065)];tone=np.median(tone if len(tone)>3 else cs[mask],axis=0)
 atlas=np.empty((h,oldw+tile,4),dtype=np.float32);atlas[:,:oldw]=pix;atlas[:,oldw:,:3]=tone;atlas[:,oldw:,3]=1
 xx=np.linspace(-.3,.3,tile)[None,:];zz=np.linspace(-.3,.3,h)[:,None]
 blush=np.exp(-((np.abs(xx)-.12)/.04)**2-((zz-(spec['eyes'][0][1]-.035))/.025)**2)*.22
 atlas[:,oldw:,:3]=tone*(1-blush[:,:,None])+np.array([tone[0],tone[1]*.72,tone[2]*.78])*blush[:,:,None]
 probe=Path(__file__).parents[2]/'ArtSource/NewCharacters/Projections'/(name+'-front.png')
 projection=bpy.data.images.load(str(probe));projection.pack();pw,ph=projection.size
 front=np.array(projection.pixels[:],dtype=np.float32).reshape(ph,pw,4)
 px=np.clip(((xx/.72+.5)*(pw-1)).astype(int),0,pw-1);pz=np.clip(((zz/.72+.5)*(ph-1)).astype(int),0,ph-1)
 original=front[pz,px,:3];rr,gg,bb=original[:,:,0],original[:,:,1],original[:,:,2]
 skin=(rr>gg*1.04)&(gg>bb*1.015)&(gg>rr*.59)&(rr>.4)&((gg-bb)<.45*(rr-bb))
 distance=np.ones((h,tile))*100
 for cx,zb,zt in spec['eyes']:
  radius=np.sqrt(((xx-cx)/(spec['rx']*.88))**2+((zz-(zb+zt)*.5)/((zt-zb)*.53))**2)
  distance=np.minimum(distance,radius)
 blend=np.clip((distance-1.0)/.55,0,1)*skin
 atlas[:,oldw:,:3]=atlas[:,oldw:,:3]*(1-blend[:,:,None])+original*blend[:,:,None]

 new=bpy.data.images.new(name+'_FaceAtlas',width=oldw+tile,height=h,alpha=True);new.pixels.foreach_set(atlas.ravel())
 for node in mat.node_tree.nodes:
  if node.type=='TEX_IMAGE' and node.image==image:node.image=new
 for uv in me.uv_layers.active.data:uv.uv.x*=oldw/(oldw+tile)
 probe=Path(__file__).parents[2]/'ArtSource/NewCharacters/Projections'/(name+'-front.png')
 tree=BVHTree.FromPolygons([v.co.copy() for v in me.vertices],[list(p.vertices) for p in me.polygons])
 def depth_at(a,b,guess,margin):
  point=tree.ray_cast(Vector((a,-2,b)),Vector((0,1,0)))[0]
  if point is None:return guess
  pc=front[min(ph-1,max(0,int((b/.72+.5)*(ph-1)))),min(pw-1,max(0,int((a/.72+.5)*(pw-1)))),:3]
  r,g,bb=pc
  if name=='Blond':hair=(r-bb>.075 and g-bb>.40*(r-bb))
  elif name=='Mint':hair=g>r*1.03 and g>bb*1.05
  elif name=='Peach':hair=r>.35 and g<r*.64
  else:hair=bb>g*1.06 and bb>r*.91
  if name=='Charcoal':hair=hair or (g>r*1.4 and bb>r*1.4)
  iris=any(((a-cx)/(spec['rx']*.73))**2+((b-(zb+zt)*.5)/((zt-zb)*.44))**2<1 for cx,zb,zt in spec['eyes'])
  if iris:hair=hair and point.y<surface(a,b)-.025
  else:hair=hair or point.y<surface(a,b)-.035
  return max(guess,point.y+.0015) if hair else min(guess,point.y-margin)
 eye=bpy.data.materials.new(name+'_EyeArtwork');eye.use_nodes=True;tex=eye.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(probe));eye.node_tree.links.new(tex.outputs['Color'],eye.node_tree.nodes.get('Principled BSDF').inputs['Base Color']);eye.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.7
 # Lashes use a tiny dark patch already present in the original atlas.
 dark=int(np.argmin(((pix[:,:,:3]-np.array([.07,.02,.02]))**2).sum(axis=2)));darkuv=((dark%oldw+.5)/(oldw+tile),(dark//oldw+.5)/h)
 vertices=[];polys=[];materials=[];uvs=[];closed=[]
 def push(c,bc,uv):vertices.append(c);closed.append(bc);uvs.append(uv)
 def skinuv(a,b):return ((oldw+tile*(a/.6+.5))/(oldw+tile),b/.6+.5)
 N=128;R=12
 for cx,zb,zt in spec['eyes']:
  cz=(zb+zt)*.5;rx=spec['rx']*.88;rz=(zt-zb)*.53;outerx=spec['rx']*1.23;outerz=(zt-zb)*.78
  # Match fitted head depth while staying in front of the sculpted eye relief.
  for mode in ['skin','eye','lash']:
   start=len(vertices)
   if mode=='skin':
    for j in range(R+1):
     t=j/R;s=t*t*(3-2*t)
     for i in range(N):
      a=2*math.pi*i/N;ix=cx+rx*math.cos(a);iz=cz+rz*math.sin(a);bx=cx+outerx*math.cos(a);bz=cz+outerz*math.sin(a)
      ex=ix*(1-s)+bx*s;ez=iz*(1-s)+bz*s;bz2=(cz-.007+.009*math.cos(a)**2)*(1-s)+bz*s
      dep=depth_at(ex,ez,surface(ex,ez)-.015*(1-s)+.012*s,.003);bdep=depth_at(ex,bz2,surface(ex,bz2)-.015*(1-s)+.012*s,.003)
      push((ex,dep,ez),(ex,bdep,bz2),skinuv(ex,ez))
    for j in range(R):
     for i in range(N):polys.append((start+j*N+i,start+(j+1)*N+i,start+(j+1)*N+(i+1)%N,start+j*N+(i+1)%N));materials.append(0)
   elif mode=='eye':
    for j in range(R+1):
     t=max(j/R,.0001)
     for i in range(N):
      a=2*math.pi*i/N;ex=cx+rx*math.cos(a)*t;ez=cz+rz*math.sin(a)*t;bz2=cz-.007+.009*(math.cos(a)*t)**2+(ez-cz)*.002
      push((ex,depth_at(ex,ez,surface(ex,ez)-.011,.001),ez),(ex,depth_at(ex,bz2,surface(ex,bz2)-.006,.001),bz2),(ex/.72+.5,ez/.72+.5))
    for j in range(R):
     for i in range(N):polys.append((start+j*N+i,start+(j+1)*N+i,start+(j+1)*N+(i+1)%N,start+j*N+(i+1)%N));materials.append(1)
   else:
    for i in range(65):
     a=math.pi*i/64;ex=cx+rx*math.cos(a);ez=cz+rz*math.sin(a);bz2=cz-.007+.009*math.cos(a)**2
     for side in [-1,1]:push((ex,depth_at(ex,ez,surface(ex,ez)-.016,.004),ez+side*.0009),(ex,depth_at(ex,bz2,surface(ex,bz2)-.016,.004),bz2+side*.0009),darkuv)
    for i in range(64):polys.append((start+i*2,start+i*2+1,start+(i+1)*2+1,start+(i+1)*2));materials.append(0)
 patch=bpy.data.meshes.new(name+'_Lids');patch.from_pydata(vertices,[],polys);patch.update();lid=bpy.data.objects.new(name+'_Lids',patch);bpy.context.collection.objects.link(lid)
 patch.materials.append(mat);patch.materials.append(eye);uv=patch.uv_layers.new(name='UVMap')
 for p,m in zip(patch.polygons,materials):
  p.material_index=m;p.use_smooth=True
  for l in p.loop_indices:uv.data[l].uv=uvs[patch.loops[l].vertex_index]
 lid.shape_key_add(name='Basis');key=lid.shape_key_add(name='Blink')
 for v,c in zip(key.data,closed):v.co=c
 bpy.ops.object.select_all(action='DESELECT');lid.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=obj;bpy.ops.object.join()
 print('EYELIDS',name,'fit',coeff.tolist(),'tone',tone.tolist())
 return obj
