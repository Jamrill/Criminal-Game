"""Extend Calle Modular without editing the user's meshes. Run with Blender -b file --python this.py."""
import bpy, math, json, shutil
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / 'Calle Modular.blend'
OUT = ROOT / 'calle_modular_amueblado'
OUT.mkdir(exist_ok=True)
BACKUP = OUT / 'Calle Modular_antes_muebles.blend'
if not BACKUP.exists(): shutil.copy2(SOURCE, BACKUP)
scene = bpy.context.scene
originals = {o.name: o for o in bpy.data.objects}
assert 'Table' in originals and 'Wall' in originals and 'Suelo' in originals
assert not bpy.data.collections.get('CM_01_Sillas'), 'Already generated: start from the backup to rebuild.'
report = {'floor_pitch': 7, 'floor_tile': [4,4,.02], 'assets': {}}
parts = []
current = None

def collection(name, parent=None):
    c = bpy.data.collections.new(name)
    (parent or scene.collection).children.link(c)
    return c

def move(o, c):
    for old in list(o.users_collection): old.objects.unlink(o)
    c.objects.link(o)

def mat(name, color, rough=.45, metal=0):
    m = bpy.data.materials.new('CM_' + name)
    m.diffuse_color = (*color,1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color,1)
    p.inputs['Roughness'].default_value = rough
    p.inputs['Metallic'].default_value = metal
    return m

oak = mat('Roble_miel', (.34,.19,.085), .38)
edgewood = mat('Roble_canto', (.19,.087,.035), .42)
teal = mat('Tapizado_petroleo', (.055,.20,.19), .88)
linen = mat('Lino_arena', (.64,.52,.35), .92)
rust = mat('Cojin_teja', (.49,.16,.075), .85)
dark = mat('Metal_grafito', (.045,.055,.06), .3, .7)
brass = mat('Metal_laton', (.48,.32,.12), .27, .8)
screen = mat('Pantalla_apagada', (.012,.025,.038), .16, .3)
rugmat = mat('Alfombra_crudo', (.68,.59,.43), .98)
rugline = mat('Alfombra_borde', (.24,.32,.29), .98)
ivory = mat('Pantalla_lampara', (.82,.72,.52), .7)

def finish(o, name, material, bevel=0, segments=1):
    o.name = name
    move(o,current)
    o.data.materials.append(material)
    if bevel:
        mod=o.modifiers.new('Cantos', 'BEVEL'); mod.width=bevel; mod.segments=segments
        bpy.context.view_layer.objects.active=o
        bpy.ops.object.modifier_apply(modifier=mod.name)
    parts.append(o)
    return o

def box(name, xyz, dims, material, bevel=0, seg=1):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz)
    o=bpy.context.object; o.dimensions=dims
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(o,name,material,bevel,seg)

def cylinder(name, xyz, radius, depth, material, sides=16, radius2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=radius,
        radius2=radius if radius2 is None else radius2, depth=depth, location=xyz)
    return finish(bpy.context.object,name,material)

def beam(name, a, b, radius, material, sides=12):
    a,b=Vector(a),Vector(b)
    o=cylinder(name,(a+b)/2,radius,(b-a).length,material,sides)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return o

