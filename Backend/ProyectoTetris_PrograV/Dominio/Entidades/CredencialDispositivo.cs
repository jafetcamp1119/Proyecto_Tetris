namespace Dominio.Entidades;

public class CredencialDispositivo
{
    public Guid CredencialDispositivoId { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string HashCredencial { get; set; } = "";
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime ExpiraEnUtc { get; set; }
    public DateTime? RevocadaEnUtc { get; set; }
}
