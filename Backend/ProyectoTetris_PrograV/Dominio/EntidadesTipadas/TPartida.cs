// Datos de una partida persistida para los servicios de mantenimiento.
namespace Dominio.EntidadesTipadas
{
    // Datos de una partida almacenada que utilizan los servicios de mantenimiento.
    public class TPartida
    {
        public int PartidaId
        {
            get;
            set;
        }

        public int CreadorId
        {
            get;
            set;
        }

        public string? CodigoSala
        {
            get;
            set;
        }

        public string TipoPartida
        {
            get;
            set;
        } = null!;

        public bool EsPrivada
        {
            get;
            set;
        }

        public int CantidadMinimaJugadores
        {
            get;
            set;
        }

        public int CantidadMaximaJugadores
        {
            get;
            set;
        }

        public string Estado
        {
            get;
            set;
        } = null!;

        public DateTime FechaCreacion
        {
            get;
            set;
        }

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

        public int? GanadorId
        {
            get;
            set;
        }
    }
}
