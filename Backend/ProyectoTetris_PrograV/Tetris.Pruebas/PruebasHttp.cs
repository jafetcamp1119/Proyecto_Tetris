using Dominio.Juego;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AccesoDatos.Contexto;
using AutoMapper;
using Dominio.DTO;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazJuego;
using Dominio.InterfazLJ;
using LogicaJuego;
using LogicaJuego.Implementacions;
using LogicaJuego.Motor;
using LogicaJuego.Servicios;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ProyectoTetris_PrograV;
using ProyectoTetris_PrograV.Hubs;
using ProyectoTetris_PrograV.Seguridad;
using ProyectoTetris_PrograV.Servicios;
using Utilitarios;

internal static class PruebasHttp
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> EjecutarAsync()
    {
        var constructor = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });
        constructor.Logging.ClearProviders();
        constructor.WebHost.UseSetting("urls", "http://127.0.0.1:0");

        var clave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        constructor.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Clave"] = clave,
            ["Jwt:Emisor"] = "Tetris.Pruebas",
            ["Jwt:Audiencia"] = "Ionic.Pruebas",
            ["Google:IdClienteWeb"] = "prueba.apps.googleusercontent.com",
            ["Cors:OrigenesPermitidos:0"] = "http://localhost:8100",
            ["Cors:OrigenesPermitidos:1"] = "https://localhost",
            ["Cors:OrigenesPermitidos:2"] = "http://localhost",
            ["Cors:OrigenesPermitidos:3"] = "capacitor://localhost",
            ["Cors:PermitirRedLocal"] = "true"
        });

        constructor.Services.AgregarApiTetris();
        constructor.Services.AgregarSeguridad(constructor.Configuration, constructor.Environment);
        constructor.Services.AddAutoMapper(configuracion => { }, typeof(AutoMapperProfile).Assembly);
        constructor.Services.AddSingleton<IUnidadTrabajoEF>(new UnidadMemoria());
        constructor.Services.AddScoped<IAccesoUsuarioLJ, AccesoUsuarioLJ>();
        constructor.Services.AddScoped<IUsuarioLJ, UsuarioLJ>();
        constructor.Services.AddScoped<IConfiguracionLJ, ConfiguracionLJ>();
        constructor.Services.AddScoped<IResultadoLJ, ResultadoLJ>();
        constructor.Services.AddSingleton<IMotorJuego, MotorHttp>();
        constructor.Services.AddSingleton(new OpcionesPausa());
        constructor.Services.AddSingleton<ICatalogoPiezasEspeciales>(
            new CatalogoFalso(
                new TipoPiezaEspecial(1, "COL", "Elimina columna", true),
                new TipoPiezaEspecial(2, "CONFUSION", "Confusión", false),
                new TipoPiezaEspecial(3, "ACELERADOR", "Acelerador", false),
                new TipoPiezaEspecial(4, "BOMBA", "Bomba", false),
                new TipoPiezaEspecial(5, "DOBLE", "Doble puntuación", false),
                new TipoPiezaEspecial(6, "BARREDORA", "Barredora", false)));
        constructor.Services.AddSingleton<ISalaLJ, SalaLJ>();
        constructor.Services.AddSingleton<PublicadorSalas>();

        await using var aplicacion = constructor.Build();
        aplicacion.UseCors(ConfiguracionSeguridad.PoliticaCors);
        aplicacion.UseAuthentication();
        aplicacion.UseAuthorization();
        aplicacion.UseRateLimiter();
        aplicacion.MapControllers();
        aplicacion.MapHub<JuegoHub>("/hubs/juego", opciones =>
        {
            opciones.CloseOnAuthenticationExpiration = true;
        });
        aplicacion.MapGet("/salud", () =>
        {
            return Results.Ok(new { Estado = "Prueba local" });
        });

        var conexiones = new List<HubPrueba>();
        var verificaciones = 0;

        void Verificar(bool condicion, string nombre)
        {
            if (!condicion)
            {
                throw new InvalidOperationException(nombre);
            }

            verificaciones++;
            Console.WriteLine("OK HTTP: " + nombre);
        }

        try
        {
            await aplicacion.StartAsync();
            var direccion = new Uri(aplicacion.Urls.Single());
            using var cliente = new HttpClient(new HttpClientHandler { UseCookies = false })
            {
                BaseAddress = direccion,
                Timeout = TimeSpan.FromSeconds(15)
            };

            using var salud = await cliente.GetAsync("/salud");
            Verificar(salud.StatusCode == HttpStatusCode.OK, "salud");

            using var sinSesion = await cliente.GetAsync("/api/usuarios/yo");
            Verificar(sinSesion.StatusCode == HttpStatusCode.Unauthorized, "rutas protegidas sin sesión");

            using var desafioGoogle = await cliente.PostAsync("/api/google/desafio", null);
            var desafio = await desafioGoogle.Content.ReadFromJsonAsync<Respuesta<JsonElement>>(Json);
            Verificar(desafioGoogle.StatusCode == HttpStatusCode.OK && desafio!.Data.GetProperty("nonce").GetString()!.Length == 64,
                "Google entrega un desafío criptográfico sin autenticar al jugador");
            using var googleFalso = await cliente.PostAsJsonAsync("/api/google/ingresar", new
            {
                idToken = "token-inventado",
                desafioId = desafio!.Data.GetProperty("id").GetString()
            });
            Verificar(googleFalso.StatusCode == HttpStatusCode.Unauthorized, "Google rechaza un ID token inventado por HTTP");
            using var vinculoSinSesion = await cliente.PostAsJsonAsync("/api/google/vincular", new { idToken = "falso", desafioId = new string('A', 64) });
            Verificar(vinculoSinSesion.StatusCode == HttpStatusCode.Unauthorized, "Google no vincula perfiles sin JWT de Tetris");

            using var invalido = await cliente.PostAsJsonAsync("/api/usuarios/registrar", new { });
            var errorValidacion = await invalido.Content.ReadFromJsonAsync<Respuesta<JsonElement>>(Json);
            Verificar(invalido.StatusCode == HttpStatusCode.BadRequest &&
                errorValidacion?.Success == false, "validación con Respuesta<T>");

            using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/salas");
            preflight.Headers.Add("Origin", "http://localhost:8100");
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            preflight.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
            using var cors = await cliente.SendAsync(preflight);
            Verificar(cors.StatusCode == HttpStatusCode.NoContent &&
                cors.Headers.GetValues("Access-Control-Allow-Origin").Single() == "http://localhost:8100" &&
                cors.Headers.GetValues("Access-Control-Allow-Credentials").Single() == "true",
                "CORS acepta el origen Ionic y Authorization");

            foreach (var origen in new[] { "https://localhost", "http://localhost", "capacitor://localhost" })
            {
                using var preflightMovil = new HttpRequestMessage(HttpMethod.Options, "/hubs/juego/negotiate");
                preflightMovil.Headers.Add("Origin", origen);
                preflightMovil.Headers.Add("Access-Control-Request-Method", "POST");
                using var movil = await cliente.SendAsync(preflightMovil);
                Verificar(movil.Headers.GetValues("Access-Control-Allow-Origin").Single() == origen,
                    "CORS admite " + origen);
            }
            using var origenAjeno = new HttpRequestMessage(HttpMethod.Options, "/api/salas");
            origenAjeno.Headers.Add("Origin", "https://origen-no-permitido.invalid");
            origenAjeno.Headers.Add("Access-Control-Request-Method", "POST");
            using var corsRechazado = await cliente.SendAsync(origenAjeno);
            Verificar(!corsRechazado.Headers.Contains("Access-Control-Allow-Origin"),
                "CORS no autoriza orígenes ajenos");

            string[] origenesLocales = ["http://192.168.1.50:8100", "http://10.0.0.7:4200", "http://172.20.1.2:8100"];
            string[] origenesPublicos = ["http://8.8.8.8:8100", "http://172.32.0.1:8100", "http://192.169.1.1"];

            foreach (var origenLocal in origenesLocales)
            {
                using var preflightRed = new HttpRequestMessage(HttpMethod.Options, "/api/salas");
                preflightRed.Headers.Add("Origin", origenLocal);
                preflightRed.Headers.Add("Access-Control-Request-Method", "GET");
                using var redLocal = await cliente.SendAsync(preflightRed);
                Verificar(redLocal.Headers.Contains("Access-Control-Allow-Origin"),
                    "CORS de red local admite " + origenLocal);
            }

            foreach (var origenPublico in origenesPublicos)
            {
                using var preflightPublico = new HttpRequestMessage(HttpMethod.Options, "/api/salas");
                preflightPublico.Headers.Add("Origin", origenPublico);
                preflightPublico.Headers.Add("Access-Control-Request-Method", "GET");
                using var rechazado = await cliente.SendAsync(preflightPublico);
                Verificar(!rechazado.Headers.Contains("Access-Control-Allow-Origin"),
                    "CORS de red local no admite " + origenPublico);
            }

            var sesiones = new List<TAutenticacion>();

            for (var numero = 1; numero <= 4; numero++)
            {
                var registro = new TRegistroUsuario
                {
                    NombreUsuario = "IonicPrueba" + numero
                };

                using var registrado = await cliente.PostAsJsonAsync("/api/usuarios/registrar", registro);
                var respuesta = await registrado.Content.ReadFromJsonAsync<Respuesta<TAutenticacion>>(Json);
                Verificar(registrado.StatusCode == HttpStatusCode.Created && respuesta?.Data?.Token.Length > 0,
                    "registro con nickname emite JWT " + numero);
                sesiones.Add(respuesta!.Data!);
            }

            using var duplicado = await cliente.PostAsJsonAsync("/api/usuarios/registrar",
                new TRegistroUsuario { NombreUsuario = " ionicPRUEBA1 " });
            Verificar(duplicado.StatusCode == HttpStatusCode.Conflict, "nickname ocupado devuelve 409 con mayúsculas y espacios");
            using var recuperacion = await cliente.PostAsJsonAsync("/api/usuarios/recuperar",
                new TRecuperarSesion { Credencial = sesiones[0].Credencial! });
            var sesionRecuperada = await recuperacion.Content.ReadFromJsonAsync<Respuesta<TAutenticacion>>(Json);
            Verificar(recuperacion.IsSuccessStatusCode && sesionRecuperada!.Data!.UsuarioId == sesiones[0].UsuarioId
                && sesionRecuperada.Data.Credencial == null, "otro inicio reconoce al dispositivo sin publicar el secreto");
            using var renovacion = await cliente.PostAsJsonAsync("/api/usuarios/renovar",
                new TRecuperarSesion { Credencial = sesiones[0].Credencial! });
            Verificar(renovacion.IsSuccessStatusCode, "sesión renovable sin JWT");
            using var suplantacion = await cliente.PostAsJsonAsync("/api/usuarios/recuperar",
                new TRecuperarSesion { Credencial = new string('0', 64) });
            Verificar(suplantacion.StatusCode == HttpStatusCode.Unauthorized, "otra credencial no recupera el nickname");
            using var disponibilidad = await cliente.GetAsync("/api/usuarios/disponibilidad?nombreUsuario=ionicprueba1");
            var disponible = await disponibilidad.Content.ReadFromJsonAsync<Respuesta<bool>>(Json);
            Verificar(disponible?.Data == false, "disponibilidad pública sin datos privados");

            void UsarSesion(int indice)
            {
                cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    sesiones[indice].Token);
            }

            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesiones[0].Token);

            using var perfil = await cliente.GetAsync("/api/usuarios/yo");
            var usuario = await perfil.Content.ReadFromJsonAsync<Respuesta<TUsuario>>(Json);
            Verificar(perfil.StatusCode == HttpStatusCode.OK && usuario?.Data?.UsuarioId == sesiones[0].UsuarioId,
                "JWT identifica al usuario sin cookies");

            using var parametros = await cliente.GetAsync("/api/configuracion");
            Verificar(parametros.StatusCode == HttpStatusCode.OK, "consulta de configuración");

            using var preferencias = await cliente.PutAsJsonAsync("/api/configuracion", new TConfiguracion
            {
                UsuarioId = 999,
                SonidoActivo = true,
                MostrarControlesPantalla = true
            });
            var guardado = await preferencias.Content.ReadFromJsonAsync<Respuesta<TConfiguracion>>(Json);
            Verificar(guardado?.Data?.UsuarioId == sesiones[0].UsuarioId,
                "el formulario no suplanta la identidad");

            using var resultados = await cliente.GetAsync("/api/resultados/mios");
            using var ranking = await cliente.GetAsync("/api/resultados/ranking");
            Verificar(resultados.StatusCode == HttpStatusCode.OK && ranking.StatusCode == HttpStatusCode.OK,
                "consulta de resultados y ranking");

            cliente.DefaultRequestHeaders.Authorization = null;
            using var queryApi = await cliente.GetAsync("/api/usuarios/yo?access_token=" +
                Uri.EscapeDataString(sesiones[0].Token));
            Verificar(queryApi.StatusCode == HttpStatusCode.Unauthorized,
                "el token en URL no autentica la API REST");

            var opciones = aplicacion.Services.GetRequiredService<OpcionesJwt>();
            var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, "1") };
            var expirado = new JwtSecurityToken(
                opciones.Emisor,
                opciones.Audiencia,
                claims,
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddMinutes(-1),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)),
                    SecurityAlgorithms.HmacSha256));

            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", new JwtSecurityTokenHandler().WriteToken(expirado));
            using var vencido = await cliente.GetAsync("/api/usuarios/yo");
            Verificar(vencido.StatusCode == HttpStatusCode.Unauthorized, "rechazo de JWT vencido");

            var falsificado = new ServicioToken(new OpcionesJwt
            {
                Emisor = opciones.Emisor,
                Audiencia = opciones.Audiencia,
                Clave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            }).Crear(sesiones[0], Guid.NewGuid());
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", falsificado.Token);
            using var firma = await cliente.GetAsync("/api/usuarios/yo");
            Verificar(firma.StatusCode == HttpStatusCode.Unauthorized, "rechazo de firma distinta");

            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesiones[0].Token);
            using var creacion = await cliente.PostAsJsonAsync("/api/salas", new TCrearSala
            {
                Modo = ModoJuego.Compartido
            }, Json);
            var sala = (await creacion.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;

            using var modoInvalido = await cliente.PostAsJsonAsync("/api/salas", new { modo = 99 });
            Verificar(modoInvalido.StatusCode == HttpStatusCode.BadRequest, "validación de modo inexistente");

            // Capacidades nuevas con teléfonos simulados por WebSockets reales, sin SQL.
            foreach (var modo in new[] { ModoJuego.Compartido, ModoJuego.Competitivo })
            {
                foreach (var capacidad in new[] { 2, 3 })
                {
                    UsarSesion(0);
                    using var crearCapacidad = await cliente.PostAsJsonAsync("/api/salas", new TCrearSala
                    {
                        Modo = modo,
                        MaximoJugadores = capacidad
                    }, Json);
                    var nuevaSala = (await crearCapacidad.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;
                    Verificar(crearCapacidad.IsSuccessStatusCode && nuevaSala.MaximoJugadores == capacidad,
                        $"{modo}: crear sala de {capacidad} por HTTP");
                    var participantes = new List<HubPrueba>();
                    try
                    {
                        for (var indice = 0; indice < capacidad; indice++)
                        {
                            UsarSesion(indice);
                            if (indice > 0)
                            {
                                using var ingresar = await cliente.PostAsync($"/api/salas/{nuevaSala.Id}/unirse", null);
                                Verificar(ingresar.IsSuccessStatusCode, $"{modo}/{capacidad}: ingreso {indice + 1}");
                            }
                            var participante = await HubPrueba.ConectarAsync(cliente, direccion, sesiones[indice].Token);
                            participantes.Add(participante);
                            Verificar((await participante.InvocarAsync("Entrar", nuevaSala.Id)).Success,
                                $"{modo}/{capacidad}: conexión {indice + 1}");
                            using var listo = await cliente.PutAsJsonAsync($"/api/salas/{nuevaSala.Id}/listo", true);
                            Verificar(listo.IsSuccessStatusCode, $"{modo}/{capacidad}: listo {indice + 1}");
                        }
                        UsarSesion(capacidad);
                        using var sinEspacio = await cliente.PostAsync($"/api/salas/{nuevaSala.Id}/unirse", null);
                        Verificar(sinEspacio.StatusCode == HttpStatusCode.BadRequest, $"{modo}/{capacidad}: rechaza exceso de participantes");
                        if (modo == ModoJuego.Compartido)
                        {
                            UsarSesion(1);
                            using var intentoInvitado = await cliente.PutAsJsonAsync($"/api/salas/{nuevaSala.Id}/piezas-especiales",
                                new TConfiguracionPiezas { Activas = true, Probabilidad = 100, TiposPermitidos = ["BOMBA"] }, Json);
                            Verificar(intentoInvitado.StatusCode == HttpStatusCode.BadRequest, "invitado de la sala no cambia las piezas");
                            UsarSesion(0);
                            using var seleccionar = await cliente.PutAsJsonAsync($"/api/salas/{nuevaSala.Id}/piezas-especiales",
                                new TConfiguracionPiezas { Activas = true, Probabilidad = 100, TiposPermitidos = ["BOMBA"] }, Json);
                            Verificar(seleccionar.IsSuccessStatusCode, "creador selecciona una pieza por código");
                            await participantes[1].InvocarAsync("Entrar", nuevaSala.Id);
                            Verificar(participantes[1].Estados.Any(estado => estado.Id == nuevaSala.Id
                                && estado.PiezasEspeciales.Probabilidad == 100
                                && estado.PiezasEspeciales.TiposPermitidos!.SequenceEqual(new[] { "BOMBA" })),
                                "SignalR comunica selección y probabilidad al invitado");
                        }
                        UsarSesion(0);
                        using var comenzar = await cliente.PostAsync($"/api/salas/{nuevaSala.Id}/iniciar", null);
                        var empezada = (await comenzar.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;
                        Verificar(comenzar.IsSuccessStatusCode && empezada.Estado == EstadoSala.EnCurso,
                            $"{modo}/{capacidad}: inicia con todos conectados y listos");
                        var movimientoCapacidad = await participantes[0].InvocarAsync("Mover", nuevaSala.Id,
                            new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
                        Verificar(movimientoCapacidad.Success && movimientoCapacidad.Data!.Tableros.Length == (modo == ModoJuego.Compartido ? 1 : capacidad),
                            $"{modo}/{capacidad}: movimiento y tableros por SignalR");
                        using var abandonar = await cliente.PostAsync($"/api/salas/{nuevaSala.Id}/abandonar", null);
                        Verificar(abandonar.IsSuccessStatusCode, "cerrar sala de prueba de capacidad");
                    }
                    finally
                    {
                        foreach (var participante in participantes)
                        {
                            await participante.DisposeAsync();
                        }
                    }
                }
            }
            foreach (var capacidad in new[] { 1, 5, 16 })
            {
                UsarSesion(0);
                using var fueraDeRango = await cliente.PostAsJsonAsync("/api/salas", new TCrearSala
                {
                    Modo = ModoJuego.Compartido, MaximoJugadores = capacidad
                }, Json);
                Verificar(fueraDeRango.StatusCode == HttpStatusCode.BadRequest, $"HTTP rechaza capacidad {capacidad}");
            }
            UsarSesion(0);

            for (var indice = 0; indice < sesiones.Count; indice++)
            {
                cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    sesiones[indice].Token);

                if (indice > 0)
                {
                    using var union = await cliente.PostAsync("/api/salas/" + sala.Id + "/unirse", null);
                    Verificar(union.IsSuccessStatusCode, "unirse a la sala " + (indice + 1));
                }

                var conexion = await HubPrueba.ConectarAsync(cliente, direccion, sesiones[indice].Token);
                conexiones.Add(conexion);
                var entrada = await conexion.InvocarAsync("Entrar", sala.Id);
                Verificar(entrada.Success, "hub autentica y permite Entrar " + (indice + 1));

                using var listo = await cliente.PutAsJsonAsync("/api/salas/" + sala.Id + "/listo", true);
                Verificar(listo.IsSuccessStatusCode, "participante listo " + (indice + 1));
            }

            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesiones[0].Token);
            using var inicio = await cliente.PostAsync("/api/salas/" + sala.Id + "/iniciar", null);
            var iniciada = await inicio.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json);
            Verificar(inicio.IsSuccessStatusCode && iniciada?.Data?.Estado == EstadoSala.EnCurso,
                "iniciar compartido con cuatro conexiones reales");

            var fueraDeTurno = await conexiones[1].InvocarAsync("Mover", sala.Id,
                new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
            Verificar(!fueraDeTurno.Success, "el hub rechaza un jugador fuera de turno");

            var movimiento = await conexiones[0].InvocarAsync("Mover", sala.Id,
                new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
            Verificar(movimiento.Success && movimiento.Data?.UsuarioTurno == sesiones[1].UsuarioId,
                "el hub conserva Mover y el cambio de turno");
            Verificar(conexiones[0].Estados.Any(estado =>
                estado.Jugadores.Any(jugador => jugador.Puntos > 0)),
                "EstadoActualizado incluye puntuaciones");

            await conexiones[0].DisposeAsync();
            var reconectada = await HubPrueba.ConectarAsync(cliente, direccion, sesiones[0].Token);
            conexiones.Add(reconectada);
            var recuperada = await reconectada.InvocarAsync("Entrar", sala.Id);
            Verificar(recuperada.Success && recuperada.Data!.Jugadores[0].UltimaSecuencia == 1,
                "reconectar y recuperar el estado autoritativo");

            // ---- Pausa por votación con SignalR ----
            var solicitudPausa = await reconectada.InvocarAsync("SolicitarPausa", sala.Id);
            Verificar(solicitudPausa.Success && solicitudPausa.Data!.Pausa.Fase == FasePausa.Votando,
                "el hub abre la votación de pausa");
            var votoDuplicado = await reconectada.InvocarAsync("VotarPausa", sala.Id, true);
            Verificar(!votoDuplicado.Success, "el hub rechaza el voto duplicado del solicitante");
            var segundoVoto = await conexiones[1].InvocarAsync("VotarPausa", sala.Id, true);
            Verificar(segundoVoto.Success && segundoVoto.Data!.Pausa.Fase == FasePausa.Votando,
                "dos votos de cuatro no aprueban la pausa");
            var tercerVoto = await conexiones[2].InvocarAsync("VotarPausa", sala.Id, true);
            Verificar(tercerVoto.Success && tercerVoto.Data!.Pausa.Fase == FasePausa.Pausada,
                "tres votos de cuatro aprueban la pausa");

            var movimientoEnPausa = await conexiones[1].InvocarAsync("Mover", sala.Id,
                new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 2 });
            Verificar(!movimientoEnPausa.Success, "el servidor rechaza movimientos durante la pausa");

            await conexiones[3].InvocarAsync("Entrar", sala.Id);
            var eventosRecibidos = conexiones[3].Eventos.Select(evento => evento.Nombre).ToArray();
            Verificar(eventosRecibidos.Contains("PausaSolicitada") &&
                eventosRecibidos.Contains("VotoPausaRegistrado") &&
                eventosRecibidos.Contains("PausaIniciada"),
                "los jugadores reciben los eventos de pausa por SignalR");
            var pausaIniciada = conexiones[3].Eventos.Last(evento => evento.Nombre == "PausaIniciada").Datos;
            Verificar(pausaIniciada.FinPausaUtc - pausaIniciada.InicioPausaUtc == TimeSpan.FromSeconds(180),
                "el servidor fija una pausa de tres minutos");

            UsarSesion(0);
            using var pausaRepetida = await cliente.PostAsync("/api/salas/" + sala.Id + "/pausa", null);
            Verificar(pausaRepetida.StatusCode == HttpStatusCode.BadRequest, "REST rechaza pausar una partida pausada");

            // ---- Salas públicas y privadas ----
            using var crearPublica = await cliente.PostAsJsonAsync("/api/salas", new TCrearSala
            {
                Modo = ModoJuego.Compartido
            }, Json);
            var publica = (await crearPublica.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;
            using var crearPrivada = await cliente.PostAsJsonAsync("/api/salas", new TCrearSala
            {
                Modo = ModoJuego.Competitivo,
                Privada = true
            }, Json);
            var privada = (await crearPrivada.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;
            Verificar(privada.Privada && privada.CodigoPrivado?.Length == 6, "el servidor genera el código privado");

            using var listado = await cliente.GetAsync("/api/salas");
            var publicas = (await listado.Content.ReadFromJsonAsync<Respuesta<TSalaPublica[]>>(Json))!.Data!;
            Verificar(publicas.Any(item => item.Id == publica.Id) && publicas.All(item => item.Id != privada.Id),
                "el listado público oculta las salas privadas");

            UsarSesion(1);
            using var porIdentificador = await cliente.PostAsync("/api/salas/" + privada.Id + "/unirse", null);
            Verificar(porIdentificador.StatusCode == HttpStatusCode.BadRequest,
                "una sala privada no admite ingreso por identificador");
            using var codigoErroneo = await cliente.PostAsync("/api/salas/codigo/ZZZZZZ/unirse", null);
            Verificar(codigoErroneo.StatusCode == HttpStatusCode.BadRequest, "un código erróneo se rechaza");
            using var porCodigo = await cliente.PostAsync(
                "/api/salas/codigo/" + privada.CodigoPrivado + "/unirse", null);
            Verificar(porCodigo.IsSuccessStatusCode, "el código privado permite ingresar");

            // ---- Configuración de piezas especiales ----
            UsarSesion(0);
            foreach (var invalida in new[] { 0, 101 })
            {
                using var rechazo = await cliente.PutAsJsonAsync(
                    "/api/salas/" + publica.Id + "/piezas-especiales",
                    new TConfiguracionPiezas { Activas = true, Probabilidad = invalida },
                    Json);
                Verificar(rechazo.StatusCode == HttpStatusCode.BadRequest, "probabilidad " + invalida + " rechazada");
            }

            using var configuracionValida = await cliente.PutAsJsonAsync(
                "/api/salas/" + publica.Id + "/piezas-especiales",
                new TConfiguracionPiezas { Activas = true, Probabilidad = 50 },
                Json);
            var configurada = (await configuracionValida.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;
            Verificar(configuracionValida.IsSuccessStatusCode && configurada.PiezasEspeciales.Probabilidad == 50,
                "el creador configura las piezas especiales");

            // ---- Configuración completa de la sala ----
            using var sinPiezas = await cliente.PutAsJsonAsync(
                "/api/salas/" + publica.Id + "/configuracion",
                new TConfiguracionSala { PiezasNormales = [] },
                Json);
            Verificar(sinPiezas.StatusCode == HttpStatusCode.BadRequest, "al menos una pieza normal");

            using var completa = await cliente.PutAsJsonAsync(
                "/api/salas/" + publica.Id + "/configuracion",
                new TConfiguracionSala
                {
                    PiezasNormales = ["I", "O"],
                    Velocidad = VelocidadInicial.Rapida,
                    Puntos = new TPuntosLineas { UnaLinea = 10, DosLineas = 20, TresLineas = 30, CuatroLineas = 40 }
                },
                Json);
            var completaSala = (await completa.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;
            Verificar(
                completa.IsSuccessStatusCode
                    && string.Join(",", completaSala.PiezasNormales) == "I,O"
                    && completaSala.Velocidad == VelocidadInicial.Rapida
                    && completaSala.Puntos.CuatroLineas == 40,
                "el creador configura piezas normales, velocidad y puntos");

            UsarSesion(1);
            using var ajena = await cliente.PutAsJsonAsync(
                "/api/salas/" + publica.Id + "/piezas-especiales",
                new TConfiguracionPiezas { Activas = false },
                Json);
            Verificar(ajena.StatusCode == HttpStatusCode.BadRequest, "quien no pertenece a la sala no la configura");
            UsarSesion(0);

            // ---- Reglas definitivas con REST y conexiones SignalR reales ----
            using var crearClasica = await cliente.PostAsJsonAsync("/api/salas", new TCrearSala
            {
                Modo = ModoJuego.Competitivo,
                PiezasEspeciales = new TConfiguracionPiezas { Activas = true, Probabilidad = 100 }
            }, Json);
            var clasica = (await crearClasica.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;
            Verificar(!clasica.PiezasEspeciales.Activas, "REST fuerza competitivo clásico");
            var activas = new[] { reconectada, conexiones[1], conexiones[2], conexiones[3] };

            for (var indice = 0; indice < 4; indice++)
            {
                UsarSesion(indice);

                if (indice > 0)
                {
                    using var ingreso = await cliente.PostAsync("/api/salas/" + clasica.Id + "/unirse", null);
                    Verificar(ingreso.IsSuccessStatusCode, "ingreso a competitivo " + indice);
                }

                await activas[indice].InvocarAsync("Entrar", clasica.Id);
                using var listo = await cliente.PutAsJsonAsync("/api/salas/" + clasica.Id + "/listo", true, Json);
                Verificar(listo.IsSuccessStatusCode, "listo en competitivo " + indice);
            }

            UsarSesion(0);
            using var iniciarClasica = await cliente.PostAsync("/api/salas/" + clasica.Id + "/iniciar", null);
            var clasicaIniciada = (await iniciarClasica.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;
            Verificar(clasicaIniciada.Tableros.All(tablero => tablero.PiezaEspecial == null),
                "SignalR y REST exponen solo piezas normales en competitivo");
            var intrusa = await HubPrueba.ConectarAsync(cliente, direccion, sesiones[1].Token);
            conexiones.Add(intrusa);
            var sinEntrar = await intrusa.InvocarAsync("Mover", clasica.Id,
                new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
            Verificar(!sinEntrar.Success, "una conexión que no entró no puede mover");

            UsarSesion(1);
            using var salida = await cliente.PostAsync("/api/salas/" + clasica.Id + "/abandonar", null);
            var abandonada = (await salida.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;
            Verificar(abandonada.Estado == EstadoSala.EnCurso && abandonada.Jugadores[1].Perdido,
                "abandono REST elimina solo al participante");
            Verificar(!(await activas[1].InvocarAsync("Entrar", clasica.Id)).Success,
                "el participante que abandonó no recupera su plaza");
            Verificar(activas[1].EventosSala.Any(evento => evento.Nombre == "JugadorAbandono"),
                "SignalR notifica el abandono voluntario");

            var cancelada = await reconectada.InvocarAsync("Abandonar", clasica.Id);
            Verificar(cancelada.Data!.Estado == EstadoSala.Cancelada && cancelada.Data.Ganadores.Length == 0,
                "abandono del creador por hub cancela sin ganador");
            Verificar(!(await activas[2].InvocarAsync("Entrar", clasica.Id)).Success,
                "la sala cancelada rechaza el ingreso");
            Verificar(activas[2].EventosSala.Any(evento => evento.Nombre == "SalaCerrada"),
                "los participantes reciben SalaCerrada");
            var estadosAntes = activas[2].Estados.Count;
            await aplicacion.Services.GetRequiredService<PublicadorSalas>().PublicarEstadoAsync(cancelada.Data);
            await activas[2].InvocarAsync("Entrar", clasica.Id);
            Verificar(activas[2].Estados.Count == estadosAntes, "el cierre retira conexiones del grupo SignalR");
            UsarSesion(0);

            await VerificarEfectosAsync(aplicacion, cliente, sesiones, activas, UsarSesion, Verificar);

            // Verifica el contrato HTTP de otra ronda sin introducir SQL en esta suite.
            var servicioSalas = aplicacion.Services.GetRequiredService<ISalaLJ>();
            var salaRondas = servicioSalas.Crear(sesiones[0].UsuarioId, sesiones[0].NombreUsuario,
                new TCrearSala { Modo = ModoJuego.Compartido }).Data!;
            for (var indice = 0; indice < sesiones.Count; indice++)
            {
                if (indice > 0)
                {
                    servicioSalas.Unirse(salaRondas.Id, sesiones[indice].UsuarioId, sesiones[indice].NombreUsuario);
                }

                servicioSalas.Conectar(salaRondas.Id, sesiones[indice].UsuarioId, "ronda-http-" + indice);
                servicioSalas.Preparar(salaRondas.Id, sesiones[indice].UsuarioId, true);
            }

            servicioSalas.Iniciar(salaRondas.Id, sesiones[0].UsuarioId);
            using var activa = await cliente.PostAsync($"/api/salas/{salaRondas.Id}/nueva-ronda", null);
            Verificar(activa.StatusCode == HttpStatusCode.BadRequest, "HTTP rechaza otra ronda durante partida activa");
            for (var indice = 1; indice < sesiones.Count; indice++)
            {
                servicioSalas.Abandonar(salaRondas.Id, sesiones[indice].UsuarioId);
            }

            var finalRonda = servicioSalas.Obtener(salaRondas.Id, sesiones[0].UsuarioId).Data!;
            using var sinGuardar = await cliente.PostAsync($"/api/salas/{salaRondas.Id}/nueva-ronda", null);
            Verificar(sinGuardar.StatusCode == HttpStatusCode.BadRequest, "HTTP espera guardado del histórico");
            servicioSalas.ConfirmarPersistencia(salaRondas.Id, finalRonda.RevisionPersistencia);
            UsarSesion(1);
            using var invitadoRonda = await cliente.PostAsync($"/api/salas/{salaRondas.Id}/nueva-ronda", null);
            Verificar(invitadoRonda.StatusCode == HttpStatusCode.BadRequest, "HTTP impide que otro jugador prepare otra ronda");
            UsarSesion(0);
            using var rondaNueva = await cliente.PostAsync($"/api/salas/{salaRondas.Id}/nueva-ronda", null);
            var nuevaRonda = await rondaNueva.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json);
            Verificar(rondaNueva.IsSuccessStatusCode && nuevaRonda!.Data!.NumeroRonda == 2
                && nuevaRonda.Data.Estado == EstadoSala.Esperando && nuevaRonda.Data.Jugadores.Length == 1,
                "HTTP prepara siguiente ronda con quienes permanecen");
            Verificar(nuevaRonda!.Data!.Jugadores[0].VictoriasSala == 1
                && nuevaRonda.Data.Jugadores[0].Corona && !nuevaRonda.Data.Jugadores[0].GanoRonda,
                "HTTP distingue corona de sala y victoria de la ronda reiniciada");

            var conexionRevocable = await HubPrueba.ConectarAsync(cliente, direccion, sesiones[0].Token);
            conexiones.Add(conexionRevocable);
            await conexionRevocable.InvocarAsync("Entrar", salaRondas.Id);
            using var cierre = await cliente.PostAsync("/api/usuarios/logout", null);
            Verificar(cierre.StatusCode == HttpStatusCode.NoContent, "logout compatible con Bearer");

            using var revocada = await cliente.PostAsJsonAsync("/api/usuarios/recuperar",
                new TRecuperarSesion { Credencial = sesiones[0].Credencial! });
            Verificar(revocada.StatusCode == HttpStatusCode.Unauthorized, "logout invalida la credencial persistente");
            using var perfilRevocado = await cliente.GetAsync("/api/usuarios/yo");
            Verificar(perfilRevocado.StatusCode == HttpStatusCode.Unauthorized, "logout invalida JWT del dispositivo");
            var hubRevocado = false;
            try
            {
                await conexionRevocable.InvocarAsync("Entrar", salaRondas.Id);
            }
            catch (Exception excepcion) when (excepcion is WebSocketException || excepcion is InvalidOperationException)
            {
                hubRevocado = true;
            }

            Verificar(hubRevocado, "SignalR rechaza acciones de un WebSocket revocado que ya estaba conectado");
            using var loginAnterior = await cliente.PostAsJsonAsync("/api/usuarios/login", new { nombreUsuario = "IonicPrueba1", contrasena = "inutil" });
            Verificar(loginAnterior.StatusCode == HttpStatusCode.NotFound, "login tradicional retirado");

            Console.WriteLine(verificaciones + " comprobaciones HTTP/SignalR correctas, sin modificar SQL.");
            return 0;
        }
        catch (Exception excepcion)
        {
            Console.WriteLine("ERROR HTTP/SignalR: " + excepcion.Message);
            return 1;
        }
        finally
        {
            foreach (var conexion in conexiones)
            {
                await conexion.DisposeAsync();
            }

            await aplicacion.StopAsync();
        }
    }

    private static async Task VerificarEfectosAsync(
        WebApplication aplicacion,
        HttpClient cliente,
        IReadOnlyList<TAutenticacion> sesiones,
        HubPrueba[] activas,
        Action<int> UsarSesion,
        Action<bool, string> Verificar)
    {
        // ---- Efectos especiales mediante REST y cuatro WebSockets reales ----
        using var consultaTipos = await cliente.GetAsync("/api/salas/tipos-especiales");
        var tipos = await consultaTipos.Content.ReadFromJsonAsync<Respuesta<TipoPiezaEspecial[]>>(Json);
        Verificar(consultaTipos.IsSuccessStatusCode && tipos!.Data!.Any(tipo => tipo.Codigo == "DOBLE"),
            "REST entrega los códigos activos para seleccionar los tipos especiales");

        foreach (var codigo in new[] { "CONFUSION", "ACELERADOR", "BOMBA", "DOBLE", "BARREDORA" })
        {
            UsarSesion(0);
            using var crearEfecto = await cliente.PostAsJsonAsync("/api/salas", new TCrearSala
            {
                Modo = ModoJuego.Compartido,
                PiezasNormales = ["I"],
                PiezasEspeciales = new TConfiguracionPiezas
                {
                    Activas = true,
                    Probabilidad = 100,
                    TiposPermitidos = [codigo]
                }
            }, Json);
            var salaEfecto = (await crearEfecto.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;

            for (var indice = 0; indice < 4; indice++)
            {
                UsarSesion(indice);
                if (indice > 0)
                {
                    using var unionEfecto = await cliente.PostAsync("/api/salas/" + salaEfecto.Id + "/unirse", null);
                    Verificar(unionEfecto.IsSuccessStatusCode, codigo + ": ingreso " + indice);
                }

                Verificar((await activas[indice].InvocarAsync("Entrar", salaEfecto.Id)).Success,
                    codigo + ": conexión " + indice);
                using var listoEfecto = await cliente.PutAsJsonAsync("/api/salas/" + salaEfecto.Id + "/listo", true, Json);
                Verificar(listoEfecto.IsSuccessStatusCode, codigo + ": listo " + indice);
            }

            UsarSesion(0);
            using var iniciarEfecto = await cliente.PostAsync("/api/salas/" + salaEfecto.Id + "/iniciar", null);
            var generado = (await iniciarEfecto.Content.ReadFromJsonAsync<Respuesta<TSala>>(Json))!.Data!;
            Verificar(generado.Tableros.Single().PiezaEspecial == codigo, codigo + ": generación autoritativa");
            if (codigo == "BARREDORA")
            {
                // Estado de prueba colocado en el mismo tablero del motor real, antes de publicar por el hub.
                var motorHttp = (MotorHttp)aplicacion.Services.GetRequiredService<IMotorJuego>();
                motorHttp.PrepararBarredora();
            }
            var activacion = await activas[0].InvocarAsync("Mover", salaEfecto.Id,
                new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
            Verificar(activacion.Success && activacion.Data!.UltimoEfecto!.Codigo == codigo,
                codigo + ": el hub devuelve la activación y los puntos de la jugada");
            await activas[3].InvocarAsync("Entrar", salaEfecto.Id);
            var recibido = activas[3].Estados.Last(estado => estado.Id == salaEfecto.Id);
            Verificar(recibido.UltimoEfecto!.Codigo == codigo && recibido.Tableros.Length == 1
                && recibido.UltimoEfecto.PuntosJugada == recibido.Jugadores[0].Puntos,
                codigo + ": EstadoActualizado sincroniza efecto, tablero y puntuación en otro teléfono");

            if (codigo == "CONFUSION" || codigo == "ACELERADOR")
            {
                var efecto = recibido.Efectos!.Single();
                Verificar(efecto.UsuarioId == sesiones[1].UsuarioId && efecto.Activo
                    && efecto.MilisegundosRestantes > 0, codigo + ": receptor y tiempo restante por SignalR");
                if (codigo == "CONFUSION")
                {
                    var columna = recibido.Tableros[0].Pieza.Min(celda => celda.Columna);
                    var invertido = await activas[1].InvocarAsync("Mover", salaEfecto.Id,
                        new TMovimiento { Movimiento = Movimiento.Izquierda, Secuencia = 1 });
                    Verificar(invertido.Data!.Tableros[0].Pieza.Min(celda => celda.Columna) == columna + 1,
                        "SignalR valida confusión en el servidor");
                }

                var logicaSalas = aplicacion.Services.GetRequiredService<ISalaLJ>();
                var publicador = aplicacion.Services.GetRequiredService<PublicadorSalas>();
                foreach (var actualizado in logicaSalas.Avanzar(efecto.FinUtc.AddMilliseconds(1)))
                {
                    await publicador.PublicarEstadoAsync(actualizado);
                }

                await activas[3].InvocarAsync("Entrar", salaEfecto.Id);
                Verificar(activas[3].Estados.Last(estado => estado.Id == salaEfecto.Id).Efectos!
                    .Any(temporal => !temporal.Activo && temporal.MotivoFin == "TiempoAgotado"),
                    codigo + ": vencimiento sincronizado por SignalR");
            }
            else if (codigo == "BOMBA")
            {
                Verificar(recibido.UltimoEfecto.CeldasEliminadas.Length > 0,
                    "SignalR incluye las coordenadas de explosión de la bomba");
            }
            else if (codigo == "BARREDORA")
            {
                Verificar(recibido.UltimoEfecto.Tipo == EfectoEspecial.Barredora
                    && recibido.UltimoEfecto.Estado == "Aplicado"
                    && recibido.UltimoEfecto.CeldasEliminadas.Any(celda => celda.Fila == 9)
                    && recibido.UltimoEfecto.CeldasEliminadas.All(celda => celda.Fila < 10),
                    "SignalR identifica Barredora aplicada y sus coordenadas");
                Verificar(recibido.Tableros[0].Celdas.Take(10).All(fila => fila.All(celda => celda == 0))
                    && recibido.Tableros[0].Celdas[12][0] == 7 && recibido.UsuarioTurno == sesiones[1].UsuarioId,
                    "SignalR transmite la limpieza superior, conserva la inferior y el turno");
            }
        }
    }

    private sealed class MotorHttp : IMotorJuego
    {
        private readonly MotorJuego motor = new();
        private TableroJuego? ultimoTablero;

        public void PrepararBarredora()
        {
            if (ultimoTablero == null)
            {
                throw new InvalidOperationException("Falta el tablero de prueba.");
            }

            ultimoTablero.Celdas[0][0] = 7;
            ultimoTablero.Celdas[9][0] = 7;
            ultimoTablero.Celdas[12][0] = 7;
        }

        public TableroJuego CrearTablero()
        {
            return CrearTablero(ConfiguracionPiezas.SoloNormales);
        }

        public TableroJuego CrearTablero(ConfiguracionPiezas configuracion)
        {
            ultimoTablero = motor.CrearTablero(configuracion);
            return ultimoTablero;
        }

        public PasoJuego Mover(TableroJuego tablero, Movimiento movimiento)
        {
            return motor.Mover(tablero, movimiento);
        }

        public PasoJuego Avanzar(TableroJuego tablero)
        {
            return motor.Avanzar(tablero);
        }

        public TTablero ObtenerEstado(TableroJuego tablero, int? usuarioId)
        {
            return motor.ObtenerEstado(tablero, usuarioId);
        }
    }

    private sealed class HubPrueba : IAsyncDisposable
    {
        private readonly ClientWebSocket _socket = new();
        private readonly Queue<string> _mensajes = new();
        private readonly StringBuilder _pendiente = new();
        private int _secuencia;
        public List<TSala> Estados { get; } = new();
        public List<(string Nombre, JsonElement Datos)> EventosSala { get; } = new();
        public List<(string Nombre, TPausa Datos)> Eventos { get; } = new();

        public static async Task<HubPrueba> ConectarAsync(HttpClient http, Uri baseUrl, string token)
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post,
                "/hubs/juego/negotiate?negotiateVersion=1");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var negociacion = await http.SendAsync(solicitud);
            negociacion.EnsureSuccessStatusCode();
            using var datos = JsonDocument.Parse(await negociacion.Content.ReadAsStringAsync());
            var identificador = datos.RootElement.GetProperty("connectionToken").GetString();
            var direccion = new UriBuilder(baseUrl)
            {
                Scheme = "ws",
                Path = "/hubs/juego",
                Query = "id=" + Uri.EscapeDataString(identificador!) +
                    "&access_token=" + Uri.EscapeDataString(token)
            };

            var cliente = new HubPrueba();

            try
            {
                using var tiempo = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await cliente._socket.ConnectAsync(direccion.Uri, tiempo.Token);
                await cliente.EnviarAsync(new { protocol = "json", version = 1 });
                var confirmacion = await cliente.LeerAsync();
                using var handshake = JsonDocument.Parse(confirmacion);

                if (handshake.RootElement.TryGetProperty("error", out _))
                {
                    throw new InvalidOperationException("El servidor rechazó el protocolo SignalR.");
                }

                return cliente;
            }
            catch
            {
                await cliente.DisposeAsync();
                throw;
            }
        }

        private async Task EnviarAsync(object contenido)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(contenido, Json) + "\u001e");
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task<string> LeerAsync()
        {
            using var tiempo = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var bytes = new byte[65536];

            while (_mensajes.Count == 0)
            {
                var recibido = await _socket.ReceiveAsync(bytes, tiempo.Token);

                if (recibido.MessageType == WebSocketMessageType.Close)
                {
                    throw new InvalidOperationException("El servidor cerró la conexión SignalR.");
                }

                _pendiente.Append(Encoding.UTF8.GetString(bytes, 0, recibido.Count));
                var contenido = _pendiente.ToString();
                var separador = contenido.IndexOf('\u001e');

                while (separador >= 0)
                {
                    _mensajes.Enqueue(contenido[..separador]);
                    contenido = contenido[(separador + 1)..];
                    separador = contenido.IndexOf('\u001e');
                }

                _pendiente.Clear();
                _pendiente.Append(contenido);
            }

            return _mensajes.Dequeue();
        }

        public async Task<Respuesta<TSala>> InvocarAsync(string metodo, params object[] argumentos)
        {
            var invocacion = (++_secuencia).ToString();
            await EnviarAsync(new { type = 1, invocationId = invocacion, target = metodo, arguments = argumentos });

            while (true)
            {
                using var documento = JsonDocument.Parse(await LeerAsync());
                var mensaje = documento.RootElement;
                var tipo = mensaje.GetProperty("type").GetInt32();

                if (tipo == 1)
                {
                    GuardarMensajeDelServidor(mensaje);
                }

                if (tipo == 3 && mensaje.GetProperty("invocationId").GetString() == invocacion)
                {
                    if (mensaje.TryGetProperty("error", out _))
                    {
                        throw new InvalidOperationException("Falló la invocación del hub: " + metodo);
                    }

                    return mensaje.GetProperty("result").Deserialize<Respuesta<TSala>>(Json)!;
                }
            }
        }

        private void GuardarMensajeDelServidor(JsonElement mensaje)
        {
            var nombre = mensaje.GetProperty("target").GetString()!;
            var argumento = mensaje.GetProperty("arguments")[0];

            if (nombre == "EstadoActualizado")
            {
                Estados.Add(argumento.Deserialize<TSala>(Json)!);
                return;
            }

            if (nombre == "SalaCerrada" || nombre == "JugadorAbandono")
            {
                EventosSala.Add((nombre, argumento.Clone()));
                return;
            }

            // Los demás mensajes del servidor son los eventos de pausa.
            Eventos.Add((nombre, argumento.Deserialize<TPausa>(Json)!));
        }

        public async ValueTask DisposeAsync()
        {
            if (_socket.State == WebSocketState.Open)
            {
                using var tiempo = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                try
                {
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Fin de prueba", tiempo.Token);
                }
                catch (Exception)
                {
                    _socket.Abort();
                }
            }

            _socket.Dispose();
        }
    }
}



