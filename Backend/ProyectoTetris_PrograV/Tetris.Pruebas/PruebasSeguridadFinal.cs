// Comprueba sesiones únicas, códigos y consultas parametrizadas sin abrir SQL.
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

using AccesoDatos.Contexto;

using AutoMapper;

using Dominio.Entidades;
using Dominio.EntidadesTipadas;

using LogicaJuego.Implementacions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class PruebasSeguridadFinal
{
    public static void Agregar(Action<string, Action> prueba, IMapper mapper)
    {
        void Verificar(bool condicion)
        {
            if (!condicion)
            {
                throw new Exception("Fallo un control de seguridad final.");
            }
        }
        prueba("Sesion unica: login desplaza, renovar no roba control y logout no revoca otra sesion", () =>
        {
            using var unidad = new UnidadMemoria();
            var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance, CredencialesPrueba.Protector);
            var original = acceso.Registrar(new TRegistroUsuario { NombreUsuario = "SesionUnica" }).Data!;
            var nueva = acceso.Recuperar(new TRecuperarSesion { Credencial = original.Credencial! }).Data!;
            Verificar(!acceso.SesionVigente(original.SesionId, original.Usuario.UsuarioId));
            Verificar(acceso.ErrorSesion(original.SesionId, original.Usuario.UsuarioId) == "SesionReemplazada");
            Verificar(acceso.SesionVigente(nueva.SesionId, nueva.Usuario.UsuarioId));
            Verificar(!acceso.Renovar(new TRecuperarSesion { Credencial = original.Credencial! }).Success);
            var renovada = acceso.Renovar(new TRecuperarSesion { Credencial = nueva.Credencial! }).Data!;
            Verificar(renovada.SesionId == nueva.SesionId);
            acceso.Revocar(original.SesionId, original.Usuario.UsuarioId);
            Verificar(acceso.SesionVigente(nueva.SesionId, nueva.Usuario.UsuarioId));
            Verificar(!acceso.GenerarRecuperacion(original.Usuario.UsuarioId, original.SesionId).Success);
            Verificar(!acceso.VincularGoogle(original.Usuario.UsuarioId, "otro-sub", null, original.SesionId).Success);
            acceso.Revocar(nueva.SesionId, nueva.Usuario.UsuarioId);
            Verificar(!acceso.SesionVigente(nueva.SesionId, nueva.Usuario.UsuarioId));
            Verificar(acceso.ErrorSesion(nueva.SesionId, nueva.Usuario.UsuarioId) == "SesionInvalida");
        });
        prueba("Ingresos simultaneos mantienen exactamente una sesion valida", () =>
        {
            using var unidad = new UnidadMemoria();
            var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance, CredencialesPrueba.Protector);
            var original = acceso.Registrar(new TRegistroUsuario { NombreUsuario = "Concurrente" }).Data!;
            var sesiones = new ConcurrentBag<TSesionJugador>();
            Parallel.For(
                0,
                8,
                _ => sesiones.Add(acceso.Recuperar(new TRecuperarSesion { Credencial = original.Credencial! }).Data!)
            );
            Verificar(sesiones.Count(s => acceso.SesionVigente(s.SesionId, s.Usuario.UsuarioId)) == 1);
        });
        prueba("Codigos cortos criptograficos: formato, unicidad y entradas maliciosas", () =>
        {
            var codigos = Enumerable.Range(0, 1000).Select(_ => CodigosRecuperacion.Crear()).ToArray();
            Verificar(codigos.Distinct().Count() == codigos.Length);
            Verificar(
                codigos.All(
                c => c.Length == 14 && c[4] == '-' && c[9] == '-' && CodigosRecuperacion.Normalizar(c)?.Length == 12
            )
            );

            foreach (var valor in new[]
            {
                "' OR 1=1 --",
                "<script>alert(1)</script>",
                "AAAA-AAAA-AAAA-",
                "OOOO-1111-IIII",
                "AAAA AAAA AAAA"
            })
            {
                Verificar(CodigosRecuperacion.Normalizar(valor) == null);
            }
        });
        prueba("Codigo legado conserva cuenta y entrega reemplazo corto al consumirlo", () =>
        {
            using var unidad = new UnidadMemoria();
            var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance, CredencialesPrueba.Protector);
            var cuenta = acceso.Registrar(new TRegistroUsuario { NombreUsuario = "Legado" }).Data!;
            var legado = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            unidad.Usuarios.Datos.Single().HashRecuperacion = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legado)));
            var recuperada = acceso.RecuperarConCodigo(legado).Data!;
            Verificar(
                recuperada.Usuario.UsuarioId == cuenta.Usuario.UsuarioId
                && recuperada.CodigoRecuperacionNuevo?.Length == 14
            );
            Verificar(!acceso.RecuperarConCodigo(recuperada.CodigoRecuperacionNuevo!).Success);
            var reintento = acceso.RecuperarConCodigo(legado).Data!;
            Verificar(
                !acceso.ConfirmarRecuperacion(recuperada.Usuario.UsuarioId, recuperada.SesionId, recuperada.CodigoRecuperacionNuevo!).Success
            );
            Verificar(
                acceso.ConfirmarRecuperacion(reintento.Usuario.UsuarioId, reintento.SesionId, reintento.CodigoRecuperacionNuevo!).Success
            );
            Verificar(
                acceso.ConfirmarRecuperacion(reintento.Usuario.UsuarioId, reintento.SesionId, reintento.CodigoRecuperacionNuevo!).Success
            );
            Verificar(!acceso.RecuperarConCodigo(legado).Success);
            Verificar(acceso.RecuperarConCodigo(reintento.CodigoRecuperacionNuevo!).Success);
        });
        prueba("EF parametriza nicknames maliciosos y protege concurrencia de sesion sin abrir SQL", () =>
        {
            var opciones = new DbContextOptionsBuilder<TetrisDBContext>().UseSqlServer("Server=localhost;Database=NoSeAbre;Integrated Security=True;TrustServerCertificate=True").Options;
            using var contexto = new TetrisDBContext(opciones);
            var nombre = "x'; DROP TABLE Usuarios;--";
            var sql = contexto.Set<Usuario>().Where(u => u.NombreUsuario == nombre).ToQueryString();
            var consulta = sql[sql.IndexOf("SELECT", StringComparison.Ordinal)..];
            Verificar(consulta.Contains('@') && consulta.Contains("WHERE") && !consulta.Contains("DROP TABLE"));
            Verificar(
                contexto.Model.FindEntityType(typeof(Usuario))!.FindProperty(nameof(Usuario.SesionActivaId))!.IsConcurrencyToken
            );
        });
    }
}
