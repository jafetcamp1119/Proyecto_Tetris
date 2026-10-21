using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class Partida
{
    public int PartidaId { get; set; }

    public int CreadorId { get; set; }

    public string? CodigoSala { get; set; }

    public string TipoPartida { get; set; } = null!;

    public bool EsPrivada { get; set; }

    public int CantidadMinimaJugadores { get; set; }

    public int CantidadMaximaJugadores { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public int? GanadorId { get; set; }

    public virtual ICollection<ConexionesJugador> ConexionesJugadors { get; set; } = new List<ConexionesJugador>();

    public virtual Usuario Creador { get; set; } = null!;

    public virtual ICollection<EliminacionesBloque> EliminacionesBloques { get; set; } = new List<EliminacionesBloque>();

    public virtual ICollection<EventosPartidum> EventosPartida { get; set; } = new List<EventosPartidum>();

    public virtual Usuario? Ganador { get; set; }

    public virtual ICollection<ParticipantesPartidum> ParticipantesPartida { get; set; } = new List<ParticipantesPartidum>();

    public virtual ICollection<PausasPartidum> PausasPartida { get; set; } = new List<PausasPartidum>();

    public virtual ICollection<PiezasJugador> PiezasJugadors { get; set; } = new List<PiezasJugador>();

    public virtual ICollection<Resultado> Resultados { get; set; } = new List<Resultado>();

    public virtual ICollection<SeleccionesRecompensa> SeleccionesRecompensas { get; set; } = new List<SeleccionesRecompensa>();

    public virtual TablerosPartidum? TablerosPartidum { get; set; }

    public virtual ICollection<TurnosPartidum> TurnosPartida { get; set; } = new List<TurnosPartidum>();
}
