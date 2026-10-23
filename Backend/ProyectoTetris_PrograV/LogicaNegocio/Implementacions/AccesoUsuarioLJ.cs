using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazLJ;
using Microsoft.Extensions.Logging;
using Utilitarios;

namespace LogicaJuego.Implementacions;

public class AccesoUsuarioLJ : IAccesoUsuarioLJ
{
    private const string NombreOcupado = "Este nombre de usuario ya está en uso. Elegí otro.";
    private readonly IUnidadTrabajoEF unidad;
    private readonly IMapper mapper;
    private readonly ILogger<AccesoUsuarioLJ> logger;

    public AccesoUsuarioLJ(IUnidadTrabajoEF unidad, IMapper mapper, ILogger<AccesoUsuarioLJ> logger)
    {
        this.unidad = unidad;
        this.mapper = mapper;
        this.logger = logger;
    }

    private static bool NombreValido(string? nombreUsuario)
    {
        return !string.IsNullOrWhiteSpace(nombreUsuario)
            && nombreUsuario.Trim().Length <= 50
            && !nombreUsuario.Any(char.IsControl);
    }

    public Respuesta<bool> NombreDisponible(string nombreUsuario)
    {
        if (!NombreValido(nombreUsuario))
        {
            return Respuesta<bool>.Fail("Ingresá un nombre de 1 a 50 caracteres sin caracteres de control.", "NombreInvalido");
        }

        var nombreNormalizado = nombreUsuario.Trim().ToUpperInvariant();
        var consulta = unidad.TUsuario.ObtenerEntidad(usuario => usuario.NombreUsuarioNormalizado == nombreNormalizado);
        if (!consulta.Success)
        {
            throw new InvalidOperationException("No se pudo comprobar la disponibilidad.");
        }

        return Respuesta<bool>.Ok(consulta.Data == null);
    }

    public Respuesta<TSesionJugador> Registrar(TRegistroUsuario datos)
    {
        var disponibilidad = NombreDisponible(datos.NombreUsuario);
        if (!disponibilidad.Success)
        {
            return Respuesta<TSesionJugador>.Fail(disponibilidad.Error!, disponibilidad.CodigoError);
        }

        if (!disponibilidad.Data)
        {
            return Respuesta<TSesionJugador>.Fail(NombreOcupado, "NombreOcupado");
        }

        var usuario = new Usuario
        {
            NombreUsuario = datos.NombreUsuario.Trim(),
            Activo = true,
            FechaRegistro = DateTime.UtcNow
        };
        // 256 bits de entropía. El secreto se entrega una sola vez y SQL guarda solo SHA-256.
        var secreto = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var credencial = new CredencialDispositivo
        {
            CredencialDispositivoId = Guid.NewGuid(),
            Usuario = usuario,
            HashCredencial = CalcularHash(secreto),
            FechaCreacionUtc = DateTime.UtcNow,
            ExpiraEnUtc = DateTime.UtcNow.AddDays(365)
        };

        try
        {
            unidad.TUsuario.Insertar(usuario);
            unidad.TCredencialDispositivo.Insertar(credencial);
            // Un solo SaveChanges inserta ambas entidades de forma atómica.
            unidad.Completar();
            return Respuesta<TSesionJugador>.Ok(CrearSesion(usuario, credencial, secreto));
        }
        catch (Exception excepcion)
        {
            unidad.Rollback();
            // Otro proceso pudo ganar entre la consulta y el índice único de SQL.
            // Solo un nombre que ahora existe se traduce a 409; los demás fallos siguen siendo internos.
            var disponible = NombreDisponible(datos.NombreUsuario);
            if (disponible.Success && !disponible.Data)
            {
                return Respuesta<TSesionJugador>.Fail(NombreOcupado, "NombreOcupado");
            }

            logger.LogError(excepcion, "No se pudo registrar la identidad del jugador.");
            throw;
        }
    }

    public Respuesta<TSesionJugador> Recuperar(TRecuperarSesion datos)
    {
        if (datos.Credencial == null || datos.Credencial.Length != 64 || !datos.Credencial.All(Uri.IsHexDigit))
        {
            return Respuesta<TSesionJugador>.Fail("La credencial del dispositivo no es válida.", "CredencialInvalida");
        }

        var hash = CalcularHash(datos.Credencial);
        var consulta = unidad.TCredencialDispositivo.ObtenerEntidad(credencial => credencial.HashCredencial == hash);
        if (!consulta.Success)
        {
            throw new InvalidOperationException("No se pudo consultar la identidad del dispositivo.");
        }

        var credencial = consulta.Data;
        if (credencial == null || !SesionVigente(credencial.CredencialDispositivoId, credencial.UsuarioId))
        {
            return Respuesta<TSesionJugador>.Fail("La credencial venció, fue invalidada o no existe. No se puede recuperar este perfil.", "CredencialInvalida");
        }

        // El uso válido prolonga la credencial sin cambiar el secreto guardado en el dispositivo.
        // Solo cambia ExpiraEnUtc; una revocación concurrente no se sobrescribe.
        credencial.ExpiraEnUtc = DateTime.UtcNow.AddDays(365);
        unidad.Completar();
        var usuario = unidad.TUsuario.ObtenerEntidad(jugador => jugador.UsuarioId == credencial.UsuarioId).Data!;
        return Respuesta<TSesionJugador>.Ok(CrearSesion(usuario, credencial, null));
    }

