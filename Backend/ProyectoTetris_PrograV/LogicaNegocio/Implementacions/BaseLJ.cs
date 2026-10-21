using AutoMapper;
using Dominio.InterfazAD;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using Utilitarios;

namespace LogicaJuego.Implementacions;

// Comparte solamente el mantenimiento persistente. Las reglas del juego viven en Motor y Servicios.
public abstract class BaseLJ<TEntity, TDatos>
    where TEntity : class where TDatos : class
{
    private readonly IRepositorioAD<TEntity> repositorio;
    private readonly ILogger logger;
    protected readonly IUnidadTrabajoEF Unidad;
    protected readonly IMapper Mapper;

    protected BaseLJ(IUnidadTrabajoEF unidad, IRepositorioAD<TEntity> repositorio, IMapper mapper, ILogger logger)
    {
        this.repositorio = repositorio;
        this.logger = logger;
        this.Unidad = unidad;
        this.Mapper = mapper;
    }

    protected abstract Expression<Func<TEntity, bool>> PorId(TDatos datos);
    protected virtual string? Validar(TDatos datos)
    {
        return null;
    }

    protected Respuesta<T> Ejecutar<T>(Func<Respuesta<T>> operacion)
    {
        try
        {
            return operacion();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error de persistencia en {Entidad}", typeof(TEntity).Name);
            Unidad.Rollback();
            return Respuesta<T>.Fail("No se pudo completar la operación de datos.");
        }
    }

    public Respuesta<TDatos> Insertar(TDatos datos)
    {
        return Ejecutar(() =>
        {
            var error = Validar(datos);
            if (error != null)
            {
                return Respuesta<TDatos>.Fail(error);
            }

            var entidad = Mapper.Map<TEntity>(datos);
            var respuesta = repositorio.Insertar(entidad);
            if (!respuesta.Success)
            {
                return Respuesta<TDatos>.Fail(respuesta.Error ?? "Error al insertar.");
            }

            Unidad.Completar();
            return Respuesta<TDatos>.Ok(Mapper.Map<TDatos>(entidad));
        });
    }

    public Respuesta<TDatos> Modificar(TDatos datos)
    {
        return Ejecutar(() =>
        {
            var error = Validar(datos);
            if (error != null)
            {
                return Respuesta<TDatos>.Fail(error);
            }

            var actual = repositorio.ObtenerEntidad(PorId(datos));
            if (!actual.Success)
            {
                return Respuesta<TDatos>.Fail(actual.Error ?? "Error al consultar.");
            }

            if (actual.Data == null)
            {
                return Respuesta<TDatos>.Fail("Registro no encontrado.");
            }

            Mapper.Map(datos, actual.Data);
            var respuesta = repositorio.Modificar(actual.Data);
            if (!respuesta.Success)
            {
                return Respuesta<TDatos>.Fail(respuesta.Error ?? "Error al modificar.");
            }

            Unidad.Completar();
            return Respuesta<TDatos>.Ok(Mapper.Map<TDatos>(actual.Data));
        });
    }

    public Respuesta<bool> Eliminar(TDatos datos)
    {
        return Ejecutar(() =>
        {
            var actual = repositorio.ObtenerEntidad(PorId(datos));
            if (!actual.Success)
            {
                return Respuesta<bool>.Fail(actual.Error ?? "Error al consultar.");
            }

            if (actual.Data == null)
            {
                return Respuesta<bool>.Fail("Registro no encontrado.");
            }

            var respuesta = repositorio.Eliminar(actual.Data);
            if (!respuesta.Success)
            {
                return respuesta;
            }

            Unidad.Completar();
            return respuesta;
        });
    }

    // Se conservan los nombres existentes: Obtener devuelve colección y Buscar una entidad.
    public Respuesta<IEnumerable<TDatos>> Obtener(TDatos datos)
    {
        return Ejecutar(() =>
        {
            var respuesta = repositorio.Buscar(PorId(datos));
            if (respuesta.Success)
            {
                return Respuesta<IEnumerable<TDatos>>.Ok(Mapper.Map<IEnumerable<TDatos>>(respuesta.Data));
            }

            return Respuesta<IEnumerable<TDatos>>.Fail(respuesta.Error ?? "Error al consultar.");
        });
    }

    public Respuesta<TDatos> Buscar(TDatos datos)
    {
        return Ejecutar(() =>
        {
            var respuesta = repositorio.ObtenerEntidad(PorId(datos));
            if (!respuesta.Success)
            {
                return Respuesta<TDatos>.Fail(respuesta.Error ?? "Error al consultar.");
            }

            if (respuesta.Data == null)
            {
                return Respuesta<TDatos>.Fail("Registro no encontrado.");
            }

            return Respuesta<TDatos>.Ok(Mapper.Map<TDatos>(respuesta.Data));
        });
    }

    public Respuesta<IEnumerable<TDatos>> Listar()
    {
        return Ejecutar(() =>
        {
            var respuesta = repositorio.Listar();
            if (respuesta.Success)
            {
                return Respuesta<IEnumerable<TDatos>>.Ok(Mapper.Map<IEnumerable<TDatos>>(respuesta.Data));
            }

            return Respuesta<IEnumerable<TDatos>>.Fail(respuesta.Error ?? "Error al consultar.");
        });
    }
}
