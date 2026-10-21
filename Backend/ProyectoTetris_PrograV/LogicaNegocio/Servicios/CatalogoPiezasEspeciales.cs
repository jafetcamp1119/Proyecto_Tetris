using Dominio.InterfazAD;
using Dominio.InterfazJuego;
using Dominio.Juego;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Utilitarios;

namespace LogicaJuego.Servicios;

// Lee de TiposPieza los tipos especiales activos. Es un servicio compartido (singleton), por eso
// abre un alcance corto para obtener el DbContext y no lo retiene.
public class CatalogoPiezasEspeciales : ICatalogoPiezasEspeciales
{
    private readonly IServiceScopeFactory alcances;
    private readonly ILogger<CatalogoPiezasEspeciales> logger;

    public CatalogoPiezasEspeciales(IServiceScopeFactory alcances, ILogger<CatalogoPiezasEspeciales> logger)
    {
        this.alcances = alcances;
        this.logger = logger;
    }

    // TiposPieza no tiene columna EliminaFila. Un tipo elimina fila cuando su código contiene "FILA"
    // (por ejemplo ELIMINA_FILA, creado por docs/sql/02_pieza_elimina_fila.sql).
    private static bool EsEliminaFila(string codigo)
    {
        return codigo.Contains("FILA", StringComparison.OrdinalIgnoreCase);
    }

    public Respuesta<TipoPiezaEspecial[]> ObtenerActivas()
    {
        try
        {
            using var alcance = alcances.CreateScope();
            var unidad = alcance.ServiceProvider.GetRequiredService<IUnidadTrabajoEF>();
            var consulta = unidad.TTipoPieza.Buscar(tipo => tipo.EsEspecial && tipo.Activa);

            if (!consulta.Success)
            {
                return Respuesta<TipoPiezaEspecial[]>.Fail(consulta.Error ?? "Error al consultar los tipos de pieza.");
            }

            var tipos = consulta.Data!
                .Select(tipo => new TipoPiezaEspecial(
                    tipo.TipoPiezaId,
                    tipo.Codigo,
                    tipo.Nombre,
                    tipo.EliminaColumna,
                    EsEliminaFila(tipo.Codigo)))
                .ToArray();

            return Respuesta<TipoPiezaEspecial[]>.Ok(tipos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudieron leer los tipos de pieza especial.");
            return Respuesta<TipoPiezaEspecial[]>.Fail("No se pudieron consultar los tipos de pieza especial.");
        }
    }
}
