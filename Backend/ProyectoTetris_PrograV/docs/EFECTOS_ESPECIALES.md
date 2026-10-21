# Piezas especiales y efectos del servidor

Actualización del 21 de octubre de 2026. Este documento sustituye los pendientes de
efectos descritos en ACTUALIZACION_DEFINITIVA.md.

## Comportamiento

| Tipo | Activación al fijar la pieza | Puntuación |
| --- | --- | --- |
| Eliminar fila | Retira la fila de la celda más baja y baja las filas superiores. Conserva la regla existente. | La fila retirada no puntúa ni suma líneas. |
| Eliminar columna | Vacía todas las columnas ocupadas por la pieza. Conserva la regla existente. | La limpieza no puntúa. |
| Confusión | Invierte izquierda y derecha del siguiente jugador activo. Rotación, bajada y caída siguen iguales. | Sin bonificación. |
| Acelerador | Divide por dos el intervalo de gravedad del siguiente jugador activo durante diez segundos. | Sin bonificación. |
| Bomba | Vacía un área de 3 × 3. Su centro es la celda más baja de la pieza; entre empates, la situada más a la izquierda. El área se recorta contra los bordes. No aplica gravedad a los bloques sueltos. | La explosión no puntúa ni suma líneas. |
| Doble puntuación | Duplica únicamente la recompensa por líneas completas de la jugada. | Recompensa personalizada × nivel anterior a la eliminación × 2. Sin líneas, no hay bonificación. Los puntos normales de caída no se duplican. |
| Barredora | Limpia las filas 0 a 9 del tablero compartido al fijarse, sin desplazar las inferiores. En otra altura limpia las primeras altura / 2 filas; conserva la central si la altura es impar. | No puntúa la limpieza ni realiza una eliminación adicional de líneas en esa jugada. Conserva los puntos normales de caída. |

Las limpiezas ocurren antes del conteo normal de líneas. Salvo en la jugada de Barredora, las líneas completas que
permanezcan se puntúan normalmente. Barredora conserva las filas inferiores sin compactarlas ni puntuarlas. No se duplica el total acumulado ni se conserva
el multiplicador para otra pieza.

El compartido conserva un solo tablero y los turnos por pieza. Los efectos que limpian
actúan sobre ese tablero. Confusión y Acelerador se asocian al jugador, por lo que no
afectan a otro usuario cuando cambia el turno. Al perder se conservan la eliminación,
el estado de espectador, la limpieza de las diez filas superiores sin puntos y las
condiciones de victoria existentes.

El competitivo continúa con tableros independientes, exclusivamente piezas normales
y sin ataques. Conserva el final cuando todos pierden, la mayor puntuación y los empates.
El individual conserva las piezas normales por defecto y la configuración opcional
de especiales que ya tenía. Los ataques temporales se activan solamente en compartido;
fuera de ese modo se informa SinObjetivo.

## Duraciones y pausa

La sección Efectos de appsettings.json define:

~~~json
{
  "SegundosConfusion": 10,
  "SegundosAcelerador": 10,
  "FactorAcelerador": 2
}
~~~

Se valida al arrancar: duraciones positivas y factor al menos dos. La aceleración usa
el intervalo correspondiente a velocidad inicial y nivel actual; cuando vence se
vuelve a calcular el intervalo normal. El reloj existente ejecuta ticks cada 100 ms;
la caída efectiva tiene esa precisión y no se acumulan caídas múltiples por un tick tardío.
Los plazos corren mientras la partida está activa, incluso fuera del turno del receptor.
Repetir un ataque del mismo tipo renueva el plazo sin sumar duraciones ni multiplicadores.
Confusión y Acelerador pueden coexistir.

En pausa se calcula el restante en el instante en que comenzó la pausa. Al reanudar se
desplaza FinUtc por la duración real de la pausa, conservando el restante. Un tick
tardío después del fin automático solo congela los tres minutos autorizados.
La votación sigue durando quince segundos, requiere mayoría estricta, permite una
solicitud por jugador y no congela el juego hasta aprobarse. Se conservan los límites
existentes y la reconexión de treinta segundos reales.

## Ataques y validación

El objetivo es el siguiente jugador no eliminado ni abandonado en la lista de turnos.
Nunca es el atacante. Se resuelve al fijar la pieza, después de procesar las derrotas,
bajo el bloqueo de la sala. No se almacenan ataques pendientes: una eliminación antes
del bloqueo se toma en cuenta al elegir al receptor. Un desconectado dentro de su plazo
de reconexión sigue siendo participante activo; al vencer ese plazo queda eliminado.
Si no existe objetivo válido, no se aplica un poder y se informa SinObjetivo.
Los efectos se cancelan al eliminar al receptor o terminar/cancelar la partida.

La secuencia de movimientos existente descarta repeticiones. La doble puntuación
se calcula durante el bloqueo de la pieza y no se vuelve a ejecutar al proyectar
estados, consultar la sala ni reconectar.

## Configuración y catálogo

Los endpoints de creación y configuración mantienen sus rutas y aceptan:

