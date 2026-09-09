"""Add six original leafless urban trees to the current furnished blend.
Blender --background Casa_Modular.blend --threads 6 --python build_urban_trees.py
Do not run again over an already extended file; preserve manual edits first.
"""
import bpy, bmesh, math, random, json, shutil
from pathlib import Path
from mathutils import Vector

OUT=Path(__file__).resolve().parent
assert not bpy.data.collections.get('ARBOLES_URBANOS_6_VARIANTES'), 'Trees already exist; use the pre-tree backup to rebuild.'
backup=OUT/'Casa_Modular_antes_arboles.blend'
if not backup.exists(): shutil.copy2(bpy.data.filepath,backup)
main=bpy.data.scenes['02_Edificio_4_plantas']
saved={o.name:(o.data if o.type=='MESH' else None, o.matrix_world.copy()) for o in bpy.data.objects}
scene=bpy.data.scenes.new('06_Arboles_urbanos_sin_hojas'); bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'; scene['escala']='Cubo 2; persona 4; arbol original 9.1. Arboles nuevos 9.2 a 10.3.'
root=bpy.data.collections.new('ARBOLES_URBANOS_6_VARIANTES'); scene.collection.children.link(root)
studio=bpy.data.collections.new('ARBOLES_Presentacion'); scene.collection.children.link(studio)

def flat(name,col,rough=.8):
    m=bpy.data.materials.new(name); m.diffuse_color=(*col,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*col,1); p.inputs['Roughness'].default_value=rough
    return m
bark=flat('Arbol_Corteza_marron_gris',(.20,.135,.086))
nt=bark.node_tree; p=nt.nodes.get('Principled BSDF')
tex=nt.nodes.new('ShaderNodeTexCoord'); mapping=nt.nodes.new('ShaderNodeVectorMath'); mapping.operation='MULTIPLY'; mapping.inputs[1].default_value=(7,7,.65)
nt.links.new(tex.outputs['Object'],mapping.inputs[0])
noise=nt.nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value=3.0; noise.inputs['Detail'].default_value=2.5; noise.inputs['Roughness'].default_value=.72
nt.links.new(mapping.outputs[0],noise.inputs['Vector'])
ramp=nt.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].position=.20; ramp.color_ramp.elements[0].color=(.085,.061,.042,1)
ramp.color_ramp.elements[1].position=.80; ramp.color_ramp.elements[1].color=(.28,.22,.16,1)
nt.links.new(noise.outputs['Fac'],ramp.inputs[0]); nt.links.new(ramp.outputs[0],p.inputs['Base Color'])
bump=nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value=.25; bump.inputs['Distance'].default_value=.035
nt.links.new(noise.outputs['Fac'],bump.inputs['Height']); nt.links.new(bump.outputs[0],p.inputs['Normal'])
groundmat=flat('Arbol_Grava_calida',(.49,.46,.39)); textmat=flat('Arbol_Rotulos',(.055,.085,.087)); refmat=flat('Arbol_Referencia_persona',(.055,.27,.30))

def relink(o,c):
    for old in list(o.users_collection): old.objects.unlink(o)
    c.objects.link(o)
def box(name,p,s,mat,c):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p); o=bpy.context.object; o.name=name; o.dimensions=s
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); o.data.materials.append(mat); relink(o,c); return o
def label(body,p,size=.38):
    d=bpy.data.curves.new(body,'FONT'); d.body=body; d.align_x='CENTER'; d.size=size; d.extrude=.001
    o=bpy.data.objects.new(body,d); studio.objects.link(o); o.location=p; o.rotation_euler=(math.pi/2,0,0); d.materials.append(textmat)

def curve(points,steps=5):
    """Centrally interpolated smooth branch centreline, keeping attachment points exact."""
    ps=[Vector(p) for p in points]; ext=[ps[0]]+ps+[ps[-1]]; out=[]
    for i in range(1,len(ext)-2):
        a,b,c,d=ext[i-1:i+3]
        for j in range(steps):
            t=j/steps
            out.append((2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)*.5)
    return out+[ps[-1]]

