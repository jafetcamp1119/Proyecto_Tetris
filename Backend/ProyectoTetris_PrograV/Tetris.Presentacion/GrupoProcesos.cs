using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tetris.Presentacion;

// Windows cierra todos los descendientes aunque Visual Studio termine el iniciador abruptamente.
// Agrupa procesos de Windows para cerrar también sus descendientes al finalizar la presentación.
public sealed class GrupoProcesos : IDisposable
{
    private IntPtr identificador;

    public GrupoProcesos()
    {
        identificador = CreateJobObject(IntPtr.Zero, null);

        if (identificador == IntPtr.Zero)
        {
            throw new Win32Exception();
        }

        var limites = new LimitesExtendidos();
        limites.Basicos.Flags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE

        if (!SetInformationJobObject(identificador, 9, ref limites, (uint)Marshal.SizeOf<LimitesExtendidos>()))
        {
            Dispose();
            throw new Win32Exception();
        }
    }

    // Asocia el proceso al Job Object de Windows que controla sus descendientes.
    public void Incluir(Process proceso)
    {
        if (!AssignProcessToJobObject(identificador, proceso.Handle))
        {
            proceso.Kill(true);
            throw new Win32Exception("No se pudo asociar el proceso al cierre seguro de la presentacion.");
        }
    }

    // Cierra el identificador del grupo para terminar los procesos que todavía dependen de él.
    public void Dispose()
    {
        var anterior = Interlocked.Exchange(ref identificador, IntPtr.Zero);

        if (anterior != IntPtr.Zero)
        {
            CloseHandle(anterior);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LimitesBasicos
    {
        public long TiempoProceso, TiempoGrupo;
        public uint Flags;
        public UIntPtr Minimo, Maximo;
        public uint Procesos;
        public UIntPtr Afinidad;
        public uint Prioridad, Planificacion;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ContadoresIo
    {
        public ulong Lecturas, Escrituras, Otras, BytesLectura, BytesEscritura, OtrosBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LimitesExtendidos
    {
        public LimitesBasicos Basicos;
        public ContadoresIo Io;
        public UIntPtr MemoriaProceso, MemoriaGrupo, PicoProceso, PicoGrupo;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr seguridad, string? nombre);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr grupo, int clase, ref LimitesExtendidos datos, uint longitud);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr grupo, IntPtr proceso);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr identificador);
}
