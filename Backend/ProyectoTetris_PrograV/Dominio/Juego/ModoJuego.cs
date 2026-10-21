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
