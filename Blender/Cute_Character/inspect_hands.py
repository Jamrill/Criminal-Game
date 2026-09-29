import bpy
from pathlib import Path
from mathutils import Vector
out=Path(__file__).resolve().parent
scene=bpy.context.scene
for o in bpy.data.objects:o.hide_render=True
scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True
scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
world=bpy.data.worlds.new('Inspection temporary');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.2,.2,.2,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.5;scene.world=world
target=Vector((1.24,-.015,1.67))
for loc,power,size in [((3,-2,5),500,2),((0,2,3),350,2)]:
    light=bpy.data.lights.new('Inspection','AREA');light.energy=power;light.size=size
    ob=bpy.data.objects.new('Inspection',light);scene.collection.objects.link(ob);ob.location=loc
    ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.cameras.new('Inspection');cam.type='ORTHO';cam.ortho_scale=.68
ob=bpy.data.objects.new('Inspection Camera',cam);scene.collection.objects.link(ob);scene.camera=ob
for name in ['Personaje_femenino','Personaje_masculino']:
    body=bpy.data.objects[name];body.hide_render=False
    for view,loc in [('palma',(3.7,-1.6,4.2)),('dorso',(-1.2,-1.5,-.8))]:
        ob.location=loc;ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(out/(name+'_'+view+'_antes.png'))
        bpy.ops.render.render(write_still=True)
    body.hide_render=True
