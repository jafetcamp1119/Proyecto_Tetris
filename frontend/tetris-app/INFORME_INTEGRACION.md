# Informe de integración de Tetris

> Informe de la etapa anterior. La revisión posterior de interfaz, salas de 2 a 4 jugadores, audio y red HTTP local está en INFORME_AUDIO_INTERFAZ.md. La configuración visible del servidor y las instrucciones anteriores de HTTPS fueron sustituidas por DESARROLLO_ANDROID.md. La revisión posterior no genera ni compila APK; las referencias a compilación Android de este informe corresponden exclusivamente a la etapa anterior.

## Lo que ya existía

El backend ya tenía el motor del tablero de 20 x 10, las siete formas normales, sus colisiones y rotaciones, puntos, velocidad, los tres modos, salas públicas/privadas, turnos, piezas especiales, votación de pausas, reconexiones, resultados, coronas y rondas nuevas. También existían SQL Server, repositorios, unidad de trabajo, AutoMapper, JWT y acceso de invitados con credencial privada.

El frontend tenía las pantallas visuales de login, menú y selección del modo compartido, creadas por la compañera. Había botones deshabilitados y mensajes temporales en lugar de salas y tableros conectados. Android ya estaba creado con Capacitor 8.5.3 y el paquete com.proyectotetris.juegocr.

## Lo que se agregó

Se conservaron las pantallas y sus estilos originales. Se conectaron los tres botones del menú y las opciones de salas. Se agregó el nickname arriba del menú, una pantalla de perfil, una pantalla reutilizable de selección/creación de salas y una pantalla de espera, juego y resultados compartida entre modos.

Los componentes nuevos son:

- TableroComponent: dibuja exclusivamente las celdas y la pieza que llegan del servidor mediante Canvas 2D. No contiene un motor ni calcula puntuaciones.
- ControlesComponent: botones de izquierda, derecha, rotación, bajada y caída rápida; admite mantener pulsado el movimiento y evita duplicarlo con el clic táctil sintetizado. Se detiene al soltar, perder foco, ocultar el documento o cambiar de turno.
- ConfiguracionSalaComponent: configura las opciones reales del backend y muestra el catálogo de tipos especiales que existe en SQL.
- GoogleAccesoComponent: botón oficial web o acceso nativo Android, con mensajes reales del backend.

Los servicios nuevos son ApiServicio (URL centralizada y solicitudes), AutenticacionServicio (registro, perfiles, credenciales, renovación y cierre), SalasServicio (REST y SignalR), GoogleServicio (desafíos, ID token e integración) y DialogosServicio (confirmaciones). Los nombres de rutas y contratos corresponden a los endpoints reales.

Angular sigue siendo Standalone. Se conservan Dominio, AccesoDatos, LogicaNegocio, Utilitarios, repositorios y unidad de trabajo del profesor. No se reescribió el motor ni se cambiaron sus condiciones de victoria.

## Cómo se juega

En Solitario se configura una sala de un jugador, se marca Listo y se inicia. El servidor mueve la pieza por gravedad; el cliente envía movimientos. Al perder, los resultados se guardan mediante el servicio existente y se puede preparar otra ronda. Perfil consulta el máximo de los resultados individuales guardados: una puntuación menor no reduce el récord. El contrato actual no expone una siguiente pieza, por lo que no se inventó una vista previa.

En Compartido, todos reciben un mismo tablero por SignalR. Solo el jugador indicado por usuarioTurno puede mover; los demás observan. La eliminación, limpieza superior, turnos, meta y supervivencia se resuelven en el servidor. Las piezas especiales se distinguen con una estrella en la pieza activa y su código; las activaciones y los efectos se muestran con los datos que envía el backend. Confusión no se aplica por segunda vez en Ionic: se envía la dirección original.

En Competitivo, cada jugador ve y controla su tablero simultáneamente. Los eliminados pueden seleccionar otro tablero para observar. El servidor mantiene al último activo jugando hasta perder y calcula las posiciones y empates por puntos. No se activan especiales ni ataques.

El creador y los participantes usan la misma pantalla de espera. Todos ven configuración, participantes, estado de conexión y quién está listo. Solo el creador puede editar o iniciar; el backend mantiene también esos controles. Los códigos privados provienen del servidor y no aparecen en el listado público.

La pausa individual es directa y reanudable. En multijugador se muestra la solicitud, los votos y el tiempo restante. La cuenta regresiva es solo visual y usa la hora del servidor: llegar a cero nunca resuelve una pausa en el cliente. El servidor conserva mayoría estricta, quince segundos para votar y tres minutos de pausa.

Los resultados muestran ganadores, empates, posiciones, puntos y victorias por sala. ♛ distingue al creador y 👑 al líder de victorias. La siguiente ronda utiliza el endpoint existente: conserva sala/configuración/victorias y espera a que el histórico de la ronda anterior se guarde.

## Sincronización y navegación

SalasServicio crea una sola conexión al Hub, registra los eventos una vez y vuelve a entrar al grupo después de reconectar. Usa el JWT vigente, bloquea movimientos sin conexión y descarta estados atrasados mediante version. Cada movimiento tiene una secuencia creciente; no se reenvía automáticamente una acción incierta.

Hay botones para regresar y un guard que confirma el abandono. En Android, el retroceso pasa por la misma confirmación. El abandono voluntario del creador sigue cerrando la sala. Si el servidor no responde, se permite salir tras otra advertencia y el servidor aplica sus reglas de desconexión. No se promete conservar una partida activa después de reiniciar la PC o superar el plazo de reconexión.

