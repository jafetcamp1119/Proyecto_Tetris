using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Microsoft.AspNetCore.SignalR;
using ProyectoTetris_PrograV.Hubs;

namespace ProyectoTetris_PrograV.Servicios;

// Único lugar que envía mensajes de las salas por SignalR. Los controllers, el hub y el reloj
// lo usan para no repetir los nombres de los eventos.
public class PublicadorSalas
{
    public const string EventoEstado = "EstadoActualizado";

    private readonly ISalaLJ salas;
    private readonly IHubContext<JuegoHub> hub;

    public PublicadorSalas(ISalaLJ salas, IHubContext<JuegoHub> hub)
    {
        this.salas = salas;
        this.hub = hub;
    }

    // Envía el estado completo de la sala y después los eventos de pausa pendientes.
    public async Task PublicarEstadoAsync(TSala estado, CancellationToken cancelacion = default)
    {
        await hub.Clients.Group(estado.Id.ToString()).SendAsync(EventoEstado, estado, cancelacion);
        await PublicarEventosAsync(cancelacion);
    }

    public async Task PublicarEventosAsync(CancellationToken cancelacion = default)
    {
        foreach (var evento in salas.TomarEventos())
        {
            await hub.Clients.Group(evento.SalaId.ToString()).SendAsync(evento.Nombre, evento.Datos, cancelacion);

            foreach (var conexion in evento.ConexionesRetirar ?? [])
            {
                await hub.Groups.RemoveFromGroupAsync(conexion, evento.SalaId.ToString(), cancelacion);
            }
        }
    }
}
