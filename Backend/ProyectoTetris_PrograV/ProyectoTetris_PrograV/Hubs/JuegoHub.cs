using System.Security.Claims;
using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ProyectoTetris_PrograV.Servicios;
using Utilitarios;

namespace ProyectoTetris_PrograV.Hubs;

[Authorize]
public class JuegoHub : Hub
{
    private readonly ISalaLJ salas;
    private readonly PublicadorSalas publicador;

    public JuegoHub(ISalaLJ salas, PublicadorSalas publicador)
    {
        this.salas = salas;
        this.publicador = publicador;
    }

    private int UsuarioId
    {
        get
        {
            return int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }

    public async Task<Respuesta<TSala>> Entrar(Guid salaId)
    {
        var respuesta = salas.Conectar(salaId, UsuarioId, Context.ConnectionId);
        if (respuesta.Success)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, salaId.ToString());
            await publicador.PublicarEstadoAsync(respuesta.Data!);
        }

        return respuesta;
    }

    public async Task<Respuesta<TSala>> Mover(Guid salaId, TMovimiento datos)
    {
        if (!salas.ConexionAutorizada(salaId, UsuarioId, Context.ConnectionId))
        {
            return Respuesta<TSala>.Fail("Debe entrar a la sala con esta conexión antes de mover.");
        }

        var respuesta = salas.Mover(salaId, UsuarioId, datos);
        await PublicarSiCorresponde(respuesta);
        return respuesta;
    }

    public async Task<Respuesta<TSala>> SolicitarPausa(Guid salaId)
    {
        var respuesta = salas.SolicitarPausa(salaId, UsuarioId);
        await PublicarSiCorresponde(respuesta);
        return respuesta;
    }

    public async Task<Respuesta<TSala>> VotarPausa(Guid salaId, bool aceptar)
    {
        var respuesta = salas.VotarPausa(salaId, UsuarioId, aceptar);
        await PublicarSiCorresponde(respuesta);
        return respuesta;
    }

    // Solo para la partida individual.
    public async Task<Respuesta<TSala>> Reanudar(Guid salaId)
    {
        var respuesta = salas.Reanudar(salaId, UsuarioId);
        await PublicarSiCorresponde(respuesta);
        return respuesta;
    }

    public async Task<Respuesta<TSala>> Abandonar(Guid salaId)
    {
        var respuesta = salas.Abandonar(salaId, UsuarioId);
        await PublicarSiCorresponde(respuesta);
        return respuesta;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var estado in salas.Desconectar(Context.ConnectionId))
        {
            await publicador.PublicarEstadoAsync(estado);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task PublicarSiCorresponde(Respuesta<TSala> respuesta)
    {
        if (respuesta.Success)
        {
            await publicador.PublicarEstadoAsync(respuesta.Data!);
        }
    }
}
