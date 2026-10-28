namespace Utilitarios;

// Cerraduras acotadas: el cambio de sesion y una accion del hub no se intercalan.
// Evita que dos cambios de sesión del mismo usuario se ejecuten al mismo tiempo.
public static class ExclusionSesiones
{
    private static readonly SemaphoreSlim[] cerraduras = Enumerable.Range(0, 64)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public static IDisposable Tomar(int usuarioId)
    {
        var cerradura = cerraduras[(int)((uint)usuarioId % cerraduras.Length)];
        cerradura.Wait();
        return new Liberacion(cerradura);
    }

    public static async ValueTask<IDisposable> TomarAsync(int usuarioId, CancellationToken cancelacion)
    {
        var cerradura = cerraduras[(int)((uint)usuarioId % cerraduras.Length)];
        await cerradura.WaitAsync(cancelacion);
        return new Liberacion(cerradura);
    }

    private sealed class Liberacion(SemaphoreSlim cerradura) : IDisposable
    {
        private int liberada;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref liberada, 1) == 0)
            {
                cerradura.Release();
            }
        }
    }
}
