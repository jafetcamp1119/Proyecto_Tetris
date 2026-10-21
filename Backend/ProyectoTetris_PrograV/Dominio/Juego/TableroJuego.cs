namespace Dominio.Juego;

// Estado mutable exclusivo del motor. No es una entidad de EF ni un control visual.
public sealed class TableroJuego
{
    public int[][] Celdas { get; } = Enumerable.Range(0, 20).Select(_ => new int[10]).ToArray();
    public int TipoPieza { get; internal set; }
    public int Rotacion { get; internal set; }
    public int Fila { get; internal set; }
    public int Columna { get; internal set; }
    public int Puntuacion { get; internal set; }
    public int Lineas { get; internal set; }

    public int Nivel
    {
        get
        {
            return 1 + Lineas / 10;
        }
    }

    public bool Perdido { get; internal set; }

    // Reglas de piezas especiales de esta partida y tipo especial de la pieza activa (null si es normal).
    // Se guardan en el tablero para que una reconexión no vuelva a sortear la pieza.
    public ConfiguracionPiezas Piezas { get; internal set; } = ConfiguracionPiezas.SoloNormales;

    public TipoPiezaEspecial? PiezaEspecial { get; internal set; }

    // Qué ocurrió en la última acción del motor. El cliente lo usa para reproducir sonidos;
    // la secuencia sube con cada acción para que un mismo evento no se reproduzca dos veces.
    public long EventoSecuencia { get; internal set; }

    public EventoSonido UltimoEvento { get; internal set; }

    public int LineasUltimoEvento { get; internal set; }

    public string? EspecialUltimoEvento { get; internal set; }
}

// Hechos de una acción del motor que el frontend puede acompañar con un sonido.
[Flags]
public enum EventoSonido
{
    Ninguno = 0,
    Movimiento = 1,
    Rotacion = 2,
    Bloqueo = 4,
    LineasEliminadas = 8,
    EspecialActivada = 16,
    Derrota = 32
}

public readonly record struct PasoJuego(bool Cambio, bool PiezaFijada);
