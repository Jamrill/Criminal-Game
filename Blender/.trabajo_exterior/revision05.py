import bpy,bmesh,math,ast,sys,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
from math import sin,cos,pi,sqrt
OUT=Path(r'C:\Users\jrjav\Documents\GitHub\Criminal-Game\Blender\carroceria_referencia')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'historial/refinamiento_v04.blend'))
scene=bpy.context.scene
car=bpy.data.collections['01 · CARROCERÍA EXTERIOR'];detail=bpy.data.collections['04 · ÓPTICAS Y ACABADOS'];studio=bpy.data.collections['03 · ESTUDIO Y VISTAS']
glazing=bpy.data.collections['05 · ACRISTALAMIENTO INDEPENDIENTE']
body=bpy.data.objects['Carrocería · superficie exterior continua']
allm=list(body.data.materials);paint,black,glass,chrome,rubber,recess,lens,led,red=allm[:9]
for filename,names in [('geometria_base.py',{'move','mat','mesh','curve','smoothpoly','samplepoly','interp'}),('refinar.py',{'tube','objmesh','ring','disk','pocket','cut_pockets'})]:
    for node in ast.parse(Path(__file__).with_name(filename).read_text(encoding='utf8')).body:
        if isinstance(node,ast.FunctionDef) and node.name in names:exec(compile(ast.Module(body=[node],type_ignores=[]),filename,'exec'))

# Reunite shared boundaries temporarily, so new surface edits remain continuous.
bm=bmesh.new();bm.from_mesh(body.data)
for ob in list(glazing.objects):
    old=set(bm.faces);bm.from_mesh(ob.data)
    for f in set(bm.faces)-old:f.material_index=2
    bpy.data.objects.remove(ob,do_unlink=True)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data);bm.free()
body.data.normals_split_custom_set([(0,0,0)]*len(body.data.loops))
for ob in list(bpy.data.objects):
    if ob.name.startswith('Piloto ·') or ob.name.startswith('Cristal · junta'):bpy.data.objects.remove(ob,do_unlink=True)

def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
def G(p):
    x,y,z=p;ay=abs(y)
    shoulder=.022*math.exp(-((x-1.48)/.58)**4)*math.exp(-((ay-.83)/.20)**2)*smooth((z-.71)/.20)
    deck=-.021*math.exp(-((x-2.02)/.34)**4)*math.exp(-(ay/.62)**6)*smooth((z-.86)/.12)
    yy=y+math.copysign(.013*math.exp(-((x-1.52)/.49)**4)*math.exp(-((z-.82)/.19)**2)*smooth((ay-.72)/.2),y)
    tail=smooth((x-2.24)/.15)
    xx=x-.037*tail*math.exp(-(ay/.50)**6)*math.exp(-((z-.742)/.12)**4)
    xx+=.012*tail*math.exp(-((z-.438)/.034)**2)*smooth((.90-ay)/.12)
    return Vector((xx,yy,z+shoulder+deck))
for col in [car,detail]:
    for ob in col.objects:
        if ob.type=='MESH':
            inv=ob.matrix_world.inverted()
            for v in ob.data.vertices:v.co=inv@G(ob.matrix_world@v.co)
        elif ob.type=='CURVE':
            inv=ob.matrix_world.inverted()
            for sp in ob.data.splines:
                for p in sp.points:p.co=(*(inv@G(ob.matrix_world@Vector(p.co[:3]))),1)
bm=bmesh.new();bm.from_mesh(body.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data)
tree=BVHTree.FromBMesh(bm)
kd=KDTree(len(body.data.vertices));normals=[]
for i,v in enumerate(body.data.vertices):kd.insert(v.co,i);normals.append(v.normal.copy())
kd.balance()
normal_source=bpy.data.objects.new('TEMP · superficie para normales',body.data.copy());detail.objects.link(normal_source)
normal_source.hide_render=True
def normal(p):
    n=Vector()
    for co,i,d in kd.find_n(p,4):n+=normals[i]/max(1e-8,d*d)
    return n.normalized()

# Rear lamps: a swept top edge, an angular inner return and two separate light bars.
tailglass=bpy.data.materials['Pilotos · lente granate']
tailglass.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.060,.0017,.004,1)
tailed=bpy.data.materials['Pilotos · difusor rojo']
tailed.node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value=.32
reverse=mat('Pilotos · banda de marcha atrás',(.14,.17,.18),.45,.24)
def roundcorners(poly,amount=.10):
    pts=[]
    for i,p in enumerate(poly):
        a=Vector(poly[i-1]).lerp(Vector(p),1-amount);b=Vector(p).lerp(Vector(poly[(i+1)%len(poly)]),amount)
        for j in range(7):
            t=j/6;pts.append(tuple(a*(1-t)**2+Vector(p)*2*(1-t)*t+b*t*t))
    return samplepoly(pts,.008)
