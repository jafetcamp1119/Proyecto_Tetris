using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Tetris.Presentacion;

if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException("El iniciador de Visual Studio requiere Windows."); }
if (args.Contains("--hijo-prueba")) { await Task.Delay(30000); return; }
if (args.Contains("--padre-prueba")) { PruebasIniciador.CrearPadreDePrueba(args[1]); return; }
if (args.Contains("--pruebas")) { await PruebasIniciador.EjecutarAsync(); return; }
var repositorio = RutasPresentacion.BuscarRepositorio();
var temporal = Path.Combine(Path.GetTempPath(), "TetrisPresentacion", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporal);
var archivoEstado = Path.Combine(temporal, "estado.json");
using var iniciador = new IniciadorSistema();
if (args.Contains("--confirmacion-prueba-interna"))
{
    iniciador.ProbarConfirmacion(repositorio, archivoEstado);
    Environment.ExitCode = await iniciador.EsperarAsync();
    return;
}
if (args.Contains("--verificar"))
{
    iniciador.Iniciar(repositorio, archivoEstado, true);
    Environment.ExitCode = await iniciador.EsperarAsync();
    return;
}
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();
app.Use(async (contexto, siguiente) =>
{
    contexto.Response.Headers.CacheControl = "no-store";
    contexto.Response.Headers.XContentTypeOptions = "nosniff";
    contexto.Response.Headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self'; style-src 'self'; script-src 'self'; frame-ancestors 'none'";
    await siguiente();
});
var panel = Path.Combine(repositorio, "frontend", "tetris-app", "presentacion");
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(panel) });
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(panel) });
app.MapGet("/estado", () => {
    if (args.Contains("--vista-previa"))
    {
        return Results.Json(new { estado = "preparando", mensaje = "VISTA PREVIA LOCAL. No se inicio API, Cloudflare ni Firebase.", actualizadoUtc = DateTime.UtcNow });
    }
    return Results.Json(EstadoPanel.Leer(archivoEstado));
});
app.MapGet("/logo.png", () => Results.File(Path.Combine(repositorio, "frontend", "tetris-app", "src", "assets", "icon", "tetris-logo-v2.png"), "image/png"));
app.Lifetime.ApplicationStopping.Register(iniciador.Dispose);
await app.StartAsync();
var direccionPanel = app.Urls.Single();
Console.WriteLine("Panel local de presentacion: " + direccionPanel);
if (args.Contains("--vista-previa"))
{
    File.WriteAllText(archivoEstado, System.Text.Json.JsonSerializer.Serialize(new {
        estado = "preparando", mensaje = "VISTA PREVIA LOCAL. No se inicio API, Cloudflare ni Firebase.", actualizadoUtc = DateTime.UtcNow
    }));
}
else
{
    iniciador.Iniciar(repositorio, archivoEstado, false);
    _ = VigilarIniciadorAsync();
}
if (!args.Contains("--sin-navegador"))
{
    Process.Start(new ProcessStartInfo(direccionPanel) { UseShellExecute = true });
}
_ = VigilanciaPadre.VigilarAsync(app.Lifetime.StopApplication, app.Lifetime.ApplicationStopping);
await app.WaitForShutdownAsync();

async Task VigilarIniciadorAsync()
{
    await iniciador.EsperarAsync();
    iniciador.Dispose();
    EstadoPanel.RegistrarFinIniciador(archivoEstado);
}
