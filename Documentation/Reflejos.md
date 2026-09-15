# Reflejos por zonas (Unity 6000.3 / URP 17.3)

La captura que seguía al jugador se ha eliminado. VideoSettings conserva el desplegable,
PlayerPrefs y @RuntimeReflections; ahora este último coordina el cielo y ReflectionZone.
No se han modificado materiales, luces, agua, AA ni otras preferencias de vídeo.

## Prueba preparada en 10_World_City

Dos objetos raíz editables, visibles también fuera de Play:

- Reflections_Exterior_Test: centro (510,116,509), volumen (140,40,150), captura
  (510,104,509), transición espacial 18 m, prioridad 0, sin Box Projection.
- Reflections_Interior_Small_Property: centro (472,104,509), volumen (47,10,47),
  captura (472,103,509), transición espacial 2 m, prioridad 10, Box Projection.
  Corresponde al perímetro de Small_Property, cuyas paredes arrancan en Y=101.
  El volumen se extiende debajo del suelo para no perder la reflexión justo en él.

Es una prueba de una parcela, no una cobertura terminada de la ciudad ni una detección
automática de habitaciones. Comprobar visualmente límites y posibles obstáculos en
el punto de captura; si se subdivide la parcela, crear zonas por habitación.

Seleccionar el objeto y activar Gizmos: caja = influencia, esfera pequeña = captura.
Modificar Volume Size y Capture Offset, no la escala ni la rotación del Transform.
Las cajas están alineadas a los ejes del mundo. Configurar antes de Play; si se ajusta
durante Play, desactivar/reactivar el componente para regenerar su captura.
Duplicar estos objetos o añadir ReflectionZone a un objeto vacío para nuevas zonas.
No añadir además una ReflectionProbe manual al mismo objeto: se crean dos sondas
temporales como hijos, una de captura y otra que muestra el resultado.

## Fundido y presupuesto

BlendedReflectionCapture mantiene tres cubemaps HDR con mipmaps: anterior, siguiente
y resultado. Nunca publica caras a medio capturar. Solo una captura se genera a la vez
entre cielo y zonas; el filtrado se reparte entre fotogramas. Las zonas ya terminadas
pueden fundir simultáneamente sus imágenes. El resultado notifica sus cambios al atlas
de URP. No se mueve ningún origen siguiendo al jugador.

Las actualizaciones posteriores funden durante Transition Seconds (2 s en la prueba).
La primera captura puede aparecer al completarse cuando no hay un cubemap compatible
para inicializarla. Cambiar calidad recrea las capturas; no se garantiza un fundido
entre resoluciones distintas. La pausa detiene capturas nuevas y avance del fundido.

| Calidad | Resolución máxima por cara | Intervalo base mínimo |
| --- | --- | --- |
| Bajo | Capturas gestionadas desactivadas | — |
| Medio | 128 | 5 s |
| Alto | 256 | 2 s |
| Ultra | 512 | 0.5 s |

Cada zona puede limitar Max Resolution y exigir un Refresh Interval mayor (2 s en las
dos zonas de prueba). El intervalo se cuenta desde que termina una captura; también
se espera a terminar el fundido y a liberar el turno de captura. No son frecuencias
garantizadas. El cielo usa 128 y funde en 2 s. Se activa en escenas con DayNightCycle.
En Bajo se restaura la reflexión ambiental previa; las sondas manuales no se borran.

Tres cubemaps de 512 HDR con mipmaps ocupan aproximadamente 48 MiB por zona, sin contar
recursos internos de Unity/atlas. Ultra debe medirse. Antes de extender a toda la ciudad
será necesario presupuestar zonas activas/visibles y memoria; no duplicar cientos de
capturas en tiempo real con esta primera prueba.

## Verificación

Los tres scripts compilan sin errores con Roslyn y las referencias de Unity 6000.3.8f1,
usando sustitutos mínimos de VideoSettings/DayNightCycle. Se verificaron referencias
de los dos objetos nuevos y ausencia de IDs duplicados en la escena. Esto no equivale
a compilar el proyecto completo ni a validar el resultado gráfico en Unity.

Pendiente en Play:

1. Probar Alto, de día y de noche: caminar dentro/fuera de Small_Property y alrededor
   del spawn. No debe existir LocalEnvironmentProbe ni una zona que siga al personaje.
2. Observar un cristal/metal durante varios refrescos: transición gradual. Las zonas
   fijas siguen teniendo límites; ajustar su encaje según el resultado visual.
3. Pausar/reanudar; cambiar Bajo/Medio/Alto/Ultra; cargar otra partida y volver al menú.
   Comprobar Console, desaparición de recursos antiguos y ausencia de duplicados.
4. Medir GPU/memoria en Profiler y build. Revisar posibles diferencias del atlas de
   URP en la GPU del equipo. Si BlendCubemap falla se conserva el último reflejo y se
   emite un aviso, en lugar de sustituir imágenes bruscamente.

Son aproximaciones del entorno, no espejos exactos, SSR ni reflejos planares. Objetos
móviles pueden reflejarse con retraso. El fundido de capturas móviles puede producir
doble imagen temporal; no pretende reemplazar una técnica específica para espejos.

Referencias: [Reflection Probes en URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/lighting/reflection-probes-introduction.html),
[BlendCubemap](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ReflectionProbe.BlendCubemap.html),
[RenderProbe](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ReflectionProbe.RenderProbe.html).
