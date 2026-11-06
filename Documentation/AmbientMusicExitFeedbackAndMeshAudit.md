# Música, woosh de salida y revisión de mallas — 2026-11-06

## Música

Composición original `Assets/Audio/Music/Neon_Quiet_Hours.wav`, duración 53,23 s. Director persistente instalado por Bootstrapper en el arranque normal. Configuración en `Assets/Audio/Music/Resources/AmbientMusic.asset`; detalles de tiempos y criterios de calma en el README de esa carpeta.

Validación en Play con arranque real del menú: 13 comprobaciones correctas. Se comprobó director único, configuración de la pista, reproducción, fundido, intervalo posterior, espera de calma, interrupción y desactivación global. No se abrió ni creó una partida. Log: `AmbientMusicPlayValidation.log`.

## Sonidos de salida

UIButtonAudio ahora detecta tanto activación/desactivación como cambios de visibilidad de CanvasGroup, necesarios para las transiciones de botones y tablet. Las salidas simultáneas se agrupan; no se emiten sonidos al terminar la aplicación. El mismo servicio 2D persistente mantiene el sonido aunque el botón se destruya.

`UIButtonSounds.asset` incorpora Disappear Volume y un clip opcional Disappear. Si este está vacío, utiliza el whoosh de entrada. Cada botón permite desactivar `Play Disappearance`. Se retiró el disparo explícito de sonido desde TabletView para evitar duplicarlo con la detección de visibilidad.

## Carpetas consultadas

Auditoría reproducible: `AmbientMusicSetup.AuditFolders`. Raíces: MainMenu, World City y ConstructionCatalog. Reporte completo: `ConstructionMeshUsage.txt`.

- `Assets/Meshes/Construction`: una malla, `GlassDoorFrame_WallDepth.asset`. Es el marco de puerta de cristal adaptado al grosor de pared; lo usan los prefabs Glass_Wall_door_complete, semincomplete e incomplete. El origen del recurso es WallJoinRevision.ApplyFrameFix, sin alterar las posiciones de bisagras y transforms del prefab.
- `Assets/Construction/WallMeshes`: 81 mallas de variantes de paredes. WallTopology las intercambia para ajustar laterales y remates según conexiones y paredes superiores, conservando el objeto construido, sus colisiones y su acabado. WallVariantsSetup las generó a partir de los modelos originales.
- Las 82 aparecen en las dependencias actuales. No se borró ni movió ninguna. Cambiar la organización es posible moviendo también sus .meta con Unity, pero no es necesario para que funcionen ni se ha hecho en esta tarea.

## Cursor: diagnóstico y corrección posterior

En el código revisado, ThirdPersonController.Start bloquea el cursor incondicionalmente. MainMenuUI no restablece explícitamente lockState/visible, y GameStateMachine.SetState tampoco establece el cursor al entrar en Menu. Esto deja el resultado dependiente del estado previo y del orden de arranque; no se puede asegurar que desaparezca al exportar. El foco de Game View también debe distinguirse de un bloqueo real del cursor.

El usuario confirmó después que ocurre en el menú, el cursor desaparece y Escape lo libera, y autorizó corregirlo. La corrección está documentada en `MenuCursorStartupFix.md`.
