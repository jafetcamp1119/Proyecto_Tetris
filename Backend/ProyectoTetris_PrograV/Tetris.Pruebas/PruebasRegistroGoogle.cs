// Comprueba registro Google, comprobantes y accesos simultáneos.
using System.Text.Json;

using AutoMapper;

using Dominio.EntidadesTipadas;

using LogicaJuego.Implementacions;

using Microsoft.Extensions.Logging.Abstractions;

using ProyectoTetris_PrograV.Seguridad;

internal static class PruebasRegistroGoogle
{
    public static void Agregar(Action<string, Action> prueba, IMapper mapper)
    {
        prueba("Google nuevo registra atomico, conserva invitado ajeno y entra luego sin otro nickname", () =>
        {
            using var unidad = new UnidadMemoria();
            var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance, CredencialesPrueba.Protector);
            acceso.Registrar(new TRegistroUsuario { NombreUsuario = "Existente" });
            var registros = new RegistrosGoogle();
            var comprobante = ObtenerComprobante(registros.Crear("sub-verificado", null));
            Exigir(
                !registros.Completar(comprobante, new TRegistroUsuario { NombreUsuario = "existente" }, acceso).Success
            );
            Exigir(unidad.Usuarios.Datos[0].GoogleSub == null);
            var nueva = registros.Completar(comprobante, new TRegistroUsuario { NombreUsuario = "GoogleNuevo" }, acceso).Data!;
            Exigir(nueva.Usuario.GoogleVinculado && unidad.Usuarios.Datos.Count == 2);
            Exigir(!registros.Completar(comprobante, new TRegistroUsuario { NombreUsuario = "Otro" }, acceso).Success);
            var ingreso = acceso.IngresarGoogle("sub-verificado").Data!;
            Exigir(
                ingreso.Usuario.UsuarioId == nueva.Usuario.UsuarioId
                && ingreso.Usuario.NombreUsuario == "GoogleNuevo"
            );
            Exigir(!acceso.SesionVigente(nueva.SesionId, nueva.Usuario.UsuarioId));
        });
        prueba("Dos registros Google simultaneos crean una sola identidad y una sola sesion activa", () =>
        {
            using var unidad = new UnidadMemoria();
            var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance, CredencialesPrueba.Protector);
            var registros = new RegistrosGoogle();
            var comprobantes = Enumerable.Range(0, 2).Select(_ => ObtenerComprobante(registros.Crear("mismo-sub", null))).ToArray();
            var sesiones = new TSesionJugador[2];
            Parallel.For(
                0,
                2,
                i => sesiones[i] = registros.Completar(comprobantes[i], new TRegistroUsuario { NombreUsuario = "Nombre" + i }, acceso).Data!
            );
            Exigir(unidad.Usuarios.Datos.Count == 1);
            Exigir(sesiones.Count(s => acceso.SesionVigente(s.SesionId, s.Usuario.UsuarioId)) == 1);
        });
        prueba("Comprobantes de Google falsos, vencidos y reutilizados no autorizan registro", () =>
        {
            using var unidad = new UnidadMemoria();
            var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance, CredencialesPrueba.Protector);
            var reloj = new RelojPrueba();
            var registros = new RegistrosGoogle(reloj);
            var comprobante = ObtenerComprobante(registros.Crear("sub", null));
            Exigir(
                !registros.Completar(new string('A', 64), new TRegistroUsuario { NombreUsuario = "Falso" }, acceso).Success
            );
            reloj.Ahora = reloj.Ahora.AddMinutes(6);
            Exigir(
                !registros.Completar(comprobante, new TRegistroUsuario { NombreUsuario = "Vencido" }, acceso).Success
            );
            Exigir(unidad.Usuarios.Datos.Count == 0);
        });
    }

    private static string ObtenerComprobante(object respuesta)
    {
        return JsonSerializer.SerializeToElement(respuesta).GetProperty("ComprobanteRegistro").GetString()!;
    }

    private static void Exigir(bool condicion)
    {
        if (!condicion)
        {
            throw new Exception("Fallo el registro seguro Google.");
        }
    }

    private sealed class RelojPrueba : TimeProvider
    {
        public DateTimeOffset Ahora = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return Ahora;
        }
    }
}
