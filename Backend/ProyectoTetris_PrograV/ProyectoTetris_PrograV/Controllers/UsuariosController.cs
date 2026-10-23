using System.Security.Claims;
using Dominio.EntidadesTipadas;
using Dominio.InterfazLJ;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoTetris_PrograV.Seguridad;
using Utilitarios;

namespace ProyectoTetris_PrograV.Controllers;

[ApiController]
[Route("api/usuarios")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class UsuariosController : ControllerBase
{
    private readonly IAccesoUsuarioLJ acceso;
    private readonly IUsuarioLJ usuarios;
    private readonly ServicioToken tokens;

    public UsuariosController(IAccesoUsuarioLJ acceso, IUsuarioLJ usuarios, ServicioToken tokens)
    {
        this.acceso = acceso;
        this.usuarios = usuarios;
        this.tokens = tokens;
    }

    [HttpPost("registrar")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Acceso")]
    public IActionResult Registrar(TRegistroUsuario datos)
    {
        var respuesta = acceso.Registrar(datos);
        if (respuesta.CodigoError == "NombreOcupado")
        {
            return Conflict(respuesta);
        }

        return ResponderSesion(respuesta, StatusCodes.Status201Created);
    }

    [HttpGet("disponibilidad")]
    public IActionResult Disponibilidad([FromQuery] string nombreUsuario)
    {
        var respuesta = acceso.NombreDisponible(nombreUsuario);
        if (!respuesta.Success)
        {
            return BadRequest(respuesta);
        }

        return Ok(respuesta);
    }

    [HttpPost("recuperar")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Acceso")]
    [HttpPost("renovar")]
    public IActionResult Recuperar(TRecuperarSesion datos)
    {
        return ResponderSesion(acceso.Recuperar(datos), StatusCodes.Status200OK);
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sesionId = Guid.Parse(User.FindFirstValue("sid")!);
        acceso.Revocar(sesionId, usuarioId);
        return NoContent();
    }

    [Authorize]
    [HttpGet("yo")]
    public IActionResult Yo()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var respuesta = usuarios.Buscar(new TUsuario { UsuarioId = usuarioId });
        if (!respuesta.Success)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, respuesta);
        }

        return Ok(respuesta);
    }

    [Authorize, HttpPost("codigo-recuperacion")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Acceso")]
    public IActionResult GenerarCodigo()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return Ok(acceso.GenerarRecuperacion(usuarioId));
    }

    [HttpPost("recuperar-codigo")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Acceso")]
    public IActionResult RecuperarCodigo(TRecuperarSesion datos)
    {
        try
        {
            return ResponderSesion(acceso.RecuperarConCodigo(datos.Credencial), StatusCodes.Status200OK);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Unauthorized(Respuesta<object>.Fail("El código ya se utilizó o fue reemplazado.", "CredencialInvalida"));
        }
    }

    private IActionResult ResponderSesion(Respuesta<TSesionJugador> respuesta, int estadoExitoso)
    {
        if (!respuesta.Success)
        {
            if (respuesta.CodigoError == "CredencialInvalida")
            {
                return Unauthorized(respuesta);
            }

            return BadRequest(respuesta);
        }

        var sesion = respuesta.Data!;
        var autenticacion = tokens.Crear(sesion.Usuario, sesion.SesionId);
        autenticacion.Credencial = sesion.Credencial;
        autenticacion.CredencialExpiraEnUtc = sesion.CredencialExpiraEnUtc;
        return StatusCode(estadoExitoso, Respuesta<TAutenticacion>.Ok(autenticacion));
    }
}
