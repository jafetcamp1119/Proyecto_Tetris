namespace Dominio.Entidades;

public partial class TurnosPartidum
{
    public long TurnoId
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

    public int NumeroTurno
    {
        get;
            set;
    }

    public string Estado
        {
            get;
            set;
        } = null!;

    public DateTime? FechaInicio
    {
        get;
            set;
    }

    public DateTime? FechaFin
    {
        get;
            set;
    }

    public bool FuePausado
    {
        get;
            set;
    }

    public virtual Partida Partida
        {
            get;
            set;
        } = null!;

    public virtual ICollection<PausasPartidum> PausasPartida
        {
            get;
            set;
        } = new List<PausasPartidum>();

    public virtual Usuario Usuario
        {
            get;
            set;
        } = null!;
}
