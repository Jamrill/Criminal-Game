# Colocación en el perímetro y sustitución de paredes — 2026-10-28

## Causas reproducidas

- La altura de una pared aceptaba cualquier superficie horizontal, incluido el terreno bajo los cimientos.
- El marco de `Door` tiene 0,6 de profundidad frente a los 0,5 del hueco. Centrado en una pared del perímetro, sobresalía unos 0,05 del solar y fallaba la validación.
- Las sustituciones exigían que el tramo elegido y el nuevo prefab midieran lo mismo: el tramo de 12 de `Glass_Wall_door_complete` no podía consumir paredes de 4.

La prueba previa reprodujo 31 fallos. Log: `PerimeterPlacementBaseline.log`.

## Cambios

- Las paredes toman su altura de suelos constructivos del mismo solar. Si el puntero alcanza terreno o mobiliario, se utiliza el nivel del suelo bajo el personaje; si no hay suelo, el nivel de cimientos configurado. La validación impide confirmar paredes bajo los cimientos. Los suelos de plantas superiores siguen siendo seleccionables.
- La puerta puede desplazarse mínimamente en profundidad hacia el interior del solar para absorber el exceso de su marco. No se desplaza lateralmente respecto al hueco ni se reducen colliders. Se siguen comprobando límites, obstáculos y puertas ya instaladas.
- Una pieza recta larga puede sustituir uno, dos o tres tramos colineales completos. Se prueban encajes que contienen el tramo apuntado; se prioriza conservar el trazado existente y después centrar la pieza cerca del punto señalado. El espacio restante puede estar vacío, pero toda la nueva pieza debe caber dentro del solar y no colisionar.
- Se conservan brazos de paredes compuestas mediante las piezas compatibles existentes. No se recortan parcialmente ventanas o puertas, ni se consumen paredes de otra altura o solar.
- Las puertas instaladas en los huecos consumidos se incluyen en esta sustitución larga, con devolución de su valor. Los cambios se realizan en una sola transacción y mantienen el sistema de variantes y registros de eliminación del guardado.

## Verificación

Unity 6000.3.8f1, escenas temporales sin guardar sobre las escenas del juego:

- `PerimeterPlacementValidation.Run`: **65 comprobaciones correctas**, incluyendo cuatro orientaciones, ambos lados de la puerta, esquinas, 1/2/3 tramos mixtos, alturas, obstáculos, precios, puerta instalada y serialización/restauración.
- `ConstructionMountValidation.Run`: 32 comprobaciones correctas.
- `WallFamilyValidation.Run`: 122 comprobaciones correctas.
- `ConstructionEditingUpgradeValidation.Run`: 242 comprobaciones correctas, además de sus regresiones anidadas de salas, techos grandes, variantes, montaje, valores y cimientos.
- Compilación del editor y ejecución batch terminadas con código 0. Log completo: `PerimeterPlacementFull.log`.

No se han modificado escenas ni prefabs para solucionar estos fallos. Estas verificaciones usan física y prefabs reales; no sustituyen la revisión visual del usuario durante su partida.
