# Casa modular: modelos de primera versión

Abre `Casa_Modular.blend` para editar la biblioteca. `FBX/` contiene 24 recursos independientes, con 6396 triángulos en total (sin contar las copias de demostración). Las tres imágenes PNG muestran muebles, paredes y montaje de escaleras/ascensor. No se han sustituido modelos ni escenas de Unity.

## Escala

Un cubo básico de Blender mide 2 unidades de lado. La referencia del personaje es de 2 unidades de ancho y 4 de alto. Las paredes miden **8 unidades de alto**, equivalentes a cuatro cubos. La separación entre plantas es **8.4 unidades**, incluyendo 0.4 de forjado. Esta escala es intencional: no reducir automáticamente los muebles a medidas humanas reales.

Los muebles incluyen tres televisores (antiguo, moderno con patas y grande de pared), una mesa de centro y tres sillas de acabado progresivo. También hay un árbol de geometría simple. Materiales de colores planos, metal compartido y sin texturas externas.

## Paredes y puertas

Coordenadas siguientes expresadas en Blender: Z vertical. Los FBX se exportan con Y vertical para Unity; comprobar orientación al importarlos antes de montar automáticamente.

- Paredes rectas: longitud 4, altura 8, grosor 0.3. Origen al inicio inferior del eje de la pared; extremo opuesto en (4, 0, 0).
- Esquinas cuadrada y redondeada: empiezan en (0, 0, 0) y terminan en (2, 2, 0); dirección inicial +X y final +Y. La curva tiene radio 2.
- `Window_Insert` comparte el origen de `Wall_Window_4x8`: superponer ambos sin desplazar. Marco y vidrio son mallas separadas.
- `Wall_Door_4x8` y `Door_Frame` comparten origen. Colocar una de las hojas en (0.74, 0, 0) respecto a ellos. Su origen está en la bisagra: girar Z en Blender, el eje vertical equivalente en Unity. Las hojas sencilla y con paneles son alternativas, no se usan juntas.
- La cristalera tiene vidrio y estructura separados.

La pared antigua del proyecto se ha consultado únicamente como referencia. Estos módulos tienen dimensiones nuevas y no son sustitutos automáticos de los existentes.

## Escaleras y ascensor

- `Stairs_U_Floor`: origen delante, centrado y a nivel de suelo. Repetir a alturas 0, 8.4, 16.8... Cada módulo sube una planta con un rellano intermedio.
- `Stairs_Start_Landing`: colocar una vez en la base.
- `Lift_Shaft_Floor`: colocar en (0, 2.8, altura de planta) respecto al origen de las escaleras. Repetir verticalmente cada 8.4. Tiene hueco vertical abierto, sin suelos que bloqueen la cabina.
- `Lift_Landing_Doors`: mismo origen que el módulo del hueco, una pareja por parada. Ambas hojas son independientes.
- `Lift_Cabin`: mismo origen inicial que el hueco. Mover el conjunto verticalmente; sus puertas están separadas para animarlas lateralmente.
- Añadir un módulo de hueco por encima de la última parada para alojar la cabina y colocar `Lift_Roof_Cap` encima de ese módulo, no directamente sobre el suelo de la última parada.

El archivo Blender incluye un montaje de ejemplo. Son modelos, **no un ascensor funcional**: faltan lógica, animaciones y colliders. Las puertas correderas no incluyen mecanismos ni bolsillos interiores; revisar el recorrido lateral y sus solapes al animarlas. Para las escaleras conviene usar colliders de rampa y probar el paso del personaje; no plantear un paso bajo el rellano intermedio, que no deja altura suficiente para esta referencia de personaje.

## Importación y edición

Importar únicamente los FBX necesarios en Assets cuando se decida integrarlos. Los materiales de Blender pueden requerir adaptación a URP, especialmente el vidrio (Surface Type Transparent). No se han generado colliders, LOD ni UV para texturas o lightmaps. Los objetos estáticos están unidos por recurso, pero conservan varias islas de geometría y ranuras de material; una sola malla no implica una sola llamada de dibujo.

`asset_report.json` documenta dimensiones, piezas y triángulos. La generación comprueba escalas y caras degeneradas. Se han revisado las vistas previas, pero no se ha probado la integración jugable en Unity.

`build_house_pack.py` permite regenerar la biblioteca usando Blender en segundo plano. **Sobrescribe los archivos generados de esta carpeta**: conservar una copia separada antes de editar manualmente el blend si se pretende volver a ejecutar el generador.

## Referencias

Se consultaron proporciones generales de mobiliario IKEA, sin importar sus modelos ni texturas. Los diseños aquí son simplificaciones propias, no réplicas de productos concretos:

- https://www.ikea.com/es/es/p/havsta-mesa-centro-blanco-00404204/
- https://www.ikea.com/es/es/p/brimnes-mueble-tv-blanco-50409874/
- https://www.ikea.com/es/es/p/besta-mueble-tv-gris-oscuro-00538618/
