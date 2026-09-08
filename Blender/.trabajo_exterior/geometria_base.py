import bpy, math, bmesh, json, sys, bisect
from pathlib import Path
from mathutils import Vector
from math import sin, cos, pi, sqrt

OUT=Path(r'C:\Users\jrjav\Documents\GitHub\Criminal-Game\Blender\carroceria_referencia')
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections):
    if c.name!='Collection': bpy.data.collections.remove(c)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.render.resolution_x=1400; scene.render.resolution_y=900; scene.render.resolution_percentage=100
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.24,.27,.32,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.35
scene.view_settings.view_transform='AgX'
car=bpy.data.collections.new('01 · CARROCERÍA EXTERIOR'); scene.collection.children.link(car)
wheels=bpy.data.collections.new('02 · RUEDAS'); scene.collection.children.link(wheels)
studio=bpy.data.collections.new('03 · ESTUDIO Y VISTAS'); scene.collection.children.link(studio)
def move(ob,col=car):
    for c in list(ob.users_collection): c.objects.unlink(ob)
    col.objects.link(ob); return ob
def mat(name,color,metal=0,rough=.35):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=rough
    return m
paint=mat('01 · Pintura perla cálida',(.63,.67,.70),.58,.24)
paint.node_tree.nodes['Principled BSDF'].inputs['Coat Weight'].default_value=.4
paint.node_tree.nodes['Principled BSDF'].inputs['Coat Roughness'].default_value=.2
black=mat('02 · Juntas y marcos',(.009,.012,.016),.12,.32)
glass=mat('03 · Cristal ahumado',(.009,.018,.026),.12,.22)
chrome=mat('04 · Aluminio pulido',(.58,.63,.68),.88,.22)
rubber=mat('05 · Caucho',(.014,.016,.019),0,.67)
recess=mat('06 · Fondo de tomas',(.008,.011,.014),.05,.55)
lens=mat('07 · Óptica oscura',(.042,.055,.067),.65,.2)
led=mat('08 · Guía de luz',(.8,.88,.96),.3,.2)
red=mat('09 · Pilotos rubí',(.38,.008,.013),.35,.21)
led.node_tree.nodes['Principled BSDF'].inputs['Emission Color'].default_value=(.65,.8,1,1)
led.node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value=.8
red.node_tree.nodes['Principled BSDF'].inputs['Coat Weight'].default_value=.5
allm=[paint,black,glass,chrome,rubber,recess,lens,led,red]

def mesh(name,verts,faces,material=paint,col=car,indices=None):
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update()
    ob=bpy.data.objects.new(name,me); col.objects.link(ob)
    for m in (allm if indices is not None else [material]): me.materials.append(m)
    for i,p in enumerate(me.polygons):
        p.use_smooth=True
        if indices is not None: p.material_index=indices[i]
    bm=bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(me); bm.free()
    return ob

def curve(name,pts,radius,material=black,cyclic=False,col=car):
    cu=bpy.data.curves.new(name,'CURVE'); cu.dimensions='3D'; cu.resolution_u=2
    cu.bevel_depth=radius; cu.bevel_resolution=3; cu.resolution_u=12
    sp=cu.splines.new('POLY'); sp.points.add(len(pts)-1)
    for p,q in zip(sp.points,pts): p.co=(*q,1)
    sp.use_cyclic_u=cyclic
    ob=bpy.data.objects.new(name,cu); col.objects.link(ob); cu.materials.append(material);return ob

def smoothpoly(points,passes=2):
    a=list(points)
    for _ in range(passes):
        b=[]
        for p,q in zip(a,a[1:]+a[:1]):
            b.extend([tuple(.75*x+.25*y for x,y in zip(p,q)),tuple(.25*x+.75*y for x,y in zip(p,q))])
        a=b
    return a

def samplepoly(points,step=.014):
    out=[]
    for p,q in zip(points,points[1:]+points[:1]):
        n=max(1,int(math.dist(p,q)/step))
        out.extend([tuple(a+(b-a)*i/n for a,b in zip(p,q)) for i in range(n)])
    return out

def interp(x,keys):
    if x<=keys[0][0]: return keys[0][1]
    if x>=keys[-1][0]: return keys[-1][1]
    for i in range(len(keys)-1):
        a,y=keys[i]; b,z=keys[i+1]
        if a<=x<=b:
            d=(z-y)/(b-a)
            prev=(y-keys[i-1][1])/(a-keys[i-1][0]) if i else d
            nex=(keys[i+2][1]-z)/(keys[i+2][0]-b) if i+2<len(keys) else d
            m0=2*prev*d/(prev+d) if prev*d>0 else 0
            m1=2*nex*d/(nex+d) if nex*d>0 else 0
            t=(x-a)/(b-a)
            return (2*t**3-3*t*t+1)*y+(t**3-2*t*t+t)*(b-a)*m0+(-2*t**3+3*t*t)*z+(t**3-t*t)*(b-a)*m1

