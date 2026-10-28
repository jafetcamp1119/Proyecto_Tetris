using System.Diagnostics;

namespace Tetris.Presentacion;

// Inicia el proceso PowerShell que prepara la presentación y espera su resultado.
// GrupoProcesos permite cerrar los procesos que dependen de este iniciador.
public sealed class IniciadorSistema : IDisposable
{
    private readonly GrupoProcesos grupo = new();
    private Process? proceso;

    // Lanza la preparación normal o la comprobación estática según el parámetro verificar.
    public void Iniciar(string repositorio, string archivoEstado, bool verificar)
    {
        var opciones = CrearOpciones(repositorio, archivoEstado, "IniciarPresentacion.ps1");

        if (verificar)
        {
            opciones.ArgumentList.Add("-VerificarSolamente");
        }

        IniciarProceso(opciones);
    }

    // Ejecuta el escenario interno que comprueba la lectura de confirmación en consola.
    public void ProbarConfirmacion(string repositorio, string archivoEstado)
    {
        IniciarProceso(CrearOpciones(repositorio, archivoEstado, "pruebas/ConfirmacionConsola.ps1"));
    }

    // Prepara argumentos separados y comparte la entrada de consola con PowerShell.
    private static ProcessStartInfo CrearOpciones(string repositorio, string archivoEstado, string nombreScript)
    {
        var script = Path.Combine(repositorio, "frontend", "tetris-app", "scripts", "presentacion", nombreScript);
        // PowerShell hereda la entrada; este proceso no crea un segundo lector de consola.
        var opciones = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, RedirectStandardInput = false };

        foreach (var argumento in new[]
        {
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            script,
            "-RaizRepo",
            repositorio,
            "-ArchivoEstado",
            archivoEstado
        })
        {
            opciones.ArgumentList.Add(argumento);
        }

        return opciones;
    }

    // Inicia PowerShell y lo incorpora al grupo que controla el cierre de procesos.
    private void IniciarProceso(ProcessStartInfo opciones)
    {
        proceso = Process.Start(opciones) ?? throw new InvalidOperationException("No se pudo iniciar PowerShell.");
        grupo.Incluir(proceso);
    }

    // Espera al proceso iniciado y devuelve su código de salida.
    public async Task<int> EsperarAsync()
    {
        if (proceso == null)
        {
            throw new InvalidOperationException("El iniciador no se ha ejecutado.");
        }

        await proceso.WaitForExitAsync();
        return proceso.ExitCode;
    }

    // Libera el grupo y el proceso que administra el iniciador.
    public void Dispose()
    {
        grupo.Dispose();
        proceso?.Dispose();
    }
}
