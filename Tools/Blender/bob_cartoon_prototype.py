"""Editable cartoon visual proof. Isolated from the current gameplay assets.

blender -b --python Tools/Blender/bob_cartoon_prototype.py -- --draft
Remove --draft for the larger render. The existing SDF mesher is reused.
"""
import json
import math
import random
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parent.parent
sys.path.insert(0, str(HERE))
import bob_characters as art

OUT = Path(r'C:\Users\acer\Documents\Codex\2026-10-04\buk\outputs\BiteOrBetray_VisualPrototype')
UNITY = PROJECT / 'Assets' / 'BiteOrBetray' / 'VisualPrototype'
OUT.mkdir(parents=True, exist_ok=True)
UNITY.mkdir(parents=True, exist_ok=True)
DRAFT = '--draft' in sys.argv
random.seed(42)
bpy.ops.wm.read_factory_settings(use_empty=True)
art.TRI_SCALE = 1
MAT = {}
GROUPS = {}
TABLE_Z = .78

def linear(hexcode):
    s = [int(hexcode.lstrip('#')[i:i+2],16)/255 for i in (0,2,4)]
    return [v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in s]

def mat(name, color, rough=.5, metal=0, transmission=0):
    m=bpy.data.materials.new('Cartoon_'+name)
    m.diffuse_color=(*linear(color),1)
    m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=m.diffuse_color
    p.inputs['Roughness'].default_value=rough
    p.inputs['Metallic'].default_value=metal
    p.inputs['Transmission Weight'].default_value=transmission
    p.inputs['IOR'].default_value=1.45
    MAT[name]=m
    art._materials[name]=m
    return m

for args in [
 ('Cream','#FFF0D2',.65),('Wall','#EADBC2',.75),('Teal','#217C7E',.4),
 ('TealDark','#15494E',.5),('Wood','#B66F39',.48),('WoodDark','#64422D',.55),
 ('Gold','#DCAF5A',.27,.65),('Milk','#FFF7E7',.42),('Ink','#20282C',.35),
 ('Skin','#E9AF82',.58),('Blush','#BD7055',.7),('Hair','#261D19',.48),
 ('SkinCrease','#C48D64',.65),('HairRidge','#35251D',.48),
 ('Green','#344E3E',.7),('Shirt','#E7D8AD',.7),('Iris','#74503B',.35),
 ('MouthDark','#562A2A',.6),('Tongue','#DA807E',.55),
 ('Yellow','#FFC62C',.2),('Blue','#188FD6',.17),('Red','#E34845',.2),
 ('Glass','#E8F9FA',.05,0,1),('Berry','#B44947',.7),
 ('Blockout','#9CACAA',.7),('MatBlue','#425D7A',.75),('Cupcake','#784534',.7),
 ('Window','#BDE0ED',.3),('PlaceholderBlue','#7795AB',.75),
 ('PlaceholderPink','#BA8F8C',.75),('PlaceholderLavender','#A49AB9',.75),
 ('PlaceholderRed','#AE7C70',.75),
]: mat(*args)

for name in ['Skin','Green','Shirt']:
    nodes=MAT[name].node_tree.nodes;links=MAT[name].node_tree.links
    noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=160 if name=='Skin' else 300
    noise.inputs['Detail'].default_value=2
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.10 if name=='Skin' else .28
    bump.inputs['Distance'].default_value=.0006 if name=='Skin' else .0009
    links.new(noise.outputs['Fac'],bump.inputs['Height'])
    links.new(bump.outputs['Normal'],nodes.get('Principled BSDF').inputs['Normal'])
    if name=='Skin':
        p=nodes.get('Principled BSDF');p.inputs['Subsurface Weight'].default_value=.06
        p.inputs['Subsurface Scale'].default_value=.018

def group(name):
    co=bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(co)
    GROUPS[name]=co
    return co

for name in ['Room','Table','Dispenser','Inventory','Hero','SeatBlockouts','PlayerHands','Lighting']:
    group(name)

def assign(ob,name,material,collection):
    ob.name=name
    if material: ob.data.materials.append(MAT[material])
    for co in list(ob.users_collection):co.objects.unlink(ob)
    GROUPS[collection].objects.link(ob)
    if ob.type=='MESH':
        for p in ob.data.polygons:p.use_smooth=True
    return ob

def ell(name,p,s,material,collection='Inventory',segments=32):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=16,location=p)
    ob=bpy.context.object
    ob.scale=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return assign(ob,name,material,collection)

def cube(name,p,s,material,collection='Inventory',bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p)
    ob=bpy.context.object
    ob.scale=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=ob.modifiers.new('Rounded edges','BEVEL');mod.width=bevel;mod.segments=3
    normal=ob.modifiers.new('Surface normals','WEIGHTED_NORMAL')
    normal.keep_sharp=True
    return assign(ob,name,material,collection)

def cyl(name,p,r,depth,material,collection='Inventory',bevel=.015,vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=p)
    ob=bpy.context.object
    if bevel:
        mod=ob.modifiers.new('Rounded rim','BEVEL');mod.width=bevel;mod.segments=3
    ob.modifiers.new('Surface normals','WEIGHTED_NORMAL')
    return assign(ob,name,material,collection)