Al salir se detienen la conexión, los controles mantenidos y el reloj visual. No se inicia otra modalidad mientras haya una reconexión pendiente sin resolver. Los resultados ya guardados permanecen en SQL.

## Perfiles y progreso

Cada jugador recordado conserva su propia credencial. Cambiar de jugador termina la selección local sin revocar ese perfil; se puede recuperarlo mediante su credencial. Cerrar sesión revoca la credencial activa y su JWT; se avisa antes de que será necesario Google o un código guardado para recuperarlo. Quitar del dispositivo elimina solo la credencial local de ese perfil. No se implementó Eliminar cuenta ni se borran estadísticas al cerrar sesión.

En Android, las credenciales duraderas se almacenan cifradas con Android Keystore; no hay fallback a localStorage si falla ese mecanismo. En navegador de desarrollo se usa almacenamiento local, con el riesgo habitual de acceso al dispositivo o XSS: no equivale a cuentas protegidas por contraseñas individuales. Los JWT de esta integración viven en memoria.

Google se vincula al mismo UsuarioId mediante sub validado en C#. No se vincula por nombre o correo, no se fusionan dos perfiles y no se reemplaza silenciosamente una asociación previa. La base impone unicidad de GoogleSub. Los detalles y el lugar del Client ID están en GOOGLE_ANDROID.md.

Guardar mi cuenta genera un código privado de 256 bits mostrado una sola vez en la respuesta. SQL guarda su SHA-256 y su vencimiento de un año; generar otro reemplaza al anterior. Recuperarlo lo invalida y crea una credencial nueva. El hash es un token de concurrencia de EF para impedir consumos simultáneos. Los endpoints de acceso limitan intentos por IP. Después de usar un código se debe generar y guardar otro desde Perfil.

## SQL y conservación de datos

Se inspeccionaron las tablas reales y el respaldo existente. Se creó y verificó otro respaldo COPY_ONLY antes de aplicar docs/sql/05_google_recuperacion.sql:

C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\Backup\TetrisDB_antes_google_20261023_1843.bak

La migración agrega GoogleSub, AvatarGoogle, HashRecuperacion y RecuperacionExpiraEnUtc a Usuarios y dos índices únicos filtrados. Es aditiva e idempotente, sin recrear TetrisDB. La primera ejecución requirió activar QUOTED_IDENTIFIER para los índices filtrados; el script quedó corregido y se aplicó correctamente.

Se conservaron los cinco usuarios reales y los datos históricos existentes. Las pruebas de integración de cuentas y las pruebas visuales usan la base aislada TetrisValidacion_Google_20261023, conservada para inspección. Las identidades externas de esas pruebas son dobles de prueba, no accesos Google reales.

La consulta de resultados se amplió con modalidad, cantidad de participantes y empate en primer lugar, obtenidos desde las partidas y resultados de SQL, sin cambiar los registros históricos ni inventar estadísticas.

## Pruebas realizadas y límites

- 155 pruebas del motor y cuentas: correctas, incluidas reglas de los tres modos, turnos, ataques, efectos, pausas, rondas, coronas, conflictos Google y consumo de recuperación.
- 175 comprobaciones HTTP/SignalR: correctas, incluidas cuatro conexiones de prueba, reconexión, permisos, pausas, efectos, nuevas rondas, rechazo de un token Google inventado y de vinculación sin JWT.
- 12 pruebas Angular: correctas, incluidos perfiles independientes, cierre de sesión, ausencia de fallback inseguro nativo, descarte de versiones viejas, bloqueo de espectadores y cancelación de pulsaciones táctiles.
- Pruebas SQL aisladas: correctas para conservar UsuarioId y récord, rechazar conflicto de cuenta y permitir un único consumo entre dos recuperaciones concurrentes.
- Revisión visual en navegador: ingreso invitado, configuración y creación individual, Listo, inicio, tablero real, movimiento, rotación, caída rápida, pausa con controles bloqueados, perfil, botón Google, diálogo de recuperación y recuperación del jugador recordado. Revisado también a 390 x 844. Captura: evidencias/tablero-movil.png.
- Angular y C# compilan. Android debug compila con los plugins y conserva el paquete y certificado originales.

Las pruebas HTTP de cuatro conexiones no son cuatro celulares físicos. Google todavía requiere probar una cuenta real en navegador y Android, vincular un invitado con progreso, recuperarlo desde otro dispositivo y comprobar el cambio/cancelación de cuenta. Tampoco se verificó en teléfonos físicos el almacenamiento Keystore, la suspensión, el retroceso del sistema ni la red Wi-Fi con HTTPS: deben probarse siguiendo DESARROLLO_ANDROID.md.

Google Play Games no se implementó: requiere un juego configurado en Play Console y sus credenciales específicas. No se lo presenta como habilitado por tener los clientes OAuth de Google Sign-In.

Persisten advertencias de Browserslist por navegadores antiguos que Angular 22 no soporta. No impiden la compilación. No se afirma compatibilidad con esos navegadores.

## Cómo continuar

Consultar DESARROLLO_ANDROID.md para ejecutar Ionic, iniciar el backend, configurar HTTPS en la Wi-Fi y compilar el APK existente. El APK se encuentra en android/app/build/outputs/apk/debug/app-debug.apk. GOOGLE_ANDROID.md indica los Client IDs y las comprobaciones externas de OAuth.

No se hicieron commits ni push. Se conservaron los cambios previos de la compañera y las reglas del backend. El proyecto no se declara 100 % terminado: quedan las verificaciones reales de Google y varios dispositivos indicadas arriba.
