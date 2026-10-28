// Nombres compartidos de modos, estados de sala y movimientos aceptados por el servidor.
namespace Dominio.Juego;

public enum ModoJuego
{
    Individual,
    Compartido,
    Competitivo
}

public enum Movimiento
{
    Izquierda,
    Derecha,
    Rotar,
    Bajar,
    Caer
}

public enum EstadoSala
{
    Esperando,
    EnCurso,
    Finalizada,
    Cancelada
}
