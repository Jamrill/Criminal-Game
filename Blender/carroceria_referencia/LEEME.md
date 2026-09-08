# Carrocería exterior — revisión 04

Abre **carroceria_referencia.blend** en Blender 5.1. La escena abre con la cámara de tres cuartos trasera para revisar la zaga.

Cambios de esta revisión:

- Retrovisores sustituidos por carcasas más bajas y alargadas, brazos finos, espejos reflectantes independientes e intermitentes estrechos.
- Hueco real para la matrícula, con bisel, fondo, placa provisional sin texto y luces superiores. Queda dentro de la tapa del maletero.
- Reflectores del paragolpes, sensores enrasados y aletas discretas en la zona inferior del difusor.
- Cercos de pilotos más oscuros y guías luminosas menos gruesas.
- Seis cristales separados de la carrocería, con juntas de asiento y espesor pendiente de ajustar junto con los marcos interiores. Su colección permite ocultarlos y trabajar posteriormente en el habitáculo.

Se conserva el trabajo anterior de techo, aletas, capó, taloneras, faros con alojamientos, parrilla y tomas con profundidad, juntas y escapes huecos.

## Preparación para el interior

La carrocería tiene ahora aberturas reales donde se asientan los seis cristales. El vidrio continúa visualmente oscuro mientras no exista habitáculo; la transmisión se ajustará al modelarlo. Las puertas, el capó y la tapa trasera siguen estáticos. Todavía faltan los retornos y espesores de los paneles, marcos interiores, suelo y revestimientos: esta revisión no representa una estructura de vehículo terminada ni mecanismos funcionales.

## Vistas y archivos

Hay seis cámaras y seis PNG actualizados: lateral, frontal, trasera, superior y ambas perspectivas de tres cuartos. Las colecciones separan carrocería, ruedas, estudio, acabados y acristalamiento.

La batalla es de 2,96 m, el diámetro de neumáticos de 0,73 m y la anchura de carrocería de aproximadamente 2 m. Son proporciones inferidas de la referencia, no cotas de fabricación. Las ruedas siguen siendo provisionales.

Las revisiones anteriores se conservan en `historial/base_v01.blend`, `historial/refinamiento_v02.blend` y `historial/refinamiento_v03.blend`. No se ha modificado el proyecto anterior `../velaro_gt/velaro_gt.blend`.

No hay interior, motor, mecanismos, animaciones ni scripts incrustados. `revision_geometria.json` comprueba la reapertura y la continuidad del conjunto carrocería/cristales, reuniéndolos solo en memoria para la comprobación. Los bordes abiertos de la carrocería corresponden a las ventanas separadas. La validación geométrica no certifica por sí sola la fidelidad visual.

