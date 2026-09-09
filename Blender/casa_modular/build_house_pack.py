"""Blender 5.x: --background --threads 4 --python build_house_pack.py
Original simple game meshes. Standard Blender cube = 2 units.
Regenerating overwrites ONLY generated files inside this script's directory.
"""
import bpy
import bmesh
import math
import json
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parent
FBX = OUT / 'FBX'
FBX.mkdir(exist_ok=True)
ROOT = OUT.parent.parent
report = {'scale': {'default_cube': 2, 'person_height': 4, 'person_width': 2,
                    'wall_height': 8, 'floor_thickness': .4, 'floor_pitch': 8.4}, 'assets': {}}

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
# Inspect an existing wall without changing the original FBX/prefab.
old = ROOT / 'Assets/Original_assets/Walls/Incompletes/Wall_Incomplete.fbx'
if old.exists():
    bpy.ops.import_scene.fbx(filepath=str(old))
    report['existing_wall_reference'] = [
        {'name': o.name, 'location': list(o.location), 'dimensions': list(o.dimensions),
         'local_bounds': [list(o.bound_box[0]), list(o.bound_box[6])]}
        for o in bpy.context.selected_objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.cycles.use_denoising = True
scene.render.resolution_x = 1100
scene.render.resolution_y = 850
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.world.color = (.24, .24, .24)
scene.view_settings.view_transform = 'AgX'
bpy.context.preferences.filepaths.save_version = 0

def mat(name, rgb, metal=0, rough=.48, alpha=1):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, alpha)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*rgb, 1)
    p.inputs['Metallic'].default_value = metal
    p.inputs['Roughness'].default_value = rough
    p.inputs['Alpha'].default_value = alpha
    if alpha < 1:
        p.inputs['Transmission Weight'].default_value = .25
        if hasattr(m, 'surface_render_method'): m.surface_render_method = 'DITHERED'
    return m

plaster = mat('Warm_plaster', (.72, .68, .59))
wood = mat('Honey_wood_flat', (.33, .16, .07))
lightwood = mat('Light_wood_flat', (.57, .37, .19))
dark = mat('Graphite', (.035, .044, .053), rough=.34)
steel = mat('Metal_shared', (.38, .43, .48), .85, .25)
blue = mat('Deep_teal_fabric', (.045, .23, .25), rough=.8)
fabric = mat('Terracotta_fabric', (.46, .17, .10), rough=.85)
glass = mat('Glass_simple', (.48, .72, .77), rough=.12, alpha=.25)
screen = mat('Screen_off_blue', (.025, .065, .085), rough=.19)
leaves = mat('Foliage_sage', (.17, .32, .095), rough=.9)

assets = {}
pieces = []
asset_collection = None

def collection(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c

def move_to(o, c):
    for old_collection in list(o.users_collection): old_collection.objects.unlink(o)
    c.objects.link(o)

def begin(name):
    global asset_collection
    asset_collection = collection(name)
    pieces.clear()

def box(name, pos, size, material, bevel=.035, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material)
    if bevel:
        m = o.modifiers.new('Small edge bevel', 'BEVEL')
        m.width = min(bevel, min(size) * .24)
        m.segments = 1
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=m.name)
    move_to(o, asset_collection)
    pieces.append(o)
    return o

def beam(name, start, end, width, material, depth=None):
    a, b = Vector(start), Vector(end)
    o = box(name, (a+b)/2, (width, depth or width, (b-a).length), material, .015)
    o.rotation_euler = (b-a).to_track_quat('Z','Y').to_euler()
    return o

def cyl(name, pos, radius, depth, material, vertices=12, rot=(0,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=pos, rotation=rot)
    o = bpy.context.object
    o.name = name
    o.data.materials.append(material)
    move_to(o, asset_collection)
    pieces.append(o)
    return o

def join(name, origin=(0,0,0)):
    bpy.ops.object.select_all(action='DESELECT')
    for o in pieces: o.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    if len(pieces) > 1: bpy.ops.object.join()
    o = bpy.context.object
    o.name = name
    scene.cursor.location = origin
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    # Merge duplicate material slots introduced by joining primitives.
    slots = list(o.data.materials)
    unique = list(dict.fromkeys(slots))
    indices = [unique.index(slots[p.material_index]) for p in o.data.polygons]
    o.data.materials.clear()
    for m in unique: o.data.materials.append(m)
    for p, index in zip(o.data.polygons, indices): p.material_index = index
    pieces.clear()
    return o

def finish(name, objects, note):
    # All FBXs are exported at their assembly origin, before gallery arrangement.
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=str(FBX / (name+'.fbx')), use_selection=True,
        object_types={'MESH','EMPTY'}, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, use_mesh_modifiers=True, bake_anim=False, add_leaf_bones=False)
    triangles = 0
    for o in objects:
        if o.type == 'MESH':
            o.data.calc_loop_triangles()
            triangles += len(o.data.loop_triangles)
        o['usage'] = note
    report['assets'][name] = {'triangles': triangles, 'objects': [o.name for o in objects], 'notes': note}
    assets[name] = (asset_collection, objects)

