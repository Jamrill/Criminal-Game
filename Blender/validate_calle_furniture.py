import bpy, json, hashlib, struct
from pathlib import Path

ROOT=Path(__file__).resolve().parent
OUT=ROOT/'calle_modular_amueblado'

def fingerprint(o):
    h=hashlib.sha256()
    for v in o.data.vertices: h.update(struct.pack('3f',*v.co))
    for p in o.data.polygons:
        h.update(struct.pack('II',len(p.vertices),p.material_index))
        for i in p.vertices: h.update(struct.pack('I',i))
    for vector in (o.location,o.rotation_euler,o.scale): h.update(struct.pack('3f',*vector))
    return h.hexdigest()

with bpy.data.libraries.load(str(OUT/'Calle Modular_antes_muebles.blend'),link=False) as (src,dst):
    names=list(src.objects)
    current={name:bpy.data.objects[name] for name in names}
    hashes={name:fingerprint(o) for name,o in current.items() if o.type=='MESH'}
    dst.objects=names.copy()
for name,old in zip(names,dst.objects):
    if old.type=='MESH': assert fingerprint(old)==hashes[name], 'Original changed: '+name

report={'original_meshes_and_origins_unchanged':len(hashes),'assets':{}}
for obj in list(current.values()):
    if obj.name=='Corner Wall':
        assert [m.name for m in obj.data.materials]==['Ext_1','Ext_2','Int_1','Int_2']
for o in bpy.data.objects:
    if o.get('LOD') != 0 or not o.name.endswith('_LOD0'): continue
    name=o['Asset'];levels=[bpy.data.objects[name+'_LOD'+str(i)] for i in range(3)]
    counts=[]
    for level in levels:
        level.data.calc_loop_triangles();counts.append(len(level.data.loop_triangles))
        assert list(level.location)==list(o.location), name+' origins differ'
        assert list(level['ModelingPivot'])==list(o['ModelingPivot']), name+' geometry offsets differ'
        assert not level.modifiers, name+' has unapplied modifiers'
    assert counts[0]>counts[1]>counts[2], name+' non decreasing LOD count'
    report['assets'][name]=counts

table=bpy.data.objects['Table'];table.data.calc_loop_triangles()
counts=[len(table.data.loop_triangles)]
for i in (1,2):
    o=bpy.data.objects['Table_LOD'+str(i)];o.data.calc_loop_triangles();counts.append(len(o.data.loop_triangles))
    assert o.matrix_world==table.matrix_world
assert counts[0]>counts[1]>counts[2]
report['assets']['Table']=counts
floor=bpy.data.objects['Tile_llegada_Z7']
assert abs(floor.location.z-7)<1e-5
assert len([o for o in bpy.data.collections['CM_05_Arquitectura_Demo'].objects if o.name.startswith('Tile_')])==31
report['floor_pitch']=7
report['tiles']=31
(OUT/'Validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('VALIDATION_PASS',json.dumps(report))
# No saving: linked-in originals exist only for this read-only comparison.