def W(x): return interp(x,[(-2.4,.865),(-2.15,.93),(-1.5,.98),(-.8,.951),(0,.941),(.8,.977),(1.45,.99),(2.05,.957),(2.4,.874)])
def S(x): return interp(x,[(-2.4,.737),(-2.15,.838),(-1.5,.945),(-.85,.958),(0,.949),(.85,.972),(1.48,.967),(2.05,.950),(2.4,.940)])-.082
def H(x): return interp(x,[(-2.4,0),(-1.02,0),(-.85,.029),(-.60,.16),(-.27,.345),(.02,.432),(.28,.459),(.52,.434),(.80,.367),(1.06,.275),(1.35,.138),(1.68,.025),(1.85,0),(2.4,0)])
def upper(x,v):
    av=abs(v)
    t=max(0,min(1,(av-.70)/.28)); dome=1-(t*t*(3-2*t))
    # One continuous surface carries bonnet, cabin, shoulders and rear deck.
    fender=.027*(math.exp(-((x+1.50)/.57)**2)+math.exp(-((x-1.46)/.59)**2))
    bump=fender*(math.exp(-((av-.82)/.16)**2)-math.exp(-((1-.82)/.16)**2))
    roll=max(0,(av-.92)/.08)*pi/2
    yv=av if av<=.92 else .92+.08*sin(roll)
    crown=.09*cos(roll)+.025*(1-v*v) if av>.92 else .09+.025*(1-v*v)
    z=S(x)+crown+bump+H(x)*dome
    xx=x-endweight(x)*math.copysign(.12*av**4,x)
    return (xx,math.copysign(W(x)*yv,v),z)

def endweight(x):
    t=max(0,min(1,(abs(x)-2.0)/.4));return t*t*(3-2*t)

def split(poly,a,b):
    def side(p): return (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])
    ins=[]; out=[]
    for p,q in zip(poly,poly[1:]+poly[:1]):
        dp,dq=side(p),side(q)
        (ins if dp>=-1e-10 else out).append(p)
        if (dp>1e-10 and dq < -1e-10) or (dp < -1e-10 and dq>1e-10):
            t=dp/(dp-dq); r=(p[0]+(q[0]-p[0])*t,p[1]+(q[1]-p[1])*t)
            ins.append(r);out.append(r)
    return ins,out

def ccw(p):
    return p if sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(p,p[1:]+p[:1]))>0 else list(reversed(p))

def partition(poly,mask):
    inside=poly; outside=[]
    for a,b in zip(mask,mask[1:]+mask[:1]):
        if len(inside)<3: break
        inside,other=split(inside,a,b)
        if len(other)>=3: outside.append(other)
    return outside,inside

def bounds(p): return (min(a[0] for a in p),max(a[0] for a in p),min(a[1] for a in p),max(a[1] for a in p))
def overlap(a,b): return a[0]<b[1] and a[1]>b[0] and a[2]<b[3] and a[3]>b[2]
def surface(name,xs,vs,fn,regions=[]):
    uv=[];faces=[];inds=[]
    masks=[(ccw(p),idx,bounds(p)) for p,idx in regions]
    for a,b in zip(xs,xs[1:]):
        for c,d in zip(vs,vs[1:]):
            parts=[([(a,c),(b,c),(b,d),(a,d)],0)]
            for mask,idx,bb in masks:
                if not overlap((a,b,c,d),bb): continue
                new=[]
                for poly,old in parts:
                    outside,inside=partition(poly,mask)
                    new.extend((p,old) for p in outside)
                    if len(inside)>=3: new.append((inside,idx))
                parts=new
            for poly,idx in parts:
                area=abs(sum(p[0]*q[1]-q[0]*p[1] for p,q in zip(poly,poly[1:]+poly[:1])))
                if area<1e-12: continue
                faces.append(tuple(range(len(uv),len(uv)+len(poly))))
                uv.extend(poly);inds.append(idx)
    # Resolve T-junctions created by exact material-region clipping before mapping
    # the UV plane into the sculpted surface. Shared boundaries retain real edges.
    unique=[];lookup={};remap=[]
    for p in uv:
        key=(round(p[0],9),round(p[1],9))
        if key not in lookup:lookup[key]=len(unique);unique.append(p)
        remap.append(lookup[key])
    faces=[tuple(remap[i] for i in face) for face in faces]
    lines={};edgekeys={}
    for face in faces:
        for i,j in zip(face,face[1:]+face[:1]):
            if i==j:continue
            ek=tuple(sorted((i,j)))
            if ek in edgekeys:continue
            p,q=unique[i],unique[j];dx,dy=q[0]-p[0],q[1]-p[1];length=math.hypot(dx,dy)
            dx/=length;dy/=length
            if dx < -1e-8 or (abs(dx)<1e-8 and dy<0):dx=-dx;dy=-dy
            key=(round(dx,6),round(dy,6),round(-dy*p[0]+dx*p[1],6))
            lines.setdefault(key,set()).update([i,j]);edgekeys[ek]=key
    sortedlines={}
    for key,ids in lines.items():
        dx,dy,_=key
        seq=sorted((unique[i][0]*dx+unique[i][1]*dy,i) for i in ids)
        sortedlines[key]=([a for a,i in seq],seq)
    clean=[]
    for face in faces:
        new=[]
        for i,j in zip(face,face[1:]+face[:1]):
            if i==j:continue
            key=edgekeys[tuple(sorted((i,j)))];dx,dy,_=key
            a=unique[i][0]*dx+unique[i][1]*dy;b=unique[j][0]*dx+unique[j][1]*dy
            vals,seq=sortedlines[key]
            mid=seq[bisect.bisect_right(vals,min(a,b)+1e-8):bisect.bisect_left(vals,max(a,b)-1e-8)]
            if b<a:mid=list(reversed(mid))
            new.append(i);new.extend(k for _,k in mid)
        clean.append(tuple(new))
    return mesh(name,[fn(*p) for p in unique],clean,indices=inds)

