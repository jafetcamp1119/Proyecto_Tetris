namespace Dominio.Entidades;

public partial class ConfiguracionJugador
{
    public int ConfiguracionId
    {
        get;
            set;
    }

    public int UsuarioId
    {
        get;
            set;
    }

    public bool MostrarControlesPantalla
    {
        get;
            set;
    }

    public bool PermitirPausa
    {
        get;
            set;
    }

    public bool SonidoActivo
    {
        get;
            set;
    }

    public virtual Usuario Usuario
        {
            get;
            set;
        } = null!;
}
