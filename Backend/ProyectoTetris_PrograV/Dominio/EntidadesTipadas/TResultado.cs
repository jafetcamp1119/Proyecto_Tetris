using System;
using System.Collections.Generic;
using System.Text;

namespace Dominio.EntidadesTipadas
{
    public class TResultado
    {
        public int ResultadoId { get; set; }
        public int PartidaId { get; set; }
        public int UsuarioId { get; set; }
        public int Puntuacion { get; set; }
        public int LineasEliminadas { get; set; }
        public int NivelMaximo { get; set; }
        public int PosicionFinal { get; set; }
        public string EstadoFinal { get; set; } = null!;
        public bool EsGanador { get; set; }
        public DateTime FechaResultado { get; set; }
        public string Modalidad { get; set; } = "";
        public int CantidadParticipantes { get; set; }
        public bool EmpatePrimerLugar { get; set; }
    }
}
