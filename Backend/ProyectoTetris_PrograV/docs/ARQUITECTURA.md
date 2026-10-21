# Arquitectura y decisiones

## Capas

| Proyecto | Responsabilidad | Referencias propias |
|---|---|---|
| Utilitarios | Respuesta<T> | Ninguna |
| Dominio | Entidades EF, tipos T, contratos y estado del juego | Utilitarios |
| AccesoDatos | Contexto SQL Server, repositorio y unidad de trabajo | Dominio |
| LogicaNegocio (ensamblado LogicaJuego) | Validaciones, motor, salas y registro | Dominio, Utilitarios |
| ProyectoTetris_PrograV | API, autenticación, SignalR y tareas de ejecución | AccesoDatos, LogicaNegocio, Dominio, Utilitarios |
| Tetris.Cliente | Servicio HTTP y contrato de canal en tiempo real | Dominio |
| Tetris.Pruebas | Comprobaciones de reglas y persistencia | LogicaNegocio, AccesoDatos, Tetris.Cliente |

Se sigue el patrón de la práctica del profesor. Los TUsuario, TPartida, etc. ya son DTO;
no se añadieron copias equivalentes. AutoMapperProfile sigue en Dominio/DTO.
Las entidades persistentes conservan sus nombres y propiedades, incluidos los nombres
generados por EF como ParticipantesPartidum. Sus espacios de nombres ahora son Dominio.Entidades.

## Recorrido de una operación

Pantalla → ITetrisApi → Controller → interfaz LJ → clase LJ → IUnidadTrabajoEF →
IRepositorioAD<T> → EF Core → SQL Server.

Los controllers no consultan directamente la base. Program.cs registra las dependencias y
valida sus alcances. DbContext y unidad de trabajo viven en un alcance corto. Los servicios de
salas y el motor se comparten como singleton; no reciben servicios scoped.

Se conservaron las firmas síncronas del CRUD original para evitar romper sus consumidores.
El registro de partidas tiene guardado asíncrono. BaseLJ comparte únicamente el mantenimiento
persistente de cinco clases antes vacías. UsuarioLJ conserva su implementación original con
validaciones y propagación de errores corregidas.

El repositorio prepara las altas/cambios/bajas. Completar/CompletarAsync confirma una sola vez.
CompletarTran permite confirmar una transacción explícita. Los errores de infraestructura se
registran y la LJ devuelve Respuesta<T> con Success=false. No se entrega la excepción SQL al cliente.

## Motor y memoria

IMotorJuego/MotorJuego implementan siete tetrominós, caída, movimientos, rotación básica,
colisiones, fijación, limpieza de líneas, nivel y derrota. Las rotaciones no incluyen el sistema
avanzado SRS ni desplazamientos especiales contra paredes.
La gravedad parte de 800 ms, disminuye 60 ms por nivel y tiene un mínimo de 100 ms.

Puntos: 100/300/500/800 por 1/2/3/4 líneas, multiplicados por el nivel anterior a la limpieza;
bajada manual +1 por celda y caída instantánea +2 por celda. El nivel sube cada diez líneas.

TableroJuego es estado en memoria. TTablero es una instantánea: matriz de celdas fijas y coordenadas
de la pieza activa separadas. El cliente dibuja ambos. SalaLJ devuelve copias y procesa cada sala
bajo su propio bloqueo. RelojJuegoService avanza las salas cada 100 ms y publica cambios.

No se escriben movimientos, posiciones ni bloques en SQL. Se conservan las entidades existentes
de tableros, recompensas, piezas especiales, pausas y eventos para continuar su desarrollo;
su modelado no significa que todas esas mecánicas estén implementadas.

## Reglas de los modos

- Individual: una persona, capacidad fijada en uno, un tablero; termina al perder.
- Compartido: al menos cuatro personas al iniciar, un tablero y turno por pieza fijada.
  Solo actúa quien tiene el turno. Las líneas y puntos se asignan al participante que coloca la pieza.
  La derrota elimina solo al dueño del turno, limpia las diez filas superiores sin puntos y continúa.
  El último activo gana; también termina si alguien alcanza su meta personal de puntos.
- Competitivo: al menos cuatro personas al iniciar, un tablero por jugador.
  La derrota congela sus puntos. El último activo continúa. La partida termina cuando todos pierden.
  Gana la mayor puntuación, independientemente del orden de eliminación.

