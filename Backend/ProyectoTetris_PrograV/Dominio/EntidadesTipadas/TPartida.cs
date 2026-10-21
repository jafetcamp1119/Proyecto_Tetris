using System;
using System.Collections.Generic;
using System.Text;

namespace Dominio.EntidadesTipadas
{
    public class TPartida
    {
        public int PartidaId { get; set; }
        public int CreadorId { get; set; }
        public string? CodigoSala { get; set; }
        public string TipoPartida { get; set; } = null!;
        public bool EsPrivada { get; set; }
        public int CantidadMinimaJugadores { get; set; }
        public int CantidadMaximaJugadores { get; set; }
        public string Estado { get; set; } = null!;
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int? GanadorId { get; set; }
    }
}
