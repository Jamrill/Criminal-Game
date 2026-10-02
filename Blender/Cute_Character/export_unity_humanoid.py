"""Build an export-only skeleton from the current FBX; never edit the authoring blend."""
import bpy
from pathlib import Path
import numpy as np

project=Path(__file__).resolve().parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(project/'Assets/Original_assets/Player.fbx'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
rig.animation_data_clear()
for pb in rig.pose.bones:pb.matrix_basis.identity()
bpy.context.view_layer.update()
original={o.name:np.array([v.co[:] for v in o.data.vertices]) for o in bpy.data.objects if o.type=='MESH'}
bpy.context.view_layer.objects.active=rig
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bones=rig.data.edit_bones
parents={}
for side in ['L','R']:
    parents['DEF-thigh.'+side]='DEF-spine'
    parents['DEF-shoulder.'+side]='DEF-spine.003'
    parents['DEF-upper_arm.'+side]='DEF-shoulder.'+side
    parents['DEF-pelvis.'+side]='DEF-spine'
    parents['DEF-breast.'+side]='DEF-spine.003'
    for i in range(1,5):parents[f'DEF-palm.{i:02}.{side}']='DEF-hand.'+side
    for finger in ['f_index','f_middle','f_ring','f_pinky','thumb']:
        parents[f'DEF-{finger}.01.{side}']='DEF-hand.'+side
for name,parent in parents.items():
    b=bones[name]
    matrix=b.matrix.copy();length=b.length
    b.use_connect=False;b.parent=bones[parent]
    b.matrix=matrix;b.length=length
for b in list(bones):
    if not b.name.startswith('DEF-') and b.name!='root':bones.remove(b)
bpy.ops.object.mode_set(mode='OBJECT')
for pb in rig.pose.bones:
    for constraint in list(pb.constraints):pb.constraints.remove(constraint)
for o in bpy.data.objects:
    if o.type=='MESH':assert np.array_equal(original[o.name],np.array([v.co[:] for v in o.data.vertices]))
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
    if o.type in {'MESH','ARMATURE'}:o.select_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(project/'Blender/Cute_Character/Player_Unity_Export.blend'))
bpy.ops.export_scene.fbx(filepath=str(project/'Assets/Original_assets/Player_Humanoid.fbx'),use_selection=True,
    object_types={'MESH','ARMATURE'},use_armature_deform_only=True,add_leaf_bones=False,
    bake_anim=False,bake_space_transform=False,axis_forward='-Z',axis_up='Y',global_scale=1.)
print('EXPORT_READY meshes unchanged; authoring blend and original FBX untouched')
