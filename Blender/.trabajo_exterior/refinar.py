import bpy,bmesh,math,ast,json,sys
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
from math import sin,cos,sqrt,pi

OUT=Path(r'C:\Users\jrjav\Documents\GitHub\Criminal-Game\Blender\carroceria_referencia')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'historial/base_v01.blend'))
scene=bpy.context.scene
car=bpy.data.collections['01 · CARROCERÍA EXTERIOR']
wheels=bpy.data.collections['02 · RUEDAS']
studio=bpy.data.collections['03 · ESTUDIO Y VISTAS']
detail=bpy.data.collections.new('04 · ÓPTICAS Y ACABADOS');scene.collection.children.link(detail)
body=bpy.data.objects['Carrocería · superficie exterior continua']
allm=list(body.data.materials)
paint,black,glass,chrome,rubber,recess,lens,led,red=allm[:9]
source=Path(__file__).with_name('geometria_base.py').read_text(encoding='utf-8')
names={'move','mat','mesh','curve','smoothpoly','samplepoly','interp','W','S','H','upper','endweight','low','sidepoint','cap','ccw','uvball'}
for node in ast.parse(source).body:
    if isinstance(node,ast.FunctionDef) and node.name in names:
        exec(compile(ast.Module(body=[node],type_ignores=[]),'<geometry helpers>','exec'))

def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
def F(p):
    x,y,z=p;ay=abs(y)
    # Progressive shoulder and lower-door curvature, fading before each seam.
    side=smooth((ay-.70)/.20)
    waist=.010*math.exp(-((z-.805)/.10)**2)*math.exp(-((x-.35)/1.55)**4)
    door=-.024*math.exp(-((z-.46)/.14)**2)*math.exp(-((x-.10)/.80)**4)
    sill=.019*math.exp(-((z-.245)/.055)**2)*math.exp(-((x-.02)/1.03)**8)
    yy=y+math.copysign(side*(waist+door+sill),y)
    hoodweight=smooth((x+2.30)/.18)*smooth((-.94-x)/.16)*smooth((z-.68)/.13)
    ridge_y=.43+.19*max(0,min(1,(x+2.3)/1.4))
    zz=z+.013*hoodweight*math.exp(-((ay-ridge_y)/.07)**2)
    roofweight=smooth((z-1.12)/.27)*smooth((x+.40)/.40)*smooth((1.15-x)/.40)
    zz-=.038*min(1,ay/.74)**2*roofweight
    # A softly raised tail edge, not an added spoiler slab.
    zz+=.010*math.exp(-((x-2.32)/.11)**2)*smooth((z-.88)/.07)*(1-.25*min(1,ay))
    rear_lower=smooth((x-1.95)/.40)*smooth((.50-z)/.22)
    zz-=.055*rear_lower*max(0,1-.8*(ay/.95)**2)
    return Vector((x,yy,zz))

for ob in list(car.objects):
    if ob.type=='MESH':
        for v in ob.data.vertices:
            p=ob.matrix_world@v.co;v.co=ob.matrix_world.inverted()@F(p)
    elif ob.type=='CURVE':
        for sp in ob.data.splines:
            for p in sp.points:
                q=F(ob.matrix_world@Vector(p.co[:3]));p.co=(*(ob.matrix_world.inverted()@q),1)

for f in body.data.polygons:
    c=f.center
    if f.material_index==8 or (f.material_index==1 and c.x>2.0 and c.z>.76):f.material_index=0
    if c.x<-1.93 and c.z>.62 and f.material_index in [1,6]:f.material_index=0
body.data.update()
bm=bmesh.new();bm.from_mesh(body.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data);bm.free()
basebm=bmesh.new();basebm.from_mesh(body.data);base_tree=BVHTree.FromBMesh(basebm)
normal_kd=KDTree(len(body.data.vertices));normal_vectors=[]
for i,v in enumerate(body.data.vertices):normal_kd.insert(v.co,i);normal_vectors.append(v.normal.copy())
normal_kd.balance()

