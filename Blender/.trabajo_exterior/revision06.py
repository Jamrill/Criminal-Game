import bpy,bmesh,math,ast,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from math import sin,cos,sqrt,pi
OUT=Path(r'C:\Users\jrjav\Documents\GitHub\Criminal-Game\Blender\carroceria_referencia')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'historial/refinamiento_v05.blend'))
scene=bpy.context.scene;car=bpy.data.collections['01 · CARROCERÍA EXTERIOR'];detail=bpy.data.collections['04 · ÓPTICAS Y ACABADOS'];studio=bpy.data.collections['03 · ESTUDIO Y VISTAS'];glazing=bpy.data.collections['05 · ACRISTALAMIENTO INDEPENDIENTE']
body=bpy.data.objects['Carrocería · superficie exterior continua']
allm=list(body.data.materials);paint,black,glass,chrome,rubber,recess,lens,led,red=allm[:9]
for filename,names in [('geometria_base.py',{'move','mat','mesh','curve','smoothpoly','samplepoly','interp','W','S','H','upper','endweight','low','sidepoint','cap','ccw','uvball'}),('refinar.py',{'smooth','F','tube','objmesh','orient','ring','disk','pocket','cut_pockets'}),('revision05.py',{'roundcorners'})]:
    for node in ast.parse(Path(__file__).with_name(filename).read_text(encoding='utf8')).body:
        if isinstance(node,ast.FunctionDef) and node.name in names:exec(compile(ast.Module(body=[node],type_ignores=[]),filename,'exec'))
# Temporarily close window boundaries for reliable manifold pocket operations.
bm=bmesh.new();bm.from_mesh(body.data)
for ob in list(glazing.objects):
    old=set(bm.faces);bm.from_mesh(ob.data)
    for f in set(bm.faces)-old:f.material_index=2
    bpy.data.objects.remove(ob,do_unlink=True)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data);bm.free()
for ob in list(detail.objects):
    if ob.name.startswith('Faro ·'):bpy.data.objects.remove(ob,do_unlink=True)
normal_source=bpy.data.objects.new('TEMP · superficie original',body.data.copy());detail.objects.link(normal_source);normal_source.hide_render=True
front_insets=[]
clear=bpy.data.materials['Ópticas · policarbonato transparente'];optical=bpy.data.materials['Ópticas · reflector grafito'];reflector=bpy.data.materials['Ópticas · reflector aluminizado'];smoke=bpy.data.materials['Ópticas · fondo negro']
clear.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.13
text=Path(__file__).with_name('refinar.py').read_text(encoding='utf8')
head=text[text.index('head_cutters=[]'):text.index('# Rear lenses wrap')]
head=head.replace("smoothpoly([(-2.64,s*.67),(-2.58,s*.92),(-2.38,s*.969),(-2.04,s*.90),(-2.0,s*.78),(-2.15,s*.72)],4)","roundcorners([(-2.64,s*.665),(-2.605,s*.920),(-2.38,s*.952),(-2.00,s*.917),(-1.98,s*.780),(-2.15,s*.713)],.19)")
def openpath(points):
    pts=[Vector(p) for p in points]
    for _ in range(3):
        out=[pts[0]]
        for a,b in zip(pts,pts[1:]):out.extend([a*.75+b*.25,a*.25+b*.75])
        pts=out+[pts[-1]]
    return [tuple(p) for p in pts]
head=head.replace("signature=[tuple(c[k]+.78*(p[k]-c[k]) for k in range(2)) for p in poly]","signature=openpath([(-2.17,s*.910),(-2.36,s*.919),(-2.535,s*.875),(-2.56,s*.746),(-2.42,s*.748)])")
head=head.replace(".0050,led,True)",".0037,led,False)")
head=head.replace("(-2.13,s*.825)","(-2.075,s*.831)")
head=head.replace(".89*(p[k]-c[k])",".94*(p[k]-c[k])")
head=head.replace("[.97,.91]","[.97,.94]").replace("[.91,.88]","[.94,.92]")
exec(compile(head,'rebuilt_headlamps','exec'))
# Transfer continuous exterior normals around the new pocket edges.
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
bn=bmesh.new();bn.from_mesh(body.data)
for e in bn.edges:
    if e.is_manifold and e.calc_face_angle()>.48:e.smooth=False
