using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ProyectoTetris_PrograV.Controllers;
using Utilitarios;

namespace ProyectoTetris_PrograV;

public static class ConfiguracionApi
{
    public static IServiceCollection AgregarApiTetris(this IServiceCollection servicios)
    {
        servicios.AddControllers()
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

        servicios.AddSignalR().AddJsonProtocol(opciones =>
        {
            opciones.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        return servicios;
    }
}

