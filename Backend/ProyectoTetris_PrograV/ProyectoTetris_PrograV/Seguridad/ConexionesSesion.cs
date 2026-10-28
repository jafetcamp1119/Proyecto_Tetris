using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using ProyectoTetris_PrograV.Hubs;
using ProyectoTetris_PrograV.Servicios;

namespace ProyectoTetris_PrograV.Seguridad;

public sealed class ConexionesSesion(IHubContext<JuegoHub> hub) : IDisposable
{
    private readonly PartitionedRateLimiter<int> acciones = PartitionedRateLimiter.Create<int, int>(usuarioId =>
        RateLimitPartition.GetFixedWindowLimiter(usuarioId, _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 120, Window = TimeSpan.FromSeconds(1), QueueLimit = 0 }));
    private sealed record Conexion(int UsuarioId, Guid SesionId, HubCallerContext Contexto)
    {
        public ConcurrentDictionary<Guid, byte> Salas { get; } = new();
    }
    private readonly ConcurrentDictionary<string, Conexion> conexiones = new();

    public bool PermitirAccion(int usuarioId)
    {
        using var permiso = acciones.AttemptAcquire(usuarioId);
        return permiso.IsAcquired;
    }

    public void Dispose()
    {
        acciones.Dispose();
    }

    public void Registrar(HubCallerContext contexto)
    {
        var usuarioId = int.Parse(contexto.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var sesionId = Guid.Parse(contexto.User!.FindFirstValue("sid")!);
        conexiones[contexto.ConnectionId] = new Conexion(usuarioId, sesionId, contexto);
    }

    public void RegistrarSala(string conexionId, Guid salaId)
    {
        if (conexiones.TryGetValue(conexionId, out var conexion)) { conexion.Salas.TryAdd(salaId, 0); }
    }

    public void Retirar(string conexionId)
    {
        conexiones.TryRemove(conexionId, out _);
    }

    public Task CerrarAnterioresAsync(int usuarioId, Guid sesionId)
    {
        return CerrarAsync(conexion => conexion.UsuarioId == usuarioId && conexion.SesionId != sesionId, "SesionReemplazada");
    }

    public Task CerrarSesionAsync(int usuarioId, Guid sesionId)
    {
        return CerrarAsync(conexion => conexion.UsuarioId == usuarioId && conexion.SesionId == sesionId, "SesionInvalida");
    }

    private async Task CerrarAsync(Func<Conexion, bool> seleccionar, string codigo)
    {
        foreach (var (identificador, conexion) in conexiones)
        {
            if (!seleccionar(conexion)) { continue; }
            try
            {
                foreach (var sala in conexion.Salas.Keys)
                {
                    await hub.Groups.RemoveFromGroupAsync(identificador, sala.ToString());
                    await hub.Groups.RemoveFromGroupAsync(identificador, PublicadorSalas.GrupoPrivado(sala, conexion.UsuarioId));
                }
                await hub.Clients.Client(identificador).SendAsync(codigo == "SesionReemplazada" ? "SesionReemplazada" : "SesionTerminada", new
                {
                    Codigo = codigo
                });
            }
            catch (Exception error) when (error is OperationCanceledException or IOException or HubException)
            {
                // Una conexion que ya termino no debe impedir entregar la nueva sesion guardada.
            }
            finally
            {
                conexion.Contexto.Abort();
                conexiones.TryRemove(identificador, out _);
            }
        }
    }
}