def tube(name,points,r,material,collection='Inventory',radii=None):
    cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D'
    cu.bevel_depth=r;cu.bevel_resolution=3;cu.resolution_u=16
    cu.use_fill_caps=True
    sp=cu.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for i,(v,p) in enumerate(zip(sp.bezier_points,points)):
        v.co=p;v.handle_left_type='AUTO';v.handle_right_type='AUTO'
        if radii:v.radius=radii[i]
    ob=bpy.data.objects.new(name,cu);GROUPS[collection].objects.link(ob)
    cu.materials.append(MAT[material]);return ob

def ring(name,p,r,thick,material,collection='Inventory',rotation=None):
    bpy.ops.mesh.primitive_torus_add(major_segments=48,minor_segments=10,location=p,major_radius=r,minor_radius=thick)
    ob=bpy.context.object
    if rotation:ob.rotation_euler=rotation
    return assign(ob,name,material,collection)

def look(ob,p):
    ob.rotation_euler=(Vector(p)-ob.location).to_track_quat('-Z','Y').to_euler()

def parent(ob,root):
    world=ob.matrix_world.copy();ob.parent=root;ob.matrix_world=world

def root(name,collection):
    ob=bpy.data.objects.new(name,None);GROUPS[collection].objects.link(ob);return ob

def sdf(name,shape,material,collection='Hero',step=.006,triangles=6000):
    v,t=art.polygonize(shape,step,triangles,symmetric=False)
    me=bpy.data.meshes.new(name);me.from_pydata(v.tolist(),[],t.tolist());me.update()
    ob=bpy.data.objects.new(name,me);GROUPS[collection].objects.link(ob)
    me.materials.append(MAT[material]);me.shade_smooth()
    return ob,v

def rigid(ob,arm,bone):
    world=ob.matrix_world.copy();ob.parent=arm;ob.parent_type='BONE';ob.parent_bone=bone
    bpy.context.view_layer.update();ob.matrix_world=world

def cloth_panel(name,points,material,arm):
    n=len(points)
    vertices=list(points)+[(x,y+.012,z) for x,y,z in points]
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    me=bpy.data.meshes.new(name);me.from_pydata(vertices,[],faces);me.update()
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    ob=bpy.data.objects.new(name,me);GROUPS['Hero'].objects.link(ob)
    me.materials.append(MAT[material])
    bevel=ob.modifiers.new('Soft cloth edge','BEVEL');bevel.width=.004;bevel.segments=2
    ob.modifiers.new('Cloth normals','WEIGHTED_NORMAL')
    rigid(ob,arm,'Spine')
    return ob

def eyelid(c,short,center,radii,side):
    x,y,z=center;rx,ry,rz=radii
    # A cap on the eyeball covers the iris as the eyelid closes.
    def layout(cut):
        vertices=[];edge=[]
        for u in np.linspace(-.999,.999,33):
            h=math.sqrt(max(0,1-u*u))
            low=min(h, max(-h,cut+side*u*.19))
            for t in np.linspace(0,1,9):
                v=low*(1-t)+h*t
                vertices.append((x+rx*u,y-ry*math.sqrt(max(0,1-u*u-v*v))-.017,z+rz*v))
            edge.append(vertices[-9])
        return vertices,edge
    base,edge=layout(.12 if side<0 else .24)
    faces=[(i*9+j,(i+1)*9+j,(i+1)*9+j+1,i*9+j+1) for i in range(32) for j in range(8)]
    me=bpy.data.meshes.new('Lid_'+short);me.from_pydata(base,[],faces);me.update();me.shade_smooth()
    ob=bpy.data.objects.new('Lid_'+short,me);GROUPS['Hero'].objects.link(ob);me.materials.append(MAT['Skin'])
    rigid(ob,c.arm,'Head')
    ob.shape_key_add(name='Basis')
    for name,cut in [('Squint',-.60),('Wide',.88)]:
        key=ob.shape_key_add(name=name);key.data.foreach_set('co',np.asarray(layout(cut)[0]).ravel());key.value=0
    return ob

