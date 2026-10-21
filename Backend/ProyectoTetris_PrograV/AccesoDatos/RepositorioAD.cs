using AccesoDatos.Contexto;
using Dominio.InterfazAD;
using System.Linq.Expressions;
using Utilitarios;

namespace AccesoDatos;

public class RepositorioAD<TEntity> : IRepositorioAD<TEntity> where TEntity : class
{
    private readonly TetrisDBContext contexto;

    public RepositorioAD(TetrisDBContext contexto)
    {
        this.contexto = contexto;
    }

    public Respuesta<TEntity> Insertar(TEntity entidad)
    {
        contexto.Set<TEntity>().Add(entidad);
        return Respuesta<TEntity>.Ok(entidad);
    }

    public Respuesta<TEntity> Modificar(TEntity entidad)
    {
        contexto.Set<TEntity>().Update(entidad);
        return Respuesta<TEntity>.Ok(entidad);
    }

    public Respuesta<bool> Eliminar(TEntity entidad)
    {
        contexto.Set<TEntity>().Remove(entidad);
        return Respuesta<bool>.Ok(true);
    }

    public Respuesta<IEnumerable<TEntity>> Listar()
    {
        return Respuesta<IEnumerable<TEntity>>.Ok(contexto.Set<TEntity>().ToList());
    }

    public Respuesta<IEnumerable<TEntity>> Buscar(Expression<Func<TEntity, bool>> predicado)
    {
        return Respuesta<IEnumerable<TEntity>>.Ok(contexto.Set<TEntity>().Where(predicado).ToList());
    }

    public Respuesta<TEntity> ObtenerEntidad(Expression<Func<TEntity, bool>> predicado)
    {
        return new()
        {
            Data = contexto.Set<TEntity>().FirstOrDefault(predicado)
        };
    }
}