def single(name, note, origin=(0,0,0)):
    o = join(name, origin)
    o.location = (0,0,0)
    finish(name, [o], note)
    return o

# --- Domestic furniture: original silhouettes, no manufacturer marks ---
begin('TV_Retro')
box('Cream housing', (0, .12, 1.25), (2.8, 1.7, 2.05), plaster, .12)
box('Rear taper block', (0, .98, 1.25), (2.15, .6, 1.5), dark, .12)
box('Bezel', (-.20, -.76, 1.28), (2.20, .14, 1.72), dark, .075)
box('Convex-looking glass', (-.20, -.85, 1.30), (1.94, .11, 1.43), screen, .09)
for z in (.85, 1.5): cyl('Controls', (1.08,-.81,z), .12,.1,steel,rot=(math.pi/2,0,0))
for z in (.62,.72,.82,.92): box('Speaker slit', (.92,-.755,z),(.18,.015,.028),dark,0)
for x in (-1,1): box('Feet',(x,0,.12),(.25,1.15,.24),dark)
single('TV_Retro', 'Origin floor centre. Front is -Y in Blender. Stylized CRT, no antennas.')

begin('TV_Modern_Stand')
box('Slim housing',(0,0,1.65),(3.6,.18,2.10),dark,.045)
box('Screen',(0,-.105,1.65),(3.38,.02,1.90),screen,.025)
box('Lower accent',(0,-.12,.66),(3.28,.025,.06),steel,.008)
for x in (-1.16,1.16):
    beam('Splayed stand',(x,0,.64),(x*1.14,-.43,.08),.1,steel)
    beam('Rear foot',(x,0,.64),(x*.94,.32,.08),.1,steel)
single('TV_Modern_Stand','Origin under feet. Stand included in one static mesh.')

begin('TV_Modern_Wall_Large')
box('Wall plate',(0,.01,0),(1.4,.08,.85),steel)
box('Thin display',(0,-.17,0),(5.6,.22,3.20),dark,.035)
box('Large screen',(0,-.291,0),(5.40,.018,3),screen,.02)
single('TV_Modern_Wall_Large','Origin at centre of rear wall mount. Mount at desired viewing height.')

begin('Coffee_Table')
box('Chamfered top',(0,0,1.10),(3.6,2.35,.20),lightwood,.09)
box('Floating shelf',(0,0,.38),(2.75,1.8,.10),wood)
for x in (-1.40,1.40):
    for y in (-.8,.8): beam('Taper-like angled leg',(x,y,1.0),(x*1.10,y*1.14,.04),.14,wood)
single('Coffee_Table','Origin floor centre. Height 1.2, sized for the 4-unit character.')

for style in range(3):
    name = ['Chair_01_Basic','Chair_02_Comfort','Chair_03_Elegant'][style]
    begin(name)
    seat_mat = [lightwood,fabric,blue][style]
    leg_mat = wood if style < 2 else steel
    for x in (-.98,.98):
        for y in (-.85,.85): beam('Leg',(x,y,1.06),(x*1.10,y*1.10,.05),.15 if style<2 else .095,leg_mat)
    box('Seat',(0,0,1.2),(2.35,2.15,.25),seat_mat,.065)
    for x in (-1,1): beam('Back upright',(x,.88,1.1),(x,.99,2.72),.13,leg_mat)
    if style==0:
        for z in (1.85,2.40): box('Open back slat',(0,.99,z),(2.16,.13,.24),lightwood,.025)
    else:
        box('Upholstered back',(0,.99,2.05),(2.18,.32,1.35),seat_mat,.10,rot=(math.radians(-5),0,0))
        if style==2:
            for x in (-1.2,1.2):
                beam('Arm support',(x,-.55,1.22),(x,-.55,1.80),.07,steel)
                box('Arm cap',(x,.05,1.85),(.16,1.55,.12),wood,.04)
            box('Back inset',(0,.806,2.08),(1.75,.05,.91),blue,.045)
    single(name,'Origin floor centre. Seat width 2.35; three distinct comfort/detail tiers.')