def normal(p):
    n=Vector((0,0,0))
    for co,i,d in normal_kd.find_n(Vector(p),4):n+=normal_vectors[i]/max(.00000001,d*d)
    return n.normalized()
def on(p,offset=0):
    p=F(p);return p+normal(p)*offset
def tube(name,pts,r,material=black,closed=False):return move(curve(name,pts,r,material,closed),detail)
def objmesh(name,verts,faces,material):return mesh(name,verts,faces,material,detail)
def orient(ob,n):
    total=Vector((0,0,0))
    for f in ob.data.polygons:total+=f.normal*f.area
    if total.dot(n)<0:
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
    return ob
def ring(name,rows,material):
    n=len(rows[0]);faces=[]
    for j in range(len(rows)-1):
        for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    return objmesh(name,[v for row in rows for v in row],faces,material)
def disk(name,poly,fn,material,rings=10):
    c=tuple(sum(p[k] for p in poly)/len(poly) for k in range(2));n=len(poly)
    verts=[fn(c,0)];faces=[]
    for j in range(1,rings+1):
        r=j/rings
        verts.extend(fn(tuple(c[k]+r*(p[k]-c[k]) for k in range(2)),r) for p in poly)
    for i in range(n):faces.append((0,1+i,1+(i+1)%n))
    for j in range(rings-1):
        a=1+j*n;b=a+n
        for i in range(n):faces.append((a+i,a+(i+1)%n,b+(i+1)%n,b+i))
    return objmesh(name,verts,faces,material)
def pocket(name,poly,fn,top,bottom,material_index):
    a=disk('TEMP · tapa corte',poly,lambda p,r:fn(p,top),recess,12)
    b=disk('TEMP · fondo corte',poly,lambda p,r:fn(p,bottom),recess,12)
    va=[v.co.copy() for v in a.data.vertices];vb=[v.co.copy() for v in b.data.vertices]
    n=len(va);faces=[tuple(f.vertices) for f in a.data.polygons]
    faces.extend(tuple(n+i for i in reversed(f.vertices)) for f in b.data.polygons)
    edge=n-len(poly)
    for j in range(len(poly)):
        i=edge+j;k=edge+(j+1)%len(poly);faces.append((i,k,n+k,n+i))
    ob=objmesh(name,va+vb,faces,recess)
    ob.data.materials.clear()
    for m in allm:ob.data.materials.append(m)
    for f in ob.data.polygons:f.material_index=material_index
    bpy.data.objects.remove(a,do_unlink=True);bpy.data.objects.remove(b,do_unlink=True)
    return ob
def cut_pockets(cutters):
    for cutter in cutters:
        bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
        before=len(body.data.polygons)
        mod=body.modifiers.new('Alojamiento exterior','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='MANIFOLD';mod.object=cutter
        bpy.ops.object.modifier_apply(modifier=mod.name)
        assert len(body.data.polygons)>before*.9,'Pocket operation invalid'
        bpy.data.objects.remove(cutter,do_unlink=True)
def signed_distance(p,poly):
    inside=False;dist=1e3
    for a,b in zip(poly,poly[1:]+poly[:1]):
        if (a[1]>p[1])!=(b[1]>p[1]) and p[0]<(b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0]:inside=not inside
        dx,dy=b[0]-a[0],b[1]-a[1];t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/(dx*dx+dy*dy)))
        dist=min(dist,math.hypot(p[0]-a[0]-t*dx,p[1]-a[1]-t*dy))
    return dist if inside else -dist
def span(poly,y):
    zs=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        if min(a[0],b[0])<=y<=max(a[0],b[0]) and abs(a[0]-b[0])>1e-9:
            zs.append(a[1]+(b[1]-a[1])*(y-a[0])/(b[0]-a[0]))
    return (min(zs),max(zs)) if zs else None
