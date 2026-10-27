# Apoyo independiente de los pies

El prefab `Assets/Prefabs/Player.prefab` incluye `FootContact_Left` y `FootContact_Right`, hijos de los huesos de los pies. Sus BoxColliders se midieron sobre los vertices de la malla y compensan la escala interna de los huesos del FBX. Son triggers en Ignore Raycast: no sustituyen a la capsula del CharacterController ni bloquean al jugador o las interacciones.

`PlayerFootGrounding`, en el objeto del Animator, adapta cada pie mediante IK Humanoid. Detecta superficies solidas con SphereCast, ignora triggers y colliders propios, conserva el avance horizontal de la animacion y suaviza altura e inclinacion. El apoyo se consulta bajo la posicion animada, no bajo la posicion corregida del fotograma anterior, para evitar realimentacion junto a bordes. Se limita la diferencia de altura detectada y se ajusta la pelvis; el solver Humanoid resuelve el alcance de las piernas sin aplicar un ClampMagnitude propio que cambiaria tambien X/Z. Se libera el apoyo durante los saltos y cuando la capsula esta lejos del suelo. No cambia el sistema de movimiento ni el seguimiento de la cabeza.

## Correccion de la zancada

Se reprodujo un desplazamiento hacia el centro de unos 14-16 cm por pie incluso en el idle plano. El objetivo devuelto por `GetIKPosition` no coincidia con el tobillo de la animacion retargeteada en este avatar. Ahora se usa el Transform del hueso animado antes de resolver las piernas, conservando sus coordenadas horizontales. La correccion vertical es un desplazamiento relativo a la elevacion del terreno y ya no fija el pie a una cota constante durante su balanceo. En suelo plano se deja el peso efectivo de IK a cero para conservar exactamente la pose y rotacion de las animaciones originales; no se altera el Avatar, los clips ni el Animator Controller.

Se amplio la prueba en Play comparando un segundo Animator sin IK en las mismas fases de idle, marcha, marcha atras y carrera, tanto en suelo plano como sobre apoyo elevado, ademas de pausa/reanudacion. Los resultados detallados se guardan en `PlayerFootIKAnimationComparison.txt`.

## Inspector

- `Ground Layers`: capas de suelo, escaleras y objetos que pueden servir de apoyo.
- `Maximum Lift` / `Maximum Drop`: diferencias de altura permitidas; no hacen que la capsula pueda subir obstaculos mas altos.
- `Maximum Ground Angle` / `Maximum Foot Tilt`: limites de pendiente e inclinacion.
- `Sole Clearance`: separacion adicional sobre la superficie.
- `Foot Response` / `Pelvis Response`: rapidez de adaptacion.
- `Position Weight` / `Rotation Weight`: intensidad del ajuste.
- `Rest Sole Height`: calibracion del modelo actual, no altura del terreno.

No hay que enlazar nada manualmente: las referencias estan guardadas en el prefab. Los objetos necesitan colliders solidos en las capas seleccionadas. Un objeto dinamico tambien puede servir de apoyo; este componente no lo convierte en una plataforma movil para transportar al jugador.

Las escaleras actuales utilizan dos BoxColliders inclinados. Por ello el apoyo sigue una rampa continua. Para apoyar visualmente sobre cada peldano hacen falta superficies de colision por peldano; no se han cambiado las escaleras en esta tarea.

## Verificacion

`PlayerFootIKValidation.Run` comprueba sensores, escala, suelo, bordillo independiente, objetos con Rigidbody, pendientes, filtros y conservacion de componentes existentes. `PlayerFootIKPlayValidation.Start` verifica el callback real del Animator, la diferencia de altura entre pies sobre un bordillo de 24 cm, el retorno al suelo plano y la liberacion durante salto y en el aire. Antes ejecuta las regresiones de movimiento, giro y saltos. Sus informes estan junto a este documento. Las pruebas crean una escena temporal; solo deben ejecutarse con el proyecto guardado y sin una sesion interactiva abierta.

La configuracion se guardo con las APIs nativas de Unity sin evaluar una animacion sobre el contenido del prefab ni modificar las transformaciones originales del esqueleto. Copia anterior: `Documentation/PlayerFootIKBackup/20261020_100716/Player.prefab`.

API: [Animator](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.html), [OnAnimatorIK](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnAnimatorIK.html).
