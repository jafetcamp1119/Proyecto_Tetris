// Datos de preferencias persistentes del usuario, separados de la entidad de SQL.
namespace Dominio.EntidadesTipadas
{
    // Preferencias persistentes del jugador para los servicios de configuración.
    public class TConfiguracion
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
    }
}
