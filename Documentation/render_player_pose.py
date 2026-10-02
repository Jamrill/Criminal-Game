import bpy,json
from pathlib import Path
from mathutils import Vector
folder=Path(__file__).resolve().parent
for state in ['Standing_Idle','Walking']:
    data=json.loads((folder/('PlayerPose_'+state+'.json')).read_text())
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for entry in data['meshes']:
        mesh=bpy.data.meshes.new(entry['name'])
        p=[(v['x'],-v['z'],v['y']) for v in entry['vertices']]
        indices=entry['triangles'];faces=[indices[i:i+3][::-1] for i in range(0,len(indices),3)]
        mesh.from_pydata(p,[],faces);mesh.update()
        ob=bpy.data.objects.new(entry['name'],mesh);bpy.context.collection.objects.link(ob)
        color=(.65,.43,.28,1)
        if 'clothes' in ob.name:color=(.08,.15,.22,1)
        if 'Pelo' in ob.name or 'Cejas' in ob.name:color=(.055,.035,.02,1)
        if 'Ojos' in ob.name:color=(.85,.85,.85,1)
        material=bpy.data.materials.new(ob.name);material.diffuse_color=color;ob.data.materials.append(material)
        for poly in mesh.polygons:poly.use_smooth=True
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,data['floor']))
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16
    scene.world=bpy.data.worlds.new('PreviewWorld');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.25,.25,.25,1)
    for loc,power in [((4,-6,7),1200),((-4,-1,5),700)]:
        light=bpy.data.lights.new('Preview','AREA');light.energy=power;light.size=5
        ob=bpy.data.objects.new('Preview',light);scene.collection.objects.link(ob);ob.location=loc
        ob.rotation_euler=(Vector((0,0,1.8))-ob.location).to_track_quat('-Z','Y').to_euler()
    camera=bpy.data.cameras.new('PreviewCamera');ob=bpy.data.objects.new('PreviewCamera',camera);scene.collection.objects.link(ob)
    ob.location=(5,-10,4);ob.rotation_euler=(Vector((0,0,1.8))-ob.location).to_track_quat('-Z','Y').to_euler()
    camera.type='ORTHO';camera.ortho_scale=4.6;scene.camera=ob
    scene.render.resolution_x=700;scene.render.resolution_y=800;scene.render.resolution_percentage=100
    scene.render.filepath=str(folder/('PlayerPose_'+state+'.png'));bpy.ops.render.render(write_still=True)
