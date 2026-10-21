# Informe de actualización definitiva — 21 de octubre de 2026

## Funcionalidades y reglas corregidas

Se conservan Dominio, AccesoDatos, LogicaNegocio, Utilitarios, servicios, repositorios,
unidad de trabajo y API. No se modificaron archivos de Ionic ni entidades autogeneradas.

### Competitivo clásico

Cada participante juega simultáneamente en su tablero. Se publican todas las puntuaciones.
Quien pierde conserva sus puntos; los demás siguen. El último continúa hasta perder.
Solo al perder todos se finaliza y se selecciona la puntuación máxima. Todos los empatados
comparten el primer puesto, sin usar líneas ni tiempo de supervivencia.

Las especiales se fuerzan apagadas al crear, configurar e iniciar. Activarlas en una petición
válida no las habilita ni obliga a consultar SQL. El sorteo solo usa I, O, T, S, Z, J, L;
se conserva la selección existente de formas normales, velocidad y valores de puntuación.
No hay ataques ni poderes.

### Compartido

Solo actúa el dueño del turno. Fijar una pieza pasa al siguiente activo.
La derrota elimina únicamente a ese jugador, que permanece como espectador si no abandonó.
Se vacían las filas 0 a 9 sin mover las inferiores y sin puntos ni líneas adicionales.
La pieza siguiente ya sorteada se conserva, se devuelve a su posición inicial y el tablero sigue.
Los eliminados no mueven, solicitan pausa ni votan.

El último activo gana automáticamente, aunque tenga menos puntos.
También gana quien alcance metaPuntos con sus puntos personales: esta interpretación fue
confirmada por la usuaria en el chat. null desactiva la meta; los valores deben ser positivos.
Las contribuciones individuales siguen siendo las diferencias de puntos del tablero común.
El resultado admite varios ganadores para las decisiones por puntuación.
Si una expiración simultánea deja el compartido sin activos, no se inventa un superviviente ganador.

### Abandonos y cierre

POST /api/salas/{id}/abandonar, sin cuerpo, y el método SignalR Abandonar(salaId)
usan el usuario autenticado. Devuelven Respuesta<ISala>.

- Creador: cierra la sala; cancela una partida activa sin ganadores, cancela la pausa,
  notifica SalaCerrada, retira las conexiones del grupo, libera el código privado y los tableros.
  Una partida finalizada antes del cierre conserva sus resultados y ganadores.
- Participante en espera: se retira de la lista y deja espacio disponible.
- Participante en curso: queda eliminado con su puntuación, sin derecho a volver a esa partida.
  Si tenía el turno compartido pasa al siguiente activo. En competitivo los demás siguen.
  Los datos de clasificación permanecen hasta guardar el resultado.

El código privado se libera verificando también su sala, para que una limpieza posterior
no retire por error un código que ya haya sido asignado a otra sala.
Se conserva una instantánea pendiente de SQL; una cancelación confirmada se retira de memoria
en el siguiente ciclo. Si SQL falla, se reintenta sin retener tableros ni conexiones de la sala cerrada.
Cerrar una sala que nunca inició no crea una partida artificial en SQL.

### Desconexión involuntaria

Al cerrar la última conexión de un jugador comienza un plazo de 30 segundos reales,
también durante una pausa. Cerrar una conexión entre varias no inicia ese plazo.
El tablero desconectado espera; en compartido se espera el turno como máximo hasta el vencimiento.
Los demás tableros competitivos continúan. La pausa sigue congelando la caída.

Entrar(salaId) con el mismo JWT recupera la misma identidad, pieza, tablero, puntuación,
pausa y ultimaSecuencia. No crea un participante nuevo ni vuelve a sortear piezas.
Se vuelve a incorporar la conexión al grupo. Continúa la secuencia desde ultimaSecuencia + 1.
Una conexión SignalR que no ejecutó Entrar no puede mover aunque pertenezca a la misma cuenta.
El servidor también rechaza movimientos del usuario mientras está desconectado.

Al vencer el plazo, el participante normal queda eliminado y pierde el derecho a reingresar.
El creador cierra la sala. Su cancelación se procesa antes de evaluar ganadores, incluso si
varios plazos vencen juntos. El vencimiento se verifica también al atender acciones,
para impedir una reconexión tardía entre dos ciclos del reloj.

