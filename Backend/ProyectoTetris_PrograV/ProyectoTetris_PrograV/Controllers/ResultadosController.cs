using System.Security.Claims;

using Dominio.InterfazLJ;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProyectoTetris_PrograV.Controllers;

// Permite consultar el historial propio y el ranking, sin aceptar puntuaciones del cliente.
[Authorize, ApiController, Route("api/resultados")]
public class ResultadosController : ControllerBase
{
    private readonly IResultadoLJ resultados;

    public ResultadosController(IResultadoLJ resultados)
    {
        this.resultados = resultados;
    }

    [HttpGet("mios")]
    // Consulta el historial del usuario autenticado.
    public IActionResult Mios()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var respuesta = resultados.ObtenerPorUsuario(id);

        if (respuesta.Success)
        {
            return Ok(respuesta);
        }

        return BadRequest(respuesta);
    }

    [HttpGet("ranking")]
    // Consulta el ranking calculado desde estadísticas persistidas.
    public IActionResult Ranking()
    {
        var respuesta = resultados.Ranking();

        if (respuesta.Success)
        {
            return Ok(respuesta);
        }

        return BadRequest(respuesta);
    }
}
