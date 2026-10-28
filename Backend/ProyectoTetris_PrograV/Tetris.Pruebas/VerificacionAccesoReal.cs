// Verifica acceso mediante la configuración real; no se ejecuta en la suite de memoria.
using AccesoDatos;
using AccesoDatos.Contexto;

using AutoMapper;

using Dominio.DTO;
using Dominio.EntidadesTipadas;

using LogicaJuego.Implementacions;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Comprobación explícita de la lógica de acceso sobre la base configurada.
// Todos los registros sintéticos se revierten; no borra usuarios existentes.
internal static class VerificacionAccesoReal
{
    public static async Task<int> EjecutarAsync()
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__TetrisDB");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException("Configure ConnectionStrings__TetrisDB explícitamente.");
        }

        var opciones = new DbContextOptionsBuilder<TetrisDBContext>().UseSqlServer(cadena).Options;
        await using var contexto = new TetrisDBContext(opciones);
        using var registroMensajes = LoggerFactory.Create(opcionesRegistro =>
        {
        });
        var configuracion = new MapperConfiguration(opcionesMapa => opcionesMapa.AddProfile<AutoMapperProfile>(), registroMensajes);
        var mapper = configuracion.CreateMapper();
        using var unidad = new UnidadTrabajoEF(contexto);
        var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance, CredencialesPrueba.Protector);
        var nombre = "Verificacion_" + Guid.NewGuid().ToString("N");
        var cantidadUsuarios = await contexto.Usuarios.CountAsync();
        await using var transaccion = await contexto.Database.BeginTransactionAsync();

        try
        {
            var registro = acceso.Registrar(new TRegistroUsuario { NombreUsuario = nombre });
            Verificar(registro.Success, "el backend registra solo con nickname en la base real");
            var sesion = registro.Data!;
            var usuario = await contexto.Usuarios.SingleAsync(jugador => jugador.UsuarioId == sesion.Usuario.UsuarioId);
            Verificar(usuario.Correo == null && usuario.PasswordHash == null, "no se inventan correos ni contraseñas");
            var duplicado = acceso.Registrar(new TRegistroUsuario { NombreUsuario = " " + nombre.ToLowerInvariant() + " " });
            Verificar(
                !duplicado.Success && duplicado.CodigoError == "NombreOcupado",
                "C# rechaza duplicados con mayúsculas y espacios"
            );
            var segundo = acceso.Registrar(new TRegistroUsuario { NombreUsuario = "Otro_" + Guid.NewGuid().ToString("N") });
            Verificar(segundo.Success, "varios usuarios pueden tener correo y contraseña NULL");
            var recuperada = acceso.Recuperar(new TRecuperarSesion { Credencial = sesion.Credencial! });
            Verificar(recuperada.Success && recuperada.Data!.Usuario.UsuarioId == usuario.UsuarioId,
                "la credencial permite recuperar el mismo perfil");
            Verificar(recuperada.Data!.Credencial == null, "la recuperación no publica el secreto otra vez");
            var renovada = acceso.Recuperar(new TRecuperarSesion { Credencial = sesion.Credencial! });
            Verificar(renovada.Success, "la sesión puede renovarse con la credencial");
            acceso.Revocar(sesion.SesionId, usuario.UsuarioId);
            Verificar(!acceso.SesionVigente(sesion.SesionId, usuario.UsuarioId), "la revocación invalida la sesión");

            // El espacio inicial evita que la restricción antigua del nombre oculte la nueva validación.
            var nombreDuplicado = " " + nombre.ToLowerInvariant() + " ";
            var indiceRechazoDuplicado = false;

            try
            {
                await contexto.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO dbo.Usuarios(NombreUsuario) VALUES({nombreDuplicado})");
            }
            catch (SqlException excepcion) when ((excepcion.Number == 2601 || excepcion.Number == 2627)
                && excepcion.Message.Contains("UX_Usuarios_NombreNormalizado", StringComparison.Ordinal))
            {
                indiceRechazoDuplicado = true;
            }

            Verificar(indiceRechazoDuplicado, "SQL rechaza duplicados aunque se omita la validación C#");
        }
        finally
        {
            await transaccion.RollbackAsync();
            contexto.ChangeTracker.Clear();
        }

        Verificar(await contexto.Usuarios.CountAsync() == cantidadUsuarios
            && !await contexto.Usuarios.AnyAsync(usuario => usuario.NombreUsuario == nombre),
            "transacción revertida: no quedan jugadores de prueba ni cambios en usuarios existentes");
        return 0;
    }

    private static void Verificar(bool condicion, string mensaje)
    {
        if (!condicion)
        {
            throw new InvalidOperationException(mensaje);
        }

        Console.WriteLine("OK BASE REAL: " + mensaje);
    }
}
