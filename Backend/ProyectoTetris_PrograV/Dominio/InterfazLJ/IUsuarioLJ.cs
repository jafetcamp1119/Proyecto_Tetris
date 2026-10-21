using System;
using System.Collections.Generic;
using System.Text;
using Dominio.EntidadesTipadas;
using Utilitarios;

namespace Dominio.InterfazLJ
{
    public interface IUsuarioLJ
    {
        Respuesta<TUsuario> Insertar(TUsuario usuario);

        Respuesta<TUsuario> Modificar(TUsuario usuario);
        Respuesta<bool> Eliminar(TUsuario usuario);
        Respuesta<IEnumerable<TUsuario>> Obtener(TUsuario usuario);
        Respuesta<TUsuario> Buscar(TUsuario usuario);
        Respuesta<IEnumerable<TUsuario>> Listar();
    }
}
