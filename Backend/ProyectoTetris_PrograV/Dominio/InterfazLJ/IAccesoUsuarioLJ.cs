using Dominio.EntidadesTipadas;
using Utilitarios;

namespace Dominio.InterfazLJ;

public interface IAccesoUsuarioLJ
{
    Respuesta<TUsuario> Registrar(TRegistroUsuario datos);
    Respuesta<TUsuario> Autenticar(TLoginUsuario datos);
}
