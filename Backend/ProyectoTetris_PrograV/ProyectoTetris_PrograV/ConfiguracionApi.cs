using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ProyectoTetris_PrograV.Controllers;
using Utilitarios;
using Microsoft.AspNetCore.SignalR;
using ProyectoTetris_PrograV.Seguridad;

namespace ProyectoTetris_PrograV;

public static class ConfiguracionApi
{
    public static IServiceCollection AgregarApiTetris(this IServiceCollection servicios)
    {
        servicios.AddSingleton<DesafiosGoogle>();
        servicios.AddSingleton<RegistrosGoogle>();
        servicios.AddSingleton<ConexionesSesion>();
        servicios.AddScoped<PublicadorSesion>();
        servicios.AddScoped<IValidadorGoogle, ValidadorGoogle>();
        servicios.AddRateLimiter(opciones =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opciones.AddPolicy("Recuperacion", contexto =>
                System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    contexto.Connection.RemoteIpAddress?.ToString() ?? "local",
                    _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            opciones.AddPolicy("GenerarCodigo", contexto =>
                System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    contexto.User.FindFirst("sub")?.Value ?? "anonimo",
                    _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    { PermitLimit = 3, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
            opciones.AddPolicy("CrearSala", contexto =>
                System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    contexto.User.FindFirst("sub")?.Value ?? "anonimo",
                    _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            opciones.AddPolicy("Acceso", contexto =>
                System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    contexto.Connection.RemoteIpAddress?.ToString() ?? "local",
                    _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 15,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });
        servicios.AddControllers(opciones =>
        {
            opciones.Filters.Add<FiltroConcurrenciaSesion>();
            opciones.Filters.Add<FiltroSesionHttp>();
        })
            .AddApplicationPart(typeof(UsuariosController).Assembly)
            .AddJsonOptions(opciones =>
            {
                opciones.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        servicios.Configure<ApiBehaviorOptions>(opciones =>
        {
            opciones.InvalidModelStateResponseFactory = contexto =>
            {
                var errores = new Dictionary<string, string[]>();

                foreach (var campo in contexto.ModelState)
                {
                    if (campo.Value == null || campo.Value.Errors.Count == 0)
                    {
                        continue;
                    }

                    errores[campo.Key] = campo.Value.Errors
                        .Select(error =>
                        {
                            if (string.IsNullOrWhiteSpace(error.ErrorMessage))
                            {
                                return "Valor inválido.";
                            }

                            return error.ErrorMessage;
                        })
                        .ToArray();
                }

                var respuesta = Respuesta<Dictionary<string, string[]>>.Fail("Revise los datos enviados.");
                respuesta.Data = errores;
                return new BadRequestObjectResult(respuesta);
            };
        });

        servicios.AddSignalR(opciones =>
        {
            opciones.AddFilter<FiltroSesionJuego>();
        }).AddJsonProtocol(opciones =>
        {
            opciones.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        return servicios;
    }
}