# --- Architecture: exact snap endpoints; no bevel at module seams ---
H, L, T, FLOOR = 8., 4., .30, 8.4
def wallbox(name,pos,size): return box(name,pos,size,plaster,0)
def trim_segment(x0,x1,y=0):
    box('Skirting',((x0+x1)/2,y-.165,.14),(x1-x0,.04,.28),wood,0)

begin('Wall_Straight_4x8')
wallbox('Wall',(2,0,4),(4,T,8)); trim_segment(0,4)
single('Wall_Straight_4x8','Start (0,0,0), end (4,0,0). Height 8. Thickness centred on Y=0.')

begin('Wall_Window_4x8')
wallbox('Sill wall',(2,0,1.5),(4,T,3))
wallbox('Above window',(2,0,7.1),(4,T,1.8))
for x in (.275,3.725): wallbox('Jamb',(x,0,4.6),(.55,T,3.2))
trim_segment(0,4)
single('Wall_Window_4x8','Same origin as straight wall. Opening X=.55..3.45, Z=3..6.2. Window separate.')

begin('Window_Insert')
for x in (.61,3.39): box('Vertical frame',(x,0,4.6),(.12,.24,3.2),wood,.012)
for z in (3.06,6.14): box('Horizontal frame',(2,0,z),(2.9,.24,.12),wood,.012)
box('Mullion',(2,0,4.6),(.07,.20,3),wood,.01)
window_frame=join('Window_Frame')
box('Glass pane',(2,.02,4.6),(2.65,.025,2.96),glass,0)
window_glass=join('Window_Glass')
finish('Window_Insert',[window_frame,window_glass],'Origin equals Wall_Window. Place at identical transform. Glass is a separate mesh.')

begin('Wall_Glazing_4x8')
for x in (.065,3.935): box('Frame',(x,0,4),(.13,T,8),steel,0)
for z in (.065,7.935): box('Frame',(2,0,z),(4,T,.13),steel,0)
box('Mullion',(2,0,4),(.08,T,7.8),steel,0)
frame=join('Glazing_Frame')
for x in (1.03,2.97): box('Pane',(x,0,4),(1.79,.03,7.74),glass,0)
panes=join('Glazing_Glass')
finish('Wall_Glazing_4x8',[frame,panes],'Start (0,0,0), end (4,0,0). Glass separate for Unity transparent material.')

begin('Wall_Corner_Square')
wallbox('Leg X',(1,0,4),(2,T,8))
wallbox('Leg Y',(2,1,4),(T,2,8))
single('Wall_Corner_Square','Origin start (0,0,0), end (2,2,0). Entry tangent +X, exit +Y. Rotate 90 degrees to connect next straight.')

begin('Wall_Corner_Round')
verts=[]
for i in range(13):
    a=-math.pi/2+i*math.pi/24
    for radius,z in [(2-T/2,0),(2+T/2,0),(2-T/2,H),(2+T/2,H)]:
        verts.append((radius*math.cos(a),2+radius*math.sin(a),z))
faces=[]
for i in range(12):
    a=i*4; b=a+4
    faces.extend([(a,b,b+2,a+2),(a+1,a+3,b+3,b+1),(a,a+1,b+1,b),(a+2,b+2,b+3,a+3)])
faces.extend([(0,2,3,1),(48,49,51,50)])
mesh=bpy.data.meshes.new('Quarter_arc_12_segments'); mesh.from_pydata(verts,[],faces); mesh.update()
o=bpy.data.objects.new('Rounded wall',mesh); asset_collection.objects.link(o); o.data.materials.append(plaster); pieces.append(o)
single('Wall_Corner_Round','Quarter circle R=2, 12 segments. Same endpoints/tangents as square corner; origin at first lower endpoint.')

