using AutoMapper;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using Utilitarios;

namespace LogicaJuego.Implementacions;

public class ResultadoLJ : BaseLJ<Resultado, TResultado>, IResultadoLJ
{
    public ResultadoLJ(
        IUnidadTrabajoEF unidad,
        IMapper mapper,
        ILogger<ResultadoLJ> logger) : base(
        unidad,
        unidad.TResultado,
        mapper,
        logger)
    {
    }

    protected override Expression<Func<Resultado, bool>> PorId(TResultado datos)
    {
        return x => x.ResultadoId == datos.ResultadoId;
    }

    public Respuesta<IEnumerable<TResultado>> ObtenerPorUsuario(int usuarioId)
    {
        return Ejecutar(() =>
        {
            var respuesta = Unidad.TResultado.Buscar(r => r.UsuarioId == usuarioId);
            if (!respuesta.Success)
            {
                return Respuesta<IEnumerable<TResultado>>.Fail(respuesta.Error ?? "Error al consultar resultados.");
            }

            var resultados = Mapper.Map<IEnumerable<TResultado>>(respuesta.Data)
                .OrderByDescending(r => r.FechaResultado)
                .ToArray();
            var ids = resultados.Select(r => r.PartidaId).ToArray();
            var partidas = Unidad.TPartida.Buscar(p => ids.Contains(p.PartidaId)).Data!.ToDictionary(p => p.PartidaId);
            var cantidades = Unidad.TParticipante.Buscar(p => ids.Contains(p.PartidaId)).Data!
                .GroupBy(p => p.PartidaId).ToDictionary(g => g.Key, g => g.Count());
            var ganadores = Unidad.TResultado.Buscar(r => ids.Contains(r.PartidaId) && r.EsGanador).Data!
                .GroupBy(r => r.PartidaId).ToDictionary(g => g.Key, g => g.Count());
            foreach (var resultado in resultados)
            {
                resultado.Modalidad = partidas.GetValueOrDefault(resultado.PartidaId)?.TipoPartida ?? "Desconocida";
                resultado.CantidadParticipantes = cantidades.GetValueOrDefault(resultado.PartidaId);
                resultado.EmpatePrimerLugar = resultado.EsGanador && ganadores.GetValueOrDefault(resultado.PartidaId) > 1;
            }
            return Respuesta<IEnumerable<TResultado>>.Ok(resultados);
        });
    }

    public Respuesta<IEnumerable<TRanking>> Ranking()
    {
        return Ejecutar(() =>
        {
            var respuesta = Unidad.TEstadistica.Listar();
            if (!respuesta.Success)
            {
                return Respuesta<IEnumerable<TRanking>>.Fail(respuesta.Error ?? "Error al consultar ranking.");
            }

            var ranking = respuesta.Data!
                .OrderByDescending(e => e.PuntuacionMaxima)
                .ThenBy(e => e.UsuarioId)
                .Take(100)
                .Select(e => new TRanking(e.UsuarioId, e.PuntuacionMaxima, e.PartidasJugadas, e.PartidasGanadas))
                .ToArray();
            return Respuesta<IEnumerable<TRanking>>.Ok(ranking);
        });
    }
}
