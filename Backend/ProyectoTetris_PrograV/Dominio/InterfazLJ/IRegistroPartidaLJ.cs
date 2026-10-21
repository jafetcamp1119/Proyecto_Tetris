using Dominio.EntidadesTipadas;

namespace Dominio.InterfazLJ;

// La API abre un alcance corto para guardar; ninguna sesión retiene un DbContext.
public interface IRegistroPartidaLJ
{
    Task CancelarInterrumpidasAsync(CancellationToken cancellationToken = default);
    Task GuardarAsync(TSala sala, CancellationToken cancellationToken = default);
}
