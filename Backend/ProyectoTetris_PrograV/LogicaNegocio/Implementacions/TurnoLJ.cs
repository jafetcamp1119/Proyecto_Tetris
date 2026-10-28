using System.Linq.Expressions;

using AutoMapper;

using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;

using Microsoft.Extensions.Logging;

namespace LogicaJuego.Implementacions;

// Mantiene los registros de turnos mediante BaseLJ; el turno activo vive en la sala en memoria.
public class TurnoLJ : BaseLJ<TurnosPartidum, TTurno>, ITurnoLJ
{
    public TurnoLJ(
        IUnidadTrabajoEF unidad,
        IMapper mapper,
        ILogger<TurnoLJ> logger) : base(
        unidad,
        unidad.TTurno,
        mapper,
        logger)
    {
    }

    protected override Expression<Func<TurnosPartidum, bool>> PorId(TTurno datos)
    {
        return x => x.TurnoId == datos.TurnoId;
    }
}
