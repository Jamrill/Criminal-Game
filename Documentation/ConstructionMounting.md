# Puertas y plafones: colocacion por soporte

Correccion aplicada al catalogo, sin modificar las posiciones, pivotes, colliders, luces ni materiales de los prefabs o de las instancias originales de la escena.

## Causas

Al registrar los nuevos prefabs en ConstructionValueSetup se usaron Renderer.bounds, que en estos assets contenian limites mundiales (Door: centro 498, 102.4, 511). BuildPlacement necesita limites en el marco de colocacion (Door: centro 2, 2.4, 0). La previsualizacion, el collider y la comprobacion de ocupacion terminaban trabajando en lugares distintos. Se sustituyo ese calculo por BuildGeometry.Bounds, calculado desde las mallas y sus matrices.

Ademas, el sistema solo tenia colocacion sobre suelo. Los plafones tienen la geometria en Y local 6.8..7, ya preparada para iluminar hacia abajo; no habia que girar sus mallas ni recolocar sus luces, sino anclar su cara superior a la inferior del techo real.

## Uso

- Crear primero Wall_door_hole mediante la conversion de pared. Seleccionar Door y apuntar al hueco: se centra y orienta automaticamente. R invierte la puerta 180 grados sin sacarla del hueco. Funciona con complete, semincomplete e incomplete. No permite colocar otra puerta en el mismo hueco, incluso con la hoja abierta.
- Seleccionar Light_point o Light_point_Cold y apuntar a la cara inferior de un Floor del propio solar. La cara superior del plafon se coloca a 2 mm del techo, manteniendo la luz hacia abajo. Se conserva el ajuste horizontal a la rejilla. No se acepta el lado superior del suelo, el cielo vacio ni un apoyo parcial en el borde del techo. R mantiene el giro horizontal.
- Los objetos siguen siendo hijos directos del solar. Se guardan sus posiciones mundiales mediante el sistema habitual de partidas. No hace falta configurar nada mas en Inspector.
- Se han actualizado los bounds de todo el catalogo (tambien Floor y Stairs tenian el mismo problema de registro), conservando precios y el estado del interruptor de costes cero del usuario.
- No se recolocan automaticamente las puertas erroneas de pruebas o partidas anteriores: no existe informacion fiable de que hueco pretendia usar cada una.

## Verificacion

Unity compilo y completo 32 comprobaciones de anclaje: geometria real de la puerta, hueco, paredes giradas, ambas orientaciones, obstaculos, duplicados, colocacion y orientacion de ambos plafones, bordes del techo y serializacion/restauracion. Tambien pasaron las 25 comprobaciones anteriores de economia y persistencia, adaptadas para respetar el modo gratuito que el usuario habia activado sin cambiar el asset para probarlo.

Resultados: ConstructionMountValidation.txt y ConstructionValueValidation.txt. Logs: ConstructionMountValidation.log y ConstructionMountEconomyRegression.log.

Copia previa del catalogo: Documentation/ConstructionMountBackup/20261016_121542/ConstructionCatalog.asset.
