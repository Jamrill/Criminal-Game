# Construccion, valoracion y persistencia

## Uso

- `Solar_1` referencia `Assets/ConstructionCatalog.asset`. Cada entrada tiene un `price` editable en Inspector. Las variantes complete/semincomplete/incomplete tienen el mismo precio por familia.
- El campo `price` de BuildPlot es solo el valor base del terreno. El precio final que muestra la tablet suma los elementos registrados y no vendidos del solar, incluidos suelo, muebles, escaleras, puertas e impresora. Actualmente: **$10331** sin el modo gratuito.
- Construir descuenta el precio completo. X y clic venden el elemento por su precio completo provisional. Convertir una pared cobra/devuelve solo la diferencia. No se cobra retroactivamente por objetos que ya estaban en la escena.
- `Tools > Criminal Game > Construccion > Costes cero (desarrollo)` activa/desactiva compras, construccion, valoracion y ventas a cero. Se conserva el catalogo real; repetir la opcion restaura los precios. Inicialmente esta desactivado. Solo tiene efecto en Editor o Development Build, nunca en una build final normal.
- Para conservar la partida: **Guardar** desde pausa. El saldo y los objetos construidos/vendidos se capturan juntos. No hay autoguardado por cada clic. Cargar no devuelve objetos vendidos ni duplica construcciones.
- Para conservar lo construido en la propia escena de Unity: durante Play, `Tools > Criminal Game > Construccion > Guardar construccion en la escena`; salir de Play y Ctrl+S. Este flujo no debe confundirse con guardar la partida.

Se retiraron las entradas de Tools de migraciones y configuraciones antiguas, y la ventana redundante de construccion. Sus implementaciones internas siguen disponibles para no romper referencias entre utilidades. Solo quedan los dos comandos anteriores del proyecto.

## Suelo

Se completaron **55** losetas nuevas en la planta base Y=101, manteniendo el prefab Floor, su origen, materiales, colliders y paso 4 en X/Z. Hay **121** losetas base: origen X=452..492 y Z=485..525. No se modificaron las plantas superiores ni sus huecos de escalera. Los bordes de las losetas respetan el volumen del solar. Una segunda ejecucion de la migracion no agrego duplicados.

## Implementacion

`ConstructionEconomy` centraliza compra, venta y conversion de elementos. `BuildPlot.TotalValue` deriva el valor del estado real, evitando guardar un segundo total que pueda quedar desincronizado. Las eliminaciones se conservan como registros `deleted`, necesarios para ocultar objetos originales de la escena al cargar. Las conversiones conservan el identificador del objeto sustituido.

`ConstructionPersistence` restaura en la escena indicada y busca un solar unico por propertyId en esa escena. Las instancias nuevas, restauradas y capturadas por la herramienta del editor quedan bajo dicho solar, conservando transformaciones mundiales. Los solares nuevos deben tener propertyId unico y el catalogo asignado. Las carreteras/aceras heredadas de la escena no se han incorporado al catalogo de edificios.

## Verificacion

Unity batch compilo y completo **25 comprobaciones**: propiedad y saldo, precios, construccion de pago/gratuita, venta unica, conversion por diferencia, jerarquia, transformaciones bajo un solar escalado, serializacion de saldo y construcciones, restauracion de objetos vendidos, carga repetida sin duplicados y cuadrícula de suelo. Las pruebas usan servicios en memoria y JSON en Documentation; no modifican los slots reales.

Resultados: `Documentation/ConstructionValueValidation.txt`. Muestra JSON: `Documentation/ConstructionValueSaveSample.json`.

Copias nativas previas: `Documentation/ConstructionValueBackup/20261016_110651` (antes de completar suelo y registrar elementos) y `20261016_111041` (antes de incluir la impresora en la valoracion). No se han cambiado shaders ni ajustes de iluminacion en esta tarea.