Los empatados comparten puesto y pueden tener EsGanador=true. En empate GanadorId de Partida es null;
los ganadores se consultan en los resultados. Esto evita adjudicar una victoria por eliminación.
En compartido la supervivencia asigna el primer puesto al único activo, sin comparar sus puntos.

La sala admite hasta 16 participantes. El creador inicia, todos deben estar listos y en multijugador
todos deben haber conectado al hub. El GUID identifica la sala; Codigo ofrece 12 caracteres para invitar a una sala pública.
Una sala privada usa además CodigoPrivado, de 6 caracteres aleatorios generados por el servidor
(clase CodigosSala). UnirsePorCodigo resuelve el código y verifica capacidad y estado.

Las acciones llevan Secuencia creciente por jugador y sala. Un duplicado se rechaza.
TPuntuacion.UltimaSecuencia permite continuar tras una reconexión.
Se permiten varias conexiones de la misma cuenta; desconectar una no elimina las restantes.
Una desconexión total concede 30 segundos reales de reconexión, también durante pausas.
Se detiene únicamente el tablero del desconectado (o el turno compartido) hasta su regreso o vencimiento.
Vencido ese plazo, se trata como derrota/abandono; en compartido se salta el turno.
Abandonar existe en REST y SignalR. El creador cierra y cancela sin ganadores; los demás se eliminan
conservando sus puntos. Los grupos SignalR retiran las conexiones de quienes salen.
La pausa (individual y por votación) se describe en docs/REGLAS_NUEVAS.md: PausaPartida concentra
sus reglas, ControlPausas las aplica a la sala y genera los eventos, y PublicadorSalas los envía por SignalR.

Las salas de espera caducan tras dos horas; los finales se retiran dos horas después de guardarse.
No se recuperan salas activas tras reiniciar. GuardadoPartidasService cancela los registros incompletos
antes de guardar las nuevas salas, elimina sus resultados provisionales y conserva los finales.
No hay soporte para varios servidores.
No se agregaron Redis, colas, microservicios ni persistencia de cada movimiento.

## Persistencia de la partida

GuardadoPartidasService trabaja con un reloj independiente y un DbContext nuevo por operación.
Guarda al iniciar, al perder participantes y al finalizar; una falla de SQL deja la revisión pendiente
y se reintenta. La simulación continúa. Las puntuaciones en curso siguen disponibles en la sala.

RegistroPartidaLJ guarda los participantes y crea el Resultado al perder. PosicionFinal=0 significa
clasificación pendiente. Al finalizar actualiza posiciones, ganadores y estadísticas, en un solo
SaveChanges atómico. Si recibe otra vez un final ya guardado no duplica estadísticas.
Las confirmaciones de revisión no descartan cambios que ocurrieron mientras SQL guardaba.

El esquema existente tenía CK_Partidas_Jugadores con mínimo >=2. El script 01_modalidades.sql
lo amplía a >=1 y cambia el DEFAULT a cuatro; la LJ exige cuatro en multijugador.
Se mantiene la relación persistente de un tablero por partida porque los tableros competitivos
viven en memoria. No se han eliminado tablas ni datos existentes.
EsPrivada se guarda con el valor de la sala. Las reglas de privacidad están en SalaLJ y se explican
en docs/REGLAS_NUEVAS.md. El código de 12 caracteres sigue siendo el identificador único que se guarda
en Partidas.CodigoSala; el código privado de 6 caracteres solo existe en memoria mientras la sala vive.

## Autenticación y cliente Ionic

El frontend definitivo será Ionic + Angular + Capacitor. Tetris.Cliente se conserva como
cliente C# independiente y referencia de contratos; no es una dependencia de la APK.

Registrar utiliza la validación existente y PasswordHasher<Usuario>. Login devuelve
Respuesta<TAutenticacion>: datos públicos del usuario, Token, TipoToken y ExpiraEnUtc.
TAutenticacion hereda TUsuario para mantener compatibles los campos del login anterior.
El cliente C# conserva su firma LoginAsync y utiliza el token automáticamente.

ConfiguracionSeguridad valida firma HS256, emisor, audiencia, vencimiento e identidad positiva.
Una política selecciona Bearer cuando recibe Authorization o access_token en el hub; en los
demás casos admite la cookie anterior. Los controllers y el hub siguen obteniendo UsuarioId
de la identidad validada. PasswordHash nunca se serializa.

La clave de firma se carga de Jwt:Clave. Development puede generar una clave temporal en
memoria; Production exige una configurada externamente. No hay claves en el repositorio.
Logout elimina la cookie, pero los JWT emitidos viven hasta su vencimiento. El frontend
elimina el token y detiene el hub; no se implementaron refresh tokens ni una lista de revocación.

