# Tablet y acceso contextual a propiedades

## Funcionamiento

- Tab (reasignable en Controles) abre y recoge la tablet. El mismo panel se desplaza desde la esquina superior derecha hasta el centro; recogido solo muestra dinero bancario, hora y notificaciones.
- Seis aplicaciones: Contactos, Asuntos legales, Mercado negro, Propiedades, Investigación y Mapa. Cada botón activa exclusivamente su panel derecho.
- Propiedades permite seleccionar un inmueble, consultar su precio y confirmar su compra. Abrirlo no cobra nada. La confirmación comprueba propiedad, saldo e identificador único; no permite cobrar dos veces.
- Dentro de una propiedad aparece abajo a la derecha **B Comprar** o **B Construir**. Comprar abre directamente los detalles del inmueble; Construir abre el catálogo sin pasar por la tablet. La acción está en Controles > Construcción y respeta la tecla reasignada.
- Banco y notificaciones empiezan en 0. El banco es un dato reservado, no una copia del efectivo. Las compras siguen usando el servicio económico existente.
- Las otras cinco aplicaciones, la fotografía y la descripción del inmueble quedan como espacios preparados para próximas funcionalidades, con textos provisionales traducidos.

## Edición en Unity

- `Assets/Prefabs/UI/ConstructionUI.prefab`: tablet persistente, paneles, datos y aviso contextual. El Player hereda esta interfaz.
- `Assets/Prefabs/UI/Button_Tablet.prefab`: estilo exclusivo de la tablet, independiente de los botones del menú. Colores de fondo en Button y colores del texto por estado en ButtonTextStateColors. Las aplicaciones y la confirmación mantienen referencias a este prefab.
- En TabletView: `slideDuration` regula la transición y `dockMargin` el margen del bloque de datos. `accent` controla el indicador de aplicación seleccionada. Los demás colores y textos están en sus componentes UI.
- `SetBankBalance(int)` y `SetNotificationCount(int)` permiten alimentar los campos futuros. La hora procede de DayNightCycle de la escena.
- Las nuevas aplicaciones deben usar LocalizedText o GameLanguage y refrescar sus textos cuando cambie el idioma, siguiendo los paneles existentes.
- Los antiguos indicadores de hora y efectivo de GameplayHUD en World City están desactivados para no superponerse al bloque de datos de la tablet.

## Validación y mantenimiento

- `TabletValidation.Run`: prefab real, seis paneles, español/inglés, compra única, acción contextual, herencia del Player y encuadre recogido a 1600×1000, 1920×1080 y 1280×720. Capturas en `Documentation/TabletOpen.png` y `TabletDocked.png`.
- `TabletPlayValidation.Start`: prueba aislada en Play de la transición, reloj, compra, entrada/salida de construcción, cambio de idioma y aviso al entrar/salir de la propiedad. No carga ni guarda partidas.
- `OptionsNavigationPlayValidation.Start`: navegación de opciones y fila localizada de Construir / Comprar propiedad.
- `TabletSetup.Apply` es una migración inicial, no debe ejecutarse para actualizar colores: reconstruye el diseño. Antes de modificar guarda copias de ConstructionUI, Player y World City en `Documentation/TabletBackup/`.

Implementación nativa de Unity UI, inspirada en la distribución negro/dorado de la referencia; no es una imagen estática del menú.

Validación completada el 2026-11-05: 34 comprobaciones del prefab, 13 en Play y 54 de navegación/opciones correctas. Regresión `FloorDragLandingValidation.Full` finalizada con código 0 (log `TabletConstructionRegression.log`). Unity queda cerrado al terminar las pruebas.
