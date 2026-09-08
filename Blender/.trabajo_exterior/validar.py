import bpy,bmesh,json,math
from pathlib import Path
from datetime import datetime,timezone
from mathutils import Vector
from mathutils.bvhtree import BVHTree
out=Path(r'C:\Users\jrjav\Documents\GitHub\Criminal-Game\Blender\carroceria_referencia')
bpy.ops.wm.open_mainfile(filepath=str(out/'carroceria_referencia.blend'))
body=bpy.data.objects['Carrocería · superficie exterior continua']
bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table()
shell_boundaries=sum(e.is_boundary for e in bm.edges)
windows=list(bpy.data.collections['05 · ACRISTALAMIENTO INDEPENDIENTE'].objects)
assert len(windows)==6
assert all(len(o.data.vertices)<15000 for o in windows)
assert shell_boundaries>0
assert not any(not e.is_manifold and not e.is_boundary for e in bm.edges)
# Reassemble only in memory to verify every new window aperture is covered exactly.
for ob in windows:bm.from_mesh(ob.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.verts.ensure_lookup_table()
assert len(bm.faces)>130000,'Exterior skin missing or truncated'
tree=BVHTree.FromBMesh(bm)
deviations=[tree.find_nearest(Vector((v.co.x,-v.co.y,v.co.z)))[3] for v in list(bm.verts)[::25]]
remaining=set(bm.verts);components=0
while remaining:
    components+=1;stack=[remaining.pop()]
    while stack:
        v=stack.pop()
        for e in v.link_edges:
            other=e.other_vert(v)
            if other in remaining:remaining.remove(other);stack.append(other)
views=['01_lateral.png','02_frontal.png','03_trasera.png','04_superior.png','05_tres_cuartos_delantero.png','06_tres_cuartos_trasero.png']
report={
    'revision':bpy.context.scene.get('Revisión','desconocida')[:2],'checked_at_utc':datetime.now(timezone.utc).isoformat(),
    'topology_measurement':'Carrocería y seis cristales reunidos temporalmente en memoria, sin aplicar espesores.',
    'body_window_boundary_edges':shell_boundaries,
    'independent_windows':[o.name for o in windows],
    'scope':'Exterior estático. Sin habitáculo, motor, mecanismos, animaciones ni scripts incrustados.',
    'reference':'Imagen adjunta en la conversación; proporciones inferidas visualmente.',
    'file_reopened_in_blender':True,'blender_version':bpy.app.version_string,
    'skin_vertices':len(bm.verts),'skin_faces':len(bm.faces),
    'skin_boundary_edges':sum(e.is_boundary for e in bm.edges),
    'skin_nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),
    'skin_connected_components':components,
    'skin_zero_area_faces':sum(f.calc_area()<1e-12 for f in bm.faces),
    'skin_signed_volume_m3':bm.calc_volume(signed=True),
    'skin_dimensions_m':[max(v.co[i] for v in bm.verts)-min(v.co[i] for v in bm.verts) for i in range(3)],
    'wheelbase_m':2.96,'tire_diameter_m':.73,
    'mirror_surface_max_deviation_m':max(deviations),
    'mirror_surface_mean_deviation_m':sum(deviations)/len(deviations),
    'nonfinite_vertices':sum(not all(math.isfinite(a) for a in v.co) for v in bm.verts),
    'embedded_text_blocks':len(bpy.data.texts),'animation_actions':len(bpy.data.actions),
    'temporary_scene_objects':[o.name for o in bpy.data.objects if o.name.startswith('TEMP')],
    'inspection_cameras':sum(o.type=='CAMERA' for o in bpy.data.objects),
    'headlamp_covers':sum(o.name.startswith('Faro · cubierta óptica') for o in bpy.data.objects),
    'rear_lenses':sum(o.name.startswith('Piloto · lente envolvente') for o in bpy.data.objects),
    'exhaust_surrounds':sum(o.name.startswith('Escape · embellecedor hueco') for o in bpy.data.objects),
    'rendered_views':views,'render_bytes':{p:(out/p).stat().st_size for p in views},
}
bm.free()
assert report['skin_boundary_edges']==0 and report['skin_nonmanifold_edges']==0
assert report['skin_connected_components']==1 and report['skin_zero_area_faces']==0
assert report['nonfinite_vertices']==0 and report['skin_signed_volume_m3']>5
assert report['animation_actions']==0 and report['embedded_text_blocks']==0
assert not report['temporary_scene_objects']
assert report['inspection_cameras']==6
assert report['headlamp_covers']==report['rear_lenses']==report['exhaust_surrounds']==2
assert all(size>100000 for size in report['render_bytes'].values())
backup=out/'carroceria_referencia.blend1'
if backup.exists():assert min((out/p).stat().st_mtime for p in views)>=backup.stat().st_mtime-2,'Stale render'
(out/'revision_geometria.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2),flush=True)
