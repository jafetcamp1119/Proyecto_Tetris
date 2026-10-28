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
    private readonly PublicadorSesion publicadorSesion;

    public UsuariosController(IAccesoUsuarioLJ acceso, IUsuarioLJ usuarios, PublicadorSesion publicadorSesion)
    {
        this.acceso = acceso;
        this.usuarios = usuarios;
        this.publicadorSesion = publicadorSesion;
    }

    [HttpPost("registrar")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Acceso")]
    public async Task<IActionResult> Registrar(TRegistroUsuario datos)
    {
        var respuesta = acceso.Registrar(datos);
        if (respuesta.CodigoError == "NombreOcupado")
        {
            return Conflict(respuesta);
        }

        return await ResponderSesion(respuesta, StatusCodes.Status201Created);
    }

    [HttpGet("disponibilidad")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Acceso")]
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
    public async Task<IActionResult> Recuperar(TRecuperarSesion datos)
    {
        return await ResponderSesion(acceso.Recuperar(datos), StatusCodes.Status200OK);
    }

    [HttpPost("renovar")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Acceso")]
    public async Task<IActionResult> Renovar(TRecuperarSesion datos)
    {
        return await ResponderSesion(acceso.Renovar(datos), StatusCodes.Status200OK);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromServices] ConexionesSesion conexiones)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sesionId = Guid.Parse(User.FindFirstValue("sid")!);
        acceso.Revocar(sesionId, usuarioId);
        await conexiones.CerrarSesionAsync(usuarioId, sesionId);
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
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("GenerarCodigo")]
    public IActionResult GenerarCodigo()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var respuesta = acceso.GenerarRecuperacion(usuarioId, Guid.Parse(User.FindFirstValue("sid")!));
        return respuesta.Success ? Ok(respuesta) : Unauthorized(respuesta);
    }

    [Authorize, HttpPost("confirmar-codigo-recuperacion")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Acceso")]
    public IActionResult ConfirmarCodigo(TRecuperarCodigo datos)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var respuesta = acceso.ConfirmarRecuperacion(usuarioId, Guid.Parse(User.FindFirstValue("sid")!), datos.Credencial);
        if (respuesta.CodigoError == "CredencialInvalida") { return Unauthorized(respuesta); }
        return respuesta.Success ? Ok(respuesta) : BadRequest(respuesta);
    }

    [HttpPost("recuperar-codigo")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Recuperacion")]
    public async Task<IActionResult> RecuperarCodigo(TRecuperarCodigo datos)
    {
        try
        {
            return await ResponderSesion(acceso.RecuperarConCodigo(datos.Credencial), StatusCodes.Status200OK);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Unauthorized(Respuesta<object>.Fail("El código ya se utilizó o fue reemplazado.", "CredencialInvalida"));
        }
    }

    private async Task<IActionResult> ResponderSesion(Respuesta<TSesionJugador> respuesta, int estadoExitoso)
    {
        if (!respuesta.Success)
        {
            if (respuesta.CodigoError is "CredencialInvalida" or "SesionInvalida" or "SesionReemplazada")
            {
                return Unauthorized(respuesta);
            }

            return BadRequest(respuesta);
        }

        var sesion = respuesta.Data!;
        var autenticacion = await publicadorSesion.CrearAsync(sesion);
        if (autenticacion == null) { return Conflict(Respuesta<object>.Fail("La cuenta cambio durante el ingreso. Reintenta.", "SesionCambioConcurrente")); }
        return StatusCode(estadoExitoso, Respuesta<TAutenticacion>.Ok(autenticacion));
    }
}
