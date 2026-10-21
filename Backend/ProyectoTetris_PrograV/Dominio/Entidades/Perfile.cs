using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class Perfile
{
    public int PerfilId { get; set; }

    public int UsuarioId { get; set; }

    public string? NombreVisible { get; set; }

    public string? Avatar { get; set; }

    public DateTime FechaActualizacion { get; set; }

    public virtual Usuario Usuario { get; set; } = null!;
}
