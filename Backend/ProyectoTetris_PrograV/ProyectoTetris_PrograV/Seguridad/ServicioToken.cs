using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Dominio.EntidadesTipadas;

using Microsoft.IdentityModel.Tokens;

namespace ProyectoTetris_PrograV.Seguridad;

// Crea el JWT firmado con el usuario, la sesión activa y su vencimiento.
public class ServicioToken
{
    private readonly OpcionesJwt _opciones;

    public ServicioToken(OpcionesJwt opciones)
    {
        _opciones = opciones;
    }

    // Firma el JWT con sub, sid, nombre y jti; devuelve el token y su fecha de vencimiento.
    public TAutenticacion Crear(TUsuario usuario, Guid sesionId)
    {
        var ahora = DateTime.UtcNow;
        var expiracion = ahora.AddMinutes(_opciones.MinutosExpiracion);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.UsuarioId.ToString()),
            new Claim("sid", sesionId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, usuario.NombreUsuario),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Clave));
        var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opciones.Emisor,
            audience: _opciones.Audiencia,
            claims: claims,
            notBefore: ahora,
            expires: expiracion,
            signingCredentials: credenciales);
        return new TAutenticacion
        {
            UsuarioId = usuario.UsuarioId,
            NombreUsuario = usuario.NombreUsuario,
            Correo = usuario.Correo,
            FechaRegistro = usuario.FechaRegistro,
            Activo = usuario.Activo,
            GoogleVinculado = usuario.GoogleVinculado,
            AvatarGoogle = usuario.AvatarGoogle,
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiraEnUtc = expiracion
        };
    }
}
