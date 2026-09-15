import bpy
from pathlib import Path
from mathutils import Vector
scene=bpy.context.scene
cam=scene.camera
cam.location=(34,-10,17)
cam.rotation_euler=(Vector((42,5,1.6))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=23
scene.render.resolution_x=1300
scene.render.resolution_y=1050
scene.render.filepath=str(Path(__file__).resolve().parent/'calle_modular_amueblado'/'Salon_detalle.png')
bpy.ops.render.render(write_still=True)
# Camera change is only for this render; do not overwrite the saved blend.
