# Barredora y catálogo SQL Server

Actualización del 21 de octubre de 2026. Trabajo realizado exclusivamente en la copia
GitHub de Proyecto_Tetris, rama main, conservando los cambios locales de la tarea
anterior. No se modificó el frontend, la copia de Progra V ni las credenciales.
No se realizaron commits ni push.

## Regla y momento de activación

Barredora se activa al fijarse la pieza, en MotorJuego.AplicarEfectoEspecial, igual que
los demás tipos. Reutiliza VaciarCelda para eliminar únicamente las celdas ocupadas de
la mitad superior. En el tablero actual de 20 × 10 limpia filas 0–9 y conserva 10–19.
No desplaza filas ni cambia dimensiones.

El límite se obtiene de Celdas.Length / 2 y el ancho de cada fila de su Length.
En una altura impar la fila central se conserva. El motor completo continúa admitiendo
el tablero original de 20 × 10; se probó la rutina de limpieza aislada con alturas
0, 1, 2, 3, 19, 20 y 21, sin ampliar las reglas ni dimensiones de las partidas.

La jugada de Barredora no ejecuta una eliminación adicional de líneas normales: evita
que el conteo posterior compacte o puntúe filas inferiores que deben conservarse.
La limpieza no modifica Puntuacion ni Lineas. Los puntos normales por bajar o dejar
caer la pieza se conservan. Las restantes piezas siguen usando su conteo de líneas.
Los bloques de la propia pieza se fijan primero; se borran los que queden en la zona
superior y se conservan los de la inferior.

SalaLJ marca el tablero compartido al crearlo. La limpieza de Barredora se limita a
ese tablero, sin crear tableros adicionales. El individual conserva su comportamiento;
el competitivo sigue forzando especiales apagadas. El siguiente jugador recibe el
turno mediante la lógica existente después de fijarse la pieza.

La secuencia de movimientos y el cambio a la siguiente pieza impiden reprocesar una
misma activación. Consultar o reconectar no vuelve a ejecutar la limpieza.
Los efectos temporales, pausas, puntuaciones y otras limpiezas conservan sus reglas.

## Configuración y comunicación

No se cambió GeneradorPiezas ni sus probabilidades. Barredora llega desde el catálogo
activo y participa en la selección uniforme existente. El creador puede incluirla
o excluirla con tiposPermitidos, además del interruptor y porcentaje de 1 a 100.

Ejemplo de configuración de sala:

~~~json
{
  "piezasEspeciales": {
    "activas": true,
    "probabilidad": 25,
    "tiposPermitidos": ["BARREDORA", "BOMBA"]
  }
}
~~~

Para PUT /api/salas/{id}/piezas-especiales se envía solo el objeto interior.
Omitir tiposPermitidos o usar null elige todos los tipos activos. Un arreglo vacío
con especiales activadas se rechaza. El catálogo global usa Activa para habilitar
o deshabilitar tipos y la sala conserva su propia selección.

Se reutilizan TSala, TActivacionEspecial y EstadoActualizado. No se crearon eventos.
Ionic reconoce:

- tablero.piezaEspecial = BARREDORA cuando se genera.
- ultimoEfecto.codigo = BARREDORA, tipo = Barredora y estado = Aplicado al activarse.
- ultimoEfecto.celdasEliminadas: coordenadas ocupadas que se borraron, antes de dibujar
  el nuevo tablero. Todas pertenecen a la mitad superior.
- ultimoEfecto.secuencia: identificador creciente para no repetir la animación.
- tableros[0].celdas: matriz completa autoritativa después del efecto.
- jugadores, puntuacion y usuarioTurno: puntuación real y siguiente turno.

No se cambió la estructura de los DTO. El modelo TypeScript de referencia incorpora
comentarios aclaratorios. PendienteDefinicion permanece en la unión por compatibilidad
con clientes anteriores; la implementación actual de Barredora emite Aplicado.

## Auditoría previa de SQL

Se consultaron los metadatos y registros reales antes de escribir:

- Instancia confirmada: Jafet\SQLEXPRESS; base confirmada: TetrisDB.
- TiposPieza tenía ocho registros: siete normales y X, especial con EliminaColumna = 1.
- Codigo es nvarchar(10), Nombre nvarchar(50) y Descripcion nvarchar(255), compatibles
  con las entidades EF. TipoPiezaId es la clave y Codigo tiene índice único.
- PiezasJugador y RecompensasPuntos mantienen sus claves foráneas hacia TiposPieza.
- La restricción CK_Partidas_Jugadores ya permitía mínimo 1 y máximo >= mínimo;
  estaba habilitada y confiable. El default del mínimo ya era 4.
