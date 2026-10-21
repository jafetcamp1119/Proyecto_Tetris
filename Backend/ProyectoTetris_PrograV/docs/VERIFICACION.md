# Verificación definitiva — 21 de octubre de 2026

Consulta [ACTUALIZACION_DEFINITIVA.md](ACTUALIZACION_DEFINITIVA.md) para la verificación más reciente.
Validación final de esta actualización: compilación sin errores ni advertencias, 123/123 pruebas
de lógica, 84 comprobaciones HTTP/SignalR e integración SQL correcta con rollback confirmado.
La verificación SQL incluye cancelación, retiro del estado activo de participantes y conservación
de finales ya guardados. No se probaron teléfonos físicos.

Las secciones siguientes conservan el historial de etapas anteriores.

# Verificación de la segunda ronda (pieza que elimina fila, piezas normales, velocidad y puntos)

Entorno Linux con .NET 10, sin SQL Server, sin teléfonos y sin tu computadora:

- Compilación de la solución: 0 errores y 0 advertencias.
- Tetris.Pruebas: 109/109 correctas (las 79 anteriores sin cambios más 30 nuevas en
  PruebasConfiguracionPartida.cs: eliminación de fila, piezas normales, velocidad, puntos,
  configuración de sala y eventos de sonido).
- Integración HTTP/SignalR: 67 comprobaciones correctas (65 anteriores más 2 del endpoint
  PUT /api/salas/{id}/configuracion).
- Comprobación de que las pruebas detectan fallos: al desactivar a propósito el efecto de fila, 4 pruebas fallaron.

**No se probó:** SQL Server (`--integracion-sql` y docs/sql/02_pieza_elimina_fila.sql), teléfonos, red
local ni sonido real (el backend solo informa los eventos). La velocidad se comprobó con la hora simulada
de la sala, no con tiempo real.

# Verificación del 21 de octubre de 2026 (piezas especiales, pausas y salas privadas)

Se ejecutó en un entorno Linux con .NET 10.0.113, sin SQL Server y sin tu computadora:

- Compilación de ProyectoTetris_PrograV.slnx: 0 errores y 0 advertencias.
- Ejecutable Tetris.Pruebas: 79/79 comprobaciones correctas (29 anteriores sin cambios más 50 nuevas:
  piezas especiales, validación de porcentajes, votaciones, límite de solicitudes, pausa de tres
  minutos, congelamiento de temporizadores, pausa individual y códigos de salas privadas).
- Integración HTTP/SignalR: 65 comprobaciones correctas (42 anteriores más 23 nuevas: eventos de pausa
  por WebSocket, listado público, ingreso por código y CORS de IPv4 privada).
- La API real arrancó con ValidateScopes y ValidateOnBuild; /salud respondió y /api/salas pidió sesión (401).

**No se probó:** la integración con SQL Server (`--integracion-sql`), el motor con las filas reales de
`TiposPieza`, teléfonos Android, la red Wi-Fi ni el firewall. Ejecuta en tu equipo
`dotnet build`, `dotnet run --project Tetris.Pruebas` y las dos integraciones antes de entregar.
La guarda de partidas privadas (`EsPrivada`) aún no se comprobó contra tu base real.

# Verificación anterior — 20 de octubre de 2026

## Resultado actual

- Solución completa: cero errores y cero advertencias de compilación.
- 29/29 comprobaciones del ejecutable Tetris.Pruebas correctas.
  Se conservaron las 28 anteriores y se añadió compatibilidad del cliente C# con JWT.
- 42 comprobaciones HTTP/SignalR correctas contra un servidor temporal real.
- Integración SQL de los tres modos correcta, con rollback y sin filas de prueba restantes.
- La API real arrancó con ValidateScopes y ValidateOnBuild activos y el perfil Tetris.API.
- Escucha confirmada: http://0.0.0.0:5186, entorno Development.
- /salud: 200 por localhost y por la IPv4 del equipo, desde la propia computadora.
- /api/usuarios/yo sin sesión: 401.
- Preflight CORS de https://localhost: 204 con el origen explícito correcto.

## Qué cubren las pruebas

Las reglas conservan puntuaciones, colisiones, líneas, nivel, derrota, turnos y modos.
Competitivo no finaliza con un único jugador activo y clasifica por puntos.

La integración HTTP utiliza la configuración real de JSON, validación y seguridad, los
controllers y JuegoHub. Los usuarios y repositorios se sustituyen por almacenamiento
en memoria para no dejar cuentas de prueba en SQL.

Verifica registro, login, JWT sin cookies, firma distinta, token vencido, validación,
CORS de Ionic/Capacitor y rechazo de orígenes ajenos, configuración, resultados,
ranking y compatibilidad con cookies. Abre cuatro conexiones WebSocket, autentica
Entrar, comprueba Mover y EstadoActualizado, rechaza movimientos fuera de turno
y recupera el estado al reconectar.

La integración SQL utiliza EF y repositorios reales dentro de una transacción.
Verifica participantes, resultados, empates y estadísticas idempotentes de individual,
compartido y competitivo. La transacción siempre se revierte; se comprobó que no quedaron
usuarios de prueba. Puede consumir valores IDENTITY aunque revierta las filas.

No se cambió el esquema SQL en esta etapa de Ionic. El ajuste de modalidades aplicado
en la reorganización anterior se conserva.

## Comandos ejecutados

~~~powershell
dotnet build ProyectoTetris_PrograV.slnx -m:1 -nodeReuse:false -p:UseSharedCompilation=false
dotnet Tetris.Pruebas/bin/Debug/net10.0/Tetris.Pruebas.dll
dotnet Tetris.Pruebas/bin/Debug/net10.0/Tetris.Pruebas.dll --integracion-http
dotnet Tetris.Pruebas/bin/Debug/net10.0/Tetris.Pruebas.dll --integracion-sql
dotnet run --no-build --project ProyectoTetris_PrograV/ProyectoTetris_PrograV.csproj --launch-profile Tetris.API
~~~

Los argumentos de un solo nodo y compilación sin servidor evitan restricciones del
entorno utilizado para las comprobaciones. Visual Studio puede compilar normalmente.
El servidor temporal y la API de comprobación se detuvieron al terminar.

## Límites de lo verificado

No se creó ni compiló un proyecto Ionic. No se generó una APK.
No se probaron teléfonos físicos, la red Wi-Fi del usuario, la regla de firewall ni
Android Studio. La prueba por IPv4 desde el propio equipo no demuestra acceso desde un teléfono.
Las cuatro conexiones WebSocket de la prueba no representan cuatro dispositivos físicos.

No se verificó un despliegue Production, HTTPS con certificados de dispositivos,
refresh tokens ni revocación anticipada de JWT. Esas funciones no se presentan como terminadas.

No se requieren recompensas ni recuperación de partidas activas: se cancelan al reiniciar.
El contador de victorias y la corona configurable se describen en el informe definitivo.
La pausa quedó implementada el 21 de octubre de 2026 (ver la verificación de arriba).

