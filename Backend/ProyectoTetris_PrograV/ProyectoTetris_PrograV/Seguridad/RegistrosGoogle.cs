using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

using Dominio.EntidadesTipadas;
using Dominio.InterfazLJ;

using Utilitarios;

namespace ProyectoTetris_PrograV.Seguridad;

// Comprobante opaco de cinco minutos: solo habilita completar un registro verificado.
// Conserva por tiempo limitado la identidad Google verificada mientras se elige un nickname.
public sealed class RegistrosGoogle(TimeProvider? tiempo = null)
{
    private readonly TimeProvider reloj = tiempo ?? TimeProvider.System;

    private sealed record Pendiente(string Sub, string? Avatar, DateTime Expira)
    {
        public readonly object Cerrojo = new();
    }

    private readonly ConcurrentDictionary<string, Pendiente> pendientes = new();

    // Guarda temporalmente la identidad verificada y entrega un comprobante opaco al cliente.
    public object Crear(string sub, string? avatar)
    {
        foreach (var item in pendientes.Where(p => p.Value.Expira <= reloj.GetUtcNow().UtcDateTime))
        {
            pendientes.TryRemove(item.Key, out _);
        }

        if (pendientes.Count >= 2048)
        {
            throw new InvalidOperationException("No se puede preparar otro registro ahora.");
        }

        var comprobante = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expira = reloj.GetUtcNow().UtcDateTime.AddMinutes(5);
        pendientes[Hash(comprobante)] = new Pendiente(sub, avatar, expira);
        return new
        {
            RequiereRegistro = true,
            ComprobanteRegistro = comprobante,
            ExpiraEnUtc = expira
        };
    }

    // Valida el comprobante, registra el nickname y lo consume cuando el alta tiene éxito.
    public Respuesta<TSesionJugador> Completar(string comprobante, TRegistroUsuario datos, IAccesoUsuarioLJ acceso)
    {
        var hash = Hash(comprobante);

        if (!pendientes.TryGetValue(hash, out var pendiente))
        {
            return Invalido();
        }

        lock (pendiente.Cerrojo)
        {
            if (pendiente.Expira <= reloj.GetUtcNow().UtcDateTime || !pendientes.ContainsKey(hash))
            {
                pendientes.TryRemove(hash, out _);
                return Invalido();
            }

            var respuesta = acceso.RegistrarGoogle(datos, pendiente.Sub, pendiente.Avatar);

            if (respuesta.Success)
            {
                pendientes.TryRemove(hash, out _);
            }

            return respuesta;
        }
    }

    // Calcula la huella del comprobante para no conservar su texto en la colección.
    private static string Hash(string valor)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
    }
    // Prepara el error de comprobante vencido, usado o inexistente.
    private static Respuesta<TSesionJugador> Invalido()
    {
        return Respuesta<TSesionJugador>.Fail(
            "La verificacion de Google vencio o ya se utilizo. Continua nuevamente con Google.",
            "RegistroGoogleVencido"
        );
    }
}
