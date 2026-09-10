"""Run on Casa_Modular_antes_arboles.blend. Writes only edificio_revisado/.
Reuses existing meshes and modeling helpers; never runs the old generator.
"""
import bpy, bmesh, math, json, ast, sys
from pathlib import Path
from mathutils import Vector, Matrix

BASE=Path(__file__).resolve().parent
OUT=BASE/'edificio_revisado'; OUT.mkdir(exist_ok=True)
assert Path(bpy.data.filepath).name=='Casa_Modular_antes_arboles.blend'
snapshot={o.name:(o.data.name if o.data else None,tuple(tuple(r) for r in o.matrix_world)) for o in bpy.data.objects}
source=ast.parse((BASE/'extend_building.py').read_text(encoding='utf-8'))
charcoal=bpy.data.materials['ED_Grafito']
functions={'col','mesh','box','cyl','beam','merge','begin','finish','place','text_obj','direct_box','camera','setup_render','exclude'}
for node in source.body:
    if isinstance(node,ast.FunctionDef) and node.name in functions:
        exec(compile(ast.Module(body=[node],type_ignores=[]),'existing_helpers','exec'))
    if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='source_offsets' for t in node.targets):
        source_offsets=ast.literal_eval(node.value)
cream=bpy.data.materials['ED_Marfil']; oak=bpy.data.materials['ED_Roble_claro']
paleoak=bpy.data.materials['ED_Roble_miel']; metal=bpy.data.materials['ED_Acero']
teal=bpy.data.materials['ED_Tejido_petroleo']; charcoal=bpy.data.materials['ED_Grafito']
glass=bpy.data.materials['ED_Vidrio']; tile=bpy.data.materials['ED_Terrazo']
grout=bpy.data.materials['ED_Junta']; linen=bpy.data.materials['ED_Lino']
sage=bpy.data.materials['ED_Salvia']; white=bpy.data.materials['ED_Ceramica']
screen=bpy.data.materials['ED_Pantalla']; yellow=bpy.data.materials['ED_Laton']
assets={c.name:c for c in bpy.data.collections['MODULOS_NUEVOS_ORIGEN'].children}; reused={}
scene=bpy.data.scenes.new('06_EDIFICIO_CORREGIDO'); bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1
building=col('REV_EDIFICIO_72x32'); lib=col('REV_MODULOS_EDITABLES')
lib.hide_render=True
lib.hide_viewport=True
active=None; parts=[]

# New walls: local +Y is interior for exterior modules. Door gaps interrupt trims.
for interior in (False,True):
    for kind in ('solid','window','door'):
        name='REV_Pared_'+('Interior_Doble_' if interior else 'Exterior_InteriorMasY_')+kind
        begin(name)
        if kind=='solid': box('Pared',(2,0,4),(4,.3,8),cream)
        elif kind=='window':
            box('Antepecho',(2,0,1.5),(4,.3,3),cream)
            box('Dintel',(2,0,7.1),(4,.3,1.8),cream)
            for x in (.275,3.725): box('Jamba',(x,0,4.6),(.55,.3,3.2),cream)
        else:
            box('Dintel',(2,0,6.4),(4,.3,3.2),cream)
            for x in (.3,3.7): box('Jamba',(x,0,2.4),(.6,.3,4.8),cream)
        for side in ((-1,1) if interior else (1,)):
            if kind=='door':
                for x in (.3,3.7): box('Rodapie',(x,side*.19,.18),(.6,.08,.36),oak)
            else: box('Rodapie',(2,side*.19,.18),(4,.08,.36),oak)
        finish(name,note='Origen extremo inferior. '+('Rodapies ambas caras.' if interior else 'Rodapie solo cara +Y, interior.'))

