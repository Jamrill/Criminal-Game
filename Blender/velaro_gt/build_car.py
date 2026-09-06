"""Velaro GT: original game coupe. Run with Blender 5.1 --background --python."""
from pathlib import Path
import sys, math, json, argparse
import bpy, bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
args = argparse.ArgumentParser()
args.add_argument('--preview-only', action='store_true')
args.add_argument('--samples', type=int, default=48)
opts = args.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT = HERE / 'exports'
OUT.mkdir(exist_ok=True, parents=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for col in list(bpy.data.collections):
    if col.name != 'Collection':
        bpy.data.collections.remove(col)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'CYCLES'
scene.cycles.samples = opts.samples
scene.cycles.use_denoising = True
scene.render.resolution_x = 1500
scene.render.resolution_y = 1050
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.world.color = (.22,.22,.22)
car = bpy.data.collections.new('VELARO GT | Game geometry')
scene.collection.children.link(car)
studio = bpy.data.collections.new('STUDIO | Not exported')
scene.collection.children.link(studio)

def material(name, color, metallic=0, rough=.45, emission=0, alpha=1):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color,alpha)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color,alpha)
    p.inputs['Metallic'].default_value = metallic
    p.inputs['Roughness'].default_value = rough
    p.inputs['Alpha'].default_value = alpha
    if emission:
        p.inputs['Emission Color'].default_value = (*color,1)
        p.inputs['Emission Strength'].default_value = emission
    if alpha < 1:
        p.inputs['Transmission Weight'].default_value = .2
        p.inputs['IOR'].default_value = 1.45
        m.surface_render_method = 'DITHERED'
    if metallic > .5:
        p.inputs['Coat Weight'].default_value = .32
        p.inputs['Coat Roughness'].default_value = .18
    return m

mats = {
 'paint': material('Paint_Metallic', (.018,.22,.32), .78,.26),
 'leather': material('Leather_Tan', (.32,.14,.055),0,.63),
 'dark': material('Trim_Dark', (.016,.021,.026),.15,.48),
 'metal': material('Alloy_Metal', (.40,.45,.49),.85,.26),
 'glass': material('Glass_Tinted', (.12,.22,.27),.10,.12,alpha=.26),
 'light': material('LED_White', (.7,.88,1),.15,.19,3),
 'red': material('LED_Red', (.52,.012,.02),.22,.3,.6),
 'rubber': material('Tire_Rubber', (.011,.014,.017),0,.78),
 'screen': material('Display', (.028,.20,.27),.2,.3,.4),
}

def link_obj(obj, collection=car):
    for old in list(obj.users_collection): old.objects.unlink(obj)
    collection.objects.link(obj)
    return obj

def mesh(name, verts, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data); bm.free()
    ob = bpy.data.objects.new(name,data); car.objects.link(ob)
    if mat: data.materials.append(mat)
    for p in data.polygons: p.use_smooth=True
    return ob

def box(name,loc,size,mat,bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    ob=bpy.context.object; ob.name=name; ob.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    ob.data.materials.append(mat); link_obj(ob)
    if bevel >= .008:
        mod=ob.modifiers.new('Soft machined edges','BEVEL'); mod.width=bevel; mod.segments=2 if bevel>=.035 else 1
        ob.modifiers.new('Corner normals','WEIGHTED_NORMAL')
    return ob

def cylinder(name,loc,radius,depth,mat,vertices=16,axis='Z'):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=loc)
    ob=bpy.context.object; ob.name=name
    if axis=='X': ob.rotation_euler[1]=math.pi/2
    if axis=='Y': ob.rotation_euler[0]=math.pi/2
    ob.data.materials.append(mat); link_obj(ob)
    for p in ob.data.polygons: p.use_smooth=(len(p.vertices)==4)
    return ob

