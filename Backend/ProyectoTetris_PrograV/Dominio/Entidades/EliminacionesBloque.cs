namespace Dominio.Entidades;

public partial class EliminacionesBloque
{
    public long EliminacionId
    {
        get;
            set;
    }

    public int PartidaId
    {
        get;
            set;
    }

    public int UsuarioAtacanteId
    {
        get;
            set;
    }

    public int? UsuarioAfectadoId
    {
        get;
            set;
    }

    public long? PiezaJugadorId
    {
        get;
            set;
    }

    public int? ColumnaAfectada
    {
        get;
            set;
    }

    public int CantidadBloquesEliminados
    {
        get;
            set;
    }

    public int PuntosObtenidos
    {
        get;
            set;
    }

    public DateTime FechaEliminacion
    {
        get;
            set;
    }

    public virtual Partida Partida
        {
            get;
            set;
        } = null!;

    public virtual PiezasJugador? PiezaJugador
    {
        get;
            set;
    }

    public virtual Usuario? UsuarioAfectado
    {
        get;
            set;
    }

    public virtual Usuario UsuarioAtacante
        {
            get;
            set;
        } = null!;
}