def roundedbox(name,p,size,material,bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);ob=bpy.context.object;ob.name=name;ob.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    ob.data.materials.append(material);move(ob,detail)
    mod=ob.modifiers.new('Redondeo de cantos','BEVEL');mod.width=bevel;mod.segments=3
    ob.modifiers.new('Normales ponderadas','WEIGHTED_NORMAL');return ob

# Remove the provisional outlines replaced below with optical and trim assemblies.
prefixes=['Lama vertical','Firma luminosa faro','Proyector faro','Guía piloto','Salida escape','Branquia lateral','Maneta enrasada']
for ob in list(car.objects):
    if any(ob.name.startswith(p) for p in prefixes):bpy.data.objects.remove(ob,do_unlink=True)

grille=smoothpoly([(-.73,.53),(-.60,.69),(.60,.69),(.73,.53),(.65,.27),(.43,.21),(-.43,.21),(-.65,.27)],3)
ducts=[smoothpoly([(s*.77,.43),(s*.94,.49),(s*.955,.13),(s*.67,.13)],2) for s in [-1,1]]
lowergrille=smoothpoly([(-.59,.13),(.59,.13),(.72,.035),(-.72,.035)],2)
front_insets=[(ccw(poly),.022) for poly in [grille,*ducts,lowergrille]]
masks=[]
for poly,depth in [(grille,.075),*[(p,.052) for p in ducts],(lowergrille,.035)]:
    yz=[tuple(F(cap(True,*p))[k] for k in (1,2)) for p in poly]
    masks.append((yz,depth))
for v in body.data.vertices:
    x,y,z=v.co
    # Sculpt the rear bumper into the diffuser instead of a single flat face.
    if x>2.27 and .16<z<.58:
        band=math.exp(-((z-.475)/.036)**2)*smooth((.87-abs(y))/.13)
        v.co.x+=.011*band
        if z<.45:v.co.x-=.018*smooth((.45-z)/.06)*smooth((.84-abs(y))/.10)
for ob in car.objects:
    if ob.type=='CURVE' and ob.name.startswith('Contorno difusor'):
        for sp in ob.data.splines:
            for point in sp.points:
                x,y,z=point.co[:3]
                shift=.011*math.exp(-((z-.475)/.036)**2)*smooth((.87-abs(y))/.13)
                if z<.45:shift-=.018*smooth((.45-z)/.06)*smooth((.84-abs(y))/.10)
                point.co.x+=shift

air_pockets=[]
for poly,depth in [(grille,.10),*[(p,.078) for p in ducts],(lowergrille,.053)]:
    center=tuple(sum(p[k] for p in poly)/len(poly) for k in range(2))
    inset=[tuple(center[k]+.96*(p[k]-center[k]) for k in range(2)) for p in poly]
    air_pockets.append(pocket('TEMP · cavidad toma',inset,lambda p,d:F(cap(True,*p))+Vector((-d,0,0)),.028,-depth,5))
cut_pockets(air_pockets)

# Deep grille, chamfered upright blades and fine background grille.
gpoly=masks[0][0]
def gx(y,z,offset=0):
    hit=base_tree.ray_cast(Vector((-5,y,z)),Vector((1,0,0)))
    return hit[0].x+offset if hit[0] else -2.43+offset
for y in [-.49,-.35,-.21,-.075,.075,.21,.35,.49]:
    a,b=span(gpoly,y);a+=.012;b-=.012
    pts=[]
    for i in range(25):
        z=a+(b-a)*i/24;x=gx(y,z,.006)
        pts.extend([(x,y-.006,z),(x+.041,y-.006,z),(x+.041,y+.006,z),(x,y+.006,z)])
    faces=[]
    for j in range(24):
        for k in range(4):faces.append((j*4+k,j*4+(k+1)%4,(j+1)*4+(k+1)%4,(j+1)*4+k))
    faces.extend([(3,2,1,0),(96,97,98,99)])
    ob=objmesh('Parrilla · lama con espesor',pts,faces,chrome)
    bevel=ob.modifiers.new('Canto satinado','BEVEL');bevel.width=.002;bevel.segments=2
