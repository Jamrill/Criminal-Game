# Animaciones del personaje

Los ocho FBX utilizados tienen Humanoid / Create From This Model y un Avatar propio de Mixamo. El Animator del personaje conserva Player_HumanoidAvatar y su T-pose. El personaje usa el controlador PlayerLocomotion existente, con 13 estados y transiciones por CrossFadeInFixedTime (0.16 segundos).

| Entrada | Comportamiento |
| --- | --- |
| Sin movimiento | Standing Idle |
| W / S | Walking normal / inversa |
| A / D de pie | Left Strafe Walk / Right Strafe Walk |
| Clic derecho, de pie y parado | Pistol Aim inversa, después Pistol Idle en bucle |
| WASD durante la pistola | Guarda con Pistol Aim normal y después permite desplazarse |
| Control | Alterna agacharse / levantarse con Crouched To Standing inversa / normal |
| W / S agachado | Crouched Walking normal / inversa |
| A / D agachado | Giro sobre el propio eje; no desplazamiento lateral |
| Agachado y parado | Mantiene el primer fotograma de Crouched To Standing |

Las diagonales de pie utilizan la animación del eje de desplazamiento dominante. Agachado se puede avanzar/retroceder a la vez que se gira. El clic derecho es una pulsación, no necesita mantenerse. No se saca la pistola mientras se camina o se está agachado. Control durante la pistola solicita guardarla antes de agacharse. Una nueva pulsación de Control durante la transición queda pendiente hasta terminarla. No se permite levantarse con un obstáculo encima.

Las acciones de sacar/guardar y agacharse/levantarse detienen temporalmente la traslación. Se conserva la duración original de los clips (Pistol Aim: aproximadamente 7.1 segundos; Crouched To Standing: 0.633 segundos). Si se interrumpe el gesto de sacar, se guarda desde el punto alcanzado. La cápsula reduce su altura con la transición. No se salta durante esas acciones ni estando agachado.

`Assets/Animations/Generated/` contiene cuatro copias Humanoid con las curvas invertidas, incluidos tangentes y eventos. Los FBX originales se conservan. Estas copias evitan depender de velocidad negativa en clips sin bucle. `Texting And Walking` no se asigna a ningún control porque no forma parte de la lista solicitada.

Para regenerar después de sustituir algún FBX: Tools → Criminal Game → Configurar animaciones del jugador. Este comando reutiliza el Avatar válido del personaje y actualiza los clips, las copias inversas y el prefab Player.

Validación automática: `PlayerAnimationExpansion.ConfigureAndValidate` configura y comprueba; `PlayerAnimationExpansion.Validate` comprueba sin reconfigurar. Resultados en `Documentation/MixamoAnimationValidation.txt`. Estas pruebas verifican importación, estados, secuencias y retargeting de extremos; no sustituyen una revisión visual interactiva en Play.