def hero():
    c=art.Character('Cartoon_Hero','Skin')
    c.bone('Spine',(0,0,.80),(0,0,1.24))
    c.bone('Head',(0,0,1.31),(0,0,1.85),'Spine')
    shoulders={}
    for side,label in [(-1,'Left'),(1,'Right')]:
        sh=(side*.34,0,1.23)
        el=(side*.43,-.26,.94)
        wr=(-.13,-.27,1.28) if side<0 else (side*.23,-.44,.87)
        c.bone('Arm_'+label,sh,el,'Spine')
        c.bone('Forearm_'+label,el,wr,'Arm_'+label)
        tail=(wr[0],wr[1],wr[2]+.10) if side<0 else (wr[0],wr[1]-.10,wr[2])
        c.bone('Hand_'+label,wr,tail,'Forearm_'+label)
        shoulders[label]=(sh,el,wr)
    c.arm=art.build_armature(c.name,c.bones)
    for co in list(c.arm.users_collection):co.objects.unlink(c.arm)
    GROUPS['Hero'].objects.link(c.arm)

    shape=art.Shape().add(art.RBox((0,.0,1.56),(.205,.145,.172),.11))
    shape.add(art.RBox((0,-.018,1.412),(.214,.142,.085),.078),None,.065)
    shape.add(art.Ellipsoid((0,-.087,1.385),(.163,.104,.067)),None,.04)
    shape.add(art.Ellipsoid((0,0,1.675),(.213,.153,.16)),None,.055)
    for s in (-1,1):
        shape.add(art.Ellipsoid((s*.157,-.105,1.515),(.075,.073,.073)),None,.036)
        shape.add(art.Ellipsoid((s*.228,-.002,1.574),(.048,.037,.074)),None,.023)
    shape.add(art.Ellipsoid((0,-.184,1.556),(.065,.068,.044)),None,.025)
    shape.add(art.Ellipsoid((0,-.178,1.611),(.029,.032,.072)),None,.023)
    for s in (-1,1):
        shape.add(art.Ellipsoid((s*.040,-.183,1.546),(.027,.049,.025)),None,.018)
    c.body=shape
    c.face={'center':art.vec(0,0,1.55),'mouth':dict(w=.243,z=1.463,smile=.025,rest_open=.006,teeth=True,mats=['MouthDark','Milk','Tongue'])}
    ob,_=sdf('HeadSkin',shape,'Skin',triangles=11500,step=.005)
    rigid(ob,c.arm,'Head')
    neck,_=sdf('Neck',art.Shape().add(art.Cone((0,0,1.24),(0,0,1.38),.09,.095)),'Skin',triangles=700)
    rigid(neck,c.arm,'Spine')

    jacket=art.Shape().add(art.Ellipsoid((0,.025,1.04),(.39,.22,.285)))
    jacket.add(art.RBox((0,.025,1.22),(.35,.18,.08),.07),None,.08)
    for label,(sh,el,wr) in shoulders.items():
        jacket.add(art.Cone(sh,el,.123,.088),None,.05)
        jacket.add(art.Cone(el,wr,.093,.065),None,.035)
    body,v=sdf('Jacket',jacket,'Green',triangles=7600)
    names=[b[0] for b in c.bones]
    parts=art.Shape().add(art.Ellipsoid((0,0,1.04),(.35,.22,.285)),'Spine')
    for label,(sh,el,wr) in shoulders.items():
        parts.add(art.Cone(sh,el,.12,.09),'Arm_'+label)
        parts.add(art.Cone(el,wr,.09,.06),'Forearm_'+label)
    mod=body.modifiers.new('Armature','ARMATURE');mod.object=c.arm
    art.apply_weights(body,names,art.skin_weights(v,parts.parts,names))
    body.parent=c.arm
    shirt=cube('Shirt front',(0,-.204,1.123),(.21,.025,.265),'Shirt','Hero',.04)
    rigid(shirt,c.arm,'Spine')
    for s in (-1,1):
        cloth_panel('Jacket lapel',[(s*.08,-.250,1.30),(s*.26,-.250,1.25),
            (s*.11,-.250,1.095)],'Green',c.arm)
        cloth_panel('Shirt collar',[(s*.025,-.256,1.321),(s*.123,-.256,1.302),
            (s*.16,-.256,1.194),(s*.057,-.256,1.226)],'Milk',c.arm)
        ear=ell('Ear inset',(s*.244,-.032,1.576),(.020,.009,.035),'SkinCrease','Hero',24)
        rigid(ear,c.arm,'Head')
        fold=tube('Ear fold',[(s*.254,-.043,1.604),(s*.260,-.044,1.584),(s*.248,-.045,1.559)],.004,'Skin','Hero',radii=[.4,1,.3])
        rigid(fold,c.arm,'Head')
        seam=tube('Jacket shoulder seam',[(s*.16,-.065,1.312),(s*.31,-.078,1.295),(s*.39,-.078,1.231)],.003,'TealDark','Hero',radii=[.2,1,.2])
        rigid(seam,c.arm,'Spine')
    for z in [1.05,.975]:
        ob=ell('Jacket button',(.016,-.228,z),(.015,.008,.015),'Gold','Hero')
        rigid(ob,c.arm,'Spine')

    for s,short in [(-1,'L'),(1,'R')]:
        x=s*.102;z=1.634
        p,_=c.surface([x],[z]);yy=p[0][1]-.007
        e=ell('EyeWhite_'+short,(x,yy,z),(.054,.030,.033),'Milk','Hero')
        rigid(e,c.arm,'Head')
        pupil=ell('Pupil_'+short,(x-s*.010,yy-.030,z-.005),(.021,.007,.022),'Iris','Hero')
        rigid(pupil,c.arm,'Head')
        pp=ell('Eye pupil '+short,(x-s*.010,yy-.036,z-.005),(.010,.004,.013),'Ink','Hero')
        rigid(pp,c.arm,'Head')
        sparkle=ell('Eye sparkle '+short,(x-s*.017,yy-.041,z+.001),(.0045,.002,.0045),'Milk','Hero',16)
        rigid(sparkle,c.arm,'Head')
        eyelid(c,short,(x,yy,z),(.054,.030,.033),s)
        xs=np.linspace(s*.031,s*.181,8)
        t=np.linspace(0,1,8)
        zs=1.656+(.028 if s>0 else .045)*t+.010*np.sin(t*math.pi)
        surf,norm=c.surface(xs,zs)
        brow=tube('Brow_'+short,(surf+norm*.011).tolist(),.015,'Hair','Hero',radii=[.1,.8,1,1,.9,.7,.4,.02])
        rigid(brow,c.arm,'Head')
        p,n=c.surface([s*.139,s*.156,s*.169],[1.512,1.489,1.483])
        crease=tube('Smile fold_'+short,(p+n*.002).tolist(),.002,'SkinCrease','Hero',radii=[.05,.6,.05])
        rigid(crease,c.arm,'Head')
        ns=ell('Nostril',(s*.034,-.235,1.541),(.010,.006,.005),'Blush','Hero',16)
        rigid(ns,c.arm,'Head')
    art.build_mouth(c)
    mouth=bpy.data.objects['Mouth']
    mouth.data.flip_normals()
    indices=[]
    for i in range(18):
        for j in range(6):
            u=(i+.5)/9-1;v=(j+.5)/6
            color=1 if v<.34 and abs(u)<.72 else 2 if v>.70 and abs(u)<.55 else 0
            indices.extend([color,color])
    mouth.data.polygons.foreach_set('material_index',np.asarray(indices,dtype=np.int32))
    # The same asymmetric smile is applied to every key, so reactions keep the facial surface.
    origin=mouth.matrix_world.translation
    for key in mouth.data.shape_keys.key_blocks:
        points=np.asarray([v.co[:] for v in key.data])+np.asarray(origin)
        points[:,2] += points[:,0]*.048
        p,n=c.surface(points[:,0],points[:,2])
        key.data.foreach_set('co',(p+n*.0018-np.asarray(origin)).ravel())
    for co in list(mouth.users_collection):co.objects.unlink(mouth)
    GROUPS['Hero'].objects.link(mouth)
    hair=art.Shape().add(art.Ellipsoid((0,.026,1.706),(.221,.159,.139)))
    hair.inter(art.Plane((0,0,1.707),(0,-.32,-1)))
    hair.add(art.Ellipsoid((-.074,-.035,1.801),(.135,.106,.070)),None,.031)
    hair.add(art.Ellipsoid((.034,.015,1.835),(.125,.110,.064)),None,.032)
    hair.add(art.Ellipsoid((.139,.014,1.774),(.067,.104,.071)),None,.026)
    hh,_=sdf('Hair mass',hair,'Hair',triangles=4600,step=.005);rigid(hh,c.arm,'Head')
    for i in range(4):
        a=-.165+i*.046
        ob=tube('Swept hair lock '+str(i),[(a,-.060,1.755),(a-.036,-.093,1.808),
            (a+.027,-.005,1.872-i*.004),(a+.051,.074,1.851-i*.007)],.016 if i>4 else .022,
            'HairRidge','Hero',radii=[.02,1,.75,.015])
        rigid(ob,c.arm,'Head')
    for s in [-1,1]:
        ob=tube('Sideburn',[(s*.206,.011,1.726),(s*.222,-.006,1.655),(s*.218,-.003,1.608)],.014,'Hair','Hero',radii=[1,.8,.04])
        rigid(ob,c.arm,'Head')
    for s,label in [(-1,'Left'),(1,'Right')]:
        sh,el,wr=shoulders[label]
        if s<0:
            hand=art.Shape().add(art.Ellipsoid((-.113,-.263,1.323),(.056,.047,.069)))
            for i in range(3):
                z=1.316+i*.032
                hand.add(art.Cone((-.147,-.294,z),(-.077,-.304,z+.009),.017,.015),None,.012)
            hand.add(art.Cone((-.063,-.261,1.298),(-.052,-.294,1.354),.022,.018),None,.018)
        else:
            hand=art.glove(art.vec(*wr),art.vec(*wr)-art.vec(*el),s,1.5,False,False)
        ho,_=sdf('Hand_'+label+'_Mesh',hand,'Skin',triangles=1800,step=.004)
        rigid(ho,c.arm,'Hand_'+label)
        cuff=ell('Sleeve cuff '+label,wr,(.073,.059,.033) if s<0 else (.074,.063,.057),'Shirt','Hero')
        rigid(cuff,c.arm,'Forearm_'+label)
    c.arm.location=(0,1.52,0)
    c.arm['production_status']='Hero prototype: visual proof, rig and mouth shapes. Not integrated with gameplay.'
    return c