- Partidas no tenía registros al realizar la auditoría.
- No había scripts de actualización posteriores al 03 en docs/sql.

No hay una tabla de historial de migraciones para estos scripts: la aplicación previa
se determinó por los objetos y datos existentes, sin asumirla por los nombres de archivo.
No se requirió alterar columnas, índices, claves foráneas ni tablas.

## Scripts revisados y ejecución real

| Script | Revisión | Ejecución en esta base |
| --- | --- | --- |
| 01_modalidades.sql | Ahora conserva restricciones ya compatibles y defaults existentes; rechaza restricciones personalizadas desconocidas antes de sustituirlas. | Ya estaba aplicado. Se probó dos veces con rollback antes y después de la actualización; no se reaplicó como cambio persistente. |
| 02_pieza_elimina_fila.sql | El código anterior ELIMINA_FILA excedía nvarchar(10). Ahora inserta FILA y reconoce alias existentes que contengan FILA, incluso deshabilitados, sin duplicarlos. | Aplicado con COMMIT: una inserción. |
| 03_efectos_especiales.sql | Inserta solo efectos faltantes; Barredora ya describe la regla definitiva y se inserta activa. Conserva estados existentes y reconoce el alias de doble puntuación. | Aplicado con COMMIT: cinco inserciones. |

Los scripts 02 y 03 rechazan códigos canónicos que estén ocupados por otra configuración
en vez de sobrescribirla. Usan transacciones y bloqueos de comprobación para no duplicar
la inserción. No actualizan ni borran registros existentes.

Se agregaron estos registros:

| ID observado | Código | Efecto | Activa |
| --- | --- | --- | --- |
| 15 | FILA | Eliminar fila | Sí |
| 16 | CONFUSION | Confusión | Sí |
| 17 | ACELERADOR | Acelerador | Sí |
| 18 | BOMBA | Bomba | Sí |
| 19 | DOBLE | Doble puntuación | Sí |
| 20 | BARREDORA | Limpieza superior | Sí |

X mantiene ID 8 y su configuración de columna. El resultado es 14 tipos: siete normales
y siete especiales. Los saltos de identidad proceden de inserciones de prueba revertidas;
los IDENTITY no se revierten con ROLLBACK. El código identifica efectos por códigos e
indicadores, no por estos números.

La comprobación transaccional ejecutó 01, 02 y 03 dos veces, comparó todas las columnas
del catálogo y verificó que la segunda ejecución no cambiara nada. También comprobó
que las ocho filas previas y las restricciones/defaults de modalidades se conservaran.
Se ejecutó antes y después de aplicar 02 y 03, siempre con ROLLBACK.
DBCC CHECKCONSTRAINTS de TiposPieza terminó sin infracciones.

No quedan cambios SQL pendientes en esta instancia para los siete efectos.
Si otra instalación había guardado BARREDORA desactivada, los scripts conservan esa
preferencia: su administrador debe habilitarla expresamente si lo desea.

## Verificación manual desde SSMS

En otra computadora, primero confirmar servidor y base:

~~~sql
SELECT @@SERVERNAME AS Servidor, DB_NAME() AS BaseActual;
SELECT TipoPiezaId, Codigo, Nombre, EsEspecial, EliminaColumna, Activa
FROM dbo.TiposPieza
ORDER BY TipoPiezaId;
SELECT name, definition, is_disabled, is_not_trusted
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.Partidas');
~~~

Antes de modificar una estructura en otra base, usar SSMS: clic derecho en TetrisDB,
Tareas, Copia de seguridad, tipo Completa, Solo copia, seleccionar un archivo .bak
con espacio y permisos del servicio, y ejecutar. Revisar los datos de Partidas,
sus restricciones y dependencias. Si 01 encuentra una restricción personalizada,
no sustituirla a ciegas: revisar su definición y el respaldo primero.

Orden lógico: 01 únicamente si falta su cambio de modalidades; después 02 y 03 para
tipos faltantes. En esta computadora ya están aplicados los cambios necesarios.
Abrir cada archivo con la base correcta y revisar los mensajes de ejecución.
No es necesario recrear TetrisDB ni cambiar la conexión.

Para consultar el catálogo desde la aplicación, iniciar sesión y llamar
GET /api/salas/tipos-especiales. Devuelve Respuesta<TipoPiezaEspecial[]> con los
especiales activos. Deben reconocerse X, FILA, CONFUSION, ACELERADOR, BOMBA, DOBLE y
BARREDORA en esta base. Deshabilitar un tipo en SQL conserva su registro y evita
su entrega por el catálogo al crear/configurar nuevas salas.

La prueba SQL con directivas :r se ejecuta con sqlcmd, desde la carpeta de la solución:

