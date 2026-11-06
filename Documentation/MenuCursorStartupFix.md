# Cursor del menú al iniciar Play

Síntoma confirmado por el usuario: cursor invisible en el menú principal; Escape lo libera fuera de Game View. La política anterior dejaba que ThirdPersonController.Start lo bloqueara de forma incondicional, sin restauración explícita al entrar en el menú.

## Cambio

- GameStateMachine libera y muestra el cursor en Boot, Loading y Menu. Solo solicita capturarlo y ocultarlo cuando el mundo termina de prepararse y entra en World.
- MainMenuUI asegura cursor libre/visible al habilitarse, al arrancar y al recuperar el foco. Mientras corresponde al menú, su LateUpdate corrige un posible estado residual de componentes que se estén cerrando. No interviene cuando GameStateMachine está en World.
- ThirdPersonController.Start deja la captura inicial a la máquina de estados. Un jugador creado tardíamente tampoco puede robar el cursor de pausa, inventario o tablet. Se mantiene el comportamiento de las escenas aisladas de prueba de gameplay, excluyendo las que tienen menú principal.
- No se modifican las teclas, escenas, prefabs, partidas ni la gestión normal de pausa/inventario/tablet.

## Validación

`MenuCursorPlayValidation.Start` inicia Play dos veces consecutivas en el mismo editor, con recarga normal de dominio y arranque real del menú. Comprueba el estado inicial, un bloqueo/ocultación residual inyectado, recuperación de foco, jugador iniciado tarde, estados Boot/Loading/Menu/World, preservación de un cursor libre para overlays y retorno al menú. No abre ni crea partidas.

El editor en batch no tiene una Game View con foco y puede rechazar el bloqueo físico del ratón. Esa limitación queda registrada en el informe; se comprueba la visibilidad y que el menú no altere la política de World, pero el movimiento/captura física debe confirmarse en una sesión interactiva. Esta corrección también se aplica a un ejecutable; no depende de pulsar Escape en el editor.

Resultados: `MenuCursorPlayValidation.txt` y `.log`. La regresión de navegación de opciones está en `MenuCursorOptionsRegression.log`.

Validación final: 26 comprobaciones correctas en los dos arranques consecutivos y 54 comprobaciones correctas de navegación de opciones/pausa. Unity queda cerrado; queda la confirmación visual del primer Play por parte del usuario.
