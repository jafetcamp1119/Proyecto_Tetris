using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Dominio.InterfazLJ;

using LogicaJuego.Implementacions;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using Utilitarios;

namespace ProyectoTetris_PrograV.Seguridad;

// Configura los orígenes permitidos y la validación de los JWT.
// Además de la firma, comprueba que la sesión siga activa en la cuenta.
public static class ConfiguracionSeguridad
{
    public const string PoliticaCors = "Ionic";
    public const string Esquema = "Tetris";

    // Los orígenes son los de la app Ionic. Con Cors:PermitirRedLocal también se aceptan páginas
    // servidas desde una IPv4 privada (por ejemplo ionic serve abierto en el navegador de un celular)
    private static void AgregarCors(IServiceCollection servicios, IConfiguration configuracion, IHostEnvironment entorno)
    {
        var origenes = configuracion.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];
        var permitirRedLocal = entorno.IsDevelopment() && configuracion.GetValue<bool>("Cors:PermitirRedLocal");

        if (!entorno.IsDevelopment() && (configuracion.GetValue<bool>("Cors:PermitirRedLocal") ||
            origenes.Length == 0 || origenes.Any(origen => !Uri.TryCreate(origen, UriKind.Absolute, out var uri) ||
                uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.GetLeftPart(UriPartial.Authority) != origen ||
                (uri.IsLoopback && origen != "https://localhost") ||
                (IPAddress.TryParse(uri.Host, out _) && origen != "https://localhost"))))
        {
            throw new InvalidOperationException(
                "Producción requiere orígenes CORS HTTPS explícitos. https://localhost corresponde a Capacitor Android."
            );
        }

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

    // Acepta un origen configurado o una dirección de red local habilitada en desarrollo.
    private static bool EsOrigenPermitido(string origen, string[] origenes, bool permitirRedLocal)
    {
        return origenes.Contains(origen) || EsOrigenDeRedLocal(origen, permitirRedLocal);
    }

    // Comprueba que el origen sea HTTP o HTTPS y pertenezca a un rango IPv4 privado.
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

        return (direccion.Scheme == "http" || direccion.Scheme == "https") && (esClaseA || esClaseB || esClaseC);
    }

    // Lee la configuración privada y registra JWT, recuperación, CORS y autorización.
    public static IServiceCollection AgregarSeguridad(
        this IServiceCollection servicios,
        IConfiguration configuracion,
        IHostEnvironment entorno)
    {
        var opciones = LeerOpcionesJwt(configuracion, entorno);

        servicios.Configure<OpcionesGoogle>(configuracion.GetSection(OpcionesGoogle.Seccion));
        servicios.AddSingleton(opciones);
        var claveRecuperacion = configuracion["Recuperacion:Clave"];
        servicios.AddSingleton(
            new ProtectorCodigosRecuperacion(string.IsNullOrWhiteSpace(claveRecuperacion) ? opciones.Clave : claveRecuperacion)
        );
        servicios.AddSingleton<ServicioToken>();
        AgregarCors(servicios, configuracion, entorno);

        servicios
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt => ConfigurarJwt(jwt, opciones));

        servicios.AddAuthorization();
        return servicios;
    }

    // Comprueba clave, emisor, audiencia y duración.
    // En desarrollo puede crear una clave temporal que cambia al reiniciar.
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

    // Exige firma HS256, emisor, audiencia y vencimiento; no agrega tolerancia al reloj.
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

    // Comprueba usuario y sesión en el servidor antes de autorizar el JWT recibido.
    private static Task ValidarIdentidadDelToken(TokenValidatedContext contexto)
    {
        var identificador = contexto.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(identificador, out var usuarioId) || usuarioId <= 0)
        {
            contexto.Fail("El token no contiene una identidad válida.");
            return Task.CompletedTask;
        }

        var sesion = contexto.Principal?.FindFirstValue("sid");
        var acceso = contexto.HttpContext.RequestServices.GetRequiredService<IAccesoUsuarioLJ>();
        var errorSesion = Guid.TryParse(sesion, out var sesionId) ? acceso.ErrorSesion(sesionId, usuarioId) : "SesionInvalida";

        if (errorSesion != null)
        {
            contexto.HttpContext.Items["ErrorSesionTetris"] = errorSesion;
            contexto.Fail("La sesión del dispositivo ya no está vigente.");
            return Task.CompletedTask;
        }

        var identidad = (ClaimsIdentity)contexto.Principal!.Identity!;
        identidad.AddClaim(new Claim(ClaimTypes.NameIdentifier, identificador!));
        return Task.CompletedTask;
    }

    // Devuelve HTTP 401 con un código que permite distinguir vencimiento y reemplazo.
    private static async Task ResponderNoAutenticado(JwtBearerChallengeContext contexto)
    {
        contexto.HandleResponse();
        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
        contexto.Response.Headers.WWWAuthenticate = "Bearer";
        var vencida = contexto.AuthenticateFailure is SecurityTokenExpiredException;
        var mensaje = vencida ? "El token de acceso venció. Renová la sesión con la credencial del dispositivo."
            : "La sesión no es válida. Intentá recuperarla con la credencial del dispositivo.";
        var codigo = vencida
            ? "SesionVencida"
            : contexto.HttpContext.Items["ErrorSesionTetris"] as string ?? "SesionInvalida";
        await contexto.Response.WriteAsJsonAsync(Respuesta<object>.Fail(mensaje, codigo));
    }

    // Devuelve HTTP 403 cuando el usuario autenticado no tiene permiso.
    private static async Task ResponderSinPermiso(ForbiddenContext contexto)
    {
        contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
        await contexto.Response.WriteAsJsonAsync(Respuesta<object>.Fail("No tiene permiso para esta acción."));
    }

}
