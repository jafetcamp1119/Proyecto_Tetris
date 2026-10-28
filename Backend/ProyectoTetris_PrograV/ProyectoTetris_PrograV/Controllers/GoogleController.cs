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

// Recibe el ID token y el desafío; no acepta como identidad un correo enviado por el formulario.
public class TAccesoGoogle
{
    [Required, StringLength(10000)]
    public string IdToken
        {
            get;
            set;
        } = "";

    [Required, StringLength(64, MinimumLength = 64)]
    public string DesafioId
        {
            get;
            set;
        } = "";
}

// Recibe el nickname y el comprobante temporal de una identidad Google ya verificada.
public class TRegistroGoogle : TRegistroUsuario
{
    [Required, StringLength(64, MinimumLength = 64)]
    public string ComprobanteRegistro
        {
            get;
            set;
        } = "";
}

[ApiController, Route("api/google"), EnableRateLimiting("Acceso")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
// Coordina los desafíos, ingresos, registros y vínculos de Google mediante la API.
public class GoogleController(IValidadorGoogle validador, DesafiosGoogle desafios,
    IAccesoUsuarioLJ acceso, PublicadorSesion publicadorSesion, RegistrosGoogle registros) : ControllerBase
{
    private int UsuarioId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("desafio")]
    // Entrega un desafío temporal para el ingreso Google.
    public IActionResult DesafioIngreso()
    {
        return Ok(Respuesta<object>.Ok(desafios.Crear(null)));
    }

    [Authorize, HttpPost("desafio-vincular")]
    // Entrega un desafío asociado al usuario que desea vincular Google.
    public IActionResult DesafioVinculacion()
    {
        return Ok(Respuesta<object>.Ok(desafios.Crear(UsuarioId)));
    }

    [HttpPost("ingresar")]
    // Valida Google y devuelve una sesión o un comprobante para completar el registro.
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
            if (respuesta.CodigoError == "GoogleSinVincular")
            {
                return Ok(Respuesta<object>.Ok(registros.Crear(identidad.Subject, AvatarSeguro(identidad.Picture))));
            }

            return Conflict(respuesta);
        }

        var sesion = respuesta.Data!;
        var autenticacion = await publicadorSesion.CrearAsync(sesion);

        if (autenticacion == null)
        {
            return Conflict(
                Respuesta<object>.Fail("La cuenta cambio durante el ingreso. Reintenta.", "SesionCambioConcurrente")
            );
        }

        return Ok(Respuesta<TAutenticacion>.Ok(autenticacion));
    }

    [HttpPost("registrar")]
    // Completa el registro usando el comprobante generado después de verificar Google.
    public async Task<IActionResult> Registrar(TRegistroGoogle datos)
    {
        var respuesta = registros.Completar(datos.ComprobanteRegistro, datos, acceso);

        if (!respuesta.Success)
        {
            return Conflict(respuesta);
        }

        var autenticacion = await publicadorSesion.CrearAsync(respuesta.Data!);

        if (autenticacion == null)
        {
            return Conflict(
                Respuesta<object>.Fail("La cuenta cambio durante el ingreso. Reintenta.", "SesionCambioConcurrente")
            );
        }

        return Ok(Respuesta<TAutenticacion>.Ok(autenticacion));
    }

    // Conserva únicamente una URL HTTPS de avatar dentro de la longitud admitida.
    private static string? AvatarSeguro(string? avatar)
    {
        return Uri.TryCreate(avatar, UriKind.Absolute, out var url) && url.Scheme == "https"
            && avatar!.Length <= 2048
            ? avatar
            : null;
    }

    [Authorize, HttpPost("vincular")]
    // Relaciona el sub verificado con la cuenta autenticada sin fusionar perfiles.
    public async Task<IActionResult> Vincular(TAccesoGoogle datos)
    {
        var identidad = await ValidarAsync(datos, UsuarioId);

        if (identidad == null)
        {
            return Unauthorized(Respuesta<object>.Fail("No se pudo verificar Google. El perfil no cambió.", "GoogleInvalido"));
        }

        var avatar = AvatarSeguro(identidad.Picture);
        var respuesta = acceso.VincularGoogle(UsuarioId, identidad.Subject, avatar, Guid.Parse(User.FindFirstValue("sid")!));
        return respuesta.Success ? Ok(respuesta) : Conflict(respuesta);
    }

    // Valida el token oficial y consume el desafío que corresponde a este intento.
    private async Task<GoogleJsonWebSignature.Payload?> ValidarAsync(TAccesoGoogle datos, int? usuarioId)
    {
        try
        {
            var identidad = await validador.ValidarAsync(datos.IdToken);

            if (string.IsNullOrWhiteSpace(identidad.Subject)
                || !desafios.Consumir(datos.DesafioId, identidad.Nonce, usuarioId))
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