regions=[]
def window(poly):
    outer=[]
    for i,p in enumerate(poly):
        prev,nex=poly[i-1],poly[(i+1)%len(poly)]
        a=tuple(.10*q+.90*r for q,r in zip(prev,p));b=tuple(.10*q+.90*r for q,r in zip(nex,p))
        for j in range(5):
            t=j/4;outer.append(tuple((1-t)**2*q+2*(1-t)*t*r+t*t*s for q,r,s in zip(a,p,b)))
    cx=sum(p[0] for p in outer)/len(outer); cy=sum(p[1] for p in outer)/len(outer)
    inner=[(cx+(x-cx)*.965,cy+(v-cy)*.94) for x,v in outer]
    regions.extend([(outer,1),(inner,2)])
    return outer,inner

wind=window([(-.95,-.72),(-.77,-.887),(-.14,-.72),(-.075,-.38),(-.075,.38),(-.14,.72),(-.77,.887),(-.95,.72),(-.98,0)])
rearwin=window([(1.02,-.52),(1.08,-.63),(1.72,-.66),(1.84,-.51),(1.84,.51),(1.72,.66),(1.08,.63),(1.02,.52)])
for s in [-1,1]:
    main=window([(-.76,s*.952),(-.59,s*.885),(-.075,s*.746),(.15,s*.739),(.635,s*.78),(.605,s*.952)])
    quarter=window([(.746,s*.795),(1.20,s*.884),(1.04,s*.947),(.717,s*.951)])

# Lens islands are cut directly into the same surface as the front wings.
lamps=[]
for s in [-1,1]:
    poly=smoothpoly([(-2.57,s*.67),(-2.52,s*.92),(-2.38,s*.969),(-2.04,s*.90),(-2.0,s*.78),(-2.15,s*.72)],2)
    regions.append((poly,1))
    cx=sum(p[0] for p in poly)/len(poly);cy=sum(p[1] for p in poly)/len(poly)
    inner=[(cx+(x-cx)*.89,cy+(v-cy)*.83) for x,v in poly];regions.append((inner,6));lamps.append((s,poly,inner))
    tail=smoothpoly([(2.09,s*.999),(2.23,s*.959),(2.4,s*.957),(2.48,s*1.035),(2.17,s*1.035)],1)
    regions.append((tail,8))

xs=sorted(set([-2.4+4.8*i/240 for i in range(241)]+ [a+d for a in [-1.5,1.46] for d in [-.437,-.434,-.430,-.426,-.424,-.42,-.415,.415,.42,.424,.426,.430,.434,.437]]))
vs=sorted(set([-1+2*i/120 for i in range(121)]+[-.998,-.995,-.99,-.985,.985,.99,.995,.998]))
body=surface('Carrocería · capó, techo, aletas y cristales integrados',xs,vs,upper,regions)
body['construction']='Cristales y marcos forman regiones de la misma superficie; sin láminas flotantes.'

def low(x):
    z=interp(x,[(-2.4,.27),(-2.05,.20),(-1,.18),(1.93,.20),(2.4,.32)])
    for axle in [-1.50,1.46]:
        d=abs(x-axle)
        if d<.437:
            # Circular crown blending into near-vertical jambs below axle height.
            if d<=.415: z=max(z,.363+sqrt(max(0,.425**2-d*d)))
            else: z=max(z,interp(d,[(.415,.45465),(.424,.388),(.430,.285),(.437,.18)]))
    return z
