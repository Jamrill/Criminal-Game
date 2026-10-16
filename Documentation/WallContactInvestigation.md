# Filo de luz al amanecer (16 octubre 2026)

Se reprodujo con los prefabs Wall_incomplete y Floor en una habitacion temporal cerrada, en coordenadas equivalentes a la escena. Un sol a 5 grados produce un filo al alejar la camara; al acercarla desaparece. Tambien ocurre con URP/Lit. Por tanto no es exclusivo del shader personalizado ni demuestra un fallo del controlador de camara.

El rayo correspondiente al pixel brillante golpea la pared en Y=107.984, con normal (0,0,-1), por debajo de la cara inferior del techo Y=107.990. Los pixeles inmediatamente inferiores quedan correctamente en sombra. No se trata de estar viendo la cara superior del suelo. Consulte ClosedRoomShadows/EdgePixelHits.txt.

Ensayos con bloqueador original, bloqueador mas ancho, sombras a dos caras, near plane del sol, una sola cascada y MSAA desactivado no eliminaron por si solos el filo en esta reproduccion.

## Correccion aplicada

WorldWall.shader dispone de Wall shadow contact offset (metres), guardado como _ShadowReceiverInset. Wall_Building.mat lo establece en 0.05. La posicion usada para consultar la sombra principal se desplaza hacia abajo sobre la superficie de las paredes verticales; no se modifica la geometria ni se desplaza hacia dentro de la normal. Un desplazamiento hacia dentro podria producir autosombra indebida en fachadas, y no es la solucion aplicada.

Es una compensacion de contacto para la precision finita de las sombras: puede desplazar ligeramente sus bordes sobre las paredes (hasta 5 cm). Se puede reducir o desactivar desde el material; no es una solucion universal para cualquier distancia o iluminacion futura.

## Proteccion de la base de la pared

Tras informar el usuario de una banda oscura inferior, se limita la compensacion para evitar consultar la sombra por debajo del suelo. Se comprueba que el origen del renderer coincide con la base en todos los prefabs y paredes actuales (WallShadowOrigins.txt). El desplazamiento nunca baja del origen mas 0.025 m y se desvanece suavemente en los primeros 0.20 m de pared. La geometria visible y la compensacion superior no cambian.

La regresion de doce combinaciones de sol/camara sigue dando cero pixeles luminosos bajo el techo. La banda inferior concreta del usuario no se ha reproducido en las comparaciones de escena ni en la pared aislada; las muestras tomadas antes/despues fueron iguales en esos encuadres. Por eso esta proteccion no demuestra aun que la banda informada este resuelta. Resultados adicionales: InteriorLightContacts/sun_before*, InteriorLightContacts/sun_after*, WallBaseShadows/Measurements.txt. Copia del shader previo a esta proteccion: WorldWall_before_base_fade.shader.txt.

Se elimino Floor_ShadowBlocker del prefab Floor y se comprobo que las instancias heredadas de la escena no conservaban bloqueadores huerfanos. No habia resuelto el filo y agregaba volumen de sombra innecesario. La malla visible, pivotes, colliders y altura de plantas se conservan. Los materiales mates de suelo y el atlas PC de 4096 permanecen como estaban tras la revision anterior.

## Validacion

ClosedRoomShadowValidation.FinalRun renderiza una habitacion con techo y suelo reales. Sol a 2, 5, 15 y 35 grados, camara a tres distancias. Las doce combinaciones finales dieron cero pixeles luminosos en la banda medida bajo el techo. La fachada exterior sigue iluminada. Resultados: ClosedRoomShadows/FinalMeasurements.txt; capturas final_angle*_distance*.png. La ejecucion de Unity termino con codigo 0.

La prueba aislada de foco no reprodujo la franja oscura informada: con bias global o local dio las mismas muestras de iluminacion cerca del suelo. No se han cambiado las luces interiores basandose en esa hipotesis. Queda por confirmar esa franja en el encuadre real, usando Tools > Criminal Game > Diagnostico Light Leaks. La herramienta registra ahora lente de camara y rotacion de luces y permite comparar bloqueadores y shader estandar con restauracion al terminar.

Copia de seguridad de material, prefab y escena: WallContactCorrectionBackup/20261016_075207. Copia del shader anterior al ajuste: WorldWall_before_receiver_test.shader.txt.