begin('Wall_Door_4x8')
for x in (.3,3.7): wallbox('Door jamb wall',(x,0,2.4),(.6,T,4.8))
wallbox('Lintel',(2,0,6.4),(4,T,3.2))
trim_segment(0,.6); trim_segment(3.4,4)
single('Wall_Door_4x8','Door opening X=.6..3.4, Z=0..4.8. Same wall origin; no geometry in opening.')

begin('Door_Frame')
for x in (.66,3.34): box('Frame upright',(x,0,2.4),(.12,.38,4.8),wood,.014)
box('Frame top',(2,0,4.74),(2.8,.38,.12),wood,.014)
single('Door_Frame','Same origin as Wall_Door. Clear width 2.56 and clear height 4.68.')

for variant in ('Plain','Panelled'):
    name='Door_Leaf_'+variant
    begin(name)
    box('Leaf',(2,0,2.36),(2.52,.12,4.64),lightwood if variant=='Plain' else wood,.018)
    if variant=='Panelled':
        for z,h in ((1.25,1.6),(3.45,1.7)):
            for y in (-.075,.075): box('Raised panel',(2,y,z),(1.9,.035,h),lightwood,.03)
    for y in (-.12,.12):
        cyl('Handle rose',(3.02,y,2.25),.095,.06,steel,rot=(math.pi/2,0,0))
        box('Lever',(2.89,y*1.4,2.25),(.32,.07,.07),steel,.018)
    single(name,'HINGE origin. Place at wall/frame local (0.74,0,0); rotate around local Z in Blender / Y in Unity.',origin=(.74,0,0))

# U staircase: two flights around an unobstructed centre lift well.
# Floor pitch 8.4 = 8 wall + .4 slab. Width 9, flight width 2.6.
begin('Stairs_U_Floor')
width, flight, landing, run, half = 9.,2.6,2.8,7.8,4.2
for i in range(12):
    z=(i+1)*.35
    box('Left step',(-3.2,landing+(i+.5)*.65,z-.10),(flight,.65,.20),plaster,0)
    box('Right step',(3.2,landing+(11-i+.5)*.65,half+z-.10),(flight,.65,.20),plaster,0)
# Sloping structural stringers; stair volumes remain open/lightweight below.
for x in (-4.32,-2.08): beam('Stringer',(x,landing,.12),(x,landing+run,4.02),.18,steel)
for x in (2.08,4.32): beam('Stringer',(x,landing+run,4.30),(x,landing,8.20),.18,steel)
box('Half landing',(0,landing+run+landing/2,half-.2),(width,landing,.4),plaster,0)
box('Arrival landing',(0,landing/2,FLOOR-.2),(width,landing,.4),plaster,0)
for x in (-4.40,-2.0):
    beam('Left handrail',(x,landing,2.15),(x,landing+run,6.35),.075,steel)
    for i in (0,3,6,9,11):
        y=landing+(i+.5)*.65; z=(i+1)*.35
        beam('Baluster',(x,y,z),(x,y,z+2),.055,steel)
for x in (2.,4.40):
    beam('Right handrail',(x,landing+run,6.35),(x,landing,10.55),.075,steel)
    for i in (0,3,6,9,11):
        y=landing+(11-i+.5)*.65; z=half+(i+1)*.35
        beam('Baluster',(x,y,z),(x,y,z+2),.055,steel)
beam('Landing rear rail',(-4.4,13.3,6.2),(4.4,13.3,6.2),.075,steel)
for x in (-4.4,-2.2,0,2.2,4.4): beam('Landing post',(x,13.3,4.2),(x,13.3,6.2),.055,steel)
single('Stairs_U_Floor','Origin front-centre ground. Repeat every Z=8.4. Open well X=-1.9..1.9,Y=2.8..10.6. Place lift module at (0,2.8,0). Visual steps; use ramp colliders in Unity.')

begin('Stairs_Start_Landing')
box('Ground landing',(0,1.4,-.2),(9,2.8,.4),plaster,0)
single('Stairs_Start_Landing','Use once at the base of the staircase. Top surface Z=0.')

