// Agrega escenarios HTTP del acceso Google con validación simulada.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Dominio.EntidadesTipadas;

using Google.Apis.Auth;

using ProyectoTetris_PrograV.Controllers;
using ProyectoTetris_PrograV.Seguridad;

using Utilitarios;

internal static partial class PruebasHttp
{
    private static async Task VerificarRegistroGoogleAsync(Uri direccion, Action<bool, string> verificar)
    {
        using var cliente = new HttpClient { BaseAddress = direccion };
        cliente.DefaultRequestHeaders.Add("X-IP-Prueba", "127.0.1.180");
        var invitado = await DatosAsync<TAutenticacion>(
            await cliente.PostAsJsonAsync("/api/usuarios/registrar", new TRegistroUsuario { NombreUsuario = "InvitadoGoogleHttp" })
        );
        var desafio = await DatosAsync<JsonElement>(await cliente.PostAsync("/api/google/desafio", null));
        var pruebaGoogle = new TAccesoGoogle
        {
            DesafioId = desafio.GetProperty("id").GetString()!,
            IdToken = "identidad-prueba|google-nuevo|" + desafio.GetProperty("nonce").GetString()
        };
        var inicio = await DatosAsync<JsonElement>(await cliente.PostAsJsonAsync("/api/google/ingresar", pruebaGoogle));
        verificar(
            inicio.GetProperty("requiereRegistro").GetBoolean(),
            "Google HTTP desconocido inicia registro sin error"
        );
        var comprobante = inicio.GetProperty("comprobanteRegistro").GetString()!;
        using var ocupado = await cliente.PostAsJsonAsync(
            "/api/google/registrar",
            new TRegistroGoogle { ComprobanteRegistro = comprobante, NombreUsuario = invitado.NombreUsuario }
        );
        var error = await ocupado.Content.ReadFromJsonAsync<Respuesta<object>>(Json);
        verificar(
            ocupado.StatusCode == HttpStatusCode.Conflict && error?.CodigoError == "NombreOcupado",
            "Google HTTP no se apropia del invitado con nickname ocupado"
        );
        var nueva = await DatosAsync<TAutenticacion>(
            await cliente.PostAsJsonAsync(
            "/api/google/registrar",
            new TRegistroGoogle { ComprobanteRegistro = comprobante, NombreUsuario = "NuevoGoogleHttp" }
        )
        );
        verificar(
            nueva.GoogleVinculado && nueva.UsuarioId != invitado.UsuarioId,
            "Google HTTP crea cuenta y vinculo juntos"
        );
        using var repetida = await cliente.PostAsJsonAsync(
            "/api/google/registrar",
            new TRegistroGoogle { ComprobanteRegistro = comprobante, NombreUsuario = "Duplicado" }
        );
        verificar(repetida.StatusCode == HttpStatusCode.Conflict, "Google HTTP rechaza comprobante consumido");
        using var nonceUsado = await cliente.PostAsJsonAsync("/api/google/ingresar", pruebaGoogle);
        verificar(
            nonceUsado.StatusCode == HttpStatusCode.Unauthorized,
            "Google HTTP no reutiliza la prueba original"
        );
        var otroDesafio = await DatosAsync<JsonElement>(await cliente.PostAsync("/api/google/desafio", null));
        var ingreso = await DatosAsync<TAutenticacion>(await cliente.PostAsJsonAsync("/api/google/ingresar", new TAccesoGoogle
        {
            DesafioId = otroDesafio.GetProperty("id").GetString()!,
            IdToken = "identidad-prueba|google-nuevo|" + otroDesafio.GetProperty("nonce").GetString()
        }));
        verificar(
            ingreso.UsuarioId == nueva.UsuarioId && ingreso.NombreUsuario == nueva.NombreUsuario,
            "Google HTTP conocido entra sin elegir otro nickname"
        );
        UsarToken(cliente, nueva);
        using var anterior = await cliente.GetAsync("/api/usuarios/yo");
        verificar(
            anterior.StatusCode == HttpStatusCode.Unauthorized,
            "Google HTTP conserva sesion unica tras registrarse"
        );
    }

    // Solo pertenece al host de pruebas en memoria; producción conserva ValidadorGoogle oficial.
    private sealed class ValidadorGooglePrueba : IValidadorGoogle
    {
        public Task<GoogleJsonWebSignature.Payload> ValidarAsync(string idToken)
        {
            var partes = idToken.Split('|');

            if (partes.Length != 3 || partes[0] != "identidad-prueba")
            {
                throw new InvalidJwtException("Token de prueba invalido");
            }

            return Task.FromResult(new GoogleJsonWebSignature.Payload { Subject = partes[1], Nonce = partes[2] });
        }
    }
}
