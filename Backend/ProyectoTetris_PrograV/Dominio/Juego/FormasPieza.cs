namespace Dominio.Juego;

// Las mismas formas se utilizan en el motor y en la vista previa de 4 por 4.
public static class FormasPieza
{
    private static readonly (int F, int C)[][] Formas =
    {
        new[] { (1, 0), (1, 1), (1, 2), (1, 3) },
        new[] { (0, 1), (0, 2), (1, 1), (1, 2) },
        new[] { (0, 1), (1, 0), (1, 1), (1, 2) },
        new[] { (0, 1), (0, 2), (1, 0), (1, 1) },
        new[] { (0, 0), (0, 1), (1, 1), (1, 2) },
        new[] { (0, 0), (1, 0), (1, 1), (1, 2) },
        new[] { (0, 2), (1, 0), (1, 1), (1, 2) }
    };

    // Devuelve las coordenadas de los cuatro bloques con la rotación solicitada.
    public static (int F, int C)[] Bloques(int tipo, int rotacion = 0)
    {
        var bloques = Formas[tipo].ToArray();

        if (tipo == 1)
        {
            return bloques;
        }

        for (var vuelta = 0; vuelta < rotacion; vuelta++)
        {
            bloques = bloques.Select(bloque => (bloque.C, 3 - bloque.F)).ToArray();
        }

        return bloques;
    }
}
