> Actualización posterior: la migración ya se aplicó a TetrisDB real después de verificar el respaldo. Ver [resultado y comprobaciones de la base real](ACTUALIZACION_BASE_REAL.md). El informe siguiente describe el trabajo inicial, previo a esa aplicación.

# Informe de actualización del backend Tetris

Trabajo realizado exclusivamente en el backend de C:/Users/jafet/Documents/GitHub/Proyecto_Tetris, solución ProyectoTetris_PrograV.slnx. Se conservaron las capas y no se hicieron commits ni push. No se modificó Ionic; durante la sesión aparecieron cambios ajenos del frontend y se dejaron intactos.

## Sistema de usuarios e identidad

Se sustituyó el acceso tradicional en IAccesoUsuarioLJ/AccesoUsuarioLJ y UsuariosController. TRegistroUsuario pide únicamente NombreUsuario. Se quitaron TLoginUsuario, Autenticar y el endpoint público /api/usuarios/login. UsuarioLJ.Insertar ya no ofrece una vía alternativa de alta con correo y contraseña. El cambio de nickname mediante el CRUD anterior no está habilitado.

Registrar crea Usuario y CredencialDispositivo mediante los repositorios y una sola llamada a la unidad de trabajo. No inventa correos ni contraseñas. Se mantienen UsuarioId, FechaRegistro, Activo y todas las relaciones previas. Correo y PasswordHash legados se conservan como columnas opcionales; no se publican en el perfil JSON.

La credencial privada usa 32 bytes criptográficamente aleatorios y se entrega una sola vez como 64 caracteres hexadecimales. SQL conserva únicamente SHA-256. Recuperar y renovar verifican ese hash, la vigencia, la revocación y el estado activo. El mismo dispositivo puede obtener otro JWT sin escribir el nickname ni presentar un JWT vigente. La credencial válida prolonga su vencimiento a 365 días desde el uso; no se rota el secreto.

Los JWT contienen sub, nombre, jti y sid. Se valida firma, emisor, audiencia, vencimiento y sesión de dispositivo en SQL. Logout revoca esa sesión, incluyendo sus JWT. FiltroSesionJuego comprueba la vigencia antes de cada acción de un WebSocket ya abierto; el hub conserva sus controles de pertenencia, creador, turnos y movimientos. No se aceptan cookies del acceso anterior.

La unicidad tiene dos defensas: consulta previa en C# y columna SQL calculada con índice único, insensible a mayúsculas y con espacios exteriores normalizados. Si otro registro gana la carrera, se revierte el intento y se devuelve NombreOcupado con HTTP 409. En SQL real se probaron ocho dispositivos simultáneos: uno registró y siete recibieron conflicto.

## Nuevas clases y contratos

- CredencialDispositivo: entidad con relación a Usuario, hash, fechas y revocación.
- TRecuperarSesion: solicitud con la credencial privada.
- TSesionJugador: resultado interno que permite emitir JWT sin publicar el hash ni la entidad EF.
- FiltroSesionJuego: validación de sesión en cada acción SignalR.
- PruebasIdentidadSql: pruebas de acceso y concurrencia contra SQL Server temporal.

Se ampliaron TAutenticacion, TPuntuacion y TSala; se agregó CodigoError a Respuesta<T>. TRegistroUsuario ahora contiene solo el nickname. Se mantuvieron los nombres públicos existentes de los datos de juego y la arquitectura con AutoMapper, repositorios, unidad de trabajo, EF, interfaces y servicios.

## Endpoints

| Ruta | Cambio |
| --- | --- |
| POST /api/usuarios/registrar | Alta con nickname, devuelve JWT y credencial; 201, 400 o 409 |
| GET /api/usuarios/disponibilidad?nombreUsuario=... | Nueva consulta pública sin datos privados |
| POST /api/usuarios/recuperar | Nueva recuperación mediante credencial; 200 o 401 |
| POST /api/usuarios/renovar | Nuevo JWT mediante la misma credencial; 200 o 401 |
| GET /api/usuarios/yo | Perfil público del sujeto autenticado |
| POST /api/usuarios/logout | Revoca la identidad persistente del dispositivo |
| POST /api/usuarios/login | Retirado, devuelve 404 |
| POST /api/salas/{id}/nueva-ronda | Nueva preparación de otra ronda, solo creador |