# Shop wall display: open shelves, real hollow cabinet, two independent doors and drawer.
begin('REV_Expositor_Pared')
for x in (-2.35,2.35): box('Costado',(x,0,2.6),(.18,1.6,5.2),oak,.02)
box('Trasera',(0,.73,2.6),(4.6,.14,5.2),sage)
for z in (.15,1.5,2.0,3.05,4.1,5.15): box('Balda',(0,0,z),(4.6,1.55,.12),paleoak,.02)
box('Tabique armario',(0,0,.83),(.12,1.4,1.2),oak)
body=merge('Expositor_Carcasa_Baldas'); moving=[]
for sign in (-1,1):
    box('Hoja',(sign*1.16,-.84,.83),(2.26,.12,1.19),cream,.025)
    box('Tirador',(sign*.22,-.94,.87),(.08,.12,.4),yellow,.015)
    o=merge('Expositor_Puerta_'+str(sign),(sign*2.31,-.84,0)); o['animar']='Rotacion Z local'; moving.append(o)
box('Frente cajon',(0,-.85,1.76),(4.48,.12,.37),sage,.02)
box('Base cajon',(0,-.03,1.61),(4.4,1.35,.08),oak)
for x in (-2.18,2.18): box('Lateral cajon',(x,-.03,1.76),(.08,1.35,.3),oak)
box('Trasera cajon',(0,.6,1.76),(4.4,.08,.3),oak)
box('Asa cajon',(0,-.95,1.76),(.75,.12,.07),yellow)
o=merge('Expositor_Cajon',(0,0,0)); o['animar']='Traslacion local -Y'; moving.append(o)
finish('REV_Expositor_Pared',[body]+moving)

begin('REV_Expositor_Isla_Huecos')
box('Zocalo',(0,0,.15),(3.4,2.5,.3),charcoal,.03)
for z in (.36,1.3,2.25): box('Estante',(0,0,z),(3.8,2.8,.14),paleoak,.03)
box('Division central',(0,0,1.3),(3.6,.12,1.8),sage)
for x in (-1.8,0,1.8): box('Division lateral',(x,0,1.3),(.12,2.6,1.8),oak)
finish('REV_Expositor_Isla_Huecos',note='Huecos abiertos reales a ambos lados +/-Y, no caras oscuras simuladas.')

begin('REV_Mostrador')
box('Frente',(0,-.95,1.25),(5.4,.18,2.3),sage,.04)
for x in (-2.6,2.6): box('Costado',(x,0,1.25),(.18,2.0,2.3),oak,.025)
for z in (.2,1.3): box('Balda posterior',(0,0,z),(5.2,1.85,.12),paleoak)
box('Encimera',(0,0,2.5),(5.65,2.2,.2),paleoak,.055)
finish('REV_Mostrador')
begin('REV_Caja_Registradora')
box('Base cajon',(0,0,.16),(1.5,1.25,.32),charcoal,.04)
box('Panel',(0,.18,.44),(1.32,.72,.32),cream,.04)
box('Soporte pantalla',(.15,.35,.85),(.14,.14,.65),metal)
box('Pantalla marco',(.15,.32,1.2),(1.15,.16,.67),charcoal,.035)
box('Pantalla',(.15,.23,1.2),(.99,.025,.5),screen)
for i in range(4):
    for j in range(3): box('Tecla',(-.42+i*.25,-.4+j*.17,.345),(.18,.12,.035),linen,.007)
case=merge('Registradora_Cuerpo')
box('Cajon frente',(0,-.66,.15),(1.39,.08,.23),metal,.02)
box('Cajon base',(0,-.09,.08),(1.32,1.04,.06),charcoal)
drawer=merge('Registradora_Cajon'); drawer['animar']='Traslacion local -Y'
finish('REV_Caja_Registradora',[case,drawer])