def sidepoint(x,z,s):
    if z>S(x)+1e-8:
        a,b=0.,1.
        for _ in range(24):
            v=(a+b)/2
            if upper(x,v)[2]>z:a=v
            else:b=v
        p=upper(x,s*(a+b)/2)
        return (p[0],p[1],z)
    t=max(0,min(1,(z-.18)/max(.1,S(x)-.18)))
    inset=.046*(1-t)**2+.022*sin(pi*max(0,min(1,t)))**2
    # Subtle concavity in the lower door, soft shoulder above it.
    inset+=.019*math.exp(-((x-.0)/.9)**4)*math.exp(-((z-.44)/.15)**2)*sin(pi*t)**2
    return (x-endweight(x)*math.copysign(.12,x),s*(W(x)-inset),z)

side_xs=sorted(set(xs+ [a+d for a in [-1.5,1.46] for d in [-.437,-.434,-.430,-.426,-.424,-.42,-.415,.415,.42,.424,.426,.430,.434,.437]]))
for s,tag in [(-1,'izquierdo'),(1,'derecho')]:
    side_tail=smoothpoly([(2.10,.994),(2.28,.927),(2.4,.916),(2.44,1.04),(2.16,1.04)],1)
    surface('Costado '+tag,side_xs,[i/65 for i in range(66)],lambda x,t:sidepoint(x,low(x)+(S(x)-low(x))*t,s),[(side_tail,8)])
    # Return lip and dark narrow inner arch surface, with continuous circular profile.
    for axle,label in [(-1.50,'delantero'),(1.46,'trasero')]:
        ax=[axle-.437+.874*i/160 for i in range(161)]
        surface('Labio paso '+label+' '+tag,ax,[0,.3,1],lambda x,t: (x,sidepoint(x,low(x),s)[1]-s*.034*t,low(x)+.006*sin(t*pi)))
        verts=[];fs=[]
        for x in ax:
            p=sidepoint(x,low(x),s)
            verts.extend([(x,p[1]-s*.025,p[2]),(x,p[1]-s*.15,p[2]-.013)])
        for i in range(len(ax)-1):fs.append((2*i,2*i+1,2*i+3,2*i+2))
        mesh('Sombra paso '+label+' '+tag,verts,fs,rubber)

# Rounded nose and tail, with genuine inset grille/duct regions.
def cap(front,u,t):
    x=-2.4 if front else 2.4
    top=upper(x,u)[2]
    bottom=(.18+.09*u*u) if front else (.24+.08*u*u)
    z=(1-t)**3*bottom+3*(1-t)**2*t*(bottom+.15)+3*(1-t)*t*t*(top-.06)+t**3*top
    offset=((1-t)**3*.04-3*(1-t)**2*t*.14-3*(1-t)*t*t*.10)*(1-u*u)
    xx=upper(x,u)[0]+(offset if front else -offset)
    # Same section as adjoining wing at u=+-1.
    edge=abs(sidepoint(x,min(z,S(x)),1)[1]);yv=abs(upper(x,u)[1])/W(x)
    if front:
        for mask,depth in globals().get('front_insets',[]):
            dist=1e9;inside=True
            for a,b in zip(mask,mask[1:]+mask[:1]):
                d=((b[0]-a[0])*(t-a[1])-(b[1]-a[1])*(u-a[0]))/max(1e-9,math.dist(a,b))
                if d<0:inside=False;break
                dist=min(dist,d)
            if inside:
                k=min(1,dist/.025);xx+=depth*k*k*(3-2*k)
    return (xx,math.copysign(edge*yv,u),z)

def capsteps(front):
    x=-2.4 if front else 2.4
    levels=[]
    for i in range(66):
        target=low(x)+(S(x)-low(x))*i/65;a,b=0.,1.
        for _ in range(36):
            t=(a+b)/2
            if cap(front,1,t)[2]<target:a=t
            else:b=t
        levels.append((a+b)/2)
    levels[0]=0.;levels[-1]=1.
    return levels

grille=smoothpoly([(-.73,.53),(-.60,.69),(.60,.69),(.73,.53),(.65,.27),(.43,.21),(-.43,.21),(-.65,.27)],3)
front_regions=[(grille,5)]
ducts=[]
for s in [-1,1]:
    duct=smoothpoly([(s*.77,.43),(s*.94,.49),(s*.955,.13),(s*.67,.13)],2)
    ducts.append(duct);front_regions.append((duct,5))
