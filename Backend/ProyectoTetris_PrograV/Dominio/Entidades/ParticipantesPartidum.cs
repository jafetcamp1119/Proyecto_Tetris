namespace Dominio.Entidades;

public partial class ParticipantesPartidum
{
    public int ParticipanteId
    {
        get;
            set;
    }

    public int PartidaId
    {
        get;
            set;
    }

    public int UsuarioId
    {
        get;
            set;
    }

    public string Estado
        {
            get;
            set;
        } = null!;

    public int Puntuacion
    {
        get;
            set;
    }

    public int LineasEliminadas
    {
        get;
            set;
    }

    public int NivelActual
    {
        get;
            set;
    }

    public int? PosicionFinal
    {
        get;
            set;
    }

    public int? OrdenTurno
    {
        get;
            set;
    }

    public bool EsTurnoActual
    {
        get;
            set;
    }

    public bool Listo
    {
        get;
            set;
    }

    public DateTime FechaIngreso
    {
        get;
            set;
    }

    public DateTime? FechaEliminacion
    {
        get;
            set;
    }

    public virtual Partida Partida
        {
            get;
            set;
        } = null!;

    public virtual Usuario Usuario
        {
            get;
            set;
        } = null!;
}