def merge(name, pivot=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join()
    o=bpy.context.object; o.name=name
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    # All new assets use the upper-right footprint corner at floor level, like Suelo.
    coords=[o.matrix_world @ v.co for v in o.data.vertices]
    pivot=Vector(pivot) if pivot is not None else Vector((max(v.x for v in coords),max(v.y for v in coords),0))
    for v,co in zip(o.data.vertices,coords): v.co=co-pivot
    o.matrix_world=Matrix.Identity(4)
    o['Origin_convention']='Max X / Max Y footprint corner, Z=0 (floor)'
    o['ModelingPivot']=list(pivot)
    o.data.update()
    return o

def chair(lod):
    seg=[3,1,0][lod]; b=[.055,.035,0][lod]
    box('Asiento_marco',(0,0,1.36),(1.7,1.7,.22),oak,b,seg or 1)
    box('Asiento_tapizado',(0,-.04,1.52),(1.54,1.5,.20),linen,b,seg or 1)
    for x in (-.66,.66):
        for y in (-.62,.62):
            beam('Pata_inclinada',(x*1.12,y*1.1,.06),(x,y,1.32),.105,oak,[12,8,4][lod])
        beam('Montante_respaldo',(x,.65,1.25),(x,.85,2.82),.085,oak,[12,8,4][lod])
    box('Respaldo_curvo_suave',(0,.85,2.43),(1.53,.21,.78),teal,[.13,.07,0][lod],seg or 1)
    if lod<2:
        for x in (-.65,.65): beam('Travesano',(x,-.62,.62),(x,.62,.62),.05,edgewood,[8,6][lod])
    if lod==0:
        for x in (-.48,.48): cylinder('Boton_respaldo',(x,.725,2.43),.035,.022,brass,10).rotation_euler.x=math.pi/2

def sofa(lod):
    seg=[4,2,1][lod]; b=[.18,.12,.06][lod]
    box('Zocalo',(0,0,.5),(8.2,3.15,.38),edgewood,.07,seg)
    for x in (-3.55,3.55):
        for y in (-1.12,1.12): cylinder('Pata',(x,y,.25),.12,.5,brass,[12,8,6][lod])
    box('Base_tapizada',(0,0,.89),(8.1,3.1,.55),teal,b,seg)
    box('Respaldo',(0,1.12,2.05),(8.1,.8,2.15),teal,b,seg)
    for x in (-3.78,3.78): box('Brazo',(x,-.05,1.52),(.68,3.05,1.4),teal,b,seg)
    for i in range(3):
        x=(i-1)*2.22
        box('Cojin_asiento',(x,-.35,1.32),(2.15,2.0,.42),teal,b*.7,seg)
        o=box('Cojin_respaldo',(x,.62,2.12),(2.15,.42,1.36),teal,b*.7,seg); o.rotation_euler.x=-.12
    if lod<2:
        for x,material in ((-2.8,rust),(2.8,linen)):
            o=box('Cojin_decorativo',(x,.1,1.95),(.95,.42,1.0),material,.14,seg);o.rotation_euler.y=x*.07
    if lod==0:
        for i in range(3):
            x=(i-1)*2.22
            beam('Ribete_frontal',(x-1.0,-1.36,1.42),(x+1,-1.36,1.42),.017,linen,6)

def coffee(lod):
    seg=[3,1,1][lod]; b=[.09,.05,0][lod]
    box('Tablero',(0,0,1.16),(4.2,2.4,.18),oak,b,seg)
    box('Estante',(0,0,.4),(3.65,1.95,.1),edgewood,b*.3,seg)
    for x in (-1.65,1.65):
        for y in (-.82,.82): beam('Pata',(x*1.1,y*1.1,.04),(x,y,1.1),.085,dark,[12,8,4][lod])
    if lod==0:
        for y in (-.75,.75): beam('Travesano',(-1.65,y,.6),(1.65,y,.6),.035,brass,8)

def cabinet(lod):
    seg=[3,1,1][lod]; b=[.04,.025,0][lod]
    box('Casco',(0,0,.93),(7.2,1.65,1.35),oak,b,seg)
    for x in (-2.8,2.8):
        for y in (-.5,.5): cylinder('Pata',(x,y,.15),.10,.3,dark,[12,8,4][lod])
    for x in (-2.4,0,2.4):
        box('Frente_puerta',(x,-.85,.92),(2.30,.09,1.19),edgewood,b,seg)
        if lod<2: beam('Tirador',(x-.3,-.93,1.26),(x+.3,-.93,1.26),.024,brass,8)

def tv(lod):
    seg=[3,1,1][lod]; b=[.055,.025,0][lod]
    box('Carcasa',(0,.08,1.99),(6.2,.23,3.5),dark,b,seg)
    box('Cristal_pantalla',(0,-.049,2.02),(5.98,.025,3.25),screen,.018 if lod==0 else 0,seg)
    for x in (-2,2):
        beam('Pie_frontal',(x,-.63,.045),(x,0,.35),.06,dark,[12,8,4][lod])
        beam('Pie_trasero',(x,.5,.045),(x,0,.35),.06,dark,[12,8,4][lod])
    if lod==0:
        box('Indicador',(0,-.07,.33),(.06,.015,.025),brass)
        for x in (-1.3,0,1.3): box('Rejilla_trasera',(x,.21,2.1),(.5,.035,1.2),edgewood,.015,1)

def rug(lod):
    box('Tejido',(0,0,.035),(9,6,.05),rugmat,.018 if lod<2 else 0,1)
    if lod<2:
        for x in (-4.2,4.2): box('Cenefa',(x,0,.063),(.14,5.5,.004),rugline)
        for y in (-2.7,2.7): box('Cenefa',(0,y,.063),(8.5,.14,.004),rugline)
    if lod==0:
        for x in [i*.3 for i in range(-14,15)]:
            for y in (-3.06,3.06): beam('Fleco',(x,y-.06,.035),(x,y+.06,.035),.012,linen,4)

def lamp(lod):
    n=[32,16,8][lod]
    cylinder('Base',(0,0,.09),.56,.18,dark,n)
    cylinder('Mastil',(0,0,2.13),.065,4.05,brass,n)
    cylinder('Pantalla',(0,0,4.25),.8,.9,ivory,n,radius2=.5)
    if lod==0:
        cylinder('Remate',(0,0,4.74),.1,.12,brass,12)

def stairs(lod):
    # 28 rises * .25 = exactly 7.0. Two 2.8-wide flights, 2.4-wide central gap.
    n=[12,8,4][lod]
    for i in range(14):
        box('Escalon_ida',(1.4,(i+.5)*.44,(i+1)*.25-.1),(2.8,.44,.2),oak)
        box('Escalon_vuelta',(6.6,(13-i+.5)*.44,3.5+(i+1)*.25-.1),(2.8,.44,.2),oak)
    box('Descansillo',(4,7.08,3.4),(8,1.84,.2),oak)
    # Sloping structural stringers underneath both flights.
    for x in (.18,2.62): beam('Zanca_ida',(x,0,.05),(x,6.16,3.30),.12,dark,n)
    for x in (5.38,7.82): beam('Zanca_vuelta',(x,6.16,3.55),(x,0,6.80),.12,dark,n)
    for x in (.13,2.67):
        for i in range(0,14,2 if lod<2 else 4):
            y=(i+.5)*.44; z=(i+1)*.25
            beam('Barrote',(x,y,z),(x,y,z+1.35),.036,dark,n)
        beam('Pasamanos',(x,.22,1.6),(x,5.94,4.85),.065,edgewood,n)
    for x in (5.33,7.87):
        for i in range(0,14,2 if lod<2 else 4):
            y=(13-i+.5)*.44;z=3.5+(i+1)*.25
            beam('Barrote',(x,y,z),(x,y,z+1.35),.036,dark,n)
        beam('Pasamanos',(x,5.94,5.1),(x,.22,8.35),.065,edgewood,n)
    for x in (0.13,4,7.87): beam('Barrote_descanso',(x,7.9,3.5),(x,7.9,4.85),.04,dark,n)
    beam('Pasamanos_descanso',(.13,7.9,4.85),(7.87,7.9,4.85),.065,edgewood,n)

chairs=collection('CM_01_Sillas')
living=collection('CM_02_Salon_Objetos')
staircol=collection('CM_03_Escalera_7m')
lod1=collection('CM_90_LOD1_solo_exportar'); lod1.hide_render=True
lod2=collection('CM_91_LOD2_solo_exportar'); lod2.hide_render=True
models={}
for name,fn,col,loc in [
    ('Silla_Roble',chair,chairs,(5,2,0)),('Sofa_3Plazas',sofa,living,(16,2,0)),
    ('Mesa_Cafe',coffee,living,(23,2,0)),('Mueble_TV',cabinet,living,(16,-5,0)),
    ('TV_Pies',tv,living,(24,-5,0)),('Alfombra',rug,living,(16,-10,0)),
    ('Lampara_Pie',lamp,living,(23,-11,0)),('Escalera_U_7m',stairs,staircol,(6,-10,0))]:
    report['assets'][name]=[]
    shared_pivot=None
    for lod in range(3):
        current=col if lod==0 else lod1 if lod==1 else lod2
        parts=[];fn(lod);o=merge(name+'_LOD'+str(lod),shared_pivot);o.location=loc
        shared_pivot=list(o['ModelingPivot'])
        o['LOD']=lod;o['Asset']=name
        o.data.calc_loop_triangles()
        report['assets'][name].append({'lod':lod,'triangles':len(o.data.loop_triangles),'vertices':len(o.data.vertices)})
        if lod==0: models[name]=o

# Table remains untouched. LOD versions share exactly its existing local origin.
for lod,ratio,col in ((1,.48,lod1),(2,.20,lod2)):
    o=originals['Table'].copy();o.data=originals['Table'].data.copy();col.objects.link(o)
    o.name='Table_LOD'+str(lod)
    bpy.context.view_layer.objects.active=o
    # Temporarily show the collection so the modifier operator can run.
    col.hide_viewport=False
    mod=o.modifiers.new('LOD_reduction','DECIMATE');mod.ratio=ratio
    bpy.ops.object.modifier_apply(modifier=mod.name)
    o['LOD']=lod;o['Asset']='Table';o.data.calc_loop_triangles()
    report['assets'].setdefault('Table',[]).append({'lod':lod,'triangles':len(o.data.loop_triangles)})
lod1.hide_viewport=True
lod2.hide_viewport=True

show=collection('CM_04_Salon_Montado')
room=collection('CM_05_Arquitectura_Demo')
rig=collection('CM_99_Preview')

def instance(source,name,col,matrix):
    o=source.copy();o.data=source.data;col.objects.link(o);o.name=name;o.parent=None;o.matrix_world=matrix
    return o

def place_model(key,xy,angle=0,z=0):
    src=models[key]
    co=[v.co for v in src.data.vertices]
    centre=Vector(((min(v.x for v in co)+max(v.x for v in co))/2,
        (min(v.y for v in co)+max(v.y for v in co))/2,0))
    matrix=Matrix.Translation((30+xy[0],xy[1],z)) @ Matrix.Rotation(angle,4,'Z') @ Matrix.Translation(-centre)
    return instance(src,key+'_Demo',show,matrix)

for x in range(6):
    for y in range(5):
        instance(originals['Suelo'],f'Tile_{x}_{y}',room,Matrix.Translation((30+(x+1)*4,(y+1)*4,0)))
# Copy authored walls, retaining face material indices and import transforms.
for i in range(6):
    src=originals['Window wall'] if i in (1,4) else originals['Wall']
    m=src.matrix_world.copy();m.translation=(30+(i+1)*4,20,0)
    instance(src,'Pared_fondo_'+str(i),room,m)
    if i in (1,4):
        m=originals['Window'].matrix_world.copy();m.translation=(30+(i+1)*4,20,0)
        instance(originals['Window'],'Ventana_demo_'+str(i),room,m)
for i in range(5):
    src=originals['Wall']; base=src.matrix_world.copy();base.translation=(0,0,0)
    m=Matrix.Translation((30,(i+1)*4,0)) @ Matrix.Rotation(math.pi/2,4,'Z') @ base
    instance(src,'Pared_lateral_'+str(i),room,m)

instance(originals['Table'],'Table_Demo',show,Matrix.Translation((41,16,0)))
for x in (3.3,6.2,9.1):
    place_model('Silla_Roble',(x,11.05),math.pi)
    place_model('Silla_Roble',(x,17.25))
place_model('Sofa_3Plazas',(7,5.1),math.pi/2)
place_model('Alfombra',(12,5.1))
place_model('Mesa_Cafe',(12,5.1))
place_model('Mueble_TV',(17,5.1),-math.pi/2)
place_model('TV_Pies',(17,5.1),-math.pi/2,z=1.62)
place_model('Lampara_Pie',(4.4,1.35))
place_model('Escalera_U_7m',(20,15.7))
# Upper floor landing: same thin tile, exactly one wall-height above ground.
instance(originals['Suelo'],'Tile_llegada_Z7',room,Matrix.Translation((54,11.7,7)))

def camera(name,position,target,scale):
    data=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,data);rig.objects.link(o)
    o.location=position;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    data.type='ORTHO';data.ortho_scale=scale;data.lens=45
    return o