lowergrille=smoothpoly([(-.59,.13),(.59,.13),(.72,.035),(-.72,.035)],2)
front_regions.append((lowergrille,5))
front_insets=[(ccw(poly),.022) for poly in [grille,*ducts,lowergrille]]
for s,poly,inner in lamps:
    # Continue the lens across the rolled bonnet edge onto the nose.
    for shape,idx in [(poly,1),(inner,6)]:
        front_regions.append(([(v,1+(x+2.4)*1.3) for x,v in shape],idx))
def frontfn(u,t):
    p=cap(True,u,t)
    return p
surface('Paragolpes delantero',vs,capsteps(True),frontfn,front_regions)
curve('Borde cromado parrilla',[Vector(cap(True,*p))+Vector((-.004,0,0)) for p in samplepoly(grille)],.008,chrome,True)
for poly in ducts:
    curve('Contorno toma delantera',[Vector(cap(True,*p))+Vector((-.002,0,0)) for p in samplepoly(poly)],.003,black,True)
for u in [-.55,-.4,-.25,-.1,.1,.25,.4,.55]:
    pts=[]
    for i in range(25):
        t=.255+.403*i/24
        p=Vector(cap(True,u,t));p.x-=.008;pts.append(p)
    curve('Lama vertical parrilla',pts,.004,chrome)

# Trident silhouette visible in the supplied front view.
def badgepoint(y,z):
    p=Vector(cap(True,y/.865,(z-.18)/(.737+.033-.18)));p.x-=.013;return p
for pts in [[(-.054,.465),(-.037,.42),(0,.39),(.037,.42),(.054,.465)],[(0,.49),(0,.385)], [(-.046,.45),(-.061,.481)],[(.046,.45),(.061,.481)],[(0,.40),(0,.335)]]:
    curve('Emblema parrilla',[badgepoint(y,z) for y,z in pts],.005,chrome)

diff=smoothpoly([(-.91,.27),(-.81,.35),(.81,.35),(.91,.27),(.86,.02),(-.86,.02)],2)
tailregions=[(diff,5)]
tail_lamps=[]
for s in [-1,1]:
    p=smoothpoly([(s*.47,.76),(s*1.06,.84),(s*1.06,1.08),(s*.94,1.04),(s*.72,.93),(s*.49,.87)],2)
    tailregions.append((p,1));cx=sum(a for a,b in p)/len(p);cy=sum(b for a,b in p)/len(p)
    inner=[(cx+(a-cx)*.94,cy+(b-cy)*.70) for a,b in p];tailregions.append((inner,8));tail_lamps.append(inner)
surface('Paragolpes y pilotos traseros',vs,capsteps(False),lambda u,t:cap(False,u,t),tailregions)
curve('Contorno difusor',[Vector(cap(False,*p))+Vector((.003,0,0)) for p in samplepoly(diff)],.006,black,True)
for s in [-1,1]:
    ex=smoothpoly([(s*.62,.08),(s*.87,.08),(s*.865,.23),(s*.71,.24)],3)
    # Exterior exhaust surround only, no internal exhaust system.
    curve('Salida escape · cerco',[Vector(cap(False,*p))+Vector((.012,0,0)) for p in samplepoly(ex)],.012,chrome,True)
    for dt in [0,.020]:
        curve('Guía piloto',[Vector(cap(False,s*(.53+.38*i/40),.803+.065*i/40+dt))+Vector((.003,0,0)) for i in range(41)],.0035,red)

# Glass perimeter is already inset by material topology; chrome follows exact boundary.
for poly,idx in regions:
    if idx==2 and max(abs(v) for x,v in poly)>.9:
        curve('Junquillo ventanilla',[Vector(upper(*p))+Vector((0,0,.0015)) for p in samplepoly(poly)],.0028,chrome,True)

# Fine seams define static panels, evaluated on the actual supporting surface.
for s in [-1,1]:
    hood=smoothpoly([(-2.29,s*.49),(-2.10,s*.51),(-1.52,s*.60),(-.96,s*.71),(-.88,s*.59)],2)
    # Open longitudinal hood shut line, not a raised outline around an independent block.
    line=[(-2.28+1.36*i/100,s*(.48+.22*i/100)) for i in range(101)]
    curve('Junta lateral capó',[Vector(upper(*p))+Vector((0,0,.001)) for p in line],.0019,black)
    door=smoothpoly([(-.755,.946),(-.81,.76),(-.78,.32),(-.69,.245),(.46,.245),(.67,.33),(.78,.59),(.64,.962)],2)
    curve('Junta puerta '+str(s),[Vector(sidepoint(x,z,s))+Vector((0,s*.0012,0)) for x,z in samplepoly(door,.008)],.0020,black,True)
    # Side sill crease follows the body rather than a separate rectangular bar.
    pts=[Vector(sidepoint(-1.035+2.02*i/100,.218,s))+Vector((0,s*.002,0)) for i in range(101)]
    curve('Borde talonera',pts,.0045,paint)
    handle=smoothpoly([(.45,.838),(.56,.858),(.69,.846),(.58,.817),(.46,.824)],3)
    curve('Maneta enrasada',[Vector(sidepoint(x,z,s))+Vector((0,s*.004,0)) for x,z in samplepoly(handle,.005)],.005,chrome,True)
    vent=smoothpoly([(-1.037,.732),(-.817,.732),(-.81,.759),(-1.025,.759)],3)
    curve('Branquia lateral',[Vector(sidepoint(x,z,s))+Vector((0,s*.003,0)) for x,z in samplepoly(vent,.005)],.009,black,True)

