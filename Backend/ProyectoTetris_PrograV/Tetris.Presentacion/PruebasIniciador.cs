using System.Diagnostics;

namespace Tetris.Presentacion;

public static class PruebasIniciador
{
    public static async Task EjecutarAsync()
    {
        var raiz = RutasPresentacion.BuscarRepositorio();
        if (!File.Exists(Path.Combine(raiz, "frontend", "tetris-app", "firebase.json"))) { throw new Exception("No se encontro el repositorio."); }
        Console.WriteLine("OK iniciador: encuentra el repositorio desde bin.");
        var opciones = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        opciones.ArgumentList.Add(typeof(PruebasIniciador).Assembly.Location);
        opciones.ArgumentList.Add("--hijo-prueba");
        using var proceso = Process.Start(opciones)!;
        var grupo = new GrupoProcesos();
        grupo.Incluir(proceso);
        grupo.Dispose();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await proceso.WaitForExitAsync(limite.Token);
        if (!proceso.HasExited) { throw new Exception("Quedo un proceso secundario ejecutandose."); }
        Console.WriteLine("OK iniciador: cerrar el Job Object termina el proceso propio.");
        var json = System.Text.Json.JsonSerializer.Serialize(EstadoPanel.NoDisponible("Sin conexion"));
        if (!json.Contains("error")) { throw new Exception("El panel anuncia disponibilidad incorrecta."); }
        Console.WriteLine("OK iniciador: estado de fallo explicito, sin anunciar disponibilidad.");
        ProbarLecturaEstadoCompartida();
        ProbarConservacionError();
        await ProbarConfirmacionHeredadaAsync();
        await ProbarSalidaAbruptaAsync();
    }

    private static async Task ProbarConfirmacionHeredadaAsync()
    {
        foreach (var entrada in new string?[] { "PUBLICAR", " publicar ", "PuBlIcAr", "NO", "", null })
        {
            var opciones = CrearOpciones("--confirmacion-prueba-interna");
            opciones.RedirectStandardInput = true;
            opciones.RedirectStandardOutput = true;
            opciones.RedirectStandardError = true;
            using var padre = Process.Start(opciones)!;
            try
            {
                var salida = padre.StandardOutput.ReadToEndAsync();
                var errores = padre.StandardError.ReadToEndAsync();
                if (entrada != null) { await padre.StandardInput.WriteLineAsync(entrada); }
                padre.StandardInput.Close();
                using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await padre.WaitForExitAsync(limite.Token);
                var texto = await salida;
                var autoriza = string.Equals(entrada?.Trim(), "PUBLICAR", StringComparison.OrdinalIgnoreCase);
                if ((padre.ExitCode == 0) != autoriza || !string.IsNullOrEmpty(await errores))
                {
                    throw new Exception("La entrada heredada entre .NET y PowerShell no conservo la confirmacion: " + texto);
                }
                if (texto.Split("Para autorizar este deploy").Length != 2) { throw new Exception("Se repitio la confirmacion."); }
                if (entrada == null && !texto.Contains("Entrada de confirmacion no disponible")) { throw new Exception("EOF no identificado."); }
            }
            finally
            {
                if (!padre.HasExited) { padre.Kill(true); }
            }
        }
        Console.WriteLine("OK iniciador: entrada .NET -> PowerShell reconoce PUBLICAR, espacios y minusculas; rechaza otras respuestas y EOF, sin deploy.");
    }

    private static void ProbarConservacionError()
    {
        var archivo = Path.GetTempFileName();
        try
        {
            const string causa = "API publica: NameResolutionFailure; /salud no alcanzado.";
            EstadoPanel.GuardarError(archivo, causa);
            EstadoPanel.RegistrarFinIniciador(archivo);
            var lectura = System.Text.Json.JsonSerializer.Serialize(EstadoPanel.Leer(archivo));
            if (!lectura.Contains(causa)) { throw new Exception("Se perdio la causa al terminar el iniciador."); }
            var listo = new { estado = "listo", mensaje = "Anterior", actualizadoUtc = DateTime.UtcNow };
            File.WriteAllText(archivo, System.Text.Json.JsonSerializer.Serialize(listo));
            EstadoPanel.RegistrarFinIniciador(archivo);
            lectura = System.Text.Json.JsonSerializer.Serialize(EstadoPanel.Leer(archivo));
            if (!lectura.Contains("\"estado\":\"error\"")) { throw new Exception("Quedo listo tras finalizar el iniciador."); }
            Console.WriteLine("OK iniciador: conserva la causa original y elimina disponibilidad al terminar.");
        }
        finally
        {
            File.Delete(archivo);
        }
    }

    private static void ProbarLecturaEstadoCompartida()
    {
        var archivo = Path.GetTempFileName();
        try
        {
            using (var escritor = File.Open(archivo, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var estado = new { estado = "listo", mensaje = "Dato de prueba", actualizadoUtc = DateTime.UtcNow };
                var contenido = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(estado);
                escritor.Write(contenido);
                escritor.Flush();
                var lectura = System.Text.Json.JsonSerializer.Serialize(EstadoPanel.Leer(archivo));
                if (!lectura.Contains("\"estado\":\"listo\"")) { throw new Exception("El panel no pudo leer el estado con un escritor abierto."); }
            }
            Console.WriteLine("OK iniciador: el panel lee el estado sin interferir con su escritor.");
        }
        finally
        {
            File.Delete(archivo);
        }
    }

    public static void CrearPadreDePrueba(string archivo)
    {
        var grupo = new GrupoProcesos();
        var opciones = CrearOpciones("--hijo-prueba");
        var hijo = Process.Start(opciones)!;
        grupo.Incluir(hijo);
        File.WriteAllText(archivo, hijo.Id.ToString());
        // Simula Stop/terminacion sin ejecutar Dispose: Windows debe cerrar el Job.
        Environment.Exit(0);
    }

    private static ProcessStartInfo CrearOpciones(params string[] argumentos)
    {
        var opciones = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        opciones.ArgumentList.Add(typeof(PruebasIniciador).Assembly.Location);
        foreach (var argumento in argumentos) { opciones.ArgumentList.Add(argumento); }
        return opciones;
    }

    private static async Task ProbarSalidaAbruptaAsync()
    {
        var archivo = Path.GetTempFileName();
        using var padre = Process.Start(CrearOpciones("--padre-prueba", archivo))!;
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await padre.WaitForExitAsync(limite.Token);
            var identificador = int.Parse(File.ReadAllText(archivo));
            try
            {
                using var hijo = Process.GetProcessById(identificador);
                await hijo.WaitForExitAsync(limite.Token);
            }
            catch (ArgumentException) { }
            Console.WriteLine("OK iniciador: salida abrupta del padre no deja el hijo de prueba ejecutandose.");
        }
        finally
        {
            if (!padre.HasExited) { padre.Kill(true); }
            File.Delete(archivo);
        }
    }
}