begin('REV_Cancela_Portal_8x8')
for x in (.15,7.85): box('Pilar',(x,0,4),(.3,.5,8),cream)
box('Dintel',(4,0,7.5),(7.4,.5,1),cream)
frame=merge('Cancela_Marco'); leaves=[]
for sign in (-1,1):
    center=4+sign*1.8
    for x in (center-1.73,center+1.73): box('Bastidor vertical',(x,0,3.45),(.13,.2,6.7),metal,.02)
    for z in (.15,1.4,6.75): box('Bastidor horizontal',(center,0,z),(3.58,.2,.14),metal,.02)
    box('Zocalo hoja',(center,0,.77),(3.38,.12,1.1),teal,.02)
    for i in range(7): box('Barrote',(center-1.4+i*.467,0,4.05),(.065,.08,5.2),metal)
    box('Tirador',(4+sign*.3,-.2,3.1),(.09,.18,.8),yellow,.02)
    leaf=merge('Cancela_Hoja_'+str(sign),(4+sign*3.6,0,0)); leaf['animar']='Rotacion Z local, bisagra lateral'; leaves.append(leaf)
finish('REV_Cancela_Portal_8x8',[frame]+leaves,note='Dos hojas independientes, paso total 7.2 y altura 6.6, mayor que puertas domesticas.')

begin('REV_Escaparate_14')
for x in (.12,5.3,8.7,13.88): box('Montante',(x,0,3.3),(.18,.24,6.6),metal)
for z in (.12,6.5): box('Travesano',(7,0,z),(14,.24,.18),metal)
box('Dintel comercial',(7,0,7.3),(14,.3,1.4),cream)
for x in (2.7,11.3): box('Cristal escaparate',(x,0,3.3),(5.02,.045,6.2),glass)
frame=merge('Escaparate_Fijo')
for x in (5.48,8.52): box('Marco puerta',(x,-.04,3.28),(.12,.16,6.3),metal)
for z in (.18,6.37): box('Marco puerta',(7,-.04,z),(3.15,.16,.12),metal)
box('Cristal puerta',(7,-.04,3.28),(2.92,.045,6.05),glass)
box('Asa',(8.2,-.18,3),(.08,.16,.65),yellow,.02)
door=merge('Comercio_Puerta_PIVOTE',(5.42,-.04,0)); door['animar']='Rotacion Z local'
finish('REV_Escaparate_14',[frame,door])

wall_records=[]; door_labels=[]; floors=[]; lowwall_sources=[]; all_common=[]
def wall(c,x,y,z,angle=0,kind='solid',interior=False,inward=1,tag=''):
    # Flip module around its far endpoint when -Y is the desired inner face.
    if not interior and inward<0:
        x+=4*math.cos(angle); y+=4*math.sin(angle); angle+=math.pi
    name='REV_Pared_'+('Interior_Doble_' if interior else 'Exterior_InteriorMasY_')+kind
    root,obs=place(name,c,(x,y,z),angle,tag)
    wall_records.append({'kind':kind,'interior':interior,'pos':[x,y,z],'angle':angle})
    if kind=='window': place('Window_Insert',c,(x,y,z),angle,tag)
    if kind=='door':
        place('Door_Frame',c,(x,y,z),angle,tag)
        delta=Vector((.74,0,0)); delta.rotate(Matrix.Rotation(angle,3,'Z'))
        r,_=place('Door_Leaf_Panelled',c,Vector((x,y,z))+delta,angle,tag)
        r['animar']='Rotacion Z local del padre (bisagra)'
    return root

def segment(c,name,x1,y1,x2,y2,z,both=True,inward=1):
    length=math.hypot(x2-x1,y2-y1); angle=math.atan2(y2-y1,x2-x1)
    root=bpy.data.objects.new(name,None); c.objects.link(root); root.location=(x1,y1,z); root.rotation_euler.z=angle
    ob=direct_box(name+'_Muro',(length/2,0,4),(length,.3,8),cream,c); ob.parent=root
    for side in ((-1,1) if both else (inward,)):
        ob=direct_box(name+'_Rodapie',(length/2,side*.19,.18),(length,.08,.36),oak,c); ob.parent=root

def floors_rect(c,x0,x1,y0,y1,z,material=tile):
    direct_box('Forjado_continuo',((x0+x1)/2,(y0+y1)/2,z-.22),(x1-x0,y1-y0,.36),cream,c)
    direct_box('Pavimento_continuo',((x0+x1)/2,(y0+y1)/2,z-.02),(x1-x0,y1-y0,.04),material,c)

