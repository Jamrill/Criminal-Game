import bpy,bmesh,ast,math,json,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from math import sin,cos,pi,sqrt
OUT=Path(r'C:\Users\jrjav\Documents\GitHub\Criminal-Game\Blender\carroceria_referencia')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'historial/refinamiento_v03.blend'))
scene=bpy.context.scene
car=bpy.data.collections['01 · CARROCERÍA EXTERIOR']
detail=bpy.data.collections['04 · ÓPTICAS Y ACABADOS']
studio=bpy.data.collections['03 · ESTUDIO Y VISTAS']
body=bpy.data.objects['Carrocería · superficie exterior continua']
allm=list(body.data.materials)
paint,black,glass,chrome,rubber,recess,lens,led,red=allm[:9]
for filename,names in [('geometria_base.py',{'move','mat','mesh','curve','smoothpoly','samplepoly'}),('refinar.py',{'tube','objmesh','ring','disk','pocket','cut_pockets','roundedbox'})]:
    for node in ast.parse(Path(__file__).with_name(filename).read_text(encoding='utf8')).body:
        if isinstance(node,ast.FunctionDef) and node.name in names:exec(compile(ast.Module(body=[node],type_ignores=[]),filename,'exec'))

# Replace bulbous provisional mirrors with a swept shell and a broad, shallow glass.
for ob in list(bpy.data.objects):
    if any(ob.name.startswith(p) for p in ['Soporte retrovisor','Carcasa retrovisor','Espejo','Junta espejo','Intermitente retrovisor']):bpy.data.objects.remove(ob,do_unlink=True)
mirror=mat('Retrovisor · cristal reflectante',(.58,.66,.70),1,.065)
for s in [-1,1]:
    tube('Retrovisor · brazo estilizado',[(-.63,s*.900,1.018),(-.655,s*.944,1.024),(-.658,s*.985,1.053)],.010,black)
    # Elliptical sections sweep rearward towards the tip; vertical thickness 67 mm.
    rows=[]
    for x,r in [(-.765,.06),(-.757,.40),(-.735,.76),(-.702,.96),(-.661,1),(-.625,.96),(-.609,.92)]:
        rows.append([(x+.021*cos(a),s*(1.040+.100*r*cos(a)),1.073+.034*r*sin(a)+.009*r*cos(a)) for a in [2*pi*i/96 for i in range(96)]])
    shell=ring('Retrovisor · carcasa aerodinámica',rows,paint)
    sol=shell.modifiers.new('Espesor carcasa 2 mm','SOLIDIFY');sol.thickness=.002
    edge=rows[-1]
    tube('Retrovisor · junta del cristal',edge,.002,black,True)
    poly=[(.089*cos(2*pi*i/96),.029*sin(2*pi*i/96)) for i in range(96)]
    disk('Retrovisor · espejo independiente',poly,lambda p,r:Vector((-.606+.021*p[0]/.092,s*(1.040+p[0]),1.073+p[1]+.009*p[0]/.092)),mirror,12)
    tube('Retrovisor · intermitente fino',[(-.739+.025*t,s*(.997+.125*t),1.063+.009*t) for t in [i/50 for i in range(51)]],.0018,led)

bm=bmesh.new();bm.from_mesh(body.data);tree=BVHTree.FromBMesh(bm)
def rear(y,z,offset=0):
    hit=tree.ray_cast(Vector((5,y,z)),Vector((-1,0,0)))
    assert hit[0] is not None,(y,z)
    return hit[0]+Vector((offset,0,0))

def roundedrect(y0,y1,z0,z1,r):
    pts=[]
    for cy,cz,start in [(y1-r,z1-r,0),(y0+r,z1-r,90),(y0+r,z0+r,180),(y1-r,z0+r,270)]:
        for i in range(13):
            a=math.radians(start+i*90/12);pts.append((cy+r*cos(a),cz+r*sin(a)))
    return pts

# A real recessed number-plate aperture gives the tail a modeled centre section.
platepoly=roundedrect(-.295,.295,.674,.816,.019)
cut_pockets([pocket('TEMP · hueco portamatrícula',platepoly,lambda p,d:rear(*p,d),.014,-.048,5)])
ring('Zaga · bisel portamatrícula',[[rear(y,z,.0015) for y,z in platepoly],[rear(y*.94,.745+(z-.745)*.85,-.023) for y,z in platepoly]],paint)
disk('Zaga · fondo portamatrícula',platepoly,lambda p,r:rear(*p,-.046),black,10)
plate=mat('Matrícula · blanco satinado',(.67,.70,.71),.2,.34)
pp=roundedrect(-.255,.255,.696,.790,.004)
disk('Zaga · placa provisional sin texto',pp,lambda p,r:rear(*p,-.028),plate,10)
for y in [-.21,.21]:
    tube('Zaga · tornillo matrícula',[rear(y,.743,-.026),rear(y,.743,-.022)],.003,chrome)
for y in [-.17,.17]:
    p=rear(y,.815,-.008)
    roundedbox('Zaga · luz matrícula',p,(.016,.053,.009),led,.003)

# Subtle reflector strips and parking sensor bezels fitted to bumper curvature.
for s in [-1,1]:
    p=smoothpoly([(s*.58,.495),(s*.81,.502),(s*.81,.522),(s*.58,.518)],3)
    disk('Zaga · reflector en paragolpes',p,lambda p,r:rear(*p,.002),red,6)
