// Datos de récord y partidas que se muestran en el ranking.
namespace Dominio.EntidadesTipadas;

public record TRanking(int UsuarioId, int PuntuacionMaxima, int PartidasJugadas, int PartidasGanadas);
