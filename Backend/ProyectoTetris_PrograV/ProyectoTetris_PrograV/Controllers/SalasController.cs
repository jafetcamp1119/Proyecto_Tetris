using System.Security.Claims;
using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoTetris_PrograV.Servicios;
using Utilitarios;

namespace ProyectoTetris_PrograV.Controllers;

[Authorize, ApiController, Route("api/salas")]
public class SalasController : ControllerBase
{
    private readonly ISalaLJ salas;
    private readonly PublicadorSalas publicador;

    public SalasController(ISalaLJ salas, PublicadorSalas publicador)
    {
        this.salas = salas;
        this.publicador = publicador;
    }

    private int UsuarioId
    {
        get
        {
            return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        }
    }

    [HttpPost]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("CrearSala")]
    public IActionResult Crear(TCrearSala datos)
    {
        return Responder(salas.Crear(UsuarioId, User.Identity!.Name!, datos));
    }

    // Solo salas públicas en espera con espacio disponible; las privadas nunca aparecen.
    [HttpGet]
    public IActionResult ListarPublicas()
    {
        return Ok(Respuesta<TSalaPublica[]>.Ok(salas.ListarPublicas()));
    }

    [HttpGet("{id:guid}")]
    public IActionResult Obtener(Guid id)
    {
        return Responder(salas.Obtener(id, UsuarioId));
    }

    [HttpGet("tipos-especiales")]
    public IActionResult TiposEspeciales([FromServices] ICatalogoPiezasEspeciales catalogo)
    {
        var respuesta = catalogo.ObtenerActivas();

        if (!respuesta.Success)
        {
            return BadRequest(respuesta);
        }

        return Ok(respuesta);
    }

    [HttpGet("{id:guid}/siguiente-pieza")]
    public IActionResult SiguientePieza(Guid id)
    {
        // La identidad sale del JWT; no hay un parámetro para consultar a otro jugador.
        var respuesta = salas.ObtenerSiguientePieza(id, UsuarioId);
        return respuesta.Success ? Ok(respuesta) : BadRequest(respuesta);
    }

    [HttpPut("{id:guid}/piezas-especiales")]
    public async Task<IActionResult> ConfigurarPiezas(Guid id, TConfiguracionPiezas datos)
    {
        return await Publicar(salas.ConfigurarPiezas(id, UsuarioId, datos));
    }

    [HttpPut("{id:guid}/configuracion")]
    public async Task<IActionResult> ConfigurarSala(Guid id, TConfiguracionSala datos)
    {
        return await Publicar(salas.ConfigurarSala(id, UsuarioId, datos));
    }

    [HttpPost("{id:guid}/unirse")]
    public async Task<IActionResult> Unirse(Guid id)
    {
        return await Publicar(salas.Unirse(id, UsuarioId, User.Identity!.Name!));
    }

    [HttpPost("codigo/{codigo}/unirse")]
    public async Task<IActionResult> UnirsePorCodigo(string codigo)
    {
        return await Publicar(salas.UnirsePorCodigo(codigo, UsuarioId, User.Identity!.Name!));
    }

    [HttpPut("{id:guid}/listo")]
    public async Task<IActionResult> Listo(Guid id, [FromBody] bool listo)
    {
        return await Publicar(salas.Preparar(id, UsuarioId, listo));
    }

    [HttpPost("{id:guid}/iniciar")]
    public async Task<IActionResult> Iniciar(Guid id)
    {
        return await Publicar(salas.Iniciar(id, UsuarioId));
    }

    [HttpPost("{id:guid}/nueva-ronda")]
    public async Task<IActionResult> NuevaRonda(Guid id)
    {
        return await Publicar(salas.NuevaRonda(id, UsuarioId));
    }

    [HttpPost("{id:guid}/movimientos")]
    public async Task<IActionResult> Mover(Guid id, TMovimiento datos)
    {
        return await Publicar(salas.Mover(id, UsuarioId, datos));
    }

    [HttpPost("{id:guid}/pausa")]
    public async Task<IActionResult> SolicitarPausa(Guid id)
    {
        return await Publicar(salas.SolicitarPausa(id, UsuarioId));
    }

    [HttpPost("{id:guid}/pausa/voto")]
    public async Task<IActionResult> VotarPausa(Guid id, TVotoPausa voto)
    {
        return await Publicar(salas.VotarPausa(id, UsuarioId, voto.Aceptar));
    }

    // Solo para la partida individual; la pausa multijugador termina sola a los tres minutos.
    [HttpPost("{id:guid}/reanudar")]
    public async Task<IActionResult> Reanudar(Guid id)
    {
        return await Publicar(salas.Reanudar(id, UsuarioId));
    }

    [HttpPost("{id:guid}/abandonar")]
    public async Task<IActionResult> Abandonar(Guid id)
    {
        return await Publicar(salas.Abandonar(id, UsuarioId));
    }

    private IActionResult Responder(Respuesta<TSala> respuesta)
    {
        if (respuesta.Success)
        {
            return Ok(respuesta);
        }

        return BadRequest(respuesta);
    }

    private async Task<IActionResult> Publicar(Respuesta<TSala> respuesta)
    {
        if (respuesta.Success)
        {
            await publicador.PublicarEstadoAsync(respuesta.Data!);
        }

        return Responder(respuesta);
    }
}
