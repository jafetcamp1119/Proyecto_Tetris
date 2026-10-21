using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class EventosPartidum
{
    public long EventoId { get; set; }

    public int PartidaId { get; set; }

    public int? UsuarioId { get; set; }

    public string TipoEvento { get; set; } = null!;

    public string? DatosEvento { get; set; }

    public DateTime FechaEvento { get; set; }

    public virtual Partida Partida { get; set; } = null!;

    public virtual Usuario? Usuario { get; set; }
}