class Wood:
    def __init__(self,seed): self.rng=random.Random(seed); self.v=[]; self.f=[]; self.branch_count=0
    def tube(self,path,radii,n=8):
        self.branch_count+=1; start=len(self.v); frame=Vector((1,0,0))
        phase=self.rng.random()*6.28
        for i,pt in enumerate(path):
            tang=(path[min(i+1,len(path)-1)]-path[max(0,i-1)]).normalized()
            frame=frame-tang*frame.dot(tang)
            if frame.length<.001: frame=tang.cross(Vector((0,1,0)))
            frame.normalize(); side=tang.cross(frame).normalized()
            for j in range(n):
                a=j*math.tau/n; irregular=1+.045*math.sin(a*3+phase)+.025*math.sin(i*.65+a*2)
                self.v.append(tuple(pt+(frame*math.cos(a)+side*math.sin(a))*radii[i]*irregular))
        self.f.append(tuple(start+j for j in reversed(range(n))))
        for i in range(len(path)-1):
            for j in range(n):
                a=start+i*n+j; b=start+i*n+(j+1)%n
                self.f.append((a,b,b+n,a+n))
        self.f.append(tuple(start+(len(path)-1)*n+j for j in range(n)))
    def stem(self,points,r0,r1=.02,n=10,flare=False):
        path=curve(points,5); rs=[]
        for i in range(len(path)):
            t=i/(len(path)-1); r=r1+(r0-r1)*(1-t)**.90
            if flare: r*=1+.72*math.exp(-t*35)
            rs.append(r)
        self.tube(path,rs,n); return path,rs
    def branch(self,start,angle,length,r0,up=.72,bend=.20,depth=2):
        R=self.rng; direction=Vector((math.cos(angle),math.sin(angle),0))
        lateral=Vector((-direction.y,direction.x,0)); p=Vector(start)
        drift=R.uniform(-.23,.23)*length
        path=curve([p,p+direction*length*.26+Vector((0,0,length*up*.17)),
                    p+direction*length*.66+lateral*drift*.5+Vector((0,0,length*up*.56)),
                    p+direction*length*(.86-bend)+lateral*drift+Vector((0,0,length*up))],4)
        rs=[max(.009,r0*(1-i/(len(path)-1))**1.0+.007) for i in range(len(path))]
        self.tube(path,rs,8 if r0>.07 else 6)
        if depth:
            indices=[5,8,10] if depth==2 else [6,9]
            for j,ix in enumerate(indices):
                sign=-1 if j%2 else 1
                newangle=angle+sign*R.uniform(.5,1.15)
                fac=R.uniform(.43,.65) if depth==2 else R.uniform(.35,.52)
                self.branch(path[ix],newangle,length*fac,min(rs[ix]*.70,r0*.49),up=R.uniform(.65,1.35),bend=.10,depth=depth-1)
        return path,rs

