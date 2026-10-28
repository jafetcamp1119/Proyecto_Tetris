namespace Tetris.Presentacion;

public static class RutasPresentacion
{
    public static string BuscarRepositorio()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta != null)
        {
            if (Directory.Exists(Path.Combine(carpeta.FullName, "frontend", "tetris-app")))
            {
                return carpeta.FullName;
            }
            carpeta = carpeta.Parent;
        }
        throw new InvalidOperationException("Ejecute el iniciador desde el repositorio Proyecto_Tetris.");
    }
}
