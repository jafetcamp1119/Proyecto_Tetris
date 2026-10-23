using Dominio.EntidadesTipadas;
using Utilitarios;

namespace Tetris.Cliente.Servicios;

// Cliente C# independiente, conservado como referencia. El frontend Ionic consume HTTP con TypeScript.
public interface ITetrisApi
{
    Task<Respuesta<TAutenticacion>> RegistrarAsync(TRegistroUsuario datos, CancellationToken ct = default);
    Task<Respuesta<TAutenticacion>> RecuperarAsync(TRecuperarSesion datos, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<Respuesta<TSala>> CrearSalaAsync(TCrearSala datos, CancellationToken ct = default);
    Task<Respuesta<TSala>> UnirseAsync(Guid sala, CancellationToken ct = default);
    Task<Respuesta<TSala>> UnirsePorCodigoAsync(string codigo, CancellationToken ct = default);
    Task<Respuesta<TSala>> ObtenerSalaAsync(Guid sala, CancellationToken ct = default);
    Task<Respuesta<TSala>> ListoAsync(Guid sala, bool listo, CancellationToken ct = default);
    Task<Respuesta<TSala>> IniciarAsync(Guid sala, CancellationToken ct = default);
    Task<Respuesta<TSala>> MoverAsync(Guid sala, TMovimiento datos, CancellationToken ct = default);
    Task<Respuesta<TSalaPublica[]>> ListarSalasPublicasAsync(CancellationToken ct = default);
    Task<Respuesta<TSala>> ConfigurarPiezasAsync(Guid sala, TConfiguracionPiezas datos, CancellationToken ct = default);
    Task<Respuesta<TSala>> ConfigurarSalaAsync(Guid sala, TConfiguracionSala datos, CancellationToken ct = default);
    Task<Respuesta<TSala>> SolicitarPausaAsync(Guid sala, CancellationToken ct = default);
    Task<Respuesta<TSala>> VotarPausaAsync(Guid sala, bool aceptar, CancellationToken ct = default);
    Task<Respuesta<TSala>> ReanudarAsync(Guid sala, CancellationToken ct = default);
    Task<Respuesta<TConfiguracion>> ObtenerConfiguracionAsync(CancellationToken ct = default);
    Task<Respuesta<TConfiguracion>> GuardarConfiguracionAsync(TConfiguracion datos, CancellationToken ct = default);
    Task<Respuesta<IEnumerable<TResultado>>> ResultadosAsync(CancellationToken ct = default);
}

