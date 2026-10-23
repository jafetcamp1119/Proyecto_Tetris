# Actualización aplicada a TetrisDB real

Fecha: 23 de octubre de 2026. Servidor: JAFET\SQLEXPRESS. Base: TetrisDB.

## Resultado

La base real quedó actualizada mediante docs/sql/04_usuarios_dispositivos_rondas.sql. No se recreó la base, no se ejecutó el instalador completo y no se borraron tablas, jugadores ni resultados. No se modificó Ionic ni se hicieron commits o push.

El script original conserva @RespaldoVerificado = 0 como protección para futuras ejecuciones. Para esta aplicación se generó una copia de ejecución con el respaldo confirmado y una transacción externa de auditoría en Tetris.Pruebas/bin/Verificacion/actualizacion-real. No es necesario volver a ejecutar la migración.

## Respaldo y comprobaciones previas

El historial de msdb identificó como respaldo completo más reciente:

~~~text
C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\Backup\TetrisDB.bak
~~~

Conjunto de respaldo 1, posición FILE=1; finalizado según el servidor SQL el 23/10/2026 a las 02:54:03. No estaba marcado como dañado. Se ejecutó:

~~~sql
RESTORE VERIFYONLY
FROM DISK = N'C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\Backup\TetrisDB.bak'
WITH FILE = 1;
~~~

SQL Server respondió: «The backup set on file 1 is valid.» No se restauró ni sobrescribió la base. Ese respaldo no tenía checksums de backup; se comprobó además la consistencia de la base con DBCC CHECKDB antes y después de la actualización, sin errores. VERIFYONLY no se presentó como una restauración de prueba completa.

Se comprobó que la estructura coincidía con el script, que no había nicknames duplicados normalizados y que no había partidas EnCurso/Pausada sin finalizar. No se detectó un proceso del backend con nombre ProyectoTetris_PrograV. La conexión de SSMS estaba inactiva.

## Cambios aplicados

- Nueva tabla dbo.CredencialesDispositivo, con identificador, UsuarioId, hash de credencial, creación, expiración y revocación. Incluye relación a Usuarios e índices.
- Usuarios.Correo y Usuarios.PasswordHash ahora permiten NULL, conservando los valores existentes.
- Nuevo campo calculado Usuarios.NombreUsuarioNormalizado e índice único UX_Usuarios_NombreNormalizado.
- Índice UX_Usuarios_Correo único y filtrado por Correo IS NOT NULL, para admitir varios jugadores sin correo.
- Partidas.CodigoSala ampliado de nvarchar(20) a nvarchar(24), sin truncar valores.
- Procedimiento DeterminarGanador actualizado para consultar los ganadores guardados por el backend, incluidos empates.

## Conservación de información

Antes de confirmar se comparó la cantidad y una huella SHA-256 de todas las filas y columnas originales de cada una de las 18 tablas existentes, ordenadas por su clave primaria. También se compararon las definiciones de las 34 claves foráneas anteriores. Las verificaciones pasaron dentro de la transacción externa antes del COMMIT definitivo.

| Elemento | Antes | Después de migrar y revertir las pruebas |
| --- | ---: | ---: |
| Usuarios | 4 | 4 |
| Estadísticas de jugadores | 4 | 4 |
| Perfiles | 4 | 4 |
| Configuraciones de jugadores | 4 | 4 |
| Tipos de pieza | 14 | 14 |
| Partidas | 0 | 0 |
| Resultados | 0 | 0 |
| Tablas | 18 | 19 |
| Claves foráneas | 34 | 35 |
| Credenciales de dispositivo | Tabla inexistente | 0 registros |

La tabla de credenciales está vacía porque no se inventaron identidades para las cuentas anteriores y todos los jugadores sintéticos usados para verificar se revirtieron. Tendrá registros cuando se registre un jugador real con el backend actualizado.

## Verificación del backend contra la base real

Se agregó VerificacionAccesoReal al proyecto de pruebas y el argumento explícito --verificar-acceso-real. La prueba utiliza AccesoUsuarioLJ, AutoMapper, repositorios, unidad de trabajo y EF Core contra la conexión configurada. Todas las escrituras sintéticas se ejecutan dentro de una transacción que se revierte al terminar.

Pasaron las diez comprobaciones:

1. Registro usando únicamente nickname.
2. Correo y hash de contraseña permanecen NULL en el jugador nuevo.
3. C# rechaza el mismo nickname con otras mayúsculas y espacios exteriores.
4. Pueden registrarse varios jugadores sin correo ni contraseña.
5. La credencial privada permite recuperar el mismo perfil.
6. Recuperar no publica otra vez el secreto.
7. La sesión puede renovarse.
8. Revocar invalida la sesión.
9. El índice SQL rechaza un duplicado directo, incluso sin validación C#.
10. La transacción se revierte: la cantidad de usuarios vuelve a cuatro y no queda el nickname de prueba.

La solución completa compiló con 0 errores y 0 advertencias usando la salida bin/Verificacion/net10.0. DBCC CHECKCONSTRAINTS y DBCC CHECKDB finales no encontraron errores. No fue necesario repetir las suites completas del motor para aplicar una migración que ya había sido comprobada en bases temporales.

## Qué hacer ahora

No hace falta ejecutar más scripts SQL. Iniciar o reiniciar el backend actualizado con la conexión a JAFET\SQLEXPRESS y TetrisDB.

El servidor ya admite:

~~~http
POST /api/usuarios/registrar
Content-Type: application/json

{"nombreUsuario":"UnNicknameDisponible"}
~~~

El frontend aún no consume ese endpoint. Se revisó sin modificarlo: LoginPage.ingresar() en frontend/tetris-app/src/app/login/login.page.ts solo valida el campo y muestra «Falta conectar el registro con el servidor». Por eso el botón «Comenzar a jugar» todavía no registra usuarios.

Para que el botón funcione falta conectar esa operación HTTP, guardar la credencial privada en almacenamiento seguro Android, conservar el JWT y recuperar/renovar la sesión al abrir. Ver [la guía de Ionic](USUARIOS_CORONAS_RONDAS.md).

Los cuatro usuarios anteriores mantienen sus identificadores, datos y nicknames reservados. No reciben credenciales automáticamente y no pueden recuperar acceso escribiendo solo el nickname. Para probar un registro nuevo, utilizar un nickname disponible.
