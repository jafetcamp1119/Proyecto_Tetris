using Dominio.Entidades;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dominio.EntidadesTipadas
{
    public class TTurno
    {
        public long TurnoId { get; set; }
        public int PartidaId { get; set; }
        public int UsuarioId { get; set; }
        public int NumeroTurno { get; set; }
        public string Estado { get; set; } = null!;
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public bool FuePausado { get; set; }
    }
}
