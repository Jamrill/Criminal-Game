# Velaro GT en Unity

Preparado para el proyecto actual: Unity 6000.3.8f1 y URP 17.3.0.

1. Copiar `VelaroOpenableParts.cs` a `Assets/Vehicles/VelaroGT/Scripts/`.
2. Copiar `VelaroAssetSetup.cs` a `Assets/Vehicles/VelaroGT/Editor/`.
3. Copiar `velaro_gt.fbx` a `Assets/Vehicles/VelaroGT/velaro_gt.fbx`.
4. Unity crea automáticamente `Assets/Vehicles/VelaroGT/Velaro_GT.prefab` tras importar y compilar, si todavía no existe. También se puede usar **Tools > Velaro GT > Build prefab**.
5. Arrastrar el prefab a la escena. Probar **Open all parts** y **Close all parts** en el menú contextual del componente `VelaroOpenableParts`. En Play las piezas se animan; fuera de Play cambian inmediatamente.

La construcción usa una escena temporal de previsualización y no modifica ni guarda ninguna escena del juego. La reconstrucción manual sustituye el prefab y conserva los materiales `.mat` existentes; la construcción automática nunca sustituye un prefab existente. Si se modifica el FBX después de crear el prefab, reconstruirlo desde el menú.

## Interacción

El componente expone `SetLeftDoor(float)`, `SetRightDoor(float)`, `SetHood(float)` y `SetTrunk(float)`: 0 cierra y 1 abre, y admite posiciones intermedias. Ejemplo desde el sistema de interacción del juego:

```csharp
using JuegoCriminal.Vehicles;

// Referencia al componente del prefab.
carParts.SetTrunk(1f);
carParts.SetLeftDoor(0f);
carParts.CloseAll();
```

No necesita Input System ni un sistema de conducción. El movimiento usa `Time.deltaTime` y se detiene al pausar el tiempo del juego. En el Inspector pueden ajustarse `Opening Seconds` y el vector `Open Euler` de cada bisagra. Los ángulos parten de la pose cerrada guardada; **Capture current transforms as CLOSED pose** solo debe utilizarse con las cuatro piezas cerradas.

Los objetos `Door_L`, `Door_R`, `Hood` y `Trunk` son pivotes independientes. Los valores iniciales corresponden a la exportación configurada: Door_L Y +65°, Door_R Y -65°, Hood X -62°, Trunk X +70°. Verificar los ejes locales y la apertura al cambiar las opciones de exportación FBX; los ángulos son configurables. El coche utiliza metros y su frente apunta a +Z en Unity.

## Materiales y colisión

Nueve materiales URP/Lit sin texturas: pintura metálica azul, cuero marrón, molduras oscuras, aleación, cristal transparente, luces blancas/rojas, neumáticos y pantalla. Los materiales se crean en `Assets/Vehicles/VelaroGT/Materials/`; pueden editarse allí. La iluminación y los reflection probes de cada escena afectan al acabado metálico.

El prefab lleva un BoxCollider bajo la carrocería y uno en cada puerta que acompaña su giro. Son colisiones básicas para interacción; no incluye Rigidbody, WheelColliders ni conducción. Adaptar la colisión y física al sistema de vehículos antes de usarlo como coche conducible.

## Validación sin interrumpir el proyecto

No lanzar Unity en batch con este proyecto si ya está abierto. Para comprobar el importador automáticamente, usar un proyecto temporal independiente con la misma versión de Unity, URP y estos archivos. El menú de creación también funciona en la sesión de Unity existente sin cambiar de escena. El archivo FBX conserva los pivotes y puede usarse sin los scripts.
