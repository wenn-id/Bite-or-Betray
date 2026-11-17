"""Bake facial artwork into the original UVs; never add overlapping face geometry."""
import bpy
import numpy as np
from pathlib import Path

OUT=Path(__file__).resolve().parents[2]/'Assets/BiteOrBetray/NewCharacters'

def smooth(a,b,x):
 t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)

def expression_textures(name,obj,image,spec):
 from bob_chibi_rig import sample_colors
 me=obj.data;co=np.array([v.co[:] for v in me.vertices]);x,y,z=co.T
 colors=sample_colors(me,image);r,g,b=colors.T
 warm=(r>g*1.08)&(g>b*1.02)&(g>r*.55)&(b>g*.55)
 skin=warm&(y<-.09)&(abs(x)<.17)&(z>-.28)&(z<.03)
 def design(x,z):return np.stack([x*0+1,x,x*x,z,z*z],axis=-1)
 X=design(x,z)
 for _ in range(3):
  fit=np.linalg.lstsq(X[skin],y[skin],rcond=None)[0]
  skin&=abs(X@fit-y)<.018
 assert skin.sum()>15,(name,'face surface fit',skin.sum())
 normals=me.uv_layers.new(name='FaceSurfaceSlope')
 for loop in me.loops:
  vx,vy,vz=co[loop.vertex_index]
  normals.data[loop.index].uv=(fit[1]+2*fit[2]*vx,fit[3]+2*fit[4]*vz)
 me.uv_layers.active_index=0
 front=smooth(-.08,-.12,y)*smooth(.09,.045,abs(y-X@fit))
 # Hair in front of the face must keep its own texture and position.
 protected=(y<X@fit-.027)|(~warm & (z>spec['eyes'][0][2]+.02))|(y>-.085)
 base=np.array(image.pixels[:],dtype=np.float32).reshape(image.size[1],image.size[0],4)
 h,w=base.shape[:2];blink=np.zeros_like(base);mouth=np.zeros_like(base);covered=np.zeros((h,w),dtype=bool)
 # Sample local skin colors from the original orthographic projection, avoiding eye artwork.
 projection=bpy.data.images.load(str(OUT.parents[2]/'ArtSource/NewCharacters/Projections'/(name+'-front.png')))
 p=np.array(projection.pixels[:],dtype=np.float32).reshape(projection.size[1],projection.size[0],4)
 zz,xx=np.mgrid[0:p.shape[0],0:p.shape[1]];px=(xx+.5)/p.shape[1]*.72-.36;pz=(zz+.5)/p.shape[0]*.72-.36
 pr,pg,pb=p[:,:,:3].transpose(2,0,1)
 samples=(pr>pg*1.1)&(pg>pb*1.025)&(pg>pr*.60)&(pb>pg*.60)&(abs(px)<.17)&(pz>-.28)&(pz<.02)
 for cx,zb,zt in spec['eyes']:
  samples&=(((px-cx)/(spec['rx']*1.15))**2+((pz-(zb+zt)/2)/((zt-zb)*.85))**2>1)
 samples&=abs(px)>.035
 tint=np.linalg.lstsq(design(px[samples],pz[samples]),p[:,:,:3][samples],rcond=None)[0]
 eye_tones=[]
 for cx,zb,zt in spec['eyes']:
  below=(abs(px-cx)<spec['rx']*.60)&(pz>zb-.025)&(pz<zb-.006)
  inner=(abs(px)<.024)&(pz>zb+.01)&(pz<zt-.01)
  skin_color=(pr>pg*1.05)&(pg>pb*1.015)&(pg>pr*.43)&(pb>pg*.56)
  lower=p[:,:,:3][below&skin_color];upper=p[:,:,:3][inner&skin_color]
  assert len(lower)>5 and len(upper)>5,(name,'eyelid tone samples')
  eye_tones.append((np.median(lower,axis=0),np.median(upper,axis=0)))
 me.calc_loop_triangles();uv=me.uv_layers.active.data
 changed=0
 for tri in me.loop_triangles:
  ids=np.array(tri.vertices);points=co[ids]
  if points[:,1].min()>-.085 or points[:,0].min()>.21 or points[:,0].max()<-.21 or points[:,2].min()>.045:continue
  tuv=np.array([uv[i].uv[:] for i in tri.loops])*[w,h]-.5
  lo=np.maximum(np.floor(tuv.min(axis=0)).astype(int),0);hi=np.minimum(np.ceil(tuv.max(axis=0)).astype(int),[w-1,h-1])
  if (hi<lo).any():continue
  gx,gy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1))
  a,bv,c=tuv;den=(bv[1]-c[1])*(a[0]-c[0])+(c[0]-bv[0])*(a[1]-c[1])
  if abs(den)<1e-8:continue
  u=((bv[1]-c[1])*(gx-c[0])+(c[0]-bv[0])*(gy-c[1]))/den
  v=((c[1]-a[1])*(gx-c[0])+(a[0]-c[0])*(gy-c[1]))/den
  inside=(u>=-.005)&(v>=-.005)&(u+v<=1.005)
  if not inside.any():continue
  ix=gx[inside];iy=gy[inside];weights=np.stack([u[inside],v[inside],1-u[inside]-v[inside]],axis=-1)
  covered[iy,ix]=True
  xyz=weights@points;tx,ty,tz=xyz.T;dep=design(tx,tz)@fit
  eligible=smooth(-.085,-.12,ty)*smooth(.040,.023,dep-ty)*smooth(.065,.035,ty-dep)
  raw=base[iy,ix,:3];rr,gg,bb=raw.T
  if name=='Blond':hair=(rr-bb>.12)&(gg-bb>.30*(rr-bb))
  elif name=='Mint':hair=(gg>rr*1.04)&(gg>bb*1.05)
  elif name=='Peach':hair=(gg<rr*.55)&(rr>.3)&(bb<rr*.75)
  elif name=='Silver':hair=(rr>.30)&(bb>gg*1.08)&(bb>rr*.95)&(gg<rr*.96)
  else:hair=(bb>gg*1.12)&(bb>rr*.88)
  if name=='Charcoal':hair|=(gg>rr*1.5)&(bb>rr*1.5)
  skin_rgb=np.clip(design(tx,tz)@tint,.02,1)
  mask=np.zeros(len(tx));line=np.zeros(len(tx));eyelid_rgb=skin_rgb.copy()
  for eye_index,(cx,zb,zt) in enumerate(spec['eyes']):
   cz=(zb+zt)/2;rx=spec['rx']*1.08;rz=(zt-zb)*.69
   radius=np.sqrt(((tx-cx)/rx)**2+((tz-cz)/rz)**2)
   central=(((tx-cx)/(spec['rx']*.72))**2+((tz-cz)/((zt-zb)*.52))**2)<1
   keep_hair=hair&((~central)|(ty<dep-.018)) if name=='Mint' else hair
   m=(1-smooth(.90,1.08,radius))*eligible*(~keep_hair)
   curve=cz-.006+.013*((tx-cx)/(spec['rx']*.90))**2
   lash=(1-smooth(.0015,.0033,abs(tz-curve)))*(1-smooth(.86,1.04,abs(tx-cx)/(spec['rx']*.92)))
   lower,upper=eye_tones[eye_index];blend=smooth(zb-.01,zt+.015,tz)[:,None]
   tone=lower*(1-blend)+upper*blend
   take=m>mask;eyelid_rgb[take]=tone[take]
   mask=np.maximum(mask,m);line=np.maximum(line,lash)
  blink[iy,ix,:3]=eyelid_rgb*(1-line[:,None])+np.array([.10,.027,.035])*line[:,None]
  blink[iy,ix,3]=np.maximum(blink[iy,ix,3],mask)
  mz=np.mean(spec['original_open']) if 'original_open' in spec else spec['mz'];mr=spec['mr']
  radius=np.sqrt((tx/(mr*1.30))**2+((tz-mz)/.033)**2)
  mouth_mask=(1-smooth(.88,1.10,radius))*eligible
  opening=np.sqrt((tx/(mr*.95))**2+((tz-(mz-.003))/.024)**2)
  inner=1-smooth(.91,1.03,opening)
  tongue=smooth(mz+.003,mz-.017,tz)
  cavity=np.array([.19,.025,.045])*(1-tongue[:,None])+np.array([.91,.24,.35])*tongue[:,None]
  mouth[iy,ix,:3]=skin_rgb*(1-inner[:,None])+cavity*inner[:,None]
  mouth[iy,ix,3]=np.maximum(mouth[iy,ix,3],mouth_mask)
  changed+=int((mask>.5).sum())
 assert changed>500,(name,'missing eyelid artwork',changed)
 for label,pixels in [('Blink',blink),('Mouth',mouth)]:
  # UV island padding prevents filtering from revealing triangle seams.
  for _ in range(3):
   padded=pixels.copy()
   for dy,dx in [(0,1),(0,-1),(1,0),(-1,0)]:
    neighbor=np.roll(pixels,(dy,dx),(0,1));take=(~covered)&(pixels[:,:,3]<.01)&(neighbor[:,:,3]>padded[:,:,3])
    padded[take]=neighbor[take]
   pixels=padded
  tex=bpy.data.images.new(name+'_'+label,width=w,height=h,alpha=True)
  tex.pixels.foreach_set(pixels.ravel());tex.filepath_raw=str(OUT/(name+'_'+label+'.png'));tex.file_format='PNG';tex.save()
 return protected,front
