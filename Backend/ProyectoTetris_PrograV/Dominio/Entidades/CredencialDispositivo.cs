namespace Dominio.Entidades;

// Representa una credencial de acceso de un dispositivo.
// SQL guarda su huella, fechas y propietario; no guarda el secreto entregado al jugador.
public class CredencialDispositivo
{
    public Guid CredencialDispositivoId
    {
        get;
            set;
    }

    public int UsuarioId
    {
        get;
            set;
    }

    public Usuario Usuario
        {
            get;
            set;
        } = null!;

    public string HashCredencial
        {
            get;
            set;
        } = "";

    public DateTime FechaCreacionUtc
    {
        get;
            set;
    }

    public DateTime ExpiraEnUtc
    {
        get;
            set;
    }

    public DateTime? RevocadaEnUtc
    {
        get;
            set;
    }
}