def railing(c,a,b,z):
    # Low-cost separate guards: clear routes to stairs remain unobstructed.
    for h in (.15,2.1):
        o=direct_box('Barandilla',((a[0]+b[0])/2,(a[1]+b[1])/2,z+h),(math.dist(a,b),.1,.1),metal,c)
        # direct_box bakes coords; rotation must be about its centre.
        center=Vector(((a[0]+b[0])/2,(a[1]+b[1])/2,z+h)); o.data.transform(Matrix.Translation(-center)); o.location=center; o.rotation_euler.z=math.atan2(b[1]-a[1],b[0]-a[0])
    n=max(1,math.ceil(math.dist(a,b)/2))
    for i in range(n+1):
        t=i/n; direct_box('Poste',(a[0]*(1-t)+b[0]*t,a[1]*(1-t)+b[1]*t,z+1.05),(.1,.1,2.1),metal,c)

shop_names=['ESTUDIO NORTE','OBJETOS & HOGAR','TALLER URBANO','LA DESPENSA']
shops=[]; apartments=[]
for level in range(4):
    z=level*8.4; f=col('REV_P%d'%level,building); floors.append(f)
    fc=col('REV_P%d_Suelos'%level,f); outer=col('REV_P%d_Exterior'%level,f)
    inner=col('REV_P%d_Tabiques'%level,f); lowwall_sources.append(inner)
    common=col('REV_P%d_Comun'%level,f); all_common.append(common)
    # Whole common circulation floor except the real stair and lift shafts.
    floors_rect(fc,-8,8,0,8,z)
    if level==0:
        floors_rect(fc,-8,-1.9,8,32,z); floors_rect(fc,1.9,8,8,32,z)
        floors_rect(fc,-1.9,1.9,8,10.8,z); floors_rect(fc,-1.9,1.9,14.6,32,z)
    else:
        floors_rect(fc,-8,-4.5,8,21.4,z); floors_rect(fc,4.5,8,8,21.4,z)
        floors_rect(fc,-8,8,21.4,32,z)
        floors_rect(fc,-1.9,1.9,14.6,21.4,z)
        # Guards along actual floor openings, not across the top of a stair flight.
        railing(common,(-4.5,8),(-4.5,21.4),z); railing(common,(4.5,8),(4.5,21.4),z)
        railing(common,(-4.5,21.4),(-1.9,21.4),z); railing(common,(1.9,21.4),(4.5,21.4),z)
        railing(common,(-1.9,14.6),(-1.9,21.4),z); railing(common,(1.9,14.6),(1.9,21.4),z)
    # Stair arrivals already supply their own 9 x 2.8 landing at every upper level.
    if level<3: place('Stairs_U_Floor',common,(0,8,z))
    place('Lift_Shaft_Floor',common,(0,10.8,z)); place('Lift_Landing_Doors',common,(0,10.8,z))
    if level==0: place('Lift_Cabin',common,(0,10.8,z))
    for x in range(-8,8,4): wall(outer,x,32,z,inward=-1)
    for x in (-8,4): wall(outer,x,0,z)
    if level==0: place('REV_Cancela_Portal_8x8',outer,(-4,0,z))
    else:
        for x in (-4,0): wall(outer,x,0,z)
    for side,ax in [('A',-36),('B',8)]:
        corex=-8 if side=='A' else 8
        for y in range(0,32,4): wall(inner,corex,y,z,math.pi/2,'door' if level>0 and y==4 else 'solid',True,tag='Acceso_'+side+'_')
        floors_rect(fc,ax,ax+28,0,32,z,paleoak if level else tile)
        if level==0:
            for k in range(2):
                sx=ax+k*14; num=len(shops); sc=col('REV_Comercio_%d'%(num+1),f); shops.append(sc)
                furn=col('REV_Comercio_%d_Muebles'%(num+1),sc)
                place('REV_Escaparate_14',outer,(sx,0,0))
                text_obj('Rotulo_comercio',shop_names[num],(sx+7,-.18,7.13),.46,outer,material=teal)
                # Rear store access door, no unnecessary internal windows.
                segment(inner,'Tabique_almacen',sx,24,sx+4,24,0)
                wall(inner,sx+4,24,0,kind='door',interior=True)
                segment(inner,'Tabique_almacen',sx+8,24,sx+14,24,0)
                for localx in (2.38,11.62): place('REV_Expositor_Pared',furn,(sx+localx,23,0))
                # Shelving backs sit close to the side partitions and face the aisles.
                for yy in (8.5,14,19.5): place('REV_Expositor_Pared',furn,(sx+1,yy,0),math.pi/2)
                for yy in (10,17): place('REV_Expositor_Isla_Huecos',furn,(sx+8.8,yy,0),math.pi/2)
                place('REV_Mostrador',furn,(sx+3.5,4.4,0))
                place('REV_Caja_Registradora',furn,(sx+3.0,4.4,2.61))
                for xx in (3,10): place('REV_Expositor_Pared',furn,(sx+xx,30.9,0))
                # A small amount of simple merchandise, not textures.
                for yy in (10,17):
                    for offset in (-.8,.8):
                        direct_box('Producto_caja',(sx+8.8+offset,yy,2.53),(1.0,.8,.42),linen,furn)
            segment(inner,'Separacion_comercios',ax+14,0,ax+14,32,0)
            outerx=ax if side=='A' else ax+28
            for yy in range(0,32,4): wall(outer,outerx,yy,z,math.pi/2,inward=-1 if side=='A' else 1)
            for xx in range(0,28,4): wall(outer,ax+xx,32,z,inward=-1)
        else:
            code='%02d%s'%(level,side); apt=col('REV_Vivienda_'+code,f); apartments.append(apt)
            furn=col('REV_'+code+'_Muebles',apt)
            def put(name,x,y,zz=0,angle=0): return place(name,furn,(ax+x,y,z+zz),angle,code+'_')
            # External openings only: 3 living openings incl balcony, 1 kitchen,
            # 1 window per bedroom, plus an optional outer-side bedroom window.
            for xx in (0,12,16,20,24): wall(outer,ax+xx,0,z,kind='window' if xx in (0,12,20) else 'solid')
            place('Corredera_Balcon_8x8',outer,(ax+4,0,z),prefix=code+'_')
            floors_rect(fc,ax+4,ax+12,-4,0,z,grout)
            for xx in (4,8): place('Muro_Terraza_4x4',outer,(ax+xx,-4,z))
            for xx in (4,12): place('Muro_Terraza_4x4',outer,(ax+xx,-4,z),math.pi/2)
            outerx=ax if side=='A' else ax+28
            for yy in range(0,32,4): wall(outer,outerx,yy,z,math.pi/2,'window' if yy in (8,28) else 'solid',inward=-1 if side=='A' else 1)
            for xx in range(0,28,4): wall(outer,ax+xx,32,z,kind='window' if xx in (4,20) else 'solid',inward=-1)
            for xx in range(0,28,4): wall(inner,ax+xx,24,z,kind='door' if xx in (4,16) else 'solid',interior=True)
            segment(inner,'Separacion_dormitorios',ax+14,24,ax+14,32,z)
            for xx in (20,24): wall(inner,ax+xx,14,z,interior=True)
            segment(inner,'Bano_remate',ax+20,14,ax+20,16,z)
            wall(inner,ax+20,16,z,math.pi/2,'door',True)
            wall(inner,ax+20,20,z,math.pi/2,interior=True)
            # Only apartment ID, directly over its entrance on the common corridor face.
            rot=(math.pi/2,0,math.pi/2 if side=='A' else -math.pi/2)
            label=text_obj('REV_Numero_'+code,code,(corex+(.24 if side=='A' else -.24),6,z+5.65),.64,common,rot=rot,material=teal)
            door_labels.append(label)
            put('Sofa_3_Plazas' if side=='A' else 'Sofa_2_Plazas',8,11,angle=-math.pi/2)
            put('Coffee_Table',3.8,11); put('TV_Modern_Wall_Large',.55,11,3.2,math.pi/2)
            direct_box(code+'_Alfombra',(ax+5.2,11,z+.025),(9,8.6,.04),linen,furn)
            put('Cama_Doble',5.4,29); put('Mesita_Noche',1.8,30); put('Mesita_Noche',9,30)
            put('Armario_Dos_Puertas',12.5,29,angle=-math.pi/2)
            put('Cama_Individual',24.5,29); put('Mesita_Noche',26.7,30)
            put('Escritorio_Ordenador',17,30.5); put('Silla_Escritorio',17,27.9,angle=math.pi)
            for name,x in [('Cocina_Modulo_Fregadero',18.5),('Cocina_Modulo_Horno',21.8),('Cocina_Modulo_Cajones',25.1)]: put(name,x,12.6)
            put('Cocina_Armario_Alto',25.1,13.25,3.45); put('Campana_Extractora',21.8,13,4)
            put('Frigorifico_Interior',25.8,1.6,angle=math.pi)
            put('Mesa_Comedor',17,5,angle=math.pi/2)
            put('Chair_02_Comfort',14.1,5,angle=math.pi/2); put('Chair_02_Comfort',19.9,5,angle=-math.pi/2)
            put('Ducha',25.95,21.95); put('Vater',22,22.4); put('Lavabo_Mueble',25,15.15,angle=math.pi)

