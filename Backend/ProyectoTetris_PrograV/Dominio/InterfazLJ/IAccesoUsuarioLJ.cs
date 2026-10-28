// Define registro, renovación, Google, recuperación y comprobación de sesiones.
using Dominio.EntidadesTipadas;

using Utilitarios;

namespace Dominio.InterfazLJ;

public interface IAccesoUsuarioLJ
{
    Respuesta<TSesionJugador> Registrar(TRegistroUsuario datos);

    Respuesta<TSesionJugador> Recuperar(TRecuperarSesion datos);

    Respuesta<TSesionJugador> Renovar(TRecuperarSesion datos);

    Respuesta<bool> NombreDisponible(string nombreUsuario);

    bool SesionVigente(Guid sesionId, int usuarioId);

    string? ErrorSesion(Guid sesionId, int usuarioId);

    void Revocar(Guid sesionId, int usuarioId);

    Respuesta<TUsuario> VincularGoogle(int usuarioId, string sub, string? avatar, Guid? sesionEsperada = null);

    Respuesta<TSesionJugador> IngresarGoogle(string sub);

    Respuesta<TSesionJugador> RegistrarGoogle(TRegistroUsuario datos, string sub, string? avatar);

    Respuesta<string> GenerarRecuperacion(int usuarioId, Guid? sesionEsperada = null);

    Respuesta<TSesionJugador> RecuperarConCodigo(string codigo);

    Respuesta<bool> ConfirmarRecuperacion(int usuarioId, Guid sesionId, string codigo);
}
