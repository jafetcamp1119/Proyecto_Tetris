using AutoMapper;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LogicaJuego.Implementacions;

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
