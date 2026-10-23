using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace ProyectoTetris_PrograV.Seguridad;

public interface IValidadorGoogle
{
    Task<GoogleJsonWebSignature.Payload> ValidarAsync(string idToken);
}

public class ValidadorGoogle(IOptions<OpcionesGoogle> opciones) : IValidadorGoogle
{
    public Task<GoogleJsonWebSignature.Payload> ValidarAsync(string idToken)
    {
        var idCliente = opciones.Value.IdClienteWeb;
        if (string.IsNullOrWhiteSpace(idCliente))
        {
            throw new InvalidOperationException("Configure Google:IdClienteWeb en el backend.");
        }

        // La biblioteca oficial valida firma, emisor, audiencia y vencimiento.
        return GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [idCliente],
            IssuedAtClockTolerance = TimeSpan.FromSeconds(30),
            ExpirationTimeClockTolerance = TimeSpan.Zero
        });
    }
}
