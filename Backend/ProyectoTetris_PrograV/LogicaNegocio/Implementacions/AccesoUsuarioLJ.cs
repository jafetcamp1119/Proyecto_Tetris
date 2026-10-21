using AutoMapper;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Utilitarios;

namespace LogicaJuego.Implementacions;

public class AccesoUsuarioLJ : IAccesoUsuarioLJ
{
    private readonly IUnidadTrabajoEF unidad;
    private readonly IMapper mapper;
    private readonly IPasswordHasher<Usuario> hasher;
    private readonly ILogger<AccesoUsuarioLJ> logger;

    public AccesoUsuarioLJ(
        IUnidadTrabajoEF unidad,
        IMapper mapper,
        IPasswordHasher<Usuario> hasher,
        ILogger<AccesoUsuarioLJ> logger)
    {
        this.unidad = unidad;
        this.mapper = mapper;
        this.hasher = hasher;
        this.logger = logger;
    }

    public Respuesta<TUsuario> Registrar(TRegistroUsuario datos)
    {
        if (string.IsNullOrWhiteSpace(datos.NombreUsuario) ||
            datos.NombreUsuario.Trim().Length < 3 ||
            datos.NombreUsuario.Trim().Length > 50 ||
            string.IsNullOrWhiteSpace(datos.Correo) ||
            datos.Correo.Length > 100 ||
            !System.Net.Mail.MailAddress.TryCreate(
            datos.Correo,
            out _) ||
            datos.Contrasena.Length < 8 ||
            datos.Contrasena.Length > 100)
        {
            return Respuesta<TUsuario>.Fail("Revise nombre, correo y contraseña (8 a 100 caracteres).");
        }

        try
        {
            var nombre = datos.NombreUsuario.Trim();
            var correo = datos.Correo.Trim().ToLowerInvariant();
            var existente = unidad.TUsuario.ObtenerEntidad(x => x.NombreUsuario == nombre || x.Correo == correo);
            if (!existente.Success)
            {
                return Respuesta<TUsuario>.Fail(existente.Error ?? "Error al consultar.");
            }

            if (existente.Data != null)
            {
                return Respuesta<TUsuario>.Fail("El nombre o correo ya está registrado.");
            }

            var usuario = new Usuario
            {
                NombreUsuario = nombre,
                Correo = correo,
                Activo = true,
                FechaRegistro = DateTime.UtcNow
            };
            usuario.PasswordHash = hasher.HashPassword(usuario, datos.Contrasena);
            unidad.TUsuario.Insertar(usuario);
            unidad.Completar();
            return Respuesta<TUsuario>.Ok(mapper.Map<TUsuario>(usuario));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al registrar usuario.");
            unidad.Rollback();
            return Respuesta<TUsuario>.Fail("No se pudo registrar el usuario.");
        }
    }

    public Respuesta<TUsuario> Autenticar(TLoginUsuario datos)
    {
        try
        {
            var usuario = unidad.TUsuario.ObtenerEntidad(x => x.NombreUsuario == datos.NombreUsuario).Data;
            if (usuario == null ||
                !usuario.Activo ||
                hasher.VerifyHashedPassword(
                usuario,
                usuario.PasswordHash,
                datos.Contrasena) == PasswordVerificationResult.Failed)
            {
                return Respuesta<TUsuario>.Fail("Credenciales inválidas.");
            }

            return Respuesta<TUsuario>.Ok(mapper.Map<TUsuario>(usuario));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al autenticar.");
            return Respuesta<TUsuario>.Fail("No se pudo iniciar sesión.");
        }
    }
}
