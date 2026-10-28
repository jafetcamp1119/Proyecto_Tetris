using System.Diagnostics;

namespace Tetris.Presentacion;

public sealed class IniciadorSistema : IDisposable
{
    private readonly GrupoProcesos grupo = new();
    private Process? proceso;

    public void Iniciar(string repositorio, string archivoEstado, bool verificar)
    {
        var opciones = CrearOpciones(repositorio, archivoEstado, "IniciarPresentacion.ps1");
        if (verificar) { opciones.ArgumentList.Add("-VerificarSolamente"); }
        IniciarProceso(opciones);
    }

    public void ProbarConfirmacion(string repositorio, string archivoEstado)
    {
        IniciarProceso(CrearOpciones(repositorio, archivoEstado, "pruebas/ConfirmacionConsola.ps1"));
    }

    private static ProcessStartInfo CrearOpciones(string repositorio, string archivoEstado, string nombreScript)
    {
        var script = Path.Combine(repositorio, "frontend", "tetris-app", "scripts", "presentacion", nombreScript);
        // PowerShell hereda la entrada; este proceso no crea un segundo lector de consola.
        var opciones = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardInput = false };
        foreach (var argumento in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-RaizRepo", repositorio, "-ArchivoEstado", archivoEstado })
        {
            opciones.ArgumentList.Add(argumento);
        }
        return opciones;
    }

    private void IniciarProceso(ProcessStartInfo opciones)
    {
        proceso = Process.Start(opciones) ?? throw new InvalidOperationException("No se pudo iniciar PowerShell.");
        grupo.Incluir(proceso);
    }

    public async Task<int> EsperarAsync()
    {
        if (proceso == null) { throw new InvalidOperationException("El iniciador no se ha ejecutado."); }
        await proceso.WaitForExitAsync();
        return proceso.ExitCode;
    }

    public void Dispose()
    {
        grupo.Dispose();
        proceso?.Dispose();
    }
}
