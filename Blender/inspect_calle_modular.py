import bpy, json
from mathutils import Vector

print('CALLE_INSPECTION_START')
print('UNITS', bpy.context.scene.unit_settings.system, bpy.context.scene.unit_settings.scale_length)
for obj in bpy.data.objects:
    if obj.type != 'MESH':
        print('OTHER', obj.name, obj.type)
        continue
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = [min(c[i] for c in corners) for i in range(3)]
    hi = [max(c[i] for c in corners) for i in range(3)]
    print(json.dumps(dict(name=obj.name, collections=[c.name for c in obj.users_collection],
        location=list(obj.location), rotation=list(obj.rotation_euler), scale=list(obj.scale),
        dimensions=list(obj.dimensions), bounds=[lo,hi], vertices=len(obj.data.vertices),
        polygons=len(obj.data.polygons), materials=[m.name if m else None for m in obj.data.materials],
        modifiers=[dict(name=m.name,type=m.type) for m in obj.modifiers])))
print('CALLE_INSPECTION_END')
