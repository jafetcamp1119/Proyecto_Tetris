# Presentación de Tetris desde Visual Studio

La guía general está en [README principal](../../README.md). El panel conserva su diseño, logo, animaciones, QR y validaciones.

## Preparación

1. Preparar la base existente con docs/sql/06_sesion_unica.sql únicamente con autorización manual. El iniciador comprueba el esquema, pero no modifica SQL.
2. Tener .NET 10, Node, dependencias npm, Firebase CLI y cloudflared disponibles.
3. Abrir la solución y seleccionar el proyecto Tetris.Presentacion, perfil normal Tetris.Presentacion.
4. Conservar la configuración privada DPAPI del usuario Windows. Nunca copiar la conexión SQL o las claves al frontend. La clave de recuperación debe ser estable; se puede configurar Recuperacion__Clave independiente. Si falta, se usa la clave privada JWT de la presentación.

## Recorrido de F5

Valida herramientas y SQL, compila e inicia la API en Production en loopback. Crea el túnel, espera Registered tunnel connection y comprueba DNS antes de HTTPS, salud, CORS y desafío de acceso. No desactiva certificados. Los registros de Cloudflare se leen con acceso compartido.

Prepara y valida www. Si Hosting necesita una actualización pide PUBLICAR una sola vez. Acepta espacios en los extremos y cualquier combinación de mayúsculas/minúsculas; otra respuesta cancela. EOF o errores de entrada se informan y nunca autorizan. Un contenido ya publicado no provoca otro deploy. No existe publicación automática.

Solo habilita el QR cuando la configuración pública apunta al backend comprobado. El monitoreo es un único bucle secuencial con diez segundos entre comprobaciones: anuncia únicamente cambios. Renueva silenciosamente la vigencia cada veinte segundos; el panel conserva su protección de cuarenta y cinco segundos. Cerrar la presentación termina sus procesos propios y descendientes.

Los registros quedan en %TEMP%/TetrisPresentacion/<sesión>. conexion.log contiene diagnóstico sin secretos. Se renuevan como máximo tres túneles ante fallos DNS persistentes, cerrando el anterior. Los túneles temporales dependen de un servicio externo y no garantizan disponibilidad.

## Pruebas sin publicar

Desde frontend/tetris-app:

~~~powershell
npm run test:presentacion
~~~

Desde Backend/ProyectoTetris_PrograV:

~~~powershell
dotnet run --project Tetris.Presentacion --no-launch-profile -- --verificar
dotnet run --project Tetris.Presentacion --no-launch-profile -- --pruebas
~~~

Estas comprobaciones no consultan SQL, abren túneles ni publican. Se conservan las regresiones de confirmación, entrada heredada .NET/PowerShell, bloqueo de registros, QR y cierre de procesos. El perfil manual temporal de confirmación fue retirado; su prueba continúa como escenario interno automatizado.

## Android

La aplicación instalada consulta la configuración estable de Firebase al iniciar. Cambiar de túnel no requiere una APK nueva. El encabezado CORS del archivo público debe estar publicado antes de probar una instalación; editar firebase.json localmente no cambia Hosting. La computadora y SQL deben estar disponibles durante la exposición.