~~~json
{
  "piezasEspeciales": {
    "activas": true,
    "probabilidad": 25,
    "tiposPermitidos": ["CONFUSION", "ACELERADOR", "BOMBA", "DOBLE"]
  }
}
~~~

PUT /api/salas/{id}/piezas-especiales recibe directamente el objeto interior.
Omitir tiposPermitidos o enviar null conserva la compatibilidad: todos los tipos
activos del catálogo. Una selección vacía o con códigos no activos se rechaza.
Los códigos se comparan sin distinguir mayúsculas; los repetidos no aumentan su peso.
Desactivadas: solo normales. Activadas: probabilidad de 1 a 100, con selección uniforme
entre los tipos permitidos después de sortear si corresponde una especial. La selección
de formas tradicionales y sus siete tipos se conserva.

GET /api/salas/tipos-especiales requiere autenticación y devuelve
Respuesta<TipoPiezaEspecial[]> con los códigos activos de SQL para construir el selector.
No genera piezas ni modifica la sala. Los tipos se copian a la configuración al
crear/configurar; la partida no consulta SQL por cada movimiento.

Códigos nuevos: CONFUSION, ACELERADOR, BOMBA, DOBLE y BARREDORA.
DOBLE respeta los diez caracteres de Codigo en el modelo EF actual; el motor también
acepta DOBLE_PUNTUACION como alias para catálogos existentes que permitan esa longitud.
Fila y columna conservan sus indicadores actuales.

## SignalR y contratos Ionic

Se reutiliza EstadoActualizado y las respuestas TSala de REST y JuegoHub. No se agregan
eventos de efecto duplicados. Los DTO existentes de tablero conservan piezaEspecial,
celdas, puntuacion y evento. TSala incorpora:

- efectos: último estado de cada tipo temporal recibido por cada jugador, incluido su
  vencimiento o cancelación. Cada entrada contiene usuarioId, atacanteId, tipo, inicioUtc,
  finUtc, milisegundosRestantes, activo, motivoFin y factorVelocidad.
- ultimoEfecto: última activación de la sala con secuencia creciente, codigo, tipo,
  atacanteId, afectadoId, ahoraServidorUtc, puntosJugada, celdasEliminadas y estado.
  PuntosJugada incluye los puntos normales de caída de esa acción. CeldasEliminadas
  indica las posiciones ocupadas borradas directamente antes de la limpieza normal de
  líneas; para fila son las coordenadas anteriores al desplazamiento.

El estado completo del tablero es la fuente de verdad para dibujar filas desplazadas,
líneas y limpiezas de emergencia. UltimoEfecto no es un historial de activaciones:
una reconexión recupera el estado actual y la última activación. La secuencia permite
no repetir animaciones al recibir varias instantáneas de una misma activación.
Los efectos finalizados permanecen como inactivos hasta recibir otro del mismo tipo.
Cada cambio de restante o de vigencia actualiza la versión y se publica por el reloj.

Ionic debe enviar los movimientos originales; el servidor invierte los horizontales.
Para la cuenta regresiva debe usar milisegundosRestantes y congelarla cuando
pausa.fase sea Pausada. FinUtc se ajusta al reanudar. No debe decidir el vencimiento,
aplicar multiplicadores al puntaje ni ejecutar explosiones locales como autoridad.

Los modelos de referencia están en contratos/tetris.model.ts:
IConfiguracionPiezas.tiposPermitidos, ITipoPiezaEspecial, IEfectoTemporal,
IActivacionEspecial, EfectoEspecial y los campos nuevos de ISala.
No se modificaron archivos del frontend Ionic.

## Organización del código e informe de archivos

Clases nuevas de producción: OpcionesEfectos, EfectosEspeciales y ControlEfectos.
Se agrega el enum EfectoEspecial y los records TEfectoTemporal y TActivacionEspecial.
Para pruebas se agrega PruebasEfectosEspeciales con un reloj y un motor de prueba.