specs=[
 ('01_Erguido_oval','ERGUIDO OVAL',9.6,6.0,13),
 ('02_Vaso_abierto','VASO ABIERTO',9.5,6.5,27),
 ('03_Bifurcado','BIFURCADO',10.0,6.3,39),
 ('04_Inclinado_suave','INCLINADO SUAVE',10.3,6.4,54),
 ('05_Multitronco','MULTITRONCO',9.8,6.1,78),
 ('06_Compacto_podado','COMPACTO PODADO',9.2,5.8,91),
]
trees=[]; report={'original_height':9.10000038,'person_height':4,'variants':[]}
for idx,(name,title,height,width,seed) in enumerate(specs):
    w=Wood(seed); R=w.rng
    # Root flare and short radial buttresses, not sprawling surface roots.
    for i in range(6):
        a=i*math.tau/6+R.uniform(-.18,.18); ll=R.uniform(.55,.95)
        w.stem([(0,0,.40),(.22*math.cos(a),.22*math.sin(a),.20),(ll*.7*math.cos(a),ll*.7*math.sin(a),.065),(ll*math.cos(a),ll*math.sin(a),.015)],.16,.018,8)
    leaders=[]
    if idx==0:
        leaders.append(w.stem([(0,0,0),(.04,-.06,2),(-.12,.04,4.5),(.25,.1,6.8),(.06,.22,8.8)],.31,.023,12,True))
    elif idx==1:
        tr=w.stem([(0,0,0),(-.10,.04,1.5),(.12,0,3.1),(.03,.03,4.2)],.34,.19,12,True)
        for a,z in [(0,7.8),(2.25,8.4),(4.4,7.6)]:
            leaders.append(w.stem([tr[0][-3],(.50*math.cos(a),.50*math.sin(a),4.7),(1.25*math.cos(a),1.25*math.sin(a),6.2),(2.0*math.cos(a),2.0*math.sin(a),z)],.15,.018))
    elif idx==2:
        tr=w.stem([(0,0,0),(.12,.05,1.8),(-.07,.10,3.6)],.34,.24,12,True)
        for sign in (-1,1):
            leaders.append(w.stem([tr[0][-3],(sign*.40,.10,4.5),(sign*1.0,sign*.20,6.5),(sign*1.55,.10,8.8 if sign==1 else 8.2)],.19,.018))
    elif idx==3:
        leaders.append(w.stem([(0,0,0),(.26,.02,2),(.65,-.06,4.2),(1.03,.10,6.4),(1.55,.28,8.6)],.33,.022,12,True))
    elif idx==4:
        w.stem([(0,0,0),(0,0,.3),(.02,0,.70)],.38,.26,12,True)
        for a,h in [(0,8.3),(2.3,8.8),(4.4,7.9)]:
            leaders.append(w.stem([(.05*math.cos(a),.05*math.sin(a),.22),(.38*math.cos(a),.38*math.sin(a),2.5),(.8*math.cos(a),.8*math.sin(a),5.1),(1.3*math.cos(a),1.3*math.sin(a),h)],.21,.018))
    else:
        tr=w.stem([(0,0,0),(-.08,0,2),(.13,.05,3.6),(0,0,4.8)],.36,.22,12,True)
        for i in range(5):
            a=i*2.40
            arm=w.stem([tr[0][-3],(.6*math.cos(a),.6*math.sin(a),5.0),(1.55*math.cos(a),1.55*math.sin(a),5.6+R.uniform(-.3,.3))],.17,.11,10)
            # Distinct pruning head, with several tapered new leaders growing upward.
            p=arm[0][-1]
            for j in range(3):
                w.branch(p,a+(j-1)*.8,R.uniform(1.9,2.7),.065,up=R.uniform(1.15,1.7),bend=.25,depth=1)
    for li,(path,rs) in enumerate(leaders):
        count=9 if len(leaders)==1 else (5 if len(leaders)==2 else 4)
        # Alternate heights and azimuths avoid artificial radial whorls / conifer silhouette.
        low=.45 if idx in (0,3,4) else .19
        for j in range(count):
            t=low+(j/(count-1))*(.88-low)+R.uniform(-.025,.025)
            k=min(len(path)-2,max(1,round(t*(len(path)-1))))
            a=j*2.39996+li*1.73+R.uniform(-.35,.35)
            ll=(3.4 if len(leaders)==1 else 2.45)*(1-.32*j/(count-1))*R.uniform(.86,1.13)
            w.branch(path[k],a,ll,min(rs[k]*.69,.135),up=R.uniform(.48,.94),depth=2)
        # A divided terminal leader instead of a blunt top.
        for sign in (-1,1): w.branch(path[-5],li*2.1+sign*.9,1.05,.031,up=1.0,depth=1)
    c=bpy.data.collections.new('Arbol_'+name); root.children.link(c)
    me=bpy.data.meshes.new('Malla_Arbol_'+name); me.from_pydata(w.v,[],w.f); me.update()
    o=bpy.data.objects.new('Arbol_Urbano_'+name,me); c.objects.link(o); me.materials.append(bark)
    # Weld intersecting branch bases into a continuous organic wood surface.
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    rem=o.modifiers.new('Uniones_organicas','REMESH'); rem.mode='VOXEL'; rem.voxel_size=.022; rem.use_smooth_shade=True
    bpy.ops.object.modifier_apply(modifier=rem.name)
    sm=o.modifiers.new('Transiciones_suaves','SMOOTH'); sm.factor=.65; sm.iterations=2; bpy.ops.object.modifier_apply(modifier=sm.name)
    tri_before=sum(len(p.vertices)-2 for p in o.data.polygons)
    dec=o.modifiers.new('Geometria_optimizada','DECIMATE'); dec.ratio=min(1,14500/max(1,tri_before)); dec.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=dec.name)
    bm=bmesh.new(); bm.from_mesh(o.data)
    # Remove any tiny isolated twig fragments created by voxelization; keep connected wood.
    unseen=set(bm.verts); components=[]
    while unseen:
        first=unseen.pop(); component={first}; todo=[first]
        while todo:
            v=todo.pop()
            for e in v.link_edges:
                other=e.other_vert(v)
                if other in unseen: unseen.remove(other); component.add(other); todo.append(other)
        components.append(component)
    largest=max(components,key=len)
    bmesh.ops.delete(bm,geom=[v for group in components if group is not largest for v in group],context='VERTS')
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=(0,0,0),plane_no=(0,0,1),clear_inner=True)
    edges=[e for e in bm.edges if e.is_boundary]
    if edges: bmesh.ops.holes_fill(bm,edges=edges,sides=0)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(o.data); bm.free(); o.data.update()
    # Normalize height to the existing project's scale, retaining the designed silhouette.
    zmin=min(v.co.z for v in o.data.vertices); zmax=max(v.co.z for v in o.data.vertices)
    factor=height/(zmax-zmin)
    for v in o.data.vertices: v.co.z-=zmin; v.co*=factor
    ext=max(max(v.co.x for v in o.data.vertices)-min(v.co.x for v in o.data.vertices),max(v.co.y for v in o.data.vertices)-min(v.co.y for v in o.data.vertices))
    xy=min(1,width/ext)
    for v in o.data.vertices: v.co.x*=xy; v.co.y*=xy
    for p in o.data.polygons: p.use_smooth=True
    o.data.update(); o.data.calc_loop_triangles()
    o['uso']='Tronco y ramas sin hojas. Origen al nivel del suelo; escala aplicada. Malla continua cerrada. Persona 4 unidades.'
    o['variante']=title; o['altura']=height; o['ramificaciones_generadas']=w.branch_count
    o.asset_mark(); o.asset_data.description=title+' / arbol urbano sin hojas / altura '+str(height)+' u'
    o.location=(idx*9,0,0); trees.append(o)
    report['variants'].append({'name':o.name,'height':height,'triangles':len(o.data.loop_triangles),'branches':w.branch_count,'connected_components':1})
    box('Peana_'+name,(idx*9,0,-.14),(3.1,3.1,.28),groundmat,studio)
    label('%02d  %s'%(idx+1,title),(idx*9,-2.7,.52),.32)
    label('%.1f u'%height,(idx*9,-2.7,.10),.26)
    print('TREE_DONE',o.name,len(o.data.loop_triangles),flush=True)

