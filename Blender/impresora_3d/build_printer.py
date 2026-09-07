"""Run with Blender --background --python build_printer.py. Units: metres."""
import bpy
import bmesh
import json
from pathlib import Path
from math import radians
from mathutils import Vector

OUT = Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1

def material(name, color, metal=0.0, rough=0.35):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Metallic'].default_value = metal
    p.inputs['Roughness'].default_value = rough
    return m

dark = material('Graphite_Metal', (.055, .065, .075), .8, .3)
silver = material('Brushed_Silver', (.32, .38, .42), .85, .28)
accent = material('Petrol_Blue_Metal', (.025, .23, .28), .65, .28)
black = material('Rubber_and_Recesses', (.012, .016, .02), .05, .5)
screen = material('Screen_Teal', (.035, .32, .38), .25, .18)
parts = []

def box(loc, size, mat, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rotation)
    o = bpy.context.object
    o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat)
    parts.append(o)
    return o

def cylinder(loc, radius, depth, mat, rotation=(0, 0, 0), vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rotation)
    o = bpy.context.object
    o.data.materials.append(mat)
    parts.append(o)
    return o

def finish(name, origin, bevel=.0015):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    o = bpy.context.object
    o.name = name
    scene.cursor.location = origin
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # Cut away the negative X half. Only a half mesh is stored in the blend.
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
        dist=.000001, plane_co=(0, 0, 0), plane_no=(1, 0, 0), clear_inner=True)
    bm.to_mesh(o.data)
    bm.free()
    mirror = o.modifiers.new('Mirror_X_EDIT_HALF_ONLY', 'MIRROR')
    mirror.use_clip = True
    mirror.use_mirror_merge = True
    mirror.merge_threshold = .00001
    edge = o.modifiers.new('Small_metal_edge_highlights', 'BEVEL')
    edge.width = bevel
    edge.segments = 1
    edge.limit_method = 'ANGLE'
    normal = o.modifiers.new('Weighted_normals', 'WEIGHTED_NORMAL')
    normal.keep_sharp = True
    o['Construction'] = 'Positive X half mesh + live Mirror modifier. Symmetric design.'
    parts.clear()
    return o

# Static chassis: central wide screen and blue trim distinguish it from reference.
box((0, 0, .065), (.46, .46, .075), dark)
box((.20, -.16, .016), (.058, .075, .032), black)
box((.20, .16, .016), (.058, .075, .032), black)
box((.225, 0, .083), (.012, .40, .018), accent)
box((.20, .07, .355), (.040, .044, .51), dark)
box((.20, .045, .355), (.014, .005, .49), silver)
box((.183, .044, .355), (.006, .006, .49), black)
box((0, .07, .618), (.454, .059, .038), dark)
box((0, .037, .62), (.41, .008, .010), accent)
box((.22, .07, .134), (.024, .07, .064), accent)
# Two bed rails, no adjustment wheels or screws.
box((.064, -.015, .112), (.019, .39, .014), silver)
box((.064, -.015, .122), (.005, .39, .005), black)
# Centered touchscreen, no dial. Tilt upward for easy reading.
tilt = (radians(24), 0, 0)
box((0, -.253, .088), (.274, .034, .099), dark, tilt)
box((0, -.270, .094), (.245, .005, .074), black, tilt)
box((0, -.274, .096), (.230, .003, .060), screen, tilt)
# Empty symmetric spool bracket: no spool, filament or cables.
box((.051, .07, .684), (.012, .043, .10), dark)
cylinder((0, .07, .729), .008, .12, silver, (0, radians(90), 0))
structure = finish('Structure', (0, 0, 0))

# Vertical carriage and X guide move together along the towers.
box((0, .017, .404), (.435, .026, .035), dark)
box((0, -.001, .409), (.393, .010, .012), silver)
box((.204, .026, .405), (.065, .058, .078), dark)
box((.205, -.006, .405), (.042, .008, .054), accent)
rails = finish('Rails', (0, .017, .404))

