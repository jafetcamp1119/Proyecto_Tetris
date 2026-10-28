namespace Dominio.Entidades;

public partial class ConexionesJugador
{
    public int ConexionId
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

    public string EstadoConexion
        {
            get;
            set;
        } = null!;

    public DateTime FechaConexion
    {
        get;
            set;
    }

    public DateTime? FechaDesconexion
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
