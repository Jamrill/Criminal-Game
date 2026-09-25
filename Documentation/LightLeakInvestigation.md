# Light leaking: investigacion controlada

## Resultado recibido 25 septiembre, sesion 12:36

El usuario informa que ninguna prueba elimina la franja. Solo hay initial.txt y dos pares Baseline.txt/png guardados (12:36:44 y 12:37:23), sin registros A-F. Ambas imagenes contienen el menu de pausa y apuntan al suelo/ventanales; no reproducen el encuadre original del techo. No es posible atribuirlas a una prueba A/B.

Datos runtime confirmados: hora 9.197707; sol intensidad 0.8541545; sombras Soft, strength 1, bias 0.05, normal 0.2, near 0.2, pipelineBias=False. Por tanto el usuario SI estaba usando Custom Bias, a diferencia de la configuracion serializada consultada inicialmente. No debe seguirse atribuyendo su resultado a Use Pipeline Settings. Pipeline efectivo es un clon runtime PC_RPAsset (Video Settings). Lightmaps=0; light probe system=0. Hay dos plafones activos de intensidad 7.

Geometria: techos Floor en Y 107.99..108.01 (grosor 0.02), paredes en Y 101..108. Los AABB se solapan 0.01 en Y; no puede afirmarse una separacion vertical global. Esto tampoco demuestra continuidad de todas las caras. Los renderers registrados tienen cast On y receive True; las piezas citadas tienen determinante positivo.

La herramienta ahora guarda automaticamente cada prueba tras cinco frames y registra events.txt. Se anade G: luces directas + ambiente + reflejos + probes clasicos/lightmaps apagados, sin fog/post, conservando cielo y materiales emisivos/unlit para detectar superficies independientes de la iluminacion. No se ha cambiado la configuracion del juego. Se necesitan Base, A, C1 y G con el encuadre de la franja, sin menu de pausa. La herramienta anterior compilo y genero registros; esta ampliacion aun requiere recompilacion en Unity.

## Actualizacion: C1 identifica los reflejos; correccion aplicada, pendiente validacion visual

Sesion `20260925_124233888`: Base y C1 tienen identica camara/hora (12:01). En C1 desaparecen las franjas azuladas de las uniones. El sol sigue encendido, intensidad 1.149985, sombras Soft, strength 1, bias 0.05 y normal 0.2; ambiente Trilight conservado. ReflectionIntensity pasa a 0 y las probes se desactivan. Esto identifica el componente especular de entorno como causa de la franja observada, no una fuga de sombra directa. No distingue por si solo entre todas las probes; la configuracion incorrecta siguiente se ha identificado mediante inspeccion de escena/codigo.

La zona interior estaba centrada en (472,104,509), capturaba desde (472,103,509), junto a la interseccion de los limites oeste/norte de la sala, y usaba una caja de proyeccion 47x10x47 con mezcla de 2 metros. La sala principal ocupa aproximadamente x472..496, y101..108, z485..509. La proyeccion no correspondia a la sala, y la mezcla admitia contribucion exterior en superficies interiores. Los cubemaps no disponen de la oclusion por pared de una luz directa.

Correccion minima:
- `Assets/Scenes/10_World_City.unity`: solo configuracion de la zona interior. Centro (484,104.5,497), captura (484,104,497), volumen 24.5x7.2x24.5, blendDistance 0.05. El margen de 0.1 vertical y 0.25 horizontal coloca las superficies interiores fuera de la banda de mezcla exterior. BoxProjection e importancia 10 conservados.
- `Assets/Scripts/Core/BlendedReflectionCapture.cs`: las zonas esperan su primera captura propia; ya no publican un cubemap de cielo heredado como reflejo inicial interior. El cielo global conserva su mecanismo de inicializacion.
- Este informe. No se modifican modelos, materiales, sombras, bias ni luz solar. Los reflejos no se desactivan globalmente.

Validacion realizada: comparacion visual Base/C1, comprobacion de parametros registrados y limites de zona. Validacion pendiente: nueva ejecucion Unity, interior/exterior y pasos por puerta, mediodia/amanecer/atardecer; posible ajuste fino de proyeccion en huecos/particiones. La causa general esta demostrada; no se afirma haber visto ya el resultado de la correccion. Para futuras habitaciones se necesita una zona interior ajustada por recinto: no hay generador de construccion implementado que se pueda corregir en esta tarea. Una unica caja no representa varias habitaciones o plantas.

Documentacion: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/lighting/reflection-probes-introduction.html y https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ReflectionProbe-boxProjection.html

## Historial previo (hipotesis anteriores, no conclusiones actuales)

El informe previo calificaba Shadow Bias como hipotesis, no como causa demostrada. Las pruebas manuales del usuario no mostraron cambios relevantes. Se necesita comparar el mismo encuadre/hora aislando componentes antes de cambiar modelos o ajustes del juego.

## Fase 1: evidencia de codigo y escena guardada