def chair(x,y,angle):
    r=root('Chair','SeatBlockouts')
    for ob in [cyl('Chair seat',(0,0,.47),.25,.10,'Berry','SeatBlockouts'),
               cube('Chair back',(0,.20,.77),(.48,.08,.54),'Berry','SeatBlockouts',.10),
               cube('Chair frame',(0,.245,.78),(.53,.065,.60),'WoodDark','SeatBlockouts',.08)]:parent(ob,r)
    for sx in [-1,1]:
        for sy in [-1,1]:parent(cube('Chair leg',(sx*.16,sy*.15,.24),(.05,.05,.46),'WoodDark','SeatBlockouts',.01),r)
    r.location=(x,y,0);r.rotation_euler.z=angle

def blockout(name,x,y,angle,shade,width=.2,height=1.52,long=False):
    r=root(name,'SeatBlockouts')
    r['production_status']='Silhouette placeholder only. Not a finished character.'
    objs=[ell(name+' torso',(0,0,1.01),(width+.07,.17,.24),shade,'SeatBlockouts'),
          ell(name+' neck',(0,0,1.28),(.075,.07,.11),'Blockout','SeatBlockouts'),
          ell(name+' head',(0,-.018,height),(width,.155,.25 if long else .20),'Blockout','SeatBlockouts'),
          ell(name+' nose',(0,-.177,height-.015),(.032,.047,.035),'Blockout','SeatBlockouts')]
    for s in [-1,1]:
        objs.append(ell(name+' eye',(s*width*.38,-.155,height+.027),(.038,.018,.041),'Milk','SeatBlockouts'))
        objs.append(ell(name+' pupil',(s*width*.38,-.173,height+.023),(.013,.007,.02),'TealDark','SeatBlockouts'))
        objs.append(tube(name+' arm',[(s*(width+.02),0,1.14),(s*(width+.10),-.15,.84),(s*.16,-.32,.82)],.063,shade,'SeatBlockouts'))
        objs.append(ell(name+' hand',(s*.16,-.35,.83),(.067,.07,.037),'Blockout','SeatBlockouts'))
    for o in objs:parent(o,r)
    r.location=(x,y,0);r.rotation_euler.z=angle

