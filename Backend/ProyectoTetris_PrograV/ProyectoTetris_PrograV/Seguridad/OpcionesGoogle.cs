namespace ProyectoTetris_PrograV.Seguridad;

// Preparación para validar ID tokens en una integración posterior.
// Esta configuración por sí sola no habilita el inicio de sesión con Google.
public class OpcionesGoogle
{
    public const string Seccion = "Google";

    // ID público del cliente OAuth de tipo web, usado como audiencia esperada.
    // Nunca almacenar aquí el secreto del cliente web.
    public string IdClienteWeb { get; set; } = "";
}
