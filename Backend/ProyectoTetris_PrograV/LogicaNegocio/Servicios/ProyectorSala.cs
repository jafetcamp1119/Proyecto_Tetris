using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.Juego;

namespace LogicaJuego.Servicios;

// Convierte el estado interno de una sala en las copias (DTO) que se envían a los clientes.
internal sealed class ProyectorSala
{
    private readonly IMotorJuego motor;

    public ProyectorSala(IMotorJuego motor)
    {
        this.motor = motor;
    }

    public TSala Sala(SalaMemoria sala)
    {
        var jugadores = ProyectarJugadores(sala);
        var finalizada = sala.Estado == EstadoSala.Finalizada;

        return new TSala(
            sala.Id,
            sala.CodigoIngreso ?? string.Empty,
            sala.Creador,
            sala.Modo,
            sala.Estado,
            sala.Maximo,
            sala.Version,
            sala.Revision,
            UsuarioConTurno(sala),
            jugadores,
            ProyectarTableros(sala),
            Ganadores(sala, jugadores, finalizada),
            sala.Inicio,
            sala.Fin,
            sala.Privada,
            sala.Privada ? sala.CodigoIngreso : null,
            ProyectarConfiguracionPiezas(sala.Piezas),
            Pausa(sala, DateTime.UtcNow, null),
            ProyectarPiezasNormales(sala.Piezas),
            sala.Piezas.Velocidad,
            ProyectarPuntos(sala.Piezas),
            sala.MetaPuntos,
            sala.MinimoVictoriasCorona,
            sala.MotivoFin,
            sala.Jugadores.SelectMany(jugador => jugador.Efectos).ToArray(),
            CopiarUltimoEfecto(sala),
            sala.NumeroRonda,
            sala.Modo == ModoJuego.Compartido ? new TProgresoCompartido(sala.Compartido?.Lineas ?? 0,
                sala.Compartido?.Nivel ?? 1, (sala.Compartido?.Lineas ?? 0) % 10,
                sala.Compartido?.IntervaloCaidaMs ?? sala.Piezas.IntervaloBaseMs) : null);
    }

    private static TActivacionEspecial? CopiarUltimoEfecto(SalaMemoria sala)
    {
        if (sala.UltimoEfecto == null)
        {
            return null;
        }

        return sala.UltimoEfecto with { CeldasEliminadas = sala.UltimoEfecto.CeldasEliminadas.ToArray() };
    }

    private static string[] ProyectarPiezasNormales(ConfiguracionPiezas piezas)
    {
        return piezas.FormasPermitidas
            .Select(indice => ConfiguracionPiezas.CodigosFormas[indice])
            .ToArray();
    }

    private static TPuntosLineas ProyectarPuntos(ConfiguracionPiezas piezas)
    {
        return new TPuntosLineas
        {
            UnaLinea = piezas.PuntosPorLineas[0],
            DosLineas = piezas.PuntosPorLineas[1],
            TresLineas = piezas.PuntosPorLineas[2],
            CuatroLineas = piezas.PuntosPorLineas[3]
        };
    }

    public TSalaPublica SalaPublica(SalaMemoria sala)
    {
        var creador = sala.BuscarJugador(sala.Creador);

        return new TSalaPublica(
            sala.Id,
            sala.CodigoIngreso!,
            sala.Modo,
            creador?.Nombre ?? string.Empty,
            sala.Jugadores.Count,
            sala.Maximo,
            sala.Piezas.EspecialesActivas);
    }

    public TPausa Pausa(SalaMemoria sala, DateTime ahora, string? detalle)
    {
        var pausa = sala.Pausa;
        var nombreSolicitante = sala.Jugadores.Find(j => j.Id == pausa.SolicitanteId)?.Nombre;

        return new TPausa(
            pausa.Fase,
            pausa.SolicitanteId,
            nombreSolicitante,
            pausa.HabilitadosActuales,
            pausa.VotosNecesariosActuales,
            pausa.VotosAFavor.ToArray(),
            pausa.VotosEnContra.ToArray(),
            pausa.JugadoresConSolicitudUsada.ToArray(),
            pausa.VencimientoVotacion,
            pausa.InicioPausa,
            pausa.FinPausa,
            SegundosRestantes(pausa, ahora),
            ahora,
            detalle);
    }

