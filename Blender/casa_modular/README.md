# Casa modular: biblioteca y edificio de cuatro plantas

`Casa_Modular.blend` incluye ahora un edificio completo de **4 plantas con 2 viviendas por planta**, construido con las piezas de la biblioteca original. La colección nueva es `EDIFICIO_4_PLANTAS`, en la escena `02_Edificio_4_plantas`. Se conservan los objetos originales en `01_Biblioteca_original` y una copia previa en `Casa_Modular_antes_edificio.blend`.

## Ampliación del edificio

### Árboles urbanos sin hojas

La escena `06_Arboles_urbanos_sin_hojas` contiene la colección `ARBOLES_URBANOS_6_VARIANTES`: seis árboles de malla continua con tronco, base ensanchada y ramas progresivamente más finas. Portes erguido oval, vaso abierto, bifurcado, inclinado suave, multitronco y compacto podado. Alturas de 9.2 a 10.3 unidades frente a las 9.1 del original; el personaje sigue midiendo 4.

Cada árbol tiene su origen al nivel del suelo, escala aplicada y está marcado como Asset. Cuatro copias se muestran alrededor del edificio. El árbol de la biblioteca original se conserva y sus antiguas copias del entorno están ocultas. La corteza utiliza un material procedural, sin imágenes externas; no hay hojas ni animaciones.

[Vista de las seis variantes](10_arboles_6_variantes.png) · [Detalle](11_arbol_tronco_detalle.png) · [Ejemplo en el edificio](12_edificio_arboles_urbanos.png) · [Referencias](REFERENCIAS_ARBOLES.md).

`Casa_Modular_antes_arboles.blend` conserva el archivo anterior a los árboles. `build_urban_trees.py` reproduce esta ampliación sobre esa copia previa, guardando en `Casa_Modular.blend`; conservar cualquier edición manual antes de ejecutarlo. `arboles_report.json` recoge tamaños y geometría.

### Escenas de arquitectura

- `02_Edificio_4_plantas`: edificio completo, plantas a cotas 0, 8.4, 16.8 y 25.2. Ocultar `Cubierta_OCULTAR_para_ver_planta_04` y las plantas superiores para trabajar dentro. Cada vivienda separa tabiques y mobiliario.
- `03_Planta_amueblada`: vista de distribución con tabiques recortados para presentación. Los muebles están vinculados a la planta inferior del edificio; los tabiques bajos son copias de presentación.
- `04_Catalogo_muebles_nuevos`: 25 módulos nuevos, agrupados por mueble y reutilizables. El frigorífico aparece abierto para mostrar el interior.
- `05_Nucleo_escaleras_ascensor`: tres tramos de escaleras, cuatro paradas y una cabina, aislados para inspección.

Las ocho viviendas tienen salón, cocina equipada, dormitorio doble, dormitorio individual con escritorio y ordenador, silla de oficina, mesitas, armarios, baño con lavabo, váter y ducha, y balcón con puerta corredera y muretes de media altura. El portal tiene ocho buzones. Hay sofás de dos y tres plazas, y losetas modulares de madera, terrazo y terraza de 4 × 4 × 0.4, además de un remate de 2 × 4.

**Piezas móviles:** las dos puertas del frigorífico son mallas independientes con el origen en la bisagra izquierda; girar Z local desde 0 hasta aproximadamente −110°. Los estantes de puerta pertenecen a la hoja superior. Para la corredera, desplazar `Corredera_Hoja_Movil` −3.8 en X local. Los armarios también tienen hojas separadas. Los muebles se mueven completos seleccionando su objeto padre. No hay animaciones creadas.

Vistas: [edificio](04_edificio_4_plantas.png), [planta](05_planta_amueblada.png), [catálogo](06_catalogo_nuevos.png), [frigorífico abierto](07_frigorifico_abierto.png), [vivienda](08_vivienda_detalle.png) y [núcleo común](09_nucleo_comun.png). Las [referencias IKEA](REFERENCIAS_EDIFICIO.md) documentan la inspiración y las diferencias de los modelos propios.

`edificio_report.json` recoge módulos, geometría y reutilización. `validate_building.py` comprueba el número de viviendas, las cotas, las piezas separadas y recorridos en planta para un volumen de personaje de 2 × 2 × 4, suponiendo puertas abiertas. Es una comprobación geométrica de distribución; no una prueba de navegación o físicas en Unity.

`extend_building.py` amplía una biblioteca **sin ampliar previamente** y guarda en `Casa_Modular.blend`. Para reconstruir esta versión, ejecutarlo sobre `Casa_Modular_antes_edificio.blend`, conservando antes cualquier edición posterior. El generador antiguo `build_house_pack.py` recrea solo la biblioteca inicial y **eliminaría la ampliación del archivo si se vuelve a ejecutar**.

## Biblioteca original y FBX existentes

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
