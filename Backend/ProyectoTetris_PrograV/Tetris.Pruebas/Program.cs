using System.Linq.Expressions;
using System.Text.Json;
using AccesoDatos.Contexto;
using AutoMapper;
using Dominio.DTO;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazAD;
using Dominio.InterfazJuego;
using Dominio.Juego;
using LogicaJuego;
using LogicaJuego.Implementacions;
using LogicaJuego.Motor;
using LogicaJuego.Servicios;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tetris.Cliente.Servicios;
using Utilitarios;

// Verificación opcional de solo lectura: no crea tablas ni modifica datos del usuario.
if (args.Contains("--verificar-sql"))
{
    try
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__TetrisDB") ?? "Server=JAFET\\SQLEXPRESS;Database=TetrisDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=5";
        using var db = new TetrisDBContext(new DbContextOptionsBuilder<TetrisDBContext>().UseSqlServer(cadena).Options);
        using var conexion = db.Database.GetDbConnection();
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT OBJECT_NAME(parent_object_id), name, definition FROM sys.check_constraints ORDER BY 1,2";
        using var lector = comando.ExecuteReader();
        Console.WriteLine("Conexión SQL correcta. Restricciones actuales:");
        while (lector.Read())
        {
            Console.WriteLine($"{lector.GetString(0)} | {lector.GetString(1)} | {lector.GetString(2)}");
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"No se pudo verificar SQL: {ex.Message}");
        return 1;
    }
}

if (args.Contains("--integracion-sql"))
{
    return await PruebasSql.EjecutarAsync();
}

if (args.Contains("--integracion-http"))
{
    return await PruebasHttp.EjecutarAsync();
}

var pruebas = new List<(string Nombre, Action Ejecutar)>();
void Prueba(string nombre, Action accion)
{
    pruebas.Add((nombre, accion));
}

void Verificar(bool condicion, string mensaje = "La condición esperada no se cumplió.")
{
    if (!condicion)
    {
        throw new Exception(mensaje);
    }
}

var motor = new MotorJuego();
var config = new MapperConfiguration(c => c.AddProfile<AutoMapperProfile>(), NullLoggerFactory.Instance);
var mapper = config.CreateMapper();
Prueba("AutoMapper valida sus contratos", () => config.AssertConfigurationIsValid());
Prueba("El DTO público no expone hash", () =>
{
    var dto = mapper.Map<TUsuario>(new Usuario { PasswordHash = "secreto", NombreUsuario = "Ana", Correo = "a@a.com" });
    Verificar(!JsonSerializer.Serialize(dto).Contains("secreto"));
    Verificar(!JsonSerializer.Serialize(dto).Contains("PasswordHash"));
});
Prueba("EF conserva las 19 entidades y la relación de tablero", () =>
{
    using var db = new TetrisDBContext(new DbContextOptionsBuilder<TetrisDBContext>().UseSqlServer("Server=localhost;Database=Prueba;Integrated Security=true;TrustServerCertificate=true").Options);
    Verificar(db.Model.GetEntityTypes().Count() == 19);
    Verificar(db.Model.FindEntityType(typeof(Partida))!.FindProperty("CantidadMinimaJugadores")!.GetDefaultValue()!.Equals(4));
});
Prueba("La gravedad baja una pieza sin HTTP", () =>
{
    var t = motor.CrearTablero();
    var fila = t.Fila;
    motor.Avanzar(t);
    Verificar(t.Fila == fila + 1);
});
Prueba("Las paredes impiden salir del tablero", () =>
{
    var t = motor.CrearTablero();
    for (var i = 0; i < 30; i++)
    {
        motor.Mover(t, Movimiento.Izquierda);
    }

    Verificar(motor.ObtenerEstado(t, 1).Pieza.All(p => p.Columna >= 0));
    Verificar(!motor.Mover(t, Movimiento.Izquierda).Cambio);
});
Prueba("Una rotación bloqueada no atraviesa celdas", () =>
{
    var t = motor.CrearTablero();
    t.TipoPieza = 0;
    t.Columna = 3;
    t.Fila = 3;
    t.Celdas[3][5] = 7;
    Verificar(!motor.Mover(t, Movimiento.Rotar).Cambio && t.Rotacion == 0);
});
Prueba("Eliminar una línea desplaza y suma 100 puntos", () =>
{
    var t = motor.CrearTablero();
    t.TipoPieza = 0;
    t.Columna = 3;
    t.Fila = 18;
    for (var c = 0; c < 10; c++)
    {
        t.Celdas[19][c] = c >= 3 && c <= 6 ? 0 : 1;
    }

    motor.Avanzar(t);
    Verificar(t.Lineas == 1 && t.Puntuacion == 100 && t.Celdas[19].All(c => c == 0));
});
Prueba("Cuatro líneas suman 800 puntos y sube el nivel", () =>
{
    var t = motor.CrearTablero();
    t.TipoPieza = 0;
    t.Rotacion = 1;
    t.Columna = 3;
    t.Fila = 16;
    t.Lineas = 9;
    for (var f = 16; f < 20; f++)
    {
        for (var c = 0; c < 10; c++)
        {
            t.Celdas[f][c] = c == 5 ? 0 : 1;
        }
    }

    motor.Avanzar(t);
    Verificar(t.Lineas == 13 && t.Nivel == 2 && t.Puntuacion == 800);
});
Prueba("Sin espacio para una pieza la partida se pierde", () =>
{
    var t = motor.CrearTablero();
    t.TipoPieza = 0;
    t.Columna = 3;
    t.Fila = 18;
    for (var f = 0; f < 4; f++)
    {
        for (var c = 3; c < 7; c++)
        {
            t.Celdas[f][c] = 1;
        }
    }

    motor.Avanzar(t);
    Verificar(t.Perdido);
    Verificar(!motor.Mover(t, Movimiento.Caer).Cambio);
});
Prueba("Caída instantánea fija cuatro bloques", () =>
{
    var t = motor.CrearTablero();
    Verificar(motor.Mover(t, Movimiento.Caer).PiezaFijada);
    Verificar(t.Celdas.Sum(f => f.Count(c => c != 0)) == 4 && t.Puntuacion > 0);
});
(Guid Id, SalaLJ Salas) CrearSala(ModoJuego modo, int cantidad = 4)
{
    var salas = new SalaLJ(new MotorControlado());
    var id = salas.Crear(1, "Jugador 1", new TCrearSala { Modo = modo }).Data!.Id;
    for (var i = 1; i <= cantidad; i++)
    {
        if (i > 1)
        {
            salas.Unirse(id, i, $"Jugador {i}");
        }

        salas.Conectar(id, i, $"conexion-{i}");
        salas.Preparar(id, i, true);
    }

    return (id, salas);
}

