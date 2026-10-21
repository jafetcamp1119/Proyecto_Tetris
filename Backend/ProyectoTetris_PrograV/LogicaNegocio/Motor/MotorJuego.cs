using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.Juego;

namespace LogicaJuego.Motor;

public class MotorJuego : IMotorJuego
{
    private readonly GeneradorPiezas generador;

    // I, O, T, S, Z, J y L dentro de una caja de cuatro filas y cuatro columnas.
    private static readonly (int F, int C)[][] Formas =
    {
        new[] { (1, 0), (1, 1), (1, 2), (1, 3) },
        new[] { (0, 1), (0, 2), (1, 1), (1, 2) },
        new[] { (0, 1), (1, 0), (1, 1), (1, 2) },
        new[] { (0, 1), (0, 2), (1, 0), (1, 1) },
        new[] { (0, 0), (0, 1), (1, 1), (1, 2) },
        new[] { (0, 0), (1, 0), (1, 1), (1, 2) },
        new[] { (0, 2), (1, 0), (1, 1), (1, 2) }
    };

    public MotorJuego() : this(new GeneradorPiezas())
    {
    }

    public MotorJuego(GeneradorPiezas generador)
    {
        this.generador = generador;
    }

    public TableroJuego CrearTablero()
    {
        return CrearTablero(ConfiguracionPiezas.SoloNormales);
    }

    public TableroJuego CrearTablero(ConfiguracionPiezas piezas)
    {
        var tablero = new TableroJuego();
        tablero.Piezas = piezas;
        NuevaPieza(tablero);
        return tablero;
    }

    private static (int F, int C)[] Bloques(int tipo, int rotacion)
    {
        var bloques = Formas[tipo].ToArray();

        if (tipo == 1)
        {
            return bloques;
        }

        for (var vuelta = 0; vuelta < rotacion; vuelta++)
        {
            bloques = bloques.Select(bloque => (bloque.C, 3 - bloque.F)).ToArray();
        }

        return bloques;
    }

