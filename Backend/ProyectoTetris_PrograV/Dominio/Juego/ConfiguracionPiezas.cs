namespace Dominio.Juego;

// Tipo de pieza especial leído de la tabla TiposPieza (EsEspecial y Activa).
// El motor recibe una copia, así que no consulta SQL Server mientras se juega.
// EliminaFila no existe como columna en la base de datos: se deduce del código del tipo
// (ver CatalogoPiezasEspeciales) y es un efecto independiente de EliminaColumna.
public sealed record TipoPiezaEspecial(
    int TipoPiezaId,
    string Codigo,
    string Nombre,
    bool EliminaColumna,
    bool EliminaFila = false);

// Velocidad inicial de caída. El nivel sigue acelerando la caída a partir de este valor.
public enum VelocidadInicial
{
    Lenta = 0,
    Normal = 1,
    Rapida = 2
}

// Reglas de una partida: piezas especiales, piezas normales permitidas, velocidad inicial y
// puntos por líneas. No cambia después de iniciar y es igual para todos los jugadores.
public sealed class ConfiguracionPiezas
{
    // Letras de las siete piezas normales, en el mismo orden que las formas del motor.
    public static readonly IReadOnlyList<string> CodigosFormas = ["I", "O", "T", "S", "Z", "J", "L"];

    public static readonly IReadOnlyList<int> TodasLasFormas = [0, 1, 2, 3, 4, 5, 6];

    // Puntos por 1, 2, 3 y 4 líneas simultáneas (antes de multiplicar por el nivel).
    public static readonly IReadOnlyList<int> PuntosPredeterminados = [100, 300, 500, 800];

    public const int PuntosMaximosPorJugada = 10000;

    public static readonly ConfiguracionPiezas SoloNormales = new ConfiguracionPiezas(false, 0, []);

    public ConfiguracionPiezas(
        bool especialesActivas,
        int probabilidadEspecial,
        IReadOnlyList<TipoPiezaEspecial> tiposDisponibles)
        : this(
            especialesActivas,
            probabilidadEspecial,
            tiposDisponibles,
            TodasLasFormas,
            VelocidadInicial.Normal,
            PuntosPredeterminados)
    {
    }

    public ConfiguracionPiezas(
        bool especialesActivas,
        int probabilidadEspecial,
        IReadOnlyList<TipoPiezaEspecial> tiposDisponibles,
        IReadOnlyList<int> formasPermitidas,
        VelocidadInicial velocidad,
        IReadOnlyList<int> puntosPorLineas)
    {
        this.EspecialesActivas = especialesActivas;
        this.ProbabilidadEspecial = probabilidadEspecial;
        this.TiposDisponibles = tiposDisponibles;
        this.FormasPermitidas = formasPermitidas;
        this.Velocidad = velocidad;
        this.PuntosPorLineas = puntosPorLineas;
    }

    public bool EspecialesActivas { get; }

    // Porcentaje de 1 a 100 de que cada pieza nueva sea especial.
    public int ProbabilidadEspecial { get; }

    public IReadOnlyList<TipoPiezaEspecial> TiposDisponibles { get; }

    // Índices (0 a 6) de las piezas normales que pueden salir. Nunca está vacía.
    public IReadOnlyList<int> FormasPermitidas { get; }

    public VelocidadInicial Velocidad { get; }

    // Cuatro valores: puntos por 1, 2, 3 y 4 líneas.
    public IReadOnlyList<int> PuntosPorLineas { get; }

    // Milisegundos entre caídas en el nivel 1.
    public int IntervaloBaseMs
    {
        get
        {
            switch (Velocidad)
            {
                case VelocidadInicial.Lenta:
                    return 1000;

                case VelocidadInicial.Rapida:
                    return 500;

                default:
                    return 800;
            }
        }
    }
}