def room():
    cube('Floor',(0,0,-.08),(7,8,.14),'WoodDark','Room',0)
    cube('Rear wall',(0,3.15,1.7),(7,.16,3.5),'Wall','Room',0)
    cube('Left wall',(-3.45,0,1.7),(.16,7,3.5),'Wall','Room',0)
    cube('Right wall',(3.45,0,1.7),(.16,7,3.5),'Wall','Room',0)
    cube('Rear baseboard',(0,3.045,.15),(7,.08,.23),'TealDark','Room',.02)
    for x in [-2.65,2.65]:
        cube('Window',(x,3.05,1.90),(1.05,.055,1.62),'Window','Room',.18)
        for xx in [x-.52,x+.52]:cube('Window trim',(xx,3.00,1.90),(.075,.065,1.62),'Teal','Room',.02)
        cube('Window middle',(x,2.99,1.90),(.052,.055,1.55),'Teal','Room',.01)
        cube('Window crossbar',(x,2.99,1.90),(1.05,.055,.052),'Teal','Room',.01)
        cube('Window sill',(x,2.93,1.10),(1.19,.25,.08),'Teal','Room',.025)
    cube('Kitchen door',(0,3.015,1.17),(1.15,.10,2.28),'TealDark','Room',.12)
    cube('Kitchen door panel',(0,2.94,1.18),(.97,.065,2.06),'Teal','Room',.10)
    ell('Door window',(0,2.891,1.63),(.19,.022,.25),'Window','Room')
    ell('Door knob',(.35,2.854,1.03),(.04,.027,.04),'Gold','Room')
    for x in [-1.56,1.56]:
        for z in [1.45,2.16]:
            cube('Shelf',(x,2.83,z),(1.18,.43,.08),'Wood','Room',.025)
            for dx in [-.38,0,.38]:
                pp=(x+dx,2.82,z+.22)
                cyl('Candy shelf jar',pp,.115,.32,'Glass','Room',.025,32)
                cyl('Candy shelf lid',(pp[0],pp[1],z+.40),.13,.044,'Gold','Room',.01,32)
                for j in range(8):
                    p=(pp[0]+random.uniform(-.05,.05),pp[1]+random.uniform(-.05,.05),z+.085+j*.026)
                    ell('Shelf sweet',p,(.039,.033,.026),['Yellow','Blue','Red'][j%3],'Room',16)
    for x in [-3.05,3.05]:
        cube('Wall sconce',(x,2.9,2.40),(.12,.10,.28),'Gold','Room',.025)
        ell('Sconce globe',(x,2.82,2.55),(.10,.10,.13),'Milk','Room')
    for j in range(12):
        yy=-3+j*.5
        cube('Floor plank joint',(0,yy,.005),(6.8,.008,.003),'Wood','Room',0)

def table_and_dispenser():
    cyl('Table pedestal',(0,0,.34),.28,.68,'TealDark','Table',.04)
    cyl('Table foot',(0,0,.08),.58,.10,'WoodDark','Table',.04)
    cyl('Table rim',(0,0,.73),1.30,.13,'WoodDark','Table',.045,96)
    cyl('Table top',(0,0,.775),1.28,.075,'Wood','Table',.026,96)
    ring('Table brass inlay',(0,0,.815),1.19,.004,'Gold','Table')
    cyl('Rotating middle',(0,0,.823),.56,.033,'WoodDark','Table',.012)
    ring('Middle rim',(0,0,.842),.53,.015,'Gold','Table')
    r=root('CandyDispenser','Dispenser')
    r['production_status']='Mechanical parts separated for animation; no gameplay binding yet.'
    initial=set(bpy.data.objects)
    cyl('Dispenser base',(0,.06,.886),.48,.09,'Teal','Dispenser',.025)
    cyl('Dispenser lower trim',(0,.06,.851),.495,.022,'Gold','Dispenser',.008)
    cube('Dispenser chassis',(0,.055,.963),(.91,.32,.12),'Teal','Dispenser',.035)
    for i,(x,color) in enumerate([(-.285,'Yellow'),(0,'Blue'),(.285,'Red')]):
        p=(x,.075,1.139)
        jar=ell('Jar_'+color,p,(.15,.15,.20),'Glass','Dispenser',48)
        jar['prototype_note']='Glass material is a Blender render material; mobile material must be measured.'
        cyl('Jar foot '+color,(x,.075,.976),.146,.045,'Gold','Dispenser',.012)
        cyl('Lid_'+color,(x,.075,1.31),.15,.038,'Gold','Dispenser',.009)
        ell('Lid dome '+color,(x,.075,1.331),(.135,.135,.037),'Gold','Dispenser')
        ell('Lid knob '+color,(x,.075,1.379),(.027,.027,.03),'Gold','Dispenser')
        for layer in range(4):
            rr=.07 if layer!=3 else .055
            for k in range(5):
                a=k*math.tau/5+layer*.43
                cp=(x+rr*math.cos(a),.075+rr*math.sin(a),1.01+layer*.065)
                candy=ell('Candy_'+color+'_'+str(layer*5+k),cp,(.041,.038,.033),color,'Dispenser',20)
                candy.rotation_euler=(random.random(),random.random(),random.random())
    for xx in [-.29,.29]:
        tube('Front trim',[(xx,-.105,1.02),(xx,-.125,.934),(xx,-.25,.884)],.012,'Gold','Dispenser')
    knob=cyl('Knob',(0,-.154,.958),.081,.040,'Gold','Dispenser',.009)
    knob.rotation_euler.x=math.pi/2
    grab=cube('Knob grip',(0,-.182,.958),(.023,.035,.117),'WoodDark','Dispenser',.009)
    grab.rotation_euler.y=-.48
    cube('Chute back',(0,-.207,.889),(.185,.085,.12),'Gold','Dispenser',.025)
    cube('Chute tongue',(0,-.299,.849),(.19,.19,.026),'Gold','Dispenser',.015)
    for x in [-.087,.087]:cube('Chute rail',(x,-.299,.87),(.022,.18,.045),'Gold','Dispenser',.01)
    clock=cyl('Timer frame',(0,.069,1.469),.094,.039,'Gold','Dispenser',.009)
    clock.rotation_euler.x=math.pi/2
    face=cyl('Timer dial',(0,.043,1.469),.081,.009,'Milk','Dispenser',.003)
    face.rotation_euler.x=math.pi/2
    for i in range(12):
        a=i*math.tau/12
        ob=cube('Clock tick',(math.sin(a)*.065,.035,1.469+math.cos(a)*.065),(.005,.005,.012),'Ink','Dispenser',.001)
        ob.rotation_euler.y=a
    tube('Clock hand',[(0,.029,1.469),(.04,.029,1.512)],.004,'Berry','Dispenser')
    for ob in set(bpy.data.objects)-initial:parent(ob,r)
    r.scale=(.78,.78,.78)
    r.location.z=TABLE_Z*(1-.78)

