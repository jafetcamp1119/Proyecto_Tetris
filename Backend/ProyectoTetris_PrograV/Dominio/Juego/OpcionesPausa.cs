namespace Dominio.Juego;

// Valores de las pausas. Se leen de la sección "Pausas" de appsettings.json;
// si no existe, se usan estos valores confirmados.
public class OpcionesPausa
{
    // Tiempo máximo para votar una solicitud de pausa.
    public int SegundosVotacion { get; set; } = 15;

    // Duración máxima de una pausa aprobada; después la partida se reanuda sola.
    public int SegundosPausa { get; set; } = 180;

    // Solicitudes de pausa que puede hacer cada jugador durante una partida multijugador.
    public int SolicitudesPorJugador { get; set; } = 1;
}
