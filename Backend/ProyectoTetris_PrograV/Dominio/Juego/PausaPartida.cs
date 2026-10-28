namespace Dominio.Juego;

public enum FasePausa
{
    Ninguna,
    Votando,
    Pausada
}

public enum ResultadoVotacion
{
    Pendiente,
    Aprobada,
    Rechazada
}

// Estado y reglas de la pausa de una sala. Es lógica pura: recibe la hora como parámetro
// y no conoce SignalR ni SQL. SalaLJ la usa siempre dentro del bloqueo de la sala.
public sealed class PausaPartida
{
    private readonly OpcionesPausa opciones;
    private readonly HashSet<int> votosAFavor = [];
    private readonly HashSet<int> votosEnContra = [];
    private readonly Dictionary<int, int> solicitudesPorJugador = new Dictionary<int, int>();

    public PausaPartida(OpcionesPausa opciones)
    {
        this.opciones = opciones;
    }

    public FasePausa Fase
    {
        get;
            private set;
    }

    public int? SolicitanteId
    {
        get;
            private set;
    }

    public DateTime? VencimientoVotacion
    {
        get;
            private set;
    }

    public DateTime? InicioPausa
    {
        get;
            private set;
    }

    // Es null en la pausa individual, que no tiene límite de tiempo.
    public DateTime? FinPausa
    {
        get;
            private set;
    }

    public int HabilitadosActuales
    {
        get;
            private set;
    }

    public IReadOnlyCollection<int> VotosAFavor
    {
        get
        {
            return votosAFavor;
        }
    }

    public IReadOnlyCollection<int> VotosEnContra
    {
        get
        {
            return votosEnContra;
        }
    }

    public IEnumerable<int> JugadoresConSolicitudUsada
    {
        get
        {
            return solicitudesPorJugador
                .Where(par => par.Value >= opciones.SolicitudesPorJugador)
                .Select(par => par.Key);
        }
    }

    // Única regla del umbral: mayoría estricta, es decir, más de la mitad de los habilitados.
    // Con 4 jugadores se necesitan 3 votos. Para cambiar el criterio solo se modifica este método.
    public static int VotosNecesarios(int habilitados)
    {
        return habilitados / 2 + 1;
    }

    public int VotosNecesariosActuales
    {
        get
        {
            return VotosNecesarios(HabilitadosActuales);
        }
    }

    // Abre una votación. El solicitante cuenta como voto a favor y la solicitud se consume
    // aunque los demás la rechacen. Devuelve el mensaje de error o null si todo salió bien.
    public string? Solicitar(int usuarioId, IReadOnlyCollection<int> habilitados, DateTime ahora)
    {
        if (Fase == FasePausa.Votando)
        {
            return "Ya hay una votación de pausa en curso.";
        }

        if (Fase == FasePausa.Pausada)
        {
            return "La partida ya está en pausa.";
        }

        if (!habilitados.Contains(usuarioId))
        {
            return "Solo los jugadores activos pueden solicitar una pausa.";
        }

        solicitudesPorJugador.TryGetValue(usuarioId, out var usadas);

        if (usadas >= opciones.SolicitudesPorJugador)
        {
            return "Ya utilizó su solicitud de pausa en esta partida.";
        }

        solicitudesPorJugador[usuarioId] = usadas + 1;
        votosAFavor.Clear();
        votosEnContra.Clear();
        votosAFavor.Add(usuarioId);
        SolicitanteId = usuarioId;
        HabilitadosActuales = habilitados.Count;
        VencimientoVotacion = ahora.AddSeconds(opciones.SegundosVotacion);
        Fase = FasePausa.Votando;
        return null;
    }

    // Comprueba que el jugador pueda votar y registra su decisión sin permitir votos repetidos.
    public string? Votar(int usuarioId, bool aceptar, IReadOnlyCollection<int> habilitados, DateTime ahora)
    {
        if (Fase != FasePausa.Votando || ahora >= VencimientoVotacion)
        {
            return "No hay una votación de pausa abierta.";
        }

        if (!habilitados.Contains(usuarioId))
        {
            return "Los espectadores no tienen derecho a voto.";
        }

        var yaVoto = votosAFavor.Contains(usuarioId) || votosEnContra.Contains(usuarioId);

        if (yaVoto)
        {
            return "Ya registró su voto.";
        }

        if (aceptar)
        {
            votosAFavor.Add(usuarioId);
        }
        else
        {
            votosEnContra.Add(usuarioId);
        }

        HabilitadosActuales = habilitados.Count;
        return null;
    }

    // Revisa si la votación ya se decidió. Los votos no emitidos nunca cuentan como favorables.
    // Si resulta aprobada, la pausa comienza en este mismo momento.
    public ResultadoVotacion Evaluar(IReadOnlyCollection<int> habilitados, DateTime ahora)
    {
        if (Fase != FasePausa.Votando)
        {
            return ResultadoVotacion.Pendiente;
        }

        HabilitadosActuales = habilitados.Count;
        var necesarios = VotosNecesarios(habilitados.Count);
        var favorables = habilitados.Count(id => votosAFavor.Contains(id));
        var contrarios = habilitados.Count(id => votosEnContra.Contains(id));
        var sinVotar = habilitados.Count - favorables - contrarios;

        if (favorables >= necesarios)
        {
            IniciarPausaPorVotacion(ahora);
            return ResultadoVotacion.Aprobada;
        }

        var yaNoPuedeAprobarse = favorables + sinVotar < necesarios;
        var tiempoAgotado = ahora >= VencimientoVotacion;

        if (yaNoPuedeAprobarse || tiempoAgotado)
        {
            LimpiarVotacion();
            return ResultadoVotacion.Rechazada;
        }

        return ResultadoVotacion.Pendiente;
    }

    // La pausa individual no necesita votos ni tiene límite de tiempo.
    public void PausarIndividual(int usuarioId, DateTime ahora)
    {
        Fase = FasePausa.Pausada;
        SolicitanteId = usuarioId;
        InicioPausa = ahora;
        FinPausa = null;
    }

    // Indica si una pausa con límite de tiempo llegó a su fecha final.
    public bool PausaVencida(DateTime ahora)
    {
        return Fase == FasePausa.Pausada && FinPausa.HasValue && ahora >= FinPausa.Value;
    }

    // Termina la pausa y devuelve cuánto duró, para desplazar los temporizadores del juego.
    public TimeSpan Reanudar(DateTime ahora)
    {
        var duracion = TimeSpan.Zero;

        if (Fase == FasePausa.Pausada && InicioPausa.HasValue)
        {
            duracion = ahora - InicioPausa.Value;

            if (FinPausa.HasValue && ahora > FinPausa.Value)
            {
                duracion = FinPausa.Value - InicioPausa.Value;
            }
        }

        Cancelar();
        return duracion;
    }

    // Se usa cuando la partida termina mientras había una votación o una pausa.
    public void Cancelar()
    {
        LimpiarVotacion();
        InicioPausa = null;
        FinPausa = null;
    }

    // Cambia de votación a pausa y fija su fecha de finalización.
    private void IniciarPausaPorVotacion(DateTime ahora)
    {
        Fase = FasePausa.Pausada;
        VencimientoVotacion = null;
        InicioPausa = ahora;
        FinPausa = ahora.AddSeconds(opciones.SegundosPausa);
    }

    // Limpia solicitante, votos y plazos para cerrar la solicitud actual.
    private void LimpiarVotacion()
    {
        Fase = FasePausa.Ninguna;
        SolicitanteId = null;
        VencimientoVotacion = null;
        votosAFavor.Clear();
        votosEnContra.Clear();
    }
}
