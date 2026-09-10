# Edificio revisado

## Actualización: kit completo y construcción modular

El mismo `Casa_Modular_Edificio_Corregido.blend` incorpora ahora la colección **KIT_COMPLETO_CONSTRUCCION**, situada a la derecha del edificio (desde X=100). La escena **11_KIT_COMPLETO_CONSTRUCCION** permite verla aislada. Se presenta una muestra por recurso utilizado, con etiquetas y subcolecciones: suelos, paredes, ventanas/puertas, escaleras/ascensor, muebles, lámparas y remates. Cada muestra tiene la propiedad `instancias_edificio` y conserva sus mallas hijas independientes, incluidas bisagras y cajones. Seleccionar el padre `MUESTRA_...` para copiar el recurso completo; seleccionar un hijo para editar una pieza móvil.

Los suelos continuos han sido sustituidos por **módulos geométricos de hasta 4 × 4 × 0.4 unidades**, con origen en la esquina superior y escala 1. Las juntas son geometría, no una textura sobre un plano grande. Hay remates menores para las dimensiones existentes del núcleo (por ejemplo 3.5, 3.8 o 1.9 unidades); son piezas específicas del edificio, no una cuadrícula universal de múltiplos de 4. Para un futuro sistema de construcción se podrá limitar su uso a remates automáticos o adaptar el núcleo a una única retícula.

Los tabiques interiores largos se han dividido en tramos de **máximo 4 unidades**, con piezas menores de cierre y rodapié doble. Los frisos largos de fachada también son tramos de 4. Las paredes con hueco, ventanas, marcos y hojas ya utilizados se incluyen en el catálogo. La escalera de una planta, la cabina y el hueco del ascensor se conservan como sus módulos funcionales completos, sin fragmentarlos arbitrariamente.

Se han añadido **lámpara de pie** (altura 3.85, origen en suelo) y **plafón** (origen en techo; geometría hacia -Z). Hay ejemplares instalados en el edificio y muestras en el catálogo. Son modelos: no añaden automáticamente luces en Unity ni lógica de encendido. Los plafones instalados están agrupados en `KIT_Lamparas_instaladas`; ocultar esta colección al inspeccionar una planta desde arriba si estorban.

`kit_report.json` enumera los recursos y sus usos, y comprueba que cada malla/texto del edificio tiene una muestra. No se incluyen cámaras ni luces de estudio como piezas construibles. Las escenas antiguas 01–05 y las copias bajas de presentación no definen el kit: la referencia es el edificio real de la escena 06.

Previews nuevas: `06_kit_completo.png`, `07_kit_suelos.png`, `08_kit_paredes.png` y `09_kit_lamparas.png`. Las previews anteriores corresponden a la revisión previa a modularizar.

Se conserva `Casa_Modular_Antes_Modularizacion.blend` como copia de seguridad anterior a este cambio. El script `../modularize_revision.py` realiza esta actualización incremental desde esa copia o una revisión todavía no modularizada. **No ejecutar `revise_building.py` sobre las salidas actuales si se quieren conservar estos cambios**, ya que ese generador reconstruye la versión previa.

Abrir `Casa_Modular_Edificio_Corregido.blend`, escena **06_EDIFICIO_CORREGIDO** (seleccionada al guardar). Se ha trabajado desde `Casa_Modular_antes_arboles.blend`; ni ese original ni `Casa_Modular.blend` se han sobrescrito. Las escenas 01–05 conservadas dentro de la copia son el montaje anterior, no la revisión.

## Distribución

