using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.InterfazLJ;
using Utilitarios;

namespace ProyectoTetris_PrograV.Seguridad;

public sealed class PublicadorSesion(ServicioToken tokens, ConexionesSesion conexiones, ISalaLJ salas, IAccesoUsuarioLJ acceso)
{
    public async Task<TAutenticacion?> CrearAsync(TSesionJugador sesion)
    {
        using var exclusion = await ExclusionSesiones.TomarAsync(sesion.Usuario.UsuarioId, CancellationToken.None);
        // Un ingreso que perdio una carrera no puede retirar las conexiones del ganador.
        if (!acceso.SesionVigente(sesion.SesionId, sesion.Usuario.UsuarioId)) { return null; }
        await conexiones.CerrarAnterioresAsync(sesion.Usuario.UsuarioId, sesion.SesionId);
        var autenticacion = tokens.Crear(sesion.Usuario, sesion.SesionId);
        autenticacion.Credencial = sesion.Credencial;
        autenticacion.CredencialExpiraEnUtc = sesion.CredencialExpiraEnUtc;
        autenticacion.CodigoRecuperacionNuevo = sesion.CodigoRecuperacionNuevo;
        autenticacion.CodigoRecuperacionPorConfirmar = sesion.CodigoRecuperacionPorConfirmar;
        autenticacion.SalaActivaId = salas.BuscarActiva(sesion.Usuario.UsuarioId);
        return autenticacion;
    }
}
