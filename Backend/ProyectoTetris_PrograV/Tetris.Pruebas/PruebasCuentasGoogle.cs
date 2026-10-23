using System.Text.Json;
using AutoMapper;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using LogicaJuego.Implementacions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProyectoTetris_PrograV.Seguridad;

internal static class PruebasCuentasGoogle
{
    public static void Agregar(Action<string, Action> prueba, IMapper mapper)
    {
        void Verificar(bool condicion)
        {
            if (!condicion)
            {
                throw new Exception("La cuenta o su progreso no coinciden con lo esperado.");
            }
        }

        prueba("Vincular Google conserva UsuarioId, nickname, credenciales y estadísticas", () =>
        {
            using var unidad = new UnidadMemoria();
            var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance);
            var invitado = acceso.Registrar(new TRegistroUsuario { NombreUsuario = "Invitada" }).Data!;
            unidad.Estadisticas.Datos.Add(new EstadisticasJugador { UsuarioId = invitado.Usuario.UsuarioId, PuntuacionMaxima = 8500, PartidasGanadas = 3 });
            var perfil = acceso.VincularGoogle(invitado.Usuario.UsuarioId, "google-verificado-123", "https://example.com/avatar");
            Verificar(perfil.Success && perfil.Data!.GoogleVinculado);
            var recuperado = acceso.IngresarGoogle("google-verificado-123");
            Verificar(recuperado.Success && recuperado.Data!.Usuario.UsuarioId == invitado.Usuario.UsuarioId);
            Verificar(unidad.Usuarios.Datos.Count == 1 && recuperado.Data!.Usuario.NombreUsuario == "Invitada");
            Verificar(unidad.Estadisticas.Datos.Single().PuntuacionMaxima == 8500 && unidad.Estadisticas.Datos.Single().PartidasGanadas == 3);
            Verificar(acceso.Recuperar(new TRecuperarSesion { Credencial = invitado.Credencial! }).Success);
            Verificar(!JsonSerializer.Serialize(perfil.Data).Contains("google-verificado-123"));
        });

        prueba("Google de otro perfil no fusiona jugadores ni reemplaza una vinculación", () =>
        {
            using var unidad = new UnidadMemoria();
            var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance);
            var primero = acceso.Registrar(new TRegistroUsuario { NombreUsuario = "Uno" }).Data!;
            var segundo = acceso.Registrar(new TRegistroUsuario { NombreUsuario = "Dos" }).Data!;
            Verificar(acceso.VincularGoogle(primero.Usuario.UsuarioId, "sub-uno", null).Success);
            Verificar(acceso.VincularGoogle(segundo.Usuario.UsuarioId, "sub-uno", null).CodigoError == "GoogleEnOtroPerfil");
            Verificar(acceso.VincularGoogle(primero.Usuario.UsuarioId, "sub-dos", null).CodigoError == "GoogleYaVinculado");
            Verificar(unidad.Usuarios.Datos.Count == 2 && unidad.Usuarios.Datos[1].GoogleSub == null);
            Verificar(acceso.IngresarGoogle("sin-vincular").CodigoError == "GoogleSinVincular");
        });

        prueba("Código privado: hash, reemplazo, consumo único y nueva credencial", () =>
        {
            using var unidad = new UnidadMemoria();
            var acceso = new AccesoUsuarioLJ(unidad, mapper, NullLogger<AccesoUsuarioLJ>.Instance);
            var jugador = acceso.Registrar(new TRegistroUsuario { NombreUsuario = "Rescate" }).Data!;
            var anterior = acceso.GenerarRecuperacion(jugador.Usuario.UsuarioId).Data!;
            var codigo = acceso.GenerarRecuperacion(jugador.Usuario.UsuarioId).Data!;
            Verificar(codigo.Length == 64 && unidad.Usuarios.Datos.Single().HashRecuperacion != codigo);
            Verificar(!acceso.RecuperarConCodigo(anterior).Success);
            var recuperado = acceso.RecuperarConCodigo(codigo.ToLowerInvariant());
            Verificar(recuperado.Success && recuperado.Data!.Usuario.UsuarioId == jugador.Usuario.UsuarioId);
            Verificar(recuperado.Data!.Credencial != jugador.Credencial && recuperado.Data.SesionId != jugador.SesionId);
            Verificar(!acceso.RecuperarConCodigo(codigo).Success && unidad.Usuarios.Datos.Single().HashRecuperacion == null);
            var vencido = acceso.GenerarRecuperacion(jugador.Usuario.UsuarioId).Data!;
            unidad.Usuarios.Datos.Single().RecuperacionExpiraEnUtc = DateTime.UtcNow.AddSeconds(-1);
            Verificar(!acceso.RecuperarConCodigo(vencido).Success);
        });

        prueba("Desafíos Google impiden reutilizar nonces y vincular a otro jugador", () =>
        {
            var desafios = new DesafiosGoogle();
            var datos = JsonSerializer.SerializeToElement(desafios.Crear(12));
            var id = datos.GetProperty("Id").GetString()!;
            var nonce = datos.GetProperty("Nonce").GetString()!;
            Verificar(!desafios.Consumir(id, nonce, 13));
            datos = JsonSerializer.SerializeToElement(desafios.Crear(12));
            id = datos.GetProperty("Id").GetString()!;
            nonce = datos.GetProperty("Nonce").GetString()!;
            Verificar(desafios.Consumir(id, nonce, 12));
            Verificar(!desafios.Consumir(id, nonce, 12));
        });

        prueba("La biblioteca oficial de Google rechaza un ID token inventado", () =>
        {
            var validador = new ValidadorGoogle(Options.Create(new OpcionesGoogle { IdClienteWeb = "prueba.apps.googleusercontent.com" }));
            try
            {
                validador.ValidarAsync("token-inventado").GetAwaiter().GetResult();
                throw new Exception("Se aceptó un token falso.");
            }
            catch (Google.Apis.Auth.InvalidJwtException)
            {
                // No representa una prueba de acceso real con una cuenta Google.
            }
        });

        prueba("El historial distingue modo individual, participantes y empates desde los resultados", () =>
        {
            using var unidad = new UnidadMemoria();
            unidad.Partidas.Datos.AddRange([
                new Partida { PartidaId = 1, TipoPartida = "Individual" },
                new Partida { PartidaId = 2, TipoPartida = "Competitivo" }
            ]);
            unidad.Participantes.Datos.AddRange([
                new ParticipantesPartidum { PartidaId = 1, UsuarioId = 1 },
                new ParticipantesPartidum { PartidaId = 2, UsuarioId = 1 },
                new ParticipantesPartidum { PartidaId = 2, UsuarioId = 2 }
            ]);
            unidad.Resultados.Datos.AddRange([
                new Resultado { PartidaId = 1, UsuarioId = 1, Puntuacion = 1500 },
                new Resultado { PartidaId = 2, UsuarioId = 1, Puntuacion = 3000, EsGanador = true },
                new Resultado { PartidaId = 2, UsuarioId = 2, Puntuacion = 3000, EsGanador = true }
            ]);
            var servicio = new ResultadoLJ(unidad, mapper, NullLogger<ResultadoLJ>.Instance);
            var historial = servicio.ObtenerPorUsuario(1).Data!.ToArray();
            Verificar(historial.Length == 2);
            Verificar(historial.Single(r => r.Modalidad == "Individual").CantidadParticipantes == 1);
            var empate = historial.Single(r => r.Modalidad == "Competitivo");
            Verificar(empate.EmpatePrimerLugar && empate.CantidadParticipantes == 2 && empate.Puntuacion == 3000);
        });
    }
}
