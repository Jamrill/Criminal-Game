"""Extend the existing library, never regenerate it. Blender --background file.blend --python this.py.
Geometry is original. Front of furniture is -Y; Z up; person 2 x 2 x 4.
"""
import bpy, bmesh, math, json, shutil
from pathlib import Path
from mathutils import Vector, Matrix

OUT = Path(__file__).resolve().parent
BACKUP = OUT / 'Casa_Modular_antes_edificio.blend'
if not BACKUP.exists(): shutil.copy2(bpy.data.filepath, BACKUP)
assert not bpy.data.collections.get('EDIFICIO_4_PLANTAS'), 'Already extended; reopen the preserved original to rebuild.'
original = bpy.context.scene
original.name = '01_Biblioteca_original'
original_snapshot = {o.name: {'matrix': [list(r) for r in o.matrix_world], 'vertices': len(o.data.vertices) if o.type=='MESH' else None} for o in original.objects}
scene = bpy.data.scenes.new('02_Edificio_4_plantas')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
def col(name, parent=None):
    c=bpy.data.collections.new(name); (parent or scene.collection).children.link(c); return c
building=col('EDIFICIO_4_PLANTAS')
building['descripcion']='4 plantas / 2 viviendas por planta / altura personaje 4 / paso entre plantas 8.4'
lib=bpy.data.collections.new('MODULOS_NUEVOS_ORIGEN'); lib.use_fake_user=True
def mat(name, rgb, metal=0, alpha=1):
    m=bpy.data.materials.new(name); m.diffuse_color=(*rgb,alpha); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*rgb,1)
    p.inputs['Roughness'].default_value=.55; p.inputs['Metallic'].default_value=metal
    p.inputs['Alpha'].default_value=alpha
    if alpha<1: m.surface_render_method='DITHERED'; p.inputs['Roughness'].default_value=.2
    return m
cream=mat('ED_Marfil',(.79,.75,.64)); oak=mat('ED_Roble_claro',(.53,.32,.16)); paleoak=mat('ED_Roble_miel',(.66,.46,.25))
teal=mat('ED_Tejido_petroleo',(.055,.25,.28)); rust=mat('ED_Tejido_caldera',(.57,.20,.105)); sage=mat('ED_Salvia',(.29,.43,.31))
white=mat('ED_Ceramica',(.87,.89,.84)); charcoal=mat('ED_Grafito',(.035,.045,.055)); metal=mat('ED_Acero',(.39,.46,.49),.7)
linen=mat('ED_Lino',(.76,.70,.54)); blue=mat('ED_Azul_suave',(.24,.43,.53)); glass=mat('ED_Vidrio',(.39,.66,.72),0,.19)
tile=mat('ED_Terrazo',(.48,.55,.52)); grout=mat('ED_Junta',(.25,.29,.28)); screen=mat('ED_Pantalla',(.035,.16,.22)); yellow=mat('ED_Laton',(.74,.46,.11),.45)
assets={}; active=None; parts=[]
def begin(name):
    global active,parts
    active=col(name,lib); parts=[]
def mesh(name,vs,fs,material):
    me=bpy.data.meshes.new(name); me.from_pydata(vs,[],fs); me.materials.append(material); me.update()
    o=bpy.data.objects.new(name,me); active.objects.link(o); parts.append(o); return o
def box(name,p,s,m,bevel=0):
    x,y,z=p; a,b,c=[v/2 for v in s]
    vs=[(x+i*a,y+j*b,z+k*c) for i,j,k in [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]
    o=mesh(name,vs,[(0,2,6,4),(1,5,7,3),(0,4,5,1),(2,3,7,6),(0,1,3,2),(4,6,7,5)],m)
    if bevel:
        bm=bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.bevel(bm,geom=list(bm.edges),offset=min(bevel,min(s)*.22),segments=1,affect='EDGES')
        bm.to_mesh(o.data); bm.free()
    return o
def cyl(name,p,r,h,m,n=12,axis='Z'):
    vs=[]
    for z in (-h/2,h/2):
        for i in range(n):
            v=Vector((r*math.cos(i*2*math.pi/n),r*math.sin(i*2*math.pi/n),z))
            if axis=='X': v=Vector((v.z,v.x,v.y))
            if axis=='Y': v=Vector((v.x,v.z,-v.y))
            vs.append(tuple(v+Vector(p)))
    fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,m)
def beam(name,a,b,w,m):
    o=box(name,(0,0,0),(w,w,(Vector(b)-Vector(a)).length),m)
    tr=Matrix.Translation((Vector(a)+Vector(b))/2) @ (Vector(b)-Vector(a)).to_track_quat('Z','Y').to_matrix().to_4x4()
    o.data.transform(tr); return o
def merge(name, origin=(0,0,0), subset=None):
    global parts
    obs=list(parts if subset is None else subset); vs=[]; fs=[]; mids=[]; mats=[]
    for o in obs:
        offset=len(vs); vs.extend([tuple(v.co-Vector(origin)) for v in o.data.vertices])
        for p in o.data.polygons:
            fs.append(tuple(i+offset for i in p.vertices)); m=o.data.materials[p.material_index]
            if m not in mats: mats.append(m)
            mids.append(mats.index(m))
    me=bpy.data.meshes.new(name); me.from_pydata(vs,[],fs)
    for m in mats: me.materials.append(m)
    for p,i in zip(me.polygons,mids): p.material_index=i
    bm=bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(me); bm.free(); me.update()
    ob=bpy.data.objects.new(name,me); active.objects.link(ob); ob.location=origin
    for o in obs:
        parts.remove(o); old=o.data; bpy.data.objects.remove(o,do_unlink=True)
        if old.users==0: bpy.data.meshes.remove(old)
    return ob
def finish(name, objects=None, note='Origen en suelo; frente -Y; escala personaje de 4 unidades.'):
    objects=objects or [merge(name)]
    for o in objects: o['modulo']=name; o['uso']=note
    active.asset_mark(); active.asset_data.description=note
    assets[name]=active
