using System.Security.Claims;
using Dominio.InterfazLJ;
using Microsoft.AspNetCore.SignalR;
using Utilitarios;

namespace ProyectoTetris_PrograV.Seguridad;

// También comprueba revocaciones para un WebSocket que ya estaba conectado.
public class FiltroSesionJuego : IHubFilter
{
    private readonly IServiceScopeFactory fabricaAmbitos;
    private readonly bool diagnosticar;
    private readonly ILogger mediciones;

    public FiltroSesionJuego(IServiceScopeFactory fabricaAmbitos, IConfiguration configuracion, ILoggerFactory registros)
    {
        this.fabricaAmbitos = fabricaAmbitos;
        diagnosticar = configuracion.GetValue<bool>("Diagnostico:Rendimiento");
        mediciones = registros.CreateLogger("TetrisRendimiento");
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext contexto,
        Func<HubInvocationContext, ValueTask<object?>> siguiente)
    {
        var inicio = diagnosticar ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        using var ambito = fabricaAmbitos.CreateScope();
        var conexiones = ambito.ServiceProvider.GetRequiredService<ConexionesSesion>();
        var acceso = ambito.ServiceProvider.GetRequiredService<IAccesoUsuarioLJ>();
        var identidad = contexto.Context.User;
        var usuarioValido = int.TryParse(identidad?.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId);
        var sesionValida = Guid.TryParse(identidad?.FindFirstValue("sid"), out var sesionId);
        if (!conexiones.PermitirAccion(usuarioId)) { throw new HubException("Demasiadas acciones. Espera un momento."); }
        using var exclusion = await ExclusionSesiones.TomarAsync(usuarioId, contexto.Context.ConnectionAborted);
        var fechaValida = long.TryParse(identidad?.FindFirstValue("exp"), out var expira) && expira > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var error = usuarioValido && sesionValida && fechaValida ? acceso.ErrorSesion(sesionId, usuarioId) : "SesionVencida";
        if (error != null)
        {
            try { await contexto.Hub.Clients.Caller.SendAsync(error == "SesionReemplazada" ? "SesionReemplazada" : "SesionTerminada", new { Codigo = error }); }
            finally { contexto.Context.Abort(); }
            throw new HubException("La sesión del dispositivo ya no está vigente.");
        }

        var validado = diagnosticar ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        var resultado = await siguiente(contexto);
        if (diagnosticar)
        {
            mediciones.LogInformation("Comando {Metodo}: validacion_y_espera_ms={Validacion:F3}; ejecucion_y_publicacion_ms={Ejecucion:F3}",
                contexto.HubMethodName, System.Diagnostics.Stopwatch.GetElapsedTime(inicio, validado).TotalMilliseconds,
                System.Diagnostics.Stopwatch.GetElapsedTime(validado).TotalMilliseconds);
        }
        return resultado;
    }

    public async Task OnConnectedAsync(HubLifetimeContext contexto, Func<HubLifetimeContext, Task> siguiente)
    {
        var usuarioId = int.Parse(contexto.Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sesionId = Guid.Parse(contexto.Context.User!.FindFirstValue("sid")!);
        using var exclusion = await ExclusionSesiones.TomarAsync(usuarioId, contexto.Context.ConnectionAborted);
        using var ambito = fabricaAmbitos.CreateScope();
        var acceso = ambito.ServiceProvider.GetRequiredService<IAccesoUsuarioLJ>();
        var error = acceso.ErrorSesion(sesionId, usuarioId);
        if (error != null)
        {
            try { await contexto.Hub.Clients.Caller.SendAsync(error == "SesionReemplazada" ? "SesionReemplazada" : "SesionTerminada", new { Codigo = error }); }
            finally { contexto.Context.Abort(); }
            return;
        }
        await siguiente(contexto);
    }
}
