# Construction: Complete / Incomplete

Migración aplicada con Unity 6000.3.8f1. Ambas ejecuciones batch terminaron con código 0.

## Resultado

Seis prefabs en `Assets/Prefabs/Construction/Completes` y seis en `Incompletes`:
Corner_Wall, Diagonal_Wall, Glass_wall, Wall, Wall_door_hole y Window_Wall.
Cada grupo usa exclusivamente su FBX de `Original_assets/Walls/Complete` o `Incomplete`.
Window_Wall incluye las dos mallas del FBX: pared y ventana.

Los cinco prefabs completos preexistentes mantienen sus GUID. Los incompletos tienen
GUID nuevos pero conservan los IDs serializados de la raíz de sus equivalentes.
Se han sustituido 88 instancias en `10_World_City`. Una comparación contra la copia
previa confirmó que el texto de la escena solo cambia en los GUID de los prefabs:
posiciones, escalas, nombres, overrides y demás referencias no se han reescrito.

Se mantiene el apoyo mínimo anterior del visual respecto a la raíz. Las paredes
revisadas miden 7 unidades, no 8. No se han recolocado techos, plantas ni puertas.
La diagonal revisada también cambia ligeramente de huella; revisar sus encuentros.

## BoxCollider

- Wall y Glass_wall: una caja ajustada.
- Corner_Wall: dos cajas, sin llenar el interior de la esquina.
- Diagonal_Wall: caja orientada según la diagonal.
- Wall_door_hole: tres cajas (dos jambas y dintel); validado que el centro del paso
  a dos unidades del suelo no queda bloqueado.
- Window_Wall: cuatro cajas de pared y una para la ventana con cristal.

La validación final es `ConstructionRevision.collider-validation.txt`; corrige los
recuentos iniciales de puertas y ventanas del primer informe de migración.

## Seguridad y comprobación pendiente

No se han borrado ni modificado los FBX de Old. La búsqueda final en .prefab, .unity
y .asset bajo Assets no encontró referencias serializadas a sus GUID. Conviene hacer
la comprobación visual en Play antes de eliminarlos, tal como pidió el usuario.

Copias previas de prefabs, metas y escenas: `Documentation/ConstructionRevisionBackup`.
Unity pudo cargar las cuatro escenas sin detectar prefabs perdidos. Se validaron las
dependencias de los doce prefabs, sus mallas, colliders y ausencia de scripts perdidos.
No se ha hecho una comprobación visual renderizada ni recorrido interactivo en Play.

`ConstructionRevision.done` evita ejecutar otra vez la migración principal. No borrar
ese marcador para repetirla: las carpetas de destino ya contienen los nuevos prefabs.