def legs(w,d,h,m=oak):
    for x in (-w/2,w/2):
        for y in (-d/2,d/2): box('Pata',(x,y,h/2),(.18,.18,h),m,.025)
def basin(name,center,w,d,depth,m=white):
    x,y,z=center
    box(name+' fondo',(x,y,z-depth),(w-.2,d-.2,.12),m,.03)
    for xx in (-w/2+.08,w/2-.08): box(name+' lateral',(x+xx,y,z-depth/2),(.16,d,depth),m,.025)
    for yy in (-d/2+.08,d/2-.08): box(name+' borde',(x,y+yy,z-depth/2),(w-.32,.16,depth),m,.025)
    cyl('Desague',(x,y,z-depth+.065),.09,.02,metal)
def tap(x,y,z):
    cyl('Grifo',(x,y,z+.32),.065,.64,metal)
    beam('Cano',(x,y,z+.63),(x,y-.42,z+.63),.10,metal)
    box('Mando',(x+.12,y,z+.36),(.22,.08,.07),metal,.02)

# Flush modular floors: exact 4 x 4 footprint, origin at lower-left of walking surface.
for name,material in [('Suelo_4x4_Madera',oak),('Suelo_4x4_Terrazo',tile),('Suelo_4x4_Terraza',grout)]:
    begin(name); box('Forjado',(2,2,-.225),(4,4,.35),cream)
    if material==oak:
        for j in range(5):
            for i in range(2): box('Tabla',(1+2*i,.4+j*.8,-.025),(1.985,.785,.05),paleoak if (i+j)%3 else oak)
    else:
        for i in range(2):
            for j in range(2): box('Loseta',(1+2*i,1+2*j,-.025),(1.985,1.985,.05),material)
    finish(name,note='Modulo exacto 4 x 4 x .4. Origen esquina de superficie transitable; encajar cada 4 unidades XY.')
begin('Suelo_2x4_Remate'); box('Remate',(1,2,-.2),(2,4,.4),tile); finish('Suelo_2x4_Remate')
begin('Muro_Terraza_4x4'); box('Murete',(2,0,1.94),(4,.3,3.88),cream); box('Albarda',(2,0,3.94),(4,.44,.12),oak); finish('Muro_Terraza_4x4',note='Longitud 4; altura 4, mitad de la pared original. Origen extremo inferior.')

for name,fab,w,count in [('Sofa_2_Plazas',teal,5.6,2),('Sofa_3_Plazas',rust,7.8,3)]:
    begin(name); legs(w-.6,2,.35,charcoal)
    box('Zocalo',(0,0,.58),(w,2.7,.5),oak,.07)
    box('Respaldo',(0,1.1,1.83),(w,.46,2.1),fab,.10)
    for x in (-w/2+.22,w/2-.22): box('Brazo',(x,-.05,1.42),(.44,2.7,1.35),fab,.10)
    seat=(w-.95)/count
    for i in range(count):
        x=-(w-.95)/2+seat*(i+.5)
        box('Cojin asiento',(x,-.20,1.08),(seat-.075,2.12,.48),fab,.12)
        box('Cojin respaldo',(x,.79,2.03),(seat-.09,.48,1.45),fab,.11)
    box('Cojin decorativo',(-w/2+1,.36,1.8),(.8,.32,.78),linen,.1)
    finish(name)

for name,w in [('Cama_Doble',4.8),('Cama_Individual',2.6)]:
    begin(name); legs(w-.4,4.8,.42)
    box('Bastidor',(0,0,.64),(w,5.4,.45),oak,.05)
    for i in range(9): box('Lama',(0,-2.35+i*.58,.90),(w-.2,.35,.08),paleoak)
    box('Colchon',(0,0,1.13),(w-.16,5.18,.43),white,.10)
    box('Edredon',(0,-.7,1.38),(w-.10,3.72,.18),teal if w>3 else rust,.08)
    box('Manta',(0,-1.68,1.49),(w,.80,.09),linen,.02)
    box('Cabecero madera',(0,2.72,1.48),(w+.18,.22,2.65),oak,.055)
    for x in ([-w/4,w/4] if w>3 else [0]):
        box('Panel acolchado',(x,2.55,1.98),(w/(2 if w>3 else 1)-.2,.18,1.35),sage,.065)
        box('Almohada',(x,1.87,1.46),(1.75,1,.30),linen,.10)
    finish(name)
begin('Mesita_Noche'); legs(1.1,1.0,.32)
box('Cuerpo',(0,0,.79),(1.45,1.35,1.0),oak,.04)
box('Tapa',(0,0,1.35),(1.55,1.45,.12),paleoak,.03)
box('Frente cajon',(0,-.70,1.05),(1.27,.09,.37),cream,.02)
box('Tirador',(0,-.79,1.05),(.35,.10,.07),yellow,.02)
box('Hueco oscuro',(0,-.685,.60),(1.13,.02,.32),charcoal)
cyl('Base lampara',(0,0,1.45),.28,.09,metal); cyl('Pie lampara',(0,0,1.76),.045,.58,metal)
cyl('Pantalla lampara',(0,0,2.13),.37,.46,linen,8); finish('Mesita_Noche')

begin('Armario_Dos_Puertas')
for x in (-2.16,2.16): box('Costado',(x,0,2.4),(.18,2.25,4.8),oak,.02)
for z in (.12,4.72): box('Base',(0,0,z),(4.5,2.25,.16),oak,.02)
box('Trasera',(0,1.04,2.4),(4.15,.12,4.5),cream)
box('Division',(.15,0,2.4),(.13,2.05,4.5),oak)
for z in (.85,1.8,2.75,3.7): box('Balda',(1.12,0,z),(1.85,1.95,.10),paleoak)
beam('Barra colgar',(-1.98,0,3.88),(.03,0,3.88),.075,metal)
body=merge('Armario_Carcasa_Interior')
doors=[]
for sign in (-1,1):
    x=sign*1.10
    box('Hoja',(x,-1.17,2.4),(2.16,.15,4.6),sage if sign<0 else cream,.035)
    box('Asa',(sign*.24,-1.29,2.35),(.075,.14,.66),yellow,.02)
    o=merge('Armario_Puerta_'+('Izquierda' if sign<0 else 'Derecha'),(sign*2.2,-1.17,0)); o['eje_animacion']='Z local; bisagra lateral'; doors.append(o)
