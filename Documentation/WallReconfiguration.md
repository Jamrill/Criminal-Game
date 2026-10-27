# Reconfiguracion de paredes por tramos

## Uso

- Seleccionar una pared normal y colocarla encima de otra sigue siendo invalido (rojo).
- Seleccionar Corner, T o + y apuntar a una pared existente ajusta la pieza al tramo senalado. La previsualizacion muestra el resultado completo de la sustitucion, incluidos los brazos que deben conservarse. R sigue controlando la orientacion. Se puede alejar la preview con la rueda para salir del ajuste cercano.
- Para una ventana o un hueco de puerta, usar los botones actuales de conversion y apuntar al brazo concreto, no al centro de la union. Un clic reemplaza ese tramo y recompone los restantes con los prefabs disponibles.
- Ejemplo: T -> Corner + Window. Convertir despues un brazo de Corner -> Wall + Wall_door_hole, conservando Window. Si se elimina el brazo central de T, los dos brazos opuestos quedan como dos paredes rectas, no como una esquina artificial.
- Las sustituciones explicitas ahora pueden cambiar tambien ventanas, huecos y cristal por el tipo seleccionado. Una pared compuesta puede sustituir los tipos situados en sus brazos, siempre que encajen los tramos y se conserve el resto. La puerta movil sigue colocandose aparte en su hueco, como hasta ahora.
- Todo queda bajo Solar y se guarda desde el menu de pausa. La herramienta existente de captura de construccion en la escena tambien conserva estas piezas y los registros de eliminacion.

No es necesario configurar el Inspector para los prefabs actuales. Se incorporaron pares de puntos `segmentPoints` a sus WallTopology, conservando mallas, pivotes, colliders y materiales.

## Validaciones y precios

El plan usa cobertura exacta de segmentos: no elimina una pared por un solapamiento parcial ni se lleva brazos fuera de la pieza elegida. Se comprueba el espacio del resultado con los colliders originales temporalmente excluidos y restaurados mediante finally. Otros objetos, paredes de otros solares y limites del solar siguen bloqueando la operacion.

Solo se cobra/devuelve la diferencia entre las piezas retiradas y las creadas. Con los precios actuales: Wall 50 -> T 120 cuesta 70; T 120 -> Corner 70 + Window 100 cuesta 50; Corner 70 -> Wall 50 + Wall_door_hole 200 cuesta 180. El interruptor de costes cero se respeta.

Una operacion fallida no modifica las paredes originales. Las piezas retiradas conservan sus IDs como tombstones; las resultantes reciben nuevos IDs. Asi no reaparece el prefab original al cargar una escena ni se duplica la construccion. Las conversiones conservan el acabado pintado. No se fusionan en una sola pieza paredes con acabados realmente distintos; el acabado base antiguo (0) y el predeterminado (-1) se consideran equivalentes.

## Variantes automaticas

Las uniones incluyen los vertices interiores de esquinas/T/+, ademas de los extremos exteriores. Las variantes mantienen la regla actual: complete con extremos descubiertos, semincomplete con todos los extremos cubiertos, incomplete cuando ademas esta cubierto todo el borde superior. La cobertura superior reconoce tanto un prefab compuesto como varias piezas independientes que cubran sus tramos. El suelo sigue usando comprobacion de cobertura de colliders. Los objetos inactivos/eliminados no participan.

## Limites deliberados

- Los huecos actuales son rectos de 4 unidades. Las diagonales existentes miden aproximadamente 5.66 unidades entre extremos: una conversion con esos huecos se rechaza con un mensaje, en lugar de deformar la ventana/puerta o cambiar la longitud de la pared. Se necesitan prefabs compatibles para esa conversion.
- No se generan mallas nuevas ni se modifican las piezas artisticas. Paredes de cristal especiales conservan su geometria y no se descomponen silenciosamente en paredes opacas.

## Implementacion

- WallGeometry: segmentos y puntos de contacto; cobertura superior por intervalos.
- WallReconfiguration: planes de sustitucion, seleccion del brazo, recomposicion, validacion y transaccion economica.
- WallChangePreview: fantasmas solo de mallas, sin scripts, luces, colliders ni registros de guardado. Restaura los renderers originales al cancelar.
- ConstructionController/ConstructionEditing: integracion con seleccion, raton, Alt, previsualizacion y clic existentes.

## Comprobacion

Unity compilo y paso 88 pruebas del nuevo sistema: secuencia del dibujo, todos los brazos de T/+/Corner/Wall en cuatro orientaciones, sustitucion al apuntar, duplicados, colisiones, acabados, dinero, modo gratuito, pisos, variantes y serializacion/carga. Tambien pasaron las 32 pruebas de anclajes, 25 de economia/persistencia y las comprobaciones de transparencia anteriores. Se verifico la representacion de los 59 objetos de pared registrados en la escena existente.

Los tests usan servicios en memoria y no escriben partidas reales. La escena del mundo no se ha modificado en esta tarea. Resultados: WallReconfigurationValidation.txt y WallReconfigurationFinal.log. Muestra de guardado: WallReconfigurationSaveSample.json.

Copia de los prefabs anterior a anadir la metadata: Documentation/WallReconfigurationBackup/20261019_101853.
