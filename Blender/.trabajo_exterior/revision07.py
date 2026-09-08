import bpy,bmesh,math,sys,json
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
OUT=Path(r'C:\Users\jrjav\Documents\GitHub\Criminal-Game\Blender\carroceria_referencia')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'historial/refinamiento_v06.blend'))
scene=bpy.context.scene;body=bpy.data.objects['Carrocería · superficie exterior continua']
def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)
def weight(p):
    x,y,z=p
    front=math.exp(-((x+2.37)/.16)**4)*smooth((z-.58)/.13)
    rear=smooth((x-1.87)/.30)*smooth((z-.81)/.13)
    return max(front,rear)
original=[v.co.copy() for v in body.data.vertices]
oldnormals=[n.vector.copy() for n in body.data.corner_normals]
bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table();bm.verts.index_update()
protected={v for f in bm.faces if f.material_index!=0 for v in f.verts}
protected.update(v for e in bm.edges if e.is_boundary for v in e.verts)
# Do not alter pocket boundaries, glass apertures or static panel grooves.
for _ in range(2):protected.update(e.other_vert(v) for v in list(protected) for e in v.link_edges)
candidates=[v for v in bm.verts if v not in protected and weight(v.co)>.001]
for _ in range(90):bmesh.ops.smooth_vert(bm,verts=candidates,factor=.42,use_axis_x=True,use_axis_y=True,use_axis_z=True)
deltas=[];largest=0
for v in bm.verts:
    d=(v.co-original[v.index])*weight(original[v.index]);largest=max(largest,d.length)
    v.co=original[v.index]+d;deltas.append(d)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(body.data);bm.free()
kd=KDTree(len(original))
for i,p in enumerate(original):kd.insert(p,i)
kd.balance()
def displacement(p):
    hits=kd.find_n(p,4)
    if hits[0][2]>.04:return Vector()
    accum=Vector();total=0
    for co,i,d in hits:
        w=1/max(1e-8,d*d);accum+=deltas[i]*w;total+=w
    return accum/total
for name in ['01 · CARROCERÍA EXTERIOR','04 · ÓPTICAS Y ACABADOS']:
    for ob in bpy.data.collections[name].objects:
        if ob==body:continue
        inv=ob.matrix_world.inverted()
        if ob.type=='MESH':
            for v in ob.data.vertices:
                p=ob.matrix_world@v.co;v.co=inv@(p+displacement(p))
        elif ob.type=='CURVE':
            for sp in ob.data.splines:
                for v in sp.points:
                    p=ob.matrix_world@Vector(v.co[:3]);v.co=(*(inv@(p+displacement(p))),1)
# Retain untouched custom normals and recalculate only the sculpted zone.
body.data.normals_split_custom_set([(0,0,0)]*len(body.data.loops))
bpy.context.view_layer.objects.active=body
newnormals=[n.vector.copy() for n in body.data.corner_normals]
blended=[]
for i,l in enumerate(body.data.loops):
    w=min(1,deltas[l.vertex_index].length/.0004)
    blended.append(oldnormals[i].lerp(newnormals[i],w).normalized())
body.data.normals_split_custom_set(blended)
scene['Revisión']='07 · Suavizado controlado de transiciones del morro y la zaga.'
scene['Máximo desplazamiento suavizado (m)']=largest
scene.cycles.samples=64
studio=bpy.data.collections['03 · ESTUDIO Y VISTAS']
cams={name:next(o for o in studio.objects if o.type=='CAMERA' and o.name.startswith(name[:2])) for name in ['01_lateral','02_frontal','03_trasera','04_superior','05_tres_cuartos_delantero','06_tres_cuartos_trasero']}
scene.camera=cams['05_tres_cuartos_delantero'];bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
views=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(cams)
for key in views:
    scene.camera=cams[key];scene.render.resolution_x=1500;scene.render.resolution_y=900
    if key=='04_superior':scene.render.resolution_y=760
    if key in ['02_frontal','03_trasera']:scene.render.resolution_x=1100;scene.render.resolution_y=800
    scene.render.filepath=str(OUT/(key+'.png'));bpy.ops.render.render(write_still=True)
scene.camera=cams['05_tres_cuartos_delantero'];bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'carroceria_referencia.blend'))
print('REVISION07_SAVED',len(candidates),largest,flush=True)