    private static int SegundosRestantes(PausaPartida pausa, DateTime ahora)
    {
        DateTime? limite = null;

        if (pausa.Fase == FasePausa.Votando)
        {
            limite = pausa.VencimientoVotacion;
        }
        else if (pausa.Fase == FasePausa.Pausada)
        {
            limite = pausa.FinPausa;
        }

        if (!limite.HasValue)
        {
            return 0;
        }

        var restante = Math.Ceiling((limite.Value - ahora).TotalSeconds);
        return Math.Max(0, (int)restante);
    }

    private static TConfiguracionPiezas ProyectarConfiguracionPiezas(ConfiguracionPiezas piezas)
    {
        return new TConfiguracionPiezas
        {
            Activas = piezas.EspecialesActivas,
            Probabilidad = piezas.ProbabilidadEspecial,
            TiposPermitidos = piezas.TiposDisponibles.Select(tipo => tipo.Codigo).ToArray()
        };
    }

    private static TPuntuacion[] ProyectarJugadores(SalaMemoria sala)
    {
        var finalizada = sala.Estado == EstadoSala.Finalizada;
        var jugadores = new List<TPuntuacion>();

        foreach (var jugador in sala.Jugadores)
        {
            int? posicion = null;

            if (finalizada && sala.Ganadores.Length > 0)
            {
                // Los empates comparten posición; el orden de eliminación no desempata.
                posicion = CalcularPosicion(sala, jugador);
            }

            jugadores.Add(new TPuntuacion(
                jugador.Id,
                jugador.Nombre,
                jugador.Puntos,
                jugador.Lineas,
                sala.Compartido?.Nivel ?? jugador.Nivel,
                jugador.Perdido,
                jugador.Listo,
                jugador.Conexiones.Count > 0,
                jugador.Secuencia,
                posicion,
                jugador.Abandono,
                jugador.Desconexion?.AddSeconds(30),
                jugador.Victorias,
                sala.Modo != ModoJuego.Individual
                    && jugador.Victorias >= (sala.MinimoVictoriasCorona ?? 1)
                    && jugador.Victorias == sala.Jugadores.Max(otro => otro.Victorias),
                sala.Modo != ModoJuego.Individual && finalizada && sala.Ganadores.Contains(jugador.Id)));
        }

        return jugadores.ToArray();
    }

    private static int CalcularPosicion(SalaMemoria sala, JugadorSala jugador)
    {
        if (sala.Ganadores.Contains(jugador.Id))
        {
            return 1;
        }

        var mejoresSinVictoria = sala.Jugadores.Count(otro =>
            !sala.Ganadores.Contains(otro.Id) && otro.Puntos > jugador.Puntos);
        return 1 + sala.Ganadores.Length + mejoresSinVictoria;
    }

    private static int[] Ganadores(SalaMemoria sala, TPuntuacion[] jugadores, bool finalizada)
    {
        return finalizada ? sala.Ganadores.ToArray() : [];
    }

    private TTablero[] ProyectarTableros(SalaMemoria sala)
    {
        var tableros = new List<TTablero>();

        if (sala.Compartido != null)
        {
            tableros.Add(motor.ObtenerEstado(sala.Compartido, null));
            return tableros.ToArray();
        }

        foreach (var jugador in sala.Jugadores.Where(jugador => jugador.Tablero != null))
        {
            tableros.Add(motor.ObtenerEstado(jugador.Tablero!, jugador.Id));
        }

        return tableros.ToArray();
    }

    private static int? UsuarioConTurno(SalaMemoria sala)
    {
        if (sala.Modo == ModoJuego.Compartido && sala.Estado == EstadoSala.EnCurso)
        {
            return sala.Jugadores[sala.Turno].Id;
        }

        return null;
    }
}
