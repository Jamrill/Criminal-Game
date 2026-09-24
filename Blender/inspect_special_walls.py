import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parent.parent
for p in (root/'Assets/Original_assets/Walls').rglob('*.fbx'):
    if not ('t_wall' in p.name.lower() or ('glass' in p.name.lower() and 'door' in p.name.lower())):continue
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(p),use_anim=False)
    for o in bpy.context.scene.objects:
        if o.type!='MESH':continue
        ps=[o.matrix_world@v.co for v in o.data.vertices]
        ps=[(-v.x,v.z,-v.y) for v in ps]
        print('GEOMETRY',p.name,o.name,json.dumps([sorted(set(round(v[i],4) for v in ps)) for i in range(3)]))
