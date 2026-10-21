namespace ProyectoTetris_PrograV.Seguridad;

public class OpcionesJwt
{
    public string Emisor { get; set; } = "Tetris.Local";
    public string Audiencia { get; set; } = "Tetris.Ionic";
    public string Clave { get; set; } = "";
    public int MinutosExpiracion { get; set; } = 60;
}