for zz in (1,3): box('Escala_persona_cubo_2',(-5,-.2,zz),(2,2,2),refmat,studio)
label('PERSONA / 4 u',(-5,-2,.22),.27)
scene['referencias']='Amelanchier urbano; porte de Prunus Amanogawa; estructura joven en vaso de Zelkova. Interpretaciones, no modelos botanicos exactos.'

# Four examples around the existing building. The old tree remains in the original library.
examples=bpy.data.collections.new('ARBOLES_URBANOS_Ejemplo_edificio'); bpy.data.collections['EDIFICIO_4_PLANTAS'].children.link(examples)
for old in main.objects:
    if old.type=='MESH' and old.data==bpy.data.objects['Tree_Stylized'].data:
        old.hide_render=True
        old.hide_set(True,view_layer=main.view_layers[0])
for idx,(x,y) in enumerate([(-35,23),(35,23),(-35,-3),(35,-3)]):
    ob=trees[idx].copy(); ob.data=trees[idx].data; examples.objects.link(ob); ob.name='Ejemplo_'+trees[idx].name
    ob.location=(x,y,0); ob.rotation_euler.z=idx*.73

def camera(name,location,target,scale):
    d=bpy.data.cameras.new(name); ob=bpy.data.objects.new(name,d); studio.objects.link(ob); ob.location=location
    ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler(); d.type='ORTHO'; d.ortho_scale=scale; d.clip_end=500; return ob
