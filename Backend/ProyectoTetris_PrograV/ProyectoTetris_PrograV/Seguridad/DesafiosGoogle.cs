using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ProyectoTetris_PrograV.Seguridad;

// Nonces breves de un solo uso, asociados al jugador y a la operación solicitada.
// Las salas y estos desafíos pertenecen a la misma instancia del servidor.
// Conserva desafíos temporales para relacionar cada respuesta Google con un intento de acceso.
public class DesafiosGoogle
{
    private record Desafio(string Nonce, int? UsuarioId, DateTime Expira);

    private readonly ConcurrentDictionary<string, Desafio> pendientes = new();

    // Genera un identificador y un nonce para un intento de acceso o vinculación.
    public object Crear(int? usuarioId)
    {
        foreach (var item in pendientes.Where(p => p.Value.Expira < DateTime.UtcNow))
        {
            pendientes.TryRemove(item.Key, out _);
        }

        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        pendientes[id] = new Desafio(nonce, usuarioId, DateTime.UtcNow.AddMinutes(10));
        return new
        {
            Id = id,
            Nonce = nonce
        };
    }

    // Acepta el desafío una sola vez si coincide el nonce, el usuario y su plazo.
    public bool Consumir(string id, string? nonce, int? usuarioId)
    {
        return pendientes.TryRemove(id, out var desafio)
            && desafio.Expira > DateTime.UtcNow
            && desafio.UsuarioId == usuarioId
            && desafio.Nonce == nonce;
    }
}