| Archivo | Cambio |
| --- | --- |
| Dominio/Juego/EfectosEspeciales.cs | Tipos, traducción de códigos y opciones centrales. Nuevo. |
| Dominio/Juego/TableroJuego.cs | Efecto activado y celdas eliminadas por la última pieza. |
| Dominio/EntidadesTipadas/TJuego.cs | Selección por códigos y DTO de efectos en TSala. |
| LogicaNegocio/Motor/MotorJuego.cs | Bomba, multiplicador por líneas y extracción de métodos de limpieza. |
| LogicaNegocio/Servicios/ControlEfectos.cs | Objetivos, duración, inversión, aceleración, renovación y cancelación. Nuevo. |
| LogicaNegocio/Servicios/SalaLJ.cs | Valida selección, traduce controles y coordina bloqueo, efectos y gravedad. |
| LogicaNegocio/Servicios/SalaMemoria.cs | Efectos por jugador y última activación de sala. |
| LogicaNegocio/Servicios/ControlPausas.cs | Desplaza los vencimientos de efectos al reanudar. |
| LogicaNegocio/Servicios/ProyectorSala.cs | Expone efectos, selección y una copia segura de la activación. |
| ProyectoTetris_PrograV/Controllers/SalasController.cs | Consulta autenticada del catálogo activo. |
| ProyectoTetris_PrograV/Program.cs | Registro y validación de opciones de efectos. |
| ProyectoTetris_PrograV/appsettings.json | Duraciones y factor configurables. |
| Tetris.Pruebas/PruebasEfectosEspeciales.cs | Pruebas nuevas de efectos, límites, pausas y generación. |
| Tetris.Pruebas/Program.cs | Registra las pruebas nuevas. |
| Tetris.Pruebas/PruebasHttp.cs | Verifica catálogo, activaciones y vencimientos con WebSockets reales. |
| docs/contratos/tetris.model.ts | Contratos de referencia para Ionic. |
| docs/sql/03_efectos_especiales.sql | Registra tipos faltantes sin alterar el esquema. Nuevo. |
| docs/EFECTOS_ESPECIALES.md | Este informe. Nuevo. |
| docs/ACTUALIZACION_DEFINITIVA.md | Referencia a la actualización de los efectos antes pendientes. |
| README.md | Enlace a este informe. |

Métodos refactorizados: MotorJuego.Fijar delega el conteo y puntuación a
EliminarLineasCompletas; AplicarEfectoEspecial delega en VaciarCelda,
EliminarFilaEspecial y ExplotarBomba. SalaLJ.Mover, AplicarPaso y
AvanzarSalaEnCurso usan ControlEfectos; Intervalo admite el factor temporal.
ControlPausas.DesplazarTemporizadores conserva los plazos. ProyectorSala separa
CopiarUltimoEfecto para impedir modificar el estado interno mediante un DTO.
Las pruebas HTTP de efectos se separaron en VerificarEfectosAsync.
No hubo una refactorización general de la solución.

## Verificación ejecutada

Resultados de la versión final:

- Solución completa: compilación correcta, cero errores y cero advertencias.
- Ejecutable Tetris.Pruebas: 148/148 pruebas correctas. Se conservan las pruebas
  anteriores y se actualiza la expectativa de Barredora a su regla definitiva.
- Integración HTTP/SignalR: 163 comprobaciones correctas con cuatro WebSockets reales.
- Integración SQL: correctos los tres modos, persistencia de participantes/resultados,
  estadísticas idempotentes, cancelación y conservación de finales. ROLLBACK confirmado
  sin registros de prueba restantes.
- git diff --check: sin errores de espacios ni marcadores de conflicto.

Comandos desde la carpeta de la solución:

~~~powershell
dotnet build ProyectoTetris_PrograV.slnx --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal
dotnet run --project Tetris.Pruebas --no-build
dotnet run --project Tetris.Pruebas --no-build -- --integracion-http
dotnet run --project Tetris.Pruebas --no-build -- --integracion-sql
~~~

Las pruebas son un ejecutable propio; no se usa dotnet test. La integración de SQL
utiliza la instancia local definida por las pruebas y una transacción que se revierte.
Los intentos restringidos de HTTP y SQL fallaron por acceso a sockets/instancia;
se repitieron con permisos y terminaron correctamente. La primera compilación
también encontró un problema de comunicación con el servidor auxiliar del compilador;
la compilación final usa un proceso y desactiva la compilación compartida.
No quedaron fallos funcionales en las ejecuciones finales.

La cobertura nueva verifica bomba en cuatro esquinas y preservación fuera del área;
recompensas personalizadas de una a cuatro líneas, nivel y ausencia de multiplicadores
persistentes; controles invertidos, gravedad acelerada y restauración; vencimiento sin
tick previo; congelación de pausa y tick tardío; coexistencia y renovación; receptores
eliminados, cancelación y duplicados; selección por códigos y muestreo con semilla fija
de probabilidades 1, 37 y 100 con reparto uniforme. Las pruebas anteriores conservan
las reglas de los tres modos, filas/columnas, turnos y puntuación.
HTTP comprueba además generación, activación, receptor, restante, expiración,
coordenadas y puntuación transmitidos a otro cliente mediante EstadoActualizado.

## SQL y pendientes

Se revisaron los tres scripts. En JAFET\SQLEXPRESS / TetrisDB ya estaba aplicada la
configuración 01; se aplicaron 02 y 03, insertando seis registros faltantes sin cambiar
los ocho anteriores. FILA reemplaza el código largo del script 02 para respetar
nvarchar(10). Barredora se registra activa si falta; una configuración existente se
conserva. Se verificó la idempotencia con ejecución repetida y rollback antes y después
de aplicar los cambios. No se alteraron tablas ni credenciales.

El catálogo persistente contiene siete normales y siete especiales. No quedan reglas
pendientes para las siete piezas solicitadas. Queda la comprobación manual con los
teléfonos Android y la app Ionic real. El informe de esta ampliación, archivos cambiados,
SQL ejecutado y guía SSMS están en [BARREDORA_SQL.md](BARREDORA_SQL.md).
