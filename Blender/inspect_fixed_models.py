"""Read FBX geometry only; do not save or change models."""
import bpy, json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent.parent
out={}
files=list((root/'Assets/Original_assets/Fixed').glob('*.fbx'))+list((root/'Assets/Original_assets/Walls/Fixed').glob('*.fbx'))
for fixed in files:
    for path in [fixed.parent.parent/fixed.name,fixed]:
        bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
        bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        objs=[]
        for ob in bpy.context.scene.objects:
            if ob.type!='MESH': continue
            pts=[ob.matrix_world@Vector(v) for v in ob.bound_box]
            objs.append({'name':ob.name,'parent':ob.parent.name if ob.parent else None,'matrix':[list(r) for r in ob.matrix_world],
                         'bounds':[list(map(lambda i:min(v[i] for v in pts),range(3))),list(map(lambda i:max(v[i] for v in pts),range(3)))],
                         'materials':[m.name if m else None for m in ob.data.materials]})
        out[str(path.relative_to(root))]=objs
print('MODEL_REPORT',json.dumps(out),flush=True)