finish('Armario_Dos_Puertas',[body]+doors)

begin('Escritorio_Ordenador'); legs(4,1.7,1.78,metal)
box('Tablero',(0,0,1.92),(4.6,2.15,.24),paleoak,.05)
box('Cajonera',(1.5,.05,1.06),(1.1,1.85,1.44),cream,.03)
for z in (.62,1.05,1.49):
    box('Frente',(1.5,-.91,z),(.98,.08,.38),sage,.02); box('Asa',(1.5,-.98,z),(.35,.08,.06),metal)
box('Base monitor',(-.4,.55,2.09),(.95,.62,.10),charcoal,.02)
box('Pie monitor',(-.4,.62,2.46),(.12,.12,.7),metal)
box('Monitor',(-.4,.57,3.05),(2.28,.16,1.38),charcoal,.045)
box('Pantalla',(-.4,.478,3.05),(2.08,.025,1.18),screen)
for i,w in enumerate((.82,1.22,.58)):
    box('Interfaz',(-.62,.46,3.38-i*.26),(w,.009,.07),blue)
box('Teclado',(-.5,-.53,2.095),(1.65,.61,.12),charcoal,.03)
for row in range(3):
    for j in range(9): box('Tecla',(-1.19+j*.17,-.72+row*.16,2.165),(.12,.10,.025),metal,.005)
box('Raton',(.72,-.5,2.14),(.25,.42,.16),charcoal,.04)
box('Torre',(-1.6,.45,.75),(.7,1.35,1.5),charcoal,.045)
for z in (.4,.65,.9): box('Ventilacion',(-1.6,-.231,z),(.42,.016,.035),metal)
cyl('Boton PC',(-1.6,-.25,1.22),.055,.025,blue,axis='Y'); finish('Escritorio_Ordenador')

begin('Silla_Escritorio')
cyl('Columna',(0,0,.7),.13,1.05,metal)
for i in range(5):
    a=i*2*math.pi/5; x,y=1.06*math.cos(a),1.06*math.sin(a)
    beam('Radio',(0,0,.36),(x,y,.22),.12,charcoal); cyl('Rueda',(x,y,.15),.15,.13,charcoal,10,axis='X')
box('Asiento',(0,0,1.31),(2.2,1.95,.36),teal,.12)
beam('Soporte respaldo',(0,.72,1.35),(0,1,2.65),.14,metal)
box('Respaldo',(0,.93,2.4),(2,.25,1.65),teal,.10)
for x in (-1.12,1.12):
    box('Soporte brazo',(x,.1,1.59),(.10,.1,.61),metal)
    box('Reposabrazos',(x,-.08,1.93),(.25,1.0,.12),charcoal,.03)
finish('Silla_Escritorio')

for name,kind in [('Cocina_Modulo_Cajones','drawers'),('Cocina_Modulo_Fregadero','sink'),('Cocina_Modulo_Horno','oven')]:
    begin(name); box('Zocalo',(0,.04,.16),(3.0,1.95,.32),charcoal)
    box('Cuerpo',(0,0,1.10),(3.18,2.24,1.8),sage,.025)
    if kind!='sink': box('Encimera',(0,0,2.09),(3.3,2.4,.18),paleoak,.025)
    else:
        for x in (-1.33,1.33): box('Encimera lateral',(x,0,2.09),(.64,2.4,.18),paleoak)
        for y in (-.94,.94): box('Encimera borde',(0,y,2.09),(2.04,.52,.18),paleoak)
        basin('Fregadero',(0,0,2.16),2.02,1.4,.48,metal); tap(.60,.89,2.17)
    for z in (.57,1.13,1.70):
        box('Frente',(0,-1.155,z),(3.03,.08,.48),cream if kind=='drawers' else sage,.02)
        box('Tirador',(0,-1.23,z+.1),(.9,.10,.06),yellow,.015)
    if kind=='oven':
        box('Horno marco',(0,-1.21,1.05),(2.50,.12,1.50),metal,.03)
        box('Horno cristal',(0,-1.286,1.02),(2.15,.04,1.02),charcoal,.04)
        box('Horno asa',(0,-1.40,1.55),(1.60,.14,.08),metal,.02)
        for x in (-.7,.7): cyl('Mando',(x,-1.30,1.70),.10,.08,charcoal,axis='Y')
        box('Vitro',(0,0,2.20),(2.70,1.86,.055),charcoal,.025)
        for x in (-.66,.66):
            for y in (-.47,.47): cyl('Fuego',(x,y,2.235),.32,.018,metal,16)
    finish(name)
begin('Cocina_Armario_Alto')
box('Caja',(0,0,.8),(3.2,1.15,1.6),oak,.025)
for x in (-.8,.8):
    box('Puerta',(x,-.61,.8),(1.53,.10,1.5),cream,.02); box('Tirador',(x,-.69,.29),(.6,.08,.055),yellow)
finish('Cocina_Armario_Alto')
begin('Campana_Extractora'); box('Visera',(0,0,0),(3.2,1.8,.20),metal,.035)
box('Motor',(0,.4,.35),(2.2,.95,.6),charcoal,.035); box('Conducto',(0,.52,1.1),(.95,.70,1.1),metal,.025); finish('Campana_Extractora')

begin('Frigorifico_Interior')
for x in (-1.32,1.32): box('Lateral',(x,0,2.38),(.16,2.52,4.76),cream,.025)
box('Trasera',(0,1.17,2.38),(2.48,.18,4.76),cream,.02)
for z in (.12,1.45,4.68): box('Separador',(0,0,z),(2.48,2.42,.16),white,.02)
box('Junta marco',(0,-1.15,1.45),(2.5,.12,.15),charcoal)
body=merge('Frigorifico_Carcasa_Hueca')
for z in (2.1,2.9,3.7):
    box('Balda',(0,.20,z),(2.35,1.65,.07),glass,.01)
    box('Canto balda',(0,-.625,z),(2.35,.09,.10),metal,.01)
