using System.Security.Claims;
using Dominio.EntidadesTipadas;
using Dominio.InterfazLJ;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoTetris_PrograV.Seguridad;
using Utilitarios;

namespace ProyectoTetris_PrograV.Controllers;

[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly IAccesoUsuarioLJ _acceso;
    private readonly IUsuarioLJ _usuarios;
    private readonly ServicioToken _tokens;

    public UsuariosController(IAccesoUsuarioLJ acceso, IUsuarioLJ usuarios, ServicioToken tokens)
    {
        _acceso = acceso;
        _usuarios = usuarios;
        _tokens = tokens;
    }

    [HttpPost("registrar")]
    public IActionResult Registrar(TRegistroUsuario datos)
    {
        var respuesta = _acceso.Registrar(datos);
        if (!respuesta.Success)
        {
            return BadRequest(respuesta);
        }

        return Ok(respuesta);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(TLoginUsuario datos)
    {
        var respuesta = _acceso.Autenticar(datos);
        if (!respuesta.Success)
        {
            return Unauthorized(respuesta);
        }

        var usuario = respuesta.Data!;
        var autenticacion = _tokens.Crear(usuario);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
            new Claim(ClaimTypes.Name, usuario.NombreUsuario)
        };
        var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        // Conserva la sesión de los clientes C# anteriores; Ionic utiliza el token.
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identidad));
        return Ok(Respuesta<TAutenticacion>.Ok(autenticacion));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [Authorize]
    [HttpGet("yo")]
    public IActionResult Yo()
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var respuesta = _usuarios.Buscar(new TUsuario { UsuarioId = usuarioId });
        if (!respuesta.Success)
        {
            return BadRequest(respuesta);
        }

        return Ok(respuesta);
    }
}
