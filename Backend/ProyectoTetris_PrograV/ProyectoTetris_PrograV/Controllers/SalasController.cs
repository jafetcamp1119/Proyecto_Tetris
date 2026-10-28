using System.Security.Claims;

using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ProyectoTetris_PrograV.Servicios;

using Utilitarios;

namespace ProyectoTetris_PrograV.Controllers;

// Recibe las acciones HTTP de las salas y las envía a SalaLJ.
// Publica por SignalR el estado que el servidor aceptó.
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
    // Crea la sala con la identidad del JWT y las opciones validadas por SalaLJ.
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
    // Consulta una sala a nombre del usuario autenticado.
    public IActionResult Obtener(Guid id)
    {
        return Responder(salas.Obtener(id, UsuarioId));
    }

    [HttpGet("tipos-especiales")]
    // Entrega el catálogo de especiales activos para configurar una sala.
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
    // Entrega únicamente la reserva privada del usuario autenticado.
    public IActionResult SiguientePieza(Guid id)
    {
        // La identidad sale del JWT; no hay un parámetro para consultar a otro jugador.
        var respuesta = salas.ObtenerSiguientePieza(id, UsuarioId);
        return respuesta.Success ? Ok(respuesta) : BadRequest(respuesta);
    }

    [HttpPut("{id:guid}/piezas-especiales")]
    // Solicita cambiar los especiales antes del inicio y publica el estado aceptado.
    public async Task<IActionResult> ConfigurarPiezas(Guid id, TConfiguracionPiezas datos)
    {
        return await Publicar(salas.ConfigurarPiezas(id, UsuarioId, datos));
    }

    [HttpPut("{id:guid}/configuracion")]
    // Solicita cambiar la configuración completa y publica el estado aceptado.
    public async Task<IActionResult> ConfigurarSala(Guid id, TConfiguracionSala datos)
    {
        return await Publicar(salas.ConfigurarSala(id, UsuarioId, datos));
    }

    [HttpPost("{id:guid}/unirse")]
    // Solicita ingresar por ID a una sala pública.
    public async Task<IActionResult> Unirse(Guid id)
    {
        return await Publicar(salas.Unirse(id, UsuarioId, User.Identity!.Name!));
    }

    [HttpPost("codigo/{codigo}/unirse")]
    // Solicita ingresar con el código de invitación.
    public async Task<IActionResult> UnirsePorCodigo(string codigo)
    {
        return await Publicar(salas.UnirsePorCodigo(codigo, UsuarioId, User.Identity!.Name!));
    }

    [HttpPut("{id:guid}/listo")]
    // Cambia la confirmación de preparación de este participante.
    public async Task<IActionResult> Listo(Guid id, [FromBody] bool listo)
    {
        return await Publicar(salas.Preparar(id, UsuarioId, listo));
    }

    [HttpPost("{id:guid}/iniciar")]
    // Solicita al servidor iniciar la ronda como creador.
    public async Task<IActionResult> Iniciar(Guid id)
    {
        return await Publicar(salas.Iniciar(id, UsuarioId));
    }

    [HttpPost("{id:guid}/nueva-ronda")]
    // Solicita preparar otra ronda después de guardar los resultados.
    public async Task<IActionResult> NuevaRonda(Guid id)
    {
        return await Publicar(salas.NuevaRonda(id, UsuarioId));
    }

    [HttpPost("{id:guid}/movimientos")]
    // Envía el movimiento y su secuencia a las reglas del servidor.
    public async Task<IActionResult> Mover(Guid id, TMovimiento datos)
    {
        return await Publicar(salas.Mover(id, UsuarioId, datos));
    }

    [HttpPost("{id:guid}/pausa")]
    // Solicita una pausa del participante autenticado.
    public async Task<IActionResult> SolicitarPausa(Guid id)
    {
        return await Publicar(salas.SolicitarPausa(id, UsuarioId));
    }

    [HttpPost("{id:guid}/pausa/voto")]
    // Envía el voto del participante para la pausa en curso.
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
    // Solicita el abandono; SalaLJ decide si debe cerrarse la sala.
    public async Task<IActionResult> Abandonar(Guid id)
    {
        return await Publicar(salas.Abandonar(id, UsuarioId));
    }

    // Devuelve 200 para una acción aceptada o 400 con el error de negocio.
    private IActionResult Responder(Respuesta<TSala> respuesta)
    {
        if (respuesta.Success)
        {
            return Ok(respuesta);
        }

        return BadRequest(respuesta);
    }

    // Publica el estado por SignalR únicamente si la operación fue aceptada.
    private async Task<IActionResult> Publicar(Respuesta<TSala> respuesta)
    {
        if (respuesta.Success)
        {
            await publicador.PublicarEstadoAsync(respuesta.Data!);
        }

        return Responder(respuesta);
    }
}