for z in (.34,.89,1.65):
    box('Cajon base',(0,.18,z),(2.27,1.50,.065),white)
    for x in (-1.10,1.10): box('Cajon costado',(x,.18,z+.20),(.07,1.50,.4),white)
    for y in (-.55,.91): box('Cajon frente',(0,y,z+.20),(2.2,.07,.4),glass)
for x in (-.65,.1,.70):
    cyl('Botella',(x,.38,3.98),.14,.46,blue if x<0 else sage,8)
    cyl('Tapon',(x,.38,4.24),.07,.06,white,8)
interior=merge('Frigorifico_Baldas_Cajones')
doors=[]
for name,zc,h in [('Congelador',.73,1.37),('Nevera',3.10,3.23)]:
    box('Junta',(0,-1.26,zc),(2.59,.09,h),charcoal,.02)
    box('Puerta',(0,-1.39,zc),(2.73,.21,h-.04),cream,.07)
    box('Revestimiento interior',(0,-1.18,zc),(2.40,.07,h-.16),white,.035)
    box('Asa',(.92,-1.61,zc),(.10,.18,min(.88,h*.55)),yellow,.025)
    if name=='Nevera':
        for z in (1.90,2.8,3.70):
            box('Estante puerta base',(0,-1.04,z),(2.12,.42,.08),white)
            box('Estante puerta frente',(0,-.85,z+.18),(2.12,.06,.36),glass)
            for x in (-1.04,1.04): box('Estante lateral',(x,-1.04,z+.18),(.065,.40,.36),white)
    o=merge('Frigorifico_Puerta_'+name+'_PIVOTE',(-1.39,-1.39,0)); o['eje_animacion']='Z local; abrir de 0 a -110 grados'; doors.append(o)
finish('Frigorifico_Interior',[body,interior]+doors,note='Carcasa hueca, baldas y cajones. Dos hojas independientes con estantes unidos a la puerta superior. Girar Z local desde bisagra izquierda. Sin animaciones.')

begin('Mesa_Comedor'); legs(3.6,2.3,1.8); box('Tablero',(0,0,1.93),(4.2,2.85,.26),paleoak,.06)
cyl('Cuenco',(0,0,2.16),.38,.20,sage,10); finish('Mesa_Comedor')

begin('Lavabo_Mueble')
legs(2.35,1.55,.36); box('Mueble',(0,0,1.08),(2.75,1.92,1.45),sage,.03)
for z in (.76,1.4):
    box('Cajon',(0,-1,z),(2.55,.09,.55),cream,.025); box('Tirador',(0,-1.09,z+.12),(.65,.09,.055),yellow)
basin('Lavabo',(0,0,2.17),2.85,2.02,.42); tap(.7,.81,2.18)
box('Marco espejo',(0,1.01,3.46),(2.85,.12,1.8),oak,.05)
box('Espejo',(0,.936,3.46),(2.59,.025,1.55),metal,.02); finish('Lavabo_Mueble')
begin('Vater')
box('Pedestal',(0,.08,.55),(.90,1.32,1.1),white,.14)
# Open low-poly oval bowl and seat, including inner wall and bottom, no solid lid hiding the cavity.
n=16; vs=[]
for rx,ry,z in [(.72,1.0,1.20),(.83,1.11,1.39),(.57,.81,1.39),(.34,.49,.80)]:
    vs.extend([(rx*math.cos(i*2*math.pi/n),-.25+ry*math.sin(i*2*math.pi/n),z) for i in range(n)])
fs=[]
for ring in range(3):
    for i in range(n): fs.append((ring*n+i,ring*n+(i+1)%n,(ring+1)*n+(i+1)%n,(ring+1)*n+i))
fs.append(tuple(range(3*n,4*n))); mesh('Taza hueca',vs,fs,white)
box('Cisterna',(0,1.01,1.83),(1.42,.60,1.61),white,.10)
box('Tapa cisterna',(0,1.01,2.66),(1.5,.66,.12),white,.04)
cyl('Pulsador',(0,1.01,2.735),.12,.035,metal)
box('Tapa levantada',(0,.83,2.0),(1.36,.14,1.20),white,.13); finish('Vater')
begin('Ducha')
box('Plato',(0,0,.10),(3.5,3.5,.20),white,.05)
cyl('Sumidero',(.95,.95,.213),.13,.02,metal)
for x in (-1.7,1.7): box('Perfil',(x,1.68,2.45),(.10,.10,4.7),metal)
box('Cristal trasero',(0,1.68,2.45),(3.3,.06,4.6),glass)
box('Cristal lateral',(-1.68,0,2.45),(.06,3.3,4.6),glass)
box('Perfil lateral',(-1.68,-1.67,2.45),(.10,.10,4.7),metal)
beam('Barra ducha',(.70,1.53,1.6),(.70,1.53,4.48),.08,metal)
beam('Brazo rociador',(.70,1.53,4.48),(.70,.70,4.48),.08,metal)
cyl('Rociador',(.70,.70,4.43),.32,.09,metal)
box('Mezclador',(.70,1.48,2.05),(.62,.14,.13),metal,.035)
finish('Ducha',note='Plato 3.5 x 3.5; altura 4.8. Entrada frontal y lateral abiertas, vidrio separado por material.')

begin('Buzon_Individual')
box('Caja',(0,0,.46),(1.18,.62,.92),charcoal,.035)
box('Puerta',(0,-.34,.46),(1.07,.08,.80),sage,.025)
box('Ranura',(0,-.385,.68),(.80,.012,.075),charcoal)
box('Etiqueta',(-.16,-.39,.38),(.40,.015,.16),linen)
cyl('Cerradura',(.34,-.40,.29),.055,.025,metal,axis='Y'); finish('Buzon_Individual')