begin('Lift_Shaft_Floor')
for x in (-1.84,1.84): box('Side shaft wall',(x,1.9,4.2),(.12,3.8,8.4),plaster,0)
box('Back shaft wall',(0,3.74,4.2),(3.56,.12,8.4),plaster,0)
for x in (-1.63,1.63): box('Front jamb',(x,0,2.45),(.30,.16,4.9),steel,0)
box('Door header',(0,0,6.65),(3.56,.16,3.5),plaster,0)
for x in (-1.65,1.65): box('Car guide',(x,2.9,4.2),(.07,.11,8.4),steel,0)
single('Lift_Shaft_Floor','Repeat at Z=8.4*n. Open top/bottom: no floor blocking the car. Place at stairs (0,2.8,0). Door opening 2.96 wide, 4.9 high.')

begin('Lift_Landing_Doors')
for x in (-.735,.735):
    box('Door',(x,-.055,2.44),(1.45,.07,4.82),steel,.014)
    o=join('LandingDoor_Left' if x<0 else 'LandingDoor_Right',origin=(x,0,0))
    if x<0: left=o
    else: right=o
finish('Lift_Landing_Doors',[left,right],'Place at each shaft floor origin. Separate leaves slide in +/-X. No animation or elevator logic included.')

begin('Lift_Cabin')
box('Floor',(0,1.88,-.12),(3.30,3.36,.24),steel,0)
box('Back',(0,3.5,2.5),(3.30,.12,5),steel,.02)
for x in (-1.59,1.59): box('Side',(x,1.88,2.5),(.12,3.36,5),steel,.02)
box('Ceiling',(0,1.88,5.1),(3.30,3.36,.20),steel,.02)
box('Cabin floor insert',(0,1.88,.014),(3.02,3.10,.025),dark,0)
beam('Rear handrail',(-1.40,3.36,2.2),(1.40,3.36,2.2),.06,wood)
box('Control panel',(1.51,.85,2.65),(.03,.36,.65),dark,.02)
cab=join('Cabin_Body')
doors=[]
for x in (-.735,.735):
    box('Cabin leaf',(x,.22,2.43),(1.45,.07,4.82),steel,.014)
    doors.append(join('CabinDoor_Left' if x<0 else 'CabinDoor_Right',origin=(x,0,0)))
finish('Lift_Cabin',[cab]+doors,'Origin at cabin floor/front. Align with shaft origin. Animate entire cabin vertically; door leaves slide separately in X. No floor count baked into cabin.')

begin('Lift_Roof_Cap')
box('Cap',(0,1.9,.12),(3.8,3.8,.24),plaster,0)
single('Lift_Roof_Cap','Use once above the highest shaft module, allowing a full module of overhead clearance.')

begin('Tree_Stylized')
cyl('Trunk',(0,0,2.45),.32,4.9,wood,10)
for a in (0,2.1,4.2): beam('Branch',(0,0,3),(math.cos(a)*1.6,math.sin(a)*1.6,5.4),.20,wood)
for pos,scale in [((0,0,6.8),(2.7,2.5,2.3)),((1.65,0,5.65),(1.9,1.65,1.8)),((-1.3,1,5.7),(1.9,1.7,1.8)),((-.7,-1.4,5.4),(1.75,1.7,1.65))]:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=pos)
    o=bpy.context.object; o.scale=scale; o.data.materials.append(leaves); move_to(o,asset_collection); pieces.append(o)
single('Tree_Stylized','Low-poly tree, origin at ground. Solid foliage clusters, no alpha cards or textures.')

# Validation before gallery: all mesh sizes, triangles and nondegenerate geometry.
bpy.context.view_layer.update()
for name,(c,objects) in assets.items():
    for o in objects:
        assert all(abs(s-1)<1e-5 for s in o.scale), (name,'non-unit scale')
        assert all(len(p.vertices)>=3 for p in o.data.polygons)
        assert all(p.area>1e-10 for p in o.data.polygons), (name,'degenerate face')
    report['assets'][name]['bounds'] = [
        [min((o.matrix_world @ Vector(v))[axis] for o in objects for v in o.bound_box) for axis in range(3)],
        [max((o.matrix_world @ Vector(v))[axis] for o in objects for v in o.bound_box) for axis in range(3)]]
report['total_triangles'] = sum(a['triangles'] for a in report['assets'].values())
(OUT/'asset_report.json').write_text(json.dumps(report,indent=2))

