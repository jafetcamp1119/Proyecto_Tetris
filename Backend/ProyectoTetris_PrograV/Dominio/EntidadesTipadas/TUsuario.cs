// Datos públicos del usuario; no incluye las huellas privadas de sus credenciales.
namespace Dominio.EntidadesTipadas
{
    // Datos del usuario que pueden exponerse a los clientes sin entregar sus secretos.
    public class TUsuario
    {
        public int UsuarioId
        {
            get;
            set;
        }

        public string NombreUsuario
        {
            get;
            set;
        } = "";

        [System.Text.Json.Serialization.JsonIgnore]
        public string? Correo
        {
            get;
            set;
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public string PasswordHash
        {
            get;
            set;
        } = "";

        public DateTime FechaRegistro
        {
            get;
            set;
        }

        public bool Activo
        {
            get;
            set;
        }

        public bool GoogleVinculado
        {
            get;
            set;
        }

        public string? AvatarGoogle
        {
            get;
            set;
        }
    }
}
