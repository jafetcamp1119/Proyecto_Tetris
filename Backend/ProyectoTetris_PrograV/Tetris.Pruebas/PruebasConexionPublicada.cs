// Comprueba seguridad de producción con un servidor local, sin abrir Cloudflare ni SQL.
using System.Net;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ProyectoTetris_PrograV;
using ProyectoTetris_PrograV.Seguridad;

internal static class PruebasConexionPublicada
{
    public static async Task<int> EjecutarAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Clave"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ["Jwt:Emisor"] = "Pruebas",
            ["Jwt:Audiencia"] = "Pruebas",
            ["Cors:OrigenesPermitidos:0"] = "https://proyecto-tetris-512517.web.app",
            ["Cors:OrigenesPermitidos:1"] = "https://localhost",
            ["Cors:PermitirRedLocal"] = "false"
        });
        builder.Services.AgregarApiTetris();
        builder.Services.AgregarSeguridad(builder.Configuration, builder.Environment);
        builder.Services.AgregarConexionPublicada();
        await using var app = builder.Build();
        app.UsarConexionPublicada(false);
        app.UseCors(ConfiguracionSeguridad.PoliticaCors);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapGet("/privado", () => "OK").RequireAuthorization();
        app.MapGet("/proxy", (Microsoft.AspNetCore.Http.HttpContext c) => c.Connection.RemoteIpAddress!.ToString());
        app.MapGet("/hubs/juego", () => "OK");
        app.MapGet("/acceso", () => "OK").RequireRateLimiting("Acceso");
        var total = 0;
        void Verificar(bool condicion, string nombre)
        {
            if (!condicion)
            {
                throw new Exception(nombre);
            }

            Console.WriteLine("OK PUBLICACIÓN: " + nombre);
            total++;
        }

        try
        {
            await app.StartAsync();
            using var cliente = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            using var http = await cliente.GetAsync("/privado");
            Verificar(http.StatusCode == HttpStatusCode.BadRequest, "producción rechaza HTTP");
            async Task<HttpResponseMessage> Solicitar(string ruta, string? origen = null, bool preflight = false, string ip = "203.0.113.10")
            {
                using var solicitud = new HttpRequestMessage(preflight ? HttpMethod.Options : HttpMethod.Get, ruta);
                solicitud.Headers.Add("X-Forwarded-Proto", "https");
                solicitud.Headers.Add("X-Forwarded-For", ip);

                if (origen != null)
                {
                    solicitud.Headers.Add("Origin", origen);
                }

                if (preflight)
                {
                    solicitud.Headers.Add("Access-Control-Request-Method", "GET");
                }

                return await cliente.SendAsync(solicitud);
            }
            using var proxy = await Solicitar("/proxy");
            Verificar(await proxy.Content.ReadAsStringAsync() == "203.0.113.10", "proxy loopback transmite HTTPS e IP");
            using var privado = await Solicitar("/privado");
            Verificar(privado.StatusCode == HttpStatusCode.Unauthorized, "HTTPS conserva JWT obligatorio");

            foreach (var origen in new[] { "https://proyecto-tetris-512517.web.app", "https://localhost" })
            {
                using var permitido = await Solicitar("/privado", origen, true);
                Verificar(
                    permitido.StatusCode == HttpStatusCode.NoContent
                    && permitido.Headers.Contains("Access-Control-Allow-Origin"),
                    "CORS permite " + origen
                );
            }

            foreach (var origen in new[] { "http://localhost:8100", "http://192.168.1.29:8100", "https://otro.example" })
            {
                using var denegado = await Solicitar("/privado", origen, true);
                Verificar(!denegado.Headers.Contains("Access-Control-Allow-Origin"), "CORS excluye " + origen);
                using var hub = await Solicitar("/hubs/juego", origen);
                Verificar(hub.StatusCode == HttpStatusCode.Forbidden, "handshake excluye " + origen);
            }

            for (var i = 0; i < 15; i++)
            {
                using var permitido = await Solicitar("/acceso");
                Verificar(permitido.IsSuccessStatusCode, "intento permitido " + (i + 1));
            }

            using var limitado = await Solicitar("/acceso");
            Verificar(limitado.StatusCode == HttpStatusCode.TooManyRequests, "intento 16 limitado");
            using var otraIp = await Solicitar("/acceso", ip: "203.0.113.11");
            Verificar(otraIp.IsSuccessStatusCode, "otra IP conserva cuota detrás del proxy");

            foreach (var origen in new[] { "http://localhost", "https://192.168.1.29", "https://permitido.example/ruta" })
            {
                var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Clave"] = new string('x', 64),
                    ["Jwt:Emisor"] = "Pruebas",
                    ["Jwt:Audiencia"] = "Pruebas",
                    ["Cors:OrigenesPermitidos:0"] = origen
                }).Build();
                var rechazo = false;

                try
                {
                    new ServiceCollection().AgregarSeguridad(configuracion, builder.Environment);
                }
                catch (InvalidOperationException)
                {
                    rechazo = true;
                }

                Verificar(rechazo, "inicio rechaza origen inseguro " + origen);
            }

            Console.WriteLine($"{total} comprobaciones de publicación correctas; sin SQL ni Internet.");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine("ERROR PUBLICACIÓN: " + e.Message);
            return 1;
        }
    }
}
