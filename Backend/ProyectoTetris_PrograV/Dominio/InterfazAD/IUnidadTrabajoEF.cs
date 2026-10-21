using Dominio.Entidades;

namespace Dominio.InterfazAD;

public interface IUnidadTrabajoEF : IDisposable
{
    IRepositorioAD<Usuario> TUsuario { get; }

    IRepositorioAD<ConfiguracionJugador> TConfiguracion { get; }

    IRepositorioAD<Partida> TPartida { get; }

    IRepositorioAD<ParticipantesPartidum> TParticipante { get; }

    IRepositorioAD<TurnosPartidum> TTurno { get; }

    IRepositorioAD<Resultado> TResultado { get; }

    IRepositorioAD<EstadisticasJugador> TEstadistica { get; }

    IRepositorioAD<TiposPieza> TTipoPieza { get; }

    int Completar();
    Task<int> CompletarAsync(CancellationToken cancellationToken = default);
    void EmpezarTransaccion();
    void CompletarTran();
    void Rollback();
}
