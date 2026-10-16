# Demolicion y conversion de paredes

- En construccion, X alterna demolicion. El objetivo propio se muestra rojo; clic izquierdo lo elimina. Alt bloquea la accion mientras se usa el HUD.
- La accion es configurable en Controles > Construccion > Demolish Building.
- Las entradas normales Wall_door_hole / Window_Wall se sustituyen por acciones Convertir pared. Requieren una pared recta compatible, conservan transform, identificador y acabado guardado, y comprueban colisiones excluyendo exclusivamente la pared sustituida.
- Solo son editables objetos con PlacedBuildObject, definitionId del catalogo y propertyId del solar actual. No se buscan objetos arbitrarios recorriendo hasta la raiz Solar.
- La migracion ha registrado 71 instancias existentes del catalogo bajo Solar, sin moverlas. Los objetos ajenos al catalogo no se hacen destructibles automaticamente.
- SaveData v7 incorpora BuildRecord.deleted. Las demoliciones conservan un registro inactivo para que los originales de escena no reaparezcan al cargar. Los guardados anteriores siguen siendo compatibles: deleted es false por defecto.
- La captura explicita del editor tambien incluye registros inactivos y sustituye el prefab cuando cambia definitionId. Usar durante Play: Tools > Criminal Game > Construccion > Guardar construccion en la escena; detener Play; Ctrl+S. Undo permite deshacer la aplicacion.

## Reflejos

Reflections_Interior_Small_Property: volumen de (24.5,7.2,24.5) a (24.9,7.6,24.9); Blend Distance de 0.05 a 0.15. Se mantienen posicion, captura, prioridad y tiempos. Esto desplaza la zona de transicion un poco hacia fuera, pero NO demuestra que el filo sea causado por la sonda: falta comparar visualmente durante el dia. Si persiste, aislar reflejos frente a sombras directas/geometria antes de ampliar mas el volumen.

Copia previa de la escena: Documentation/ConstructionEditingBackup/20261015_114826/10_World_City.unity.
Pruebas: ConstructionEditingSetup.Validate, resultados en Documentation/ConstructionEditingValidation.log. No ejecutar Run de nuevo para probar: modifica la escena; Validate usa una escena temporal.