def heart(name,p,size,material='Red'):
    x,y,z=p
    shape=art.Shape().add(art.Ellipsoid((x-size*.20,y,z+size*.14),(size*.28,size*.10,size*.28)))
    shape.add(art.Ellipsoid((x+size*.20,y,z+size*.14),(size*.28,size*.10,size*.28)),None,size*.13)
    shape.add(art.Cone((x,y,z+size*.09),(x,y,z-size*.30),size*.24,size*.03),None,size*.12)
    return sdf(name,shape,material,'Inventory',step=.003,triangles=450)[0]

def mug(x,y):
    r=root('HotMilk','Inventory')
    objs=[cyl('Mug',(x,y,.866),.060,.11,'Milk',bevel=.008),
          cyl('Milk surface',(x,y,.923),.047,.002,'Cream',bevel=0),
          ring('Mug rim',(x,y,.925),.054,.007,'Milk'),
          ring('Mug handle',(x+.073,y,.865),.036,.010,'Milk',rotation=(math.pi/2,0,0))]
    for o in objs:parent(o,r)

def cupcake(x,y):
    cyl('Cupcake wrapper',(x,y,.848),.043,.070,'Cupcake',bevel=.009,vertices=24)
    cyl('Cupcake plate',(x,y,.817),.067,.009,'Milk',bevel=.003)
    pts=[]
    for t in np.linspace(0,1,65):
        a=t*math.tau*3.4;r=.037*(1-t)
        pts.append((x+r*math.cos(a),y+r*math.sin(a),.881+.072*t))
    tube('Frosting',pts,.017,'Cream')
    ell('Cherry',(x,y,.965),(.013,.013,.013),'Red',segments=16)

def pocket(x,y):
    ell('LittlePocket',(x,y,.854),(.062,.048,.056),'MatBlue')
    cyl('Pocket neck',(x,y,.90),.025,.036,'MatBlue',bevel=.008)
    ring('Pocket cord',(x,y,.891),.027,.004,'Gold')
    tube('Pocket string',[(x+.02,y,.89),(x+.08,y-.02,.849),(x+.04,y-.06,.824)],.003,'Gold')

def fork(x,y):
    cube('Fork handle',(x,y,.825),(.022,.145,.025),'Berry',bevel=.01)
    cube('Fork shoulder',(x,y+.089,.825),(.065,.040,.016),'Milk',bevel=.01)
    for i in range(4):cube('Fork prong',(x-.024+i*.016,y+.128,.825),(.009,.065,.014),'Milk',bevel=.004)

def mallet(x,y):
    cyl('Hammer handle',(x,y,.83),.015,.21,'Wood',bevel=.005).rotation_euler.x=math.pi/2
    ob=cyl('Hammer head',(x,y+.11,.862),.043,.11,'Wood',bevel=.01)
    ob.rotation_euler.y=math.pi/2
    for xx in [-.056,.056]:cyl('Hammer cap',(x+xx,y+.11,.862),.044,.019,'Berry',bevel=.004).rotation_euler.y=math.pi/2

def inventory(x,y,angle):
    r=root('InventoryMat','Inventory')
    before=set(bpy.data.objects)
    cube('Inventory mat',(0,0,.812),(.58,.29,.012),'MatBlue',bevel=.017)
    mug(-.17,.015);cupcake(-.015,.035);pocket(.20,.035);fork(-.20,-.11);mallet(.22,-.14)
    cyl('Clover coin',(.065,-.065,.83),.031,.008,'Gold',bevel=.002)
    for a in [0,math.pi/2,math.pi,math.pi*1.5]:
        ell('Clover leaf',(.065+math.cos(a)*.009,-.065+math.sin(a)*.009,.835),(.010,.010,.0018),'Teal',segments=12)
    card=cube('Health card',(0,.135,.875),(.22,.019,.076),'Milk',bevel=.006)
    for i in range(5):heart('HP_'+str(i),(-.08+i*.04,.123,.876),.043,'Red' if i<4 else 'Gold')
    for o in set(bpy.data.objects)-before:
        if o.parent is None:parent(o,r)
    r.location=(x,y,0);r.rotation_euler.z=angle

