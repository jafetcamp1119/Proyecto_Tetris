using System.Security.Cryptography;
using LogicaJuego.Implementacions;

internal static class CredencialesPrueba
{
    public static readonly ProtectorCodigosRecuperacion Protector = new(
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
}
