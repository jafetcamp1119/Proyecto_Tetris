# Correcciones de interfaz, salas, audio y red local
Revisión del 23/10/2026. Esta etapa no generó APK, no ejecutó Gradle, no hizo commits ni push y no modificó TetrisDB. Se conservaron las pantallas originales, las capas de C#, el motor del tablero, los perfiles invitados y la integración Google previa.

## Interfaz y navegación

Se retiró Configurar servidor del login y se eliminó su almacenamiento de dirección por jugador. Los mensajes públicos ya no muestran errores HTTP, JWT, SQL, SignalR, excepciones de plugins ni motivos internos como TodosEliminados. Los detalles se registran en consola y los jugadores reciben mensajes naturales. El perfil y los resultados no muestran estados internos ni afirman un guardado que todavía no fue confirmado.

Las tres entradas a salas usan un componente común con operaciones separadas:

- Crear sala: /salas/crear, solamente configuración y creación.
- Salas públicas: /salas/publicas, solamente listado del modo elegido, creador, ocupación, especiales y botón Entrar. Si no hay salas muestra “No hay partidas disponibles. ¡Podés crear una!”.
- Sala privada: /salas/privada, solamente el código de seis caracteres y Unirme.

Todas incluyen regreso y menú principal. Se conserva el modo compartido o competitivo al navegar. También se atienden cambios de parámetros cuando Angular reutiliza la pantalla. Solitario mantiene su preparación propia y ya no presenta un código de sala pública.

## Capacidad y reglas

El creador selecciona 2, 3 o 4 participantes. La capa de negocio rechaza 1 o más de 4 en compartido y competitivo; solitario conserva un participante. Se requieren al menos dos multijugadores conectados y listos, se respeta la capacidad elegida y se bloquean ingresos adicionales. Las votaciones conservan la mayoría estricta: 2 de 2, 2 de 3 y 3 de 4.

Se actualizó el mínimo registrado en partidas nuevas a dos. Se inspeccionó la restricción real de SQL: ya admite esos valores, por lo que no se ejecutó ninguna migración ni se cambiaron registros históricos. Los valores predeterminados antiguos de la entidad no sustituyen los valores explícitos guardados por RegistroPartidaLJ.

No se cambiaron formas, colisiones, dimensiones, turnos, puntuación, reglas de derrota, coronas ni efectos. El cambio del motor para audio únicamente agrega la marca “caida” a una caída rápida ya procesada.

## Siete piezas especiales

El interruptor general oculta las tarjetas cuando está apagado. Al activarlo aparecen las siete piezas que entrega el catálogo real: Elimina columnas, Elimina fila, Confusión, Acelerador, Bomba, Doble puntuación y Barredora. No se utilizan listas de selección con Ctrl.

Cada tarjeta tiene nombre natural, descripción, casilla táctil e indicador Activada/Desactivada. Se revisó el comportamiento real de C# para las descripciones: la bomba usa un área 3 × 3 en la celda inferior; Barredora limpia la mitad superior sin puntuar; Doble puntuación afecta las líneas de esa jugada; los ataques van al siguiente jugador activo, omitiendo al atacante y a eliminados.

La selección conserva los códigos reales del catálogo. En TetrisDB Elimina columnas tiene código X; la interfaz usa su indicador eliminaColumna para reconocerlo, sin inventar otro código ni modificar SQL.

El control de probabilidad usa enteros de 1 a 100 %. Activar especiales sin seleccionar ninguna se rechaza. Omitir tiposPermitidos conserva la semántica existente de todos los tipos activos.

Los invitados consultan las tarjetas seleccionadas sin casillas editables. La configuración viene del estado de la sala y se actualiza al recibir EstadoActualizado. C# rechaza modificaciones de invitados, incluso si se intenta llamar al endpoint directamente. Competitivo no ofrece piezas especiales y el backend conserva su bloqueo.

## Audio y archivos

Se leyó MAPA_DE_AUDIO.md antes de integrar los archivos. Se copiaron los trece MP3 del ZIP sin renombrarlos ni crear canciones:

    frontend/tetris-app/src/assets/audio/musica/
    frontend/tetris-app/src/assets/audio/efectos/

Angular publica src/assets en assets; también se conserva MAPA_DE_AUDIO.md junto a los audios. Los trece recursos respondieron HTTP 200 con sus tamaños completos.

ServicioAudio centraliza archivos, volumen, preferencias, música y efectos:

