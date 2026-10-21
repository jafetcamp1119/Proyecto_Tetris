using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class BloquesTablero
{
    public long BloqueId { get; set; }

    public int TableroId { get; set; }

    public int UsuarioId { get; set; }

    public long? PiezaJugadorId { get; set; }

    public int Fila { get; set; }

    public int Columna { get; set; }

    public bool Activo { get; set; }

    public DateTime FechaColocacion { get; set; }

    public virtual PiezasJugador? PiezaJugador { get; set; }

    public virtual TablerosPartidum Tablero { get; set; } = null!;

    public virtual Usuario Usuario { get; set; } = null!;
}