Los errores de formato devuelven 400 y los internos 500. El JWT vencido responde 401 con SesionVencida; un nombre ocupado nunca usa 401. Los contratos JSON y ejemplos para Ionic se encuentran en [la guía de integración](USUARIOS_CORONAS_RONDAS.md) y [tetris.model.ts](contratos/tetris.model.ts).

## Coronas y rondas

Se conservaron los ganadores calculados por el motor. Competitivo espera que todos pierdan y conserva todos los empatados con la puntuación máxima. Compartido conserva victoria por último activo y por meta. No se modificó el motor ni los efectos especiales.

TPuntuacion agrega GanoRonda para la corona de resultados. VictoriasSala conserva el acumulado y Corona marca a los líderes de sala: con cero victorias no hay corona; por omisión se exige al menos una victoria y se respeta MinimoVictoriasCorona cuando está configurado. Los líderes empatados comparten el distintivo. Individual no recibe coronas multijugador.

NuevaRonda espera la finalización y la confirmación del guardado final; así no descarta resultados pendientes. Conserva creador, participantes que permanecen, conexiones, configuración, nicknames y victorias. Restablece tableros, puntos, líneas, nivel, secuencia de movimientos, eliminaciones, efectos, pausas y preparación. Los jugadores vuelven a marcar listo y el creador usa Iniciar. No puede ejecutarse durante una partida activa ni por un invitado.

Cada ronda crea una Partida SQL distinta. RegistroPartidaLJ conserva el código histórico para la primera y usa codigo-2, codigo-3, etc. para las siguientes. Los resultados no se sobrescriben y las estadísticas no se cuentan dos veces. Dos rondas y ocho resultados ganadores se comprobaron en SQL real.

## SQL preparado y comprobado

- [04_usuarios_dispositivos_rondas.sql](sql/04_usuarios_dispositivos_rondas.sql): migración transaccional, repetible, con verificación explícita de respaldo previa y rechazo de duplicados legados. No elimina usuarios ni históricos.
- [TetrisDB_Completa_Actualizada.txt](sql/TetrisDB_Completa_Actualizada.txt): creación desde cero con tablas, relaciones, restricciones, vista, procedimientos y catálogos. Contiene siete piezas normales y siete especiales, sin datos personales ni credenciales.
- [ProbarUsuariosYRondas.ps1](sql/ProbarUsuariosYRondas.ps1): validación reproducible en bases temporales; crea y elimina únicamente bases TetrisValidacion_* propias de la ejecución.

Se hacen opcionales Correo y PasswordHash. Se sustituye la unicidad del correo por un índice filtrado para admitir varios NULL. Se agrega NombreUsuarioNormalizado calculado e índice único. Se crea CredencialesDispositivo con clave foránea y restricciones de fechas. CodigoSala se amplía a 24 caracteres, sin truncamiento. DeterminarGanador queda como consulta de los ganadores guardados por el backend para evitar el desempate antiguo por líneas y la finalización externa de partidas activas.

TetrisDB real NO recibió la migración, registros sintéticos ni cambios de tablas. No se afirmó ni se realizó un respaldo de esa base porque no se alteró. Se consultó únicamente para verificar disponibilidad y cantidades/estructura antes y después. Las pruebas SQL crearon bases vacías independientes y las eliminaron al terminar.

## Refactorización realizada

- AccesoUsuarioLJ: Registrar, Recuperar, NombreDisponible, SesionVigente, Revocar y auxiliares descriptivos; se retiró el password hasher.
- UsuariosController: registro, recuperación/renovación, cierre y perfil; emisión de JWT separada de la lógica de datos.
- ConfiguracionSeguridad y ServicioToken: autenticación JWT vinculada a sid, estados de error claros y retiro de cookies.
- UsuarioLJ y AutoMapperProfile: eliminación de la vía de alta anterior y protección de campos legados/calculados.
- ProyectorSala: coronas de ronda y de liderazgo separadas.
- SalaLJ: preparación de una nueva ronda con validación del creador y del guardado final.
- RegistroPartidaLJ: identificación independiente de cada ronda y conservación de históricos.
- TetrisApi/ITetrisApi: cliente C# de referencia adaptado al nuevo registro y recuperación, conservando Bearer.

