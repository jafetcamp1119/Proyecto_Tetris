using AccesoDatos;
using AccesoDatos.Contexto;
using AutoMapper;
using Dominio.DTO;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using LogicaJuego.Implementacions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class PruebasCuentasSql
{
    public static async Task<int> EjecutarAsync()
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__TetrisDB") ?? "";
        var conexion = new SqlConnectionStringBuilder(cadena);
        if (!conexion.InitialCatalog.StartsWith("TetrisValidacion_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Use exclusivamente una base TetrisValidacion_ de pruebas.");
        }
        var opciones = new DbContextOptionsBuilder<TetrisDBContext>().UseSqlServer(cadena).Options;
        await using var db = new TetrisDBContext(opciones);
        await db.Database.EnsureCreatedAsync();
        var mapper = new MapperConfiguration(c => c.AddProfile<AutoMapperProfile>(), NullLoggerFactory.Instance).CreateMapper();
        using var unidad = new UnidadTrabajoEF(db);
        var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance, CredencialesPrueba.Protector);
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var primero = acceso.Registrar(new TRegistroUsuario { NombreUsuario = "GoogleSQL_" + sufijo }).Data!;
        var segundo = acceso.Registrar(new TRegistroUsuario { NombreUsuario = "OtroSQL_" + sufijo }).Data!;
        var sub = "identidad-simulada-para-prueba-SQL-" + sufijo;
        db.EstadisticasJugadors.Add(new EstadisticasJugador { UsuarioId = primero.Usuario.UsuarioId, PuntuacionMaxima = 16000, PartidasGanadas = 4 });
        await db.SaveChangesAsync();
        Verificar(acceso.VincularGoogle(primero.Usuario.UsuarioId, sub, null).Success, "vinculación persistida en SQL");
        Verificar(acceso.VincularGoogle(segundo.Usuario.UsuarioId, sub, null).CodigoError == "GoogleEnOtroPerfil", "conflicto sin fusión de perfiles");
        db.ChangeTracker.Clear();
        var regreso = acceso.IngresarGoogle(sub);
        Verificar(regreso.Success && regreso.Data!.Usuario.UsuarioId == primero.Usuario.UsuarioId, "recuperación del mismo usuario en otro dispositivo");
        Verificar((await db.EstadisticasJugadors.SingleAsync(e => e.UsuarioId == primero.Usuario.UsuarioId)).PuntuacionMaxima == 16000, "récord conservado");
        var codigo = acceso.GenerarRecuperacion(primero.Usuario.UsuarioId).Data!;
        db.ChangeTracker.Clear();
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tareas = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await salida.Task;
            await using var contexto = new TetrisDBContext(opciones);
            using var trabajo = new UnidadTrabajoEF(contexto);
            var servicio = new AccesoUsuarioLJ(trabajo, mapper, NullLogger<AccesoUsuarioLJ>.Instance, CredencialesPrueba.Protector);
            try
            {
                return servicio.RecuperarConCodigo(codigo).Success;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        })).ToArray();
        salida.SetResult();
        var respuestas = await Task.WhenAll(tareas);
        Verificar(respuestas.Count(r => r) == 1, "un solo consumo entre recuperaciones simultáneas");
        Verificar(!acceso.RecuperarConCodigo(codigo).Success, "código consumido invalidado en SQL");
        Verificar((await db.Usuarios.SingleAsync(u => u.UsuarioId == primero.Usuario.UsuarioId)).HashRecuperacion == null, "código privado ausente de SQL después de utilizarse");
        Console.WriteLine("Pruebas de cuentas SQL correctas. La identidad Google fue simulada únicamente en esta prueba; no se contactó a Google.");
        Console.WriteLine("Base aislada conservada: " + conexion.InitialCatalog);
        return 0;
    }

    private static void Verificar(bool condicion, string nombre)
    {
        if (!condicion)
        {
            throw new Exception(nombre);
        }
        Console.WriteLine("OK SQL CUENTAS: " + nombre);
    }
}