curve('Junta frontal capó',[Vector(upper(-2.28,v))+Vector((0,0,.001)) for v in [-.48+.96*i/90 for i in range(91)]],.0019,black)
curve('Junta base parabrisas',[Vector(upper(-.965+.14*(abs(v)/.75)**2,v))+Vector((0,0,.001)) for v in [-.75+1.5*i/100 for i in range(101)]],.002,black)
deck=smoothpoly([(2.48,-.79),(2.18,-.74),(1.90,-.68),(1.90,.68),(2.18,.74),(2.48,.79)],2)
curve('Junta tapa maletero superior',[Vector(upper(*p))+Vector((0,0,.001)) for p in samplepoly(deck) if p[0]<=2.4],.002,black,False)
lid=smoothpoly([(-.78,1.12),(-.77,.58),(-.64,.51),(.64,.51),(.77,.58),(.78,1.12)],2)
curve('Junta tapa maletero posterior',[Vector(cap(False,*p))+Vector((.001,0,0)) for p in samplepoly(lid) if p[1]<=1],.002,black,False)

for s,poly,inner in lamps:
    cx=sum(p[0] for p in inner)/len(inner);cy=sum(p[1] for p in inner)/len(inner)
    ledpoly=[(cx+(x-cx)*.79,cy+(v-cy)*.73) for x,v in inner]
    def lightpoint(x,v):
        if x>=-2.4:return Vector(upper(x,v))+Vector((0,0,.002))
        return Vector(cap(True,v,1+(x+2.4)*1.3))+Vector((-.002,0,.001))
    curve('Firma luminosa faro',[lightpoint(*p) for p in samplepoly(ledpoly,.005)],.0045,led,True)
    projector=smoothpoly([(-2.24,s*.77),(-2.24,s*.86),(-2.12,s*.865),(-2.11,s*.795)],2)
    curve('Proyector faro',[lightpoint(*p) for p in samplepoly(projector,.004)],.005,chrome,True)

def uvball(name,loc,scale,material,col=car):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=40,ring_count=20,location=loc)
    ob=bpy.context.object;ob.name=name;ob.scale=scale;ob.data.materials.append(material);move(ob,col)
    for p in ob.data.polygons:p.use_smooth=True
    return ob

for s in [-1,1]:
    curve('Soporte retrovisor',[(-.62,s*.905,1.004),(-.63,s*.976,1.021),(-.66,s*1.020,1.044)],.021,black)
    housing=uvball('Carcasa retrovisor',(-.65,s*1.029,1.065),(.119,.096,.062),paint)
    for vert in housing.data.vertices:vert.co.x=min(vert.co.x,.68)
    uvball('Espejo',(-.567,s*1.029,1.065),(.002,.068,.043),glass)
    curve('Junta espejo',[(-.566,s*1.029+.070*cos(2*pi*i/64),1.065+.045*sin(2*pi*i/64)) for i in range(64)],.002,black,True)
    curve('Intermitente retrovisor',[(-.748,s*(.995+.090*i/30),1.059) for i in range(31)],.003,led)