roof=col('REV_Cubierta_Ocultable',building)
floors_rect(roof,-36,-1.9,0,32,33.6); floors_rect(roof,1.9,36,0,32,33.6)
floors_rect(roof,-1.9,1.9,0,10.8,33.6); floors_rect(roof,-1.9,1.9,14.6,32,33.6)
place('Lift_Shaft_Floor',roof,(0,10.8,33.6)); place('Lift_Roof_Cap',roof,(0,10.8,42))
for x in range(-36,36,4):
    for y in (0,32): place('Muro_Terraza_4x4',roof,(x,y,33.6))
for x in (-36,36):
    for y in range(0,32,4): place('Muro_Terraza_4x4',roof,(x,y,33.6),math.pi/2)
site=col('REV_Entorno',building)
floors_rect(site,-41,41,-8,36,-.05,grout)
for z in (1,3): direct_box('Referencia_cubo_2',(1,-4,z),(2,2,2),teal,site)
# Modest canopy and continuous floor bands give the expanded facade a clear rhythm.
direct_box('Marquesina_portal',(0,-1,7.7),(9,2.4,.22),metal,site)
for z in (8.25,16.65,25.05): direct_box('Banda_fachada',(0,-.2,z),(72,.14,.22),cream,site)

studio=col('REV_Camaras_Luces')
cam=camera('REV_CAM_Exterior',(100,-136,91),(0,12,17),119)
cam_shop=camera('REV_CAM_Comercio',(-12,-12,12),(-27,12,2.6),31)
cam_ground=camera('REV_CAM_Planta0',(0,16,113),(0,16,0),85)
cam_flat=camera('REV_CAM_Vivienda',(0,16,120),(0,16,8.4),85)
scene.camera=cam; setup_render(scene); scene.cycles.samples=20
for name,p,power,size in [('Key',(0,-35,65),42000,40),('Fill',(-45,-5,40),25000,30),('Rim',(30,50,65),38000,30)]:
    d=bpy.data.lights.new('REV_'+name,'AREA'); d.energy=power; d.shape='DISK'; d.size=size
    o=bpy.data.objects.new(d.name,d); studio.objects.link(o); o.location=p; o.rotation_euler=(Vector((0,12,10))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.lights.new('REV_Sol','SUN'); d.energy=1.2; d.angle=.15
o=bpy.data.objects.new(d.name,d); studio.objects.link(o); o.rotation_euler=(.35,-.45,-.35)

# Cutaway scenes use independent clipped copies; full-height building stays intact.
def cut_scene(name,level,cam):
    sc=bpy.data.scenes.new(name); setup_render(sc); sc.cycles.samples=16
    sc.collection.children.link(floors[level]); sc.collection.children.link(studio); sc.camera=cam
    bpy.context.window.scene=sc; bpy.context.view_layer.update()
    exclude(sc,{'REV_P%d_Exterior'%level,'REV_P%d_Tabiques'%level,'REV_P%d_Comun'%level})
    cuts=bpy.data.collections.new(name+'_Muros_bajos'); sc.collection.children.link(cuts)
    bpy.context.window.scene=scene; bpy.context.view_layer.update()
    for src in (lowwall_sources[level],bpy.data.collections['REV_P%d_Exterior'%level]):
        for o in src.all_objects:
            if o.type!='MESH': continue
            me=o.data.copy(); me.transform(o.matrix_world)
            bm=bmesh.new(); bm.from_mesh(me)
            bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=(0,0,level*8.4+.8),plane_no=(0,0,1),clear_outer=True,clear_inner=False)
            edges=[e for e in bm.edges if e.is_boundary]
            if edges: bmesh.ops.holes_fill(bm,edges=edges,sides=0)
            bm.to_mesh(me); bm.free(); me.update()
            ob=bpy.data.objects.new('CORTE_'+o.name,me); cuts.objects.link(ob)
    return sc