### Reinicio y SQL

Las salas viven en memoria y no se recuperan tras un reinicio.
Antes de guardar nuevas sesiones, GuardadoPartidasService solicita a RegistroPartidaLJ
cancelar los registros sin FechaFin. Elimina sus resultados provisionales, deja a los
participantes eliminados sin posición y mantiene intactos los resultados finalizados.
Si SQL no responde, reintenta esa limpieza antes de guardar las nuevas salas.

Los cambios de una partida se guardan con un único SaveChanges de la unidad de trabajo.
Se siguen registrando las puntuaciones al perder y las estadísticas solo al finalizar.
Ahora también se guarda el resultado del ganador que sigue activo en el compartido.
Repetir un final no duplica resultados ni victorias. Cancelar no genera estadísticas de victoria.
Un final aún no guardado al apagarse el proceso puede perderse; se garantiza conservar
los finales que SQL ya confirmó, conforme al requisito.

### Corona: preparación sencilla

victoriasSala aumenta una vez para cada ganador, incluidos los empatados.
minimoVictoriasCorona es configurable antes de iniciar; null deja la corona desactivada,
pues aún no se ha fijado el criterio definitivo. Si se configura, corona es true para quienes
alcancen el mínimo y compartan el máximo de victorias dentro de la sala.
No modifica puntos ni reglas, ni crea monedas, tiendas o ventajas.

Limitación explícita: la arquitectura actual tiene una partida por sala; se preparó el contador
y su proyección, pero no se agregó una operación para jugar rondas sucesivas en la misma sala.
Por tanto, hoy el contador puede pasar de 0 a 1. Un mínimo mayor que 1 no se alcanza todavía.
No se presenta como un sistema completo de acumulación entre rondas. Tampoco persiste
la corona tras reiniciar, porque las salas se cancelan y no se recuperan.

## Efectos especiales existentes y pendientes

- EliminaColumna vacía todas las columnas ocupadas por la pieza al fijarse.
- EliminaFila elimina la fila de la celda más baja de la pieza y baja las filas superiores.
  Se identifica mediante el código que contiene FILA, conservando el contrato existente de SQL.
- Ningún efecto añade puntos por su limpieza; las líneas completas restantes se puntúan normalmente.
- Ambos funcionan en individual y compartido si están habilitados. Nunca se generan en competitivo.

No se encontraron reglas ejecutables suficientes para añadir ataques, intercambios, poderes
o efectos temporales compartidos. Un tipo de catálogo sin indicadores de fila o columna
puede identificarse como especial, pero no tiene otro efecto implementado.
Esas mecánicas siguen sin definición y no se afirman terminadas.
La interpretación previa de EliminaColumna se conservó por instrucción expresa.

## API y modelos de referencia para Ionic

Los contratos están en contratos/tetris.model.ts. No se tocó el frontend.
Se conservan endpoints, JWT, CORS local, puerto 5186 y /hubs/juego.
Los nombres JSON siguen siendo Individual, Compartido, Competitivo.
Los valores numéricos antiguos siguen siendo 0, 1, 2 respectivamente: no enviar 1/2/3
como si fueran los números de sección del enunciado.

Campos añadidos:

| Objeto | Campo | Uso |
|---|---|---|
| Crear/configurar sala | metaPuntos | Entero positivo o null; meta personal, solo aplicada en compartido |
| Crear/configurar sala | minimoVictoriasCorona | Entero positivo o null; preparación del distintivo |
| ISala | metaPuntos, minimoVictoriasCorona | Configuración efectiva |
| ISala | motivoFin | Supervivencia, MetaPuntos, TodosEliminados o motivo de cancelación |
| EstadoSala | Cancelada | No anunciar ganador |
| IPuntuacion | abandono | Ya no pertenece a la sala; perdido sin abandono es espectador |
| IPuntuacion | reconectarHastaUtc | Límite UTC o null si no espera reconexión |
| IPuntuacion | victoriasSala, corona | Datos decorativos del distintivo |

PUT configuracion reemplaza toda la configuración: los campos omitidos vuelven a sus valores
por omisión. PUT piezas-especiales conserva meta, corona, normales, velocidad y puntos.

Eventos nuevos, además de EstadoActualizado y los eventos de pausa existentes:

