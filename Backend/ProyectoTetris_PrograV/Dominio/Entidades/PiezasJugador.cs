namespace Dominio.Entidades;

public partial class PiezasJugador
{
    public long PiezaJugadorId
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

    public int TipoPiezaId
    {
        get;
            set;
    }

    public bool Seleccionada
    {
        get;
            set;
    }

    public bool Utilizada
    {
        get;
            set;
    }

    public bool ObtenidaPorPuntos
    {
        get;
            set;
    }

    public int PuntosNecesarios
    {
        get;
            set;
    }

    public DateTime FechaAsignacion
    {
        get;
            set;
    }

    public DateTime? FechaUso
    {
        get;
            set;
    }

    public virtual ICollection<BloquesTablero> BloquesTableros
        {
            get;
            set;
        } = new List<BloquesTablero>();

    public virtual ICollection<EliminacionesBloque> EliminacionesBloques
        {
            get;
            set;
        } = new List<EliminacionesBloque>();

    public virtual Partida Partida
        {
            get;
            set;
        } = null!;

    public virtual TiposPieza TipoPieza
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