cam=camera('CAM_Arboles_Seis',(27,-49,21),(21,0,4.4),59); scene.camera=cam
detail=camera('CAM_Arbol_Detalle',(16,-16,10),(9,0,4),12.5)
scene.render.engine='CYCLES'; scene.cycles.samples=24; scene.cycles.use_denoising=True
scene.render.resolution_x=2200; scene.render.resolution_y=1050; scene.render.resolution_percentage=100; scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
world=bpy.data.worlds.new('Arboles_Fondo_claro'); world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.78,.80,.78,1); world.node_tree.nodes['Background'].inputs[1].default_value=.7; scene.world=world
for name,loc,energy,size in [('Arboles_Key',(15,-15,22),8500,20),('Arboles_Fill',(40,6,18),11000,20)]:
    d=bpy.data.lights.new(name,'AREA'); d.energy=energy; d.shape='DISK'; d.size=size
    ob=bpy.data.objects.new(name,d); studio.objects.link(ob); ob.location=loc; ob.rotation_euler=(Vector((22,0,4))-ob.location).to_track_quat('-Z','Y').to_euler()

# Verify the original geometry and transforms were not modified by this extension.
for name,(data,matrix) in saved.items():
    ob=bpy.data.objects[name]
    assert max(abs(ob.matrix_world[i][j]-matrix[i][j]) for i in range(4) for j in range(4))<1e-5,name
    if data is not None: assert ob.data==data,name
for ob in trees:
    bm=bmesh.new(); bm.from_mesh(ob.data)
    assert all(e.is_manifold for e in bm.edges),(ob.name,'open edges')
    assert all(f.calc_area()>1e-12 for f in bm.faces),(ob.name,'degenerate faces')
    bm.free()
    assert abs(max(v.co.z for v in ob.data.vertices)-ob['altura'])<1e-4
    assert all(abs(v-1)<1e-6 for v in ob.scale)
report['original_objects_preserved']=len(saved); report['no_leaves']=True; report['new_building_examples']=4
(OUT/'arboles_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
readme='''ARBOLES URBANOS SIN HOJAS - 6 VARIANTES
Escena 06_Arboles_urbanos_sin_hojas / coleccion ARBOLES_URBANOS_6_VARIANTES.
Cada arbol es una malla continua cerrada de tronco y ramas, con origen en el suelo y escala aplicada.
Alturas: 9.6 / 9.5 / 10.0 / 10.3 / 9.8 / 9.2 unidades. Original: 9.1. Personaje: 4.
Los seis objetos estan marcados como Asset y se pueden duplicar o mover directamente.
Cuatro copias de ejemplo rodean el edificio. Los arboles anteriores de ese entorno estan ocultos; el original de la biblioteca sigue intacto.
Corteza procedural discreta; sin texturas externas ni hojas. Las mallas usan sombreado suave y aproximadamente 14500 triangulos cada una.
No hay animaciones ni colliders. Las referencias de porte urbano estan en REFERENCIAS_ARBOLES.md.
Una copia del blend anterior a esta ampliacion queda en Casa_Modular_antes_arboles.blend.
'''
txt=bpy.data.texts.new('LEEME_ARBOLES'); txt.write(readme)
(OUT/'ARBOLES_LEEME.txt').write_text(readme,encoding='utf-8')
bpy.context.preferences.filepaths.save_version=0
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active; sp.shading.type='MATERIAL'; sp.clip_end=1000
            sp.region_3d.view_location=(21,0,4); sp.region_3d.view_distance=53
            sp.region_3d.view_rotation=cam.rotation_euler.to_quaternion(); sp.region_3d.view_perspective='ORTHO'
bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=None
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Casa_Modular.blend'))
print('TREES_SAVED',flush=True)
scene.render.filepath=str(OUT/'10_arboles_6_variantes.png'); bpy.ops.render.render(write_still=True)
scene.camera=detail; scene.render.resolution_x=1200; scene.render.resolution_y=1200
scene.render.filepath=str(OUT/'11_arbol_tronco_detalle.png'); bpy.ops.render.render(write_still=True)
scene.camera=cam; scene.render.resolution_x=2200; scene.render.resolution_y=1050
bpy.context.window.scene=main; main.render.filepath=str(OUT/'12_edificio_arboles_urbanos.png'); bpy.ops.render.render(write_still=True)
bpy.context.window.scene=scene; bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Casa_Modular.blend'))
print('TREES_COMPLETE',flush=True)
