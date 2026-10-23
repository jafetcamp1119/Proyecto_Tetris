using System.Security.Claims;
using Dominio.InterfazLJ;
using Microsoft.AspNetCore.SignalR;

namespace ProyectoTetris_PrograV.Seguridad;

// También comprueba revocaciones para un WebSocket que ya estaba conectado.
public class FiltroSesionJuego : IHubFilter
{
    private readonly IServiceScopeFactory fabricaAmbitos;

    public FiltroSesionJuego(IServiceScopeFactory fabricaAmbitos)
    {
        this.fabricaAmbitos = fabricaAmbitos;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext contexto,
        Func<HubInvocationContext, ValueTask<object?>> siguiente)
    {
        using var ambito = fabricaAmbitos.CreateScope();
        var acceso = ambito.ServiceProvider.GetRequiredService<IAccesoUsuarioLJ>();
        var identidad = contexto.Context.User;
        var usuarioValido = int.TryParse(identidad?.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId);
        var sesionValida = Guid.TryParse(identidad?.FindFirstValue("sid"), out var sesionId);
        if (!usuarioValido || !sesionValida || !acceso.SesionVigente(sesionId, usuarioId))
        {
            contexto.Context.Abort();
            throw new HubException("La sesión del dispositivo ya no está vigente.");
        }

        return await siguiente(contexto);
    }
}
