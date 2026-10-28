namespace Dominio.EntidadesTipadas;

// Conserva los datos públicos del usuario para los clientes anteriores y agrega el token móvil.
public class TAutenticacion : TUsuario
{
    public string? CodigoRecuperacionNuevo { get; set; }
    public bool CodigoRecuperacionPorConfirmar { get; set; }
    public Guid? SalaActivaId { get; set; }
    public string Token { get; set; } = "";
    public string TipoToken { get; set; } = "Bearer";
    public DateTime ExpiraEnUtc { get; set; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Credencial { get; set; }
    public DateTime CredencialExpiraEnUtc { get; set; }
}
