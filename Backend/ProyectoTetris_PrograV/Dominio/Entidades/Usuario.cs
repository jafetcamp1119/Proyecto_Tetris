using System;
using System.Collections.Generic;

namespace Dominio.Entidades;

public partial class Usuario
{
    public int UsuarioId { get; set; }

    public string NombreUsuario { get; set; } = null!;

    public string? Correo { get; set; }

    public string? PasswordHash { get; set; }

    public string NombreUsuarioNormalizado { get; set; } = "";

    public DateTime FechaRegistro { get; set; }

    public bool Activo { get; set; }

    public string? GoogleSub { get; set; }
    public string? AvatarGoogle { get; set; }
    public string? HashRecuperacion { get; set; }
    public DateTime? RecuperacionExpiraEnUtc { get; set; }

    public virtual ICollection<BloquesTablero> BloquesTableros { get; set; } = new List<BloquesTablero>();

    public virtual ICollection<ConexionesJugador> ConexionesJugadors { get; set; } = new List<ConexionesJugador>();

    public virtual ConfiguracionJugador? ConfiguracionJugador { get; set; }

    public virtual ICollection<EliminacionesBloque> EliminacionesBloqueUsuarioAfectados { get; set; } = new List<EliminacionesBloque>();

    public virtual ICollection<EliminacionesBloque> EliminacionesBloqueUsuarioAtacantes { get; set; } = new List<EliminacionesBloque>();

    public virtual EstadisticasJugador? EstadisticasJugador { get; set; }

    public virtual ICollection<EventosPartidum> EventosPartida { get; set; } = new List<EventosPartidum>();

    public virtual ICollection<ParticipantesPartidum> ParticipantesPartida { get; set; } = new List<ParticipantesPartidum>();

    public virtual ICollection<Partida> PartidaCreadors { get; set; } = new List<Partida>();

    public virtual ICollection<Partida> PartidaGanadors { get; set; } = new List<Partida>();

    public virtual ICollection<PausasPartidum> PausasPartida { get; set; } = new List<PausasPartidum>();

    public virtual Perfile? Perfile { get; set; }

    public virtual ICollection<PiezasJugador> PiezasJugadors { get; set; } = new List<PiezasJugador>();

    public virtual ICollection<Resultado> Resultados { get; set; } = new List<Resultado>();

    public virtual ICollection<SeleccionesRecompensa> SeleccionesRecompensas { get; set; } = new List<SeleccionesRecompensa>();

    public virtual ICollection<TablerosPartidum> TablerosPartida { get; set; } = new List<TablerosPartidum>();

    public virtual ICollection<TurnosPartidum> TurnosPartida { get; set; } = new List<TurnosPartidum>();
}