| Evento | Cuerpo |
|---|---|
| SalaCerrada | { salaId: string, motivo: string } |
| JugadorAbandono | { salaId: string, usuarioId: number } |

JugadorAbandono también informa una salida por plazo vencido de un participante normal.
La diferencia entre salida voluntaria e involuntaria la controla el servidor mediante
Abandonar y OnDisconnectedAsync; la reconexión solo corresponde al segundo caso.
Los eventos no sustituyen la instantánea autoritativa devuelta por Entrar.

Ionic debe ejecutar Entrar al reconectar, actualizar secuencias y puntuaciones desde su respuesta,
mostrar eliminados como espectadores, habilitar controles según usuarioTurno y perdido,
procesar SalaCerrada volviendo al listado y mostrar todos los IDs de ganadores solo en Finalizada.
El botón de salir debe invocar Abandonar antes de cerrar la conexión; cerrar la conexión solo
inicia el plazo. La corona se dibuja junto al nombre cuando corona sea true.
Ignorar versiones antiguas de EstadoActualizado si llegan fuera de orden.

## Refactorización y archivos

Se mantuvo la arquitectura universitaria. Se separaron eliminación, selección de siguiente turno,
finalización, cierre, autorización de conexión, cancelación de participantes y cálculo de posiciones.
Se añadió TimeProvider opcional para probar los 30 segundos sin esperas ni cambios al reloj del equipo.
Los métodos usan llaves, cuatro espacios e instrucciones separadas. No se añadieron patrones,
paquetes externos ni tablas de recompensas.

Archivos de producción modificados:

- Dominio/EntidadesTipadas/TJuego.cs: campos de configuración, estado y eventos.
- Dominio/Juego/ModoJuego.cs: estado Cancelada, conservando los valores anteriores.
- Dominio/InterfazJuego/ISalaLJ.cs: abandono y autorización de conexión.
- Dominio/InterfazLJ/IRegistroPartidaLJ.cs: limpieza de partidas interrumpidas.
- LogicaNegocio/Servicios/SalaMemoria.cs: abandono, ganadores, meta y contador.
- LogicaNegocio/Servicios/SalaLJ.cs: reglas, cierre, plazos y protección del competitivo.
- LogicaNegocio/Servicios/ProyectorSala.cs: resultados y campos públicos.
- LogicaNegocio/Servicios/ControlPausas.cs: el plazo de reconexión ya no se desplaza con la pausa.
- LogicaNegocio/Servicios/CodigosSala.cs: liberar solo la reserva de la sala correspondiente.
- LogicaNegocio/Servicios/RegistroPartidaLJ.cs: cancelación e inclusión de ganadores activos.
- ProyectoTetris_PrograV/Controllers/SalasController.cs: endpoint de abandono.
- ProyectoTetris_PrograV/Hubs/JuegoHub.cs: abandono y validación de conexión al mover.
- ProyectoTetris_PrograV/Servicios/PublicadorSalas.cs: eventos de ciclo de sala y retiro de grupos.
- ProyectoTetris_PrograV/Servicios/RelojJuegoService.cs: publicación de eventos pendientes aun sin un estado nuevo.
- ProyectoTetris_PrograV/Servicios/GuardadoPartidasService.cs: limpieza inicial con reintentos.

Pruebas modificadas o añadidas:

- Tetris.Pruebas/PruebasDefinitivas.cs (nuevo): abandonos, cancelación, meta, supervivencia,
  limpieza, plazos, conservación de resultados, autorización y corona preparada.
- Tetris.Pruebas/Program.cs: registro de pruebas y actualización del caso de desconexión.
- Tetris.Pruebas/PruebasReglasNuevas.cs: se conservan los casos y se ajustan expectativas antiguas.
- Tetris.Pruebas/PruebasConfiguracionPartida.cs: especiales solo en los modos permitidos.
- Tetris.Pruebas/PruebasHttp.cs: nuevos contratos y ciclo de sala con sockets reales.
- Tetris.Pruebas/PruebasSql.cs: superviviente activo y cancelación transaccional.

Documentación modificada: README.md, este informe (nuevo), REGLAS_NUEVAS.md,
ARQUITECTURA.md, FRONTEND.md, CONFIGURACION_PARTIDA.md, VERIFICACION.md y contratos/tetris.model.ts.