~~~powershell
sqlcmd -S "JAFET\SQLEXPRESS" -d TetrisDB -E -C -b -f 65001 -i Tetris.Pruebas/Sql/VerificarCatalogo.sql
~~~

Está diseñada para comprobar esta base con 01 ya aplicado. Siempre revierte las
inserciones y no debe confundirse con aplicar los scripts para una instalación nueva.

## Código modificado en esta ampliación

| Archivo | Cambio |
| --- | --- |
| Dominio/Juego/TableroJuego.cs | Indicador EsCompartido del tablero del motor. |
| LogicaNegocio/Motor/MotorJuego.cs | BarrerMitadSuperior, reutilización de VaciarCelda y protección del conteo de líneas de Barredora. |
| LogicaNegocio/Servicios/SalaLJ.cs | Marca el tablero compartido al crearlo. |
| LogicaNegocio/Servicios/ControlEfectos.cs | Barredora pasa de pendiente a aplicada mediante la activación existente. |
| LogicaNegocio/Servicios/CatalogoPiezasEspeciales.cs | Comentario del código FILA corregido. |
| LogicaNegocio/LogicaJuego.csproj | Acceso interno al motor únicamente para sus pruebas. |
| Tetris.Pruebas/PruebasEfectosEspeciales.cs | Actualiza la prueba provisional de Barredora y agrega cinco pruebas. |
| Tetris.Pruebas/PruebasHttp.cs | Barredora con cuatro WebSockets y un motor de prueba que delega al motor real. |
| Tetris.Pruebas/PruebasSql.cs | Verifica catálogo SQL real mediante EF, repositorio, servicio y selección de sala. |
| Tetris.Pruebas/Sql/VerificarCatalogo.sql | Prueba transaccional e idempotencia de scripts. Nuevo. |
| docs/sql/01_modalidades.sql | Conservación de configuración válida e idempotencia. |
| docs/sql/02_pieza_elimina_fila.sql | Código compatible, validaciones y prevención de duplicados. |
| docs/sql/03_efectos_especiales.sql | Regla definitiva y validaciones de catálogo. |
| docs/contratos/tetris.model.ts | Comentarios de reconocimiento de Barredora. |
| docs/EFECTOS_ESPECIALES.md | Regla, resultados y SQL actualizados. |
| docs/ACTUALIZACION_DEFINITIVA.md | Retira el aviso obsoleto de Barredora pendiente. |
| docs/CONFIGURACION_PARTIDA.md | Código FILA y ejecución SQL real. |
| docs/BARREDORA_SQL.md | Este informe. Nuevo. |
| README.md | Enlace al informe. |

No se agregaron clases de producción. Se extrajo BarrerMitadSuperior y se adaptaron
Fijar, AplicarEfectoEspecial y VaciarCelda sin introducir un segundo motor de efectos.
La nueva clase MotorHttp pertenece solo a las pruebas.

## Pruebas ejecutadas y pendientes

- Solución completa: compilación correcta, cero errores y advertencias.
- Pruebas de lógica: 148/148 correctas. No se eliminaron pruebas; la expectativa
  provisional de Barredora se actualizó a la regla definitiva y se agregaron cinco casos.
- HTTP/SignalR: 163 comprobaciones correctas con cuatro WebSockets reales.
- SQL: correctos catálogo real, entidades EF, siete efectos, selección por código,
  tres modos, persistencia, estadísticas idempotentes y cancelación; rollback confirmado.
- Scripts SQL: ejecución repetida correcta antes y después de la aplicación,
  sin duplicados ni sobrescrituras.
- Comprobación posterior: 14 tipos, siete especiales y restricciones válidas.

La cobertura comprueba alturas pares e impares en la rutina, frontera entre filas 9 y
10, conservación de la mitad inferior incluso con una fila completa, ausencia de
puntos y desplazamientos, dimensiones, repetición de solicitudes, configuración y
modos permitidos. SignalR comprueba el tipo de efecto, estado, coordenadas, matriz y
turno que recibe otro cliente. La batería anterior verifica las otras seis piezas.

Comandos de compilación y pruebas:

~~~powershell
dotnet build ProyectoTetris_PrograV.slnx --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal
dotnet run --project Tetris.Pruebas --no-build
dotnet run --project Tetris.Pruebas --no-build -- --integracion-http
dotnet run --project Tetris.Pruebas --no-build -- --integracion-sql
~~~

Todas las ejecuciones funcionales terminaron correctamente. No queda incompleto un
efecto del sistema de siete piezas solicitado. Queda la prueba manual de conexión
Wi-Fi y visualización con los teléfonos Android y la aplicación Ionic real.
