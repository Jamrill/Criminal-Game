import bpy
import numpy as np
import json
import shutil
from pathlib import Path
from datetime import datetime
from mathutils import Matrix, Euler, Vector
from mathutils.kdtree import KDTree

source=Path(bpy.data.filepath)
out=source.parent / ('HeadBinding_Test_'+datetime.now().strftime('%Y%m%d_%H%M%S'))
out.mkdir()
backup=out/source.name
shutil.copy2(source,backup)
report={'backup':str(backup),'tests':{}}
rig=bpy.data.objects['rig.001']
names=['Pelo_01','Ojos_femeninos']
export_names=['rig.001','Retopo_Personaje_femenino','Retopo_Cejas_femenino','Interior clothes','Interior clothes_2']+names
scene=bpy.context.scene
original_frame=scene.frame_current
original_active=bpy.context.view_layer.objects.active
original_selection=list(bpy.context.selected_objects)
assert bpy.context.mode=='OBJECT'
assert rig.data.pose_position=='POSE'
assert rig.data.bones['DEF-spine.006'].use_deform

def update():
    rig.update_tag()
    bpy.context.view_layer.update()

def points(ob):
    ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh=ev.to_mesh()
    try:
        p=np.empty((len(mesh.vertices),3),dtype=np.float64)
        mesh.vertices.foreach_get('co',p.ravel())
        m=np.array(ev.matrix_world)
        return p@m[:3,:3].T+m[:3,3]
    finally:ev.to_mesh_clear()

def transform(p,m):
    m=np.array(m)
    return p@m[:3,:3].T+m[:3,3]

update()
before={n:points(bpy.data.objects[n]) for n in names}
head_rest=rig.data.bones['DEF-spine.006'].matrix_local.copy()
assert max(abs(rig.pose.bones['DEF-spine.006'].matrix[i][j]-head_rest[i][j]) for i in range(4) for j in range(4))<1e-5, 'Current head is not in reference pose; stop.'
for n in names:
    ob=bpy.data.objects[n]
    assert ob.parent==rig
    assert ob.data.users==1
    arm=[m for m in ob.modifiers if m.type=='ARMATURE']
    assert len(arm)==1 and arm[0].object==rig
    positive={ob.vertex_groups[g.group].name for v in ob.data.vertices for g in v.groups if g.weight>0}
    assert positive==(set() if n=='Pelo_01' else {'DEF-spine.005','DEF-spine.006'})
    world=ob.matrix_world.copy()
    ob.parent=rig
    ob.parent_type='OBJECT'
    ob.parent_bone=''
    ob.matrix_parent_inverse=rig.matrix_world.inverted()
    ob.matrix_world=world
    mod=arm[0]
    mod.vertex_group=''
    mod.invert_vertex_group=False
    mod.use_vertex_groups=True
    mod.use_bone_envelopes=False
    mod.use_deform_preserve_volume=False
    mod.use_multi_modifier=False
    mod.show_viewport=True
    mod.show_render=True
    indices=list(range(len(ob.data.vertices)))
    for group in ob.vertex_groups:
        group.remove(indices)
    group=ob.vertex_groups.get('DEF-spine.006') or ob.vertex_groups.new(name='DEF-spine.006')
    group.add(indices,1.0,'REPLACE')
    assert all([(g.group,g.weight) for g in v.groups if g.weight>0]==[(group.index,1.0)] for v in ob.data.vertices)
    ob.data.update()
update()
for n in names:
    error=float(np.max(np.linalg.norm(points(bpy.data.objects[n])-before[n],axis=1)))
    report['tests'][n+'_position_error']=error
    assert error<1e-5,(n,error)

# Pose-mode tests: restore the exact control matrix afterwards; no keyframes inserted.
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='POSE')
control=rig.pose.bones['head']
saved_basis=control.matrix_basis.copy()
base_matrix=rig.matrix_world@rig.pose.bones['DEF-spine.006'].matrix
baseline={n:points(bpy.data.objects[n]) for n in names}
try:
    for label,rotation,offset in [('turn',(0,.4,0),(0,0,0)),('tilt',(.25,0,.15),(0,0,0)),('move',(0,0,0),(.025,.03,.015))]:
        control.matrix_basis=Matrix.Translation(offset)@Euler(rotation,'XYZ').to_matrix().to_4x4()@saved_basis
        update()
        change=(rig.matrix_world@rig.pose.bones['DEF-spine.006'].matrix)@base_matrix.inverted()
        for n in names:
            expected=transform(baseline[n],change)
            error=float(np.max(np.linalg.norm(points(bpy.data.objects[n])-expected,axis=1)))
            report['tests'][n+'_'+label+'_rigid_error']=error
            assert error<2e-5,(n,label,error)
finally:
    control.matrix_basis=saved_basis
    update()
    bpy.ops.object.mode_set(mode='OBJECT')

# Save only intended binding changes, with original frame and selection.
scene.frame_set(original_frame)
bpy.ops.object.select_all(action='DESELECT')
for ob in original_selection:ob.select_set(True)
bpy.context.view_layer.objects.active=original_active
assert source.stat().st_mtime==backup.stat().st_mtime, 'Source changed externally; refusing overwrite.'
bpy.ops.wm.save_as_mainfile(filepath=str(source))
report['blend_saved']=True

# Sample the authored idle for a numerical round-trip comparison.
samples={}
frames=[0,10,20,30,40,50,59]
compare_names=['Retopo_Personaje_femenino']+names
for frame in frames:
    scene.frame_set(frame)
    update()
    samples[frame]={n:points(bpy.data.objects[n]) for n in compare_names}
scene.frame_set(0)
scene.frame_start=0
scene.frame_end=59
bpy.ops.object.select_all(action='DESELECT')
for n in export_names:bpy.data.objects[n].select_set(True)
bpy.context.view_layer.objects.active=rig
fbx=out/'Player_Female_HeadBinding_Test.fbx'
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','ARMATURE'},
    use_armature_deform_only=True,add_leaf_bones=False,global_scale=1.,axis_forward='-Z',axis_up='Y',
    bake_space_transform=False,use_mesh_modifiers=True,bake_anim=True,bake_anim_step=1.,bake_anim_simplify_factor=0.,
    bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False)
report['fbx']=str(fbx)

# New empty Blender scene in this background process only; saved source stays intact.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(fbx),use_anim=True)
for frame in frames:
    # Blender FBX importer starts takes at frame 1 by default (source take starts at 0).
    bpy.context.scene.frame_set(frame+1)
    bpy.context.view_layer.update()
    for n in compare_names:
        actual=points(bpy.data.objects[n])
        expected=samples[frame][n]
        tree=KDTree(len(actual))
        for i,p in enumerate(actual):tree.insert(Vector(p),i)
        tree.balance()
        error=max(tree.find(Vector(p))[2] for p in expected)
        reverse=KDTree(len(expected))
        for i,p in enumerate(expected):reverse.insert(Vector(p),i)
        reverse.balance()
        error=max(error,max(reverse.find(Vector(p))[2] for p in actual))
        report['tests'][n+'_fbx_frame_'+str(frame)]=error
report['reimport_max_error']=max(v for k,v in report['tests'].items() if '_fbx_frame_' in k)
report['reimport_pass']=report['reimport_max_error']<.002
report['imported_actions']=[a.name for a in bpy.data.actions]
bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FBX_Reimport_Check.blend'))
(out/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('HEAD_BINDING_REPORT',json.dumps(report),flush=True)
assert report['reimport_pass'], 'FBX round trip differs; inspect report before further edits.'