# Gallery placement is independent of the exported FBX coordinates.
positions = {
 'TV_Retro':(0,0,0),'TV_Modern_Stand':(4.5,0,0),'TV_Modern_Wall_Large':(10,0,3.1),
 'Coffee_Table':(0,-5,0),'Chair_01_Basic':(4.5,-5,0),'Chair_02_Comfort':(8,-5,0),'Chair_03_Elegant':(11.5,-5,0),
 'Wall_Straight_4x8':(0,10,0),'Wall_Window_4x8':(5.5,10,0),'Window_Insert':(5.5,10,0),
 'Wall_Glazing_4x8':(11,10,0),'Wall_Corner_Square':(17,10,0),'Wall_Corner_Round':(21,10,0),
 'Wall_Door_4x8':(26,10,0),'Door_Frame':(26,10,0),'Door_Leaf_Plain':(26.74,10,0),'Door_Leaf_Panelled':(32,10,0),
 'Stairs_U_Floor':(43,0,0),'Stairs_Start_Landing':(43,0,0),'Lift_Shaft_Floor':(43,2.8,0),
 'Lift_Landing_Doors':(43,2.8,0),'Lift_Cabin':(43,2.8,0),'Lift_Roof_Cap':(43,2.8,25.2),
 'Tree_Stylized':(16,0,0)}
for name,(c,objects) in assets.items():
    offset=Vector(positions[name])
    for o in objects: o.location += offset

demo=collection('DEMO_two_floors_not_exported')
for name in ('Stairs_U_Floor','Lift_Shaft_Floor','Lift_Landing_Doors'):
    for o in assets[name][1]:
        clone=o.copy(); clone.data=o.data; demo.objects.link(clone); clone.location.z += FLOOR
        clone.name = 'Demo_upper_'+o.name
# Shaft overhead above the uppermost stop; prevents roof clipping the cabin.
for o in assets['Lift_Shaft_Floor'][1]:
    clone=o.copy(); clone.data=o.data; demo.objects.link(clone); clone.location.z += FLOOR*2
    clone.name='Demo_overhead_'+o.name

reference=collection('REFERENCE_person_2_cubes_not_exported')
for z in (1,3):
    bpy.ops.mesh.primitive_cube_add(size=2, location=(-4,-1,z))
    o=bpy.context.object; o.name='Reference_person_cube'; move_to(o,reference)
    o.data.materials.append(blue)

studio=collection('STUDIO_not_exported')
bpy.ops.object.camera_add()
camera=bpy.context.object; move_to(camera,studio); scene.camera=camera
camera.data.type='ORTHO'
def aim(o,target): o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
lights=[]
for pos,power,size in [((6,-10,18),5000,12),((-8,0,14),3500,10),((8,12,18),6000,12)]:
    bpy.ops.object.light_add(type='AREA',location=pos)
    o=bpy.context.object; move_to(o,studio); o.data.energy=power; o.data.shape='DISK'; o.data.size=size
    aim(o,(5,0,1)); lights.append(o)

def preview(file,target,offset,ortho,visible):
    for name,(c,objects) in assets.items(): c.hide_render = name not in visible
    demo.hide_render = 'Stairs_U_Floor' not in visible
    reference.hide_render = 'TV_Retro' not in visible
    camera.location=Vector(target)+Vector(offset); camera.data.ortho_scale=ortho; aim(camera,target)
    for i,o in enumerate(lights):
        o.location=Vector(target)+Vector([(8,-12,18),(-10,-4,13),(4,10,20)][i])
        aim(o,target)
        o.data.energy=[5500,3800,6500][i] * (2 if ortho>25 else 1)
    scene.render.filepath=str(OUT/file)
    bpy.ops.render.render(write_still=True)

furniture=list(assets)[:7]+['Tree_Stylized']
preview('01_furniture.png',(5,-1,2.1),(15,-25,19),25,furniture)
preview('02_walls_and_doors.png',(15,10,3.5),(12,-34,19),42,[n for n in assets if n.startswith(('Wall_','Window_','Door_'))])
preview('03_stairs_and_lift.png',(43,6,10),(24,-32,27),33,[n for n in assets if n.startswith(('Stairs_','Lift_'))])
for c,objects in assets.values(): c.hide_render=False
demo.hide_render=False; reference.hide_render=False
scene.cursor.location=(0,0,0)
for screen_layout in bpy.data.screens:
    for area in screen_layout.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=45
            area.spaces.active.region_3d.view_location=(20,4,5)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Casa_Modular.blend'))
print('PACK_DONE', len(assets),'assets;',report['total_triangles'],'triangles')
