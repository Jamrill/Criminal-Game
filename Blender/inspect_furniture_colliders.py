import bpy
from pathlib import Path
root = Path(__file__).resolve().parent.parent
paths = list((root / 'Assets/Original_assets/furniture').glob('*.fbx'))
paths += list((root / 'Assets/Original_assets/Walls').rglob('*door*.fbx'))
for path in paths:
    if path.stem.lower() not in ['cash_register']:
        continue
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    print('FILE', path.name)
    for ob in bpy.context.scene.objects:
        if ob.type != 'MESH': continue
        print('OBJECT', ob.name, 'LOCAL', list(ob.location), 'ROT', list(ob.rotation_euler), 'SCALE', list(ob.scale))
        mesh = ob.data
        if 'furniture' in str(path):
            points=[ob.matrix_world @ v.co for v in mesh.vertices]
            points=[(-v.x,v.z,-v.y) for v in points]
            print('AXES', [[round(n,5) for n in sorted(set(round(v[a],5) for v in points))] for a in range(3)])
            for face in mesh.polygons:
                if face.area < 100: continue
                ps=[points[i] for i in face.vertices]
                print('FACE', [[round(min(v[a] for v in ps),4),round(max(v[a] for v in ps),4)] for a in range(3)])
        neighbors = [set() for v in mesh.vertices]
        for e in mesh.edges:
            a,b=e.vertices
            neighbors[a].add(b); neighbors[b].add(a)
        remaining=set(range(len(mesh.vertices)))
        while remaining:
            pending=[remaining.pop()]; ids=[]
            while pending:
                i=pending.pop(); ids.append(i)
                for j in neighbors[i] & remaining:
                    remaining.remove(j); pending.append(j)
            vs=[ob.matrix_world @ mesh.vertices[i].co for i in ids]
            coords=[(-v.x,v.z,-v.y) for v in vs]
            lo=[min(v[a] for v in coords) for a in range(3)]
            hi=[max(v[a] for v in coords) for a in range(3)]
            print('BOX', [round((lo[a]+hi[a])/2,5) for a in range(3)], [round(hi[a]-lo[a],5) for a in range(3)], 'verts',len(ids))