# Print head: simple fan represented by flat geometry, not an expensive grille.
box((0, -.035, .397), (.078, .080, .086), dark)
box((0, -.077, .40), (.069, .009, .067), accent)
cylinder((0, -.084, .40), .025, .005, black, (radians(90), 0, 0), 16)
cylinder((0, -.088, .40), .009, .004, silver, (radians(90), 0, 0))
box((.016, -.088, .40), (.010, .003, .004), silver)
box((0, -.088, .417), (.004, .003, .012), silver)
box((0, -.088, .383), (.004, .003, .012), silver)
box((0, -.031, .347), (.025, .027, .013), silver)
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=.002, radius2=.009, depth=.016, location=(0, -.031, .333))
parts.append(bpy.context.object)
parts[-1].data.materials.append(silver)
head = finish('Head', (0, -.031, .333), .0008)

box((0, -.02, .137), (.17, .22, .023), dark)
box((0, -.02, .156), (.332, .326, .012), silver)
box((0, -.02, .164), (.321, .315, .004), dark)
box((0, -.181, .157), (.306, .007, .007), accent)
bed = finish('Base', (0, -.02, .166), .001)

assets = [structure, rails, head, bed]
for o in assets:
    o['Unity_motion'] = {'Structure': 'Static', 'Rails': 'Vertical Y in Unity', 'Head': 'Horizontal X; follow Rails vertically', 'Base': 'Depth Z in Unity'}[o.name]

# Export only four meshes, with evaluated Mirror and bevels; .blend remains editable.
bpy.ops.object.select_all(action='DESELECT')
for o in assets: o.select_set(True)
bpy.context.view_layer.objects.active = structure
bpy.ops.export_scene.fbx(filepath=str(OUT / 'Printer_Symmetric.fbx'), use_selection=True,
    object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
    use_mesh_modifiers=True, bake_anim=False, add_leaf_bones=False)

report = {}
depsgraph = bpy.context.evaluated_depsgraph_get()
for o in assets:
    mesh = o.evaluated_get(depsgraph).to_mesh()
    mesh.calc_loop_triangles()
    report[o.name] = {'triangles': len(mesh.loop_triangles), 'live_mirror': True}
    o.evaluated_get(depsgraph).to_mesh_clear()
report['total_triangles'] = sum(v['triangles'] for v in report.values())
(OUT / 'model_report.json').write_text(json.dumps(report, indent=2))

# Lightweight studio preview; no textures, no ground mesh in the game export.
def point_at(o, target):
    o.rotation_euler = (Vector(target)-o.location).to_track_quat('-Z', 'Y').to_euler()
bpy.ops.object.camera_add(location=(1.05, -1.5, 1.03))
camera = bpy.context.object
point_at(camera, (0, 0, .35))
camera.data.type = 'ORTHO'
camera.data.ortho_scale = .95
scene.camera = camera
for loc, energy, size in [((1, -1, 1.7), 160, 1.3), ((-1, -.4, .9), 100, 1), ((.2, 1, 1.3), 180, 1)]:
    bpy.ops.object.light_add(type='AREA', location=loc)
    light = bpy.context.object
    light.data.energy = energy
    light.data.shape = 'DISK'
    light.data.size = size
    point_at(light, (0, 0, .32))
scene.world.color = (.15, .15, .15)
scene.render.engine = 'CYCLES'
scene.cycles.samples = 16
scene.cycles.use_denoising = True
scene.render.resolution_x = 700
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = str(OUT / 'preview.png')
bpy.ops.object.select_all(action='DESELECT')
structure.select_set(True)
bpy.context.view_layer.objects.active = structure
scene.cursor.location = (0, 0, 0)
for screen_layout in bpy.data.screens:
    for area in screen_layout.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_distance = 1.3
            area.spaces.active.region_3d.view_location = (0, 0, .35)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Printer_Symmetric.blend'))
bpy.ops.render.render(write_still=True)
print('PRINTER_REPORT', json.dumps(report))
