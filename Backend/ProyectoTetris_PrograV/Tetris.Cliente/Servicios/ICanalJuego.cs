using Dominio.EntidadesTipadas;
using Utilitarios;

namespace Tetris.Cliente.Servicios;

// Contrato de referencia para un cliente C#. El frontend Ionic usará @microsoft/signalr.
// La implementación C# del canal sigue pendiente; no es una dependencia del frontend Ionic.
public interface ICanalJuego : IAsyncDisposable
{
    event Action<TSala>? EstadoActualizado;

    // Eventos de la pausa: PausaSolicitada, VotoPausaRegistrado, PausaRechazada, PausaIniciada y PausaFinalizada.
    event Action<string, TPausa>? EventoPausa;
    Task<Respuesta<TSala>> ConectarAsync(Guid sala, CancellationToken ct = default);
    Task<Respuesta<TSala>> MoverAsync(Guid sala, TMovimiento movimiento, CancellationToken ct = default);
    Task<Respuesta<TSala>> SolicitarPausaAsync(Guid sala, CancellationToken ct = default);
    Task<Respuesta<TSala>> VotarPausaAsync(Guid sala, bool aceptar, CancellationToken ct = default);
    Task<Respuesta<TSala>> ReanudarAsync(Guid sala, CancellationToken ct = default);
}

