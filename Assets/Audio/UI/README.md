# Sonidos de interfaz — primera prueba

Tres efectos originales sintetizados, sin grabaciones ni muestras externas:

- `UI_Appear_Whoosh.wav`: barrido de aire suave, 280 ms.
- `UI_Hover_Cylinder.wav`: tres pequeños contactos metálicos, inspirados en un mecanismo de carga, 145 ms.
- `UI_Click_SoftShot.wav`: ataque seco de tecla, cuerpo grave corto y retorno mecánico; no es un disparo realista, 105 ms.

WAV mono, PCM 16 bits, 48 kHz. Picos limitados aproximadamente al 80 %, con entradas/salidas suavizadas. El volumen de reproducción es deliberadamente bajo.

## Ajustar y escuchar

Selecciona cada WAV en Unity y usa la previsualización de audio del Inspector. Para probarlos juntos, abre el menú, pasa el ratón sobre los botones y pulsa uno.

Selecciona `Resources/UIButtonSounds.asset` para cambiar `Master Volume`, `Appear Volume`, `Hover Volume` y `Click Volume`, sustituir los clips o desactivarlos con `Sound Enabled`. Opcionalmente asigna un grupo de AudioMixer. No cambia el idioma ni el diseño de los botones.

`UIButtonAudio` permite desactivar aparición, desaparición, hover o clic en un botón concreto. Los botones heredados usan los prefabs sample, sample_small y Tablet. Los botones creados por los constructores de UI también reciben el componente. Para un botón futuro creado manualmente fuera de esos prefabs, añade `UIButtonAudio`.

Las salidas también hacen whoosh, incluso con transiciones CanvasGroup sin desactivar el objeto. `Disappear Volume` regula su volumen; el clip opcional `Disappear` permite sustituirlo. Si está vacío, reutiliza el sonido de entrada. No se reproduce al salir de la aplicación.

Una sola fuente 2D persistente evita cortar el clic al cerrar un panel; sigue funcionando con el juego pausado. Las apariciones simultáneas se agrupan en un solo whoosh. Los controles deshabilitados no reaccionan al hover.

Generador reproducible: `Assets/Scripts/UI/Tools/UIButtonAudioSetup.cs`. No sobrescribe WAV ya existentes. Las copias previas de los prefabs modificados se guardan en `Documentation/UIButtonAudioBackup/`.