- menu_y_espera.mp3 se comparte entre login, menú, configuración, búsqueda y espera. No se reinicia al pasar entre esas pantallas.
- partida.mp3 se usa en los tres modos. Una única instancia de música reproduce en bucle; cambiar de pista detiene la anterior.
- El sonido espera una interacción real. Si el navegador lo bloquea, se ofrece Activar sonido.
- Movimiento, rotación, caída, colocación y líneas usan los eventos confirmados por el motor. La gravedad normal no simula un movimiento manual.
- Eliminar varias filas usa eliminar_varias_filas.mp3 en lugar de superponer el efecto de una sola línea. Subir de nivel se detecta por el incremento confirmado.
- Las secuencias se recuerdan por tablero y ronda. El estado inicial o una reconexión fijan la base sin reproducir acciones históricas. La respuesta a un movimiento y su actualización repetida no generan dos efectos.
- Movimiento tiene volumen reducido al 22 % del volumen de efectos y un intervalo mínimo de 95 ms. Hay una sola voz por efecto, evitando acumular instancias al mantener un control.
- No hay avisos sonoros de entrada, salida, Listo o configuración de salas. Un clic del propio usuario puede usar clic_boton.mp3; una notificación remota no lo utiliza.
- Se ignora la marca de activación especial. ataque_recibido.mp3 únicamente corresponde a Confusión o Acelerador aplicado al jugador receptor, nunca al atacante ni a otro espectador.
- Victoria y derrota se registran una sola vez. En competitivo se escuchan acciones del tablero propio, no del tablero observado.
- Una pausa detiene la música y los efectos del tablero. Reanudar conserva la posición de la canción y las preferencias. Ocultar la app detiene el audio; volver lo recupera. La pérdida de conexión suspende el audio de partida.

## Cambiar canciones o efectos

Para reemplazar una canción basta cambiar el contenido de musica/menu_y_espera.mp3 o musica/partida.mp3 conservando el nombre. Para reemplazar un efecto, sustituir el MP3 correspondiente en efectos.

Para utilizar otro nombre, cambiar la ruta en archivosAudio, al principio de src/app/servicios/audio.servicio.ts. Todas las rutas se concentran ahí. Tras modificar recursos, compilar Angular y sincronizar Capacitor antes de que la desarrolladora empaquete Android.

## Ajustes del jugador

Menú > Ajustes de sonido permite activar música y efectos por separado y mover sus volúmenes entre 0 y 100 %. Se aplican inmediatamente y no cambian el volumen del teléfono.

Se guardan en localStorage bajo tetris.audio.<usuarioId>. Cada jugador recordado conserva sus preferencias en ese dispositivo. Vincular Google conserva UsuarioId y, por tanto, estas preferencias locales. No se promete sincronización de volumen entre dispositivos ni conservación al borrar los datos de la app.

## Red local centralizada

Se detectó y comprobó la IPv4 Wi-Fi real 192.168.1.20. El backend sigue usando el puerto 5186; se verificó /salud mediante http://192.168.1.20:5186 desde la propia PC.

La fuente interna es src/environments/configuracion-red.ts:
- Navegador de la PC: http://localhost:5186.
- Android: http://192.168.1.20:5186.
- HTTP local habilitado para las pruebas de desarrollo solicitadas.

Al cambiar de red, ejecutar npm.cmd run red:local desde el frontend. El script detecta la IPv4 privada activa y actualiza la fuente interna y la excepción debug. Si hay varias conexiones pide seleccionar la interfaz en la terminal del desarrollador. No se muestra configuración al jugador.

No se implementó descubrimiento automático desde celulares. Si cambia la IP, la desarrolladora debe recompilar, sincronizar y volver a empaquetar e instalar. La guía DESARROLLO_ANDROID.md explica los pasos, el firewall y las comprobaciones físicas.

HTTP está limitado por la política de Android debug a la IP elegida. El WebView permite contenido mixto solamente en una app depurable. Las excepciones debug no se incluyen en release. Antes de distribuir se necesita HTTPS válido, dirección estable, permitirHttpLocal=false y CORS explícito. Se conserva el identificador com.proyectotetris.juegocr.

Referencia oficial de Capacitor sobre contenido mixto de desarrollo: https://capacitorjs.com/docs/config . También se revisó el código de la versión 8.5.3 instalada, Bridge.java y las declaraciones de configuración.

## Archivos principales de esta revisión

Frontend:
- src/app/app.component.ts y .html: interacción inicial, navegación y ciclo de vida del audio.
- src/app/app.routes.ts: operaciones de salas y ruta de ajustes.
- src/app/home/home.page.ts y .html: navegación por modo y acceso a ajustes.
- src/app/login/login.page.ts y .html: eliminación de configuración pública y mensajes.
- src/app/pages/salas/salas.page.ts y .html: operaciones separadas y validación.
- src/app/pages/salas-compartidas/salas-compartidas.page.ts y .html: destinos distintos y modo conservado.
- src/app/pages/partida/partida.page.ts y .html: selección consultable, validación, mensajes y resultados naturales.
- src/app/pages/perfil/perfil.page.ts y .html: mensajes e historial sin estados internos.
- src/app/pages/ajustes/ajustes.page.ts: controles de música y efectos.
- src/app/componentes/configuracion-sala.component.ts: capacidad y probabilidad.
- src/app/componentes/piezas-especiales.component.ts: tarjetas compartidas de selección y consulta.
- src/app/componentes/tablero.component.ts: nombre natural de la especial activa.
- src/app/componentes/controles.component.ts: los controles no generan clics decorativos del menú.
- src/app/componentes/google-acceso.component.ts: conversión de errores a mensajes públicos.
- src/app/servicios/audio.servicio.ts: audio centralizado.
- src/app/servicios/api.servicio.ts: direcciones internas por plataforma.
- src/app/servicios/salas.servicio.ts: estado confirmado enviado a audio y suspensión/reconexión.
- src/app/servicios/mensajes-jugador.ts y src/app/modelos/presentacion-juego.ts: presentación amigable.
- src/app/modelos/tetris.model.ts: evento de caída rápida.
- src/environments/configuracion.ts y configuracion-red.ts: fuente pública de configuración interna, sin secretos.
- src/global.scss: tarjetas táctiles, interruptores, rangos y botón inicial de sonido.
- src/assets/audio: trece MP3 y mapa original.
- scripts/ConfigurarRedLocal.ps1 y package.json: comando red:local.
- android/app/src/debug/res/xml/seguridad_red_desarrollo.xml y android/app/src/main/java/com/proyectotetris/juegocr/MainActivity.java: HTTP local solo debug.
- Pruebas nuevas de audio, configuración, navegación de salas y mensajes.
- DESARROLLO_ANDROID.md, GOOGLE_ANDROID.md e INFORME_INTEGRACION.md: guía actual y separación del informe anterior.
- evidencias/ajustes-sonido.png y piezas-especiales-audio.png: capturas de la revisión.