def area(name,pos,power,size,target):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    o=bpy.data.objects.new(name,data);rig.objects.link(o);o.location=pos
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()

cam=camera('CM_Camera_Salon',(72,-30,33),(42,10,2),39)
scene.camera=cam
area('CM_Key',(40,-5,22),6500,14,(42,8,0))
area('CM_Fill',(60,16,18),4500,12,(42,8,1))
area('CM_Rim',(27,20,16),3500,10,(40,10,2))
# Dedicated world prevents altering the original world datablock.
world=bpy.data.worlds.new('CM_Preview_World');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.32,.38,.46,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.45
scene.world=world
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
scene.render.filepath=str(OUT/'Salon_preview.png')
for screen_data in bpy.data.screens:
    for ar in screen_data.areas:
        if ar.type=='VIEW_3D':
            ar.spaces.active.region_3d.view_distance=40
            ar.spaces.active.region_3d.view_location=(42,10,2)
            ar.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
            ar.spaces.active.shading.type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT')
models['Silla_Roble'].select_set(True);bpy.context.view_layer.objects.active=models['Silla_Roble']
scene['CM_FloorPitch']=7.0
scene['CM_Notes']='Original meshes preserved. CM_90/91 hold hidden LOD variants. Floor pitch=7, not 7.02.'
(OUT/'LOD_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE), relative_remap=False)
bpy.ops.render.render(write_still=True)
print('CM_GENERATION_COMPLETE', json.dumps(report))