    private static bool Cabe(TableroJuego tablero, int fila, int columna, int rotacion)
    {
        foreach (var bloque in Bloques(tablero.TipoPieza, rotacion))
        {
            var filaFinal = fila + bloque.F;
            var columnaFinal = columna + bloque.C;

            if (columnaFinal < 0 || columnaFinal >= 10 || filaFinal >= 20)
            {
                return false;
            }

            if (filaFinal >= 0 && tablero.Celdas[filaFinal][columnaFinal] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private void NuevaPieza(TableroJuego tablero)
    {
        tablero.TipoPieza = generador.SortearForma(tablero.Piezas.FormasPermitidas);
        tablero.PiezaEspecial = generador.SortearEspecial(tablero.Piezas);
        tablero.Rotacion = 0;
        tablero.Fila = 0;
        tablero.Columna = 3;

        if (!Cabe(tablero, tablero.Fila, tablero.Columna, 0))
        {
            tablero.Perdido = true;
        }
    }

    public PasoJuego Mover(TableroJuego tablero, Movimiento movimiento)
    {
        if (tablero.Perdido || !Enum.IsDefined(movimiento))
        {
            return new PasoJuego(false, false);
        }

        switch (movimiento)
        {
            case Movimiento.Izquierda:
                return Trasladar(tablero, -1);

            case Movimiento.Derecha:
                return Trasladar(tablero, 1);

            case Movimiento.Rotar:
                var rotacion = (tablero.Rotacion + 1) % 4;

                if (!Cabe(tablero, tablero.Fila, tablero.Columna, rotacion))
                {
                    return new PasoJuego(false, false);
                }

                tablero.Rotacion = rotacion;
                RegistrarEvento(tablero, EventoSonido.Rotacion, 0, null);
                return new PasoJuego(true, false);

            case Movimiento.Bajar:
                var filaAnterior = tablero.Fila;
                var paso = Avanzar(tablero);

                if (!paso.PiezaFijada && tablero.Fila > filaAnterior)
                {
                    tablero.Puntuacion++;
                    RegistrarEvento(tablero, EventoSonido.Movimiento, 0, null);
                }

                return paso;

            case Movimiento.Caer:
                while (Cabe(tablero, tablero.Fila + 1, tablero.Columna, tablero.Rotacion))
                {
                    tablero.Fila++;
                    tablero.Puntuacion += 2;
                }

                return Fijar(tablero);

            default:
                return new PasoJuego(false, false);
        }
    }

    private static PasoJuego Trasladar(TableroJuego tablero, int desplazamiento)
    {
        if (!Cabe(tablero, tablero.Fila, tablero.Columna + desplazamiento, tablero.Rotacion))
        {
            return new PasoJuego(false, false);
        }

        tablero.Columna += desplazamiento;
        RegistrarEvento(tablero, EventoSonido.Movimiento, 0, null);
        return new PasoJuego(true, false);
    }

    public PasoJuego Avanzar(TableroJuego tablero)
    {
        if (tablero.Perdido)
        {
            return new PasoJuego(false, false);
        }

        if (Cabe(tablero, tablero.Fila + 1, tablero.Columna, tablero.Rotacion))
        {
            tablero.Fila++;
            return new PasoJuego(true, false);
        }

        return Fijar(tablero);
    }

    private PasoJuego Fijar(TableroJuego tablero)
    {
        foreach (var bloque in Bloques(tablero.TipoPieza, tablero.Rotacion))
        {
            if (tablero.Fila + bloque.F < 0)
            {
                tablero.Perdido = true;
                RegistrarEvento(tablero, EventoSonido.Bloqueo | EventoSonido.Derrota, 0, null);
                return new PasoJuego(true, true);
            }

            tablero.Celdas[tablero.Fila + bloque.F][tablero.Columna + bloque.C] = tablero.TipoPieza + 1;
        }

        var evento = EventoSonido.Bloqueo;
        var especialActivada = AplicarEfectoEspecial(tablero);

        if (especialActivada != null)
        {
            evento |= EventoSonido.EspecialActivada;
        }

        var nivelAnterior = tablero.Nivel;
        var lineasCompletas = tablero.Celdas.Count(fila => fila.All(celda => celda != 0));

        if (lineasCompletas > 0)
        {
            var filasRestantes = tablero.Celdas
                .Where(fila => fila.Any(celda => celda == 0))
                .Select(fila => fila.ToArray())
                .ToArray();

            for (var fila = 0; fila < 20; fila++)
            {
                if (fila < lineasCompletas)
                {
                    tablero.Celdas[fila] = new int[10];
                }
                else
                {
                    tablero.Celdas[fila] = filasRestantes[fila - lineasCompletas];
                }
            }

            tablero.Lineas += lineasCompletas;
            var indicePuntos = Math.Min(lineasCompletas, 4) - 1;
            tablero.Puntuacion += tablero.Piezas.PuntosPorLineas[indicePuntos] * nivelAnterior;
            evento |= EventoSonido.LineasEliminadas;
        }

        NuevaPieza(tablero);

        if (tablero.Perdido)
        {
            evento |= EventoSonido.Derrota;
        }

        RegistrarEvento(tablero, evento, lineasCompletas, especialActivada);
        return new PasoJuego(true, true);
    }

    // Efectos de las piezas especiales. Devuelve el código del tipo si se activó alguno.
    // EliminaColumna: la pieza vacía por completo las columnas que ocupa (incluida la propia pieza).
    // EliminaFila: se elimina la fila de la celda más baja de la pieza; las filas de arriba bajan
    // una posición y el tablero conserva sus 20 filas.
    // Ninguno suma puntos extra: las líneas que queden completas se puntúan como siempre, y una
    // fila eliminada por el efecto no se cuenta como línea, así que nunca se puntúa dos veces.
    private static string? AplicarEfectoEspecial(TableroJuego tablero)
    {
        var especial = tablero.PiezaEspecial;

        if (especial == null || (!especial.EliminaColumna && !especial.EliminaFila))
        {
            return null;
        }

        var bloques = Bloques(tablero.TipoPieza, tablero.Rotacion);

        if (especial.EliminaColumna)
        {
            var columnas = bloques
                .Select(bloque => tablero.Columna + bloque.C)
                .Distinct();

            foreach (var columna in columnas)
            {
                foreach (var fila in tablero.Celdas)
                {
                    fila[columna] = 0;
                }
            }
        }

        if (especial.EliminaFila)
        {
            var filaMasBaja = tablero.Fila + bloques.Max(bloque => bloque.F);

            for (var fila = filaMasBaja; fila > 0; fila--)
            {
                tablero.Celdas[fila] = tablero.Celdas[fila - 1];
            }

            tablero.Celdas[0] = new int[10];
        }

        return especial.Codigo;
    }

    private static void RegistrarEvento(TableroJuego tablero, EventoSonido evento, int lineas, string? especial)
    {
        tablero.EventoSecuencia++;
        tablero.UltimoEvento = evento;
        tablero.LineasUltimoEvento = lineas;
        tablero.EspecialUltimoEvento = especial;
    }

    public TTablero ObtenerEstado(TableroJuego tablero, int? usuarioId)
    {
        var celdas = tablero.Celdas.Select(fila => fila.ToArray()).ToArray();
        var pieza = new List<TCelda>();

        if (!tablero.Perdido)
        {
            foreach (var bloque in Bloques(tablero.TipoPieza, tablero.Rotacion))
            {
                pieza.Add(new TCelda(tablero.Fila + bloque.F, tablero.Columna + bloque.C));
            }
        }

        return new TTablero(
            usuarioId,
            celdas,
            tablero.TipoPieza,
            pieza.ToArray(),
            tablero.Puntuacion,
            tablero.Lineas,
            tablero.Nivel,
            tablero.Perdido,
            tablero.PiezaEspecial?.Codigo,
            ProyectarEvento(tablero));
    }

    private static TEventoTablero? ProyectarEvento(TableroJuego tablero)
    {
        if (tablero.EventoSecuencia == 0)
        {
            return null;
        }

        var sonidos = new List<string>();
        var evento = tablero.UltimoEvento;

        if (evento.HasFlag(EventoSonido.Movimiento))
        {
            sonidos.Add("movimiento");
        }

        if (evento.HasFlag(EventoSonido.Rotacion))
        {
            sonidos.Add("rotacion");
        }

        if (evento.HasFlag(EventoSonido.Bloqueo))
        {
            sonidos.Add("bloqueo");
        }

        if (evento.HasFlag(EventoSonido.LineasEliminadas))
        {
            sonidos.Add("lineas");
        }

        if (evento.HasFlag(EventoSonido.EspecialActivada))
        {
            sonidos.Add("especial");
        }

        if (evento.HasFlag(EventoSonido.Derrota))
        {
            sonidos.Add("derrota");
        }

        return new TEventoTablero(
            tablero.EventoSecuencia,
            sonidos.ToArray(),
            tablero.LineasUltimoEvento,
            tablero.EspecialUltimoEvento);
    }
}

