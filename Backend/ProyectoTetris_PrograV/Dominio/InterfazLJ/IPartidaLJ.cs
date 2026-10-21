using Dominio.EntidadesTipadas;
using System;
using System.Collections.Generic;
using System.Text;
using Utilitarios;

namespace Dominio.InterfazLJ
{
    public interface IPartidaLJ
    {
        Respuesta<TPartida> Insertar(TPartida partida);

        Respuesta<TPartida> Modificar(TPartida partida);
        Respuesta<bool> Eliminar(TPartida partida);
        Respuesta<IEnumerable<TPartida>> Obtener(TPartida partida);
        Respuesta<TPartida> Buscar(TPartida partida);
        Respuesta<IEnumerable<TPartida>> Listar();
    }
}