ground=cut_scene('07_PLANTA_0_COMERCIOS',0,cam_ground)
flat=cut_scene('08_PLANTA_1_VIVIENDAS',1,cam_flat)
# Show common circulation with the actual stair and lift at this level in cut plans.
# Use only lower portions to prevent a stair arriving above the camera's target masking the floor.
for sc,level in ((ground,0),(flat,1)):
    commoncopy=bpy.data.collections.new(sc.name+'_Nucleo'); sc.collection.children.link(commoncopy)
    for ob in all_common[level].all_objects:
        if ob.type!='MESH': continue
        n=ob.copy(); n.data=ob.data; commoncopy.objects.link(n); n.parent=None; n.matrix_world=ob.matrix_world.copy()

detail=bpy.data.scenes.new('09_DETALLE_COMERCIO'); setup_render(detail); detail.cycles.samples=24
detail.collection.children.link(studio); detail.collection.children.link(shops[0]); detail.camera=cam_shop
detailcol=bpy.data.collections.new('REV_Detalle_contexto'); detail.collection.children.link(detailcol)
floors_rect(detailcol,-36,-22,0,32,0,tile)
segment(detailcol,'Pared_fondo',-36,24,-32,24,0)
wall(detailcol,-32,24,0,kind='door',interior=True)
segment(detailcol,'Pared_fondo',-28,24,-22,24,0)
segment(detailcol,'Pared_lateral',-36,0,-36,24,0)