bn.to_mesh(body.data);bn.free()
wn=body.modifiers.new('Reflejos continuos','WEIGHTED_NORMAL');wn.keep_sharp=True;wn.weight=35;bpy.ops.object.modifier_apply(modifier=wn.name)
dt=body.modifiers.new('Continuidad alrededor de faros','DATA_TRANSFER');dt.object=normal_source;dt.use_loop_data=True;dt.data_types_loops={'CUSTOM_NORMAL'};dt.loop_mapping='POLYINTERP_NEAREST';dt.use_max_distance=True;dt.max_distance=.003
bpy.ops.object.modifier_apply(modifier=dt.name);bpy.data.objects.remove(normal_source,do_unlink=True)
# Narrow the upper cabin and reduce the over-tall grille while keeping attached trim fitted.
def shape(p):
    x,y,z=p
    yy=y*(1-.085*smooth((z-1.045)/.32))
    w=smooth((-x-2.11)/.22)*smooth((z-.20)/.08)*smooth((.72-z)/.08)
    return Vector((x,yy,z-.09*(z-.47)*w))
for col in [car,detail]:
    for ob in col.objects:
        if ob.type=='MESH':
            inv=ob.matrix_world.inverted()
            for v in ob.data.vertices:v.co=inv@shape(ob.matrix_world@v.co)
        elif ob.type=='CURVE':
            inv=ob.matrix_world.inverted()
            for sp in ob.data.splines:
                for p in sp.points:p.co=(*(inv@shape(ob.matrix_world@Vector(p.co[:3]))),1)
# Restore individually editable glazing using the same geometry, without overlapping skins.
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
    ob=mesh('Cristal',[v.co.copy() for v in vs],[tuple(ids[v] for v in f.verts) for f in faces],glass,glazing);windows.append(ob)
    c=sum((v.co for v in ob.data.vertices),Vector())/len(ob.data.vertices)
    label='Parabrisas' if c.x<-.3 else 'Luneta trasera' if c.x>1 else ('Ventanilla puerta' if c.x<.55 else 'Cristal de custodia')+(' izquierda' if c.y<0 else ' derecha')
    ob.name='Cristal · '+label
    ob.data.normals_split_custom_set([source_normals[tuple(ob.data.vertices[l.vertex_index].co)] for l in ob.data.loops])
assert len(windows)==6
bmesh.ops.delete(gb,geom=selected,context='FACES');gb.to_mesh(body.data);gb.free()
body.data.normals_split_custom_set([source_normals[tuple(body.data.vertices[l.vertex_index].co)] for l in body.data.loops])
scene['Revisión']='06 · Ópticas delanteras angulares, proporción frontal y anchura de techo.'
scene.cycles.samples=64
cams={name:next(o for o in studio.objects if o.type=='CAMERA' and o.name.startswith(name[:2])) for name in ['01_lateral','02_frontal','03_trasera','04_superior','05_tres_cuartos_delantero','06_tres_cuartos_trasero']}
scene.camera=cams['05_tres_cuartos_delantero'];bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
views=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(cams)
for key in views:
    scene.camera=cams[key];scene.render.resolution_x=1500;scene.render.resolution_y=900
    if key=='04_superior':scene.render.resolution_y=760
    if key in ['02_frontal','03_trasera']:scene.render.resolution_x=1100;scene.render.resolution_y=800
    scene.render.filepath=str(OUT/(key+'.png'));bpy.ops.render.render(write_still=True)
scene.camera=cams['05_tres_cuartos_delantero'];bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
print('REVISION06_SAVED',flush=True)
