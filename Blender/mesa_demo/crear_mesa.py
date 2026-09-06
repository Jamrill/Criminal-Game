from pathlib import Path
import bpy
from mathutils import Vector

OUT = Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'

wood = bpy.data.materials.new('Roble natural · veta procedural')
wood.use_nodes = True
nodes = wood.node_tree.nodes
links = wood.node_tree.links
bsdf = nodes.get('Principled BSDF')
bsdf.inputs['Roughness'].default_value = 0.36
tex = nodes.new('ShaderNodeTexCoord')
mapping = nodes.new('ShaderNodeVectorMath')
mapping.operation = 'MULTIPLY'
mapping.inputs[1].default_value = (2.5, 65, 5)
links.new(tex.outputs['Generated'], mapping.inputs[0])
noise = nodes.new('ShaderNodeTexNoise')
noise.inputs['Scale'].default_value = 3
noise.inputs['Detail'].default_value = 3
noise.inputs['Roughness'].default_value = 0.65
links.new(mapping.outputs[0], noise.inputs['Vector'])
ramp = nodes.new('ShaderNodeValToRGB')
ramp.color_ramp.elements[0].position = 0.18
ramp.color_ramp.elements[0].color = (0.12, 0.048, 0.014, 1)
ramp.color_ramp.elements[1].position = 0.82
ramp.color_ramp.elements[1].color = (0.55, 0.30, 0.105, 1)
links.new(noise.outputs['Fac'], ramp.inputs[0])
links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
bump = nodes.new('ShaderNodeBump')
bump.inputs['Strength'].default_value = 0.12
bump.inputs['Distance'].default_value = 0.001
links.new(noise.outputs['Fac'], bump.inputs['Height'])
links.new(bump.outputs[0], bsdf.inputs['Normal'])

table = bpy.data.collections.new('MESA · 160 × 85 × 75 cm')
scene.collection.children.link(table)
studio = bpy.data.collections.new('Estudio · cámara y luces')
scene.collection.children.link(studio)

def move_to(obj, collection):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)

def block(name, location, dimensions, bevel=0.006):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(wood)
    mod = obj.modifiers.new('Cantos suavizados', 'BEVEL')
    mod.width = bevel
    mod.segments = 4
    obj.modifiers.new('Normales ponderadas', 'WEIGHTED_NORMAL')
    move_to(obj, table)
    return obj

top = block('Tablero de roble', (0, 0, 0.7275), (1.60, 0.85, 0.045), 0.012)
for x in (-0.67, 0.67):
    for y in (-0.30, 0.30):
        # Tapered square legs, broad at the joint and slimmer at the floor.
        vertices = [(x + sx * width, y + sy * width, z)
                    for z, width in ((0.005, 0.025), (0.705, 0.038))
                    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        mesh = bpy.data.meshes.new('Malla de pata')
        mesh.from_pydata(vertices, [], [(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
        mesh.update()
        leg = bpy.data.objects.new(f'Pata {x:+.2f} {y:+.2f}', mesh)
        table.objects.link(leg)
        leg.data.materials.append(wood)
        mod = leg.modifiers.new('Cantos suavizados', 'BEVEL')
        mod.width = 0.004
        mod.segments = 3
        leg.modifiers.new('Normales ponderadas', 'WEIGHTED_NORMAL')
for y in (-0.30, 0.30):
    block('Faldón longitudinal', (0, y, 0.6475), (1.34, 0.028, 0.115), 0.003)
for x in (-0.67, 0.67):
    block('Faldón transversal', (x, 0, 0.6475), (0.028, 0.60, 0.115), 0.003)

floor_mat = bpy.data.materials.new('Fondo arena')
floor_mat.diffuse_color = (0.30, 0.33, 0.32, 1)
bpy.ops.mesh.primitive_plane_add(size=200)
floor = bpy.context.object
floor.name = 'Suelo de estudio'
floor.data.materials.append(floor_mat)
move_to(floor, studio)

def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat('-Z', 'Y').to_euler()

bpy.ops.object.camera_add(location=(2.25, -2.65, 1.85))
camera = bpy.context.object
camera.name = 'Cámara · perspectiva general'
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 2.65
aim(camera, (0, 0, 0.39))
scene.camera = camera
move_to(camera, studio)
for name, pos, power, size in [
    ('Luz principal', (0.5, -1.6, 3), 450, 2.5),
    ('Luz de relleno', (-2, -0.3, 1.8), 180, 2),
    ('Luz de contorno', (0.5, 2, 2.5), 350, 1.8),
]:
    bpy.ops.object.light_add(type='AREA', location=pos)
    lamp = bpy.context.object
    lamp.name = name
    lamp.data.energy = power
    lamp.data.shape = 'DISK'
    lamp.data.size = size
    aim(lamp, (0, 0, 0.4))
    move_to(lamp, studio)

scene.world.color = (0.22, 0.22, 0.22)
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.cycles.use_denoising = True
scene.render.resolution_x = 1200
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(OUT / 'mesa.png')
bpy.ops.object.select_all(action='DESELECT')
for obj in table.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = top
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_distance = 2.8
            area.spaces.active.region_3d.view_location = (0, 0, 0.4)
            area.spaces.active.region_3d.view_rotation = camera.rotation_euler.to_quaternion()
            area.spaces.active.clip_end = 1000
            area.spaces.active.shading.color_type = 'MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'mesa.blend'))
bpy.ops.render.render(write_still=True)
print('Mesa creada: 9 piezas editables; 1.60 x 0.85 x 0.75 m.')
