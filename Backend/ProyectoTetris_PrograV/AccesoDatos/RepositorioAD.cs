using System.Linq.Expressions;

using AccesoDatos.Contexto;

using Dominio.InterfazAD;

using Utilitarios;

namespace AccesoDatos;

// Realiza las consultas y prepara los cambios de una entidad con Entity Framework.
// Los cambios se guardan cuando la unidad de trabajo llama a SaveChanges.
public class RepositorioAD<TEntity> : IRepositorioAD<TEntity> where TEntity : class
{
    private readonly TetrisDBContext contexto;

    public RepositorioAD(TetrisDBContext contexto)
    {
        this.contexto = contexto;
    }

    // Agrega la entidad al contexto; todavía no ejecuta el guardado en SQL.
    public Respuesta<TEntity> Insertar(TEntity entidad)
    {
        contexto.Set<TEntity>().Add(entidad);
        return Respuesta<TEntity>.Ok(entidad);
    }

    // Marca la entidad para actualizarla cuando se confirmen los cambios.
    public Respuesta<TEntity> Modificar(TEntity entidad)
    {
        contexto.Set<TEntity>().Update(entidad);
        return Respuesta<TEntity>.Ok(entidad);
    }

    // Marca la entidad para eliminarla al confirmar los cambios.
    public Respuesta<bool> Eliminar(TEntity entidad)
    {
        contexto.Set<TEntity>().Remove(entidad);
        return Respuesta<bool>.Ok(true);
    }

    // Consulta todas las entidades de este repositorio.
    public Respuesta<IEnumerable<TEntity>> Listar()
    {
        return Respuesta<IEnumerable<TEntity>>.Ok(contexto.Set<TEntity>().ToList());
    }

    // Ejecuta en SQL el filtro recibido y devuelve sus coincidencias.
    public Respuesta<IEnumerable<TEntity>> Buscar(Expression<Func<TEntity, bool>> predicado)
    {
        return Respuesta<IEnumerable<TEntity>>.Ok(contexto.Set<TEntity>().Where(predicado).ToList());
    }

    // Devuelve la primera coincidencia del filtro, o Data en null si no existe.
    public Respuesta<TEntity> ObtenerEntidad(Expression<Func<TEntity, bool>> predicado)
    {
        return new()
        {
            Data = contexto.Set<TEntity>().FirstOrDefault(predicado)
        };
    }
}
