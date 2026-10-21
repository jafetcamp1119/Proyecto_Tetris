using Dominio.Juego;

namespace LogicaJuego.Servicios;

// Estado en memoria de un jugador dentro de una sala. Solo SalaLJ lo modifica, con el bloqueo de la sala.
internal sealed class JugadorSala
{
    public int Id;
    public string Nombre;
    public bool Listo;
    public bool Perdido;
    public bool Abandono;
    public int Victorias;
    public int Puntos;
    public int Lineas;
    public int Nivel = 1;
    public long Secuencia;
    public readonly HashSet<string> Conexiones = [];
    public DateTime? Desconexion;
    public TableroJuego? Tablero;
    public DateTime UltimaCaida;

    public JugadorSala(int id, string nombre)
    {
        this.Id = id;
        this.Nombre = nombre;
    }
}

// Estado en memoria de una sala. Cada sala tiene su propio bloqueo para serializar sus cambios.
internal sealed class SalaMemoria
{
    public readonly object Cerrojo = new();
    public Guid Id = Guid.NewGuid();
    public int Creador;
    public int Maximo;
    public int Turno;
    public int? MetaPuntos;
    public int? MinimoVictoriasCorona;
    public int[] Ganadores = [];
    public string? MotivoFin;
    public ModoJuego Modo;
    public EstadoSala Estado;
    public List<JugadorSala> Jugadores = [];
    public TableroJuego? Compartido;
    public DateTime Creada = DateTime.UtcNow;
    public DateTime UltimaCaida;
    public DateTime? Inicio;
    public DateTime? Fin;
    public long Version;
    public long Revision;
    public long RevisionGuardada;

    // Privacidad: una sala privada se identifica con un código de seis caracteres generado por el servidor.
    public bool Privada;
    public string? CodigoPrivado;

    public ConfiguracionPiezas Piezas = ConfiguracionPiezas.SoloNormales;
    public PausaPartida Pausa;

    public SalaMemoria(int creador, ModoJuego modo, int maximo, OpcionesPausa opcionesPausa)
    {
        this.Creador = creador;
        this.Maximo = maximo;
        this.Modo = modo;
        this.Pausa = new PausaPartida(opcionesPausa);
    }

    public JugadorSala? BuscarJugador(int usuarioId)
    {
        return Jugadores.Find(jugador => jugador.Id == usuarioId);
    }

    // Jugadores con derecho a pausar y votar: los que siguen en la partida. Los eliminados son espectadores.
    public List<int> JugadoresHabilitados()
    {
        return Jugadores
            .Where(jugador => !jugador.Perdido)
            .Select(jugador => jugador.Id)
            .ToList();
    }
}