for i in range(-22,23):
    y=i*.024;limits=span(gpoly,y)
    if not limits:continue
    a,b=limits
    tube('Malla interior parrilla',[(gx(y,z,.060),y,z) for z in [a+.013+(b-a-.026)*j/12 for j in range(13)]],.0011,black)
for z in [.34+.023*i for i in range(12)]:
    pts=[]
    for i in range(101):
        y=-.56+1.12*i/100
        if signed_distance((y,z),gpoly)>.012:pts.append((gx(y,z,.060),y,z))
    if len(pts)>2:tube('Malla transversal parrilla',pts,.0011,black)
for poly,depth in masks[1:]:
    ya,yb=min(p[0] for p in poly),max(p[0] for p in poly)
    za,zb=min(p[1] for p in poly),max(p[1] for p in poly)
    for z in [za+(zb-za)*j/8 for j in range(1,8)]:
        pts=[]
        for i in range(60):
            y=ya+(yb-ya)*i/59
            if signed_distance((y,z),poly)>.007:pts.append((gx(y,z,depth*.67),y,z))
        if len(pts)>2:tube('Toma de aire · rejilla horizontal',pts,.0021,black)

# Each headlamp has a shaped pocket, black seal, metal bezel, projector and cover.
clear=mat('Ópticas · policarbonato transparente',(.96,.98,1),0,.085)
shader=clear.node_tree.nodes['Principled BSDF'];shader.inputs['Transmission Weight'].default_value=1;shader.inputs['IOR'].default_value=1.48
optical=mat('Ópticas · reflector grafito',(.035,.043,.051),.72,.24)
reflector=mat('Ópticas · reflector aluminizado',(.53,.57,.62),.96,.15)
smoke=mat('Ópticas · fondo negro',(.006,.009,.012),.2,.3)
head_cutters=[]
for s in [-1,1]:
    poly=smoothpoly([(-2.64,s*.67),(-2.58,s*.92),(-2.38,s*.969),(-2.04,s*.90),(-2.0,s*.78),(-2.15,s*.72)],4)
    poly=samplepoly(poly,.008)
    c=tuple(sum(p[k] for p in poly)/len(poly) for k in range(2))
    def hraw(p):
        x,v=p
        q=upper(x,v) if x>=-2.4 else cap(True,v,1+(x+2.4)*1.3)
        return F(q)
    def hbase(p):
        x,v=p;a,b=-2.44,-2.36
        if not a<x<b:return hraw(p)
        eps=.0001;t=(x-a)/(b-a)
        pa,pb=hraw((a,v)),hraw((b,v))
        da=(hraw((a+eps,v))-hraw((a-eps,v)))/(2*eps)
        db=(hraw((b+eps,v))-hraw((b-eps,v)))/(2*eps)
        return pa*(2*t**3-3*t*t+1)+da*((t**3-2*t*t+t)*(b-a))+pb*(-2*t**3+3*t*t)+db*((t**3-t*t)*(b-a))
    def hn(p):
        x,v=p;eps=.0001
        dx=hbase((x+eps,v))-hbase((x-eps,v));dv=hbase((x,v+eps))-hbase((x,v-eps))
        n=dx.cross(dv).normalized()
        return n if n.z>0 else -n
    def hp(p,off=0):return hbase(p)+hn(p)*off
    pocket_poly=[tuple(c[k]+.94*(p[k]-c[k]) for k in range(2)) for p in poly]
    head_cutters.append(pocket('TEMP · cavidad faro',pocket_poly,hp,.025,-.046,6))
    for name,rs,ofs,material in [
        ('Faro · junta perimetral',[1,.97],[.0008,.0013],black),
        ('Faro · cerco mecanizado',[.97,.91],[.0012,-.005],optical),
        ('Faro · pared del alojamiento',[.91,.88],[-.005,-.030],optical)]:
        rows=[]
        for r,off in zip(rs,ofs):rows.append([hp(tuple(c[k]+r*(p[k]-c[k]) for k in range(2)),off) for p in poly])
        orient(ring(name,rows,material),hn(c))
    inner=[tuple(c[k]+.89*(p[k]-c[k]) for k in range(2)) for p in poly]
    orient(disk('Faro · fondo óptico',inner,lambda p,r:hp(p,-.030),smoke,12),hn(c))
    signature=[tuple(c[k]+.78*(p[k]-c[k]) for k in range(2)) for p in poly]
    tube('Faro · guía LED continua',[hp(p,-.007) for p in signature],.0050,led,True)
    # Projector optical axis follows the local panel normal.
    q=hp((-2.13,s*.825),-.018);n=hn((-2.13,s*.825));t=Vector((0,1,0));t=(t-n*t.dot(n)).normalized();b=n.cross(t).normalized()
    tube('Faro · aro proyector',[q+.031*(t*cos(2*pi*i/64)+b*sin(2*pi*i/64)) for i in range(64)],.0035,reflector,True)
    ob=uvball('Faro · lente del proyector',q,(.027,.027,.007),clear,detail)
    ob.rotation_euler=n.to_track_quat('Z','Y').to_euler()
    q2=q-n*.009
    ob=uvball('Faro · copa reflectora',q2,(.029,.029,.011),reflector,detail);ob.rotation_euler=n.to_track_quat('Z','Y').to_euler()
    cover=disk('Faro · cubierta óptica',inner,lambda p,r:hp(p,.0015+.004*(1-r*r)),clear,18)
    orient(cover,hn(c))
    mod=cover.modifiers.new('Espesor lente 1,5 mm','SOLIDIFY');mod.thickness=.0015;mod.offset=-1
