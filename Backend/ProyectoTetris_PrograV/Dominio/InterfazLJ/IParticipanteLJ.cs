using System;
using System.Collections.Generic;
using System.Text;
using Dominio.EntidadesTipadas;
using Utilitarios;

namespace Dominio.InterfazLJ
{
    public interface IParticipanteLJ
    {
        Respuesta<TParticipante> Insertar(TParticipante participante);

        Respuesta<TParticipante> Modificar(TParticipante participante);
        Respuesta<bool> Eliminar(TParticipante participante);
        Respuesta<IEnumerable<TParticipante>> Obtener(TParticipante participante);
        Respuesta<TParticipante> Buscar(TParticipante participante);
        Respuesta<IEnumerable<TParticipante>> Listar();
    }
}
