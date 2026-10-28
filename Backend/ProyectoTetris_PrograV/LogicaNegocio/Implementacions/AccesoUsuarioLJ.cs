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

// Administra el registro, los ingresos y la recuperación de las cuentas.
// Comprueba la identidad y conserva una sola sesión activa por jugador.
public class AccesoUsuarioLJ : IAccesoUsuarioLJ
{
    private const string NombreOcupado = "Este nombre de usuario ya está en uso. Elegí otro.";
    private static readonly object CerrojoIdentidades = new();
    private readonly IUnidadTrabajoEF unidad;
    private readonly IMapper mapper;
    private readonly ILogger<AccesoUsuarioLJ> logger;
    private readonly ProtectorCodigosRecuperacion protectorCodigos;

    // Recibe repositorios, conversión de datos, registro de errores y protección de códigos.
    public AccesoUsuarioLJ(
        IUnidadTrabajoEF unidad,
        IMapper mapper,
        ILogger<AccesoUsuarioLJ> logger,
        ProtectorCodigosRecuperacion protectorCodigos
    )
    {
        this.unidad = unidad;
        this.mapper = mapper;
        this.logger = logger;
        this.protectorCodigos = protectorCodigos;
    }

    // Acepta nombres de 1 a 50 caracteres, sin caracteres de control.
    private static bool NombreValido(string? nombreUsuario)
    {
        return !string.IsNullOrWhiteSpace(nombreUsuario)
            && nombreUsuario.Trim().Length <= 50
            && !nombreUsuario.Any(char.IsControl);
    }

    // Busca el nickname sin distinguir mayúsculas y minúsculas, después de quitar espacios exteriores.
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

    // Crea una cuenta de invitado bajo el mismo bloqueo que protege las altas de Google.
    public Respuesta<TSesionJugador> Registrar(TRegistroUsuario datos)
    {
        lock (CerrojoIdentidades)
        {
            return RegistrarInterno(datos, null, null);
        }
    }

    // Registra la identidad Google verificada o ingresa a su cuenta si ya fue vinculada.
    public Respuesta<TSesionJugador> RegistrarGoogle(TRegistroUsuario datos, string sub, string? avatar)
    {
        lock (CerrojoIdentidades)
        {
            var existente = unidad.TUsuario.ObtenerEntidad(u => u.GoogleSub == sub).Data;

            if (existente != null)
            {
                return IngresarGoogle(sub);
            }

            return RegistrarInterno(datos, sub, avatar);
        }
    }