begin('Corredera_Balcon_8x8')
for x in (.10,7.9): box('Jamba',(x,0,2.5),(.20,.42,5),oak,.02)
box('Dintel',(4,0,6.5),(8,.3,3),cream)
for z in (.065,4.94): box('Carril',(4,0,z),(7.8,.48,.13),metal)
frame=merge('Corredera_Marco_Dintel'); leaves=[]
for name,x,y in [('Fija',2.04,.09),('Movil',5.96,-.10)]:
    for xx in (x-1.87,x+1.87): box('Montante',(xx,y,2.5),(.10,.12,4.75),metal)
    for z in (.19,4.81): box('Travesano',(x,y,z),(3.84,.12,.12),metal)
    box('Vidrio',(x,y,2.5),(3.64,.045,4.5),glass)
    box('Tirador',(x+1.64,y-.12,2.45),(.07,.12,.58),charcoal,.02)
    o=merge('Corredera_Hoja_'+name,(x,y,0)); o['eje_animacion']='Hoja Movil: trasladar X local de 0 a -3.8; hueco libre 3.6'; leaves.append(o)
finish('Corredera_Balcon_8x8',[frame]+leaves,note='Modulo de fachada 8 x 8. Hueco 7.6 x 4.8. Hoja movil corre 3.8 unidades hacia -X. Vidrio incluido en cada hoja.')

# Reuse source meshes, removing only their gallery offset (retain local hinge offsets).
source_offsets={
 'Wall_Straight_4x8':(0,10,0),'Wall_Window_4x8':(5.5,10,0),'Window_Insert':(5.5,10,0),
 'Wall_Door_4x8':(26,10,0),'Door_Frame':(26,10,0),'Door_Leaf_Panelled':(32,10,0),
 'Door_Leaf_Plain':(26.74,10,0),'Stairs_U_Floor':(43,0,0),'Stairs_Start_Landing':(43,0,0),
 'Lift_Shaft_Floor':(43,2.8,0),'Lift_Landing_Doors':(43,2.8,0),'Lift_Cabin':(43,2.8,0),
 'Lift_Roof_Cap':(43,2.8,25.2),'Coffee_Table':(0,-5,0),'TV_Modern_Wall_Large':(10,0,3.1),
 'Chair_02_Comfort':(8,-5,0),'Tree_Stylized':(16,0,0)}
reused={}
def place(name,target,p=(0,0,0),angle=0,prefix=''):
    src=assets.get(name) or bpy.data.collections[name]; offset=Vector(source_offsets.get(name,(0,0,0)))
    R=Matrix.Rotation(angle,4,'Z'); clones=[]
    root=bpy.data.objects.new(prefix+name,None); root.empty_display_size=.35; target.objects.link(root); root.location=p; root.rotation_euler.z=angle
    root['recurso']=name
    for o in src.objects:
        n=o.copy(); n.data=o.data; target.objects.link(n); n.name=prefix+o.name
        n.parent=root; n.matrix_parent_inverse=Matrix.Identity(4); n.location=o.location-offset
        n.hide_render=False; n.hide_viewport=False; clones.append(n)
    if name not in assets: reused[name]=reused.get(name,0)+1
    return root,clones
def text_obj(name,body,p,size,target,rot=(math.pi/2,0,0),material=charcoal):
    cu=bpy.data.curves.new(name,'FONT'); cu.body=body; cu.size=size; cu.align_x='CENTER'; cu.extrude=.003
    o=bpy.data.objects.new(name,cu); target.objects.link(o); o.location=p; o.rotation_euler=rot; cu.materials.append(material); return o
def direct_box(name,p,s,m,target):
    global active,parts
    old=active; active=target; o=box(name,p,s,m); parts.remove(o); active=old; return o
def wall(target,x,y,z,angle=0,kind='solid',prefix=''):
    key={'solid':'Wall_Straight_4x8','window':'Wall_Window_4x8','door':'Wall_Door_4x8'}[kind]
    place(key,target,(x,y,z),angle,prefix)
    if kind=='window': place('Window_Insert',target,(x,y,z),angle,prefix)
    if kind=='door':
        place('Door_Frame',target,(x,y,z),angle,prefix)
        d=Vector((.74,0,0)); d.rotate(Matrix.Rotation(angle,3,'Z'))
        r,obs=place('Door_Leaf_Panelled',target,Vector((x,y,z))+d,angle,prefix)
        r['eje_animacion']='Girar Z local; origen de bisagra'; r['cerrada_grados']=math.degrees(angle)

