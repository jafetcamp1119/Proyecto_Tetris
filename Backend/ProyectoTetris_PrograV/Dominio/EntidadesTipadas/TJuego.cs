using System.ComponentModel.DataAnnotations;
using Dominio.Juego;

namespace Dominio.EntidadesTipadas;

public class TCrearSala
{
    [EnumDataType(typeof(ModoJuego))]
    public ModoJuego Modo { get; set; }

    [Range(1, 16)]
    public int MaximoJugadores { get; set; } = 4;

    // true: la sala no aparece en el listado y se entra con el código de seis caracteres.
    public bool Privada { get; set; }

    // Apagado por omisión: solo aparecen piezas normales.
    public TConfiguracionPiezas PiezasEspeciales { get; set; } = new TConfiguracionPiezas();

    // Letras de las piezas normales permitidas (I, O, T, S, Z, J, L). Sin enviar: todas.
    public string[]? PiezasNormales { get; set; }

    [EnumDataType(typeof(VelocidadInicial))]
    public VelocidadInicial Velocidad { get; set; } = VelocidadInicial.Normal;

    public TPuntosLineas Puntos { get; set; } = new TPuntosLineas();

    [Range(1, int.MaxValue)]
    public int? MetaPuntos { get; set; }

    [Range(1, int.MaxValue)]
    public int? MinimoVictoriasCorona { get; set; }
}

// Cambia toda la configuración de una sala en espera (solo el creador). Lo que no se envía
// toma su valor por omisión.
public class TConfiguracionSala
{
    public TConfiguracionPiezas PiezasEspeciales { get; set; } = new TConfiguracionPiezas();

    public string[]? PiezasNormales { get; set; }

    [EnumDataType(typeof(VelocidadInicial))]
    public VelocidadInicial Velocidad { get; set; } = VelocidadInicial.Normal;

    public TPuntosLineas Puntos { get; set; } = new TPuntosLineas();

    [Range(1, int.MaxValue)]
    public int? MetaPuntos { get; set; }

    [Range(1, int.MaxValue)]
    public int? MinimoVictoriasCorona { get; set; }
}

// Puntos base por 1, 2, 3 y 4 líneas. Después se multiplican por el nivel, como antes.
public class TPuntosLineas
{
    [Range(0, ConfiguracionPiezas.PuntosMaximosPorJugada)]
    public int UnaLinea { get; set; } = 100;

    [Range(0, ConfiguracionPiezas.PuntosMaximosPorJugada)]
    public int DosLineas { get; set; } = 300;

    [Range(0, ConfiguracionPiezas.PuntosMaximosPorJugada)]
    public int TresLineas { get; set; } = 500;

    [Range(0, ConfiguracionPiezas.PuntosMaximosPorJugada)]
    public int CuatroLineas { get; set; } = 800;
}

// El cliente elige tipos activos del catálogo. El servidor valida la selección y sortea
// cada pieza; el cliente nunca envía el resultado del sorteo ni un efecto a ejecutar.
public class TConfiguracionPiezas
{
    public bool Activas { get; set; }

    // null conserva compatibilidad: todos los tipos activos del catálogo. Vacío no es válido si Activas.
    public string[]? TiposPermitidos { get; set; }

    // De 1 a 100 cuando Activas es true. Se ignora cuando Activas es false.
    [Range(0, 100)]
    public int Probabilidad { get; set; }
}

public class TVotoPausa
{
    public bool Aceptar { get; set; }
}

public class TMovimiento
{
    [EnumDataType(typeof(Movimiento))]
    public Movimiento Movimiento { get; set; }

    // Un número creciente por jugador permite descartar envíos duplicados.
    [Range(1, long.MaxValue)]
    public long Secuencia { get; set; }
}

public record TCelda(int Fila, int Columna);
public record TTablero(
    int? UsuarioId,
    int[][] Celdas,
    int TipoPieza,
    TCelda[] Pieza,
    int Puntuacion,
    int Lineas,
    int Nivel,
    bool Perdido,
    string? PiezaEspecial = null,
    TEventoTablero? Evento = null);

// Qué pasó en la última acción de un tablero, para que el frontend elija el sonido.
// Sonidos posibles: movimiento, rotacion, bloqueo, lineas, especial, derrota.
// Secuencia crece con cada acción; el cliente reproduce solo si es mayor a la última que vio.
public record TEventoTablero(
    long Secuencia,
    string[] Sonidos,
    int LineasEliminadas,
    string? PiezaEspecial);
public record TPuntuacion(
    int UsuarioId,
    string Nombre,
    int Puntos,
    int Lineas,
    int Nivel,
    bool Perdido,
    bool Listo,
    bool Conectado,
    long UltimaSecuencia,
    int? Posicion,
    bool Abandono = false,
    DateTime? ReconectarHastaUtc = null,
    int VictoriasSala = 0,
    bool Corona = false);
public record TSala(
    Guid Id,
    string Codigo,
    int CreadorId,
    ModoJuego Modo,
    EstadoSala Estado,
    int MaximoJugadores,
    long Version,
    long RevisionPersistencia,
    int? UsuarioTurno,
    TPuntuacion[] Jugadores,
    TTablero[] Tableros,
    int[] Ganadores,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    bool Privada,
    string? CodigoPrivado,
    TConfiguracionPiezas PiezasEspeciales,
    TPausa Pausa,
    string[] PiezasNormales,
    VelocidadInicial Velocidad,
    TPuntosLineas Puntos,
    int? MetaPuntos = null,
    int? MinimoVictoriasCorona = null,
    string? MotivoFin = null,
    TEfectoTemporal[]? Efectos = null,
    TActivacionEspecial? UltimoEfecto = null);

public record TEfectoTemporal(
    int UsuarioId,
    int AtacanteId,
    EfectoEspecial Tipo,
    DateTime InicioUtc,
    DateTime FinUtc,
    int MilisegundosRestantes,
    bool Activo,
    string? MotivoFin,
    int FactorVelocidad);

// Secuencia de sala para identificar cada activación aunque la siguiente pieza tenga el mismo código.
public record TActivacionEspecial(
    long Secuencia,
    string Codigo,
    int AtacanteId,
    int? AfectadoId,
    DateTime AhoraServidorUtc,
    int PuntosJugada,
    TCelda[] CeldasEliminadas,
    string Estado,
    EfectoEspecial Tipo = EfectoEspecial.Ninguno);

// Datos públicos de una sala del listado. No incluye códigos privados.
public record TSalaPublica(
    Guid Id,
    string Codigo,
    ModoJuego Modo,
    string CreadorNombre,
    int Jugadores,
    int MaximoJugadores,
    bool PiezasEspecialesActivas);

// Estado de la pausa y de su votación. Las horas son del servidor: el cliente calcula la cuenta
// regresiva con ellas y no decide cuándo termina la pausa.
public record TPausa(
    FasePausa Fase,
    int? SolicitanteId,
    string? SolicitanteNombre,
    int Habilitados,
    int VotosNecesarios,
    int[] VotosAFavor,
    int[] VotosEnContra,
    int[] SolicitudesUsadasPor,
    DateTime? VencimientoVotacionUtc,
    DateTime? InicioPausaUtc,
    DateTime? FinPausaUtc,
    int SegundosRestantes,
    DateTime AhoraServidorUtc,
    string? Detalle);

// Evento de SignalR pendiente de enviar al grupo de la sala.
public record TEventoSala(string Nombre, Guid SalaId, object Datos, string[]? ConexionesRetirar = null);
