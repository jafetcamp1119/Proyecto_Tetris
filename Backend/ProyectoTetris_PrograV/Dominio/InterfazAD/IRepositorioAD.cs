using System.Linq.Expressions;
using Utilitarios;

namespace Dominio.InterfazAD;

// Los cambios se confirman únicamente mediante la unidad de trabajo.
public interface IRepositorioAD<TEntity>
    where TEntity : class
{
    Respuesta<TEntity> Insertar(TEntity entidad);
    Respuesta<TEntity> Modificar(TEntity entidad);
    Respuesta<bool> Eliminar(TEntity entidad);
    Respuesta<IEnumerable<TEntity>> Listar();
    Respuesta<IEnumerable<TEntity>> Buscar(Expression<Func<TEntity, bool>> predicado);
    Respuesta<TEntity> ObtenerEntidad(Expression<Func<TEntity, bool>> predicado);
}
