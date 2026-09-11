# Calidad de reflejos

Implementación para Unity 6000.3 / URP 17.3. El desplegable **Reflejos** se añade al abrir Vídeo usando una copia visual de la fila VSync; funciona en el menú inicial y en pausa. No hay que conectar botones ni modificar escenas. Si se crea una fila propia, asignar `Reflections Button` y `Reflections Label` en VideoMenuUI para evitar la fila automática.

| Perfil | Captura local | Intervalo mínimo entre solicitudes |
| --- | --- | --- |
| Bajo | Desactivada; conserva cielo y sondas precalculadas | — |
| Medio | Cubemap de 128 px por cara | 5 segundos |
| Alto | Cubemap de 256 px por cara | 2 segundos |
| Ultra | Cubemap de 512 px por cara | 0.5 segundos |

El perfil general también configura este campo. Cambiarlo individualmente marca el perfil como Personalizado. `Video.Reflections` se guarda en PlayerPrefs, sin tocar partidas. Preferencias personalizadas anteriores usan Alto por defecto para el campo nuevo. No se modifica la configuración de AA, MSAA, resolución o VSync al cambiar únicamente reflejos.

## Captura y límites

VideoSettings crea `@RuntimeReflections` durante la ejecución. Solo captura cuando existe un LocalPlayerMarker activo, así que no crea una captura en el menú inicial. Usa una única sonda cercana al jugador, con volumen de 64 unidades y distancia máxima de captura de 96. En Play se pueden inspeccionar estos valores en ReflectionQualityController; los cambios realizados en ese objeto temporal no se guardan al salir de Play.

El trabajo se distribuye entre fotogramas. No se mueve la sonda ni se cambia su resolución hasta terminar la captura anterior; los intervalos son mínimos, no una garantía de frecuencia. No captura con el juego pausado. Desactiva y libera su sonda al seleccionar Bajo, perder al jugador o cargar otra escena. No modifica las sondas colocadas manualmente, sus texturas o materiales. El interruptor global de sondas en tiempo real sí sigue la calidad seleccionada y se restaura al finalizar la ejecución.

Esto son reflejos aproximados del entorno, no reflejos planares, SSR ni ray tracing. Los objetos en movimiento pueden verse con retraso y las capturas pueden presentar paralaje incorrecto cerca de paredes. Para habitaciones conviene colocar sondas específicas con límites ajustados y box projection; tendrán prioridad frente a la sonda móvil. Espejos y reflejos precisos del agua necesitarían otro tratamiento.

Los materiales tienen que admitir reflejos del entorno: por ejemplo URP/Lit con Environment Reflections habilitado y Smoothness suficiente. Un shader Unlit o un Shader Graph personalizado sin muestreo de reflexión no cambiará por activar esta opción. No se han alterado los materiales existentes ni el shader del mar para forzar brillos.

## Verificación en Unity pendiente

1. Entrar en Opciones > Vídeo desde menú y pausa: una sola fila Reflejos con cuatro opciones, dentro del scroll.
2. Elegir cada perfil general y comprobar el campo; cambiar solo Reflejos y comprobar Personalizado.
3. Salir de Play y volver: conserva la preferencia.
4. En el mundo, probar con un material URP/Lit brillante. En `@RuntimeReflections/LocalEnvironmentProbe`, comprobar 128/256/512 según el perfil.
5. Pasar a Bajo: desaparece la sonda local. Volver a Alto: reaparece tras reanudar la partida.
6. Cargar otra partida y volver al menú: no quedan capturas del mundo anterior ni gestores duplicados.
7. Medir el coste en Profiler y en un ejecutable; Ultra no garantiza un rendimiento adecuado en todos los equipos.

No se ha ejecutado Unity ni una compilación del proyecto para evitar acceder a Library, Temp, obj o Logs. Se ha revisado el código y la estructura serializada del menú.

Referencias oficiales: [Reflection Probes en URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/lighting/reflection-probes-introduction.html), [RenderProbe](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ReflectionProbe.RenderProbe.html), [IsFinishedRendering](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ReflectionProbe.IsFinishedRendering.html).