catalog=bpy.data.scenes.new('10_MODULOS_COMERCIO_Y_PAREDES'); setup_render(catalog)
catalog.collection.children.link(studio); catcol=bpy.data.collections.new('REV_Catalogo'); catalog.collection.children.link(catcol)
newnames=[n for n in assets if n.startswith('REV_')]
for i,name in enumerate(newnames):
    r,obs=place(name,catcol,((i%4)*18,(i//4)*12,0))
    if name=='REV_Expositor_Pared':
        for ob in obs:
            if 'Puerta_' in ob.name: ob.rotation_euler.z=math.radians(-65 if '-1' in ob.name else 65)
            if 'Cajon' in ob.name: ob.location.y-=.7
    text_obj('Etiqueta',name.replace('REV_',''),((i%4)*18,(i//4)*12-2,.02),.4,catcol,rot=(0,0,0))
catalog.camera=camera('REV_CAM_Modulos',(62,-65,90),(28,14,1),98)

# Validation: original data preserved; no openings on apartment/common boundaries.
bpy.context.window.scene=scene; bpy.context.view_layer.update()
assert len(shops)==4 and len(apartments)==6 and len(door_labels)==6
assert not any(w['kind']=='window' and w['interior'] for w in wall_records)
shop_pairs=0
for co in shops:
    roots=[ob for ob in co.all_objects if ob.type=='EMPTY']
    bounds=[]
    for ob in roots:
        pts=[child.matrix_world@Vector(v) for child in ob.children if child.type=='MESH' for v in child.bound_box]
        if pts: bounds.append((ob.name,[min(p[i] for p in pts) for i in range(3)],[max(p[i] for p in pts) for i in range(3)]))
    for i,(name,lo,hi) in enumerate(bounds):
        for other,ol,oh in bounds[i+1:]:
            assert not all(min(hi[k],oh[k])-max(lo[k],ol[k])>.01 for k in range(3)),('shop furniture overlap',name,other)
            shop_pairs+=1
for ob in door_labels:
    normal=ob.matrix_world.to_3x3()@Vector((0,0,1))
    desired=Vector((1,0,0)) if ob.location.x<0 else Vector((-1,0,0))
    assert normal.dot(desired)>.99,ob.name
# Downward samples across all required common circulation areas, avoiding only
# intentional stair/lift openings. This catches missing strips of paving.
depsgraph=bpy.context.evaluated_depsgraph_get(); floor_samples=0
for level in range(4):
    z=level*8.4
    for ix in range(32):
        x=-7.75+ix*.5
        for iy in range(64):
            y=.25+iy*.5
            lift=abs(x)<1.9 and 10.8<y<14.6
            stairs=abs(x)<4.5 and 8<y<21.4
            center_rear=abs(x)<1.9 and 14.6<y<21.4
            if lift or (level>0 and stairs and not center_rear): continue
            hit,location,*_=scene.ray_cast(depsgraph,Vector((x,y,z+.015)),Vector((0,0,-1)),distance=.035)
            assert hit and abs(location.z-z)<.01,('missing common floor',level,x,y)
            floor_samples+=1
for name,(data,matrix) in snapshot.items():
    ob=bpy.data.objects[name]
    assert (ob.data.name if ob.data else None)==data,name
    assert max(abs(ob.matrix_world[i][j]-matrix[i][j]) for i in range(4) for j in range(4))<1e-5,name
triangles={}
for name in newnames:
    count=0
    for ob in assets[name].objects:
        ob.data.calc_loop_triangles(); count+=len(ob.data.loop_triangles)
        assert all(p.area>1e-10 for p in ob.data.polygons),ob.name
    triangles[name]=count
report={'input':str(BASE/'Casa_Modular_antes_arboles.blend'),'building_size':[72,32], 'floor_pitch':8.4,
        'shops':4,'apartments':6,'labels_only_at_apartment_entrances':len(door_labels),
        'windows_on_internal_boundaries':0,'original_objects_preserved':len(snapshot),'new_asset_triangles':triangles,
        'common_floor_surface_samples_passed':floor_samples,
        'shop_furniture_pairs_without_overlap':shop_pairs,
        'note':'Visual geometry checks only. No Unity collision/navigation test.'}
(OUT/'revision_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
scene['descripcion']='P0: cuatro comercios. P1-P3: dos viviendas por planta. Muros 8; paso 8.4; 72 x 32.'
for screen_ui in bpy.data.screens:
    for area in screen_ui.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active; sp.clip_end=1000; sp.region_3d.view_location=(0,12,16)
            sp.region_3d.view_distance=110; sp.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
            sp.region_3d.view_perspective='ORTHO'; sp.shading.type='MATERIAL'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Casa_Modular_Edificio_Corregido.blend'))
print('REVISION_SAVED',json.dumps(report),flush=True)
for sc,filename in [(scene,'01_exterior.png'),(ground,'02_planta_comercios.png'),(flat,'03_planta_viviendas.png'),(detail,'04_comercio_detalle.png'),(catalog,'05_modulos.png')]:
    if '--only' in sys.argv and filename[:2] not in sys.argv[sys.argv.index('--only')+1].split(','): continue
    bpy.context.window.scene=sc; sc.render.filepath=str(OUT/filename)
    bpy.ops.render.render(write_still=True,scene=sc.name)
bpy.context.window.scene=scene
print('REVISION_DONE',flush=True)