- Huella principal ampliada de 64 × 28 a **72 × 32 unidades**; balcones y acera sobresalen. Persona de 4 unidades de alto, paredes de 8 y plantas cada 8.4.
- Planta 0: **cuatro comercios**, dos a cada lado del portal. Cada local tiene escaparate y puerta de vidrio a la calle, zona de venta y almacén independiente al fondo.
- Plantas 1, 2 y 3: dos viviendas por planta, seis en total, con dos dormitorios, baño y salón-cocina. Los salones tienen tres o cuatro aperturas exteriores contando la balconera; los dormitorios una o dos ventanas; cocina una; baños sin ventana.
- Las paredes entre viviendas/locales y núcleo común son opacas. No hay ventanas de habitaciones hacia pasillos ni escaleras.
- Rodapié a ambos lados en tabiques interiores; solamente en la cara interior de muros exteriores. Los rodapiés se interrumpen en los pasos de puerta.
- Numeración de las viviendas únicamente encima de su puerta de entrada, visible desde el pasillo. La fachada solo lleva nombres de los comercios.
- Cancela del portal con dos hojas y bisagras independientes: aproximadamente 7.2 unidades de ancho libre frente a las puertas domésticas de unos 2.56.
- Pavimento continuo en la zona común salvo los huecos necesarios para las escaleras y el ascensor. Las llegadas de escalera pertenecen al módulo del piso inferior: no borrar ese módulo al editar una planta.

## Muebles nuevos y animación

Cada recurso está agrupado mediante un padre vacío para moverlo completo. Las instancias comparten geometría, conservando el estilo de materiales planos del catálogo existente.

- `REV_Expositor_Pared`: carcasa hueca con baldas; dos hojas independientes (rotar Z local) y cajón con fondo y laterales (trasladar -Y local). Los expositores colocados en el edificio están cerrados; el catálogo muestra uno abierto.
- `REV_Expositor_Isla_Huecos`: estantes abiertos a ambos lados, no imágenes oscuras que simulen huecos.
- `REV_Mostrador`: parte posterior abierta con baldas; caja registradora colocada como objeto separado.
- `REV_Caja_Registradora`: cuerpo y cajón independientes.
- `REV_Cancela_Portal_8x8` y `REV_Escaparate_14`: hojas independientes con pivotes laterales para girar Z local en Blender.

No se han creado animaciones, lógica, colliders ni nuevos FBX. La integración en Unity queda pendiente. En Unity habrá que usar el eje vertical resultante de la importación para las bisagras y adaptar la transparencia del material de vidrio a URP.

## Escenas y previews

| Escena | Uso | Imagen |
| --- | --- | --- |
| 06_EDIFICIO_CORREGIDO | Edificio completo editable | 01_exterior.png |
| 07_PLANTA_0_COMERCIOS | Planta baja en corte | 02_planta_comercios.png |
| 08_PLANTA_1_VIVIENDAS | Planta residencial en corte | 03_planta_viviendas.png |
| 09_DETALLE_COMERCIO | Mobiliario y acceso al almacén | 04_comercio_detalle.png |
| 10_MODULOS_COMERCIO_Y_PAREDES | Nuevos módulos separados | 05_modulos.png |

Las paredes bajas de las escenas de corte son copias para presentación. **Editar las paredes reales en la escena 06**; las copias recortadas no se actualizan automáticamente. Los muebles sí comparten sus objetos con el edificio. En el Outliner se pueden ocultar `REV_Cubierta_Ocultable` y las plantas superiores para acceder a los interiores. La colección de plantillas `REV_MODULOS_EDITABLES` está oculta para no apilar modelos en el origen: utilizar el catálogo de la escena 10 para verlas.

## Comprobaciones

`revision_report.json` registra los objetos originales preservados, triángulos de módulos nuevos, recuentos de comercios/viviendas, muestras de superficie de suelo común y pares de muebles de comercios comprobados sin intersección de sus envolventes. También se comprueba que las placas miran al pasillo y que los tabiques interiores no tengan ventanas.

Son comprobaciones geométricas y visuales, no una prueba de navegación con el personaje en Unity ni un proyecto de construcción real. El recorrido completo de cada hoja al abrir debe comprobarse al animarla.

`../revise_building.py` regenera esta revisión y sobrescribe únicamente sus salidas en esta carpeta: guardar aparte cualquier edición manual antes de ejecutarlo de nuevo. Se apoya en las funciones geométricas de `../extend_building.py`, sin ejecutar su generador antiguo.
