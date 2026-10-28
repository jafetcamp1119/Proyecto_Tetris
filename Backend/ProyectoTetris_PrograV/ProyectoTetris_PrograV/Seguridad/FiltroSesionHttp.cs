using System.Security.Claims;
using Dominio.InterfazLJ;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProyectoTetris_PrograV.Controllers;
using Utilitarios;

namespace ProyectoTetris_PrograV.Seguridad;

public sealed class FiltroSesionHttp(IAccesoUsuarioLJ acceso) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate siguiente)
    {
        // Las operaciones de cuentas sincronizan su propia transaccion en AccesoUsuarioLJ.
        if (contexto.HttpContext.User.Identity?.IsAuthenticated != true || contexto.Controller is UsuariosController or GoogleController)
        {
            await siguiente();
            return;
        }
        var usuarioId = int.Parse(contexto.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sesionId = Guid.Parse(contexto.HttpContext.User.FindFirstValue("sid")!);
        using var exclusion = await ExclusionSesiones.TomarAsync(usuarioId, contexto.HttpContext.RequestAborted);
        var error = acceso.ErrorSesion(sesionId, usuarioId);
        if (error != null)
        {
            contexto.Result = new UnauthorizedObjectResult(Respuesta<object>.Fail("La sesion ya no esta activa.", error));
            return;
        }
        await siguiente();
    }
}
