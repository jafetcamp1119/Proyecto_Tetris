namespace Dominio.EntidadesTipadas;

// Conserva los datos públicos del usuario para los clientes anteriores y agrega el token móvil.
public class TAutenticacion : TUsuario
{
    public string Token { get; set; } = "";
    public string TipoToken { get; set; } = "Bearer";
    public DateTime ExpiraEnUtc { get; set; }
}