# Wheels are kept simple but their diameter, width and axle spacing are fixed.
def wheelmesh(axle,s):
    yc=s*.867;zc=.363
    profile=[(-.119,.272),(-.126,.298),(-.119,.33),(-.098,.355),(-.073,.365),(.073,.365),(.098,.355),(.119,.33),(.126,.298),(.119,.272)]
    verts=[];faces=[]
    n=96
    for y,r in profile:
        for i in range(n):
            a=2*pi*i/n;verts.append((axle+r*sin(a),yc+y,zc+r*cos(a)))
    for j in range(len(profile)):
        for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,((j+1)%len(profile))*n+(i+1)%n,((j+1)%len(profile))*n+i))
    mesh('Neumático',verts,faces,rubber,wheels)
    facey=yc+s*.121
    for r,radius,material in [(.275,.011,chrome),(.249,.007,chrome),(.313,.0018,rubber),(.338,.0015,rubber)]:
        curve('Anillo rueda',[(axle+r*sin(2*pi*i/n),facey,zc+r*cos(2*pi*i/n)) for i in range(n)],radius,material,True,wheels)
    bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=.251,depth=.12,location=(axle,yc,zc),rotation=(pi/2,0,0))
    ob=bpy.context.object;ob.name='Fondo llanta';ob.data.materials.append(lens);move(ob,wheels)
    for p in ob.data.polygons:p.use_smooth=len(p.vertices)==4
    for i in range(7):
        a=2*pi*i/7
        p0=Vector((axle+.048*sin(a),facey,zc+.048*cos(a)))
        p1=Vector((axle+.262*sin(a+.08),facey-.008*s,zc+.262*cos(a+.08)))
        mid=(p0+p1)*.5
        bpy.ops.mesh.primitive_cube_add(size=1,location=mid)
        ob=bpy.context.object;ob.name='Radio llanta';ob.dimensions=(.027,.025,(p1-p0).length)
        ob.rotation_euler=(p1-p0).to_track_quat('Z','Y').to_euler()
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        mod=ob.modifiers.new('Cantos suaves','BEVEL');mod.width=.006;mod.segments=3
        ob.modifiers.new('Normales','WEIGHTED_NORMAL');ob.data.materials.append(chrome);move(ob,wheels)
    uvball('Centro llanta',(axle,facey+s*.004,zc),(.052,.013,.052),chrome,wheels)
for a in [-1.50,1.46]:
    for s in [-1,1]:wheelmesh(a,s)

# Continuous lower exterior and wheel-arch returns; no floating undertray.
def underside(x,v):
    p=upper(x,v);yv=abs(p[1])/W(x)
    edge=abs(sidepoint(x,low(x),1)[1]);y=math.copysign(edge*yv,v)
    base=interp(x,[(-2.4,.18),(-2.0,.17),(1.95,.18),(2.4,.24)])
    wheel=1. if min(abs(x+1.5),abs(x-1.46))<.437 else 0.
    t=max(0,min(1,(abs(y)-.70)/.04));step=t*t*(3-2*t)
    z=base+(low(x)-base)*(step if wheel else v*v)
    xx=p[0]-math.copysign(.04,x)*endweight(x)*(1-v*v)
    return (xx,y,z)
under=surface('Cierre exterior inferior',side_xs,vs,underside)
for face in under.data.polygons:face.material_index=5

# Weld the exterior skin at common sampled boundaries. The only deliberate open
# borders are the underside and wheel openings; Solidify supplies a thin return.
shell=[ob for ob in car.objects if ob.type=='MESH' and (ob==body or ob==under or ob.name.startswith('Costado ') or ob.name.startswith('Paragolpes'))]
bpy.ops.object.select_all(action='DESELECT')
for ob in shell:ob.select_set(True)
bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
body.name='Carrocería · superficie exterior continua'
bm=bmesh.new();bm.from_mesh(body.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000012)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
boundary_edges=sum(e.is_boundary for e in bm.edges)
nonmanifold_inner=sum(not e.is_manifold and not e.is_boundary for e in bm.edges)
remaining=set(e for e in bm.edges if e.is_boundary);clusters=[];fillgroups=[]
while remaining:
    seed=remaining.pop();stack=[seed];cluster=[seed]
    while stack:
        edge=stack.pop()
        for v in edge.verts:
            for e in v.link_edges:
                if e in remaining:remaining.remove(e);stack.append(e);cluster.append(e)
    coords=[v.co for e in cluster for v in e.verts]
    clusters.append({'edges':len(cluster),'length':sum(e.calc_length() for e in cluster),'min':[min(v[k] for v in coords) for k in range(3)],'max':[max(v[k] for v in coords) for k in range(3)]})
    fillgroups.append(cluster)
print('BOUNDARY_CLUSTERS',json.dumps(sorted(clusters,key=lambda c:c['edges'],reverse=True)[:15]),flush=True)
for group in fillgroups:
    # Exact mask clipping can leave narrow slivers after coordinate welding.
    # Close and triangulate these local borders with their adjacent material.
    if any(not e.is_valid for e in group):continue
    adjacent=[f for e in group for f in e.link_faces]
    material=max(set(f.material_index for f in adjacent),key=lambda k:sum(f.material_index==k for f in adjacent)) if adjacent else 0
    filled=bmesh.ops.holes_fill(bm,edges=group,sides=0).get('faces',[])
    for f in filled:f.material_index=material;f.smooth=True
    if filled:bmesh.ops.triangulate(bm,faces=filled,quad_method='BEAUTY',ngon_method='BEAUTY')
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
boundary_edges=sum(e.is_boundary for e in bm.edges)
nonmanifold_inner=sum(not e.is_manifold and not e.is_boundary for e in bm.edges)
bm.to_mesh(body.data);bm.free();body.data.update()

