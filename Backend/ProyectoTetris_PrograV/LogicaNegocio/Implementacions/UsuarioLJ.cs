using AutoMapper;

using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;

using Microsoft.Extensions.Logging;

using Utilitarios;

namespace LogicaJuego
{
    // Consulta y mantiene los datos del usuario mediante la unidad de trabajo.
    // El registro de identidades se realiza en AccesoUsuarioLJ.
    public class UsuarioLJ : IUsuarioLJ
    {
        private readonly IUnidadTrabajoEF _unidadDeTrabajo;

        private readonly ILogger<UsuarioLJ> _logger;
        private readonly IMapper _mapper;

        public UsuarioLJ(IUnidadTrabajoEF unidadDeTrabajo, ILogger<UsuarioLJ> logger, IMapper mapper)
        {
            _unidadDeTrabajo = unidadDeTrabajo;
            _logger = logger;
            _mapper = mapper;
        }

        // Rechaza el alta por mantenimiento; las cuentas se crean con AccesoUsuarioLJ.
        public Respuesta<TUsuario> Insertar(TUsuario datos)
        {
            return Respuesta<TUsuario>.Fail("Para crear una identidad utilice el registro con nickname de IAccesoUsuarioLJ.");
        }

        // Actualiza el estado del usuario sin permitir cambiar su nickname.
        public Respuesta<TUsuario> Modificar(TUsuario datos)
        {
            var resultado = new Respuesta<TUsuario>();

            try
            {
                var usuarioActual = _unidadDeTrabajo.TUsuario.ObtenerEntidad(x => x.UsuarioId == datos.UsuarioId);

                if (usuarioActual.Data == null)
                {
                    resultado.Success = false;
                    resultado.Error = "No existe el usuario a modificar.";
                    return resultado;
                }

                if (datos.NombreUsuario.Trim() != usuarioActual.Data.NombreUsuario)
                {
                    return Respuesta<TUsuario>.Fail("El cambio de nickname no está habilitado.");
                }

                usuarioActual.Data.Activo = datos.Activo;
                var respuestaRepositorio = _unidadDeTrabajo.TUsuario.Modificar(usuarioActual.Data);

                if (!respuestaRepositorio.Success)
                {
                    resultado.Success = false;
                    resultado.Error = respuestaRepositorio.Error;
                    return resultado;
                }

                _unidadDeTrabajo.Completar();
                resultado.Data = _mapper.Map<TUsuario>(respuestaRepositorio.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al modificar el usuario {UsuarioId}", datos.UsuarioId);
                resultado.Success = false;
                _unidadDeTrabajo.Rollback();
                resultado.Error = "No se pudo completar la operación de usuarios.";
            }

            return resultado;
        }

        // Solicita eliminar el usuario y devuelve un error controlado si falla la persistencia.
        public Respuesta<bool> Eliminar(TUsuario datos)
        {
            var resultado = new Respuesta<bool>();

            try
            {
                var usuario = _unidadDeTrabajo.TUsuario.ObtenerEntidad(x => x.UsuarioId == datos.UsuarioId);

                if (usuario.Data == null)
                {
                    resultado.Success = false;
                    resultado.Error = "No existe el usuario a eliminar.";
                    return resultado;
                }

                var respuestaRepositorio = _unidadDeTrabajo.TUsuario.Eliminar(usuario.Data);

                if (!respuestaRepositorio.Success)
                {
                    resultado.Success = false;
                    resultado.Error = respuestaRepositorio.Error;
                    return resultado;
                }

                _unidadDeTrabajo.Completar();
                resultado.Data = respuestaRepositorio.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar el usuario {UsuarioId}", datos.UsuarioId);
                resultado.Success = false;
                _unidadDeTrabajo.Rollback();
                resultado.Error = "No se pudo completar la operación de usuarios.";
            }

            return resultado;
        }

        // Busca por UsuarioId y devuelve los datos en una colección.
        public Respuesta<IEnumerable<TUsuario>> Obtener(TUsuario datos)
        {
            var resultado = new Respuesta<IEnumerable<TUsuario>>();

            try
            {
                var respuestaRepositorio = _unidadDeTrabajo.TUsuario.Buscar(x => x.UsuarioId == datos.UsuarioId);

                if (!respuestaRepositorio.Success)
                {
                    resultado.Success = false;
                    resultado.Error = respuestaRepositorio.Error;
                    return resultado;
                }

                resultado.Data = _mapper.Map<IEnumerable<TUsuario>>(respuestaRepositorio.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario.");
                resultado.Success = false;
                _unidadDeTrabajo.Rollback();
                resultado.Error = "No se pudo completar la operación de usuarios.";
            }

            return resultado;
        }

        // Busca por UsuarioId y devuelve el usuario público o un error si no existe.
        public Respuesta<TUsuario> Buscar(TUsuario datos)
        {
            var resultado = new Respuesta<TUsuario>();

            try
            {
                var respuestaRepositorio = _unidadDeTrabajo.TUsuario.ObtenerEntidad(
                    x => x.UsuarioId == datos.UsuarioId);

                if (!respuestaRepositorio.Success)
                {
                    resultado.Success = false;
                    resultado.Error = respuestaRepositorio.Error;
                    return resultado;
                }

                if (respuestaRepositorio.Data == null)
                {
                    resultado.Success = false;
                    resultado.Error = "Usuario no encontrado.";
                    return resultado;
                }

                resultado.Data = _mapper.Map<TUsuario>(respuestaRepositorio.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar usuario.");
                resultado.Success = false;
                _unidadDeTrabajo.Rollback();
                resultado.Error = "No se pudo completar la operación de usuarios.";
            }

            return resultado;
        }

        // Consulta los usuarios y los convierte en datos públicos.
        public Respuesta<IEnumerable<TUsuario>> Listar()
        {
            var resultado = new Respuesta<IEnumerable<TUsuario>>();

            try
            {
                var respuestaRepositorio = _unidadDeTrabajo.TUsuario.Listar();

                if (!respuestaRepositorio.Success)
                {
                    resultado.Success = false;
                    resultado.Error = respuestaRepositorio.Error;
                    return resultado;
                }

                resultado.Data = _mapper.Map<IEnumerable<TUsuario>>(respuestaRepositorio.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar usuarios.");
                resultado.Success = false;
                _unidadDeTrabajo.Rollback();
                resultado.Error = "No se pudo completar la operación de usuarios.";
            }

            return resultado;
        }
    }
}