cut_pockets(head_cutters)

# Rear lenses wrap around the corner as one continuous, fitted ribbon.
tailglass=mat('Pilotos · lente granate',(.17,.0025,.007),.1,.18)
tailglass.node_tree.nodes['Principled BSDF'].inputs['Coat Weight'].default_value=.7
tailed=mat('Pilotos · difusor rojo',(.48,.005,.011),.1,.24)
tailed.node_tree.nodes['Principled BSDF'].inputs['Emission Color'].default_value=(.32,.002,.004,1)
tailed.node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value=.65
tail_centers=[]
for s in [-1,1]:
    def tp(u,v,off=0):
        x=interp(u,[(0,2.10),(.28,2.29),(.53,2.42),(.74,2.47),(1,2.47)])
        y=s*interp(u,[(0,.953),(.28,.936),(.53,.85),(.74,.67),(1,.435)])
        z=.905+.002*sin(pi*u)
        h=.047*sqrt(max(0,1-(2*u-1)**6))
        z+=v*h
        angle=pi/2*(1-smooth((u-.15)/.60))
        direction=Vector((cos(angle),s*sin(angle),0))
        target=Vector((x,y,z));hit=base_tree.ray_cast(target+direction*.6,-direction)
        if not hit[0]:hit=base_tree.find_nearest(target)
        p=hit[0];n=hit[1].normalized()
        return p+n*off
    samples=100
    for name,width,off,material in [('Piloto · cerco negro',1.0,.0015,black),('Piloto · bisel perimetral',.86,.003,chrome),('Piloto · lente envolvente',.78,.0045,tailglass)]:
        verts=[];faces=[]
        for i in range(samples+1):
            u=.004+.992*i/samples
            for j in range(13):verts.append(tp(u,(-1+2*j/12)*width,off))
        for i in range(samples):
            for j in range(12):a=i*13+j;faces.append((a,a+1,a+14,a+13))
        objmesh(name,verts,faces,material)
    for v in [-.39,.32]:tube('Piloto · guía de luz',[tp(.035+.93*i/140,v,.008) for i in range(141)],.0042,tailed)
    for i in range(12):
        u=.40+.045*i
        tube('Piloto · nervadura difusora',[tp(u,-.52+1.04*j/10,.006) for j in range(11)],.0014,tailed)
    tail_centers.extend(tp(.005+.99*i/120,0) for i in range(121))

