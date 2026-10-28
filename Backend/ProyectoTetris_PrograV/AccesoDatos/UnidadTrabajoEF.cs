using AccesoDatos.Contexto;

using Dominio.Entidades;
using Dominio.InterfazAD;

using Microsoft.EntityFrameworkCore.Storage;

namespace AccesoDatos;

// Reúne los repositorios que usan el mismo contexto de SQL Server.
// Permite confirmar los cambios juntos o deshacer una operación que falló.
public class UnidadTrabajoEF : IUnidadTrabajoEF
{
    private readonly TetrisDBContext contexto;
    private IDbContextTransaction? _transaccion;

    // Crea los repositorios sobre el mismo contexto para guardar sus cambios en conjunto.
    public UnidadTrabajoEF(TetrisDBContext contexto)
    {
        this.contexto = contexto;
        this.TUsuario = new RepositorioAD<Usuario>(contexto);
        this.TCredencialDispositivo = new RepositorioAD<CredencialDispositivo>(contexto);
        this.TConfiguracion = new RepositorioAD<ConfiguracionJugador>(contexto);
        this.TPartida = new RepositorioAD<Partida>(contexto);
        this.TParticipante = new RepositorioAD<ParticipantesPartidum>(contexto);
        this.TTurno = new RepositorioAD<TurnosPartidum>(contexto);
        this.TResultado = new RepositorioAD<Resultado>(contexto);
        this.TEstadistica = new RepositorioAD<EstadisticasJugador>(contexto);
        this.TTipoPieza = new RepositorioAD<TiposPieza>(contexto);
    }

    public IRepositorioAD<Usuario> TUsuario
    {
        get;
    }

    public IRepositorioAD<CredencialDispositivo> TCredencialDispositivo
    {
        get;
    }

    public IRepositorioAD<ConfiguracionJugador> TConfiguracion
    {
        get;
    }

    public IRepositorioAD<Partida> TPartida
    {
        get;
    }

    public IRepositorioAD<ParticipantesPartidum> TParticipante
    {
        get;
    }

    public IRepositorioAD<TurnosPartidum> TTurno
    {
        get;
    }

    public IRepositorioAD<Resultado> TResultado
    {
        get;
    }

    public IRepositorioAD<EstadisticasJugador> TEstadistica
    {
        get;
    }

    public IRepositorioAD<TiposPieza> TTipoPieza
    {
        get;
    }

    // Guarda los cambios pendientes de todos los repositorios del contexto.
    public int Completar()
    {
        return contexto.SaveChanges();
    }

    // Guarda los cambios sin bloquear la espera y permite cancelar la operación.
    public Task<int> CompletarAsync(CancellationToken cancellationToken = default)
    {
        return contexto.SaveChangesAsync(cancellationToken);
    }

    // Abre una transacción explícita y evita abrir otra mientras exista una activa.
    public void EmpezarTransaccion()
    {
        if (_transaccion != null)
        {
            throw new InvalidOperationException("Ya existe una transacción activa.");
        }

        _transaccion = contexto.Database.BeginTransaction();
    }

    // Guarda y confirma la transacción; si falla, la revierte antes de liberar sus recursos.
    public void CompletarTran()
    {
        if (_transaccion == null)
        {
            throw new InvalidOperationException("No existe una transacción activa.");
        }

        try
        {
            contexto.SaveChanges();
            _transaccion.Commit();
        }
        catch
        {
            _transaccion.Rollback();
            throw;
        }
        finally
        {
            _transaccion.Dispose();
            _transaccion = null;
        }
    }

    // Revierte la transacción activa y limpia las entidades pendientes del contexto.
    public void Rollback()
    {
        _transaccion?.Rollback();
        _transaccion?.Dispose();
        _transaccion = null;
        contexto.ChangeTracker.Clear();
    }

    // DI administra la vida del contexto.
    public void Dispose()
    {
        _transaccion?.Dispose();
    }
}
