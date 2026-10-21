using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Utilitarios;

namespace ProyectoTetris_PrograV.Seguridad;

public static class ConfiguracionSeguridad
{
    public const string PoliticaCors = "Ionic";
    public const string Esquema = "Tetris";

    // Los orígenes son los de la app Ionic. Con Cors:PermitirRedLocal también se aceptan páginas
    // servidas desde una IPv4 privada (por ejemplo ionic serve abierto en el navegador de un celular).
    private static void AgregarCors(IServiceCollection servicios, IConfiguration configuracion)
    {
        var origenes = configuracion.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];
        var permitirRedLocal = configuracion.GetValue<bool>("Cors:PermitirRedLocal");

        servicios.AddCors(cors =>
        {
            cors.AddPolicy(PoliticaCors, politica =>
            {
                if (origenes.Length == 0 && !permitirRedLocal)
                {
                    return;
                }

                politica
                    .SetIsOriginAllowed(origen => EsOrigenPermitido(origen, origenes, permitirRedLocal))
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    private static bool EsOrigenPermitido(string origen, string[] origenes, bool permitirRedLocal)
    {
        return origenes.Contains(origen) || EsOrigenDeRedLocal(origen, permitirRedLocal);
    }

    private static bool EsOrigenDeRedLocal(string origen, bool permitirRedLocal)
    {
        if (!permitirRedLocal || !Uri.TryCreate(origen, UriKind.Absolute, out var direccion))
        {
            return false;
        }

        if (!IPAddress.TryParse(direccion.Host, out var ip) || ip.GetAddressBytes().Length != 4)
        {
            return false;
        }

        var partes = ip.GetAddressBytes();
        var esClaseA = partes[0] == 10;
        var esClaseB = partes[0] == 172 && partes[1] >= 16 && partes[1] <= 31;
        var esClaseC = partes[0] == 192 && partes[1] == 168;

        return esClaseA || esClaseB || esClaseC;
    }

    public static IServiceCollection AgregarSeguridad(
        this IServiceCollection servicios,
        IConfiguration configuracion,
        IHostEnvironment entorno)
    {
        var opciones = LeerOpcionesJwt(configuracion, entorno);

        servicios.AddSingleton(opciones);
        servicios.AddSingleton<ServicioToken>();
        AgregarCors(servicios, configuracion);

        servicios
            .AddAuthentication(Esquema)
            .AddPolicyScheme(Esquema, Esquema, ConfigurarSelectorDeEsquema)
            .AddJwtBearer(jwt => ConfigurarJwt(jwt, opciones))
            .AddCookie(ConfigurarCookie);

        servicios.AddAuthorization();
        return servicios;
    }

    private static OpcionesJwt LeerOpcionesJwt(IConfiguration configuracion, IHostEnvironment entorno)
    {
        var opciones = configuracion.GetSection("Jwt").Get<OpcionesJwt>() ?? new OpcionesJwt();

        if (string.IsNullOrWhiteSpace(opciones.Clave) && entorno.IsDevelopment())
        {
            // Solo desarrollo: el secreto vive en memoria y cambia al reiniciar el servidor.
            opciones.Clave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        if (Encoding.UTF8.GetByteCount(opciones.Clave) < 32)
        {
            throw new InvalidOperationException(
                "Configure Jwt:Clave con al menos 32 bytes mediante secretos de usuario o una variable de entorno.");
        }

        var datosIncompletos = string.IsNullOrWhiteSpace(opciones.Emisor) ||
            string.IsNullOrWhiteSpace(opciones.Audiencia);
        var duracionInvalida = opciones.MinutosExpiracion < 1 || opciones.MinutosExpiracion > 480;

        if (datosIncompletos || duracionInvalida)
        {
            throw new InvalidOperationException("Revise el emisor, audiencia y duración de Jwt.");
        }

        return opciones;
    }

    // Elige JWT cuando la solicitud trae Authorization: Bearer (o access_token en el hub) y cookie en los demás casos.
    private static void ConfigurarSelectorDeEsquema(PolicySchemeOptions politica)
    {
        politica.ForwardDefaultSelector = contexto =>
        {
            var autorizacion = contexto.Request.Headers.Authorization.ToString();
            var esHub = contexto.Request.Path.StartsWithSegments("/hubs/juego");
            var tokenHub = esHub && !string.IsNullOrEmpty(contexto.Request.Query["access_token"]);
            var esBearer = autorizacion.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

            if (esBearer || tokenHub)
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }

            return CookieAuthenticationDefaults.AuthenticationScheme;
        };
    }

    private static void ConfigurarJwt(JwtBearerOptions jwt, OpcionesJwt opciones)
    {
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opciones.Emisor,
            ValidateAudience = true,
            ValidAudience = opciones.Audiencia,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.Clave)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.UniqueName
        };
        jwt.Events = new JwtBearerEvents
        {
            OnMessageReceived = LeerTokenDelHub,
            OnTokenValidated = ValidarIdentidadDelToken,
            OnChallenge = ResponderNoAutenticado,
            OnForbidden = ResponderSinPermiso
        };
    }

    // El navegador envía el token en la URL al abrir WebSocket o SSE.
    private static Task LeerTokenDelHub(MessageReceivedContext contexto)
    {
        var esHub = contexto.Request.Path.StartsWithSegments("/hubs/juego");

        if (esHub && string.IsNullOrEmpty(contexto.Request.Headers.Authorization))
        {
            contexto.Token = contexto.Request.Query["access_token"];
        }

        return Task.CompletedTask;
    }

    private static Task ValidarIdentidadDelToken(TokenValidatedContext contexto)
    {
        var identificador = contexto.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(identificador, out var usuarioId) || usuarioId <= 0)
        {
            contexto.Fail("El token no contiene una identidad válida.");
            return Task.CompletedTask;
        }

        var identidad = (ClaimsIdentity)contexto.Principal!.Identity!;
        identidad.AddClaim(new Claim(ClaimTypes.NameIdentifier, identificador!));
        return Task.CompletedTask;
    }

    private static async Task ResponderNoAutenticado(JwtBearerChallengeContext contexto)
    {
        contexto.HandleResponse();
        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
        contexto.Response.Headers.WWWAuthenticate = "Bearer";
        await contexto.Response.WriteAsJsonAsync(Respuesta<object>.Fail("Inicie sesión con un token válido."));
    }

    private static async Task ResponderSinPermiso(ForbiddenContext contexto)
    {
        contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
        await contexto.Response.WriteAsJsonAsync(Respuesta<object>.Fail("No tiene permiso para esta acción."));
    }

    private static void ConfigurarCookie(CookieAuthenticationOptions cookie)
    {
        cookie.Cookie.Name = "Tetris.Sesion";
        cookie.Cookie.HttpOnly = true;
        cookie.Cookie.SameSite = SameSiteMode.Strict;
        cookie.ExpireTimeSpan = TimeSpan.FromHours(8);
        cookie.Events.OnRedirectToLogin = async contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await contexto.Response.WriteAsJsonAsync(Respuesta<object>.Fail("Inicie sesión para continuar."));
        };
        cookie.Events.OnRedirectToAccessDenied = async contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            await contexto.Response.WriteAsJsonAsync(Respuesta<object>.Fail("No tiene permiso para esta acción."));
        };
    }
}
