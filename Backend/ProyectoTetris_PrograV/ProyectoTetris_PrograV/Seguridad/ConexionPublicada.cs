using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;

namespace ProyectoTetris_PrograV.Seguridad;

// Configura HTTPS y los encabezados del proxy que conecta Cloudflare con la API local.
public static class ConexionPublicada
{
    // Configura los proxies de confianza que pueden informar el esquema HTTPS original.
    public static IServiceCollection AgregarConexionPublicada(this IServiceCollection servicios)
    {
        servicios.Configure<ForwardedHeadersOptions>(opciones =>
        {
            // Conservar los proxies conocidos por defecto: únicamente loopback.
            // cloudflared debe ejecutarse en la misma computadora que Kestrel.
            opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            opciones.ForwardLimit = 1;
        });
        return servicios;
    }

    // Aplica los encabezados del proxy y exige HTTPS en producción salvo la ruta de salud.
    public static IApplicationBuilder UsarConexionPublicada(this IApplicationBuilder app, bool desarrollo)
    {
        app.UseForwardedHeaders();
        app.Use(async (contexto, siguiente) =>
        {
            contexto.Response.Headers.XContentTypeOptions = "nosniff";
            contexto.Response.Headers.CacheControl = "no-store";
            // Solo salud local puede consultarse sin HTTPS en producción.
            if (!desarrollo && !contexto.Request.IsHttps && contexto.Request.Path != "/salud")
            {
                contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            // CORS no protege el handshake de WebSocket: validar también su Origin.
            if (contexto.Request.Path.StartsWithSegments("/hubs/juego") &&
                contexto.Request.Headers.TryGetValue("Origin", out var origen))
            {
                var proveedor = contexto.RequestServices.GetRequiredService<ICorsPolicyProvider>();
                var politica = await proveedor.GetPolicyAsync(contexto, ConfiguracionSeguridad.PoliticaCors);

                if (origen.Count != 1 || politica == null || !politica.IsOriginAllowed(origen.ToString()))
                {
                    contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            await siguiente();
        });
        return app;
    }
}
