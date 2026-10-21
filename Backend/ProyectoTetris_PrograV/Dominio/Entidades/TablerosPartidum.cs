using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class TablerosPartidum
{
    public int TableroId { get; set; }

    public int PartidaId { get; set; }

    public int Filas { get; set; }

    public int Columnas { get; set; }

    public string Estado { get; set; } = null!;

    public bool EstaPausado { get; set; }

    public int? UsuarioPausaId { get; set; }

    public DateTime FechaActualizacion { get; set; }

    public virtual ICollection<BloquesTablero> BloquesTableros { get; set; } = new List<BloquesTablero>();

    public virtual Partida Partida { get; set; } = null!;

    public virtual Usuario? UsuarioPausa { get; set; }
}
