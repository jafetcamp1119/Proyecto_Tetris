// Datos de un turno persistido, separados del turno activo de la sala.
namespace Dominio.EntidadesTipadas
{
    // Datos de un turno almacenado; el turno activo se controla en la sala.
    public class TTurno
    {
        public long TurnoId
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

        public int NumeroTurno
        {
            get;
            set;
        }

        public string Estado
        {
            get;
            set;
        } = null!;

        public DateTime? FechaInicio
        {
            get;
            set;
        }

        public DateTime? FechaFin
        {
            get;
            set;
        }

        public bool FuePausado
        {
            get;
            set;
        }
    }
}
