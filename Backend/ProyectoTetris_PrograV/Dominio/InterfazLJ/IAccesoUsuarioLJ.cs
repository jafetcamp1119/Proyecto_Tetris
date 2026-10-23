using Dominio.EntidadesTipadas;
using Utilitarios;

namespace Dominio.InterfazLJ;

public interface IAccesoUsuarioLJ
{
    Respuesta<TSesionJugador> Registrar(TRegistroUsuario datos);
    Respuesta<TSesionJugador> Recuperar(TRecuperarSesion datos);
    Respuesta<bool> NombreDisponible(string nombreUsuario);
    bool SesionVigente(Guid sesionId, int usuarioId);
    void Revocar(Guid sesionId, int usuarioId);
    Respuesta<TUsuario> VincularGoogle(int usuarioId, string sub, string? avatar);
    Respuesta<TSesionJugador> IngresarGoogle(string sub);
    Respuesta<string> GenerarRecuperacion(int usuarioId);
    Respuesta<TSesionJugador> RecuperarConCodigo(string codigo);
}
