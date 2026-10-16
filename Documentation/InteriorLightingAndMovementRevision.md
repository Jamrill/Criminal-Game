# Revision de iluminacion, movimiento y controles

## Iluminacion interior

Las normales de Floor y Wall_incomplete coinciden con las normales geometricas (producto escalar minimo y promedio 1). Sup/Inf son opacos y no metalicos. No se ha modificado el FBX.

La comparacion con las luces reales mostro que el suelo recupera iluminacion al desactivar las sombras de los focos o reducir su bias. Las luces calida/fria ahora usan bias local Depth 0.01 y Normal 0.02, sin heredar el Normal Bias 0.5 del pipeline. Se aplico a dos prefabs y dos luces de escena. Floor vuelve a Cast Shadows=On en el prefab y 132 renderers de escena; se retira el TwoSided anadido en la revision anterior. Su malla es cerrada y tiene normales correctas por ambas caras.

En la captura cercana anterior, la muestra de pared a Y=101.02 era (0.435,0.471,0.525), mientras que con sombras desactivadas era (0.482,0.502,0.537). Con la correccion guardada da (0.482,0.502,0.537) manteniendo las sombras activas. El suelo pasa de gris uniforme a recibir la luz calida. Los detalles y encuadres particulares del usuario se deben comprobar en Play.

Copia previa: InteriorLightingBackup/20261016_100457. Reportes: InteriorLightingCorrection.txt, FloorNormalsReview.txt e InteriorLightContacts/Report.txt. La regresion del filo superior sigue sin pixeles luminosos en las doce combinaciones de sol/camara de ClosedRoomShadows/FinalMeasurements.txt.

## Movimiento

- Marcha atras andando: multiplicador 0.65, configurable en ThirdPersonController. Las diagonales hacia atras reciben el mismo multiplicador.
- Carrera hacia atras: conserva runSpeed, usa Fast Run y orienta el cuerpo hacia la direccion de desplazamiento; la cabeza mira hacia delante del cuerpo mientras corre asi.
- Fast Run se reproduce a 0.9 de velocidad, sin cambiar la velocidad de traslacion. El generador PlayerAnimationExpansion conserva ese ajuste si se vuelve a ejecutar.
- Se conservan salto quieto y salto en movimiento.

## Controles

Tab aparece como Tablet en controles normales. Se conserva el identificador antiguo de la accion y sus GUID para mantener los bindings existentes; GameInput expone TabletPressed. La UI actual usa el titulo Tablet, con construccion como funcion existente.

La seccion Construccion contiene exclusivamente Rotate Building, Demolish Building, Build Cursor (hold) y Build Preview Distance. Los duplicados de movimiento/salto/carrera se ocultan. BuildDistance es una accion Vector2 propia con Mouse/scroll; la previsualizacion lee esta accion, no una lectura directa independiente del menu de controles. OrganizeSections se comprobo dos veces sin duplicar entradas.

## Paneles de menu

OptionsPanel y SlotsPanel en 01_MainMenu son instancias enlazadas de Assets/Prefabs/UI y contienen overrides. No se han aplicado automaticamente los cambios de escena a esos prefabs. Para hacerlo manualmente, seleccionar la raiz de cada panel y usar Overrides > Apply All o aplicar solo los overrides deseados.

Validacion: nueve comprobaciones en MovementControlsRevisionValidation.txt; ejecucion de Unity con codigo 0 en MovementControlsAndLighting.log.
