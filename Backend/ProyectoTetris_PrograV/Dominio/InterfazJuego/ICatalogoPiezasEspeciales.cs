using Dominio.Juego;

using Utilitarios;

namespace Dominio.InterfazJuego;

// Entrega los tipos de pieza especial habilitados por la configuración administrativa (SQL Server).
// Se consulta una vez al configurar una sala, nunca durante la partida.
public interface ICatalogoPiezasEspeciales
{
    Respuesta<TipoPiezaEspecial[]> ObtenerActivas();
}
