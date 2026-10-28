using System.Security.Claims;

using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

using ProyectoTetris_PrograV.Servicios;

using Utilitarios;

namespace ProyectoTetris_PrograV.Hubs;

// Recibe las acciones de juego en tiempo real y registra las conexiones de las salas.
// SalaLJ valida las reglas y PublicadorSalas envía los cambios a sus participantes.
[Authorize]
public class JuegoHub : Hub
{
    private readonly ISalaLJ salas;
    private readonly PublicadorSalas publicador;
    private readonly Seguridad.ConexionesSesion conexiones;

    public JuegoHub(ISalaLJ salas, PublicadorSalas publicador, Seguridad.ConexionesSesion conexiones)
    {
        this.salas = salas;
        this.publicador = publicador;
        this.conexiones = conexiones;
    }

    private int UsuarioId
    {
        get
        {
            return int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }

    // Registra la conexión en la sala y en sus grupos público y privado.
    public async Task<Respuesta<TSala>> Entrar(Guid salaId)
    {
        var respuesta = salas.Conectar(salaId, UsuarioId, Context.ConnectionId);

        if (respuesta.Success)
        {
            conexiones.RegistrarSala(Context.ConnectionId, salaId);
            await Groups.AddToGroupAsync(Context.ConnectionId, salaId.ToString());
            await Groups.AddToGroupAsync(Context.ConnectionId, PublicadorSalas.GrupoPrivado(salaId, UsuarioId));
            await publicador.PublicarEstadoAsync(respuesta.Data!);
        }

        return respuesta;
    }

    // Exige que esta conexión haya entrado a la sala antes de aceptar movimientos.
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

    // Solicita una pausa y publica el resultado aceptado.
    public async Task<Respuesta<TSala>> SolicitarPausa(Guid salaId)
    {
        var respuesta = salas.SolicitarPausa(salaId, UsuarioId);
        await PublicarSiCorresponde(respuesta);
        return respuesta;
    }

    // Registra un voto y publica el estado resultante.
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

    // Solicita el abandono y publica los cambios de la sala.
    public async Task<Respuesta<TSala>> Abandonar(Guid salaId)
    {
        var respuesta = salas.Abandonar(salaId, UsuarioId);
        await PublicarSiCorresponde(respuesta);
        return respuesta;
    }

    // Retira la conexión e informa las salas que iniciaron su plazo de reconexión.
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        conexiones.Retirar(Context.ConnectionId);

        foreach (var estado in salas.Desconectar(Context.ConnectionId))
        {
            await publicador.PublicarEstadoAsync(estado);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Registra la conexión con el usuario y sesión ya verificados por el filtro.
    public override Task OnConnectedAsync()
    {
        conexiones.Registrar(Context);
        return base.OnConnectedAsync();
    }

    // Publica por SignalR solo cuando la respuesta de negocio tuvo éxito.
    private async Task PublicarSiCorresponde(Respuesta<TSala> respuesta)
    {
        if (respuesta.Success)
        {
            await publicador.PublicarEstadoAsync(respuesta.Data!);
        }
    }
}