def player_hands():
    r=root('PlayerHandProof','PlayerHands')
    before=set(bpy.data.objects)
    for s in [-1,1]:
        wr=art.vec(s*.33,-1.10,.92 if s<0 else 1.055)
        el=art.vec(s*.43,-1.66,.70)
        sleeve=art.Shape().add(art.Cone(el,wr,.10,.073))
        sdf('Player sleeve',sleeve,'MatBlue','PlayerHands',step=.006,triangles=1800)
        if s<0:
            hand=art.glove(wr,wr-el,s,1.8,False,False)
        else:
            hand=art.Shape().add(art.Ellipsoid((.33,-1.09,1.07),(.076,.053,.064)))
            for i in range(3):
                a=(.29+i*.032,-1.035,1.077);b=(a[0]+.005,-1.007,1.13)
                hand.add(art.Cone(a,b,.018,.015),None,.013)
            hand.add(art.Cone((.275,-1.09,1.073),(.24,-1.025,1.115),.024,.018),None,.012)
            hand.add(art.Cone((.31,-1.046,1.104),(.27,-.998,1.151),.017,.012),None,.008)
        sdf('Player hand L' if s<0 else 'Player hand R',hand,'Skin','PlayerHands',step=.004,triangles=2600)
    ell('Held blue candy',(.259,-1.004,1.143),(.04,.031,.032),'Blue','PlayerHands',32)
    for ob in set(bpy.data.objects)-before:parent(ob,r)

def lighting():
    scene=bpy.context.scene
    scene.world=bpy.data.worlds.new('Candy parlour ambient')
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes['Background']
    bg.inputs[0].default_value=(.44,.55,.62,1);bg.inputs[1].default_value=.30
    for name,pos,target,power,size,color in [
        ('Warm ceiling',(0,-.10,3.1),(0,0,.8),420,3.0,(1,.83,.63)),
        ('Window fill',(-2.8,-.5,2.6),(0,1,1.2),350,2.7,(.72,.88,1)),
        ('Camera fill',(0,-2.2,2.35),(0,1.4,1.3),230,2.0,(1,.91,.80)),
        ('Hair rim',(1.6,2.4,2.9),(0,1.4,1.5),320,1.8,(1,.76,.49)),
    ]:
        light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size;light.color=color
        ob=bpy.data.objects.new(name,light);GROUPS['Lighting'].objects.link(ob);ob.location=pos;look(ob,target)
    cyl('Pendant shade',(0,.3,2.6),.50,.13,'Teal','Room',.035)
    ell('Pendant globe',(0,.3,2.55),(.32,.32,.09),'Milk','Room')
    tube('Pendant cord',[(0,.3,2.68),(0,.3,3.3)],.012,'WoodDark','Room')

def camera(name,pos,target,lens):
    data=bpy.data.cameras.new(name);ob=bpy.data.objects.new(name,data)
    bpy.context.scene.collection.objects.link(ob);ob.location=pos;look(ob,target)
    data.lens=lens;data.clip_start=.02;data.clip_end=30
    return ob

def export_collection(name,path):
    bpy.ops.object.select_all(action='DESELECT')
    objs=list(GROUPS[name].objects)
    for ob in objs:
        if ob.type=='CURVE':
            bpy.context.view_layer.objects.active=ob;ob.select_set(True)
            bpy.ops.object.convert(target='MESH');ob.select_set(False)
    for ob in GROUPS[name].objects:
        if ob.type in ['MESH','CURVE','ARMATURE','EMPTY']:ob.select_set(True)
    if name=='Hero':
        arm=bpy.data.objects['Cartoon_Hero']
        old=arm.location.copy();arm.location=(0,0,0)
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',
        add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE',use_mesh_modifiers=True,
        primary_bone_axis='Y',secondary_bone_axis='X')
    if name=='Hero':arm.location=old

room();table_and_dispenser()
main=hero()
for name,x,y,ang,shade,width,h,long in [
 ('Left_Round_Blockout',-1.35,.60,.70,'PlaceholderPink',.23,1.47,False),
 ('Left_Tall_Blockout',-.85,1.34,.37,'PlaceholderBlue',.145,1.61,True),
 ('Right_Curl_Blockout',.85,1.34,-.37,'PlaceholderLavender',.19,1.48,False),
 ('Right_Round_Blockout',1.35,.60,-.70,'PlaceholderRed',.23,1.48,False),
]:
    chair(x,y,ang);blockout(name,x,y,ang,shade,width,h,long)
chair(0,1.52,0)
for x,y,a in [(0,-.99,0),(-.88,.24,-1.1),(-.55,.86,-.5),(.55,.86,.5),(.88,.24,1.1),(0,1.04,math.pi)]:
    inventory(x,y,a)
