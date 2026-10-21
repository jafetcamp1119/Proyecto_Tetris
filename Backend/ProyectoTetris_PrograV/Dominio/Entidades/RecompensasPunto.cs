using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class RecompensasPunto
{
    public int RecompensaId { get; set; }

    public string Nombre { get; set; } = null!;

    public int PuntosRequeridos { get; set; }

    public int? TipoPiezaId { get; set; }

    public string? Descripcion { get; set; }

    public bool Activa { get; set; }

    public virtual ICollection<SeleccionesRecompensa> SeleccionesRecompensas { get; set; } = new List<SeleccionesRecompensa>();

    public virtual TiposPieza? TipoPieza { get; set; }
}