Prueba("Multijugador no inicia con tres participantes", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Competitivo, 3);
    Verificar(!salas.Iniciar(id, 1).Success);
});
Prueba("Un invitado no puede iniciar ni consultar una sala ajena", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Compartido);
    Verificar(!salas.Iniciar(id, 2).Success && !salas.Obtener(id, 99).Success);
});
Prueba("Tablero compartido rechaza movimientos fuera de turno", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Compartido);
    Verificar(salas.Iniciar(id, 1).Success);
    Verificar(!salas.Mover(id, 2, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 }).Success);
    var estado = salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 }).Data!;
    Verificar(estado.Tableros.Length == 1 && estado.UsuarioTurno == 2);
});
Prueba("Una secuencia repetida no suma dos veces", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Competitivo);
    salas.Iniciar(id, 1);
    var accion = new TMovimiento
    {
        Movimiento = Movimiento.Bajar,
        Secuencia = 1
    };
    salas.Mover(id, 1, accion);
    Verificar(!salas.Mover(id, 1, accion).Success);
    Verificar(salas.Obtener(id, 1).Data!.Jugadores[0].Puntos == 10);
});
Prueba("Competitivo mantiene al último activo y gana la mayor puntuación", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Competitivo);
    salas.Iniciar(id, 1);
    for (var i = 1; i <= 3; i++)
    {
        salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = i });
    }

    for (var i = 1; i <= 3; i++)
    {
        salas.Mover(id, i, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 10 });
    }

    var estado = salas.Obtener(id, 4).Data!;
    Verificar(estado.Estado == EstadoSala.EnCurso && estado.Ganadores.Length == 0);
    Verificar(!salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 11 }).Success);
    estado = salas.Mover(id, 4, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 }).Data!;
    Verificar(estado.Estado == EstadoSala.Finalizada && estado.Ganadores.SequenceEqual(new[] { 1 }));
    Verificar(estado.Jugadores[0].Puntos == 30 && estado.Jugadores[0].Posicion == 1);
});
Prueba("Los empates comparten el primer puesto", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Competitivo);
    salas.Iniciar(id, 1);
    for (var i = 1; i <= 4; i++)
    {
        salas.Mover(id, i, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
    }

    Verificar(salas.Obtener(id, 1).Data!.Ganadores.Length == 4);
});
Prueba("Reconectar dentro de 30 segundos conserva al jugador", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Competitivo);
    salas.Iniciar(id, 1);
    salas.Desconectar("conexion-1");
    salas.Conectar(id, 1, "nueva");
    salas.Avanzar(DateTime.UtcNow.AddSeconds(31));
    Verificar(!salas.Obtener(id, 1).Data!.Jugadores[0].Perdido);
});
Prueba("Una desconexión vencida cuenta como derrota y salta el turno", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Compartido);
    salas.Iniciar(id, 1);
    salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
    salas.Desconectar("conexion-2");
    salas.Avanzar(DateTime.UtcNow.AddSeconds(31));
    var estado = salas.Obtener(id, 1).Data!;
    Verificar(estado.Jugadores[1].Perdido && estado.UsuarioTurno != 2);
});
Prueba("Una instantánea no permite modificar la sesión", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Competitivo);
    salas.Iniciar(id, 1);
    salas.Obtener(id, 1).Data!.Tableros[0].Celdas[19][0] = 7;
    Verificar(salas.Obtener(id, 1).Data!.Tableros[0].Celdas[19][0] == 0);
});
Prueba("Las revisiones de guardado no ocultan derrotas posteriores", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Competitivo);
    salas.Iniciar(id, 1);
    var inicio = salas.PendientesPersistencia().Single();
    salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
    salas.ConfirmarPersistencia(id, inicio.RevisionPersistencia);
    Verificar(salas.PendientesPersistencia().Single().RevisionPersistencia > inicio.RevisionPersistencia);
});
Prueba("Registro y login usan hash y rechazan duplicados", () =>
{
    var unidad = new UnidadMemoria();
    var acceso = new AccesoUsuarioLJ(
        unidad,
        mapper,
        new PasswordHasher<Usuario>(),
        NullLogger<AccesoUsuarioLJ>.Instance);
    var registro = new TRegistroUsuario
    {
        NombreUsuario = "Ana",
        Correo = "ana@example.com",
        Contrasena = "Prueba123!"
    };
    Verificar(acceso.Registrar(registro).Success);
    Verificar(unidad.Usuarios.Datos[0].PasswordHash != registro.Contrasena);
    Verificar(!acceso.Registrar(registro).Success);
    Verificar(acceso.Autenticar(new TLoginUsuario { NombreUsuario = "Ana", Contrasena = registro.Contrasena }).Success);
    Verificar(!acceso.Autenticar(new TLoginUsuario { NombreUsuario = "Ana", Contrasena = "incorrecta" }).Success);
});
Prueba("UsuarioLJ propaga un error del repositorio", () =>
{
    var unidad = new UnidadMemoria();
    unidad.Usuarios.Fallar = true;
    var logica = new UsuarioLJ(unidad, NullLogger<UsuarioLJ>.Instance, mapper);
    Verificar(!logica.Listar().Success);
});
Prueba("Resultados persisten al perder y las estadísticas se guardan una vez", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Competitivo);
    salas.Iniciar(id, 1);
    var unidad = new UnidadMemoria();
    var registro = new RegistroPartidaLJ(unidad);
    registro.GuardarAsync(salas.Obtener(id, 1).Data!).GetAwaiter().GetResult();
    salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
    salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 2 });
    registro.GuardarAsync(salas.Obtener(id, 1).Data!).GetAwaiter().GetResult();
    Verificar(unidad.Resultados.Datos.Count == 1 && unidad.Resultados.Datos[0].PosicionFinal == 0);
    for (var i = 2; i <= 4; i++)
    {
        salas.Mover(id, i, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
    }

    var final = salas.Obtener(id, 1).Data!;
    registro.GuardarAsync(final).GetAwaiter().GetResult();
    registro.GuardarAsync(final).GetAwaiter().GetResult();
    Verificar(unidad.Resultados.Datos.Count == 4 && unidad.Estadisticas.Datos.Count == 4);
    Verificar(unidad.Estadisticas.Datos.All(e => e.PartidasJugadas == 1));
    Verificar(unidad.Resultados.Datos.Single(r => r.UsuarioId == 1).EsGanador);
});
Prueba("El código de invitación incorpora al participante", () =>
{
    var salas = new SalaLJ(new MotorControlado());
    var creada = salas.Crear(1, "Ana", new TCrearSala { Modo = ModoJuego.Compartido }).Data!;
    Verificar(salas.UnirsePorCodigo(creada.Codigo, 2, "Beto").Data!.Jugadores.Length == 2);
    Verificar(!salas.UnirsePorCodigo("invalido", 3, "Caro").Success);
});
Prueba("Acciones concurrentes duplicadas se procesan una sola vez", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Competitivo);
    salas.Iniciar(id, 1);
    Parallel.For(0, 30, _ => salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 }));
    var jugador = salas.Obtener(id, 1).Data!.Jugadores[0];
    Verificar(jugador.Puntos == 10 && jugador.UltimaSecuencia == 1);
});
Prueba("Varias conexiones no causan una derrota al cerrar solo una", () =>
{
    var(id, salas) = CrearSala(ModoJuego.Competitivo);
    salas.Iniciar(id, 1);
    salas.Conectar(id, 1, "segunda");
    Verificar(salas.Desconectar("conexion-1").Length == 1);
    salas.Avanzar(DateTime.UtcNow.AddSeconds(31));
    Verificar(!salas.Obtener(id, 1).Data!.Jugadores[0].Perdido);
});
Prueba("Las preferencias no aceptan la identidad de otro formulario", () =>
{
    var unidad = new UnidadMemoria();
    var servicio = new ConfiguracionLJ(unidad, mapper, NullLogger<ConfiguracionLJ>.Instance);
    var datos = new TConfiguracion
    {
        UsuarioId = 99,
        ConfiguracionId = 87,
        SonidoActivo = true
    };
    var respuesta = servicio.GuardarPorUsuario(1, datos);
    Verificar(respuesta.Success && respuesta.Data!.UsuarioId == 1 && respuesta.Data.ConfiguracionId == 0);
});
Prueba("El cliente HTTP convierte un 401 en una respuesta fallida", () =>
{
    using var http = new HttpClient(new HttpSinSesion())
    {
        BaseAddress = new Uri("http://localhost/")
    };
    var cliente = new TetrisApi(http);
    Verificar(!cliente.ObtenerSalaAsync(Guid.NewGuid()).GetAwaiter().GetResult().Success);
});
Prueba("El cliente C# conserva el contrato de login y envía Bearer", () =>
{
    var transporte = new HttpConToken();
    using var http = new HttpClient(transporte)
    {
        BaseAddress = new Uri("http://localhost/")
    };
    var cliente = new TetrisApi(http);
    var respuesta = cliente.LoginAsync(new TLoginUsuario
    {
        NombreUsuario = "Prueba",
        Contrasena = "SoloPruebas123!"
    }).GetAwaiter().GetResult();
    Verificar(respuesta.Success && respuesta.Data!.UsuarioId == 1);
    cliente.ObtenerSalaAsync(Guid.NewGuid()).GetAwaiter().GetResult();
    Verificar(transporte.Autorizado);
});
PruebasDefinitivas.Agregar(Prueba);
PruebasReglasNuevas.Agregar(Prueba);
PruebasConfiguracionPartida.Agregar(Prueba);
PruebasEfectosEspeciales.Agregar(Prueba);
var fallos = 0;
foreach (var prueba in pruebas)
{
    try
    {
        prueba.Ejecutar();
        Console.WriteLine($"OK: {prueba.Nombre}");
    }
    catch (Exception ex)
    {
        fallos++;
        Console.WriteLine($"ERROR: {prueba.Nombre}: {ex.Message}");
    }
}

