using System.Linq.Expressions;

using AutoMapper;

using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;

using Microsoft.Extensions.Logging;

namespace LogicaJuego.Implementacions;

// Mantiene los registros de participantes de una partida mediante BaseLJ.
public class ParticipanteLJ : BaseLJ<ParticipantesPartidum, TParticipante>, IParticipanteLJ
{
    public ParticipanteLJ(
        IUnidadTrabajoEF unidad,
        IMapper mapper,
        ILogger<ParticipanteLJ> logger) : base(
        unidad,
        unidad.TParticipante,
        mapper,
        logger)
    {
    }

    protected override Expression<Func<ParticipantesPartidum, bool>> PorId(TParticipante datos)
    {
        return x => x.ParticipanteId == datos.ParticipanteId;
    }
}
