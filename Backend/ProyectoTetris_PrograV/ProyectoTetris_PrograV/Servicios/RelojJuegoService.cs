using Dominio.InterfazJuego;
namespace ProyectoTetris_PrograV.Servicios;

// Avanza las salas cada cien milisegundos y publica los estados que cambiaron.
public class RelojJuegoService : BackgroundService
{
    private readonly ISalaLJ salas;
    private readonly PublicadorSalas publicador;

    public RelojJuegoService(ISalaLJ salas, PublicadorSalas publicador)
    {
        this.salas = salas;
        this.publicador = publicador;
    }

    // Usa un temporizador del servidor para avanzar las salas y publicar los cambios.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var reloj = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

        try
        {
            while (await reloj.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var estado in salas.Avanzar(DateTime.UtcNow))
                {
                    await publicador.PublicarEstadoAsync(estado, stoppingToken);
                }

                await publicador.PublicarEventosAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
