using AccesoDatos;
using AccesoDatos.Contexto;

using Dominio.DTO;
using Dominio.InterfazAD;
using Dominio.InterfazJuego;
using Dominio.InterfazLJ;
using Dominio.Juego;

using LogicaJuego;
using LogicaJuego.Implementacions;
using LogicaJuego.Motor;
using LogicaJuego.Servicios;

using Microsoft.EntityFrameworkCore;

using ProyectoTetris_PrograV;
using ProyectoTetris_PrograV.Hubs;
using ProyectoTetris_PrograV.Seguridad;
using ProyectoTetris_PrograV.Servicios;

using Utilitarios;

// Prepara la API y registra sus servicios. No ejecuta migraciones ni crea el esquema SQL.
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider(opciones =>
{
    opciones.ValidateScopes = true;
    opciones.ValidateOnBuild = true;
});
builder.Services.AgregarApiTetris();
builder.Services.AgregarConexionPublicada();

builder.Services.AgregarSeguridad(builder.Configuration, builder.Environment);
// Configura EF con la conexión privada y el proveedor SQL Server.
// Cada alcance recibe su contexto; las salas compartidas no lo retienen.
builder.Services.AddDbContext<TetrisDBContext>(opciones =>
{
    var conexion = builder.Configuration.GetConnectionString("TetrisDB");

    if (string.IsNullOrWhiteSpace(conexion))
    {
        throw new InvalidOperationException("Configure ConnectionStrings:TetrisDB.");
    }

    opciones.UseSqlServer(conexion);
});
builder.Services.AddScoped<IUnidadTrabajoEF, UnidadTrabajoEF>();
builder.Services.AddAutoMapper(configuracion =>
{
}, typeof(AutoMapperProfile).Assembly);
builder.Services.AddScoped<IUsuarioLJ, UsuarioLJ>();
builder.Services.AddScoped<IAccesoUsuarioLJ, AccesoUsuarioLJ>();
builder.Services.AddScoped<IConfiguracionLJ, ConfiguracionLJ>();
builder.Services.AddScoped<IPartidaLJ, PartidaLJ>();
builder.Services.AddScoped<IParticipanteLJ, ParticipanteLJ>();
builder.Services.AddScoped<ITurnoLJ, TurnoLJ>();
builder.Services.AddScoped<IResultadoLJ, ResultadoLJ>();
builder.Services.AddScoped<IRegistroPartidaLJ, RegistroPartidaLJ>();
// Las sesiones se comparten, pero no retienen un DbContext de las solicitudes HTTP.
builder.Services.AddSingleton<GeneradorPiezas>();
builder.Services.AddSingleton<IMotorJuego, MotorJuego>();
builder.Services.AddSingleton(
    builder.Configuration.GetSection("Pausas").Get<OpcionesPausa>() ?? new OpcionesPausa());
builder.Services.AddSingleton<ICatalogoPiezasEspeciales, CatalogoPiezasEspeciales>();
var opcionesEfectos = builder.Configuration.GetSection("Efectos").Get<OpcionesEfectos>() ?? new OpcionesEfectos();
opcionesEfectos.Validar();
builder.Services.AddSingleton(opcionesEfectos);
builder.Services.AddSingleton<ISalaLJ, SalaLJ>();
builder.Services.AddSingleton<PublicadorSalas>();
builder.Services.AddHostedService<RelojJuegoService>();
builder.Services.AddHostedService<GuardadoPartidasService>();
var app = builder.Build();
app.UseExceptionHandler(error =>
{
    error.Run(async contexto =>
    {
        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await contexto.Response.WriteAsJsonAsync(Respuesta<object>.Fail("Ocurrió un error al procesar la solicitud."));
    });
});
app.UsarConexionPublicada(app.Environment.IsDevelopment());
app.UseCors(ConfiguracionSeguridad.PoliticaCors);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<JuegoHub>("/hubs/juego", opciones =>
{
    opciones.CloseOnAuthenticationExpiration = true;
});
app.MapGet("/salud", () =>
{
    return Results.Ok(new
    {
        Estado = "Servidor Tetris activo"
    });
});
app.Run();


