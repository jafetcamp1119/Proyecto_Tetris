using System.Diagnostics;
using System.Text.Json;

internal static class MedicionesRendimiento
{
    private static readonly object cerrojo = new();
    private static readonly Dictionary<string, List<double>> tiempos = new();

    public static void Registrar(string nombre, long inicio)
    {
        lock (cerrojo)
        {
            if (!tiempos.TryGetValue(nombre, out var valores))
            {
                valores = new List<double>();
                tiempos.Add(nombre, valores);
            }
            valores.Add(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
        }
    }

    public static void Imprimir()
    {
        lock (cerrojo)
        {
            foreach (var (nombre, valores) in tiempos)
            {
                var ordenados = valores.Order().ToArray();
                Console.WriteLine("MEDICION_LOCAL " + JsonSerializer.Serialize(new
                {
                    nombre, muestras = ordenados.Length,
                    p50Ms = ordenados[(int)((ordenados.Length - 1) * 0.5)],
                    p95Ms = ordenados[(int)((ordenados.Length - 1) * 0.95)], maxMs = ordenados[^1]
                }));
            }
        }
    }
}
