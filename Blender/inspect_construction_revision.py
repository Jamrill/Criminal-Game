import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent.parent
out={}
for folder in ('Old','Complete','Incomplete'):
    for path in sorted((root/'Assets/Original_assets/Walls'/folder).glob('*.fbx')):
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        meshes=[]
        for o in bpy.context.scene.objects:
            if o.type!='MESH':continue
            points=[o.matrix_world@v.co for v in o.data.vertices]
            meshes.append(dict(name=o.name,bounds=[[min(v[i] for v in points) for i in range(3)],[max(v[i] for v in points) for i in range(3)]],
                materials=[m.name if m else None for m in o.data.materials],
                positions=[list(p) for p in points]))
        out[str(path.relative_to(root))]=meshes
(root/'Documentation/ConstructionRevision.geometry.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
for name,meshes in out.items():
    print(name, json.dumps([{k:v for k,v in m.items() if k!='positions'} for m in meshes]))