El código nuevo usa nombres en español, llaves y métodos de varias líneas. No hubo reorganización general ni cambios al motor del Tetris.

## Pruebas ejecutadas y resultado

| Verificación | Resultado final |
| --- | --- |
| Solución completa, salida bin/Verificacion/net10.0 | Compilación correcta; 0 advertencias y 0 errores |
| Suite de lógica y regresión | 149/149 correctas |
| Suite HTTP y SignalR en localhost | 172 comprobaciones correctas |
| Script completo en SQL Server | Correcto en base temporal Nueva |
| Actualización de estructura anterior | Correcta en base temporal Migrada |
| Repetición de la migración | Correcta |
| Duplicados legados | Se detuvo y conservó esquema/datos |
| Históricos y relaciones sintéticos | Conservados |
| Registros simultáneos, índices, hashes, vencimiento, inactividad y revocación | Correctos en ambas estructuras SQL |
| Tres modos, siete efectos, estadísticas, cancelación y dos rondas | Correctos en ambas estructuras SQL |
| Limpieza de pruebas SQL | Bases temporales eliminadas; TetrisDB sin migraciones |

Se conservaron todas las pruebas anteriores. Se actualizaron expectativas legítimas del acceso tradicional, el total de entidades (19 anteriores más la nueva credencial) y la corona por omisión. Durante el trabajo se corrigieron fallos de compilación en las pruebas nuevas, un dato sintético de expiración incompatible con su restricción de fechas, y dos defectos del script de migración (SQL dinámico y repetición sobre un índice filtrado). Las ejecuciones finales indicadas pasaron; no quedan fallos conocidos en esas suites.

La compilación original en bin/Debug encontró archivos bloqueados por una instancia del backend abierta; se verificó toda la solución en una salida alternativa sin detener esa instancia. HTTP y SQL requirieron acceso fuera del entorno restringido; se ejecutaron con los permisos solicitados.

## Pendientes para ponerlo en uso

1. Revisar los cambios en GitHub Desktop.
2. Detener el backend anterior, respaldar TetrisDB con CHECKSUM y ejecutar RESTORE VERIFYONLY. Revisar y aplicar la migración preparada; no usar el script completo sobre una base existente.
3. Reiniciar el backend actualizado con conexión y secreto JWT configurados fuera del código.
4. Integrar en Ionic el registro por nickname, almacén seguro Android, recuperación al abrir, renovación coordinada, Bearer/SignalR, coronas y botón de nueva ronda. El frontend no fue implementado por solicitud expresa.

Limitaciones documentadas: no hay recuperación de una credencial perdida ni acceso a cuentas ajenas por nickname. Las cuentas anteriores conservan todos sus datos y sus nombres reservados, pero no reciben automáticamente credenciales: migrar su acceso requerirá demostrar propiedad mediante un mecanismo posterior. Las salas y victorias de sala siguen en memoria y desaparecen al reiniciar el servidor; los resultados SQL permanecen. Estas limitaciones no permiten apropiación de perfiles.

## Archivos modificados o agregados

Rutas relativas a la carpeta de la solución. Este informe también es un archivo nuevo.

