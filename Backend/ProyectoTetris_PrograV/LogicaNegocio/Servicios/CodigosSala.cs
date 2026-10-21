using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace LogicaJuego.Servicios;

// Genera los códigos de las salas privadas. Son aleatorios (no se pueden deducir de otro dato),
// cortos para compartirlos y únicos entre las salas activas.
internal sealed class CodigosSala
{
    public const int Longitud = 6;

    // Sin 0, 1, I ni O para evitar confusiones al dictar o escribir el código.
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int IntentosMaximos = 100;

    private readonly ConcurrentDictionary<string, Guid> codigosActivos = new();

    // Reserva un código nuevo para la sala. Devuelve null si no logró encontrar uno libre.
    public string? Reservar(Guid salaId)
    {
        for (var intento = 0; intento < IntentosMaximos; intento++)
        {
            var codigo = Generar();

            if (codigosActivos.TryAdd(codigo, salaId))
            {
                return codigo;
            }
        }

        return null;
    }

    public bool TryObtenerSala(string codigo, out Guid salaId)
    {
        var normalizado = Normalizar(codigo);
        return codigosActivos.TryGetValue(normalizado, out salaId);
    }

    public void Liberar(string? codigo, Guid salaId)
    {
        if (codigo != null)
        {
            codigosActivos.TryRemove(new KeyValuePair<string, Guid>(codigo, salaId));
        }
    }

    public static string Normalizar(string codigo)
    {
        return codigo.Trim().ToUpperInvariant();
    }

    private static string Generar()
    {
        var letras = new char[Longitud];

        for (var i = 0; i < Longitud; i++)
        {
            letras[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        }

        return new string(letras);
    }
}
