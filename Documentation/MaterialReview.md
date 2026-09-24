# Revisión de materiales

Se revisaron 31 materiales propios y se creó Luz_Lampara_Calida. No se modificaron los materiales de SimpleSea, el cielo, TextMesh Pro ni los paquetes de texturas descargados. No se eliminaron imágenes: solo sus referencias en los materiales revisados.

Copias de los materiales anteriores: MaterialReviewBackup. Los GUID de los materiales existentes no se cambiaron.

| Material | Metallic | Smoothness | Mapas retirados |
| --- | ---: | ---: | ---: |
| Asphalt White | 0 | 0.12 | 1 |
| Asphalt | 0 | 0.08 | 1 |
| Border | 0 | 0.15 | 0 |
| Bordillo | 0 | 0.15 | 1 |
| Brick_wall | 0 | 0.12 | 3 |
| Concrete_Floor | 0 | 0.18 | 3 |
| Césped | 0 | 0 | 3 |
| Glass | 0 | 0.9 | 0 |
| Grass | 0 | 0 | 0 |
| Grass_2 | 0 | 0 | 0 |
| Handguard | 1 | 0.5 | 0 |
| knob | 1 | 0.6 | 1 |
| Leather | 0 | 0.3 | 0 |
| Luz roja | 0 | 0.45 | 0 |
| Luz verde | 0 | 0.45 | 0 |
| Luz ámbar | 0 | 0.45 | 0 |
| Metal_Brushed | 1 | 0.42 | 1 |
| Metal_Container | 0 | 0.35 | 1 |
| Negro | 0 | 0.08 | 0 |
| Plastic | 0 | 0.28 | 3 |
| Plastic_2 | 0 | 0.42 | 1 |
| Public_Metal | 1 | 0.45 | 0 |
| SideWalk | 0 | 0.12 | 3 |
| Signal | 0 | 0.4 | 0 |
| Steel | 1 | 0.7 | 0 |
| Steel_2 | 1 | 0.45 | 0 |
| Wood | 0 | 0.28 | 3 |
| Wooden_Planks | 0 | 0.22 | 3 |
| Black_Paint | 0 | 0.25 | 1 |
| White_Paint | 0 | 0.25 | 1 |
| Scuffed_cement | 0 | 0.12 | 3 |

Las luces roja/verde/ámbar tienen emisión HDR con RGB multiplicado por 3. Luz_Lampara_Calida usa emisión (4, 3.44, 2.6), sin textura. Se habilita emisión para GI horneada, pero no se ha ejecutado un bake. Para iluminar dinámicamente, añadir Point Light o Spot Light a la lámpara. Bloom puede dar halo visual, pero no ilumina objetos.

Glass conserva su transparencia y smoothness 0.9. Grass y Grass_2 conservan sus colores y acabado mate. Los materiales que eran blancos porque dependían de una textura reciben colores planos de referencia. Metal_Container se considera metal pintado: la capa visible no se configura como metal desnudo.

Verificación: revisión de propiedades serializadas, ausencia de referencias a texturas en los 32 materiales finales y emisión habilitada en los cuatro materiales de luz. Pendiente la comprobación visual en Unity con iluminación diurna y nocturna. Los valores son ajustes artísticos iniciales, no una calibración física.

Referencia: https://docs.unity3d.com/6000.1/Documentation/Manual/StandardShaderMaterialParameterEmission.html
