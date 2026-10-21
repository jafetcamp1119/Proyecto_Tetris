using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class VistaRanking
{
    public int UsuarioId { get; set; }

    public string NombreUsuario { get; set; } = null!;

    public int PartidasJugadas { get; set; }

    public int PartidasGanadas { get; set; }

    public int PartidasPerdidas { get; set; }

    public int PuntuacionMaxima { get; set; }

    public long TotalPuntos { get; set; }

    public int LineasEliminadas { get; set; }

    public int NivelMaximoAlcanzado { get; set; }
}
