using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Dominio.EntidadesTipadas;
using Dominio.InterfazLJ;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProyectoTetris_PrograV.Seguridad;
using Utilitarios;

namespace ProyectoTetris_PrograV.Controllers;

public class TAccesoGoogle
{
    [Required, StringLength(10000)]
    public string IdToken { get; set; } = "";
    [Required, StringLength(64, MinimumLength = 64)]
    public string DesafioId { get; set; } = "";
}

[ApiController, Route("api/google"), EnableRateLimiting("Acceso")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class GoogleController(IValidadorGoogle validador, DesafiosGoogle desafios,
    IAccesoUsuarioLJ acceso, ServicioToken tokens) : ControllerBase
{
    private int UsuarioId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("desafio")]
    public IActionResult DesafioIngreso()
    {
        return Ok(Respuesta<object>.Ok(desafios.Crear(null)));
    }

    [Authorize, HttpPost("desafio-vincular")]
    public IActionResult DesafioVinculacion()
    {
        return Ok(Respuesta<object>.Ok(desafios.Crear(UsuarioId)));
    }

    [HttpPost("ingresar")]
    public async Task<IActionResult> Ingresar(TAccesoGoogle datos)
    {
        var identidad = await ValidarAsync(datos, null);
        if (identidad == null)
        {
            return Unauthorized(Respuesta<object>.Fail("Google no pudo verificarse. Volvé a intentarlo.", "GoogleInvalido"));
        }

        var respuesta = acceso.IngresarGoogle(identidad.Subject);
        if (!respuesta.Success)
        {
            return Conflict(respuesta);
        }

        var sesion = respuesta.Data!;
        var autenticacion = tokens.Crear(sesion.Usuario, sesion.SesionId);
        autenticacion.Credencial = sesion.Credencial;
        autenticacion.CredencialExpiraEnUtc = sesion.CredencialExpiraEnUtc;
        return Ok(Respuesta<TAutenticacion>.Ok(autenticacion));
    }

    [Authorize, HttpPost("vincular")]
    public async Task<IActionResult> Vincular(TAccesoGoogle datos)
    {
        var identidad = await ValidarAsync(datos, UsuarioId);
        if (identidad == null)
        {
            return Unauthorized(Respuesta<object>.Fail("No se pudo verificar Google. El perfil no cambió.", "GoogleInvalido"));
        }

        var avatar = Uri.TryCreate(identidad.Picture, UriKind.Absolute, out var url)
            && url.Scheme == "https" && identidad.Picture.Length <= 2048 ? identidad.Picture : null;
        var respuesta = acceso.VincularGoogle(UsuarioId, identidad.Subject, avatar);
        return respuesta.Success ? Ok(respuesta) : Conflict(respuesta);
    }

    private async Task<GoogleJsonWebSignature.Payload?> ValidarAsync(TAccesoGoogle datos, int? usuarioId)
    {
        try
        {
            var identidad = await validador.ValidarAsync(datos.IdToken);
            if (string.IsNullOrWhiteSpace(identidad.Subject) || !desafios.Consumir(datos.DesafioId, identidad.Nonce, usuarioId))
            {
                return null;
            }

            return identidad;
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