Console.WriteLine($"{pruebas.Count - fallos}/{pruebas.Count} pruebas correctas.");
{
    if (fallos == 0)
    {
        return 0;
    }

    return 1;
}

sealed class MotorControlado : IMotorJuego
{
    public TableroJuego CrearTablero()
    {
        return new();
    }

    public TableroJuego CrearTablero(ConfiguracionPiezas piezas)
    {
        return new();
    }

    public PasoJuego Mover(TableroJuego t, Movimiento movimiento)
    {
        if (movimiento == Movimiento.Caer)
        {
            t.Perdido = true;
        }
        else
        {
            t.Puntuacion += 10;
        }

        return new(true, true);
    }

    public PasoJuego Avanzar(TableroJuego t)
    {
        return new(false, false);
    }

    public TTablero ObtenerEstado(TableroJuego t, int? id)
    {
        return new(id, t.Celdas.Select(f => f.ToArray()).ToArray(), 0, [], t.Puntuacion, t.Lineas, t.Nivel, t.Perdido);
    }
}

sealed class RepositorioMemoria<T> : IRepositorioAD<T> where T : class
{
    public List<T> Datos = [];
    public bool Fallar;
    public Respuesta<T> Insertar(T entidad)
    {
        Datos.Add(entidad);
        return Respuesta<T>.Ok(entidad);
    }

