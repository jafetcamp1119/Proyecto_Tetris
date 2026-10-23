# Audios para Tetris (Ionic / Angular / Capacitor)

Contiene **los 6 audios originales aportados** y **7 efectos nuevos creados para este paquete**.
Los seis originales solo fueron remultiplexados de `.mpeg` a `.mp3` (sin volver a comprimir el audio).

## MUSICA
- `musica/menu_y_espera.mp3` — audio original **Lobby.mpeg**. Usarlo tanto en **menú** como en **sala de espera** (no crear otra canción para la sala de espera). Reproducir en bucle.
- `musica/partida.mp3` — audio original **Juego sonido.mpeg**. Reproducir en bucle durante el juego.

## EFECTOS ORIGINALES
- `efectos/colocar_pieza.mp3` — colocar una pieza (original).
- `efectos/eliminar_filas.mp3` — eliminar filas (original).
- `efectos/victoria.mp3` — ganar una ronda/partida (original `ganador.mpeg`).
- `efectos/derrota.mp3` — perder una partida (original).

## EFECTOS NUEVOS (sintetizados, estilo retro)
- `efectos/mover_pieza.mp3` — mover a izquierda/derecha. Muy discreto.
- `efectos/girar_pieza.mp3` — rotar una pieza.
- `efectos/caida_rapida.mp3` — caída instantánea de una pieza.
- `efectos/eliminar_varias_filas.mp3` — eliminación múltiple, alternativa a `eliminar_filas`.
- `efectos/ataque_recibido.mp3` — aviso cuando el jugador RECIBE un ataque; no al activar la pieza especial.
- `efectos/clic_boton.mp3` — pulsar botón de interfaz.
- `efectos/subir_nivel.mp3` — aumento de nivel por líneas completadas.

## EXCLUSIONES SOLICITADAS
- **NO** sonido para notificaciones de salas.
- **NO** sonido para activar piezas especiales.
- **NO** música independiente para la sala de espera: reutilizar `menu_y_espera.mp3`.

## INTEGRACION
1. Copiar los directorios `musica/` y `efectos/` al directorio de assets PUBLICADOS por Angular en este proyecto (verificar `angular.json`; en algunos proyectos es `src/assets/` y en otros `public/assets/`). Mantener una carpeta `audio` intermedia, por ejemplo `assets/audio/musica/` y `assets/audio/efectos/`.
2. Centralizar rutas y reproducción en un `ServicioAudio` Angular; reproducir sonidos solo ante eventos locales/confirmados para evitar duplicaciones por SignalR.
3. Reutilizar `menu_y_espera.mp3` entre menú y sala de espera; NO reproducir otra instancia encima al navegar.
4. Separar volumen de música y efectos, permitir silencio y guardar preferencias en dispositivo.
5. Los navegadores requieren interacción del usuario para iniciar sonido; no forzar autoplay bloqueado.
6. En modo móvil optimizar volumen del efecto `mover_pieza` (ocurre muchas veces).
7. No generar APK: la persona encargada hará el empaquetado.

NOTA: Los siete efectos sintéticos fueron generados originalmente para este paquete. La autoría/licencias de los seis audios proporcionados por la persona usuaria deben verificarse antes de distribución pública.