## Verificación y comprobaciones locales

Resultados ejecutados sobre la versión final:

| Verificación | Resultado |
|---|---|
| Compilación de toda la solución | Correcta, 0 advertencias y 0 errores |
| Pruebas de lógica, contratos y persistencia simulada | 123/123 correctas |
| Integración HTTP y SignalR con sockets reales y usuarios en memoria | 84 comprobaciones correctas |
| Integración con SQL Server local | Correcta: tres modos, empates, estadísticas idempotentes, cancelación y conservación de finales |
| Limpieza de datos de integración SQL | Rollback confirmado, sin usuarios de prueba restantes |
| Consulta de restricciones SQL | Conexión correcta; Cancelada y capacidad individual admitidas |

Las pruebas cubren los 16 puntos solicitados: ausencia de especiales en competitivo,
finalización al perder todos, empates, eliminación individual compartida, limpieza de diez filas,
continuidad de turnos, supervivencia, meta personal, abandono del creador y participantes,
reconexión antes del plazo, vencimiento a los 30 segundos, cancelaciones, especiales permitidas,
conservación de finales e integración API/SignalR. También se ejecutaron los casos anteriores
de pausas, salas privadas, configuraciones, puntuaciones, JWT y preferencias.
No se eliminaron pruebas existentes; se actualizaron expectativas que contradecían las nuevas reglas.
El reinicio se verifica mediante la operación de limpieza y conservación, sin afirmar una prueba
física de apagado de la computadora.

La compilación paralela inicial, el acceso a SQL y la apertura de sockets encontraron restricciones
del aislamiento. Se completó la compilación con un nodo y sin compilador compartido, y las
integraciones se ejecutaron con los permisos concedidos fuera del aislamiento.

Comandos reproducibles desde la raíz:

~~~powershell
dotnet build ProyectoTetris_PrograV.slnx --no-restore -m:1 -p:UseSharedCompilation=false
dotnet run --project Tetris.Pruebas --no-build
dotnet run --project Tetris.Pruebas --no-build -- --verificar-sql
dotnet run --project Tetris.Pruebas --no-build -- --integracion-http
dotnet run --project Tetris.Pruebas --no-build -- --integracion-sql
~~~

SQL Server local respondió y se verificó que CK_Partidas_Estado admite Cancelada y que
CK_Partidas_Jugadores admite el individual. No se modificó el esquema ni se ejecutaron migraciones.
Las pruebas SQL crean datos dentro de una transacción y ejecutan rollback. Pueden consumir
valores IDENTITY aunque no conserven filas de prueba.

En la computadora, comprobar la cadena TetrisDB, el servicio SQL Server y los permisos de la cuenta.
Si se traslada a otra base, revisar las mismas restricciones y los tipos especiales activos.
El script sql/01_modalidades.sql documenta el ajuste de capacidad y sql/02_pieza_elimina_fila.sql
el tipo opcional de fila; no se ejecutaron automáticamente en esta actualización.
Guardar una partida, reiniciar y consultar resultados permite revisar el caso real con la instalación final.
Una caída de SQL no detiene el juego, pero un resultado aún no confirmado puede perderse al apagar.

No se probaron teléfonos Android físicos ni la red Wi-Fi. Desde cada teléfono comprobar
http://IPV4-DE-LA-PC:5186/salud, firewall de red privada, CORS, JWT y reconexión tras apagar Wi-Fi.
localhost en el teléfono apunta al teléfono. Seguir FRONTEND.md para Capacitor y HTTP local.
No se generó APK ni se modificó Ionic.

## Pendientes reales

- Definir el umbral definitivo de corona; hoy está desactivada por omisión.
- Si se desea acumulación de varias victorias por sala, definir/agregar rondas sucesivas;
  el contador y el contrato están preparados, la sala actual sigue siendo de una partida.
- Definir efectos especiales adicionales antes de programarlos; no hay reglas para inventarlos.
- Implementación visual en Ionic y validación con varios teléfonos físicos.
- El historial detallado de pausas continúa en memoria; no se añadió auditoría SQL de pausas.

La recuperación de partidas activas y un sistema de recompensas no son pendientes de esta solicitud:
se excluyeron expresamente.
