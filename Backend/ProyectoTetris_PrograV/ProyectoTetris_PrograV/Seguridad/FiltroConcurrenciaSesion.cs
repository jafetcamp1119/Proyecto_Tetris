using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Utilitarios;

namespace ProyectoTetris_PrograV.Seguridad;

public sealed class FiltroConcurrenciaSesion : IExceptionFilter
{
    public void OnException(ExceptionContext contexto)
    {
        if (contexto.Exception is not DbUpdateConcurrencyException) { return; }
        contexto.Result = new ConflictObjectResult(Respuesta<object>.Fail(
            "La cuenta cambio durante esta operacion. Volve a intentarlo.", "SesionCambioConcurrente"));
        contexto.ExceptionHandled = true;
    }
}
