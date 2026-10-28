using Dominio.Juego;

namespace LogicaJuego.Motor;

// Decide qué pieza sale a continuación. Concentra todo el azar del juego para poder probarlo
// con una semilla fija. El sorteo ocurre una sola vez por pieza, en el servidor.
public class GeneradorPiezas
{
    private readonly Random azar;

    public GeneradorPiezas() : this(Random.Shared)
    {
    }

    public GeneradorPiezas(Random azar)
    {
        this.azar = azar;
    }

    public PiezaPreparada PrepararPieza(ConfiguracionPiezas configuracion)
    {
        return new PiezaPreparada(SortearForma(configuracion.FormasPermitidas), SortearEspecial(configuracion));
    }

    public int SortearForma(int cantidadFormas)
    {
        return azar.Next(cantidadFormas);
    }

    // Sortea solo entre las piezas normales que el creador dejó habilitadas.
    public int SortearForma(IReadOnlyList<int> formasPermitidas)
    {
        var posicion = azar.Next(formasPermitidas.Count);
        return formasPermitidas[posicion];
    }

    // Devuelve el tipo especial de la nueva pieza, o null si debe ser una pieza normal.
    // La probabilidad se evalúa de forma independiente en cada pieza: con 50 % no hay garantía
    // de que exactamente la mitad salgan especiales.
    public TipoPiezaEspecial? SortearEspecial(ConfiguracionPiezas configuracion)
    {
        var hayTipos = configuracion.TiposDisponibles.Count > 0;

        if (!configuracion.EspecialesActivas || !hayTipos)
        {
            return null;
        }

        var sorteo = azar.Next(100);
        var esEspecial = sorteo < configuracion.ProbabilidadEspecial;

        if (!esEspecial)
        {
            return null;
        }

        // Todos los tipos habilitados tienen la misma probabilidad.
        var posicion = azar.Next(configuracion.TiposDisponibles.Count);
        return configuracion.TiposDisponibles[posicion];
    }
}
