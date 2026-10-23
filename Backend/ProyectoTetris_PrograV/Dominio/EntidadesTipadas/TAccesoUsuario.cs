using System.ComponentModel.DataAnnotations;

namespace Dominio.EntidadesTipadas;

public class TRegistroUsuario
{
    [Required, StringLength(50)]
    public string NombreUsuario { get; set; } = "";
}

public class TRecuperarSesion
{
    [Required, StringLength(64, MinimumLength = 64)]
    public string Credencial { get; set; } = "";
}

// Resultado interno. El controlador publica solamente TAutenticacion.
public class TSesionJugador
{
    public TUsuario Usuario { get; set; } = new();
    public Guid SesionId { get; set; }
    public string? Credencial { get; set; }
    public DateTime CredencialExpiraEnUtc { get; set; }
}
