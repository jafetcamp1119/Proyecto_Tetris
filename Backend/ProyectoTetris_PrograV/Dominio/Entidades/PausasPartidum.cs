using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class PausasPartidum
{
    public long PausaId { get; set; }

    public int PartidaId { get; set; }

    public int UsuarioId { get; set; }

    public long? TurnoId { get; set; }

    public DateTime FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public bool Activa { get; set; }

    public virtual Partida Partida { get; set; } = null!;

    public virtual TurnosPartidum? Turno { get; set; }

    public virtual Usuario Usuario { get; set; } = null!;
}