for y in [-.79,-.39,.39,.79]:
    pts=[rear(y+.009*cos(a),.563+.009*sin(a),.001) for a in [2*pi*i/48 for i in range(48)]]
    tube('Zaga · cerco sensor aparcamiento',pts,.0007,black,True)
    poly=[(y+.0078*cos(2*pi*i/48),.563+.0078*sin(2*pi*i/48)) for i in range(48)]
    disk('Zaga · sensor enrasado',poly,lambda p,r:rear(*p,.0008),paint,4)

# Recessed diffuser centre and discreet longitudinal strakes between exhausts.
dp=roundedrect(-.47,.47,.258,.398,.020)
# Keep the existing sculpted fascia continuous; fins blend into its lower surface.
for y in [-.35,-.175,.175,.35]:
    verts=[]
    for yy in [y-.003,y+.003]:
        verts.extend([rear(yy,.260,.000),rear(yy,.342,.000),rear(yy,.313,.018),rear(yy,.246,.022)])
    ob=objmesh('Difusor · aleta longitudinal',verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],black)
    mod=ob.modifiers.new('Cantos suaves','BEVEL');mod.width=.002;mod.segments=2

# Less toy-like rear optics: darker lens with restrained guide brightness.
bpy.data.materials['Pilotos · difusor rojo'].node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value=.14
for ob in detail.objects:
    if ob.name.startswith('Piloto · bisel perimetral'):ob.data.materials[0]=black
    if ob.name.startswith('Piloto · guía de luz'):ob.data.bevel_depth=.0028

# Glazing is now individually editable, leaving actual cabin window apertures.
glazing=bpy.data.collections.new('05 · ACRISTALAMIENTO INDEPENDIENTE');scene.collection.children.link(glazing)
# Retain the shading across the original glass/frame seam when separating meshes.
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
for mod in list(body.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
original_normals={}
for loop,n in zip(body.data.loops,body.data.corner_normals):
    original_normals[tuple(body.data.vertices[loop.vertex_index].co)]=n.vector.copy()
gb=bmesh.new();gb.from_mesh(body.data)
remaining={f for f in gb.faces if f.material_index==2}
selected=list(remaining);windows=[]
while remaining:
    faces=[];stack=[remaining.pop()]
    while stack:
        f=stack.pop();faces.append(f)
        for edge in f.edges:
            for other in edge.link_faces:
                if other in remaining:remaining.remove(other);stack.append(other)
    verts=list({v for f in faces for v in f.verts});indices={v:i for i,v in enumerate(verts)}
    windows.append(mesh('Cristal',[v.co.copy() for v in verts],[tuple(indices[v] for v in f.verts) for f in faces],glass,glazing))
assert len(windows)==6,('Unexpected window regions',len(windows))
bmesh.ops.delete(gb,geom=selected,context='FACES');gb.to_mesh(body.data);gb.free()
body.data.normals_split_custom_set([original_normals[tuple(body.data.vertices[l.vertex_index].co)] for l in body.data.loops])
for ob in windows:
    c=sum((v.co for v in ob.data.vertices),Vector())/len(ob.data.vertices)
    if c.x<-.3:label='Parabrisas'
    elif c.x>1:label='Luneta trasera'
    else:label=('Ventanilla puerta' if c.x<.55 else 'Cristal de custodia')+(' izquierda' if c.y<0 else ' derecha')
    ob.name='Cristal · '+label;move(ob,glazing)
    ob.modifiers.clear()
    # Preserve the exterior-facing orientation of each open glass patch.
    avg=sum((p.normal*p.area for p in ob.data.polygons),Vector())
    if avg.dot(Vector((c.x*.4,c.y,c.z-.75)))<0:
        nb=bmesh.new();nb.from_mesh(ob.data);bmesh.ops.reverse_faces(nb,faces=list(nb.faces));nb.to_mesh(ob.data);nb.free()
    ob.data.normals_split_custom_set([original_normals[tuple(ob.data.vertices[l.vertex_index].co)] for l in ob.data.loops])
    ob['Espesor previsto (m)']=.003
    eb=bmesh.new();eb.from_mesh(ob.data)
    edges={e for e in eb.edges if e.is_boundary}
    while edges:
        edge=edges.pop();first,current=edge.verts;points=[first.co.copy(),current.co.copy()]
        while current!=first:
            options=[e for e in current.link_edges if e in edges]
            if not options:break
            edge=options[0];edges.remove(edge);current=edge.other_vert(current);points.append(current.co.copy())
        tube('Cristal · junta de asiento',[p for p in points[:-1]],.0018,black,True)
    eb.free()
    ob['Preparación interior']='Vidrio independiente. Ajustar transmisión cuando exista habitáculo.'
body['Preparación interior']='Huecos de ventanas abiertos. Pendientes paneles interiores, marcos de puertas y suelo del habitáculo.'
bm.free()
scene['Revisión']='04 · Retrovisores estilizados, zaga detallada y seis cristales independientes.'
scene.cycles.samples=64
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);bpy.context.view_layer.objects.active=body
cams={name:next(o for o in studio.objects if o.type=='CAMERA' and o.name.startswith(name[:2])) for name in ['01_lateral','02_frontal','03_trasera','04_superior','05_tres_cuartos_delantero','06_tres_cuartos_trasero']}
scene.camera=cams['06_tres_cuartos_trasero']
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
views=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(cams)
for key in views:
    scene.camera=cams[key];scene.render.resolution_x=1500;scene.render.resolution_y=900
    if key=='04_superior':scene.render.resolution_y=760
    if key in ['02_frontal','03_trasera']:scene.render.resolution_x=1100;scene.render.resolution_y=800
    scene.render.filepath=str(OUT/(key+'.png'));bpy.ops.render.render(write_still=True)
scene.camera=cams['06_tres_cuartos_trasero']
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
print('REVISION04_SAVED',len(windows),'windows',flush=True)
