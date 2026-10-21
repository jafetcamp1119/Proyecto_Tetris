using Dominio.EntidadesTipadas;
using System;
using System.Collections.Generic;
using System.Text;
using Utilitarios;

namespace Dominio.InterfazLJ
{
    public interface ITurnoLJ
    {
        Respuesta<TTurno> Insertar(TTurno turno);

        Respuesta<TTurno> Modificar(TTurno turno);
        Respuesta<bool> Eliminar(TTurno turno);
        Respuesta<IEnumerable<TTurno>> Obtener(TTurno turno);
        Respuesta<TTurno> Buscar(TTurno turno);
        Respuesta<IEnumerable<TTurno>> Listar();
    }
}
