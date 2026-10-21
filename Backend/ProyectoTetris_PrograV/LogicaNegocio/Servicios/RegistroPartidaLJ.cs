using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;
using Dominio.Juego;

namespace LogicaJuego.Servicios;

public class RegistroPartidaLJ : IRegistroPartidaLJ
{
    private readonly IUnidadTrabajoEF unidad;

    public RegistroPartidaLJ(IUnidadTrabajoEF unidad)
    {
        this.unidad = unidad;
    }

    // Al iniciar el proceso no se recupera ninguna sala en memoria.
    public async Task CancelarInterrumpidasAsync(CancellationToken cancellationToken = default)
    {
        var consulta = unidad.TPartida.Buscar(partida => partida.FechaFin == null);

        if (!consulta.Success)
        {
            throw new InvalidOperationException(consulta.Error);
        }

        foreach (var partida in consulta.Data!)
        {
            partida.Estado = EstadoSala.Cancelada.ToString();
            partida.FechaFin = DateTime.UtcNow;
            partida.GanadorId = null;
            CancelarParticipantes(partida);
            EliminarResultadosProvisionales(partida);
        }

        await unidad.CompletarAsync(cancellationToken);
    }

    private void CancelarParticipantes(Partida partida)
    {
        foreach (var participante in BuscarParticipantes(partida))
        {
            participante.Estado = "Eliminado";
            participante.FechaEliminacion ??= DateTime.UtcNow;
            participante.PosicionFinal = null;
            participante.EsTurnoActual = false;
        }
    }

    private void EliminarResultadosProvisionales(Partida partida)
    {
        foreach (var resultado in BuscarResultados(partida))
        {
            unidad.TResultado.Eliminar(resultado);
        }
    }

    public async Task GuardarAsync(TSala sala, CancellationToken cancellationToken = default)
    {
        var partida = unidad.TPartida.ObtenerEntidad(p => p.CodigoSala == sala.Codigo).Data;

        if (partida?.FechaFin != null)
        {
            return; // Una repetición no duplica resultados ni estadísticas.
        }

        if (partida == null && sala.Estado == EstadoSala.Cancelada && sala.FechaInicio == null)
        {
            return;
        }

        partida ??= CrearPartida(sala);
        ActualizarPartida(partida, sala);

        if (sala.Estado == EstadoSala.Cancelada)
        {
            CancelarParticipantes(partida);
            EliminarResultadosProvisionales(partida);
            await unidad.CompletarAsync(cancellationToken);
            return;
        }

        var participantes = BuscarParticipantes(partida);
        var resultados = BuscarResultados(partida);

        foreach (var jugador in sala.Jugadores)
        {
            var participante = GuardarParticipante(partida, participantes, jugador, sala);

            if (!jugador.Perdido && sala.Estado != EstadoSala.Finalizada)
            {
                continue;
            }

            var resultado = GuardarResultado(partida, resultados, jugador, participante, sala);

            if (sala.Estado == EstadoSala.Finalizada)
            {
                ActualizarEstadistica(jugador, resultado);
            }
        }

        // Un solo SaveChanges guarda partida, participantes, resultados y estadísticas atómicamente.
        await unidad.CompletarAsync(cancellationToken);
    }

    private Partida CrearPartida(TSala sala)
    {
        var partida = new Partida
        {
            CodigoSala = sala.Codigo,
            CreadorId = sala.CreadorId,
            TipoPartida = sala.Modo.ToString(),
            EsPrivada = sala.Privada,
            CantidadMinimaJugadores = sala.Modo == ModoJuego.Individual ? 1 : 4,
            CantidadMaximaJugadores = sala.MaximoJugadores,
            FechaCreacion = sala.FechaInicio ?? DateTime.UtcNow
        };

        unidad.TPartida.Insertar(partida);
        return partida;
    }

    private static void ActualizarPartida(Partida partida, TSala sala)
    {
        partida.Estado = sala.Estado.ToString();
        partida.FechaInicio = sala.FechaInicio;
        partida.FechaFin = sala.FechaFin;

        // Un empate tiene varios resultados ganadores y ningún GanadorId único.
        partida.GanadorId = sala.Ganadores.Length == 1 ? sala.Ganadores[0] : null;
    }

