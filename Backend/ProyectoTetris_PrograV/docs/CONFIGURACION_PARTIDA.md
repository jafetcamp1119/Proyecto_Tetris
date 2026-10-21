# Configuración de partida: pieza que elimina fila, piezas normales, velocidad y puntos

Segunda ronda del servidor C# (21 de octubre de 2026). No se tocó `tetris-app`.
Todo se valida y se aplica en el servidor; el cliente solo envía las opciones.

Las reglas definitivas y la meta personal se documentan en [ACTUALIZACION_DEFINITIVA.md](ACTUALIZACION_DEFINITIVA.md).

## 1. Qué se puede configurar al crear la sala

`POST /api/salas` (`ICrearSala`) acepta, además de lo anterior:

```json
{
  "modo": "Competitivo",
  "maximoJugadores": 4,
  "privada": false,
  "piezasEspeciales": { "activas": true, "probabilidad": 25 },
  "piezasNormales": ["I", "O", "T"],
  "velocidad": "Rapida",
  "puntos": { "unaLinea": 100, "dosLineas": 300, "tresLineas": 500, "cuatroLineas": 800 }
}
```

Todo lo nuevo es opcional. Si no se envía: las siete piezas normales, velocidad `Normal` y puntos
100 / 300 / 500 / 800 (los valores de siempre).

Mientras la sala está en espera, **solo el creador** puede cambiarlo:

| Método | Ruta | Cuerpo | Qué cambia |
|---|---|---|---|
| PUT | /api/salas/{id}/configuracion | `IConfiguracionSala` | Todo: especiales, normales, velocidad y puntos (lo que no se envía vuelve a su valor por omisión) |
| PUT | /api/salas/{id}/piezas-especiales | `IConfiguracionPiezas` | Solo las especiales; conserva normales, velocidad y puntos (sin cambios) |

Después de iniciar la configuración **no cambia** (el servidor responde con error). Es de la partida
completa, no de un tablero: todos los jugadores de la sala juegan con las mismas reglas.
`ISala` devuelve la configuración vigente en `piezasEspeciales`, `piezasNormales`, `velocidad` y `puntos`.

## 2. Pieza especial que elimina una fila

- Al fijarse la pieza, se elimina la fila de su **celda ocupada más baja**. Las filas de arriba bajan una
  posición, entra una fila vacía arriba y el tablero conserva sus 20 filas por 10 columnas.
- Es un efecto **separado** de `EliminaColumna` (que sigue funcionando igual). Un tipo puede tener uno,
  otro o ambos.
- Se permite en individual y compartido. El competitivo fuerza piezas especiales desactivadas,
  aunque el cliente solicite activarlas.
- **Puntos:** el efecto no da puntos ni bonificaciones. Una fila quitada por el efecto no cuenta como
  línea (nunca se elimina ni se puntúa dos veces). Las demás filas que queden completas se puntúan como siempre.
- **Cómo se identifica el tipo:** la tabla `TiposPieza` solo tiene la columna `EliminaColumna`, así que
  el servidor considera que un tipo elimina fila cuando su `Codigo` contiene `FILA`
  (por ejemplo `FILA`). Para crear ese tipo ejecuta una vez `docs/sql/02_pieza_elimina_fila.sql`
  (agrega una fila a `TiposPieza`; no borra ni cambia nada y se puede repetir sin duplicar).
  El script fue corregido para `Codigo nvarchar(10)`, probado con rollback y aplicado en
  `JAFET\SQLEXPRESS / TetrisDB`. Consulta `docs/BARREDORA_SQL.md` antes de trasladarlo a otra base.
- Se activa con el mismo interruptor y porcentaje de las piezas especiales. Todos los tipos activos
  salen con la misma probabilidad.

> Si prefieres otra regla para elegir la fila, solo cambia `MotorJuego.AplicarEfectoEspecial`.

## 3. Piezas normales permitidas

- Letras `I`, `O`, `T`, `S`, `Z`, `J`, `L` (mayúsculas o minúsculas; las repetidas se ignoran).
- Sin el campo: las siete. Con el campo: **al menos una**; una lista vacía o una letra desconocida se rechaza.
- `GeneradorPiezas.SortearForma` elige solo entre las permitidas. Es independiente de las piezas especiales.

## 4. Velocidad inicial

| Valor | Intervalo de caída en el nivel 1 |
|---|---|
| `Lenta` | 1000 ms |
| `Normal` | 800 ms (como antes) |
| `Rapida` | 500 ms |

La progresión por nivel se conserva: cada nivel resta 60 ms y nunca baja de 100 ms. El reloj del servidor
(el mismo de siempre) controla la caída; el cliente no la decide. Es igual para todos los jugadores.

## 5. Puntos por líneas

Puntos base por 1, 2, 3 y 4 líneas, enteros de 0 a 10000. Se multiplican por el nivel como antes.
Sin cambiar nada siguen siendo 100 / 300 / 500 / 800. Los puntos por bajar (1 por celda) y por caída
rápida (2 por celda) no cambian. Se rechazan negativos y valores mayores a 10000.

## 6. Eventos para sonido (el backend no reproduce audio)

No se agregó ninguna biblioteca de audio. Cada tablero (`ITablero.evento`) indica qué ocurrió en su
última acción:

```json
{ "secuencia": 42, "sonidos": ["bloqueo", "lineas", "especial"], "lineasEliminadas": 2, "piezaEspecial": "ELIMINA_FILA" }
```

| Sonido | Cuándo |
|---|---|
| `movimiento` | Izquierda, derecha o bajar que sí cambió la posición |
| `rotacion` | Rotación aceptada |
| `bloqueo` | La pieza se fijó |
| `lineas` | Se eliminaron líneas (`lineasEliminadas`) |
| `especial` | Se activó una pieza especial (`piezaEspecial`) |
| `derrota` | El tablero perdió |

`secuencia` sube con cada acción: el cliente reproduce el sonido solo si es mayor que la última que vio.
`evento` es `null` si aún no hubo acciones. El fin de la partida se ve en `ISala.estado === 'Finalizada'`.
Una caída normal por gravedad sin bloqueo no genera sonido. `SonidoActivo` (`/api/configuracion`) no
cambió: el cliente decide si reproduce o no.

## 7. Qué se probó

Ver docs/VERIFICACION.md. Pruebas nuevas en `Tetris.Pruebas/PruebasConfiguracionPartida.cs`.