    // Comprueba el nickname y guarda juntos usuario, credencial y código de recuperación.
    // Devuelve la nueva sesión y los secretos que el jugador debe conservar.
    private Respuesta<TSesionJugador> RegistrarInterno(TRegistroUsuario datos, string? sub, string? avatar)
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
            FechaRegistro = DateTime.UtcNow,
            GoogleSub = sub,
            AvatarGoogle = avatar
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
            usuario.SesionActivaId = credencial.CredencialDispositivoId;
            var codigo = PrepararCodigoRecuperacion(usuario);
            unidad.TUsuario.Insertar(usuario);
            unidad.TCredencialDispositivo.Insertar(credencial);
            // Un solo SaveChanges inserta ambas entidades de forma atómica.
            unidad.Completar();
            var sesion = CrearSesion(usuario, credencial, secreto);
            sesion.CodigoRecuperacionNuevo = codigo;
            return Respuesta<TSesionJugador>.Ok(sesion);
        }
        catch (Exception excepcion)
        {
            unidad.Rollback();

            if (sub != null && unidad.TUsuario.ObtenerEntidad(u => u.GoogleSub == sub).Data != null)
            {
                // Otra instancia pudo registrar este Google antes del SaveChanges.
                return IngresarGoogle(sub);
            }
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

    // Valida la credencial recordada y crea una sesión nueva que toma el control de la cuenta.
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

        if (!CredencialVigente(credencial))
        {
            return Respuesta<TSesionJugador>.Fail(
                "La credencial venció, fue invalidada o no existe. No se puede recuperar este perfil.",
                "CredencialInvalida"
            );
        }

        using var exclusion = ExclusionSesiones.Tomar(credencial!.UsuarioId);

        if (!CredencialSigueVigente(hash))
        {
            return SesionInvalida();
        }

        var usuario = unidad.TUsuario.ObtenerEntidad(jugador => jugador.UsuarioId == credencial.UsuarioId && jugador.Activo).Data;

        if (usuario == null)
        {
            return SesionInvalida();
        }

        var sesion = NuevaCredencial(usuario);
        unidad.Completar();
        return Respuesta<TSesionJugador>.Ok(sesion);
    }

    // Renueva el acceso de la sesión actual sin desplazar un ingreso desde otro dispositivo.
    public Respuesta<TSesionJugador> Renovar(TRecuperarSesion datos)
    {
        if (datos.Credencial == null || datos.Credencial.Length != 64 || !datos.Credencial.All(Uri.IsHexDigit))
        {
            return SesionInvalida();
        }

        var hash = CalcularHash(datos.Credencial);
        var credencial = unidad.TCredencialDispositivo.ObtenerEntidad(c => c.HashCredencial == hash).Data;

        if (!CredencialVigente(credencial))
        {
            return SesionInvalida();
        }

        using var exclusion = ExclusionSesiones.Tomar(credencial!.UsuarioId);

        if (!CredencialSigueVigente(hash))
        {
            return SesionInvalida();
        }

        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.UsuarioId == credencial.UsuarioId && u.Activo
            && (u.SesionActivaId == credencial.CredencialDispositivoId || u.SesionActivaId == null)).Data;

        if (usuario == null)
        {
            var error = ErrorSesion(credencial.CredencialDispositivoId, credencial.UsuarioId);
            return Respuesta<TSesionJugador>.Fail("La sesion ya no esta activa. Ingresa nuevamente.", error ?? "CredencialInvalida");
        }

        if (usuario.SesionActivaId == null)
        {
            // No reactivar JWT antiguos cuyo sid era el identificador de la credencial anterior.
            var migrada = NuevaCredencial(usuario);
            unidad.Completar();
            return Respuesta<TSesionJugador>.Ok(migrada);
        }

        credencial.ExpiraEnUtc = DateTime.UtcNow.AddDays(365);
        unidad.Completar();
        return Respuesta<TSesionJugador>.Ok(CrearSesion(usuario, credencial, null));
    }

    // Comprueba que la credencial exista, no esté revocada y todavía no haya vencido.
    private static bool CredencialVigente(CredencialDispositivo? credencial)
    {
        return credencial != null && credencial.RevocadaEnUtc == null && credencial.ExpiraEnUtc > DateTime.UtcNow;
    }

    // Consulta otra vez la credencial después de obtener el bloqueo del usuario.
    private bool CredencialSigueVigente(string hash)
    {
        var ahora = DateTime.UtcNow;
        return unidad.TCredencialDispositivo.ObtenerEntidad(c => c.HashCredencial == hash && c.RevocadaEnUtc == null && c.ExpiraEnUtc > ahora).Data != null;
    }

    // Prepara la respuesta de acceso rechazado sin devolver datos privados de la cuenta.
    private static Respuesta<TSesionJugador> SesionInvalida()
    {
        return Respuesta<TSesionJugador>.Fail(
            "Esta sesion ya no esta activa. Tu cuenta pudo iniciarse en otro dispositivo. Ingresa nuevamente para tomar el control.",
            "CredencialInvalida"
        );
    }

    // Indica si el identificador presentado todavía corresponde a una sesión autorizada.
    public bool SesionVigente(Guid sesionId, int usuarioId)
    {
        return ErrorSesion(sesionId, usuarioId) == null;
    }

    // Comprueba credencial, usuario activo y sesión actual.
    // Devuelve null al autorizar o un código que distingue una sesión inválida de una reemplazada.
    public string? ErrorSesion(Guid sesionId, int usuarioId)
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

        var usuario = unidad.TUsuario.ObtenerEntidad(jugador => jugador.UsuarioId == usuarioId && jugador.Activo && jugador.SesionActivaId == sesionId);

        if (!usuario.Success)
        {
            throw new InvalidOperationException("No se pudo validar el perfil.");
        }

        if (consulta.Data != null && usuario.Data != null)
        {
            return null;
        }

        if (consulta.Data == null)
        {
            return "SesionInvalida";
        }
        // Consultar el motivo solo al fallar, con predicado SQL que evita una entidad rastreada desactualizada.
        var reemplazada = unidad.TUsuario.ObtenerEntidad(jugador => jugador.UsuarioId == usuarioId && jugador.Activo
            && jugador.SesionActivaId != null && jugador.SesionActivaId != Guid.Empty && jugador.SesionActivaId != sesionId);

        if (!reemplazada.Success)
        {
            throw new InvalidOperationException("No se pudo validar el perfil.");
        }

        return reemplazada.Data != null ? "SesionReemplazada" : "SesionInvalida";
    }

    // Cierra únicamente la sesión indicada si todavía tiene el control de esa cuenta.
    public void Revocar(Guid sesionId, int usuarioId)
    {
        using var exclusion = ExclusionSesiones.Tomar(usuarioId);
        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.UsuarioId == usuarioId && u.SesionActivaId == sesionId).Data;

        if (usuario == null)
        {
            return;
        }

        var consulta = unidad.TCredencialDispositivo.ObtenerEntidad(credencial =>
            credencial.CredencialDispositivoId == sesionId && credencial.UsuarioId == usuarioId);

        if (!consulta.Success || consulta.Data == null)
        {
            throw new InvalidOperationException("No se pudo invalidar la sesión.");
        }

        consulta.Data.RevocadaEnUtc = DateTime.UtcNow;
        usuario.SesionActivaId = Guid.Empty;
        unidad.Completar();
    }

    // Protege la vinculación de Google frente a otras altas o vinculaciones simultáneas.
    public Respuesta<TUsuario> VincularGoogle(int usuarioId, string sub, string? avatar, Guid? sesionEsperada = null)
    {
        lock (CerrojoIdentidades)
        {
            return VincularGoogleInterno(usuarioId, sub, avatar, sesionEsperada);
        }
    }

    // Comprueba la sesión y que Google no pertenezca a otro perfil antes de guardar el vínculo.
    private Respuesta<TUsuario> VincularGoogleInterno(int usuarioId, string sub, string? avatar, Guid? sesionEsperada)
    {
        using var exclusion = ExclusionSesiones.Tomar(usuarioId);

        if (sesionEsperada.HasValue && !SesionVigente(sesionEsperada.Value, usuarioId))
        {
            return Respuesta<TUsuario>.Fail("La sesion ya no esta activa.", "CredencialInvalida");
        }

        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.UsuarioId == usuarioId && u.Activo).Data;

        if (usuario == null)
        {
            return Respuesta<TUsuario>.Fail("El perfil no está disponible.");
        }

        var propietario = unidad.TUsuario.ObtenerEntidad(u => u.GoogleSub == sub).Data;

        if (propietario != null && propietario.UsuarioId != usuarioId)
        {
            return Respuesta<TUsuario>.Fail(
                "Esa cuenta Google pertenece a otro jugador. Cambiá de jugador e ingresá con Google para abrirlo. Los perfiles no se fusionaron.",
                "GoogleEnOtroPerfil"
            );
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

    // Busca la cuenta por el identificador verificado de Google y crea una sesión activa nueva.
    public Respuesta<TSesionJugador> IngresarGoogle(string sub)
    {
        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.GoogleSub == sub && u.Activo).Data;

        if (usuario == null)
        {
            return Respuesta<TSesionJugador>.Fail(
                "Google aún no está vinculado. Creá o recuperá tu jugador invitado y vinculalo desde Perfil.",
                "GoogleSinVincular"
            );
        }

        using var exclusion = ExclusionSesiones.Tomar(usuario.UsuarioId);
        var sesion = NuevaCredencial(usuario);
        unidad.Completar();
        return Respuesta<TSesionJugador>.Ok(sesion);
    }

    // Reemplaza deliberadamente el código de recuperación y entrega el nuevo una sola vez.
    public Respuesta<string> GenerarRecuperacion(int usuarioId, Guid? sesionEsperada = null)
    {
        using var exclusion = ExclusionSesiones.Tomar(usuarioId);

        if (sesionEsperada.HasValue && !SesionVigente(sesionEsperada.Value, usuarioId))
        {
            return Respuesta<string>.Fail("La sesion ya no esta activa.", "CredencialInvalida");
        }

        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.UsuarioId == usuarioId && u.Activo).Data;

        if (usuario == null)
        {
            return Respuesta<string>.Fail("El jugador no está disponible.");
        }

        var codigo = PrepararCodigoRecuperacion(usuario);
        unidad.Completar();
        return Respuesta<string>.Ok(codigo);
    }

    // Comprueba el código vigente y crea una sesión con un código nuevo pendiente.
    // El anterior continúa válido hasta confirmar que se guardó el nuevo.
    public Respuesta<TSesionJugador> RecuperarConCodigo(string codigo)
    {
        var normalizado = CodigosRecuperacion.Normalizar(codigo);

        if (normalizado == null)
        {
            return Respuesta<TSesionJugador>.Fail("Código de recuperación inválido.", "CredencialInvalida");
        }

        var hash = normalizado.Length == 64 ? CalcularHash(normalizado) : protectorCodigos.CalcularHash(normalizado);
        var ahora = DateTime.UtcNow;
        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.HashRecuperacion == hash && u.Activo && u.RecuperacionExpiraEnUtc > ahora).Data;

        if (usuario == null)
        {
            return Respuesta<TSesionJugador>.Fail("El código venció, ya se utilizó o no existe.", "CredencialInvalida");
        }

        using var exclusion = ExclusionSesiones.Tomar(usuario.UsuarioId);
        usuario = unidad.TUsuario.ObtenerEntidad(u => u.HashRecuperacion == hash && u.Activo && u.RecuperacionExpiraEnUtc > ahora).Data;

        if (usuario == null)
        {
            return SesionInvalida();
        }

        var nuevoCodigo = PrepararCodigoRecuperacion(usuario, pendiente: true);
        var sesion = NuevaCredencial(usuario);
        sesion.CodigoRecuperacionNuevo = nuevoCodigo;
        sesion.CodigoRecuperacionPorConfirmar = true;
        // El anterior permanece utilizable si esta respuesta se pierde.
        unidad.Completar();
        return Respuesta<TSesionJugador>.Ok(sesion);
    }

    // Genera un secreto de dispositivo y guarda su huella.
    // Asigna su identificador como única sesión activa del usuario.
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
        usuario.SesionActivaId = credencial.CredencialDispositivoId;
        return CrearSesion(usuario, credencial, secreto);
    }

    // Activa el código pendiente solo desde la sesión vigente.
    // Permite repetir la confirmación si se perdió la respuesta anterior.
    public Respuesta<bool> ConfirmarRecuperacion(int usuarioId, Guid sesionId, string codigo)
    {
        using var exclusion = ExclusionSesiones.Tomar(usuarioId);

        if (!SesionVigente(sesionId, usuarioId))
        {
            return Respuesta<bool>.Fail("La sesion ya no esta activa.", "CredencialInvalida");
        }

        var normalizado = CodigosRecuperacion.Normalizar(codigo);

        if (normalizado?.Length != 12)
        {
            return Respuesta<bool>.Fail("Codigo pendiente invalido.");
        }

        var hash = protectorCodigos.CalcularHash(normalizado);
        var usuario = unidad.TUsuario.ObtenerEntidad(u => u.UsuarioId == usuarioId && u.Activo).Data!;

        if (usuario.HashRecuperacionPendiente == null && HashIgual(usuario.HashRecuperacion, hash))
        {
            return Respuesta<bool>.Ok(true);
        }

        if (!HashIgual(usuario.HashRecuperacionPendiente, hash))
        {
            return Respuesta<bool>.Fail("Este codigo fue reemplazado. Genera otro desde tu perfil.");
        }

        usuario.HashRecuperacion = usuario.HashRecuperacionPendiente;
        usuario.HashRecuperacionPendiente = null;
        usuario.RecuperacionExpiraEnUtc = DateTime.UtcNow.AddDays(365);
        unidad.Completar();
        return Respuesta<bool>.Ok(true);
    }

    // Compara las huellas sin terminar antes por una diferencia entre sus caracteres.
    private static bool HashIgual(string? esperado, string actual)
    {
        return esperado != null
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(esperado), Encoding.ASCII.GetBytes(actual));
    }

    // Genera un código que no coincida con otro vigente o pendiente.
    // Guarda únicamente su huella y devuelve el texto para mostrarlo al jugador.
    private string PrepararCodigoRecuperacion(Usuario usuario, bool pendiente = false)
    {
        for (var intento = 0; intento < 5; intento++)
        {
            var codigo = CodigosRecuperacion.Crear();
            var hash = protectorCodigos.CalcularHash(CodigosRecuperacion.Normalizar(codigo)!);
            var consulta = unidad.TUsuario.ObtenerEntidad(u => u.HashRecuperacion == hash || u.HashRecuperacionPendiente == hash);

            if (!consulta.Success)
            {
                throw new InvalidOperationException("No se pudo preparar un codigo seguro.");
            }

            if (consulta.Data != null)
            {
                continue;
            }

            if (pendiente)
            {
                usuario.HashRecuperacionPendiente = hash;
            }
            else
            {
                usuario.HashRecuperacion = hash;
                usuario.HashRecuperacionPendiente = null;
                usuario.RecuperacionExpiraEnUtc = DateTime.UtcNow.AddDays(365);
            }

            return codigo;
        }

        throw new InvalidOperationException("No se pudo generar un codigo unico. Reintenta.");
    }

    // Reúne usuario público, identificador y vencimiento para preparar la autenticación.
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

    // Obtiene SHA-256 del secreto de dispositivo o del código legado de 64 caracteres.
    private static string CalcularHash(string secreto)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secreto)));
    }
}
