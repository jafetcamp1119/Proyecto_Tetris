using System.Security.Cryptography;

namespace LogicaJuego.Implementacions;

// Genera y normaliza los códigos privados para recuperar una cuenta.
public static class CodigosRecuperacion
{
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Crear()
    {
        var caracteres = new char[12];

        for (var indice = 0; indice < caracteres.Length; indice++)
        {
            caracteres[indice] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        }

        var codigo = new string(caracteres);
        return codigo[..4] + "-" + codigo[4..8] + "-" + codigo[8..];
    }

    public static string? Normalizar(string? codigo)
    {
        if (codigo == null)
        {
            return null;
        }

        var valor = codigo.Trim().ToUpperInvariant();
        // Los codigos anteriores siguen siendo validos hasta usarse o reemplazarse.
        if (valor.Length == 64 && valor.All(Uri.IsHexDigit))
        {
            return valor;
        }

        if (valor.Length == 14 && valor[4] == '-' && valor[9] == '-')
        {
            valor = valor[..4] + valor[5..9] + valor[10..];
        }

        return valor.Length == 12 && valor.All(Alfabeto.Contains) ? valor : null;
    }
}
