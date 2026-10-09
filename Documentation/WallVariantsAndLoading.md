# Paredes, controles y preparacion de shaders

## Prefabs y variantes

Hay 9 familias y 27 prefabs en Completes, Semincompletes e Incompletes.
Los prefabs existentes conservan GUID y jerarquia funcional. La piel de pared se
regenera desde los FBX definitivos; las mallas adaptadas al espacio local del prefab
estan en `Assets/Construction/WallMeshes`. Ventanas y puertas funcionales existentes
se conservan. El material Wall_Building se aplica a los slots de pared, no al vidrio.

El catalogo oculta las variantes semi/incompletas, pero conserva sus IDs para cargar
partidas anteriores. La preview y las nuevas colocaciones usan completas.

WallTopology cambia la malla del objeto sin sustituir su identidad ni componentes:

- Extremos abiertos: completa.
- Todos los extremos conectados a paredes del mismo nivel: semincompleta.
- Ademas hay una pared coincidente encima: incompleta.
- Al desaparecer un vecino se recuperan las caras necesarias.

Esquinas y diagonales tienen dos extremos; T tiene tres y cruz tiene cuatro. Se
exigen todos para no eliminar tapas expuestas. Las conexiones admiten .06 unidades
de tolerancia. Altura actual: 7 unidades, obtenida de estas familias de FBX.
La rejilla horizontal sigue siendo 4x4. Las variantes no cambian los colliders.
Apuntar al tercio superior de una pared permite previsualizar otra encima; siempre
se comprueban el volumen del solar y las colisiones. No se incluyen pisos/techos automaticos.

Los acabados y el ID persisten. La variante se recalcula desde las vecinas al cargar;
la herramienta de conservar construccion de Play tambien la recalcula en edicion.

## Controles

La lista tiene encabezados Normales y Construccion. Movimiento/salto/carrera/Alt
comparten sus bindings originales (modificarlos afecta a ambos contextos).
En construccion se bloquean interaccion, inventario, agacharse, apuntar arma y cambio
de hombro/objetivo. Se mantienen mirar, colocar, WASD, salto, carrera, rueda, Alt y R;
Tab y Escape siguen disponibles para los menus y salir. Bajo techo bajo, levantarse
desde agachado sigue respetando la comprobacion fisica de espacio.

## Pantalla de carga

SceneLoader mantiene el panel hasta terminar la preparacion de los shaders cargados
con Shader.WarmupAllShaders y las colecciones de variantes configuradas. Despues
renderiza estados de mediodia, atardecer, noche y amanecer ocultos por la carga,
restaurando la iluminacion original y sin avanzar la hora del juego. Las capturas de
reflejos se suspenden durante esas muestras para no conservar el cielo de prueba.
En Editor, la compilacion asincrona se suspende durante esta preparacion y se espera
a la compilacion pendiente antes de retirar la pantalla. Los controles permanecen
bloqueados durante la carga.

Esto precalienta shaders **cargados**, no todos los recursos futuros del proyecto ni
todos los PSO posibles de cada GPU. DX12/Vulkan/Metal pueden requerir trazas de estados
graficos para garantizar cobertura en una build. La primera carga puede tardar mas.
No se ha confirmado visualmente que el problema nocturno desaparezca: si persiste,
hay que perfilar la primera transicion y distinguir compilacion, reflejos y luz azul real.

Referencia oficial: https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/shadervariantcollection/warmup
