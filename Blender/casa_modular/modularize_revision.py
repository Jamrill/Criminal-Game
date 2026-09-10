"""Incrementally modularize the edited revision; keep a backup before saving."""
import bpy, bmesh, math, json, ast, shutil, hashlib
from pathlib import Path
from mathutils import Matrix, Vector
BASE=Path(__file__).resolve().parent
OUT=BASE/'edificio_revisado'
path=OUT/'Casa_Modular_Edificio_Corregido.blend'
assert Path(bpy.data.filepath) in (path,OUT/'Casa_Modular_Antes_Modularizacion.blend')
assert not bpy.data.collections.get('KIT_COMPLETO_CONSTRUCCION'), 'Already modularized'
backup=OUT/'Casa_Modular_Antes_Modularizacion.blend'
if not backup.exists(): shutil.copy2(path,backup)
scene=bpy.data.scenes['06_EDIFICIO_CORREGIDO']; bpy.context.window.scene=scene
bpy.context.view_layer.update()
charcoal=bpy.data.materials['ED_Grafito']
for node in ast.parse((BASE/'extend_building.py').read_text(encoding='utf-8')).body:
    if isinstance(node,ast.FunctionDef) and node.name in {'col','mesh','box','cyl','beam','merge','begin','finish','direct_box','text_obj','camera','setup_render'}:
        exec(compile(ast.Module(body=[node],type_ignores=[]),'helpers','exec'))
cream=bpy.data.materials['ED_Marfil']; oak=bpy.data.materials['ED_Roble_claro']
metal=bpy.data.materials['ED_Acero']; linen=bpy.data.materials['ED_Lino']
white=bpy.data.materials['ED_Ceramica']; teal=bpy.data.materials['ED_Tejido_petroleo']
lib=col('KIT_PLANTILLAS_OCULTAS'); lib.hide_render=True; lib.hide_viewport=True
assets={}; active=None; parts=[]; floor_count=0; walls_count=0

def instance(key,c,transform):
    root=bpy.data.objects.new(key,None); c.objects.link(root); root.matrix_world=transform
    root['recurso']=key; root['modulo_construccion']=True
    for ob in assets[key].objects:
        n=ob.copy(); n.data=ob.data; c.objects.link(n); n.parent=root; n.matrix_parent_inverse=Matrix.Identity(4)
        n.matrix_basis=ob.matrix_basis.copy()
    return root

