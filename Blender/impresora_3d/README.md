# Impresora 3D simetrica

Primera version low-poly inspirada en la referencia, con pantalla central ancha,
detalles azul petroleo y soporte central vacio. Sin bobina, cables ni texturas.

## Archivos

- `Printer_Symmetric.blend`: modelo editable; Mirror X activo en las cuatro mallas.
- `Printer_Symmetric.fbx`: cuatro mallas exportadas con Mirror y biseles evaluados.
- `preview.png`: vista previa; camara y luces solo estan en el blend.
- `build_printer.py`: generador reproducible. Ejecutarlo sobrescribe estos resultados.
- `model_report.json`: recuento evaluado, incluidos Mirror y biseles.

## Piezas y Unity

2640 triangulos en total. Dimensiones aproximadas: 46 cm de ancho y 74 cm de alto.
Unidades en metros; exportacion FBX Y arriba.

| Malla | Triangulos | Movimiento previsto en Unity |
|---|---:|---|
| Structure | 1392 | Estatica, origen al nivel del suelo |
| Rails | 296 | Vertical, eje Y |
| Head | 712 | Horizontal X y acompanamiento vertical de Rails |
| Base | 240 | Profundidad, eje Z; origen en superficie de impresion |

Las cuatro piezas se exportan como hermanas. Al integrar, Head debe seguir a Rails
mediante jerarquia o codigo, pero no ambos a la vez. No se ha sustituido el prefab
actual, ni conectado animaciones, camaras, UI, puntos de aparicion o colisiones.

La estructura es un solo objeto de malla con varias islas geometricas; no una
union booleana estanca. Se prioriza un recurso de juego ligero sobre fabricacion.
Cinco materiales planos compartidos. Varias ranuras de material pueden producir
varias llamadas de dibujo: una sola malla no garantiza una sola llamada.
Para muchas instancias, comprobar GPU instancing en materiales URP y perfilar.

Para importar, copiar el FBX a la carpeta de Assets que elijas, extraer materiales
y comprobar su shader URP/Lit. Metallic y Smoothness pueden necesitar ajuste en
Unity: FBX no garantiza una traduccion identica de los materiales de Blender.
El archivo .blend mantiene medios modelos: editar el lado positivo X y conservar
Mirror antes de Bevel. La imagen se ha comprobado en Blender, no en Unity.
