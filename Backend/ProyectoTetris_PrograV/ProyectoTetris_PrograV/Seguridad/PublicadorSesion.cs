using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.InterfazLJ;

using Utilitarios;

namespace ProyectoTetris_PrograV.Seguridad;

// Prepara el ingreso y retira las conexiones de sesiones que perdieron el control de la cuenta.
public sealed class PublicadorSesion(ServicioToken tokens, ConexionesSesion conexiones, ISalaLJ salas, IAccesoUsuarioLJ acceso)
{
    // Vuelve a comprobar el ingreso, cierra conexiones anteriores y genera el JWT.
    // También informa la sala activa para poder retomarla desde otro dispositivo.
    public async Task<TAutenticacion?> CrearAsync(TSesionJugador sesion)
    {
        using var exclusion = await ExclusionSesiones.TomarAsync(sesion.Usuario.UsuarioId, CancellationToken.None);
        // Un ingreso que perdio una carrera no puede retirar las conexiones del ganador.
        if (!acceso.SesionVigente(sesion.SesionId, sesion.Usuario.UsuarioId))
        {
            return null;
        }

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
