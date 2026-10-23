using AccesoDatos;
using AccesoDatos.Contexto;
using AutoMapper;
using Dominio.DTO;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using LogicaJuego.Implementacions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal static class PruebasIdentidadSql
{
    public static async Task<int> EjecutarAsync()
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__TetrisDB")
            ?? throw new InvalidOperationException("Falta la conexión a la base temporal.");
        var conexion = new SqlConnectionStringBuilder(cadena);
        if (!conexion.InitialCatalog.StartsWith("TetrisValidacion_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Estas pruebas solo permiten bases TetrisValidacion_.");
        }

        using var fabricaRegistro = LoggerFactory.Create(opciones => { });
        var configuracion = new MapperConfiguration(opciones => opciones.AddProfile<AutoMapperProfile>(), fabricaRegistro);
        var mapper = configuracion.CreateMapper();
        var opciones = new DbContextOptionsBuilder<TetrisDBContext>().UseSqlServer(cadena).Options;
        var nombre = "Simultaneo_" + Guid.NewGuid().ToString("N")[..10];
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tareas = Enumerable.Range(0, 8).Select(numero => Task.Run(async () =>
        {
            await salida.Task;
            await using var contexto = new TetrisDBContext(opciones);
            using var unidad = new UnidadTrabajoEF(contexto);
            var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance);
            var nickname = numero % 2 == 0 ? nombre.ToUpperInvariant() : " " + nombre.ToLowerInvariant() + " ";
            return acceso.Registrar(new TRegistroUsuario { NombreUsuario = nickname });
        })).ToArray();
        salida.SetResult();
        var respuestas = await Task.WhenAll(tareas);
        Verificar(respuestas.Count(respuesta => respuesta.Success) == 1, "un solo registro entre ocho dispositivos simultáneos");
        Verificar(respuestas.Count(respuesta => respuesta.CodigoError == "NombreOcupado") == 7, "los otros registros se convierten en conflicto");

        await using var db = new TetrisDBContext(opciones);
        using var unidadPrueba = new UnidadTrabajoEF(db);
        var servicio = new AccesoUsuarioLJ(unidadPrueba, mapper, NullLogger<AccesoUsuarioLJ>.Instance);
        var sesion = respuestas.Single(respuesta => respuesta.Success).Data!;
        var credencial = await db.Set<CredencialDispositivo>().SingleAsync(dato => dato.UsuarioId == sesion.Usuario.UsuarioId);
        Verificar(credencial.HashCredencial != sesion.Credencial && credencial.HashCredencial.Length == 64, "SQL solo conserva el hash");
        var usuario = await db.Usuarios.SingleAsync(dato => dato.UsuarioId == sesion.Usuario.UsuarioId);
        Verificar(usuario.Correo == null && usuario.PasswordHash == null, "sin correo ni contraseña en SQL");
        Verificar(!servicio.NombreDisponible(" " + nombre.ToLowerInvariant() + " ").Data, "normalización calculada de SQL coincide con consultas C#");
        Verificar(servicio.Recuperar(new TRecuperarSesion { Credencial = sesion.Credencial! }).Success, "recuperación real desde SQL");
        var otro = servicio.Registrar(new TRegistroUsuario { NombreUsuario = "Otro_" + nombre });
        Verificar(otro.Success, "varios correos NULL aceptados");
        credencial.FechaCreacionUtc = DateTime.UtcNow.AddDays(-2);
        credencial.ExpiraEnUtc = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        Verificar(!servicio.Recuperar(new TRecuperarSesion { Credencial = sesion.Credencial! }).Success, "credencial vencida rechazada");
        credencial.ExpiraEnUtc = DateTime.UtcNow.AddDays(1);
        await db.SaveChangesAsync();
        usuario.Activo = false;
        await db.SaveChangesAsync();
        Verificar(!servicio.Recuperar(new TRecuperarSesion { Credencial = sesion.Credencial! }).Success, "perfil inactivo rechazado");
        usuario.Activo = true;
        await db.SaveChangesAsync();
        servicio.Revocar(sesion.SesionId, usuario.UsuarioId);
        Verificar(!servicio.SesionVigente(sesion.SesionId, usuario.UsuarioId), "revocación persistida");

        // Prueba directamente el índice sin pasar por la comprobación previa de C#.
        var directo = new Usuario { NombreUsuario = nombre.ToLowerInvariant(), FechaRegistro = DateTime.UtcNow, Activo = true };
        db.Usuarios.Add(directo);
        try
        {
            await db.SaveChangesAsync();
            throw new InvalidOperationException("El índice permitió un duplicado.");
        }
        catch (DbUpdateException excepcion) when (excepcion.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        {
            db.ChangeTracker.Clear();
            Console.WriteLine("OK SQL IDENTIDAD: restricción única rechaza inserciones directas duplicadas.");
        }

        return 0;
    }

    private static void Verificar(bool condicion, string nombre)
    {
        if (!condicion)
        {
            throw new InvalidOperationException(nombre);
        }

        Console.WriteLine("OK SQL IDENTIDAD: " + nombre);
    }
}
