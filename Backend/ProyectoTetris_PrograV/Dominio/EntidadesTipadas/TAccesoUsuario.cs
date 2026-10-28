// Solicitudes de registro y recuperación, junto con el resultado interno de acceso.
using System.ComponentModel.DataAnnotations;

namespace Dominio.EntidadesTipadas;

// Nickname solicitado para crear una cuenta nueva.
public class TRegistroUsuario
{
    [Required, StringLength(50)]
    public string NombreUsuario
        {
            get;
            set;
        } = "";
}

// Credencial de dispositivo para ingreso explícito o renovación.
public class TRecuperarSesion
{
    [Required, StringLength(64, MinimumLength = 64)]
    public string Credencial
        {
            get;
            set;
        } = "";
}

// Resultado interno. El controlador publica solamente TAutenticacion.
public class TSesionJugador
{
    public string? CodigoRecuperacionNuevo
    {
        get;
            set;
    }

    public bool CodigoRecuperacionPorConfirmar
    {
        get;
            set;
    }

    public TUsuario Usuario
        {
            get;
            set;
        } = new();

    public Guid SesionId
    {
        get;
            set;
    }

    public string? Credencial
    {
        get;
            set;
    }

    public DateTime CredencialExpiraEnUtc
    {
        get;
            set;
    }
}

// Código privado entregado por el jugador para recuperar o confirmar su cuenta.
public class TRecuperarCodigo
{
    [Required, StringLength(64, MinimumLength = 12)]
    public string Credencial
        {
            get;
            set;
        } = "";
}
