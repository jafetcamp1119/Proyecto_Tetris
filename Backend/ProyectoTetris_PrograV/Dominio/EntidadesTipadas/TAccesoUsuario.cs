using System.ComponentModel.DataAnnotations;

namespace Dominio.EntidadesTipadas;

public class TRegistroUsuario
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string NombreUsuario { get; set; } = "";

    [Required, EmailAddress, StringLength(100)]
    public string Correo { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8)]
    public string Contrasena { get; set; } = "";
}

public class TLoginUsuario
{
    [Required]
    public string NombreUsuario { get; set; } = "";

    [Required]
    public string Contrasena { get; set; } = "";
}
