using AutoMapper;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LogicaJuego.Implementacions;

public class PartidaLJ : BaseLJ<Partida, TPartida>, IPartidaLJ
{
    public PartidaLJ(
        IUnidadTrabajoEF unidad,
        IMapper mapper,
        ILogger<PartidaLJ> logger) : base(
        unidad,
        unidad.TPartida,
        mapper,
        logger)
    {
    }

    protected override Expression<Func<Partida, bool>> PorId(TPartida datos)
    {
        return x => x.PartidaId == datos.PartidaId;
    }

    protected override string? Validar(TPartida datos)
    {
        if (!Enum.TryParse<Dominio.Juego.ModoJuego>(datos.TipoPartida, out var modo) || !Enum.IsDefined(modo))
        {
            return "Modo de juego inválido.";
        }

        var minimo = modo == Dominio.Juego.ModoJuego.Individual ? 1 : 4;
        if (datos.CantidadMinimaJugadores < minimo || datos.CantidadMaximaJugadores < datos.CantidadMinimaJugadores)
        {
            return "Cantidad de jugadores incompatible con el modo.";
        }

        return null;
    }
}
