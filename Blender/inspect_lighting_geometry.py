import bpy
from pathlib import Path
root = Path(__file__).resolve().parent.parent
for relative in ['Assets/Original_assets/Traffic_light.fbx', 'Assets/Original_assets/Walls/Incomplete/Wall_incomplete.fbx']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root / relative), use_anim=False)
    print('MODEL', relative)
    for ob in bpy.data.objects:
        if ob.type != 'MESH': continue
        mesh = ob.data
        print('MESH', ob.name, 'smooth faces', sum(f.use_smooth for f in mesh.polygons), '/', len(mesh.polygons))
        if 'Wall' in relative:
            for face in mesh.polygons:
                deviations=[face.normal.angle(mesh.corner_normals[i].vector) * 57.2958 for i in face.loop_indices]
                print('FACE NORMAL', list(face.normal), 'CORNER DEVIATION DEGREES', [round(a,2) for a in deviations])
        for mi, mat in enumerate(mesh.materials):
            if not mat.name.startswith('Luz'): continue
            faces=[f for f in mesh.polygons if f.material_index==mi]
            adjacency={i:set() for f in faces for i in f.vertices}
            for f in faces:
                for i in f.vertices: adjacency[i].update(f.vertices)
            remaining=set(adjacency)
            while remaining:
                pending=[remaining.pop()]; ids=[]
                while pending:
                    i=pending.pop(); ids.append(i)
                    for j in adjacency[i] & remaining:
                        remaining.remove(j); pending.append(j)
                vs=[ob.matrix_world @ mesh.vertices[i].co for i in ids]
                vs=[(-v.x,v.z,-v.y) for v in vs]
                lo=[min(v[a] for v in vs) for a in range(3)]
                hi=[max(v[a] for v in vs) for a in range(3)]
                print(mat.name, 'CENTER', [round((lo[a]+hi[a])/2,4) for a in range(3)], 'SIZE', [round(hi[a]-lo[a],4) for a in range(3)])
