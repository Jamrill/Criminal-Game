# Velaro GT

Coupé ficticio inspirado en las proporciones de las cuatro vistas de la referencia
`../mesa_demo/Referencias/2025_maserati_granturismo_folgore.jpg`.
Carrocería azul metalizada, interior de cuatro plazas en tonos cuero/carbón y mecánica
estilizada propia. Sin logotipos ni texturas externas.

## Archivos

- `velaro_gt.blend`: modelo editable, materiales, estudio de iluminación y animación de apertura.
- `exports/velaro_gt.fbx`: modelo cerrado, en metros, sin luces, cámaras ni animaciones; preparado para Unity.
- `exports/velaro_gt.glb`: alternativa portátil con materiales PBR, también en pose cerrada.
- `velaro_closed.png`, `velaro_open.png`, `velaro_rear_open.png`, `velaro_interior.png`: vistas renderizadas del modelo real.
- `asset_report.json`: recuento exacto de triángulos, materiales y coordenadas de bisagras.

## Probar las piezas móviles en Blender

Abre `velaro_gt.blend`. El fotograma **1** está cerrado; el **45** muestra las dos
puertas, el capó y el maletero abiertos. La animación mantiene la apertura hasta el
75 y cierra en el 110. Usa Espacio para reproducirla.

Los objetos `Door_L`, `Door_R`, `Hood` y `Trunk` son las bisagras. Cada uno tiene una
malla hija independiente, cuyo origen también coincide con la bisagra. Ruedas,
cristales de las puertas, espejos y paneles interiores siguen su conjunto correcto.
Las cuatro ruedas tienen pivotes `Wheel_FL`, `Wheel_FR`, `Wheel_RL` y `Wheel_RR`.

En Blender, X recorre el ancho, Z apunta arriba y el frontal mira hacia -Y.
El centro del coche está sobre el suelo, con la escala expresada en metros.

| Pieza | Eje local Blender | Apertura |
| --- | --- | --- |
| Door_L | Z | +65° |
| Door_R | Z | -65° |
| Hood | X | -62° |
| Trunk | X | +70° |

El vano del motor y el maletero tienen fondo, paredes y revestimiento. No hay una
superficie cerrada de carrocería atravesando las aberturas. El motor es decorativo.

## Unity

Los archivos de integración están en `Assets/Vehicles/VelaroGT` dentro del proyecto.
El importador configura materiales URP y genera `Velaro_GT.prefab` al importar el
FBX si el prefab todavía no existe. También se puede ejecutar
**Tools > Velaro GT > Build prefab**.

Arrastra el prefab a una escena. En el menú contextual del componente
`VelaroOpenableParts`, **Open all parts** y **Close all parts** permiten inspeccionarlo.
En ejecución, `SetLeftDoor(1)`, `SetRightDoor(1)`, `SetHood(1)` y `SetTrunk(1)` abren
las piezas; el valor `0` las cierra. Admiten valores intermedios y apertura suave.

Incluye colisiones básicas del bajo y de las puertas. El control de conducción,
la interacción del jugador y la física de suspensión quedan a cargo del juego.
Es un único nivel de detalle; el recuento incluye interior, motor y ruedas.
No necesita mapas de color, normales o metalizado: usa nueve materiales uniformes.

## Regenerar

Desde la raíz del proyecto, en PowerShell:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' --background --factory-startup --python-exit-code 1 --python 'Blender\velaro_gt\build_car.py' -- --samples 48
```

Esto vuelve a crear el `.blend`, los dos formatos de exportación y las cuatro
imágenes en esta carpeta. Sobrescribe los resultados generados; guarda cualquier
edición manual del modelo con otro nombre antes de regenerarlo. La copia instalada
en `Assets/Vehicles/VelaroGT` se actualiza copiando de nuevo el FBX de `exports`.

`build_car.py` construye la carrocería y monta la escena. `interior.py` y
`mechanics.py` construyen el habitáculo, las ruedas y el motor.
