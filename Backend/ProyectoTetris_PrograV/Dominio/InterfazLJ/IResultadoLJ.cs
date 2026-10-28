// Define el mantenimiento de resultados y las consultas de historial y ranking.
using Dominio.EntidadesTipadas;

using Utilitarios;

namespace Dominio.InterfazLJ
{
    public interface IResultadoLJ
    {
        Respuesta<IEnumerable<TResultado>> ObtenerPorUsuario(int usuarioId);

        Respuesta<IEnumerable<TRanking>> Ranking();

        Respuesta<TResultado> Insertar(TResultado resultado);

        Respuesta<TResultado> Modificar(TResultado resultado);

        Respuesta<bool> Eliminar(TResultado resultado);

        Respuesta<IEnumerable<TResultado>> Obtener(TResultado resultado);

        Respuesta<TResultado> Buscar(TResultado resultado);

        Respuesta<IEnumerable<TResultado>> Listar();
    }
}