# Genuine hollow exhaust trims with rolled metal edges and recessed dark mouths.
exhaust_cutters=[]
for s in [-1,1]:
    outline=[(s*.54,.305),(s*.76,.305),(s*.77,.410),(s*.59,.418)]
    yz=[]
    for i,p in enumerate(outline):
        prev,nex=outline[i-1],outline[(i+1)%len(outline)]
        a=tuple(.13*q+.87*r for q,r in zip(prev,p));b=tuple(.13*q+.87*r for q,r in zip(nex,p))
        for j in range(8):
            t=j/7;yz.append(tuple((1-t)**2*q+2*(1-t)*t*r+t*t*s0 for q,r,s0 in zip(a,p,b)))
    cy=sum(y for y,z in yz)/len(yz);cz=sum(z for y,z in yz)/len(yz)
    def ep(y,z,depth):
        hit=base_tree.ray_cast(Vector((5,y,z)),Vector((-1,0,0)))
        p=hit[0] if hit[0] else Vector((2.45,y,z))
        if z<.45:p.x-=.018*smooth((.45-z)/.06)*smooth((.84-abs(y))/.10)
        return p+Vector((depth,0,0))
    rows=[]
    for scale,depth in [(1.02,.001),(1,.006),(.88,.006),(.88,-.032)]:
        rows.append([ep(cy+(y-cy)*scale,cz+(z-cz)*scale,depth) for y,z in yz])
    ring('Escape · embellecedor hueco',rows,chrome)
    disk('Escape · fondo oscuro',yz,lambda p,r:ep(*p,-.034),smoke,6)
    cutpoly=[(cy+(y-cy)*.90,cz+(z-cz)*.90) for y,z in yz]
    exhaust_cutters.append(pocket('TEMP · cavidad escape',cutpoly,lambda p,d:ep(*p,d),.024,-.058,5))
cut_pockets(exhaust_cutters)

# Flush door handles, side outlet inserts and a sculpted sill extension.
for s in [-1,1]:
    hp=smoothpoly([(.43,.834),(.54,.856),(.68,.846),(.61,.823),(.45,.821)],2)
    verts=[on(sidepoint(x,z,s),.0008) for x,z in hp]
    objmesh('Maneta · alojamiento',verts,[tuple(range(len(verts)))],black)
    small=[(.555+(x-.555)*.85,.838+(z-.838)*.67) for x,z in hp]
    ob=objmesh('Maneta · tirador enrasado',[on(sidepoint(x,z,s),.003) for x,z in small],[tuple(range(len(small)))],chrome)
    mod=ob.modifiers.new('Espesor tirador','SOLIDIFY');mod.thickness=.003
    vent=smoothpoly([(-1.04,.723),(-.80,.724),(-.798,.760),(-1.024,.757)],3)
    objmesh('Aleta · fondo salida de aire',[on(sidepoint(x,z,s),.001) for x,z in vent],[tuple(range(len(vent)))],black)
    tube('Aleta · cerco salida de aire',[on(sidepoint(x,z,s),.002) for x,z in vent],.0025,chrome,True)
    for z in [.735,.748]:tube('Aleta · lama salida de aire',[on(sidepoint(-1.022+.204*i/40,z,s),.003) for i in range(41)],.002,chrome)
    rows=[]
    for z,off in [(.198,.000),(.213,.003),(.240,.001)]:
        rows.append([on(sidepoint(-1.00+1.98*i/100,z,s),off) for i in range(101)])
    faces=[]
    for j in range(2):
        for i in range(100):a=j*101+i;faces.append((a,a+1,a+102,a+101))
    objmesh('Talonera · perfil inferior',sum(rows,[]),faces,paint)