cutters=[]
for s in [-1,1]:
    def tp(p,off=0):
        u,z=p;z-=.025
        x=interp(u,[(0,2.035),(.28,2.24),(.53,2.405),(.74,2.47),(1,2.47)])
        y=s*interp(u,[(0,.962),(.28,.951),(.53,.861),(.74,.682),(1,.445)])
        angle=pi/2*(1-smooth((u-.13)/.61));direction=Vector((cos(angle),s*sin(angle),0))
        q=Vector((x,y,z));hit=tree.ray_cast(q+direction*.5,-direction)
        if not hit[0]:hit=tree.find_nearest(q)
        return hit[0]+normal(hit[0])*off
    poly=roundcorners([(0,.946),(.54,.956),(1,.945),(.885,.831),(.62,.825),(.20,.852),(.015,.878)],.09)
    c=(.52,.891)
    inner=[(c[0]+(u-c[0])*.979,c[1]+(z-c[1])*.92) for u,z in poly]
    cutters.append(pocket('TEMP · alojamiento piloto',inner,tp,.018,-.029,5))
    ring('Piloto · junta ajustada',[[tp(p,.001) for p in poly],[tp(p,-.001) for p in inner]],black)
    ring('Piloto · pared óptica',[[tp(p,-.001) for p in inner],[tp(p,-.020) for p in inner]],black)
    disk('Piloto · lente envolvente',inner,lambda p,r:tp(p,-.004+.004*(1-r*r)),tailglass,16)
    upperbar=[(.025+.93*i/140,.925+.009*sin(pi*i/140)) for i in range(141)]
    lowerpath=[(.07,.878),(.27,.863),(.62,.849),(.78,.849),(.85,.860),(.91,.887)]
    lowerbar=[]
    for a,b in zip(lowerpath,lowerpath[1:]):
        for j in range(18):lowerbar.append(tuple(Vector(a).lerp(Vector(b),j/18)))
    for label,path in [('superior',upperbar),('inferior',lowerbar)]:
        tube('Piloto · guía '+label,[tp(p,.003) for p in path],.0037,tailed)
    strip=roundcorners([(.10,.905),(.73,.908),(.70,.895),(.11,.893)],.07)
    disk('Piloto · banda clara',strip,lambda p,r:tp(p,.001),reverse,8)
    # The closure line crosses each cluster where its inner section meets the lid.
    tube('Piloto · división tapa',[tp((.69,.839+.104*i/40),.002) for i in range(41)],.0015,black)
cut_pockets(cutters)
bm.free()

# Restore smooth panel shading and keep the six glazing objects independently editable.
bcheck=bmesh.new();bcheck.from_mesh(body.data)
for edge in bcheck.edges:
    if edge.is_manifold and edge.calc_face_angle()>.48:edge.smooth=False
bcheck.to_mesh(body.data);bcheck.free()
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
wn=body.modifiers.new('Reflejos continuos','WEIGHTED_NORMAL');wn.keep_sharp=True;wn.weight=35
bpy.ops.object.modifier_apply(modifier=wn.name)
transfer=body.modifiers.new('Continuidad alrededor de ópticas','DATA_TRANSFER')
transfer.object=normal_source;transfer.use_loop_data=True;transfer.data_types_loops={'CUSTOM_NORMAL'}
transfer.loop_mapping='POLYINTERP_NEAREST';transfer.use_max_distance=True;transfer.max_distance=.003
bpy.ops.object.modifier_apply(modifier=transfer.name)
bpy.data.objects.remove(normal_source,do_unlink=True)
source_normals={tuple(body.data.vertices[l.vertex_index].co):n.vector.copy() for l,n in zip(body.data.loops,body.data.corner_normals)}
gb=bmesh.new();gb.from_mesh(body.data);remaining={f for f in gb.faces if f.material_index==2};selected=list(remaining);windows=[]
while remaining:
    faces=[];stack=[remaining.pop()]
    while stack:
        f=stack.pop();faces.append(f)
        for e in f.edges:
            for other in e.link_faces:
                if other in remaining:remaining.remove(other);stack.append(other)
    vs=list({v for f in faces for v in f.verts});ids={v:i for i,v in enumerate(vs)}
    ob=mesh('Cristal', [v.co.copy() for v in vs],[tuple(ids[v] for v in f.verts) for f in faces],glass,glazing);windows.append(ob)
    c=sum((v.co for v in ob.data.vertices),Vector())/len(ob.data.vertices)
    label='Parabrisas' if c.x<-.3 else 'Luneta trasera' if c.x>1 else ('Ventanilla puerta' if c.x<.55 else 'Cristal de custodia')+(' izquierda' if c.y<0 else ' derecha')
    ob.name='Cristal · '+label
    ob.data.normals_split_custom_set([source_normals[tuple(ob.data.vertices[l.vertex_index].co)] for l in ob.data.loops])
assert len(windows)==6
bmesh.ops.delete(gb,geom=selected,context='FACES');gb.to_mesh(body.data);gb.free()
body.data.normals_split_custom_set([source_normals[tuple(body.data.vertices[l.vertex_index].co)] for l in body.data.loops])
scene['Revisión']='05 · Volúmenes de la zaga y pilotos reconstruidos con referencias fotográficas.'
scene.cycles.samples=64
cams={name:next(o for o in studio.objects if o.type=='CAMERA' and o.name.startswith(name[:2])) for name in ['01_lateral','02_frontal','03_trasera','04_superior','05_tres_cuartos_delantero','06_tres_cuartos_trasero']}
scene.camera=cams['06_tres_cuartos_trasero'];bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
views=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(cams)
for key in views:
    scene.camera=cams[key];scene.render.resolution_x=1500;scene.render.resolution_y=900
    if key=='04_superior':scene.render.resolution_y=760
    if key in ['02_frontal','03_trasera']:scene.render.resolution_x=1100;scene.render.resolution_y=800
    scene.render.filepath=str(OUT/(key+'.png'));bpy.ops.render.render(write_still=True)
scene.camera=cams['06_tres_cuartos_trasero'];bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
print('REVISION05_SAVED',flush=True)
