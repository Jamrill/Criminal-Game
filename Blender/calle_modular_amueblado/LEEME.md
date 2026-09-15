# Calle Modular: muebles y salón

Archivo principal: `../Calle Modular.blend`.
Copia del archivo original: `Calle Modular_antes_muebles.blend`.

## Colecciones

- Las colecciones originales y sus mallas no se han alterado.
- `CM_01_Sillas`: silla de roble y asiento tapizado, modelo principal.
- `CM_02_Salon_Objetos`: sofá, mesa de café, mueble de TV, TV independiente,
  alfombra y lámpara. Catálogo situado al lado de los originales.
- `CM_03_Escalera_7m`: escalera en U con descansillo y barandillas.
- `CM_04_Salon_Montado`: ejemplo de distribución con seis sillas y copias enlazadas.
- `CM_05_Arquitectura_Demo`: 30 tiles de suelo de 4x4, paredes y ventanas enlazadas
  a las originales, más un tile superior de llegada a la escalera.
- `CM_90_LOD1_solo_exportar` y `CM_91_LOD2_solo_exportar`: ocultas en viewport/render
  para que no se superpongan a LOD0. Activar una y ocultar LOD0 para comparar.
- `CM_99_Preview`: cámara y luces de presentación; no exportar a Unity.

El salón está desplazado a X=30..54, Y=0..20, sin techo y con dos paredes abiertas
para inspeccionarlo. No es un edificio terminado. Los muebles del catálogo y los
montados comparten datos de malla; editar una malla modifica sus copias enlazadas.

## Medidas y orígenes

Se usa este archivo como referencia: persona de 4 de alto, mesa de 2.25,
paredes de 7 y tile de 0.02. Las plantas se repiten a Z=0,7,14... No a 7.02.
La escalera tiene 28 contrahuellas de 0.25 y dos tramos de 14; descansillo en 3.5
y última huella en 7. La barandilla sobresale por encima de 7, como corresponde.
Ancho nominal total 8, dos tramos de 2.8 y hueco central de 2.4.
El tile de llegada está a Z=7. Falta el resto de la planta superior a propósito.

Los objetos nuevos usan origen en la esquina máxima X/Y de su huella y Z=0,
siguiendo la convención de Suelo. Sus mallas se extienden hacia X/Y negativos.
Cada familia comparte exactamente el mismo origen entre sus tres LOD.
La mesa mantiene exactamente el origen y la transformación originales.
No se modifican Ext/Int ni los índices de material de ninguna pared original.

## Triángulos por objeto

| Objeto | LOD0 | LOD1 | LOD2 |
| --- | ---: | ---: | ---: |
| Mesa original | 2252 | 1080 | 450 |
| Silla | 956 | 340 | 108 |
| Sofá | 4136 | 1516 | 564 |
| Mesa de café | 608 | 200 | 72 |
| Mueble TV | 1012 | 372 | 96 |
| TV | 696 | 168 | 72 |
| Alfombra | 788 | 92 | 12 |
| Lámpara | 416 | 180 | 84 |
| Escalera | 2108 | 1468 | 684 |

Los LOD de muebles nuevos simplifican geometría y eliminan detalles pequeños.
Los de la mesa son reducciones de una copia; el original no se ha decimado.
Las mallas tienen materiales de color sencillo; algunos objetos usan varios slots,
así que una malla unida no implica una única llamada de dibujo.

## Unity: pendiente de importar/configurar

No se ha modificado Assets ni se han creado LODGroup en Unity.
Al exportar, seleccionar solo la familia LOD0/1/2, con el mismo origen, no el salón
ni las luces. En el caso de la mesa, `Table` es el LOD0.
En Unity poner los tres renderers en un LODGroup. Como punto de partida visual,
probar transiciones de altura relativa de pantalla 0.45 / 0.18 / 0.03 y ajustar
según el tamaño del objeto y la cámara; no son distancias fijas en metros.
Los colliders no deben cambiar con el LOD visual. Para la escalera usar un collider
estable, por ejemplo rampas simplificadas, al integrarla en construcción.
Las puertas del mueble TV son detalle estático de la malla, no hojas animables.
No se ha hecho UV unwrap ni bake de texturas para los muebles nuevos.

## Verificación y preview

`Validation.json` verifica las 13 mallas originales, sus coordenadas, transformaciones
e índices de material; orígenes coincidentes y triángulos decrecientes en los LOD;
31 tiles y llegada a Z=7. Las previews se renderizaron y revisaron en Blender.
Todavía se debe probar el cambio visual entre LOD y los colliders dentro de Unity.

`Salon_preview.png`: conjunto. `Salon_detalle.png`: zona de estar y frontal del TV.
Las imágenes externas de los materiales originales siguen siendo responsabilidad
del archivo original; no se han sustituido ni empaquetado.
