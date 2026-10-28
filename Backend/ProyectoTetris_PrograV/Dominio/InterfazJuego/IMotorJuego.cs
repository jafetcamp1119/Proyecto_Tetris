// Define la creación, avance y consulta de tableros que utiliza SalaLJ.
using Dominio.EntidadesTipadas;
using Dominio.Juego;

namespace Dominio.InterfazJuego;

public interface IMotorJuego
{
    TableroJuego CrearTablero();

    TableroJuego CrearTablero(ConfiguracionPiezas piezas);

    PasoJuego Mover(TableroJuego tablero, Movimiento movimiento);

    PasoJuego Avanzar(TableroJuego tablero);

    TTablero ObtenerEstado(TableroJuego tablero, int? usuarioId);
}
