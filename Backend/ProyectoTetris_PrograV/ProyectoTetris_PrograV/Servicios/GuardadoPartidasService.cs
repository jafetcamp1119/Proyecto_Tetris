using Dominio.InterfazJuego;
using Dominio.InterfazLJ;

namespace ProyectoTetris_PrograV.Servicios;

// El guardado tiene su propio reloj: una caída de SQL no bloquea la caída de las piezas.
public class GuardadoPartidasService : BackgroundService
{
    private readonly ISalaLJ salas;
    private readonly IServiceScopeFactory scopes;
    private readonly ILogger<GuardadoPartidasService> logger;

    public GuardadoPartidasService(ISalaLJ salas, IServiceScopeFactory scopes, ILogger<GuardadoPartidasService> logger)
    {
        this.salas = salas;
        this.scopes = scopes;
        this.logger = logger;
    }

    // Cada dos segundos cancela históricos interrumpidos y guarda revisiones pendientes.
    // Abre un contexto corto por operación y reintenta los fallos sin detener el reloj del juego.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var inicioLimpio = false;
        using var reloj = new PeriodicTimer(TimeSpan.FromSeconds(2));

        try
        {
            while (await reloj.WaitForNextTickAsync(stoppingToken))
            {
                if (!inicioLimpio)
                {
                    try
                    {
                        await using var alcance = scopes.CreateAsyncScope();
                        await alcance.ServiceProvider.GetRequiredService<IRegistroPartidaLJ>()
                            .CancelarInterrumpidasAsync(stoppingToken);
                        inicioLimpio = true;
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError(ex, "No se pudieron cancelar las partidas interrumpidas; se reintentará.");
                        continue;
                    }
                }

                foreach (var estado in salas.PendientesPersistencia())
                {
                    try
                    {
                        await using var alcance = scopes.CreateAsyncScope();
                        await alcance.ServiceProvider.GetRequiredService<IRegistroPartidaLJ>().GuardarAsync(
                            estado,
                            stoppingToken);
                        salas.ConfirmarPersistencia(estado.Id, estado.RevisionPersistencia);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError(ex, "No se pudo guardar la sala {Sala}; se reintentará.", estado.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
