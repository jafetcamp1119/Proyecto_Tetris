using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dominio.EntidadesTipadas;
using Utilitarios;

namespace Tetris.Cliente.Servicios;

public class TetrisApi : ITetrisApi
{
    private readonly HttpClient http;
    private string? _token;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public TetrisApi(HttpClient http)
    {
        this.http = http;
    }

    private async Task<Respuesta<T>> Enviar<T>(HttpMethod metodo, string ruta, object? datos, CancellationToken ct)
    {
        using var solicitud = new HttpRequestMessage(metodo, ruta);
        if (datos != null)
        {
            solicitud.Content = JsonContent.Create(datos, datos.GetType(), options: Json);
        }

        if (!string.IsNullOrEmpty(_token))
        {
            solicitud.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        }

        using var respuesta = await http.SendAsync(solicitud, ct);
        if (respuesta.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return Respuesta<T>.Fail("Inicie sesión para continuar.");
        }

        if (respuesta.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return Respuesta<T>.Fail("No tiene permiso para esta acción.");
        }

        try
        {
            var resultado = await respuesta.Content.ReadFromJsonAsync<Respuesta<T>>(Json, ct);
            if (resultado == null)
            {
                return Respuesta<T>.Fail("Respuesta vacía del servidor.");
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                return Respuesta<T>.Fail(resultado.Error ?? "El servidor rechazó los datos.");
            }

            return resultado;
        }
        catch (JsonException)
        {
            return Respuesta<T>.Fail("El servidor devolvió una respuesta inesperada.");
        }
    }

    public async Task<Respuesta<TAutenticacion>> RegistrarAsync(TRegistroUsuario datos, CancellationToken ct = default)
    {
        var respuesta = await Enviar<TAutenticacion>(HttpMethod.Post, "api/usuarios/registrar", datos, ct);
        GuardarToken(respuesta);
        return respuesta;
    }

    public async Task<Respuesta<TAutenticacion>> RecuperarAsync(TRecuperarSesion datos, CancellationToken ct = default)
    {
        var respuesta = await Enviar<TAutenticacion>(HttpMethod.Post, "api/usuarios/recuperar", datos, ct);
        GuardarToken(respuesta);
        return respuesta;
    }

    private void GuardarToken(Respuesta<TAutenticacion> respuesta)
    {
        if (respuesta.Success && respuesta.Data != null)
        {
            _token = respuesta.Data.Token;
        }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "api/usuarios/logout");

        if (!string.IsNullOrEmpty(_token))
        {
            solicitud.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        }

        using var respuesta = await http.SendAsync(solicitud, ct);
        respuesta.EnsureSuccessStatusCode();
        _token = null;
    }
    public Task<Respuesta<TSala>> CrearSalaAsync(TCrearSala datos, CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Post, "api/salas", datos, ct);
    }

    public Task<Respuesta<TSala>> UnirseAsync(Guid sala, CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Post, $"api/salas/{sala}/unirse", null, ct);
    }

    public Task<Respuesta<TSala>> UnirsePorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Post, $"api/salas/codigo/{Uri.EscapeDataString(codigo)}/unirse", null, ct);
    }

    public Task<Respuesta<TSala>> ObtenerSalaAsync(Guid sala, CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Get, $"api/salas/{sala}", null, ct);
    }

    public Task<Respuesta<TSala>> ListoAsync(Guid sala, bool listo, CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Put, $"api/salas/{sala}/listo", listo, ct);
    }

    public Task<Respuesta<TSala>> IniciarAsync(Guid sala, CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Post, $"api/salas/{sala}/iniciar", null, ct);
    }

    public Task<Respuesta<TSala>> MoverAsync(Guid sala, TMovimiento datos, CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Post, $"api/salas/{sala}/movimientos", datos, ct);
    }

    public Task<Respuesta<TSalaPublica[]>> ListarSalasPublicasAsync(CancellationToken ct = default)
    {
        return Enviar<TSalaPublica[]>(HttpMethod.Get, "api/salas", null, ct);
    }

    public Task<Respuesta<TSala>> ConfigurarPiezasAsync(
        Guid sala,
        TConfiguracionPiezas datos,
        CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Put, $"api/salas/{sala}/piezas-especiales", datos, ct);
    }

    public Task<Respuesta<TSala>> ConfigurarSalaAsync(
        Guid sala,
        TConfiguracionSala datos,
        CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Put, $"api/salas/{sala}/configuracion", datos, ct);
    }

    public Task<Respuesta<TSala>> SolicitarPausaAsync(Guid sala, CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Post, $"api/salas/{sala}/pausa", null, ct);
    }

    public Task<Respuesta<TSala>> VotarPausaAsync(Guid sala, bool aceptar, CancellationToken ct = default)
    {
        var voto = new TVotoPausa { Aceptar = aceptar };
        return Enviar<TSala>(HttpMethod.Post, $"api/salas/{sala}/pausa/voto", voto, ct);
    }

    public Task<Respuesta<TSala>> ReanudarAsync(Guid sala, CancellationToken ct = default)
    {
        return Enviar<TSala>(HttpMethod.Post, $"api/salas/{sala}/reanudar", null, ct);
    }

    public Task<Respuesta<TConfiguracion>> ObtenerConfiguracionAsync(CancellationToken ct = default)
    {
        return Enviar<TConfiguracion>(HttpMethod.Get, "api/configuracion", null, ct);
    }

    public Task<Respuesta<TConfiguracion>> GuardarConfiguracionAsync(
        TConfiguracion datos,
        CancellationToken ct = default)
    {
        return Enviar<TConfiguracion>(HttpMethod.Put, "api/configuracion", datos, ct);
    }

    public Task<Respuesta<IEnumerable<TResultado>>> ResultadosAsync(CancellationToken ct = default)
    {
        return Enviar<IEnumerable<TResultado>>(HttpMethod.Get, "api/resultados/mios", null, ct);
    }
}

