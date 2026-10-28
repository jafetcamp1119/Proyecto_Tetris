// Guarda el sorteo de forma y especial para no repetirlo al mostrar o transferir la pieza.
namespace Dominio.Juego;

public sealed record PiezaPreparada(int TipoPieza, TipoPiezaEspecial? Especial);
