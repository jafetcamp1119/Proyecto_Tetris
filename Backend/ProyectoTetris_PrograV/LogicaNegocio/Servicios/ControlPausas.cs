using System.Collections.Concurrent;
using Dominio.EntidadesTipadas;
using Dominio.Juego;

namespace LogicaJuego.Servicios;

// Coordina la pausa de las salas: solicitudes, votos, vencimientos y reanudación.
// Las reglas del voto están en PausaPartida; aquí se aplican a la sala y se generan los eventos
// que la API publicará por SignalR. Todos los métodos se llaman con el bloqueo de la sala tomado.
internal sealed class ControlPausas
{
    public const string EventoSolicitada = "PausaSolicitada";
    public const string EventoVotoRegistrado = "VotoPausaRegistrado";
    public const string EventoRechazada = "PausaRechazada";
    public const string EventoIniciada = "PausaIniciada";
    public const string EventoFinalizada = "PausaFinalizada";

    private readonly ProyectorSala proyector;
    private readonly ConcurrentQueue<TEventoSala> eventos = new();

    public ControlPausas(ProyectorSala proyector)
    {
        this.proyector = proyector;
    }

    public bool EstaPausada(SalaMemoria sala)
    {
        return sala.Pausa.Fase == FasePausa.Pausada;
    }

    public string? Solicitar(SalaMemoria sala, JugadorSala jugador, DateTime ahora)
    {
        if (sala.Estado != EstadoSala.EnCurso)
        {
            return "Solo se puede pausar una partida en curso.";
        }

        if (sala.Modo == ModoJuego.Individual)
        {
            return PausarIndividual(sala, jugador, ahora);
        }

        return AbrirVotacion(sala, jugador, ahora);
    }

    public string? Votar(SalaMemoria sala, JugadorSala jugador, bool aceptar, DateTime ahora)
    {
        var error = sala.Pausa.Votar(jugador.Id, aceptar, sala.JugadoresHabilitados(), ahora);

        if (error != null)
        {
            return error;
        }

        sala.Version++;
        Encolar(EventoVotoRegistrado, sala, ahora, $"{jugador.Nombre} votó.");
        ResolverVotacion(sala, ahora);
        return null;
    }

    public string? ReanudarIndividual(SalaMemoria sala, DateTime ahora)
    {
        if (sala.Modo != ModoJuego.Individual)
        {
            return "La pausa multijugador termina automáticamente al cumplirse el tiempo.";
        }

        if (!EstaPausada(sala))
        {
            return "La partida no está en pausa.";
        }

        Reanudar(sala, ahora, "Partida reanudada.");
        return null;
    }

    // Se llama en cada tick del reloj: cierra votaciones vencidas y reanuda pausas que cumplieron su tiempo.
    public void Actualizar(SalaMemoria sala, DateTime ahora)
    {
        if (sala.Pausa.Fase == FasePausa.Votando)
        {
            ResolverVotacion(sala, ahora);
        }
        else if (sala.Pausa.PausaVencida(ahora))
        {
            Reanudar(sala, ahora, "La pausa terminó por tiempo.");
        }
    }

    // Si la partida termina mientras hay una votación o una pausa, se cierran sin más trámite.
    public void CancelarSiTermino(SalaMemoria sala, DateTime ahora)
    {
        var habiaPausa = sala.Pausa.Fase != FasePausa.Ninguna;

        if (sala.Estado != EstadoSala.Finalizada || !habiaPausa)
        {
            return;
        }

        sala.Pausa.Cancelar();
        Encolar(EventoFinalizada, sala, ahora, "La partida terminó.");
    }

    public TEventoSala[] TomarEventos()
    {
        var pendientes = new List<TEventoSala>();

        while (eventos.TryDequeue(out var evento))
        {
            pendientes.Add(evento);
        }

        return pendientes.ToArray();
    }

    private string? PausarIndividual(SalaMemoria sala, JugadorSala jugador, DateTime ahora)
    {
        if (EstaPausada(sala))
        {
            return "La partida ya está en pausa.";
        }

        sala.Pausa.PausarIndividual(jugador.Id, ahora);
        sala.Version++;
        Encolar(EventoIniciada, sala, ahora, "Partida pausada.");
        return null;
    }

    private string? AbrirVotacion(SalaMemoria sala, JugadorSala jugador, DateTime ahora)
    {
        var error = sala.Pausa.Solicitar(jugador.Id, sala.JugadoresHabilitados(), ahora);

        if (error != null)
        {
            return error;
        }

        sala.Version++;
        Encolar(EventoSolicitada, sala, ahora, $"{jugador.Nombre} solicitó una pausa.");
        ResolverVotacion(sala, ahora);
        return null;
    }

    private void ResolverVotacion(SalaMemoria sala, DateTime ahora)
    {
        var solicitante = sala.Jugadores.Find(j => j.Id == sala.Pausa.SolicitanteId)?.Nombre;
        var resultado = sala.Pausa.Evaluar(sala.JugadoresHabilitados(), ahora);

        if (resultado == ResultadoVotacion.Aprobada)
        {
            sala.Version++;
            Encolar(EventoIniciada, sala, ahora, "Pausa aprobada por mayoría.");
        }
        else if (resultado == ResultadoVotacion.Rechazada)
        {
            sala.Version++;
            var detalle = $"La pausa solicitada por {solicitante} no obtuvo la mayoría de votos.";
            Encolar(EventoRechazada, sala, ahora, detalle);
        }
    }

    private void Reanudar(SalaMemoria sala, DateTime ahora, string detalle)
    {
        var duracion = sala.Pausa.Reanudar(ahora);
        DesplazarTemporizadores(sala, duracion);
        sala.Version++;
        Encolar(EventoFinalizada, sala, ahora, detalle);
    }

    // La caída no acumula tiempo en pausa. La reconexión usa siempre 30 segundos reales.
    private void DesplazarTemporizadores(SalaMemoria sala, TimeSpan duracion)
    {
        sala.UltimaCaida = sala.UltimaCaida.Add(duracion);

        foreach (var jugador in sala.Jugadores)
        {
            jugador.UltimaCaida = jugador.UltimaCaida.Add(duracion);
            for (var indice = 0; indice < jugador.Efectos.Count; indice++)
            {
                var efecto = jugador.Efectos[indice];

                if (efecto.Activo)
                {
                    jugador.Efectos[indice] = efecto with { FinUtc = efecto.FinUtc.Add(duracion) };
                }
            }
        }
    }

    private void Encolar(string nombre, SalaMemoria sala, DateTime ahora, string detalle)
    {
        var datos = proyector.Pausa(sala, ahora, detalle);
        eventos.Enqueue(new TEventoSala(nombre, sala.Id, datos));
    }
}
