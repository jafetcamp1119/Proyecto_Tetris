using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.Juego;

namespace LogicaJuego.Motor;

// Aplica las reglas del tablero: movimientos, colisiones, fijación, filas y puntos.
// Trabaja en el servidor y no depende de la pantalla ni de SQL Server.
public class MotorJuego : IMotorJuego
{
    private readonly GeneradorPiezas generador;

    public MotorJuego() : this(new GeneradorPiezas())
    {
    }

    // Permite recibir un generador controlado para probar el motor sin depender del azar.
    public MotorJuego(GeneradorPiezas generador)
    {
        this.generador = generador;
    }

    // Prepara un tablero nuevo; esta sobrecarga utiliza únicamente piezas normales.
    public TableroJuego CrearTablero()
    {
        return CrearTablero(ConfiguracionPiezas.SoloNormales);
    }

    // Crea un tablero con las reglas elegidas y prepara su primera pieza.
    public TableroJuego CrearTablero(ConfiguracionPiezas piezas)
    {
        var tablero = new TableroJuego();
        tablero.Piezas = piezas;
        NuevaPieza(tablero);
        return tablero;
    }

    // Revisa cada bloque de la pieza propuesta. Rechaza bordes laterales, fondo y celdas ocupadas.
    // Las filas negativas se permiten mientras la pieza entra por la parte superior.
    private static bool Cabe(TableroJuego tablero, int fila, int columna, int rotacion)
    {
        foreach (var bloque in FormasPieza.Bloques(tablero.TipoPieza, rotacion))
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

    // Activa la pieza ya preparada y comprueba si tiene espacio para aparecer.
    // Si la zona está ocupada, marca el tablero como perdido.
    private void NuevaPieza(TableroJuego tablero)
    {
        tablero.PiezaSecuencia++;
        var preparada = tablero.SiguientePieza ?? generador.PrepararPieza(tablero.Piezas);
        tablero.TipoPieza = preparada.TipoPieza;
        tablero.PiezaEspecial = preparada.Especial;
        // Compartido prepara la siguiente pieza por jugador, bajo el bloqueo de la sala.
        tablero.SiguientePieza = tablero.EsCompartido ? null : generador.PrepararPieza(tablero.Piezas);
        tablero.Rotacion = 0;
        tablero.Fila = 0;
        tablero.Columna = 3;

        if (!Cabe(tablero, tablero.Fila, tablero.Columna, 0))
        {
            tablero.Perdido = true;
        }
    }

    // Aplica un movimiento válido y devuelve si cambió el tablero o se fijó la pieza.
    // La bajada manual suma un punto por celda y la caída rápida suma dos.
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

                var caida = Fijar(tablero);
                tablero.UltimoEvento |= EventoSonido.CaidaRapida;
                return caida;

            default:
                return new PasoJuego(false, false);
        }
    }

    // Intenta mover la pieza horizontalmente sin atravesar bloques ni salir del tablero.
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

    // Hace descender la pieza una fila; si ya no cabe debajo, la fija en el tablero.
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

    // Convierte la pieza activa en bloques fijos, aplica su especial y limpia filas normales.
    // Luego prepara la nueva pieza y registra lo ocurrido para la vista y el audio.
    private PasoJuego Fijar(TableroJuego tablero)
    {
        foreach (var bloque in FormasPieza.Bloques(tablero.TipoPieza, tablero.Rotacion))
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
        var efecto = tablero.PiezaEspecial == null
            ? EfectoEspecial.Ninguno
            : EfectosEspeciales.Identificar(tablero.PiezaEspecial);
        var especialActivada = AplicarEfectoEspecial(tablero);

        if (especialActivada != null)
        {
            evento |= EventoSonido.EspecialActivada;
        }

        var lineasCompletas = 0;

        // La jugada de Barredora solo barre: no compacta filas ni puntúa líneas de la zona inferior.
        if (efecto != EfectoEspecial.Barredora || !tablero.EsCompartido)
        {
            lineasCompletas = EliminarLineasCompletas(tablero, efecto);
        }

        if (lineasCompletas > 0)
        {
            evento |= EventoSonido.LineasEliminadas;
        }

        NuevaPieza(tablero);

        if (tablero.Perdido)
        {
            evento |= EventoSonido.Derrota;
        }

        RegistrarEvento(tablero, evento, lineasCompletas, especialActivada);
        tablero.EfectoUltimoEvento = efecto;
        return new PasoJuego(true, true);
    }

    // Retira las filas llenas, baja las restantes y agrega filas vacías arriba.
    // Suma filas y puntos con el nivel anterior; DoblePuntuacion duplica solo esos puntos.
    private static int EliminarLineasCompletas(TableroJuego tablero, EfectoEspecial efecto)
    {
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
            var multiplicador = efecto == EfectoEspecial.DoblePuntuacion ? 2 : 1;
            tablero.Puntuacion += tablero.Piezas.PuntosPorLineas[indicePuntos] * nivelAnterior * multiplicador;
        }

        return lineasCompletas;
    }

    // Efectos de las piezas especiales. Devuelve el código del tipo si se activó alguno.
    // EliminaColumna: la pieza vacía por completo las columnas que ocupa (incluida la propia pieza).
    // EliminaFila: se elimina la fila de la celda más baja de la pieza; las filas de arriba bajan
    // una posición y el tablero conserva sus 20 filas.
    // La limpieza no puntúa: solo las líneas completas restantes pasan a EliminarLineasCompletas.
    // Ese método aplica doble puntuación cuando corresponde, una sola vez por jugada.
    private static string? AplicarEfectoEspecial(TableroJuego tablero)
    {
        var especial = tablero.PiezaEspecial;

        tablero.CeldasEliminadasEspecial = [];

        if (especial == null)
        {
            return null;
        }

        var efecto = EfectosEspeciales.Identificar(especial);

        if (efecto == EfectoEspecial.Barredora && !tablero.EsCompartido)
        {
            return null;
        }

        var bloques = FormasPieza.Bloques(tablero.TipoPieza, tablero.Rotacion);
        var eliminadas = new List<(int Fila, int Columna)>();

        if (especial.EliminaColumna)
        {
            var columnas = bloques
                .Select(bloque => tablero.Columna + bloque.C)
                .Distinct();

            foreach (var columna in columnas)
            {
                for (var fila = 0; fila < 20; fila++)
                {
                    VaciarCelda(tablero, fila, columna, eliminadas);
                }
            }
        }

        if (especial.EliminaFila)
        {
            EliminarFilaEspecial(tablero, tablero.Fila + bloques.Max(bloque => bloque.F), eliminadas);
        }

        if (efecto == EfectoEspecial.Bomba)
        {
            ExplotarBomba(tablero, bloques, eliminadas);
        }

        if (efecto == EfectoEspecial.Barredora)
        {
            BarrerMitadSuperior(tablero.Celdas, eliminadas);
        }

        tablero.CeldasEliminadasEspecial = eliminadas.ToArray();

        if (efecto == EfectoEspecial.Ninguno)
        {
            return null;
        }

        return especial.Codigo;
    }

    // Vacía la mitad superior sin bajar las otras filas ni aumentar puntos o líneas.
    internal static void BarrerMitadSuperior(int[][] celdas, List<(int Fila, int Columna)> eliminadas)
    {
        // En alturas impares la fila central queda en la mitad inferior.
        var limiteSuperior = celdas.Length / 2;

        for (var fila = 0; fila < limiteSuperior; fila++)
        {
            for (var columna = 0; columna < celdas[fila].Length; columna++)
            {
                VaciarCelda(celdas, fila, columna, eliminadas);
            }
        }
    }

    // Registra una celda ocupada antes de vaciarla para poder mostrar la eliminación.
    private static void VaciarCelda(TableroJuego tablero, int fila, int columna,
        List<(int Fila, int Columna)> eliminadas)
    {
        VaciarCelda(tablero.Celdas, fila, columna, eliminadas);
    }

    private static void VaciarCelda(int[][] celdas, int fila, int columna,
        List<(int Fila, int Columna)> eliminadas)
    {
        if (celdas[fila][columna] != 0)
        {
            eliminadas.Add((fila, columna));
            celdas[fila][columna] = 0;
        }
    }

    // Elimina la fila más baja ocupada por la pieza y baja las que estaban encima.
    // Esta limpieza especial no suma filas normales ni puntos.
    private static void EliminarFilaEspecial(TableroJuego tablero, int filaMasBaja,
        List<(int Fila, int Columna)> eliminadas)
    {
        for (var columna = 0; columna < 10; columna++)
        {
            VaciarCelda(tablero, filaMasBaja, columna, eliminadas);
        }

        for (var fila = filaMasBaja; fila > 0; fila--)
        {
            tablero.Celdas[fila] = tablero.Celdas[fila - 1];
        }

        tablero.Celdas[0] = new int[10];
    }

    // Vacía el área de 3 por 3 alrededor del bloque inferior izquierdo de la pieza.
    // Recorta el área en los bordes y no suma puntos por los bloques retirados.
    private static void ExplotarBomba(TableroJuego tablero, (int F, int C)[] bloques,
        List<(int Fila, int Columna)> eliminadas)
    {
        // Centro: celda más baja y, en empate, la más a la izquierda.
        var centro = bloques.OrderByDescending(bloque => bloque.F).ThenBy(bloque => bloque.C).First();
        var filaCentro = tablero.Fila + centro.F;
        var columnaCentro = tablero.Columna + centro.C;

        for (var fila = Math.Max(0, filaCentro - 1); fila <= Math.Min(19, filaCentro + 1); fila++)
        {
            for (var columna = Math.Max(0, columnaCentro - 1); columna <= Math.Min(9, columnaCentro + 1); columna++)
            {
                VaciarCelda(tablero, fila, columna, eliminadas);
            }
        }
    }

    // Guarda el último evento con una secuencia nueva para evitar repetir sonidos.
    private static void RegistrarEvento(TableroJuego tablero, EventoSonido evento, int lineas, string? especial)
    {
        tablero.EventoSecuencia++;
        tablero.UltimoEvento = evento;
        tablero.LineasUltimoEvento = lineas;
        tablero.EspecialUltimoEvento = especial;
    }

    // Copia el tablero y la pieza visible en un DTO. El cliente no recibe el estado mutable.
    public TTablero ObtenerEstado(TableroJuego tablero, int? usuarioId)
    {
        var celdas = tablero.Celdas.Select(fila => fila.ToArray()).ToArray();
        var pieza = new List<TCelda>();

        if (!tablero.Perdido)
        {
            foreach (var bloque in FormasPieza.Bloques(tablero.TipoPieza, tablero.Rotacion))
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
            ProyectarEvento(tablero), tablero.Fila, tablero.Columna, tablero.Rotacion, tablero.PiezaSecuencia);
    }

    // Convierte las marcas del motor en los nombres de eventos que reconoce el frontend.
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

        if (evento.HasFlag(EventoSonido.CaidaRapida))
        {
            sonidos.Add("caida");
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