ConfiguracionApi concentra JSON, validación de modelos y configuración del protocolo SignalR.
No contiene reglas de juego. La respuesta 400 de validación conserva Respuesta<T>.

## Red local y entornos

El perfil Tetris.API utiliza Development y escucha en 0.0.0.0:5186; Tetris.SoloLocal limita
la escucha a localhost. Los celulares usan la IPv4 actual del equipo. La API no necesita IP fija.
appsettings.Development.json enumera orígenes concretos de Ionic, Angular y Capacitor.
La lista base de Production está vacía hasta configurarse. No se habilita cualquier origen.
No se modificó el firewall; docs/FRONTEND.md muestra una regla privada limitada a TCP 5186.

JuegoHub conserva Entrar, Mover y EstadoActualizado. El token en query se acepta únicamente
en /hubs/juego; el REST exige el encabezado Bearer o una cookie válida.
CloseOnAuthenticationExpiration cierra conexiones vencidas. La reconexión debe volver a
invocar Entrar para recuperar grupo y estado. Las reglas y el plazo de 30 segundos se conservan.

## Reorganización de 21 de octubre de 2026

SalaLJ se dividió por responsabilidades sin cambiar sus reglas: SalaMemoria (estado interno),
ProyectorSala (conversión a DTO), ControlPausas, CodigosSala y CatalogoPiezasEspeciales.
GeneradorPiezas concentra el azar del motor. RegistroPartidaLJ.GuardarAsync y
ConfiguracionSeguridad.AgregarSeguridad, que superaban las 100 líneas, ahora delegan en métodos privados.
Los tipos de pieza especial se leen de SQL una vez por sala; la partida no consulta la base de datos.

## Legibilidad

El código manual utiliza constructores explícitos, bloques con llaves, métodos con cuerpo
y parámetros largos distribuidos en varias líneas. No se refactorizaron las entidades ni el
contexto generado por EF. .editorconfig conserva estas preferencias para futuras ediciones.

SalaLJ.Avanzar delega caducidad, desconexiones y caída a métodos privados con una sola función.
MotorJuego.Cabe comprueba cada bloque mediante condiciones explícitas. Estado construye la
instantánea paso a paso. No cambian puntuaciones, turnos, eliminación ni clasificación.

## Archivos conservados y cambios

- Dominio/Entidades/*.cs: mismas 19 entidades y relaciones; namespace corregido.
- Dominio/EntidadesTipadas y DTO: tipos originales conservados; hash excluido y nuevos contratos de acceso/juego.
- Dominio/InterfazAD, AccesoDatos/RepositorioAD y UnidadTrabajoEF: contratos e implementaciones completados.
- AccesoDatos/Contexto/TetrisDBContext.cs: mantiene mapeos; conexión externa y DEFAULT mínimo cuatro.
- LogicaNegocio/Implementacions: UsuarioLJ ajustado; otras cinco LJ implementadas mediante BaseLJ.
- LogicaNegocio/Motor y Servicios: motor, sesiones y guardado por revisiones.
- ProyectoTetris_PrograV: biblioteca vacía convertida en servidor web.
- Tetris.Cliente: base de servicios, sin diseños de pantalla.
- Tetris.Pruebas: pruebas del motor, reglas, mapeos y persistencia.
- .csproj y .slnx: referencias corregidas; retirados paquetes de negocio sin uso.
- AccesoDatos: referencia explícita a System.Security.Cryptography.Pkcs 10.0.13 para resolver los
  avisos transitivos detectados por NuGet. Fuente: https://github.com/advisories/GHSA-846f-f764-76mx

## Verificación

Comprobaciones sin base de datos:

~~~powershell
dotnet run --project Tetris.Pruebas/Tetris.Pruebas.csproj
~~~

Lectura de restricciones del SQL existente:

~~~powershell
dotnet run --project Tetris.Pruebas/Tetris.Pruebas.csproj -- --verificar-sql
~~~

Integración con SQL dentro de una transacción que siempre revierte:

~~~powershell
dotnet run --project Tetris.Pruebas/Tetris.Pruebas.csproj -- --integracion-sql
~~~

La prueba de integración crea usuarios y partidas temporales y verifica que el rollback no deja registros.
Consume valores IDENTITY aunque revierta las filas, comportamiento normal de SQL Server.
ConnectionStrings__TetrisDB permite apuntar estas pruebas a otra base compatible.


