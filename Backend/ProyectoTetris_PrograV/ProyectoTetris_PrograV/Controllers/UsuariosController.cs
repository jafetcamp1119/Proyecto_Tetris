using System.Security.Claims;

using Dominio.EntidadesTipadas;
using Dominio.InterfazLJ;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ProyectoTetris_PrograV.Seguridad;

using Utilitarios;

namespace ProyectoTetris_PrograV.Controllers;

// Recibe las solicitudes HTTP de cuentas y devuelve datos públicos de autenticación.
// La identidad de las operaciones protegidas se obtiene del JWT.
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
    // Solicita el registro y devuelve 409 si el nickname ya existe.
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
    // Consulta si el nickname puede usarse para una cuenta nueva.
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
    // Ingresa con una credencial recordada y reemplaza la sesión anterior.
    public async Task<IActionResult> Recuperar(TRecuperarSesion datos)
    {
        return await ResponderSesion(acceso.Recuperar(datos), StatusCodes.Status200OK);
    }

    [HttpPost("renovar")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Acceso")]
    // Renueva una sesión existente sin tomar el control de otro ingreso.
    public async Task<IActionResult> Renovar(TRecuperarSesion datos)
    {
        return await ResponderSesion(acceso.Renovar(datos), StatusCodes.Status200OK);
    }

    [Authorize]
    [HttpPost("logout")]
    // Revoca la sesión del JWT y cierra únicamente sus conexiones.
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
    // Consulta el usuario identificado por el JWT.
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
    // Genera otro código privado desde la sesión autorizada.
    public IActionResult GenerarCodigo()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var respuesta = acceso.GenerarRecuperacion(usuarioId, Guid.Parse(User.FindFirstValue("sid")!));
        return respuesta.Success ? Ok(respuesta) : Unauthorized(respuesta);
    }

    [Authorize, HttpPost("confirmar-codigo-recuperacion")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Acceso")]
    // Activa el código pendiente cuando el jugador confirma que ya lo guardó.
    public IActionResult ConfirmarCodigo(TRecuperarCodigo datos)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var respuesta = acceso.ConfirmarRecuperacion(usuarioId, Guid.Parse(User.FindFirstValue("sid")!), datos.Credencial);

        if (respuesta.CodigoError == "CredencialInvalida")
        {
            return Unauthorized(respuesta);
        }

        return respuesta.Success ? Ok(respuesta) : BadRequest(respuesta);
    }

    [HttpPost("recuperar-codigo")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("Recuperacion")]
    // Recupera la cuenta con el código e informa si fue reemplazado durante la operación.
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

    // Convierte el resultado de acceso en una respuesta HTTP y pide publicar la nueva sesión.
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

        if (autenticacion == null)
        {
            return Conflict(
                Respuesta<object>.Fail("La cuenta cambio durante el ingreso. Reintenta.", "SesionCambioConcurrente")
            );
        }

        return StatusCode(estadoExitoso, Respuesta<TAutenticacion>.Ok(autenticacion));
    }
}
