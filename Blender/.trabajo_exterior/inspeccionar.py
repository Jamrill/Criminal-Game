import bpy
from mathutils import Vector
from pathlib import Path
out=Path(r'C:\Users\jrjav\Documents\GitHub\Criminal-Game\Blender\carroceria_referencia')
bpy.ops.wm.open_mainfile(filepath=str(out/'carroceria_referencia.blend'))
scene=bpy.context.scene
camera=scene.camera
camera.location=(-3.5,-3,2.0)
camera.rotation_euler=(Vector((-2.10,-.65,.77))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=1.05
scene.render.resolution_x=1100;scene.render.resolution_y=900
scene.render.filepath=str(out.parent/'.trabajo_exterior/detalle_faro.png')
bpy.ops.render.render(write_still=True)
