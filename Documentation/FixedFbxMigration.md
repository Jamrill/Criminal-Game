# Sustitución por FBX Fixed

Migración aplicada y validada con Unity 6000.3.8f1 en batch. No se borraron FBX originales ni se guardaron cambios en escenas. Las copias previas de los prefabs están en `Documentation/FixedFbxBackup/Assets/Prefabs`.

## Prefabs

- `Construction/Incompletes/Wall`, `Wall_door_hole`, `Corner_Wall` y `Glass_wall_Incomplete` usan ahora los **completos** nuevos, según confirmación del usuario. Se conservan sus rutas/nombres para mantener referencias.
- `Construction/Glass_Wall` también utiliza Fixed.
- Se añadió `Construction/Diagonal_Wall`: no existía un prefab para el modelo diagonal. Su visual compensa 0.25 unidades de Z para conservar el extremo de apoyo del modelo anterior.
- `Door` utiliza `Original_assets/Fixed/Door.fbx` y `Fixed/Knob.fbx`. Ya no tiene Animator.

La raíz y el GUID de cada prefab existente se conservan. Los modelos de pared están en un hijo `FixedVisual`, alineado al apoyo anterior. Las paredes usan MeshCollider no convexo para respetar huecos y esquinas; están pensadas como construcción estática, no como Rigidbody dinámico. La hoja de la puerta tiene BoxCollider ajustado a su malla y el marco MeshCollider sin tapar el paso.

Los muros nuevos tienen altura **8**, no 5. La puerta es también más alta que antes. No se han recolocado forjados, techos ni plantas de escenas existentes: comprobar su encaje con las alturas nuevas.

## Puerta por código

El archivo se llama ahora `DoorController.cs`, igual que su clase. Conserva el GUID del antiguo `DoorInteractable.cs`, los métodos públicos ToggleDoor/OpenDoor/CloseDoor y la conexión de InteractableObject.

En el Inspector de DoorController:

- `Door Pivot`: Sheet; `Door Axis`: eje local de bisagra; `Open Angle`: 95 grados, admite signo negativo para invertir el sentido.
- `Movement Lock Time`: duración del movimiento suave; conserva el valor anterior del prefab.
- `Knob Pivot`: KnobPoint, hijo de Sheet; `Knob Axis`, `Knob Angle` y `Knob Duration`: giro breve del pomo y vuelta al reposo.
- Textos Open/Close/Moving y Can Close se conservan.

El marco no gira; hoja, collider y pomo se mueven juntos. Al desactivar una puerta a mitad de movimiento se completa su estado objetivo y se restablece el pomo para no dejar bloqueada la interacción. No se añade persistencia del estado de la puerta.

## Verificación

`FixedFbxMigration.report.txt`: dependencias de modelos antiguos eliminadas de los prefabs migrados; ausencia de Animator.

`FixedFbxMigration.validation.txt`: carga de prefabs, ausencia de scripts perdidos, mallas/colliders de Fixed, alturas de 8, collider de hoja ajustado, rotaciones finales abierta/cerrada, raíz fija y pomo que acompaña.

Estas pruebas verifican geometría y estados finales, no una partida interactiva ni la interpolación fotograma a fotograma. Probar en Play apertura, cierre, raycast desde ambos lados y paso por el hueco. Comparar visualmente materiales y encuentros con la construcción existente antes de retirar originales. Los controladores de animación antiguos no se borraron.

La herramienta de migración se protege mediante `FixedFbxMigration.done`: no vuelve a aplicar cambios automáticamente ni necesita permanecer activa durante el juego. No borrar ese marcador para repetirla sobre prefabs editados manualmente.
