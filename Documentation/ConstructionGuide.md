# Construccion inicial

## Propiedades y jerarquia del Player

La UI es una instancia anidada de `ConstructionUI.prefab` dentro de `Player.prefab`.
`ConstructionController.embeddedUI` referencia esa instancia; no se crea otra al iniciar.
`ConstructionPersistence` es ahora un componente del Player: restaura una vez por
escena/partida aunque reaparezca el jugador. Los objetos construidos NO son hijos
del Player y no se destruyen con el. Se ha eliminado el antiguo host vacio de la escena.

Tab > Propiedades permite comprar los solares activos de la escena por su precio,
sin cartel ni necesidad de ser ya propietario. Tambien hay un acceso desde el HUD.
`BuildPlot` permite editar Display Name, Property Id, Price y Buildable. Los IDs deben
ser unicos y estables. El solar actual se ha habilitado para construir y cuesta 1;
un ID negativo se asigna durante la migracion sin reutilizar IDs de carteles existentes.
Comprar descuenta dinero, bloquea compras duplicadas y actualiza la propiedad en la
partida actual. Se guarda con el guardado normal; comprar no fuerza un guardado a disco.
No se trasladan automaticamente los solares de otras escenas a este listado inicial.

## Solar ya creado

En `10_World_City`, el objeto `Solar` conserva su volumen y `Grid Origin`.
El solar actual ya tiene **Buildable** e ID asignados por la migracion. Para nuevos
solares, asigna un **Property Id** unico y marca **Buildable**; si hay un cartel de esa
misma propiedad, ambos deben compartir ID. El jugador debe haber comprado esa
propiedad desde Tab > Propiedades. No hace falta hacer
el edificio hijo del solar. Collider en Trigger; escala del solar y origen `(1,1,1)`.
El origen fija el nivel de construccion actual y los ejes de la rejilla; solo rotacion Y.

## Controles

En construccion, WASD y raton permiten caminar y mirar normalmente; la pieza sigue
el centro de la camara. Mantener Alt (la tecla asignada a cambiar objetivo) libera
el cursor y detiene al personaje para usar el HUD. Al soltarla vuelve el control
del personaje sin perder la pieza seleccionada. El cambio entre interactuables
queda bloqueado durante todo el modo construccion. Tab siempre libera el cursor.
La presencia en el solar se comprueba con el centro de la capsula, no con el pivote
de los pies; los limites de colocacion de objetos siguen siendo estrictos.

- Tab abre/cierra el menu. Construir solo se habilita dentro de un solar propio construible.
- Pulsa Construir y cierra con Tab para ver el HUD derecho.
- Paredes/Muebles muestran el catalogo. Clic en un nombre selecciona la pieza.
- R gira 90 grados; rueda modifica la distancia; clic coloca solo si es verde.
- Escape termina la construccion sin abrir pausa en la misma pulsacion.
- Tambien se termina desde Tab > Salir de construccion.
- Acabado alterna colocar / textura original / pintura blanca / negra. En pintura,
  clic sobre una pared construida por el jugador cambia su acabado, no cristales o marcos.

Cada pieza reserva una celda 4x4. No se reescala el modelo para meterlo en esa celda:
tambien se comprueba su volumen real contra colliders y los limites del solar.
La comprobacion de volumen es conservadora: no encaja muebles dentro de huecos de otros.
Por ahora se construye en un solo nivel por solar; no incluye demolicion, coste,
arrastre de paredes, techo automatico ni sincronizacion multijugador.

## Editar UI, catalogo y materiales

- `Assets/Prefabs/UI/ConstructionUI.prefab`: Canvas editable.
- `Assets/ConstructionCatalog.asset`: piezas, limites medidos y acabados disponibles.
- `Assets/Materials/Wall_Building.mat`: textura proyectada en coordenadas del mundo,
  con repeticion cada 4 unidades. Las paredes contiguas comparten fase y escala.
  No depende de sus UV; la calidad de la costura al repetir depende de que la imagen sea tileable.
- `BuildSurface` define exactamente los slots que admiten pintura. No sustituye vidrio,
  madera de ventanas ni herrajes. Esta primera version cambia todos los lados pintables de la pieza.
- El catalogo usa GUID estables. No regenerar metas al renombrar prefabs.
- Tras anadir o modificar geometria, `Tools > Criminal Game > Construccion > Preparar sistema`
  actualiza catalogo/limites. Es una operacion de configuracion: restablece los acabados base
  de las paredes de la escena y guarda `10_World_City`; no usar para simples cambios de UI.

## Conservar construcciones

En una partida normal se incluyen al guardar, y se restauran al cargar esa escena.
Para crear el mundo desde Play: `Tools > Criminal Game > Construccion > Guardar
construccion en la escena`. Captura lo construido en la escena activa en ese instante.
Sal de Play: se crean instancias de prefab en la escena de edicion. **Ctrl+S** guarda;
**Ctrl+Z** deshace esa aplicacion. Salir de Play sin pulsar la opcion no cambia la escena.
Esta herramienta conserva colocaciones/acabados, no otros cambios de Play ni eliminaciones.
