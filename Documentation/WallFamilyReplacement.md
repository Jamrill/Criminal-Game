# Sustitucion entre familias de paredes

Actualizacion del 19/10/2026: pared normal, ventana, hueco de puerta y cristal pueden reemplazarse mutuamente en tramos de la misma longitud. Las variantes complete/semincomplete/incomplete de una familia se consideran el mismo tipo y no permiten una sustitucion redundante. Las paredes compuestas conservan la recomposicion del resto de brazos.

## Uso

Seleccionar el tipo deseado y apuntar a la pared o al brazo. La previsualizacion muestra el resultado y la diferencia de precio. En espacio vacio las paredes normales y de cristal siguen colocandose como antes. Ventanas y huecos siguen siendo conversiones.

La seleccion tambien funciona a traves de un hueco vacio: no exige acertar en el marco o en el dintel. Respeta los objetos que bloquean el rayo y los impactos fisicos en caras y extremos de las paredes.

Si un hueco tiene una puerta independiente instalada, hay que retirarla/venderla antes de cambiar la pared que la sostiene, incluso si la hoja esta abierta. No se elimina ni reembolsa una puerta sin que el jugador lo decida. El sistema habitual de saldo, propiedad, colisiones, limites del solar, guardado y variantes automaticas se mantiene.

## Assets opcionales

Para una unica pieza artistica tras cada conversion se necesitarian esquinas, T y cruces con las combinaciones de brazos solidos, ventana, puerta y cristal. Cada combinacion requeriria sus tres variantes de terminacion y metadata por brazo para integrarla correctamente; anadir solo una malla no activa automaticamente su semantica de conversion. No es necesario crear todas esas combinaciones para el sistema modular actual. Sus objetos siguen siendo hijos de Solar y se guardan, aunque sean varias piezas. Una agrupacion logica seria otra tarea distinta para seleccionarlos o moverlos conjuntamente.

Los assets que faltan para convertir diagonales son los huecos de ventana y puerta con la longitud y geometria diagonales. Las cristaleras especiales de 12 unidades tampoco son intercambiables directamente con un unico tramo de 4 unidades. No se estiran modelos ni se ocultan diferencias de tamano.

## Verificacion

122 comprobaciones nuevas: todas las parejas de cuatro familias en cuatro orientaciones, bloqueo de sustituciones por si mismas, precio diferencial, brazos de Corner/T/+, hueco con puerta abierta instalada, venta de esa puerta, seleccion de huecos vacios, obstaculos, variantes de la misma familia y seleccion de extremos fisicos.

Pasaron tambien las 88 pruebas del sistema de tramos (actualizadas para la sustitucion explicita de ventanas por una pared compuesta), las 32 de anclajes, 25 de economia/persistencia y las pruebas de transparencia. Se verificaron los 59 objetos de pared registrados en la escena existente. Unity compilo y termino con codigo 0. Las pruebas no escriben slots reales ni modifican la escena.

Resultados: Documentation/WallFamilyValidation.txt y WallFamilyValidation.log.