    public Respuesta<T> Modificar(T entidad)
    {
        return Respuesta<T>.Ok(entidad);
    }

    public Respuesta<bool> Eliminar(T entidad)
    {
        return Respuesta<bool>.Ok(Datos.Remove(entidad));
    }

    public Respuesta<IEnumerable<T>> Listar()
    {
        if (Fallar)
        {
            return Respuesta<IEnumerable<T>>.Fail("Error simulado");
        }

        return Respuesta<IEnumerable<T>>.Ok(Datos.ToArray());
    }

    public Respuesta<IEnumerable<T>> Buscar(Expression<Func<T, bool>> predicado)
    {
        return Respuesta<IEnumerable<T>>.Ok(Datos.Where(predicado.Compile()).ToArray());
    }

    public Respuesta<T> ObtenerEntidad(Expression<Func<T, bool>> predicado)
    {
        return new()
        {
            Data = Datos.FirstOrDefault(predicado.Compile())
        };
    }
}

sealed class UnidadMemoria : IUnidadTrabajoEF
{
    public RepositorioMemoria<Usuario> Usuarios = new();
    public RepositorioMemoria<Partida> Partidas = new();
    public RepositorioMemoria<Resultado> Resultados = new();
    public RepositorioMemoria<EstadisticasJugador> Estadisticas = new();
    public RepositorioMemoria<ParticipantesPartidum> Participantes = new();
    public RepositorioMemoria<TiposPieza> TiposPiezas = new();
    public IRepositorioAD<TiposPieza> TTipoPieza
    {
        get
        {
            return TiposPiezas;
        }
    }

