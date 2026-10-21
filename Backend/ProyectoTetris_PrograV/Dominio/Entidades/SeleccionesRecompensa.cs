using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class SeleccionesRecompensa
{
    public long SeleccionId { get; set; }

    public int PartidaId { get; set; }

    public int UsuarioId { get; set; }

    public int RecompensaId { get; set; }

    public bool Confirmada { get; set; }

    public DateTime FechaSeleccion { get; set; }

    public virtual Partida Partida { get; set; } = null!;

    public virtual RecompensasPunto Recompensa { get; set; } = null!;

    public virtual Usuario Usuario { get; set; } = null!;
}
