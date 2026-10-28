// Respuesta pública de ingreso: usuario, JWT, vencimiento y datos de recuperación de esta operación.
namespace Dominio.EntidadesTipadas;

// Conserva los datos públicos del usuario para los clientes anteriores y agrega el token móvil.
// Datos públicos de la autenticación que recibe la aplicación.
public class TAutenticacion : TUsuario
{
    public string? CodigoRecuperacionNuevo
    {
        get;
            set;
    }

    public bool CodigoRecuperacionPorConfirmar
    {
        get;
            set;
    }

    public Guid? SalaActivaId
    {
        get;
            set;
    }

    public string Token
        {
            get;
            set;
        } = "";

    public string TipoToken
        {
            get;
            set;
        } = "Bearer";

    public DateTime ExpiraEnUtc
    {
        get;
            set;
    }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Credencial
    {
        get;
            set;
    }

    public DateTime CredencialExpiraEnUtc
    {
        get;
            set;
    }
}