def tube(name,points,radius,mat,sides=6):
    vs=[]; pts=[Vector(p) for p in points]
    for i,p in enumerate(pts):
        t=(pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized()
        ref=Vector((0,0,1)) if abs(t.z)<.9 else Vector((0,1,0))
        u=t.cross(ref).normalized(); v=t.cross(u).normalized()
        for j in range(sides):
            a=math.tau*j/sides
            vs.append(tuple(p+radius*(u*math.cos(a)+v*math.sin(a))))
    fs=[]
    for i in range(len(pts)-1):
        for j in range(sides): fs.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
    fs.extend([tuple(reversed(range(sides))),tuple((len(pts)-1)*sides+j for j in range(sides))])
    return mesh(name,vs,fs,mat)

def grid(name, rows,mat, thickness=0):
    n=len(rows[0]); fs=[]
    for j in range(len(rows)-1):
        for i in range(n-1): fs.append((j*n+i,j*n+i+1,(j+1)*n+i+1,(j+1)*n+i))
    ob=mesh(name,[p for r in rows for p in r],fs,mat)
    if thickness:
        mod=ob.modifiers.new('Panel thickness','SOLIDIFY'); mod.thickness=thickness; mod.offset=-1
    return ob

def interp(y, vals):
    for (a,x),(b,z) in zip(vals,vals[1:]):
        if a<=y<=b: return x+(z-x)*(y-a)/(b-a)
    return vals[0][1] if y<vals[0][0] else vals[-1][1]

def width(y):
    return interp(y,[(-2.43,.77),(-2.2,.88),(-1.45,.95),(-.65,.91),(.1,.91),(.97,.96),(1.45,.98),(2.15,.93),(2.43,.80)])

def shoulder(y):
    return interp(y,[(-2.43,.65),(-2.20,.78),(-1.45,.86),(-.65,.89),(.1,.91),(.97,.94),(1.45,.90),(2.15,.81),(2.43,.72)])

def side_x(y,z):
    return width(y)-.075*((z-.65)/.65)**2

root=bpy.data.objects.new('Velaro_GT',None);car.objects.link(root)
root['design']='Original stylized coupe inspired by provided orthographic reference. No brand affiliation.'
root['dimensions_m']='Approximately 5.05 L x 2.22 W including mirrors x 1.38 H'
root['forward_axis']='Blender -Y; Unity +Z'

def pivot(name,loc):
    p=bpy.data.objects.new(name,None);car.objects.link(p);p.location=loc;p.parent=root
    p.empty_display_type='PLAIN_AXES';p.empty_display_size=.15
    return p

hinges={
 'Door_L':pivot('Door_L',(-side_x(-.625,.89),-.625,.89)),
 'Door_R':pivot('Door_R',(side_x(-.625,.89),-.625,.89)),
 'Hood':pivot('Hood',(0,-.685,.885)),
 'Trunk':pivot('Trunk',(0,1.435,.94)),
}

def parent_world(ob,parent):
    world=ob.matrix_world.copy();ob.parent=parent;ob.matrix_world=world

def attach_new(before,parent):
    for ob in set(car.objects)-before:
        if ob.parent is None: parent_world(ob,parent)

# Continuous wheel-cut quarter panels, with no hidden body across door apertures.
for sign,tag in [(-1,'L'),(1,'R')]:
    for ya,yb,axle,label in [(-2.43,-.65,-1.40,'Front'),(.985,2.43,1.45,'Rear')]:
        ys=sorted(set([ya,yb]+[ya+(yb-ya)*i/38 for i in range(39)]+[axle+v for v in [-.425,-.41,.41,.425] if ya<axle+v<yb]))
        rows=[]
        for y in ys:
            dy=abs(y-axle)
            low=.23
            if dy<.425:
                low=.36+math.sqrt(max(0,.425**2-dy**2))
            elif dy<.445: low=.36
            high=shoulder(y)
            rows.append([(sign*side_x(y,low+(high-low)*k/3),y,low+(high-low)*k/3) for k in range(4)])
        grid(f'{label}_quarter_{tag}',rows,mats['paint'],.016)
        # Top fender runs beside separate bonnet / deck lid.
        rows=[]
        for y in ys:
            z=shoulder(y)
            rows.append([(sign*x,y,z+.025*math.sin(math.pi*t)) for t,x in [(0,.665),(.4,.77),(.8,width(y)-.025),(1,side_x(y,z))]])
        grid(f'{label}_upper_fender_{tag}',rows,mats['paint'],.015)
        arch=[]
        for i in range(25):
            a=math.pi*i/24
            y=axle-.425*math.cos(a);z=.36+.425*math.sin(a)
            arch.append((sign*(side_x(y,z)+.004),y,z))
        tube(f'{label}_wheelarch_lip_{tag}',arch,.013,mats['paint'],6)
        # Black inside wheel arch liner, following the actual opening.
        grid(f'{label}_wheelwell_{tag}',[[(sign*x,p[1],p[2]-.012) for x in [.73,abs(p[0])-.008]] for p in arch],mats['rubber'])
    box('Sill_'+tag,(sign*.855,.16,.25),(.10,1.64,.12),mats['paint'],.035)
    box('Sill_insert_'+tag,(sign*.903,.14,.224),(.014,1.47,.037),mats['dark'],.01)

# Front and rear rounded fascia: broad sculpted surfaces around the grille and lamps.
for front in [True,False]:
    ys=[-2.43,-2.37,-2.23] if front else [2.23,2.37,2.43]
    rows=[]
    for y in ys:
        w=width(y);h=shoulder(y)
        cross=[(-w,.30),(-w*.99,.52),(-w*.92,h-.025),(-w*.62,h+.008),(0,h+.028),(w*.62,h+.008),(w*.92,h-.025),(w*.99,.52),(w,.30),(w*.84,.20),(0,.18),(-w*.84,.20),(-w,.30)]
        rows.append([(x,y,z) for x,z in cross])
    grid('Front_fascia' if front else 'Rear_fascia',rows,mats['paint'],.018)
    y=ys[0] if front else ys[-1]
    outline=[(x,y,z) for x,z in [(-width(y),.30),(-width(y)*.99,.52),(-width(y)*.92,shoulder(y)-.025),(-.5,shoulder(y)+.015),(0,shoulder(y)+.028),(.5,shoulder(y)+.015),(width(y)*.92,shoulder(y)-.025),(width(y)*.99,.52),(width(y),.30),(.68,.20),(-.68,.20)]]
    mesh('Front_face' if front else 'Rear_face',outline,[tuple(range(len(outline)))],mats['paint'])

# Bumper inserts sit on the forward-most surface. Original grille / light signature.
box('Grille_deep_recess',(0,-2.443,.405),(1.21,.027,.30),mats['dark'],.115)
for i in range(15):
    x=-.53+i*.076
    box('Grille_vertical_blade',(x,-2.467,.413),(.018,.027,.235),mats['metal'],.005)
tube('Front_splitter',[(-.80,-2.34,.20),(-.64,-2.49,.18),(0,-2.50,.17),(.64,-2.49,.18),(.80,-2.34,.20)],.025,mats['dark'])
for sign in [-1,1]:
    lamp=box('Headlight_housing',(sign*.622,-2.450,.628),(.266,.037,.071),mats['dark'],.018)
    for z in [.617,.640]:
        tube('Front_LED_signature',[(sign*.514,-2.474,z),(sign*.62,-2.476,z),(sign*.733,-2.467,z+.004)],.006,mats['light'])
    box('Front_brake_duct',(sign*.713,-2.416,.322),(.16,.033,.12),mats['dark'],.045)
    # Side vents behind the front wheel.
    for j in range(3):
        y=-.91+j*.071
        box('Fender_gill',(sign*(width(y)+.003),y,.76),(.018,.042,.07),mats['dark'],.013)
    tube('Rear_LED_signature',[(sign*.24,2.445,.677),(sign*.66,2.45,.676),(sign*.82,2.405,.704)],.017,mats['red'])
    tube('Rear_LED_lower',[(sign*.32,2.447,.642),(sign*.67,2.446,.642),(sign*.79,2.411,.661)],.007,mats['red'])
    box('Rear_reflector',(sign*.75,2.432,.325),(.17,.025,.026),mats['red'],.01)
box('Rear_diffuser',(0,2.445,.259),(1.30,.09,.16),mats['dark'],.035)
for x in [-.54,-.27,0,.27,.54]:
    box('Diffuser_fin',(x,2.395,.196),(.023,.26,.085),mats['dark'],.009)
box('Rear_plate_backing',(0,2.455,.486),(.42,.03,.12),mats['dark'],.014)
box('Front_badge',(0,-2.482,.596),(.075,.012,.035),mats['metal'],.012)

# Cabin structure leaves two full door openings.
box('Cabin_floor',(0,.40,.296),(1.57,2.00,.062),mats['dark'],.015)
box('Firewall',(0,-.657,.54),(1.48,.048,.43),mats['dark'],.018)
box('Rear_seat_bulkhead',(0,1.37,.56),(1.51,.045,.50),mats['dark'],.015)
for sign,tag in [(-1,'L'),(1,'R')]:
    tube('A_pillar_'+tag,[(sign*.847,-.655,.88),(sign*.79,-.37,1.06),(sign*.699,.055,1.305)],.038,mats['paint'],8)
    tube('Roof_rail_'+tag,[(sign*.699,.055,1.305),(sign*.715,.40,1.35),(sign*.692,.88,1.30)],.035,mats['paint'],8)
    # Broad C pillar behind quarter glass.
    verts=[(sign*.693,.81,1.303),(sign*.72,.93,1.28),(sign*.916,1.48,.916),(sign*.906,1.21,.936)]
    p=mesh('C_pillar_'+tag,verts,[(0,1,2,3)],mats['paint'])
    mod=p.modifiers.new('Pillar thickness','SOLIDIFY');mod.thickness=.045
    pts=[(sign*.73,.84,1.257),(sign*.867,1.19,.969),(sign*.88,.998,.949),(sign*.758,.757,1.241)]
    quarter_glass=mesh('Rear_quarter_glass_'+tag,pts,[(0,1,2,3)],mats['glass'])
    if quarter_glass.data.polygons[0].normal.x*sign < 0:
        bm=bmesh.new();bm.from_mesh(quarter_glass.data)
        bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(quarter_glass.data);bm.free()
    tube('Quarter_glass_trim_'+tag,pts+[pts[0]],.013,mats['dark'],6)
    tube('B_pillar_'+tag,[(sign*.89,.978,.90),(sign*.758,.756,1.263)],.026,mats['dark'],6)

# Curved roof and large windscreens, low resolution quads.
roofrows=[]
for j in range(11):
    t=j/10;y=.055+.845*t
    w=.698+.019*math.sin(math.pi*t)
    z=1.308+.035*math.sin(math.pi*t)
    roofrows.append([(w*u,y,z+.018*(1-u*u)) for u in [-1,-.75,-.5,-.25,0,.25,.5,.75,1]])
grid('Roof_skin',roofrows,mats['paint'],.025)
for front in [True,False]:
    rows=[]
    for j in range(9):
        t=j/8
        if front: y=-.635+.687*t; w=.815+(.669-.815)*t; z=.917+(1.304-.917)*t
        else: y=.917+.50*t;w=.665+(.831-.665)*t;z=1.289+(.952-1.289)*t
        rows.append([(w*u,y-.035*(1-u*u)*math.sin(math.pi*t),z+.025*(1-u*u)) for u in [-1,-.75,-.5,-.25,0,.25,.5,.75,1]])
    grid('Windshield' if front else 'Rear_windshield',rows,mats['glass'])
    edge=rows[0]+[r[-1] for r in rows[1:]]+list(reversed(rows[-1][:-1]))+[r[0] for r in reversed(rows[1:-1])]+[rows[0][0]]
    tube('Windscreen_seal',edge,.014,mats['dark'],6)
for x in [-.37,.26]:
    tube('Wiper_arm',[(x,-.64,.945),(x-.05,-.47,1.015),(x+.21,-.42,1.04)],.007,mats['dark'],5)

# Independent doors. Outer shell, inside upholstery, side window and mirror share hinge.
for sign,tag in [(-1,'L'),(1,'R')]:
    before=set(car.objects)
    rows=[]
    for j in range(13):
        y=-.625+1.59*j/12
        bottom=.296+.018*math.sin(math.pi*j/12)
        high=shoulder(y)-.005
        rows.append([(sign*side_x(y,z),y,z) for z in [bottom,bottom+.06,.48,.66,.79,high]])
    grid('Door_outer_'+tag,rows,mats['paint'],.03)
    # Inward-facing upholstered shell.
    inside=[]
    for j in range(7):
        y=-.602+1.54*j/6
        inside.append([(sign*(side_x(y,z)-.055),y,z) for z in [.325,.47,.64,.84]])
    grid('Door_lining_'+tag,inside,mats['dark'],.018)
    box('Door_leather_insert_'+tag,(sign*.817,.17,.653),(.028,1.16,.19),mats['leather'],.027)
    box('Door_armrest_'+tag,(sign*.773,.18,.58),(.10,.70,.075),mats['dark'],.025)
    box('Door_inner_latch_'+tag,(sign*.754,-.21,.745),(.017,.14,.036),mats['metal'],.009)
    box('Door_storage_'+tag,(sign*.803,.25,.40),(.065,.83,.09),mats['dark'],.015)
    speaker=cylinder('Door_speaker_'+tag,(sign*.786,-.42,.465),.073,.013,mats['metal'],16,'X')
    for dz in [-.04,-.02,0,.02,.04]:
        box('Speaker_slot_'+tag,(sign*.776,-.42,.465+dz),(.006,.105,.005),mats['dark'])
    pts=[(sign*.83,-.586,.922),(sign*.694,.074,1.272),(sign*.736,.716,1.258),(sign*.874,.943,.951)]
    glass=mesh('Door_window_'+tag,pts,[(0,1,2,3)],mats['glass'])
    if glass.data.polygons[0].normal.x*sign < 0:
        bm=bmesh.new();bm.from_mesh(glass.data)
        bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(glass.data);bm.free()
    tube('Window_surround_'+tag,pts+[pts[0]],.014,mats['dark'],6)
    tube('Door_belt_trim_'+tag,[(sign*(width(y)-.018),y,shoulder(y)) for y in [-.60,-.3,0,.3,.6,.94]],.011,mats['metal'],6)
    box('Flush_handle_'+tag,(sign*(side_x(.70,.79)+.009),.70,.79),(.021,.16,.027),mats['metal'],.01)
    tube('Mirror_stem_'+tag,[(sign*.88,-.47,.92),(sign*1.005,-.44,.97)],.025,mats['dark'],6)
    box('Mirror_shell_'+tag,(sign*1.015,-.44,.998),(.19,.22,.10),mats['paint'],.042)
    box('Mirror_reflector_'+tag,(sign*1.017,-.327,1.004),(.137,.012,.062),mats['metal'],.017)
    attach_new(before,hinges['Door_'+tag])
    # Exposed body jambs remain on the chassis.
    for y in [-.638,.977]:
        tube('Door_jamb_'+tag,[(sign*.87,y,.29),(sign*.90,y,.60),(sign*.89,y,.90)],.024,mats['dark'],6)

# Hood/deck are actual covers over open compartments, with modeled inner reinforcement.
for kind,ya,yb in [('Hood',-2.217,-.698),('Trunk',1.445,2.218)]:
    before=set(car.objects)
    rows=[]
    for j in range(17 if kind=='Hood' else 9):
        t=j/(16 if kind=='Hood' else 8);y=ya+(yb-ya)*t
        rows.append([(u*.650,y,shoulder(y)+.032*(1-u*u)+.012+(.014*math.exp(-((abs(u)-.6)/.16)**2)*math.sin(math.pi*t) if kind=='Hood' else 0)) for u in [-1,-.8,-.6,-.3,0,.3,.6,.8,1]])
    lid=grid(kind+'_outer',rows,mats['paint'],.024)
    inner=[[(x*.96,y,z-.037) for x,y,z in row] for row in rows]
    grid(kind+'_inner_liner',inner,mats['dark'],.007)
    for x in [-.44,.44]:
        tube(kind+'_reinforcement',[(x,y,shoulder(y)-.035) for y in [ya+.10,(ya+yb)/2,yb-.10]],.018,mats['metal'],6)
    if kind=='Trunk':
        tube('Integrated_ducktail',[(-.65,2.17,.855),(-.4,2.20,.863),(0,2.21,.87),(.4,2.20,.863),(.65,2.17,.855)],.025,mats['paint'],6)
    attach_new(before,hinges[kind])

# Engine bay and trunk are real tubs; the top stays open under the animated cover.
box('Engine_bay_floor',(0,-1.40,.332),(1.30,1.50,.055),mats['dark'],.02)
for sign in [-1,1]:
    box('Engine_bay_side',(sign*.631,-1.40,.553),(.035,1.49,.43),mats['dark'],.02)
box('Engine_bay_front',(0,-2.18,.51),(1.25,.034,.32),mats['dark'],.01)
for sign in [-1,1]:
    tube('Hood_seal',[(sign*.661,y,shoulder(y)+.006) for y in [-2.20,-1.7,-1.2,-.71]],.011,mats['rubber'],6)
box('Trunk_carpet_floor',(0,1.835,.398),(1.25,.72,.035),mats['dark'],.018)
for x in [-.615,.615]: box('Trunk_liner_side',(x,1.82,.631),(.027,.76,.45),mats['dark'],.014)
box('Trunk_liner_back',(0,2.205,.61),(1.22,.032,.43),mats['dark'],.016)
box('Trunk_liner_front',(0,1.447,.65),(1.23,.03,.48),mats['dark'],.015)
for sign in [-1,1]:
    tube('Trunk_seal',[(sign*.66,y,shoulder(y)+.012) for y in [1.45,1.68,1.92,2.20]],.011,mats['rubber'],6)
for x in [-.38,.38]:
    tube('Trunk_cargo_tie',[(x-.035,1.57,.43),(x-.035,1.57,.454),(x+.035,1.57,.454),(x+.035,1.57,.43)],.005,mats['metal'],5)
box('Trunk_threshold',(0,2.192,.798),(.72,.049,.026),mats['metal'],.008)

api=dict(mesh=mesh,box=box,cylinder=cylinder,tube=tube,mats=mats)
from interior import build_interior
from mechanics import build_mechanics
build_interior(api)
mechanics = build_mechanics(api)
for ob in mechanics['objects']:
    if ob.name not in car.objects: link_obj(ob)

# Flat undertray does not cross the cabin, wheel openings, engine or trunk cavity.
box('Undertray',(0,.0,.191),(1.43,4.27,.035),mats['dark'],.02)
for ob in list(car.objects):
    if ob!=root and ob.parent is None: parent_world(ob,root)

# Apply only light bevel/solidify modifiers; no subdivision surface in the game asset.
for ob in list(car.objects):
    if ob.type!='MESH':continue
    bpy.context.view_layer.objects.active=ob
    for mod in list(ob.modifiers): bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.data.set_sharp_from_angle(angle=math.radians(42))

# Consolidate meshes by mechanical assembly (retaining shared materials).
for parent in [root,*hinges.values(),mechanics['engine']]+list(mechanics['wheels'].values()):
    children=[o for o in car.objects if o.type=='MESH' and o.parent==parent]
    if not children:continue
    bpy.ops.object.select_all(action='DESELECT')
    for o in children:o.select_set(True)
    bpy.context.view_layer.objects.active=children[0]
    bpy.ops.object.join()
    joined=bpy.context.object
    joined.name='Body_Interior' if parent==root else parent.name+'_Mesh'
    # Mesh origin exactly on the assembly's hinge / wheel centre.
    scene.cursor.location=parent.matrix_world.translation
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
scene.cursor.location=(0,0,0)

opening={'Door_L':(2,65),'Door_R':(2,-65),'Hood':(0,-62),'Trunk':(0,70)}
for name,(axis,degrees) in opening.items():
    ob=hinges[name];ob.rotation_mode='XYZ'
    ob['open_axis_blender']='Z' if axis==2 else 'X'
    ob['open_angle_degrees']=degrees
    ob['open_axis_unity']='Y' if axis==2 else 'X'
    for frame,factor in [(1,0),(45,1),(75,1),(110,0)]:
        ob.rotation_euler[axis]=math.radians(degrees)*factor
        ob.keyframe_insert(data_path='rotation_euler',frame=frame,group='Openable parts')
scene.frame_start=1;scene.frame_end=110;scene.frame_set(1)
for frame,name in [(1,'CLOSED / Game export'),(45,'OPEN / Inspect interior'),(75,'OPEN hold'),(110,'CLOSED')]:
    scene.timeline_markers.new(name,frame=frame)

def select_car():
    bpy.ops.object.select_all(action='DESELECT')
    for o in car.objects:o.select_set(True)
    bpy.context.view_layer.objects.active=root

select_car()
triangles=sum(len(p.vertices)-2 for o in car.objects if o.type=='MESH' for p in o.data.polygons)
verts=sum(len(o.data.vertices) for o in car.objects if o.type=='MESH')
stats={'name':'Velaro GT','triangles':triangles,'vertices':verts,'mesh_objects':len([o for o in car.objects if o.type=='MESH']),
       'materials':len(mats),'textures':0,'units':'meters','blender_forward':'-Y','unity_forward':'+Z',
       'parts':{name:{'pivot_blender':list(ob.location),'axis':opening[name][0],'degrees':opening[name][1]} for name,ob in hinges.items()},
       'mesh_breakdown':{o.name:sum(len(p.vertices)-2 for p in o.data.polygons) for o in car.objects if o.type=='MESH'}}
(HERE/'asset_report.json').write_text(json.dumps(stats,indent=2),encoding='utf-8')
print('ASSET_REPORT '+json.dumps(stats),flush=True)
if not opts.preview_only:
    bpy.ops.export_scene.fbx(filepath=str(OUT/'velaro_gt.fbx'),use_selection=True,object_types={'MESH','EMPTY'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,use_mesh_modifiers=True,
        add_leaf_bones=False,bake_anim=False,path_mode='AUTO',mesh_smooth_type='FACE')
    bpy.ops.export_scene.gltf(filepath=str(OUT/'velaro_gt.glb'),export_format='GLB',use_selection=True,export_animations=False,
        export_materials='EXPORT',export_yup=True,export_apply=True)

# Presentation stage saved in Blend only.
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object;floor.name='Studio_floor';link_obj(floor,studio)
floor.data.materials.append(material('Studio_floor',(.12,.15,.17),.1,.48))
def aim(ob,target):ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
for name,loc,energy,size in [('Key',(-3,-4,6),1700,5),('Fill',(4,-1,3.5),1000,4),('Rim',(1,4,5),1900,4),('Roof reflection',(-1,0,6),700,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);lamp=bpy.context.object;lamp.name=name
    lamp.data.energy=energy;lamp.data.shape='DISK';lamp.data.size=size;aim(lamp,(0,0,.5));link_obj(lamp,studio)
bpy.ops.object.camera_add(location=(-6,-7,4))
cam=bpy.context.object;cam.name='Camera';link_obj(cam,studio);cam.data.type='ORTHO';cam.data.ortho_scale=6.55
aim(cam,(0,0,.62));scene.camera=cam
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active;space.region_3d.view_distance=7
            space.region_3d.view_location=(0,0,.6);space.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
            space.shading.type='MATERIAL';space.clip_end=1000
select_car()
scene.render.filepath=str(HERE/'velaro_closed.png')
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'velaro_gt.blend'))
bpy.ops.render.render(write_still=True)
scene.frame_set(45)
cam.location=(-6,-7,5);aim(cam,(0,0,.65));cam.data.ortho_scale=7.3
scene.render.filepath=str(HERE/'velaro_open.png');bpy.ops.render.render(write_still=True)
cam.location=(-5,6,4);aim(cam,(0,.4,.62));cam.data.ortho_scale=6.6
scene.render.filepath=str(HERE/'velaro_rear_open.png');bpy.ops.render.render(write_still=True)
hinges['Hood'].rotation_euler=(0,0,0);hinges['Trunk'].rotation_euler=(0,0,0)
cam.location=(-2.5,.75,1.35);aim(cam,(.10,-.15,.74));cam.data.ortho_scale=2.12
scene.render.filepath=str(HERE/'velaro_interior.png');bpy.ops.render.render(write_still=True)
scene.frame_set(1)
print('BUILD_COMPLETE',flush=True)
