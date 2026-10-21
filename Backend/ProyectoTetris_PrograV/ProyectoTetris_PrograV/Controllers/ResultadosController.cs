using System.Security.Claims;
using Dominio.InterfazLJ;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProyectoTetris_PrograV.Controllers;

[Authorize, ApiController, Route("api/resultados")]
public class ResultadosController : ControllerBase
{
    private readonly IResultadoLJ resultados;

    public ResultadosController(IResultadoLJ resultados)
    {
        this.resultados = resultados;
    }

    [HttpGet("mios")]
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