def divisions(length,maximum=4):
    full=int((length+.00001)//maximum); vals=[maximum]*full
    rest=round(length-full*maximum,5)
    if rest>.00001: vals.append(rest)
    return vals

def floor_key(w,d,material):
    name='KIT_Suelo_%gx%g_%s'%(w,d,material.name.replace('ED_',''))
    if name not in assets:
        begin(name)
        box('Soporte',(w/2,d/2,-.205),(w,d,.35),cream)
        # Real geometry, shared between copies. Thin dark joint at the edges.
        box('Junta',(w/2,d/2,-.03),(w,d,.04),oak if 'Roble' in material.name else charcoal)
        box('Acabado',(w/2,d/2,-.005),(w-.016,d-.016,.05),material)
        ob=merge(name); ob.data.transform(Matrix.Translation((0,0,-.02)))
        finish(name,[ob],note='Origen esquina superior. Pieza real de suelo; escala 1. Junta geometrica, sin textura.')
    return name

# Replace paired continuous slabs wherever used, including presentation scenes.
all_floor_objects=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Forjado_continuo')]
for ob in all_floor_objects:
    coords=[ob.matrix_world@Vector(v) for v in ob.bound_box]
    lo=[min(v[i] for v in coords) for i in range(3)]; hi=[max(v[i] for v in coords) for i in range(3)]
    cx=(lo[0]+hi[0])/2; cy=(lo[1]+hi[1])/2; top=hi[2]+.04
    collections=list(ob.users_collection)
    matches=[]
    for c in collections:
        for p in c.objects:
            if p.type!='MESH' or not p.name.startswith('Pavimento_continuo'): continue
            points=[p.matrix_world@Vector(v) for v in p.bound_box]
            center=sum(points,Vector())/8
            if abs(center.x-cx)<.001 and abs(center.y-cy)<.001 and abs(center.z-(top-.02))<.001:
                matches.append(p)
    assert matches,ob.name
    material=matches[0].data.materials[0]
    x=lo[0]
    for w in divisions(hi[0]-lo[0]):
        y=lo[1]
        for d in divisions(hi[1]-lo[1]):
            key=floor_key(w,d,material)
            root=instance(key,collections[0],Matrix.Translation((x,y,top)))
            for c in collections[1:]:
                c.objects.link(root)
                for ch in root.children: c.objects.link(ch)
            floor_count+=1; y+=d
        x+=w
    for p in set(matches): bpy.data.objects.remove(p,do_unlink=True)
    bpy.data.objects.remove(ob,do_unlink=True)

# Custom segment walls were merged into long parents. Replace with <=4-unit modules.
parents=[o for o in bpy.data.objects if o.type=='EMPTY' and not o.get('recurso') and any('_Muro' in ch.name for ch in o.children)]
for root in parents:
    children=list(root.children); wall=next(ch for ch in children if '_Muro' in ch.name)
    length=wall.dimensions.x
    if length<.001: continue
    # Direct boxes have baked coordinates relative to this parent.
    skirtings=[ch for ch in children if 'Rodapie' in ch.name]
    sides=sorted(set(1 if sum(v[1] for v in ch.bound_box)>0 else -1 for ch in skirtings))
    collections=list(root.users_collection); offset=0
    for w in divisions(length):
        key='KIT_Tabique_%gx8_%s'%(w,'Doble' if len(sides)==2 else 'UnaCara')
        if key not in assets:
            begin(key); box('Pared',(w/2,0,4),(w,.3,8),cream)
            for side in sides: box('Rodapie',(w/2,side*.19,.18),(w,.08,.36),oak)
            finish(key,note='Origen extremo inferior; encaje horizontal cada longitud del modulo.')
        n=instance(key,collections[0],root.matrix_world@Matrix.Translation((offset,0,0)))
        for c in collections[1:]:
            c.objects.link(n)
            for ch in n.children: c.objects.link(ch)
        offset+=w; walls_count+=1
    for ch in children: bpy.data.objects.remove(ch,do_unlink=True)
    bpy.data.objects.remove(root,do_unlink=True)

# Low-poly lighting fixtures; no real-time lights are imposed on Unity.
begin('KIT_Lampara_Pie')
cyl('Base',(0,0,.10),.68,.20,metal,16)
cyl('Mastil',(0,0,1.65),.07,3.0,metal,12)
# Open shade with thickness, rather than a solid opaque cylinder.
vs=[]; n=16
for radius,z in [(1.0,2.8),(.66,3.85),(.61,3.85),(.95,2.8)]:
    vs.extend([(radius*math.cos(i*2*math.pi/n),radius*math.sin(i*2*math.pi/n),z) for i in range(n)])
fs=[]
for ring in range(4):
    for i in range(n): fs.append((ring*n+i,ring*n+(i+1)%n,((ring+1)%4)*n+(i+1)%n,((ring+1)%4)*n+i))
mesh('Pantalla_hueca',vs,fs,linen)
cyl('Bombilla',(0,0,3.2),.19,.32,white,12)
finish('KIT_Lampara_Pie',note='Origen suelo. Altura 3.85 para personaje de 4. Sin componente de luz.')
begin('KIT_Plafon_Techo')
cyl('Soporte',(0,0,-.07),.92,.14,metal,20)
cyl('Difusor',(0,0,-.23),.85,.24,white,20)
finish('KIT_Plafon_Techo',note='Origen de montaje en techo; geometria hacia -Z. Colocar a cota del techo.')
lighting=col('KIT_Lamparas_instaladas',bpy.data.collections['REV_EDIFICIO_72x32'])
for level in (1,2,3):
    for ax in (-36,8):
        instance('KIT_Lampara_Pie',lighting,Matrix.Translation((ax+10,13,level*8.4)))
        for x,y in ((8,11),(17,5),(6,28),(23,28),(24,19)):
            instance('KIT_Plafon_Techo',lighting,Matrix.Translation((ax+x,y,level*8.4+7.95)))
for x in (-29,-15,15,29):
    for y in (8,18,28): instance('KIT_Plafon_Techo',lighting,Matrix.Translation((x,y,7.95)))
for level in range(4):
    for y in (4,25): instance('KIT_Plafon_Techo',lighting,Matrix.Translation((0,y,level*8.4+7.95)))

# Split continuous facade bands into actual repeatable 4-unit trim pieces.
for ob in list(bpy.data.collections['REV_EDIFICIO_72x32'].all_objects):
    if ob.type!='MESH' or not ob.name.startswith('Banda_fachada'): continue
    points=[ob.matrix_world@Vector(v) for v in ob.bound_box]
    lo=Vector([min(p[i] for p in points) for i in range(3)])
    hi=Vector([max(p[i] for p in points) for i in range(3)])
    c=ob.users_collection[0]; x=lo.x
    for w in divisions(hi.x-lo.x):
        key='KIT_Remate_Fachada_%g'%w
        if key not in assets:
            begin(key); box('Friso',(w/2,(hi.y-lo.y)/2,(hi.z-lo.z)/2),(w,hi.y-lo.y,hi.z-lo.z),cream)
            finish(key)
        instance(key,c,Matrix.Translation((x,lo.y,lo.z))); x+=w
    bpy.data.objects.remove(ob,do_unlink=True)

# Every top-level building asset and every loose component is catalogued.
# Pieces remain editable with their original child meshes/hinges.
bpy.context.view_layer.update()
building=bpy.data.collections['REV_EDIFICIO_72x32']
catalog=col('KIT_COMPLETO_CONSTRUCCION')
catalog['uso']='Una muestra por recurso usado, a la derecha del edificio. Copias enlazadas; origen propio bajo cada pieza.'
categories={name:col(name,catalog) for name in ['01_Suelos','02_Paredes','03_Ventanas_Puertas','04_Escaleras_Ascensor','05_Muebles','06_Lamparas','07_Remates_y_Otros']}
records={}; catalogued=set()
def category(name):
    name=name.lower()
    if 'suelo' in name: return '01_Suelos'
    if name.startswith(('rev_pared_','kit_tabique_','wall_')): return '02_Paredes'
    if any(x in name for x in ('expositor','armario','tv_')): return '05_Muebles'
    if any(x in name for x in ('window','door','puerta','frame','cancela','corredera','escaparate')): return '03_Ventanas_Puertas'
    if any(x in name for x in ('stairs','lift_','barandilla','poste')): return '04_Escaleras_Ascensor'
    if 'lampara' in name or 'plafon' in name: return '06_Lamparas'
    if any(x in name for x in ('banda','remate','marquesina','alfombra','rotulo','numero','referencia','muro_terraza')): return '07_Remates_y_Otros'
    return '05_Muebles'

for root in list(building.all_objects):
    if root.parent or root.type not in ('EMPTY','MESH','FONT'): continue
    obs=[ob for ob in root.children_recursive if ob.type in ('MESH','FONT')] if root.type=='EMPTY' else [root]
    if not obs: continue
    if root.get('recurso'):
        key=root['recurso']; origin=root.matrix_world.copy()
    else:
        # Deduplicate baked-coordinate boxes by geometry, not datablock name.
        origin=root.matrix_world.copy()
        if root.type=='MESH':
            center=Vector((min(v.co.x for v in root.data.vertices),min(v.co.y for v in root.data.vertices),min(v.co.z for v in root.data.vertices)))
            signature=repr(([(tuple(round(t,4) for t in v.co-center)) for v in root.data.vertices], [m.name for m in root.data.materials]))
            key=root.name.split('.')[0]+'_'+hashlib.sha1(signature.encode()).hexdigest()[:6]
            origin=origin@Matrix.Translation(center)
        else: key='Rotulo_'+root.data.body
    catalogued.update(ob.name for ob in obs)
    if key in records: records[key]['instances']+=1; continue
    records[key]={'source':root.name,'objects':obs,'origin':origin,'instances':1,'category':category(key)}

# Large floor bands become explicit straight repeatable trim segments as well.
# Other trim shapes retain their own displayed resource, never hidden in the catalog.
rows={}; index=0
for cat,c in categories.items():
    keys=[k for k,v in records.items() if v['category']==cat]
    if not keys: continue
    widths=[]; depths=[]
    for key in keys:
        r=records[key]; inv=r['origin'].inverted()
        pts=[inv@ob.matrix_world@Vector(v) for ob in r['objects'] if ob.type=='MESH' for v in ob.bound_box]
        if pts:
            widths.append(max(p.x for p in pts)-min(p.x for p in pts)); depths.append(max(p.y for p in pts)-min(p.y for p in pts))
    step_x=max(8,max(widths or [4])+4); step_y=max(9,max(depths or [4])+5)
    text_obj('Seccion_'+cat,cat.replace('_',' '),(110,index-5,0),1.1,c,rot=(0,0,0))
    for j,key in enumerate(keys):
        rec=records[key]; x=100+(j%8)*step_x; y=index+(j//8)*step_y
        target=bpy.data.objects.new('MUESTRA_'+key,None); c.objects.link(target); target.location=(x,y,0)
        target['recurso']=key; target['instancias_edificio']=rec['instances']
        inv=rec['origin'].inverted()
        for ob in rec['objects']:
            n=ob.copy(); n.data=ob.data; c.objects.link(n); n.parent=target; n.matrix_parent_inverse=Matrix.Identity(4)
            n.matrix_basis=inv@ob.matrix_world; n.hide_render=False; n.hide_viewport=False
        label=key.replace('REV_','').replace('KIT_','')
        text_obj('Etiqueta_'+key,label[:33]+'\n'+label[33:] if len(label)>33 else label,(x+2,y-3,0),.46,c,rot=(0,0,0))
    index+=math.ceil(len(keys)/8)*step_y+8

expected={o.name for o in building.all_objects if o.type in ('MESH','FONT')}
assert expected<=catalogued,sorted(expected-catalogued)
assert not any(o.name.startswith(('Forjado_continuo','Pavimento_continuo')) for o in building.all_objects)
assert all(max(float(k.split('_')[2].split('x')[0]),float(k.split('_')[2].split('x')[1]))<=4.001 for k in assets if k.startswith('KIT_Suelo_'))

# Catalog-only scene uses the same collection visible beside the actual building.
sc=bpy.data.scenes.new('11_KIT_COMPLETO_CONSTRUCCION'); setup_render(sc); sc.cycles.samples=16
sc.collection.children.link(catalog)
studio=col('KIT_Camaras_y_Luces'); sc.collection.children.link(studio)
center=Vector((155,index/2-4,1))
cam=camera('KIT_CAM_General',center+Vector((0,-85,280)),center,max(150,index*.88))
sc.camera=cam
d=bpy.data.lights.new('KIT_Sol','SUN'); d.energy=2; d.angle=.18
o=bpy.data.objects.new(d.name,d); studio.objects.link(o); o.rotation_euler=(.35,-.4,-.3)
cam_lamps=camera('KIT_CAM_Lamparas',(0,0,0),(0,0,0),16)
lamp_samples=[o for o in categories['06_Lamparas'].objects if o.type=='EMPTY']
target=lamp_samples[0].location+Vector((3,0,1.7))
cam_lamps.location=target+Vector((9,-15,11)); cam_lamps.rotation_euler=(target-cam_lamps.location).to_track_quat('-Z','Y').to_euler()

report={'floor_modules_placed':floor_count,'wall_modules_placed':walls_count,'catalog_resources':len(records),'building_meshes_and_texts_covered':len(expected),
 'assets':[{'resource':k,'category':v['category'],'instances':v['instances'],'source':v['source']} for k,v in records.items()]}
(OUT/'kit_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.context.window.scene=scene
scene['kit_completo']='Coleccion KIT_COMPLETO_CONSTRUCCION a partir de X=100; escena 11 para verlo aislado.'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(path))
print('KIT_SAVED',floor_count,walls_count,len(records),flush=True)
sc.render.resolution_x=1600; sc.render.resolution_y=2000
sc.render.filepath=str(OUT/'06_kit_completo.png'); bpy.context.window.scene=sc
bpy.ops.render.render(write_still=True,scene=sc.name)
# Category close-ups show the small pieces clearly without changing any layout.
for cat,filename in [('01_Suelos','07_kit_suelos.png'),('02_Paredes','08_kit_paredes.png'),('06_Lamparas','09_kit_lamparas.png')]:
    for name,c in categories.items(): c.hide_render=name!=cat
    roots=[ob for ob in categories[cat].objects if ob.type=='EMPTY']
    pts=[ob.matrix_world@Vector(v) for root in roots for ob in root.children if ob.type=='MESH' for v in ob.bound_box]
    lo=Vector([min(p[i] for p in pts) for i in range(3)]); hi=Vector([max(p[i] for p in pts) for i in range(3)])
    target=(lo+hi)/2+Vector((0,-1,0)); cam.location=target+Vector((0,-24,25))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.ortho_scale=max(16,(hi.x-lo.x)*1.25,(hi.y-lo.y+9)*1.9)
    sc.render.resolution_x=1600; sc.render.resolution_y=850; sc.render.filepath=str(OUT/filename)
    bpy.ops.render.render(write_still=True,scene=sc.name)
for c in categories.values(): c.hide_render=False
print('KIT_DONE',flush=True)