    public bool SesionVigente(Guid sesionId, int usuarioId)
    {
        var ahora = DateTime.UtcNow;
        var consulta = unidad.TCredencialDispositivo.ObtenerEntidad(credencial =>
            credencial.CredencialDispositivoId == sesionId
            && credencial.UsuarioId == usuarioId
            && credencial.RevocadaEnUtc == null
            && credencial.ExpiraEnUtc > ahora);
        if (!consulta.Success)
        {
            throw new InvalidOperationException("No se pudo validar la sesión.");
        }

        var usuario = unidad.TUsuario.ObtenerEntidad(jugador => jugador.UsuarioId == usuarioId);
        if (!usuario.Success)
        {
            throw new InvalidOperationException("No se pudo validar el perfil.");
        }

        return consulta.Data != null && usuario.Data?.Activo == true;
    }

    public void Revocar(Guid sesionId, int usuarioId)
    {
        var consulta = unidad.TCredencialDispositivo.ObtenerEntidad(credencial =>
            credencial.CredencialDispositivoId == sesionId && credencial.UsuarioId == usuarioId);
        if (!consulta.Success || consulta.Data == null)
        {
            throw new InvalidOperationException("No se pudo invalidar la sesión.");
        }

        consulta.Data.RevocadaEnUtc = DateTime.UtcNow;
        unidad.Completar();
    }

    public Respuesta<TUsuario> VincularGoogle(int usuarioId, string sub, string? avatar)
    {
        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.UsuarioId == usuarioId && u.Activo).Data;
        if (usuario == null)
        {
            return Respuesta<TUsuario>.Fail("El perfil no está disponible.");
        }

        var propietario = unidad.TUsuario.ObtenerEntidad(u => u.GoogleSub == sub).Data;
        if (propietario != null && propietario.UsuarioId != usuarioId)
        {
            return Respuesta<TUsuario>.Fail("Esa cuenta Google pertenece a otro jugador. Cambiá de jugador e ingresá con Google para abrirlo. Los perfiles no se fusionaron.", "GoogleEnOtroPerfil");
        }

        if (usuario.GoogleSub != null && usuario.GoogleSub != sub)
        {
            return Respuesta<TUsuario>.Fail("Este jugador ya tiene otra cuenta Google vinculada.", "GoogleYaVinculado");
        }

        usuario.GoogleSub = sub;
        usuario.AvatarGoogle = avatar;
        try
        {
            unidad.Completar();
        }
        catch
        {
            unidad.Rollback();
            // El índice único y la concurrencia de EF protegen dos vinculaciones simultáneas.
            var actual = unidad.TUsuario.ObtenerEntidad(u => u.GoogleSub == sub).Data;
            if (actual != null && actual.UsuarioId != usuarioId)
            {
                return Respuesta<TUsuario>.Fail("Google ya está vinculado a otro jugador.", "GoogleEnOtroPerfil");
            }

            throw;
        }

        return Respuesta<TUsuario>.Ok(mapper.Map<TUsuario>(usuario));
    }

    public Respuesta<TSesionJugador> IngresarGoogle(string sub)
    {
        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.GoogleSub == sub && u.Activo).Data;
        if (usuario == null)
        {
            return Respuesta<TSesionJugador>.Fail("Google aún no está vinculado. Creá o recuperá tu jugador invitado y vinculalo desde Perfil.", "GoogleSinVincular");
        }

        var sesion = NuevaCredencial(usuario);
        unidad.Completar();
        return Respuesta<TSesionJugador>.Ok(sesion);
    }

    public Respuesta<string> GenerarRecuperacion(int usuarioId)
    {
        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.UsuarioId == usuarioId && u.Activo).Data;
        if (usuario == null)
        {
            return Respuesta<string>.Fail("El jugador no está disponible.");
        }

        var codigo = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        usuario.HashRecuperacion = CalcularHash(codigo);
        usuario.RecuperacionExpiraEnUtc = DateTime.UtcNow.AddDays(365);
        unidad.Completar();
        return Respuesta<string>.Ok(codigo);
    }

    public Respuesta<TSesionJugador> RecuperarConCodigo(string codigo)
    {
        if (codigo.Length != 64 || !codigo.All(Uri.IsHexDigit))
        {
            return Respuesta<TSesionJugador>.Fail("Código de recuperación inválido.", "CredencialInvalida");
        }

        var hash = CalcularHash(codigo.ToUpperInvariant());
        var ahora = DateTime.UtcNow;
        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.HashRecuperacion == hash && u.Activo && u.RecuperacionExpiraEnUtc > ahora).Data;
        if (usuario == null)
        {
            return Respuesta<TSesionJugador>.Fail("El código venció, ya se utilizó o no existe.", "CredencialInvalida");
        }

        usuario.HashRecuperacion = null;
        usuario.RecuperacionExpiraEnUtc = null;
        var sesion = NuevaCredencial(usuario);
        // El hash es un token de concurrencia: solo una solicitud puede consumirlo.
        unidad.Completar();
        return Respuesta<TSesionJugador>.Ok(sesion);
    }

    private TSesionJugador NuevaCredencial(Usuario usuario)
    {
        var secreto = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var credencial = new CredencialDispositivo
        {
            CredencialDispositivoId = Guid.NewGuid(),
            Usuario = usuario,
            UsuarioId = usuario.UsuarioId,
            HashCredencial = CalcularHash(secreto),
            FechaCreacionUtc = DateTime.UtcNow,
            ExpiraEnUtc = DateTime.UtcNow.AddDays(365)
        };
        unidad.TCredencialDispositivo.Insertar(credencial);
        return CrearSesion(usuario, credencial, secreto);
    }

    private TSesionJugador CrearSesion(Usuario usuario, CredencialDispositivo credencial, string? secreto)
    {
        return new TSesionJugador
        {
            Usuario = mapper.Map<TUsuario>(usuario),
            SesionId = credencial.CredencialDispositivoId,
            Credencial = secreto,
            CredencialExpiraEnUtc = credencial.ExpiraEnUtc
        };
    }

    private static string CalcularHash(string secreto)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secreto)));
    }
}