    public IRepositorioAD<Usuario> TUsuario
    {
        get
        {
            return Usuarios;
        }
    }

    public IRepositorioAD<Partida> TPartida
    {
        get
        {
            return Partidas;
        }
    }

    public IRepositorioAD<Resultado> TResultado
    {
        get
        {
            return Resultados;
        }
    }

    public IRepositorioAD<EstadisticasJugador> TEstadistica
    {
        get
        {
            return Estadisticas;
        }
    }

    public IRepositorioAD<ParticipantesPartidum> TParticipante
    {
        get
        {
            return Participantes;
        }
    }

    public IRepositorioAD<TurnosPartidum> TTurno { get; } = new RepositorioMemoria<TurnosPartidum>();
    public IRepositorioAD<ConfiguracionJugador> TConfiguracion { get; } = new RepositorioMemoria<ConfiguracionJugador>();

    public int Completar()
    {
        for (var i = 0; i < Usuarios.Datos.Count; i++)
        {
            Usuarios.Datos[i].UsuarioId = i + 1;
        }

        for (var i = 0; i < Partidas.Datos.Count; i++)
        {
            Partidas.Datos[i].PartidaId = i + 1;
        }

        foreach (var p in Participantes.Datos)
        {
            p.PartidaId = p.Partida.PartidaId;
        }

        foreach (var r in Resultados.Datos)
        {
            r.PartidaId = r.Partida.PartidaId;
        }

        return 1;
    }

    public Task<int> CompletarAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Completar());
    }

    public void EmpezarTransaccion()
    {
    }

    public void CompletarTran()
    {
        Completar();
    }

    public void Rollback()
    {
    }

    public void Dispose()
    {
    }
}

sealed class HttpSinSesion : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
    }
}


sealed class HttpConToken : HttpMessageHandler
{
    public bool Autorizado { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var respuesta = new HttpResponseMessage(System.Net.HttpStatusCode.OK);

        if (request.RequestUri!.AbsolutePath.EndsWith("login"))
        {
            respuesta.Content = System.Net.Http.Json.JsonContent.Create(
                Respuesta<TAutenticacion>.Ok(new TAutenticacion
                {
                    UsuarioId = 1,
                    NombreUsuario = "Prueba",
                    Token = "token-de-transporte-simulado"
                }));
        }
        else
        {
            Autorizado = request.Headers.Authorization?.Scheme == "Bearer" &&
                request.Headers.Authorization.Parameter == "token-de-transporte-simulado";
            respuesta.Content = System.Net.Http.Json.JsonContent.Create(new Respuesta<TSala>());
        }

        return Task.FromResult(respuesta);
    }
}