floors=[]; front_cols=[]; partition_cols=[]; door_cols=[]; floor_furniture=[]
for level in range(4):
    z=level*8.4; f=col('Planta_%02d'%(level+1),building); floors.append(f); f['cota_suelo']=z
    floor=col('P%d_Forjados_modulares'%(level+1),f)
    front=col('P%d_Fachada_frontal_ocultable'%(level+1),f); front_cols.append(front)
    outer=col('P%d_Fachadas_laterales_y_trasera'%(level+1),f)
    common=col('P%d_Zonas_comunes'%(level+1),f)
    for x in range(-8,8,4):
        for y in (0,4): place('Suelo_4x4_Terrazo',floor,(x,y,z))
    for x in (-8,8):
        for y in range(8,28,4): wall(outer,x,y,z,math.pi/2,'window' if y in (12,20) else 'solid')
    for x in range(-8,8,4): wall(outer,x,28,z,kind='window' if x in (-8,4) else 'solid')
    if level==0:
        for x in (-8,4): wall(front,x,0,z,kind='window')
        place('Corredera_Balcon_8x8',front,(-4,0,z),prefix='Portal_')
        place('Stairs_Start_Landing',common,(0,8,z))
    else:
        for x in range(-8,8,4): wall(front,x,0,z,kind='window')
    if level<3: place('Stairs_U_Floor',common,(0,8,z))
    place('Lift_Shaft_Floor',common,(0,10.8,z)); place('Lift_Landing_Doors',common,(0,10.8,z))
    if level==0: place('Lift_Cabin',common,(0,10.8,z))
    # Close the unused side/rear ground void; upper floors leave the stairwell open.
    if level==0:
        # A pit below the cabin and no coplanar duplicate over the starting landing.
        for x in (-6.25,6.25): direct_box('Suelo_base_lateral',(x,18,z-.2),(3.5,20,.4),tile,common)
        for x in (-3.2,3.2): direct_box('Suelo_base_bajo_tramo',(x,16.1,z-.2),(2.6,10.6,.4),tile,common)
        direct_box('Suelo_base_detras_hueco',(0,18,z-.2),(3.8,6.8,.4),tile,common)
        direct_box('Suelo_base_fondo',(0,24.7,z-.2),(9,6.6,.4),tile,common)
    else:
        for x in (-6.25,6.25):
            direct_box('Proteccion_hueco_escalera',(x,8,z+1.05),(3.5,.16,2.1),metal,common)
    text_obj('Rotulo_Planta_%d'%level,'%02d'%(level+1),(-2.8,10.54,z+5.8),.8,common,material=teal)
    for side,ax in [('A',-32),('B',8)]:
        code='%02d%s'%(level+1,side); apt=col('Vivienda_'+code,f)
        apt['programa']='Salon / cocina / dormitorio doble / dormitorio individual con escritorio / bano / terraza'
        walls=col(code+'_Tabiques_ocultables',apt); partition_cols.append(walls)
        furnishings=col(code+'_Mobiliario',apt); floor_furniture.append(furnishings)
        balcony=col(code+'_Balcon',apt)
        for xx in range(0,24,4):
            for yy in range(0,28,4):
                kind='Suelo_4x4_Terrazo' if (xx>=16 and yy<20) else 'Suelo_4x4_Madera'
                place(kind,floor,(ax+xx,yy,z),prefix=code+'_')
        # Perimeter sides, with entry from central lobby.
        for xx in (0,24):
            for yy in range(0,28,4):
                inner=(side=='A' and xx==24) or (side=='B' and xx==0)
                # Core-side walls beyond y=8 already belong to shared core perimeter.
                if inner and yy>=8: continue
                kind='door' if inner and yy==4 else ('window' if not inner and yy in (4,12,24) else 'solid')
                wall(walls if inner else outer,ax+xx,yy,z,math.pi/2,kind,code+'_')
        for xx in range(0,24,4): wall(outer,ax+xx,28,z,kind='window' if xx in (0,8,16,20) else 'solid')
        for xx in (0,12,16,20): wall(front,ax+xx,0,z,kind='window' if xx in (0,16) else 'solid')
        place('Corredera_Balcon_8x8',front,(ax+4,0,z),prefix=code+'_')
        for xx in (4,8):
            place('Suelo_4x4_Terraza',balcony,(ax+xx,-4,z)); place('Muro_Terraza_4x4',balcony,(ax+xx,-4,z))
        for xx in (4,12): place('Muro_Terraza_4x4',balcony,(ax+xx,-4,z),math.pi/2)
        # Bedrooms and bathroom have real door openings; corridor is not capped by a partition.
        for xx in range(0,24,4): wall(walls,ax+xx,20,z,kind='door' if xx in (4,12) else 'solid',prefix=code+'_')
        for yy in (20,24): wall(walls,ax+12,yy,z,math.pi/2,prefix=code+'_')
        for xx in (16,20): wall(walls,ax+xx,12,z,prefix=code+'_')
        for yy in (12,16): wall(walls,ax+16,yy,z,math.pi/2,'door' if yy==12 else 'solid',code+'_')
        def put(name,x,y,zz=0,angle=0): return place(name,furnishings,(ax+x,y,z+zz),angle,code+'_')
        put('Sofa_3_Plazas' if side=='A' else 'Sofa_2_Plazas',8,10,angle=-math.pi/2)
        put('Coffee_Table',3.8,10)
        put('TV_Modern_Wall_Large',.55,10,3.2,math.pi/2)
        direct_box(code+'_Alfombra',(ax+5.2,10,z+.022),(9.0,8.6,.035),linen,furnishings)
        put('Cama_Doble',4.65,25); put('Mesita_Noche',1.2,26); put('Mesita_Noche',8.0,26)
        put('Armario_Dos_Puertas',10.65,25,angle=-math.pi/2)
        put('Cama_Individual',20.5,25); put('Mesita_Noche',22.7,26)
        put('Escritorio_Ordenador',14.8,26.5); put('Silla_Escritorio',14.8,23.9,angle=math.pi)
        put('Cocina_Modulo_Fregadero',15.7,10.6)
        put('Cocina_Modulo_Horno',19,10.6)
        put('Cocina_Modulo_Cajones',22.3,10.6)
        put('Cocina_Armario_Alto',22.3,11.25,3.45)
        put('Campana_Extractora',19,11.0,4)
        # No appliances behind either apartment entry or in the balcony opening.
        put('Frigorifico_Interior',21.9,1.6,angle=math.pi)
        put('Mesa_Comedor',15.9,4.2,angle=math.pi/2)
        put('Chair_02_Comfort',13.0,4.2,angle=math.pi/2)
        put('Chair_02_Comfort',18.8,4.2,angle=-math.pi/2)
        put('Ducha',21.95,17.95)
        put('Vater',18,18.4)
        put('Lavabo_Mueble',21.0,13.15,angle=math.pi)
        # Front-facing numbering kept above the sliding opening.
        text_obj(code+'_Numero',code,(ax+8,-.18,z+6.40),.72,front,material=teal)
    if level==0:
        for i in range(8):
            x=-5.9+(i%4)*1.3; zz=1.3+(i//4)*1.06
            place('Buzon_Individual',common,(x,.48,zz),math.pi,prefix='Buzon_%02d_'%(i+1))
            # Labels on front visible from lobby (positive Y).
            text_obj('Buzon_num_%d'%i,'%02d%s'%(i//2+1,'AB'[i%2]),(x,.89,zz+.37),.11,common,rot=(math.pi/2,0,math.pi))

roof=col('Cubierta_OCULTAR_para_ver_planta_04',building)
for x in range(-32,32,4):
    for y in range(0,28,4):
        if -4<=x<4 and 8<=y<16: continue
        place('Suelo_4x4_Terrazo',roof,(x,y,33.6))
for x in range(-32,32,4):
    for y in (0,28): place('Muro_Terraza_4x4',roof,(x,y,33.6))
for x in (-32,32):
    for y in range(0,28,4): place('Muro_Terraza_4x4',roof,(x,y,33.6),math.pi/2)
place('Lift_Shaft_Floor',roof,(0,10.8,33.6)); place('Lift_Roof_Cap',roof,(0,10.8,42))
for x in (-2.95,2.95): direct_box('Remate_cubierta_lateral_hueco',(x,12,33.4),(2.1,8,.4),tile,roof)
direct_box('Remate_cubierta_frente_hueco',(0,9.4,33.4),(3.8,2.8,.4),tile,roof)
direct_box('Remate_cubierta_fondo_hueco',(0,15.3,33.4),(3.8,1.4,.4),tile,roof)

site=col('Entorno_y_escala',building)
direct_box('Plinto',(0,12,-.72),(76,40,.62),grout,site)
for x,y in [(-35,23),(35,23),(-35,-3),(35,-3)]: place('Tree_Stylized',site,(x,y,0))
for z in (1,3): direct_box('Referencia_persona_cubo_2',(1,-3,z),(2,2,2),teal,site)
text_obj('Referencia_escala','2 CUBOS / 4 u',(1,-4.03,.05),.42,site,rot=(0,0,0))
studio=col('Camaras_e_iluminacion')
def camera(name,p,target,scale):
    d=bpy.data.cameras.new(name); o=bpy.data.objects.new(name,d); studio.objects.link(o); o.location=p
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler(); d.type='ORTHO'; d.ortho_scale=scale; d.clip_end=500
    return o
cam=camera('CAM_01_Edificio',(84,-111,83),(0,10,17),105)
cam_floor=camera('CAM_02_Planta_amueblada',(60,-55,107),(0,13,1),87)
cam_apt=camera('CAM_03_Vivienda',(44,-14,45),(20,13,1),43)
scene.camera=cam
def setup_render(sc):
    sc.render.engine='CYCLES'; sc.cycles.samples=24; sc.cycles.use_denoising=True
    sc.render.resolution_x=1600; sc.render.resolution_y=1200; sc.render.resolution_percentage=100
    sc.render.image_settings.file_format='PNG'; sc.view_settings.view_transform='AgX'
    w=bpy.data.worlds.new(sc.name+'_World'); w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(.60,.68,.73,1); w.node_tree.nodes['Background'].inputs[1].default_value=.55; sc.world=w
setup_render(scene)
for name,p,power,size in [('Luz_principal',(5,-25,65),26000,35),('Luz_relleno',(-45,-5,40),18000,30),('Luz_trasera',(25,45,65),32000,25)]:
    d=bpy.data.lights.new(name,'AREA'); d.energy=power; d.shape='DISK'; d.size=size
    o=bpy.data.objects.new(name,d); studio.objects.link(o); o.location=p; o.rotation_euler=(Vector((0,12,10))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.lights.new('Sol','SUN'); d.energy=1.5; d.angle=.15; o=bpy.data.objects.new('Sol',d); studio.objects.link(o); o.rotation_euler=(.35,-.45,-.35)

# Linked scene with ground floor only, no roof or exterior walls obscuring the furniture.
cut=bpy.data.scenes.new('03_Planta_amueblada'); setup_render(cut)
cut.collection.children.link(floors[0]); cut.collection.children.link(studio)
cut.camera=cam_floor
# Per-view-layer exclusions leave the complete building untouched.
def exclude(sc,names):
    def walk(lc):
        if lc.name in names: lc.exclude=True
        for ch in lc.children: walk(ch)
    walk(sc.view_layers[0].layer_collection)
exclude(cut,{front_cols[0].name,'P1_Fachadas_laterales_y_trasera'})
# A low-wall presentation uses linked mesh copies clipped geometrically, not scaled doors.
cutwalls=bpy.data.collections.new('Presentacion_Tabiques_bajos'); cut.collection.children.link(cutwalls)
for co in partition_cols[:2]:
    for o in co.objects:
        if o.type!='MESH': continue
        bpy.context.view_layer.update()
        me=o.data.copy(); me.transform(o.matrix_world)
        bm=bmesh.new(); bm.from_mesh(me)
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=(0,0,1.0),plane_no=(0,0,1),clear_outer=True,clear_inner=False)
        edges=[e for e in bm.edges if e.is_boundary]
        if edges: bmesh.ops.holes_fill(bm,edges=edges,sides=0)
        bm.to_mesh(me); bm.free(); me.update()
        ob=bpy.data.objects.new('CORTE_'+o.name,me); cutwalls.objects.link(ob)
exclude(cut,{c.name for c in partition_cols[:2]})

core_scene=bpy.data.scenes.new('05_Nucleo_escaleras_ascensor'); setup_render(core_scene)
core_scene.collection.children.link(studio)
for level in range(4): core_scene.collection.children.link(bpy.data.collections['P%d_Zonas_comunes'%(level+1)])
core_scene.camera=camera('CAM_06_Nucleo',(34,-28,43),(0,14,15),48)

# Catalog uses real editable objects and grouped furniture roots; original asset meshes linked.
catalog=bpy.data.scenes.new('04_Catalogo_muebles_nuevos'); setup_render(catalog)
gallery=bpy.data.collections.new('CATALOGO_NUEVOS_MODULOS'); catalog.collection.children.link(gallery)
catalog.collection.children.link(studio)
for idx,name in enumerate(assets):
    x=(idx%6)*11; y=(idx//6)*11
    r,obs=place(name,gallery,(x,y,0),prefix='Catalogo_')
    if name=='Frigorifico_Interior':
        for ob in obs:
            if 'Puerta_' in ob.name: ob.rotation_euler.z=math.radians(-105)
    text_obj('Etiqueta_'+name,name.replace('_',' '),(x,y-3.4,.03),.36,gallery,rot=(0,0,0))
    direct_box('Peana_'+name,(x,y,-.30),(9.8,9.8,.20),cream,gallery)
catalog.camera=camera('CAM_04_Catalogo',(70,-65,92),(27,22,0),88)
cam_fridge=camera('CAM_05_Frigorifico',(0,0,0),(0,0,1),10)
idx=list(assets).index('Frigorifico_Interior'); fp=Vector(((idx%6)*11,(idx//6)*11,0))
cam_fridge.location=fp+Vector((7,-11,7)); cam_fridge.rotation_euler=(fp+Vector((-.5,0,2.2))-cam_fridge.location).to_track_quat('-Z','Y').to_euler()

notes='''EDIFICIO MODULAR - 4 PLANTAS / 8 VIVIENDAS
Escenas: 01 biblioteca original intacta; 02 edificio completo; 03 planta amueblada en corte; 04 catalogo de muebles; 05 nucleo comun.
Cada planta tiene dos viviendas, salon-cocina, dos dormitorios (cama doble e individual), escritorio, armarios, bano y balcon.
Escala: cubo 2; personaje 2 x 2 x 4; paredes 8; plantas 8.4. Suelos 4 x 4 x .4, origen en esquina superior.
En escena 02 ocultar Cubierta y plantas superiores para editar interiores. Cada vivienda tiene tabiques y mobiliario separados.
La escena 03 comparte los muebles de planta 01 y usa copias recortadas de tabiques solo para mostrar distribucion.
Frigorifico: seleccionar malla Puerta_Nevera_PIVOTE o Puerta_Congelador_PIVOTE, girar Z local (-110 grados).
Los estantes interiores de cada puerta viajan con ella. Carcasa hueca y cajones reales. No hay animaciones.
Corredera: seleccionar Hoja_Movil, trasladar X local -3.8. Dejar Hoja_Fija. Hueco libre aproximado 3.6 x 4.6.
Las copias comparten mallas. Los objetos de cada mueble tienen un empty padre para mover el conjunto.
Biblioteca de modulos de origen conservada como colecciones marcadas Asset; catalogo editable en escena 04.
Escaleras originales: tres tramos de planta, cuatro paradas, una cabina y espacio superior del ascensor. No hay forjados atravesando el hueco.
Modelado visual: no incluye logica, colliders ni animaciones. FBX existentes sin modificar.
Referencias de diseno IKEA consultadas; reinterpretaciones propias en proporciones, patas, frentes y combinacion de materiales.
'''
txt=bpy.data.texts.new('LEEME_EDIFICIO'); txt.write(notes)
(OUT/'EDIFICIO_LEEME.txt').write_text(notes,encoding='utf-8')

bpy.context.window.scene=scene; bpy.context.view_layer.update()
for name,snap in original_snapshot.items():
    o=bpy.data.objects[name]
    assert max(abs(o.matrix_world[i][j]-snap['matrix'][i][j]) for i in range(4) for j in range(4))<1e-6, name
    if snap['vertices'] is not None: assert len(o.data.vertices)==snap['vertices'],name
checks={'plantas':len(floors),'viviendas':sum(1 for c in bpy.data.collections if c.name.startswith('Vivienda_')),'modulos_nuevos':len(assets),'reutilizacion_original':reused,'originales_verificados':len(original_snapshot),'assets':{}}
for name,c in assets.items():
    triangles=0
    for o in c.objects:
        o.data.calc_loop_triangles(); triangles+=len(o.data.loop_triangles)
        assert all(p.area>1e-10 for p in o.data.polygons),(name,'degenerate face')
        assert all(abs(s-1)<1e-6 for s in o.scale)
    checks['assets'][name]={'triangulos':triangles,'objetos':[o.name for o in c.objects]}
assert checks['plantas']==4 and checks['viviendas']==8
(OUT/'edificio_report.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
for sc in (scene,cut,catalog):
    sc['personaje_altura']=4.0; sc['cubo_lado']=2.0; sc['paso_plantas']=8.4
bpy.context.window.scene=scene
for screen_ui in bpy.data.screens:
    for area in screen_ui.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active; sp.clip_end=1000; sp.shading.type='MATERIAL'
            sp.region_3d.view_location=(0,10,15); sp.region_3d.view_distance=100
            sp.region_3d.view_rotation=cam.rotation_euler.to_quaternion(); sp.region_3d.view_perspective='ORTHO'
scene.cursor.location=(0,0,0)
bpy.context.preferences.filepaths.save_version=0
validation_script=OUT/'validate_building.py'
exec(compile(validation_script.read_text(encoding='utf-8'),str(validation_script),'exec'),{'__file__':str(validation_script)})
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Casa_Modular.blend'))
print('BUILDING_SAVED',json.dumps({k:v for k,v in checks.items() if k!='assets'}),flush=True)
def render(sc,filename,camera_ob=None):
    bpy.context.window.scene=sc
    if camera_ob: sc.camera=camera_ob
    sc.render.filepath=str(OUT/filename); bpy.ops.render.render(write_still=True,scene=sc.name)
render(scene,'04_edificio_4_plantas.png')
render(cut,'05_planta_amueblada.png')
render(cut,'08_vivienda_detalle.png',cam_apt)
cut.camera=cam_floor
render(catalog,'06_catalogo_nuevos.png')
render(catalog,'07_frigorifico_abierto.png',cam_fridge)
render(core_scene,'09_nucleo_comun.png')
catalog.camera=bpy.data.objects['CAM_04_Catalogo']; bpy.context.window.scene=scene
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Casa_Modular.blend'))
print('ALL_DONE',flush=True)
