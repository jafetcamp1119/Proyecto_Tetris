using System.Security.Cryptography;
using System.Text;

namespace LogicaJuego.Implementacions;

public sealed class ProtectorCodigosRecuperacion
{
    private readonly byte[] clave;

    public ProtectorCodigosRecuperacion(string claveServidor)
    {
        clave = Encoding.UTF8.GetBytes(claveServidor);
        if (clave.Length < 32) { throw new InvalidOperationException("Se requiere una clave privada persistente para proteger la recuperacion."); }
    }

    public string CalcularHash(string codigoNormalizado)
    {
        // Separacion de proposito: el hash no permite probar codigos cortos con solo una copia de SQL.
        var datos = Encoding.UTF8.GetBytes("tetris-recuperacion-v1:" + codigoNormalizado);
        return Convert.ToHexString(HMACSHA256.HashData(clave, datos));
    }
}
