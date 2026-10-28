using System.Security.Claims;

using Dominio.EntidadesTipadas;
using Dominio.InterfazLJ;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProyectoTetris_PrograV.Controllers;

// Consulta y guarda las preferencias del usuario identificado por su JWT.
[Authorize, ApiController, Route("api/configuracion")]
public class ConfiguracionController : ControllerBase
{
    private readonly IConfiguracionLJ configuracion;

    public ConfiguracionController(IConfiguracionLJ configuracion)
    {
        this.configuracion = configuracion;
    }

    private int UsuarioId
    {
        get
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }

    [HttpGet]
    // Consulta las preferencias del usuario indicado por el JWT.
    public IActionResult Obtener()
    {
        var respuesta = configuracion.ObtenerPorUsuario(UsuarioId);

        if (respuesta.Success)
        {
            return Ok(respuesta);
        }

        return BadRequest(respuesta);
    }

    [HttpPut]
    // Guarda preferencias usando la identidad del servidor y no la del formulario.
    public IActionResult Guardar(TConfiguracion datos)
    {
        var respuesta = configuracion.GuardarPorUsuario(UsuarioId, datos);

        if (respuesta.Success)
        {
            return Ok(respuesta);
        }

        return BadRequest(respuesta);
    }
}
