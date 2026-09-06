"""Read-only source and FBX validation. Does not save or modify either asset."""
from pathlib import Path
import json
import math
import sys
import tempfile

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
REPORT = {"checks": [], "source": {}, "fbx": {}}


def check(name, passed, details=None, severity="error"):
    REPORT["checks"].append({"name": name, "passed": bool(passed),
                             "severity": severity, "details": details})
    print(("PASS " if passed else severity.upper() + " ") + name + ": " + str(details), flush=True)


def world_points(obj):
    return [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


def descendants(root):
    return [root] + list(root.children_recursive)


def inspect_scene(section):
    root = bpy.data.objects.get("Velaro_GT")
    check(section + "/root_exists", root is not None)
    if root is None:
        return None, []
    components = descendants(root)
    meshes = [obj for obj in components if obj.type == "MESH"]
    triangles = sum(len(face.vertices) - 2 for obj in meshes for face in obj.data.polygons)
    check(section + "/mesh_count_10", len(meshes) == 10, len(meshes))
    check(section + "/triangle_budget", triangles <= 30000, triangles)
    points = [point for obj in meshes for point in world_points(obj)]
    finite = all(math.isfinite(value) for point in points for value in point)
    check(section + "/finite_geometry", finite, len(points))
    minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    size = maximum - minimum
    check(section + "/realistic_dimensions", 1.8 < size.x < 2.5 and 4.5 < size.y < 5.3 and 1.15 < size.z < 1.6,
          {"width_m": size.x, "length_m": size.y, "height_m": size.z})
    REPORT[section].update({"mesh_count": len(meshes), "triangles": triangles,
                           "bbox_min": list(minimum), "bbox_max": list(maximum),
                           "meshes": [obj.name for obj in meshes], "pivots": {}})
    expected_wheels = {"FL": (-.90, -1.40, .36), "FR": (.90, -1.40, .36),
                       "RL": (-.90, 1.45, .36), "RR": (.90, 1.45, .36)}
    for suffix, expected in expected_wheels.items():
        name = "Wheel_" + suffix
        pivot = bpy.data.objects.get(name)
        check(section + "/" + name + "_exists", pivot is not None)
        if pivot is None:
            continue
        position = pivot.matrix_world.translation.copy()
        check(section + "/" + name + "_parent", pivot.parent == root,
              pivot.parent.name if pivot.parent else None)
        check(section + "/" + name + "_centre", (position - Vector(expected)).length < .001,
              list(position))
        wheel_mesh = bpy.data.objects.get(name + "_Mesh")
        check(section + "/" + name + "_mesh_parent", wheel_mesh is not None and wheel_mesh.parent == pivot)
        if wheel_mesh:
            origin = wheel_mesh.matrix_world.translation
            check(section + "/" + name + "_mesh_origin", (origin - position).length < .001, list(origin))
            vertices = world_points(wheel_mesh)
            centre_y = (min(p.y for p in vertices) + max(p.y for p in vertices)) * .5
            centre_z = (min(p.z for p in vertices) + max(p.z for p in vertices)) * .5
            check(section + "/" + name + "_geometry_position",
                  abs(centre_y - expected[1]) < .001 and abs(centre_z - expected[2]) < .001,
                  [centre_y, centre_z])
        REPORT[section]["pivots"][name] = list(position)

    hinges = {"Door_L": (-.8997751474, -.625, .89), "Door_R": (.8997751474, -.625, .89),
              "Hood": (0, -.685, .885), "Trunk": (0, 1.435, .94)}
    for name, expected in hinges.items():
        pivot = bpy.data.objects.get(name)
        check(section + "/" + name + "_pivot", pivot is not None and pivot.type == "EMPTY")
        if pivot is None:
            continue
        pos = pivot.matrix_world.translation
        check(section + "/" + name + "_hinge_position", (pos - Vector(expected)).length < .001, list(pos))
        check(section + "/" + name + "_parent", pivot.parent == root)
        part = bpy.data.objects.get(name + "_Mesh")
        check(section + "/" + name + "_mesh_at_hinge",
              part is not None and part.parent == pivot and (part.matrix_world.translation - pos).length < .001)
        REPORT[section]["pivots"][name] = list(pos)
    return root, meshes


def ray_normal(name, object_name, origin, direction, material_prefix=None):
    obj = bpy.data.objects[object_name]
    origin, direction = Vector(origin), Vector(direction).normalized()
    inverse = obj.matrix_world.inverted()
    local_origin = inverse @ origin
    local_direction = (inverse.to_3x3() @ direction).normalized()
    hit, location, normal, index = obj.ray_cast(local_origin, local_direction)
    if not hit:
        check("source/normal_" + name, False, "Ray missed target", "warning")
        return
    location = obj.matrix_world @ location
    normal = (obj.matrix_world.inverted().transposed().to_3x3() @ normal).normalized()
    dot = normal.dot(-direction)
    mat_index = obj.data.polygons[index].material_index
    material = obj.material_slots[mat_index].name
    details = {"position": list(location), "normal": list(normal), "outward_dot": dot,
               "material": material, "object": object_name}
    if material_prefix and not material.startswith(material_prefix):
        check("source/normal_" + name + "_material", False, details, "warning")
        return
    check("source/normal_" + name, dot > .05, details)


bpy.ops.wm.open_mainfile(filepath=str(HERE / "velaro_gt.blend"))
bpy.context.scene.frame_set(1)
bpy.context.view_layer.update()
root, meshes = inspect_scene("source")
check("source/metres", bpy.context.scene.unit_settings.system == "METRIC" and bpy.context.scene.unit_settings.scale_length == 1)

# A point near each free edge must move outwards / upwards, while the hinge stays put.
free_edges = {"Door_L": (-.90, .95, .62), "Door_R": (.90, .95, .62),
              "Hood": (0, -2.1, .83), "Trunk": (0, 2.1, .84)}
local_probes = {name: bpy.data.objects[name].matrix_world.inverted() @ Vector(point)
                for name, point in free_edges.items()}
closed_pivots = {name: bpy.data.objects[name].matrix_world.translation.copy() for name in free_edges}
bpy.context.scene.frame_set(45)
bpy.context.view_layer.update()
for name, local_point in local_probes.items():
    obj = bpy.data.objects[name]
    moved = obj.matrix_world @ local_point
    delta = moved - Vector(free_edges[name])
    opening_ok = delta.x < -.8 if name == "Door_L" else delta.x > .8 if name == "Door_R" else delta.z > .5
    check("source/animation_" + name, opening_ok,
          {"closed_probe": free_edges[name], "open_probe": list(moved), "delta": list(delta),
           "rotation_degrees": [math.degrees(value) for value in obj.rotation_euler]})
    check("source/fixed_hinge_" + name, (obj.matrix_world.translation - closed_pivots[name]).length < .0001)
bpy.context.scene.frame_set(1)
bpy.context.view_layer.update()

for side, suffix in ((-1, "L"), (1, "R")):
    for y, label in ((-2.23, "front_quarter"), (2.25, "rear_quarter")):
        ray_normal(label + "_" + suffix, "Body_Interior", (side * 3, y, .53), (-side, 0, 0), "Paint_")
    ray_normal("door_" + suffix, "Door_" + suffix + "_Mesh", (side * 3, .3, .72), (-side, 0, 0), "Paint_")
    ray_normal("side_glass_" + suffix, "Door_" + suffix + "_Mesh", (side * 3, .35, 1.12), (-side, 0, 0), "Glass_")
    ray_normal("quarter_glass_" + suffix, "Body_Interior", (side * 3, .947, 1.104), (-side, 0, 0), "Glass_")
    body = bpy.data.objects["Body_Interior"]
    for front, label, mat in ((True, "front_LED", "LED_White"), (False, "rear_LED", "LED_Red")):
        candidates = []
        for face in body.data.polygons:
            if body.material_slots[face.material_index].name != mat:
                continue
            centre = body.matrix_world @ face.center
            if centre.x * side > .1 and (centre.y < -2.3 if front else centre.y > 2.3):
                candidates.append(centre)
        if candidates:
            centre = min(candidates, key=lambda p: p.y) if front else max(candidates, key=lambda p: p.y)
            ray_normal(label + "_" + suffix, "Body_Interior",
                       (centre.x, -4 if front else 4, centre.z), (0, 1 if front else -1, 0), mat)
ray_normal("hood", "Hood_Mesh", (.13, -1.4, 3), (0, 0, -1), "Paint_")
ray_normal("trunk", "Trunk_Mesh", (.13, 1.86, 3), (0, 0, -1), "Paint_")
ray_normal("roof", "Body_Interior", (.13, .43, 3), (0, 0, -1), "Paint_")
ray_normal("front_glass", "Body_Interior", (.16, -.25, 3), (0, 0, -1), "Glass_")
ray_normal("rear_glass", "Body_Interior", (.16, 1.15, 3), (0, 0, -1), "Glass_")

# Optional export-setting diagnostic writes only to a disposable temporary folder.
# It preserves both delivered assets and reports exact import behaviour.
if "--probe-export" in sys.argv:
    with tempfile.TemporaryDirectory(prefix="velaro_fbx_validation_") as temporary:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in descendants(root):
            obj.select_set(True)
        bpy.context.view_layer.objects.active = root
        variants = [("probe_no_bake", {}),
                    ("probe_no_bake_units", {"apply_scale_options": "FBX_SCALE_UNITS"})]
        for label, extras in variants:
            path = str(Path(temporary) / (label + ".fbx"))
            bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
                                     object_types={"MESH", "EMPTY"}, axis_forward="-Z", axis_up="Y",
                                     apply_unit_scale=True, bake_space_transform=False,
                                     use_mesh_modifiers=True, add_leaf_bones=False,
                                     bake_anim=False, path_mode="AUTO", mesh_smooth_type="FACE", **extras)
        for label, extras in variants:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.fbx(filepath=str(Path(temporary) / (label + ".fbx")))
            bpy.context.view_layer.update()
            REPORT[label] = {}
            inspect_scene(label)

# Import into an empty in-memory scene. Neither the .blend nor .fbx is written.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(HERE / "exports" / "velaro_gt.fbx"))
bpy.context.view_layer.update()
inspect_scene("fbx")
check("fbx/triangle_preservation", REPORT["source"]["triangles"] == REPORT["fbx"]["triangles"],
      {"source": REPORT["source"]["triangles"], "fbx": REPORT["fbx"]["triangles"]})
check("fbx/no_studio_objects", all(obj.type not in {"CAMERA", "LIGHT"} for obj in bpy.data.objects))
REPORT["passed"] = all(check["passed"] for check in REPORT["checks"] if check["severity"] == "error")
REPORT["failures"] = [check for check in REPORT["checks"] if not check["passed"]]
(HERE / "validation_report.json").write_text(json.dumps(REPORT, indent=2), encoding="utf-8")
print("VALIDATION_RESULT " + json.dumps({"passed": REPORT["passed"], "failures": REPORT["failures"]}), flush=True)
if not REPORT["passed"]:
    raise AssertionError("Asset validation found %d failed checks; see validation_report.json" % len(REPORT["failures"]))
