namespace Dominio.Entidades;

public partial class Resultado
{
    public int ResultadoId
    {
        get;
            set;
    }

    public int PartidaId
    {
        get;
            set;
    }

    public int UsuarioId
    {
        get;
            set;
    }

    public int Puntuacion
    {
        get;
            set;
    }

    public int LineasEliminadas
    {
        get;
            set;
    }

    public int NivelMaximo
    {
        get;
            set;
    }

    public int PosicionFinal
    {
        get;
            set;
    }

    public string EstadoFinal
        {
            get;
            set;
        } = null!;

    public bool EsGanador
    {
        get;
            set;
    }

    public DateTime FechaResultado
    {
        get;
            set;
    }

    public virtual Partida Partida
        {
            get;
            set;
        } = null!;

    public virtual Usuario Usuario
        {
            get;
            set;
        } = null!;
}