report={
    'scope':'Exterior estático; sin interior, motor, mecanismos, animaciones ni scripts incrustados.',
    'reference':'Imagen adjunta en la conversación; proporciones inferidas visualmente, sin cotas técnicas.',
    'wheelbase_m':2.96,'tire_diameter_m':.73,'body_width_m':1.98,
    'skin_vertices':len(body.data.vertices),'skin_faces':len(body.data.polygons),
    'skin_boundary_edges_before_thickness':boundary_edges,
    'skin_nonmanifold_nonboundary_edges':nonmanifold_inner,
    'embedded_text_blocks':len(bpy.data.texts),'animation_actions':len(bpy.data.actions),
}
(OUT/'revision_geometria.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('GEOMETRY_REPORT',json.dumps(report,ensure_ascii=False),flush=True)

# Studio and six named inspection cameras. No animation or embedded scripts.
floor=mat('Estudio · suelo',(.19,.215,.245),.05,.53)
bpy.ops.mesh.primitive_plane_add(size=200);ob=bpy.context.object;ob.name='Suelo estudio';ob.location.z=-.009;ob.data.materials.append(floor);move(ob,studio)
def light(name,loc,power,size,color=(1,1,1),shape='DISK',size_y=None):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape=shape;data.size=size;data.color=color
    if size_y is not None:data.size_y=size_y
    ob=bpy.data.objects.new(name,data);studio.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector((0,0,.6))-ob.location).to_track_quat('-Z','Y').to_euler()
light('Softbox principal',(-3,-4,6),1600,5,shape='RECTANGLE',size_y=3)
light('Franja techo',(1,2,5),1900,5,shape='RECTANGLE',size_y=1.5)
light('Relleno frontal',(-5,2,2.8),850,3)
light('Contraluz zaga',(4,-1,3.4),1100,3)
def camera(name,pos,target,scale,ortho=True):
    data=bpy.data.cameras.new(name);ob=bpy.data.objects.new(name,data);studio.objects.link(ob);ob.location=pos;ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
    data.type='ORTHO' if ortho else 'PERSP';data.ortho_scale=scale;data.lens=58;return ob
cams={
'01_lateral':camera('01 · Lateral',(-.0,-10,.73),(0,0,.73),5.8),
'02_frontal':camera('02 · Frontal',(-10,0,.77),(0,0,.77),3.1),
'03_trasera':camera('03 · Trasera',(10,0,.77),(0,0,.77),3.1),
'04_superior':camera('04 · Superior',(0,0,12),(0,0,0),5.8),
'05_tres_cuartos_delantero':camera('05 · Tres cuartos delantero',(-7.0,-7.0,3.5),(0,0,.65),6.0),
'06_tres_cuartos_trasero':camera('06 · Tres cuartos trasero',(6.7,-7,3.25),(0,0,.65),6.0),
}
scene.camera=cams['05_tres_cuartos_delantero']
scene.render.image_settings.file_format='PNG'
scene['Alcance']='Exterior estático basado en la referencia adjunta. Sin interior, motor, mecanismos, animaciones ni integración Unity.'
scene['Proporciones']='Longitud aprox. 4.99 m; ancho carrocería 1.98 m; altura 1.44 m; batalla 2.96 m. Escala inferida de imagen, no cotas de ingeniería.'
scene['Ejes']='X longitudinal, frontal -X; Z arriba.'
for ob in bpy.context.selected_objects:ob.select_set(False)
body.select_set(True);bpy.context.view_layer.objects.active=body
for screen in bpy.data.screens:
    for ar in screen.areas:
        if ar.type=='VIEW_3D':
            ar.spaces.active.region_3d.view_perspective='CAMERA'
            ar.spaces.active.clip_end=300
            ar.spaces.active.shading.type='MATERIAL'
            ar.spaces.active.overlay.show_overlays=False
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
views=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(cams)
for key in views:
    if key not in cams:continue
    scene.camera=cams[key]
    if key=='04_superior':
        scene.render.resolution_x=1500;scene.render.resolution_y=760
        # Longitudinal axis X stays horizontal in the top inspection image.
        scene.camera.rotation_euler=(0,0,0)
    elif key in ['02_frontal','03_trasera']:
        scene.render.resolution_x=1100;scene.render.resolution_y=800
    else:scene.render.resolution_x=1500;scene.render.resolution_y=900
    scene.render.filepath=str(OUT/(key+'.png'));bpy.ops.render.render(write_still=True)
scene.camera=cams['05_tres_cuartos_delantero'];scene.render.resolution_x=1500;scene.render.resolution_y=1000
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
print('EXTERIOR_BUILD_COMPLETE',flush=True)
