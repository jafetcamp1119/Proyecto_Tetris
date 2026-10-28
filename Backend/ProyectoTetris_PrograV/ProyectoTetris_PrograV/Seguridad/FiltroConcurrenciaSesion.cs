using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

using Utilitarios;

namespace ProyectoTetris_PrograV.Seguridad;

// Convierte un cambio simultáneo de sesión en una respuesta de conflicto controlada.
public sealed class FiltroConcurrenciaSesion : IExceptionFilter
{
    // Traduce el conflicto de concurrencia de EF a una respuesta HTTP sin detalles internos.
    public void OnException(ExceptionContext contexto)
    {
        if (contexto.Exception is not DbUpdateConcurrencyException)
        {
            return;
        }

        contexto.Result = new ConflictObjectResult(Respuesta<object>.Fail(
            "La cuenta cambio durante esta operacion. Volve a intentarlo.", "SesionCambioConcurrente"));
        contexto.ExceptionHandled = true;
    }
}