player_hands();lighting()
scene=bpy.context.scene
comparison=camera('Comparison_Seated',(0,-1.83,1.48),(0,.45,1.19),23)
camera('Gameplay_Seat',(0,-1.55,1.34),(0,.30,1.20),23)
portrait=camera('Hero_Portrait',(.26,.29,1.72),(0,1.46,1.54),48)
scene.camera=comparison
scene.render.engine='CYCLES';scene.cycles.samples=24 if DRAFT else 64
scene.cycles.use_denoising=True
scene.cycles.max_bounces=7;scene.cycles.transmission_bounces=6
scene.cycles.device='GPU'
prefs=bpy.context.preferences.addons['cycles'].preferences
try:
    prefs.compute_device_type='CUDA';prefs.get_devices()
    for dev in prefs.devices:dev.use=dev.type=='CUDA'
    if not any(dev.use for dev in prefs.devices):scene.cycles.device='CPU'
except Exception:scene.cycles.device='CPU'
print('Render device',scene.cycles.device,flush=True)
scene.render.resolution_x=1280 if DRAFT else 1920
scene.render.resolution_y=720 if DRAFT else 1080
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
scene.view_settings.exposure=-1.45
scene.unit_settings.system='METRIC'
scene['reference']='Selected cartoon 3D concept; image is reference, not scene geometry.'
scene['production_status']='Stages 1-4 foundations; stage 5 hero prototype. Other seats are unfinished silhouette placeholders.'
scene['gameplay_status']='Visual proof only. No transport or gameplay integration changes.'
ref=bpy.data.images.load(str(OUT.parent.parent / 'work' / 'visual-production' / 'CartoonReference.png'))
ref.pack()

export_collection('Hero',UNITY/'Cartoon_Hero_Prototype.fbx')
export_collection('Dispenser',UNITY/'Cartoon_Dispenser_Prototype.fbx')
for name in ['Table','Room','Inventory','PlayerHands','SeatBlockouts']:
    export_collection(name,UNITY/('Cartoon_'+name+'_Prototype.fbx'))
scene.camera=comparison
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA'
        area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BiteOrBetray_Cartoon_VisualProof.blend'))
scene.render.filepath=str(OUT/'BiteOrBetray_Cartoon_VisualProof.png')
bpy.ops.render.render(write_still=True)
scene.camera=portrait
scene.render.resolution_x=900;scene.render.resolution_y=900
scene.render.filepath=str(OUT/'BiteOrBetray_Hero_Prototype.png')
bpy.ops.render.render(write_still=True)
mouth=bpy.data.objects['Mouth'].data.shape_keys.key_blocks
lids=[bpy.data.objects['Lid_'+s].data.shape_keys.key_blocks for s in ['L','R']]
for mood,values in [('Good',{'Open':.3,'Grin':.8,'Wide':.8}),('Bad',{'Frown':1,'Squint':.75})]:
    for key in mouth:
        if key.name!='Basis':key.value=values.get(key.name,0)
    for lid in lids:
        for key in lid:
            if key.name!='Basis':key.value=values.get(key.name,0)
    scene.render.filepath=str(OUT/('BiteOrBetray_Hero_Expression_'+mood+'.png'))
    bpy.ops.render.render(write_still=True)
for keys in [mouth,*lids]:
    for key in keys:
        if key.name!='Basis':key.value=0
scene.render.fps=24;scene.frame_start=1;scene.frame_end=72
for frame,values in [(1,{}),(12,{}),(24,{'Open':.3,'Grin':.8,'Wide':.8}),
        (36,{'Open':.3,'Grin':.8,'Wide':.8}),(48,{'Frown':1,'Squint':.75}),
        (60,{'Frown':1,'Squint':.75}),(72,{})]:
    for keys in [mouth,*lids]:
        for key in keys:
            if key.name!='Basis':
                key.value=values.get(key.name,0);key.keyframe_insert(data_path='value',frame=frame)
scene.frame_set(1)
scene.camera=comparison
scene.render.resolution_x=1280 if DRAFT else 1920
scene.render.resolution_y=720 if DRAFT else 1080
scene.render.filepath=str(OUT/'BiteOrBetray_Cartoon_VisualProof.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BiteOrBetray_Cartoon_VisualProof.blend'))
manifest={'status':'visual prototype, not final likeness approval or game integration',
    'revision':2,'units':'meters','table_height':TABLE_Z,'table_radius':1.28,'hero_bones':len(main.bones),
    'hero_changes':['smaller eyes with keyed lids','asymmetric smile','reworked jaw and cheeks','swept hair','chin hand pose','cloth lapels'],
    'expression_preview':'Blender timeline frames 1-72 at 24fps. Smug, good and bad. No gameplay binding.',
    'unfinished':['four seat silhouettes','waiter','finger rig','final facial likeness','mobile materials','VR integration'],
    'outputs':[str(p) for p in OUT.glob('*')],'unity_exports':[str(p) for p in UNITY.glob('*.fbx')],
    'collections':{n:len(co.objects) for n,co in GROUPS.items()},
    'mesh_triangles':sum(sum(len(p.vertices)-2 for p in ob.data.polygons) for ob in bpy.data.objects if ob.type=='MESH')}
assert len(main.bones)==8
assert bpy.data.objects['Mouth'].data.shape_keys is not None
for lid in lids:
    assert len(lid)==3 and all(k.value==0 for k in lid if k.name!='Basis')
    assert all(np.isfinite(tuple(v.co)).all() for k in lid for v in k.data)
assert all((UNITY/f'Cartoon_{n}_Prototype.fbx').is_file() for n in ['Hero','Dispenser','Table'])
(OUT/'Prototype_Checks.json').write_text(json.dumps(manifest,indent=2))
print('PROTOTYPE_DONE',json.dumps(manifest),flush=True)