- `Assets/Scripts/Environment/DayNightCycle.cs`: `Update` llama `ApplyLighting` cada frame. Cambia rotacion, intensidad, color y enabled del sol; intensidad y enabled de la luna; RenderSettings.sun (si hay material de cielo), ambientMode y colores sky/equator/ground. Tambien actualiza cielo, nubes y fogColor.
- NO escribe shadowBias, shadowNormalBias, shadowNearPlane, shadows ni shadowStrength. No se encontraron otras escrituras de estos campos en los scripts de Assets. La busqueda en Packages/ProjectSettings tampoco devolvio escrituras C#; no se inspecciono Library ni su cache de paquetes.
- Escena `10_World_City`: DayNightCycle.sun y RenderSettings.sun apuntan al Light local 636053354. Moon apunta a 910100002. Hora inicial 8, duracion 2 minutos reales por dia, intensidad solar maxima 1.15.
- A las 8, la formula de intensidad solar da aproximadamente 0.575; no es una medida runtime. La intensidad 1 guardada en Light se sustituye al arrancar y en cada Update.
- Sol serializado: Soft Shadows, strength 1, bias local 0.05, normal bias local 0.4, near plane 0.2. UniversalAdditionalLightData.usePipelineSettings = true.
- PC_RPAsset: depth bias 0.1, normal bias 0.5, resolucion 2048, distancia 50, 4 cascadas, splits 0.123/0.2926/0.536.
- Por tanto, modificar campos locales de bias sin seleccionar Custom no prueba los valores efectivos de URP. No significa que bias sea la causa.
- `ReflectionQualityController.LateUpdate` renueva probes de zonas y reflejo global. Con timeScale 0 retorna sin actualizarlos. Deshabilitar este componente destruiria cubemaps (`OnDisable` -> `Release`), por eso la herramienta NO lo deshabilita.
- PC_Renderer usa Forward+ y SSAO activo (intensidad 0.4, radio 0.3, influencia directa 0.25).

## Geometria verificada hasta ahora

- Wall_incomplete: dimensiones usadas por el sistema de colision 4 x 7 x 0.5; no debe confundirse el BoxCollider con un volumen que proyecta sombra.
- FBX inspeccionado con Blender: cinco poligonos, caras marcadas smooth pero normales de esquina con desviacion 0 grados respecto a las normales de cara. No se detecto sombreado curvo en esta pieza. Faltan caras respecto a una caja cerrada; no demuestra por si solo una fuga en la habitacion mostrada.
- Unity importa las normales (`normalImportMode: 0`). White_Paint no tiene metal ni emision, Smoothness 0.25.
- Aun faltan inspeccion de la union concreta, sus transformaciones runtime y pruebas de TwoSided, bloque y solapamiento. No se ha demostrado que uniones coincidan sin grosor, ni descartado huecos. No se encontro un generador de paredes en los scripts revisados.

## Pruebas A/B preparadas (NO ejecutadas desde esta sesion)

Abrir `Tools > Criminal Game > Diagnostico Light Leaks`.
1. Entrar en Play y situarse ante la fuga de dia. Dejar estabilizar reflejos. No usar Pause del Editor: debe seguir renderizando.
2. Pulsar `Congelar y registrar estado real`. Congela DayNightCycle y timeScale, fija la pose de Camera.main al renderizar. Mantiene el cielo diurno y las cubemaps existentes.
3. Guardar Base. Probar A (sol intensidad cero), B (ambiente negro), C1 (reflejos), C2 (GI difusa: ambiente + lightmaps por renderer + probes clasicos), D1 (post), D2 (SSAO). Guardar captura de cada una y esperar a que se escriba el PNG antes de cambiar.
4. Cada boton restaura primero la base: no se acumulan los cambios. C2 no certifica la desactivacion de APV ni de una GI personalizada; revisar el informe del pipeline si se usan.
5. E cambia temporalmente los casters activos de la escena a TwoSided, sin convertir los que estaban Off. El informe lista sus valores iniciales, limites, escalas, determinantes y lightmaps en un radio de 30 unidades de la camara.
6. F crea un cubo opaco temporal de 3 unidades hacia el sol respecto a la pieza seleccionada. Su posicion inicial es aproximada: colocarlo fuera de la union afectada sin tapar la camara. Retirarlo volviendo a Base.
7. `Restaurar y terminar`, cerrar ventana, salir de Play o recompilar restaura cambios. No guardar assets durante D2: SSAO se conmuta en memoria y se restaura; no se guarda en disco por la herramienta.

Capturas y estados se escriben en `Documentation/LightLeakDiagnostics/<fecha>/`.
Esta herramienta requiere un Game View activo, no una camara de Scene View. No hay acceso remoto a la ventana Unity en esta sesion para realizar/interpretar las capturas. No se ha validado aun su compilacion dentro de Unity.

## Fases pendientes antes de corregir

- Interpretar A/B y capturas runtime, separar luz directa, ambiente y reflejos.
- Medir uniones exactas y probar solapamiento sin z-fighting si las pruebas de geometria son positivas.
- Solo despues: variar resolucion, distancia, cascadas, soft shadows, bias Custom y near plane de forma independiente.
- Repetir prueba y eventual correccion a mediodia, amanecer y atardecer, restaurando y dejando estabilizar reflejos en cada hora.

## Cambios de esta investigacion

- Nuevo `Assets/Editor/LightLeakDiagnostics.cs` y meta: herramienta manual, solo Editor/Play, sin ejecucion automatica, no entra en la build.
- Este informe.
- Configuracion de produccion anterior = nueva. Ningun cambio adicional a escenas, prefabs, FBX, materiales, sombras o scripts runtime.

Referencia oficial: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shadows-troubleshooting-urp.html
