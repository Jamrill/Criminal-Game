# Revision de iluminacion (25 septiembre 2026)

## Brillos interiores durante el dia

Hipotesis principal: fugas de sombra solar en bordes de geometria fina/incompleta.
No confirmada visualmente en Unity. No se han cambiado sombras ni reflejos.

- PC_RPAsset: Normal Bias 0.5, Depth Bias 0.1, distancia 50, cuatro cascadas, mapa 2048.
- El sol de 10_World_City usa los ajustes globales de URP.
- White_Paint: Metallic 0, Smoothness 0.25, sin emision.
- Inspeccion FBX de Wall_incomplete: aunque las caras estan marcadas smooth, las normales importadas de las esquinas coinciden con las de las caras (desviacion 0 grados). No se observa suavizado curvando esas caras.

Prueba recomendada (sin guardar hasta comprobar): en el sol, Shadows > Bias > Custom, mantener Depth 0.1 y probar Normal 0.1. Comparar el mismo encuadre/hora. Si mejora, afinar entre 0.1 y 0.2 comprobando acne de sombras. Si persiste, probar Cast Shadows > Two Sided en la pieza afectada y comprobar uniones de techo/pared y caras ausentes. No aumentar brillo ni desactivar reflejos como solucion a una fuga de sombra.

Referencia: https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shadows-troubleshooting-urp.html

## Luces modificadas

- Prefab Furniture/Light_point: intensidad 4 -> 7, alcance 16 -> 24, cono 115 -> 150 grados, cono interior 75 -> 110.
- El semaforo en 10_World_City es una instancia directa de Traffic_light.fbx, no un prefab propio. Se han anadido diez focos hijos situados en las diez lentes emisivas (incluidos peatones), alcance 18, intensidad 4, cono 130/interior 85, sombras suaves. El FBX y los materiales existentes permanecen intactos.
- Los colores permanecen simultaneamente activos, como las superficies emisivas actuales. No se implementa un ciclo de trafico. Al implementarlo deberan sincronizarse Light.enabled y la emision de cada lente.
- Los focos del semaforo son overrides de esa instancia en escena: arrastrar el FBX de nuevo no incluye esas luces.
- Varias luces con sombras tienen coste de renderizado: revisar rendimiento al multiplicar semaforos; el renderer PC usa Forward+.

Validacion: posiciones medidas desde geometria FBX, referencias YAML verificadas. Pendiente prueba visual y de rendimiento en Unity.
