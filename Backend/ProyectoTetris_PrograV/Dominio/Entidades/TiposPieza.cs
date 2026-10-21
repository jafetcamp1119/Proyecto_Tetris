using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class TiposPieza
{
    public int TipoPiezaId { get; set; }

    public string Nombre { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public bool EsEspecial { get; set; }

    public bool EliminaColumna { get; set; }

    public string? Descripcion { get; set; }

    public bool Activa { get; set; }

    public virtual ICollection<PiezasJugador> PiezasJugadors { get; set; } = new List<PiezasJugador>();

    public virtual ICollection<RecompensasPunto> RecompensasPuntos { get; set; } = new List<RecompensasPunto>();
}
