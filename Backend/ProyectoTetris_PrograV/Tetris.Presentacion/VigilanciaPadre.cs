using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tetris.Presentacion;

public static class VigilanciaPadre
{
    public static async Task VigilarAsync(Action detener, CancellationToken cancelacion)
    {
        var datos = new InformacionProceso();
        var resultado = NtQueryInformationProcess(Process.GetCurrentProcess().Handle, 0, ref datos, Marshal.SizeOf<InformacionProceso>(), out _);
        if (resultado != 0) { return; }
        try
        {
            using var padre = Process.GetProcessById(datos.Padre.ToInt32());
            // Al usar el perfil Executable, dotnet run es el padre; F5 puede terminarlo primero.
            if (!padre.ProcessName.Equals("dotnet", StringComparison.OrdinalIgnoreCase)) { return; }
            await padre.WaitForExitAsync(cancelacion);
            detener();
        }
        catch (OperationCanceledException) { }
        catch (ArgumentException) { detener(); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InformacionProceso
    {
        public IntPtr Reservado, Peb, Reservado2, Reservado3, Identificador, Padre;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr proceso, int clase, ref InformacionProceso informacion, int tamano, out int devueltos);
}