    private List<ParticipantesPartidum> BuscarParticipantes(Partida partida)
    {
        if (partida.PartidaId == 0)
        {
            return [];
        }

        return unidad.TParticipante.Buscar(p => p.PartidaId == partida.PartidaId).Data!.ToList();
    }

    private List<Resultado> BuscarResultados(Partida partida)
    {
        if (partida.PartidaId == 0)
        {
            return [];
        }

        return unidad.TResultado.Buscar(r => r.PartidaId == partida.PartidaId).Data!.ToList();
    }

    private ParticipantesPartidum GuardarParticipante(
        Partida partida,
        List<ParticipantesPartidum> participantes,
        TPuntuacion jugador,
        TSala sala)
    {
        var participante = participantes.Find(p => p.UsuarioId == jugador.UsuarioId);

        if (participante == null)
        {
            participante = new ParticipantesPartidum
            {
                Partida = partida,
                UsuarioId = jugador.UsuarioId,
                FechaIngreso = sala.FechaInicio ?? DateTime.UtcNow
            };
            unidad.TParticipante.Insertar(participante);
        }

        participante.Estado = jugador.Perdido ? "Eliminado"
            : sala.Estado == EstadoSala.Finalizada ? "Finalizado" : "Jugando";
        participante.EsTurnoActual = sala.UsuarioTurno == jugador.UsuarioId;
        participante.Puntuacion = jugador.Puntos;
        participante.LineasEliminadas = jugador.Lineas;
        participante.NivelActual = jugador.Nivel;
        participante.Listo = jugador.Listo;
        participante.PosicionFinal = jugador.Posicion;

        if (jugador.Perdido)
        {
            participante.FechaEliminacion ??= DateTime.UtcNow;
        }

        return participante;
    }

    // El resultado se crea cuando el jugador pierde; su puntuación queda registrada desde ese momento.
    private Resultado GuardarResultado(
        Partida partida,
        List<Resultado> resultados,
        TPuntuacion jugador,
        ParticipantesPartidum participante,
        TSala sala)
    {
        var resultado = resultados.Find(r => r.UsuarioId == jugador.UsuarioId);

        if (resultado == null)
        {
            resultado = new Resultado
            {
                Partida = partida,
                UsuarioId = jugador.UsuarioId
            };
            unidad.TResultado.Insertar(resultado);
        }

        resultado.Puntuacion = jugador.Puntos;
        resultado.LineasEliminadas = jugador.Lineas;
        resultado.NivelMaximo = jugador.Nivel;
        resultado.PosicionFinal = jugador.Posicion ?? 0; // 0 significa clasificación todavía pendiente.
        resultado.EstadoFinal = sala.Ganadores.Contains(jugador.UsuarioId) ? "Ganador" : "Eliminado";
        resultado.EsGanador = sala.Ganadores.Contains(jugador.UsuarioId);
        resultado.FechaResultado = sala.FechaFin ?? participante.FechaEliminacion!.Value;
        return resultado;
    }

    private void ActualizarEstadistica(TPuntuacion jugador, Resultado resultado)
    {
        var estadistica = unidad.TEstadistica.ObtenerEntidad(e => e.UsuarioId == jugador.UsuarioId).Data;

        if (estadistica == null)
        {
            estadistica = new EstadisticasJugador
            {
                UsuarioId = jugador.UsuarioId,
                NivelMaximoAlcanzado = 1
            };
            unidad.TEstadistica.Insertar(estadistica);
        }

        estadistica.PartidasJugadas++;

        if (resultado.EsGanador)
        {
            estadistica.PartidasGanadas++;
        }
        else
        {
            estadistica.PartidasPerdidas++;
        }

        estadistica.TotalPuntos += jugador.Puntos;
        estadistica.LineasEliminadas += jugador.Lineas;
        estadistica.PuntuacionMaxima = Math.Max(estadistica.PuntuacionMaxima, jugador.Puntos);
        estadistica.NivelMaximoAlcanzado = Math.Max(estadistica.NivelMaximoAlcanzado, jugador.Nivel);
        estadistica.FechaActualizacion = DateTime.UtcNow;
    }
}
