using AutoMapper;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;
using Microsoft.Extensions.Logging;
using Utilitarios;

namespace LogicaJuego
{
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

        public Respuesta<TUsuario> Insertar(TUsuario datos)
        {
            var resultado = new Respuesta<TUsuario>();
            try
            {
                if (string.IsNullOrWhiteSpace(datos.NombreUsuario) ||
                    string.IsNullOrWhiteSpace(datos.Correo) ||
                    string.IsNullOrWhiteSpace(datos.PasswordHash))
                {
                    return Respuesta<TUsuario>.Fail(
                        "Para crear cuentas desde la API utilice Registrar con una contraseña.");
                }

                var duplicado = _unidadDeTrabajo.TUsuario.ObtenerEntidad(
                    x => x.NombreUsuario == datos.NombreUsuario || x.Correo == datos.Correo);
                if (!duplicado.Success)
                {
                    return Respuesta<TUsuario>.Fail(duplicado.Error ?? "Error al consultar.");
                }

                if (duplicado.Data != null)
                {
                    return Respuesta<TUsuario>.Fail("El nombre o correo ya está registrado.");
                }

                // 2. Convertimos TUsuario -> Usuario
                var entidad = _mapper.Map<Usuario>(datos);
                entidad.PasswordHash = datos.PasswordHash;
                // 3. Insertar mediante el repositorio.
                // Esta línea depende de cómo hiciste IUnidadTrabajoEF.
                var respuestaRepositorio = _unidadDeTrabajo.TUsuario.Insertar(entidad);
                if (!respuestaRepositorio.Success)
                {
                    resultado.Success = false;
                    resultado.Error = respuestaRepositorio.Error;
                    return resultado;
                }

                // 4. Guardar cambios
                _unidadDeTrabajo.Completar();
                // 5. Usuario -> TUsuario
                resultado.Data = _mapper.Map<TUsuario>(respuestaRepositorio.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al insertar usuario.");
                resultado.Success = false;
                _unidadDeTrabajo.Rollback();
                resultado.Error = "No se pudo completar la operación de usuarios.";
            }

            return resultado;
        }

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

                _mapper.Map(datos, usuarioActual.Data);
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
