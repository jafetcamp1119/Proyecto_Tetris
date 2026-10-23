# Acceso por nickname, coronas y rondas

Esta guía sustituye las instrucciones anteriores de registro con correo, contraseña y cookies. El frontend Ionic no se modificó. Los contratos de referencia están en contratos/tetris.model.ts.

## Registrar y comprobar el nickname

GET /api/usuarios/disponibilidad?nombreUsuario=DragonCR devuelve HTTP 200:

~~~json
{"success":true,"data":true,"error":null,"codigoError":null}
~~~

Un nombre ocupado devuelve data:false. La consulta no reserva el nombre. El registro definitivo puede responder 409 si otro dispositivo lo tomó antes.

POST /api/usuarios/registrar recibe únicamente:

~~~json
{"nombreUsuario":"DragonCR"}
~~~

Se eliminan espacios exteriores, se permiten de 1 a 50 caracteres y se rechazan caracteres de control. C# comprueba el nombre y SQL protege la unicidad con una columna calculada e índice único insensible a mayúsculas. La normalización no intenta detectar nombres visualmente parecidos.

Respuesta HTTP 201 (valores secretos representados por marcadores, no son credenciales utilizables):

~~~json
{
  "success": true,
  "data": {
    "usuarioId": 12,
    "nombreUsuario": "DragonCR",
    "fechaRegistro": "2026-10-23T12:00:00Z",
    "activo": true,
    "token": "<JWT-de-acceso>",
    "tipoToken": "Bearer",
    "expiraEnUtc": "2026-10-23T13:00:00Z",
    "credencial": "<secreto-aleatorio-de-64-caracteres-hexadecimales>",
    "credencialExpiraEnUtc": "2027-10-23T12:00:00Z"
  },
  "error": null,
  "codigoError": null
}
~~~

La duración real del JWT procede de Jwt:MinutosExpiracion. La credencial persistente se entrega solamente al registrar. No aparece al consultar el perfil ni al recuperar una sesión. Las respuestas de acceso llevan Cache-Control: no-store.

HTTP 409 devuelve NombreOcupado y el mensaje: «Este nombre de usuario ya está en uso. Elegí otro.» Un nombre inválido devuelve 400. Los errores internos devuelven 500 mediante el manejador global y no revelan detalles de SQL.

## Guardar la identidad desde Ionic y Android

Después del 201, guardar inmediatamente credencial en un almacén nativo cifrado que utilice una clave protegida por Android Keystore. El JWT puede permanecer en memoria. usuarioId y nombreUsuario son públicos, pero no demuestran identidad.

Android Keystore protege claves criptográficas; el adaptador nativo debe usar esa clave para cifrar el secreto en el almacenamiento privado de la aplicación. El contrato del backend no exige un plugin comercial determinado. No guardar el secreto en localStorage, Capacitor Preferences sin cifrado, URLs, registros de consola, analítica ni en el repositorio. Excluir la credencial y el cifrado de copias de seguridad que puedan restaurarse sin la clave del dispositivo. Usar HTTPS fuera del entorno local de desarrollo.