Backend:
- Dominio/EntidadesTipadas/TJuego.cs: máximo válido de cuatro en el contrato.
- LogicaNegocio/Servicios/SalaLJ.cs: límites dos a cuatro e inicio mínimo dos.
- LogicaNegocio/Servicios/RegistroPartidaLJ.cs: mínimo explícito en partidas nuevas.
- Dominio/Juego/TableroJuego.cs y LogicaNegocio/Motor/MotorJuego.cs: metadato de sonido de caída rápida.
- docs/contratos/tetris.model.ts: contrato de audio actualizado.
- Tetris.Pruebas/Program.cs y PruebasReglasNuevas.cs: adecuación de expectativas a la capacidad solicitada.
- Tetris.Pruebas/PruebasCapacidadSalas.cs: nuevos casos por modo/capacidad.
- Tetris.Pruebas/PruebasHttp.cs: salas de dos/tres con conexiones reales y selección sincronizada.

El repositorio ya tenía numerosos cambios y archivos nuevos de la integración anterior y de la compañera. No se hizo reset ni se reemplazó ese trabajo.

## Pruebas ejecutadas

- Angular: npm.cmd run build, correcto.
- Angular/Vitest: npm.cmd test -- --watch=false, 34/34 correctas, incluidas 14 de audio.
- C#: dotnet run --project Tetris.Pruebas -p:UseAppHost=false, 164/164 correctas.
- HTTP/SignalR: dotnet run --project Tetris.Pruebas -p:UseAppHost=false -- --integracion-http, 230 comprobaciones correctas. El servidor y los datos de esta suite están en memoria; no usan SQL.
- Se verificaron dos, tres y cuatro participantes en ambos modos, ingreso adicional rechazado, inicio, tableros, movimientos y pausas. HTTP/SignalR usa WebSockets reales para dos/tres y conserva los casos previos de cuatro.
- HTTP comprueba que un invitado de la propia sala no puede cambiar especiales y que recibe selección y probabilidad por EstadoActualizado.
- Se verificaron todas las rutas de audio por HTTP, trece archivos completos.
- Revisión visual en navegador con el backend de validación: ingreso invitado, tres opciones de salas, ausencia de formulario ajeno en privada/públicas, tarjetas del catálogo, selección individual, ajustes independientes y conservación al recargar. También se creó una partida solitaria real y se probaron movimiento, rotación, caída, pausa con controles bloqueados, reanudación y salida.
- No se capturaron errores de reproducción durante la revisión del botón de sonido y las acciones del solitario. La suite de audio usa un reproductor simulado para verificar reglas; no acredita cómo se escucha cada MP3 en un teléfono.
- Las pruebas visuales usaron TetrisValidacion_Google_20261023. Se agregó allí un invitado de prueba y se copió el catálogo de siete especiales desde TetrisDB, consultada solo para leer. No se modificaron tablas ni datos de TetrisDB y no se borró ninguna base.
- git diff --check, correcto. Persiste el aviso previo de Browserslist por navegadores antiguos no soportados por Angular 22; no impide compilar.
- El APK preexistente conserva tamaño 8451250 bytes y fecha 23/10/2026 13:19:16. No fue regenerado ni actualizado con esta revisión.

## Pendiente y límites reales

La desarrolladora debe compilar e instalar su APK. Esta revisión no compiló los cambios Java/XML y no realizó pruebas en Android físico. Quedan por verificar sonido real, controles mantenidos, ciclo de vida, conexión y firewall desde varios celulares y cambio de Wi-Fi.

Google conserva el Client ID web en src/environments/configuracion.ts y el mismo valor en Google:IdClienteWeb del backend. No se añadió ningún secreto. Google Sign-In sigue pendiente de vinculación y recuperación con cuentas reales en Android; no se presenta como terminado por pasar pruebas de tokens inválidos o dobles de identidad.

La guía de red local es para desarrollo en una red de confianza. La versión definitiva necesita el backend HTTPS descrito anteriormente. No se instaló ninguna herramienta adicional.