# Bumper shut-lines fitted to the supporting surface, kept fine and static.
for s in [-1,1]:
    for x0,label in [(-2.00,'delantero'),(1.995,'trasero')]:
        pts=[]
        for i in range(61):
            t=i/60
            z=.211+(.647 if x0<0 else .685)*t
            x=x0+(-.085 if x0<0 else .105)*smooth(t)
            pts.append(on(sidepoint(x,z,s),.0005))
        tube('Junta paragolpes '+label,pts,.0014,black)

# Finishing material balance: broad highlights reveal rather than wash out curvature.
# Fine static panel grooves are cut into the surface; their dark gasket sits below it.
cutters=[];gaskets=[];grooves_applied=False
joint_prefixes=['Junta puerta','Junta lateral capó','Junta frontal capó']
for sourceob in list(car.objects):
    if sourceob.type!='CURVE' or not any(sourceob.name.startswith(p) for p in joint_prefixes):continue
    cu=sourceob.data.copy();cu.bevel_depth=.0025;cu.bevel_resolution=2;cu.use_fill_caps=True
    ob=bpy.data.objects.new('TEMP · herramienta junta',cu);detail.objects.link(ob)
    for sp in cu.splines:
        for p in sp.points:
            q=Vector(p.co[:3]);q-=normal(q)*.0014;p.co=(*q,1)
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.convert(target='MESH');cutters.append(bpy.context.object)
    sourceob.data.bevel_depth=.00085
    gaskets.append(sourceob)
    for sp in sourceob.data.splines:
        for p in sp.points:
            q=Vector(p.co[:3]);q-=normal(q)*.0029;p.co=(*q,1)
if cutters:
    bpy.ops.object.select_all(action='DESELECT')
    for ob in cutters:ob.select_set(True)
    bpy.context.view_layer.objects.active=cutters[0];bpy.ops.object.join();cutter=bpy.context.object
    cb=bmesh.new();cb.from_mesh(cutter.data)
    bmesh.ops.remove_doubles(cb,verts=list(cb.verts),dist=.0000001)
    bmesh.ops.dissolve_degenerate(cb,edges=list(cb.edges),dist=.00000001)
    bmesh.ops.recalc_face_normals(cb,faces=list(cb.faces))
    print('CUTTER_TOPOLOGY',len(cb.faces),sum(not e.is_manifold for e in cb.edges),flush=True)
    cb.to_mesh(cutter.data);cb.free()
    cutter.data.materials.clear()
    for material in allm:cutter.data.materials.append(material)
    for f in cutter.data.polygons:f.material_index=1
    bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
    backup=body.data.copy();original_faces=len(backup.polygons)
    probe=body.modifiers.new('Consulta solver','BOOLEAN');available=list(probe.bl_rna.properties['solver'].enum_items.keys());body.modifiers.remove(probe)
    for solver in ['MANIFOLD','FLOAT','FAST']:
        if solver not in available:continue
        body.data=backup.copy()
        mod=body.modifiers.new('Juntas estáticas de paneles','BOOLEAN');mod.operation='DIFFERENCE';mod.solver=solver;mod.object=cutter
        bpy.ops.object.modifier_apply(modifier=mod.name)
        bmcheck=bmesh.new();bmcheck.from_mesh(body.data)
        good=len(body.data.polygons)>original_faces+100 and sum(not e.is_manifold for e in bmcheck.edges)==0
        bmcheck.free()
        print('PANEL_SOLVER',solver,len(body.data.polygons),good,flush=True)
        if good:grooves_applied=True;break
    if not grooves_applied:
        body.data=backup
        for gasket in gaskets:
            gasket.data.bevel_depth=.0015
            for sp in gasket.data.splines:
                for p in sp.points:
                    q=Vector(p.co[:3]);q+=normal(q)*.0023;p.co=(*q,1)
    bpy.data.objects.remove(cutter,do_unlink=True)
    print('PANEL_GROOVES',grooves_applied,flush=True)
    if grooves_applied:
        bm=bmesh.new();bm.from_mesh(body.data)
        for edge in bm.edges:
            if edge.is_manifold and edge.calc_face_angle()<1.15 and edge.calc_face_angle()>.5:edge.smooth=False
        bm.to_mesh(body.data);bm.free()
        wn=body.modifiers.new('Reflejos de paneles','WEIGHTED_NORMAL');wn.keep_sharp=True;wn.weight=35

