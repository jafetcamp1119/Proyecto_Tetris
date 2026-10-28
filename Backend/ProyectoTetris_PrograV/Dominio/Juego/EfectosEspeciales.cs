using System.Globalization;
using System.Text;

namespace Dominio.Juego;

public enum EfectoEspecial
{
    Ninguno,
    EliminarFila,
    EliminarColumna,
    Confusion,
    Acelerador,
    Bomba,
    Barredora,
    DoblePuntuacion
}

// Los códigos del catálogo SQL se traducen una sola vez a reglas conocidas del servidor.
public static class EfectosEspeciales
{
    // Traduce las propiedades y el código del catálogo a un efecto conocido por el motor.
    public static EfectoEspecial Identificar(TipoPiezaEspecial tipo)
    {
        if (tipo.EliminaColumna)
        {
            return EfectoEspecial.EliminarColumna;
        }

        if (tipo.EliminaFila)
        {
            return EfectoEspecial.EliminarFila;
        }

        var codigo = string.Concat(tipo.Codigo.Normalize(NormalizationForm.FormD)
            .Where(caracter => CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark))
            .Replace("_", "").Replace(" ", "").ToUpperInvariant();

        switch (codigo)
        {
            case "CONFUSION":
                return EfectoEspecial.Confusion;
            case "ACELERADOR":
                return EfectoEspecial.Acelerador;
            case "BOMBA":
                return EfectoEspecial.Bomba;
            case "BARREDORA":
                return EfectoEspecial.Barredora;
            case "DOBLEPUNTUACION":
            case "DOBLE":
                return EfectoEspecial.DoblePuntuacion;
            default:
                return EfectoEspecial.Ninguno;
        }
    }
}

public sealed class OpcionesEfectos
{
    public int SegundosConfusion
        {
            get;
            set;
        } = 10;

    public int SegundosAcelerador
        {
            get;
            set;
        } = 10;

    public int FactorAcelerador
        {
            get;
            set;
        } = 2;

    // Rechaza duraciones no positivas y un factor de aceleración menor que dos.
    public void Validar()
    {
        if (SegundosConfusion < 1 || SegundosAcelerador < 1 || FactorAcelerador < 2)
        {
            throw new ArgumentException("Las duraciones deben ser positivas y el factor de aceleración al menos 2.");
        }
    }
}
