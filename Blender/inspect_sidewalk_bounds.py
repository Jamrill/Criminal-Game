import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parent.parent
for path in sorted((root/'Assets/Original_assets').glob('SideWalk*_incomplete.fbx')):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    points=[o.matrix_world@v.co for o in bpy.context.scene.objects if o.type=='MESH' for v in o.data.vertices]
    # Unity FBX import uses left-handed Y-up; Blender's imported world is Z-up.
    points=[(-p.x,p.z,-p.y) for p in points]
    lo=[min(p[i] for p in points) for i in range(3)]
    hi=[max(p[i] for p in points) for i in range(3)]
    print('SIDEWALK',json.dumps(dict(name=path.stem,center=[round((a+b)/2,5) for a,b in zip(lo,hi)],size=[round(b-a,5) for a,b in zip(lo,hi)])))