- [AccesoDatos/Contexto/TetrisDBContext.cs](../AccesoDatos/Contexto/TetrisDBContext.cs)
- [AccesoDatos/UnidadTrabajoEF.cs](../AccesoDatos/UnidadTrabajoEF.cs)
- [Dominio/DTO/AutoMapperProfile.cs](../Dominio/DTO/AutoMapperProfile.cs)
- [Dominio/Entidades/CredencialDispositivo.cs](../Dominio/Entidades/CredencialDispositivo.cs)
- [Dominio/Entidades/Usuario.cs](../Dominio/Entidades/Usuario.cs)
- [Dominio/EntidadesTipadas/TAccesoUsuario.cs](../Dominio/EntidadesTipadas/TAccesoUsuario.cs)
- [Dominio/EntidadesTipadas/TAutenticacion.cs](../Dominio/EntidadesTipadas/TAutenticacion.cs)
- [Dominio/EntidadesTipadas/TJuego.cs](../Dominio/EntidadesTipadas/TJuego.cs)
- [Dominio/EntidadesTipadas/TUsuario.cs](../Dominio/EntidadesTipadas/TUsuario.cs)
- [Dominio/InterfazAD/IUnidadTrabajoEF.cs](../Dominio/InterfazAD/IUnidadTrabajoEF.cs)
- [Dominio/InterfazJuego/ISalaLJ.cs](../Dominio/InterfazJuego/ISalaLJ.cs)
- [Dominio/InterfazLJ/IAccesoUsuarioLJ.cs](../Dominio/InterfazLJ/IAccesoUsuarioLJ.cs)
- [LogicaNegocio/Implementacions/AccesoUsuarioLJ.cs](../LogicaNegocio/Implementacions/AccesoUsuarioLJ.cs)
- [LogicaNegocio/Implementacions/UsuarioLJ.cs](../LogicaNegocio/Implementacions/UsuarioLJ.cs)
- [LogicaNegocio/Servicios/ProyectorSala.cs](../LogicaNegocio/Servicios/ProyectorSala.cs)
- [LogicaNegocio/Servicios/RegistroPartidaLJ.cs](../LogicaNegocio/Servicios/RegistroPartidaLJ.cs)
- [LogicaNegocio/Servicios/SalaLJ.cs](../LogicaNegocio/Servicios/SalaLJ.cs)
- [LogicaNegocio/Servicios/SalaMemoria.cs](../LogicaNegocio/Servicios/SalaMemoria.cs)
- [ProyectoTetris_PrograV/ConfiguracionApi.cs](../ProyectoTetris_PrograV/ConfiguracionApi.cs)
- [ProyectoTetris_PrograV/Controllers/SalasController.cs](../ProyectoTetris_PrograV/Controllers/SalasController.cs)
- [ProyectoTetris_PrograV/Controllers/UsuariosController.cs](../ProyectoTetris_PrograV/Controllers/UsuariosController.cs)
- [ProyectoTetris_PrograV/Program.cs](../ProyectoTetris_PrograV/Program.cs)
- [ProyectoTetris_PrograV/Seguridad/ConfiguracionSeguridad.cs](../ProyectoTetris_PrograV/Seguridad/ConfiguracionSeguridad.cs)
- [ProyectoTetris_PrograV/Seguridad/FiltroSesionJuego.cs](../ProyectoTetris_PrograV/Seguridad/FiltroSesionJuego.cs)
- [ProyectoTetris_PrograV/Seguridad/ServicioToken.cs](../ProyectoTetris_PrograV/Seguridad/ServicioToken.cs)
- [README.md](../README.md)
- [Tetris.Cliente/Servicios/ITetrisApi.cs](../Tetris.Cliente/Servicios/ITetrisApi.cs)
- [Tetris.Cliente/Servicios/TetrisApi.cs](../Tetris.Cliente/Servicios/TetrisApi.cs)
- [Tetris.Pruebas/Program.cs](../Tetris.Pruebas/Program.cs)
- [Tetris.Pruebas/PruebasDefinitivas.cs](../Tetris.Pruebas/PruebasDefinitivas.cs)
- [Tetris.Pruebas/PruebasHttp.cs](../Tetris.Pruebas/PruebasHttp.cs)
- [Tetris.Pruebas/PruebasIdentidadSql.cs](../Tetris.Pruebas/PruebasIdentidadSql.cs)
- [Tetris.Pruebas/PruebasSql.cs](../Tetris.Pruebas/PruebasSql.cs)
- [Utilitarios/Respuesta.cs](../Utilitarios/Respuesta.cs)
- [docs/CAMBIOS_IONIC.md](../docs/CAMBIOS_IONIC.md)
- [docs/FRONTEND.md](../docs/FRONTEND.md)
- [docs/USUARIOS_CORONAS_RONDAS.md](../docs/USUARIOS_CORONAS_RONDAS.md)
- [docs/contratos/tetris.model.ts](../docs/contratos/tetris.model.ts)
- [docs/sql/04_usuarios_dispositivos_rondas.sql](../docs/sql/04_usuarios_dispositivos_rondas.sql)
- [docs/sql/ProbarUsuariosYRondas.ps1](../docs/sql/ProbarUsuariosYRondas.ps1)
- [docs/sql/TetrisDB_Completa_Actualizada.txt](../docs/sql/TetrisDB_Completa_Actualizada.txt)