# Evaluation wheels retain their reference size, with a readable hub and brake disc.
disc_mat=mat('Ruedas · acero del disco',(.17,.19,.21),.82,.38)
for axle in [-1.50,1.46]:
    for s in [-1,1]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=80,radius=.218,depth=.012,location=(axle,s*.948,.363),rotation=(pi/2,0,0))
        ob=bpy.context.object;ob.name='Rueda · disco visible';ob.data.materials.append(disc_mat);move(ob,wheels)
        for f in ob.data.polygons:f.use_smooth=len(f.vertices)==4
        for rad in [.191,.211]:tube('Rueda · pista del disco',[(axle+rad*sin(2*pi*i/96),s*.955,.363+rad*cos(2*pi*i/96)) for i in range(96)],.0008,chrome,True)
        cal=roundedbox('Rueda · pinza', (axle+.158,s*.958,.391),(.062,.026,.139),optical,.012);move(cal,wheels)

# Static wiper blades and the bonnet badge are exterior parts visible in the reference.
for s in [-1,1]:
    pts=[]
    for i in range(61):
        v=s*(.08+.53*i/60);x=-.88+.10*(abs(v)/.61)**2
        pts.append(on(upper(x,v),.003))
    tube('Parabrisas · escobilla en reposo',pts,.0031,black)
    tube('Parabrisas · brazo en reposo',[on(upper(-.91,s*.25),.004),on(upper(-.90,s*.33),.008),pts[36]],.004,black)

badgepos=F(cap(True,0,.84))
ob=uvball('Capó · emblema oval',badgepos+Vector((-.002,0,.001)),(.004,.022,.029),chrome,detail)
ob=uvball('Capó · centro emblema',badgepos+Vector((-.005,0,.001)),(.003,.015,.021),black,detail)

p=paint.node_tree.nodes['Principled BSDF'];p.inputs['Base Color'].default_value=(.39,.43,.47,1);p.inputs['Metallic'].default_value=.62;p.inputs['Roughness'].default_value=.28
paint.diffuse_color=(.39,.43,.47,1)
p=glass.node_tree.nodes['Principled BSDF'];p.inputs['Base Color'].default_value=(.009,.016,.022,1);p.inputs['Metallic'].default_value=.18;p.inputs['Roughness'].default_value=.14
scene.cycles.samples=64;scene.cycles.max_bounces=10;scene.cycles.transmission_bounces=6
scene.view_settings.exposure=-.15
bm=bmesh.new();bm.from_mesh(body.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data);bm.free();body.data.update()
basebm.free()
scene['Revisión']='03 · Curvatura del techo, ópticas y cavidades exteriores refinadas; paneles estáticos.'
for ob in bpy.context.selected_objects:ob.select_set(False)
body.select_set(True);bpy.context.view_layer.objects.active=body
cams={name:next(o for o in studio.objects if o.type=='CAMERA' and o.name.startswith(name[:2])) for name in ['01_lateral','02_frontal','03_trasera','04_superior','05_tres_cuartos_delantero','06_tres_cuartos_trasero']}
scene.camera=cams['05_tres_cuartos_delantero']
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
views=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(cams)
for key in views:
    scene.camera=cams[key]
    scene.render.resolution_x=1500;scene.render.resolution_y=900
    if key=='04_superior':scene.render.resolution_y=760
    if key in ['02_frontal','03_trasera']:scene.render.resolution_x=1100;scene.render.resolution_y=800
    scene.render.filepath=str(OUT/(key+'.png'));bpy.ops.render.render(write_still=True)
scene.camera=cams['05_tres_cuartos_delantero'];scene.render.resolution_x=1500;scene.render.resolution_y=1000
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
print('REFINEMENT_SAVED',flush=True)
