# Neon Quiet Hours

Primera propuesta musical original para Street Legacy: 53,23 segundos, 78 BPM, piano eléctrico sintético, acordes abiertos de sintetizador, bajo cálido y percusión electrónica muy discreta. No contiene grabaciones, muestras ni melodías de otros juegos. La referencia de ambiente no implica una copia de GTA o Minecraft.

Selecciona `Neon_Quiet_Hours.wav` y usa el reproductor del Inspector para escucharla completa. Es un WAV estéreo PCM16 a 44,1 kHz; Unity lo importa como Vorbis en streaming. La mezcla tiene picos de 0,72 y entrada/salida suaves; no se reproduce en bucle continuo.

## Ajustes

`Resources/AmbientMusic.asset` permite cambiar la lista de pistas, volumen general, grupo del mezclador, fundidos e intervalos aleatorios:

- Menú principal: primera entrada a los 2–5 segundos; después, silencios de 30–65 segundos entre piezas.
- Mundo: requiere 10 segundos de calma y después una espera inicial de 45–90 segundos; tras una pieza completa deja 120–240 segundos de silencio.
- Correr, saltar, sacar/apuntar el arma, pausar, cargar otra escena o perder el foco hace que la música se retire con un fundido. Volver a la calma inicia una nueva espera, no reanuda repetidamente el mismo fragmento.
- La gestión/construcción y caminar tranquilamente pueden seguir acompañadas de música.
- El volumen inicial es 0,22. `Music Enabled` permite desactivarla sin afectar a los sonidos de botones.

`AmbientMusicDirector` se añade automáticamente a `@App` en el arranque normal, una sola vez. Los scripts de futuras situaciones de peligro pueden llamar a `SuppressFor(segundos)`. Actualmente no se infiere una persecución o combate que aún no estén implementados.

El generador reproducible está en `Assets/Scripts/Audio/Tools/AmbientMusicSetup.cs`. No sobrescribe el WAV existente al repetir la instalación.
