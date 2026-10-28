using System.Linq.Expressions;

using AutoMapper;

using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;

using Microsoft.Extensions.Logging;

using Utilitarios;

namespace LogicaJuego.Implementacions;

// Consulta y guarda las preferencias asociadas al usuario autenticado.
// Utiliza BaseLJ para confirmar los cambios en la base de datos.
public class ConfiguracionLJ : BaseLJ<ConfiguracionJugador, TConfiguracion>, IConfiguracionLJ
{
    public ConfiguracionLJ(
        IUnidadTrabajoEF unidad,
        IMapper mapper,
        ILogger<ConfiguracionLJ> logger) : base(
        unidad,
        unidad.TConfiguracion,
        mapper,
        logger)
    {
    }

    protected override Expression<Func<ConfiguracionJugador, bool>> PorId(TConfiguracion datos)
    {
        return x => x.ConfiguracionId == datos.ConfiguracionId;
    }

    // Devuelve las preferencias propias o los valores iniciales si aún no se guardaron.
    public Respuesta<TConfiguracion> ObtenerPorUsuario(int usuarioId)
    {
        return Ejecutar(() =>
        {
            var actual = Unidad.TConfiguracion.ObtenerEntidad(c => c.UsuarioId == usuarioId);

            if (!actual.Success)
            {
                return Respuesta<TConfiguracion>.Fail(actual.Error ?? "Error al consultar configuración.");
            }

            if (actual.Data == null)
            {
                var valoresIniciales = new TConfiguracion
                {
                    UsuarioId = usuarioId,
                    MostrarControlesPantalla = true,
                    PermitirPausa = true,
                    SonidoActivo = true
                };

                return Respuesta<TConfiguracion>.Ok(valoresIniciales);
            }

            var configuracion = Mapper.Map<TConfiguracion>(actual.Data);
            return Respuesta<TConfiguracion>.Ok(configuracion);
        });
    }

    // Asigna los identificadores desde el servidor y decide entre insertar o actualizar.
    public Respuesta<TConfiguracion> GuardarPorUsuario(int usuarioId, TConfiguracion datos)
    {
        return Ejecutar(() =>
        {
            var actual = Unidad.TConfiguracion.ObtenerEntidad(c => c.UsuarioId == usuarioId);

            if (!actual.Success)
            {
                return Respuesta<TConfiguracion>.Fail(actual.Error ?? "Error al consultar configuración.");
            }

            // La identidad y el ID se obtienen del servidor, nunca del formulario.
            datos.UsuarioId = usuarioId;
            datos.ConfiguracionId = actual.Data?.ConfiguracionId ?? 0;

            if (actual.Data == null)
            {
                return Insertar(datos);
            }

            return Modificar(datos);
        });
    }
}