Referencia oficial: [Android Keystore](https://developer.android.com/privacy-and-security/keystore).

Si falla el guardado después del registro, conservar la respuesta en memoria y reintentar el guardado: no volver a registrar el mismo nickname. El servidor no puede volver a mostrar el secreto perdido.

## Abrir de nuevo y renovar

Al iniciar Ionic:

1. Leer la credencial del almacenamiento seguro. Si no existe, mostrar la pantalla de nickname.
2. POST /api/usuarios/recuperar con el cuerpo siguiente, sin necesitar un JWT vigente.
3. Guardar el nuevo JWT en memoria y usar el mismo usuarioId/perfil.

~~~json
{"credencial":"<credencial-privada-guardada-al-registrarse>"}
~~~

POST /api/usuarios/renovar acepta exactamente el mismo contrato. Ambas rutas devuelven 200 con IAutenticacion, sin la propiedad credencial. Mantener el secreto original. Cada uso válido prolonga la validez persistente a 365 días desde ese momento. No se rota el secreto, evitando perder el perfil por respuestas perdidas o renovaciones simultáneas; sí cambia cada JWT.

Renovar antes de expiraEnUtc. Un interceptor puede coordinar una sola renovación para las solicitudes concurrentes. Ante 401 con SesionVencida, renovar y reintentar una sola vez; no crear un perfil nuevo automáticamente. Si falla la red o hay 500, conservar el secreto y ofrecer reintento. Si /recuperar devuelve 401 con CredencialInvalida, mostrar una pantalla que explique que no puede abrirse ese perfil y permita crear otro nickname disponible.

GET /api/usuarios/yo necesita Authorization: Bearer <JWT>. Devuelve solamente el perfil público. Ninguna ruta acepta un usuarioId elegido por el cliente para actuar como otra persona.

El secreto tiene 256 bits aleatorios, generados con RandomNumberGenerator. SQL guarda únicamente su SHA-256. La credencial funciona como un token al portador: quien la copie puede usarla; el almacenamiento seguro protege esa prueba de identidad. No es una contraseña oculta ni un identificador Android público.

## Invalidación y pérdida del perfil

POST /api/usuarios/logout requiere Bearer y revoca la credencial de ese dispositivo. Los JWT de esa sesión dejan de autenticar HTTP y nuevas conexiones SignalR; el filtro del hub rechaza nuevas acciones de conexiones ya abiertas. Desactivar el usuario también invalida sus sesiones. Las conexiones sin acciones se cierran al expirar su JWT; el frontend debe detener el hub al cerrar sesión.

Cerrar la aplicación normalmente NO debe llamar logout ni borrar el secreto. Para este flujo sin recuperación, un botón que revoque y borre la identidad debe explicar que puede perderse definitivamente el acceso al nickname.

Desinstalar, borrar datos, perder la credencial, revocarla o dejarla vencer por más de 365 días sin uso puede impedir recuperar el perfil. No existe recuperación por correo, contraseña, nickname ni preguntas de seguridad. No se permite apropiarse de nombres reservados.

Los usuarios anteriores conservan sus identificadores, estadísticas, correos y hashes legados, pero esos campos ya no habilitan login. No se crean credenciales automáticamente para ellos ni se entregan por nickname. Para una migración de acceso de esas cuentas hará falta un procedimiento posterior que compruebe su propiedad; en esta versión permanecen reservadas y el jugador puede crear otro nickname.

## Endpoints y estados

| Método y ruta | Uso | HTTP |
| --- | --- | --- |
| POST /api/usuarios/registrar | Crear identidad, credencial y JWT | 201 / 400 / 409 / 500 |
| GET /api/usuarios/disponibilidad?nombreUsuario=... | Consultar disponibilidad pública | 200 / 400 / 500 |
| POST /api/usuarios/recuperar | Reconocer el dispositivo | 200 / 400 / 401 / 500 |
| POST /api/usuarios/renovar | Obtener otro JWT con el secreto | 200 / 400 / 401 / 500 |
| GET /api/usuarios/yo | Perfil actual con Bearer | 200 / 401 / 500 |
| POST /api/usuarios/logout | Revocar identidad del dispositivo | 204 / 401 / 500 |
| POST /api/salas/{id}/nueva-ronda | Preparar siguiente ronda, solo creador | 200 / 400 / 401 |

POST /api/usuarios/login fue retirado y responde 404. No se emiten ni se aceptan cookies de acceso. Los cuerpos mal formados o incompletos se rechazan con 400 mediante Respuesta<T>. Los 401 distinguen SesionVencida, SesionInvalida o CredencialInvalida mediante codigoError.

## SignalR

Se conserva /hubs/juego y los métodos Entrar, Mover, SolicitarPausa, VotarPausa, Reanudar y Abandonar. Configurar accessTokenFactory para obtener el JWT actual; renovar previamente si está por vencer. El token de acceso viaja como Bearer o access_token únicamente en el hub. Nunca enviar la credencial persistente en la URL ni al hub.

Cuando el JWT expire, el servidor cierra la conexión. Renovar, reconectar y volver a invocar Entrar(salaId) dentro del plazo existente de 30 segundos. La secuencia de movimientos y el estado autoritativo se recuperan del servidor. Los permisos y el usuario provienen del JWT y de la pertenencia a la sala.

## Coronas y victorias

TPuntuacion/IJugador conserva nombre, puntos y posicion, y publica:

~~~json
{"usuarioId":12,"nombre":"DragonCR","puntos":900,"posicion":1,"victoriasSala":2,"corona":true,"ganoRonda":true}
~~~

Mostrar una corona de resultados cuando ganoRonda sea true. Mostrar una corona en la sala cuando corona sea true. Son distintivos independientes: un líder de sala puede perder la ronda más reciente. Ninguno altera puntos, piezas ni permisos.

En competitivo se conserva el máximo de puntos al perder todos. Todos los empatados comparten primer lugar, ganoRonda=true y suman una victoria. El tiempo de supervivencia no desempata. En compartido se conservan supervivencia y meta. Individual no recibe coronas multijugador.

victoriasSala comienza en cero. Al finalizar una ronda se incrementa una sola vez para cada ganador. Los líderes con al menos una victoria comparten corona por omisión; minimoVictoriasCorona conserva el umbral configurable existente. Si todos tienen cero, nadie lleva corona. Son contadores de la sala en memoria, separados de EstadisticasJugador. Reiniciar el servidor descarta salas y sus contadores, como en la arquitectura anterior; los resultados guardados permanecen en SQL.

## Otra ronda en la misma sala

1. Esperar Estado=Finalizada y el guardado final del servicio de persistencia.
2. El creador llama POST /api/salas/{id}/nueva-ronda sin cuerpo. Una partida activa, una sala cancelada o un invitado no pueden hacerlo. Si SQL todavía guarda el final, devuelve 400 con un mensaje para reintentar.
3. La sala pasa a Esperando y aumenta numeroRonda. Conserva identificador, código público/privado, creador, configuración, conexiones y victorias. Retira participantes que abandonaron.
4. Todos vuelven a marcar listo mediante PUT /api/salas/{id}/listo con true. El creador usa POST /api/salas/{id}/iniciar. Se conservan los mínimos actuales de jugadores y conexión.

Se limpian tableros, puntos, líneas, nivel, secuencias de movimientos, eliminaciones, efectos, pausa, votos y solicitudes usadas de la ronda. La versión de sala y la secuencia de activaciones especiales siguen creciendo para no confundir eventos antiguos.

Cada ronda crea una Partida independiente. El código SQL de la primera conserva el código previo y las siguientes usan codigo-2, codigo-3, etc.; el código de invitación público/privado no cambia. Ampliar CodigoSala a 24 permite conservar ese identificador sin truncar. Los resultados y estadísticas anteriores no se sobrescriben.

## SQL y despliegue

No ejecutar el script completo sobre TetrisDB existente. En producción/local real:

1. Detener el backend durante el mantenimiento y realizar un respaldo completo con CHECKSUM a una ruta accesible para el servicio SQL Server. Ejecutar RESTORE VERIFYONLY sobre ese archivo. Conservarlo fuera de Git.
2. Revisar 04_usuarios_dispositivos_rondas.sql. Cambiar @RespaldoVerificado a 1 únicamente después del respaldo verificado.
3. Ejecutar el script de actualización en SSMS o sqlcmd. La operación es transaccional y se detiene si encuentra nicknames legados duplicados normalizados. Resolver esos casos manualmente preservando identidad y relaciones; no borra ni renombra usuarios.
4. Iniciar el backend actualizado con ConnectionStrings:TetrisDB y Jwt:Clave configurados mediante variables de entorno o secretos de usuario. La clave JWT de desarrollo en memoria cambia al reiniciar; la credencial permite renovar después del reinicio.
5. Verificar registro, recuperación y juego. Después integrar Ionic siguiendo esta guía.

Se hacen opcionales Correo y PasswordHash, conservando sus datos. El índice de Correo pasa a ser único filtrado para admitir varios NULL. Se agrega NombreUsuarioNormalizado calculado e índice único y CredencialesDispositivo con clave foránea a Usuarios. Se amplía CodigoSala sin truncamiento. DeterminarGanador pasa a consultar los ganadores ya guardados por el motor, sin resolver empates de manera distinta ni finalizar partidas activas.

TetrisDB_Completa_Actualizada.txt crea las tablas existentes, las nuevas credenciales, relaciones, restricciones, vista de ranking, procedimientos y catálogos (siete piezas normales y siete especiales), sin jugadores ni secretos. Usar SQLCMD Mode en SSMS por :ON ERROR EXIT. Se detiene si la base ya existe.

Referencia de índices: [documentación oficial de EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/indexes).

## Verificación reproducible

Desde la carpeta de la solución:

~~~powershell
dotnet build ProyectoTetris_PrograV.slnx --no-restore -m:1 -p:OutputPath=bin/Verificacion/net10.0/
dotnet Tetris.Pruebas/bin/Verificacion/net10.0/Tetris.Pruebas.dll
dotnet Tetris.Pruebas/bin/Verificacion/net10.0/Tetris.Pruebas.dll --integracion-http
./docs/sql/ProbarUsuariosYRondas.ps1
~~~

La salida alternativa permite compilar aunque otra instancia tenga bloqueado bin/Debug. La suite es un ejecutable propio, por eso se usa dotnet con la DLL en lugar de depender de dotnet test.

ProbarUsuariosYRondas.ps1 crea dos bases TetrisValidacion_* nuevas, comprueba creación completa, migración, bloqueo de duplicados legados, repetición segura del script, conservación de un histórico sintético, ocho registros simultáneos, hashes, expiración, revocación, tres modos, catálogos y estadísticas. Las elimina al terminar. Nunca migra TetrisDB y compara sus cantidades antes/después. Requiere permisos locales de SQL para crear bases temporales y un puerto localhost para las pruebas HTTP separadas.
