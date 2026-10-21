using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.Juego;
using LogicaJuego.Motor;
using LogicaJuego.Servicios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Utilitarios;

// Pruebas de las reglas nuevas: piezas especiales, pausas por votación y salas privadas.
// No usan HTTP ni SQL: el tiempo se simula pasando la hora a SalaLJ.Avanzar.
internal static class PruebasReglasNuevas
{
    private static readonly TipoPiezaEspecial TipoColumna = new TipoPiezaEspecial(1, "COL", "Elimina columna", true);
    private static readonly TipoPiezaEspecial TipoOtro =
        new TipoPiezaEspecial(2, "OTR", "Sin efecto de columna", false);

    public static void Agregar(Action<string, Action> prueba)
    {
        AgregarPiezasEspeciales(prueba);
        AgregarValidacionDePiezas(prueba);
        AgregarVotacionDePausa(prueba);
        AgregarDuracionYCongelamiento(prueba);
        AgregarPausaIndividual(prueba);
        AgregarSalasPrivadas(prueba);
    }

    private static void Verificar(bool condicion, string mensaje = "La condición esperada no se cumplió.")
    {
        if (!condicion)
        {
            throw new Exception(mensaje);
        }
    }

    // ---------- Piezas especiales ----------

    private static int ContarEspeciales(GeneradorPiezas generador, ConfiguracionPiezas configuracion, int cantidad)
    {
        var especiales = 0;

        for (var i = 0; i < cantidad; i++)
        {
            if (generador.SortearEspecial(configuracion) != null)
            {
                especiales++;
            }
        }

        return especiales;
    }

    private static ConfiguracionPiezas Configuracion(bool activas, int probabilidad)
    {
        return new ConfiguracionPiezas(activas, probabilidad, [TipoColumna, TipoOtro]);
    }

    private static void AgregarPiezasEspeciales(Action<string, Action> prueba)
    {
        prueba("Piezas especiales apagadas: solo salen piezas normales", () =>
        {
            var generador = new GeneradorPiezas(new Random(1));
            Verificar(ContarEspeciales(generador, Configuracion(false, 100), 2000) == 0);
        });
        prueba("Probabilidad 100 %: todas las piezas son especiales", () =>
        {
            var generador = new GeneradorPiezas(new Random(2));
            Verificar(ContarEspeciales(generador, Configuracion(true, 100), 2000) == 2000);
        });
        prueba("Probabilidad 1 %: aparecen muy pocas piezas especiales", () =>
        {
            var generador = new GeneradorPiezas(new Random(3));
            var especiales = ContarEspeciales(generador, Configuracion(true, 1), 20000);
            Verificar(especiales > 100 && especiales < 300, $"Se esperaban cerca de 200 y salieron {especiales}.");
        });
        prueba("Probabilidad 50 %: es un sorteo por pieza, no un reparto exacto", () =>
        {
            var generador = new GeneradorPiezas(new Random(4));
            var especiales = ContarEspeciales(generador, Configuracion(true, 50), 10000);
            Verificar(especiales > 4500 && especiales < 5500, $"Se esperaban cerca de 5000 y salieron {especiales}.");
        });
        prueba("Los tipos especiales habilitados salen con la misma probabilidad", () =>
        {
            var generador = new GeneradorPiezas(new Random(5));
            var configuracion = Configuracion(true, 100);
            var columna = 0;

            for (var i = 0; i < 20000; i++)
            {
                if (generador.SortearEspecial(configuracion)!.Codigo == "COL")
                {
                    columna++;
                }
            }

            Verificar(columna > 9000 && columna < 11000, $"COL salió {columna} veces de 20000.");
        });
        prueba("Sin tipos disponibles no se genera ninguna pieza especial", () =>
        {
            var generador = new GeneradorPiezas(new Random(6));
            var sinTipos = new ConfiguracionPiezas(true, 100, []);
            Verificar(ContarEspeciales(generador, sinTipos, 100) == 0);
        });
        prueba("El motor marca la pieza especial y una consulta no vuelve a sortearla", () =>
        {
            var motor = new MotorJuego(new GeneradorPiezas(new Random(7)));
            var tablero = motor.CrearTablero(Configuracion(true, 100));
            var primera = motor.ObtenerEstado(tablero, 1);
            var segunda = motor.ObtenerEstado(tablero, 1);
            Verificar(primera.PiezaEspecial != null);
            Verificar(primera.PiezaEspecial == segunda.PiezaEspecial && primera.TipoPieza == segunda.TipoPieza);
        });
        prueba("Con las piezas especiales apagadas el tablero no marca ninguna", () =>
        {
            var motor = new MotorJuego(new GeneradorPiezas(new Random(8)));
            var tablero = motor.CrearTablero(Configuracion(false, 100));
            Verificar(motor.ObtenerEstado(tablero, 1).PiezaEspecial == null);
        });
        prueba("EliminaColumna vacía las columnas que ocupa la pieza al fijarse", () =>
        {
            var tablero = TableroConPiezaEnElFondo(TipoColumna);
            var motor = new MotorJuego(new GeneradorPiezas(new Random(9)));
            motor.Avanzar(tablero);

            for (var columna = 3; columna <= 6; columna++)
            {
                Verificar(tablero.Celdas.All(fila => fila[columna] == 0), $"La columna {columna} no quedó vacía.");
            }

            Verificar(tablero.Celdas[19][0] == 1, "No debe tocar las columnas que la pieza no ocupa.");
        });
        prueba("Un tipo especial sin EliminaColumna no vacía columnas", () =>
        {
            var tablero = TableroConPiezaEnElFondo(TipoOtro);
            var motor = new MotorJuego(new GeneradorPiezas(new Random(10)));
            motor.Avanzar(tablero);
            Verificar(tablero.Celdas[10][3] == 1 && tablero.Celdas[19][3] != 0);
        });
        prueba("Una sala con piezas especiales al 100 % publica el código de la pieza", () =>
        {
            var catalogo = new CatalogoFalso(TipoColumna);
            var motor = new MotorJuego(new GeneradorPiezas(new Random(11)));
            var datos = DatosConPiezas(ModoJuego.Compartido, true, 100);
            var (salas, id) = CrearPartida(motor, ModoJuego.Compartido, 4, datos, catalogo);
            var sala = salas.Obtener(id, 1).Data!;
            Verificar(sala.Tableros.All(tablero => tablero.PiezaEspecial == "COL"));
            Verificar(sala.PiezasEspeciales.Activas && sala.PiezasEspeciales.Probabilidad == 100);
        });
        prueba("El catálogo solo entrega tipos especiales activos de la base de datos", () =>
        {
            var unidad = new UnidadMemoria();
            unidad.TiposPiezas.Datos.AddRange(
            [
                new TiposPieza { TipoPiezaId = 1, Codigo = "I", Nombre = "Normal", Activa = true },
                new TiposPieza
                {
                    TipoPiezaId = 2,
                    Codigo = "COL",
                    Nombre = "Columna",
                    EsEspecial = true,
                    Activa = true,
                    EliminaColumna = true
                },
                new TiposPieza { TipoPiezaId = 3, Codigo = "OFF", Nombre = "Apagada", EsEspecial = true }
            ]);
            var servicios = new ServiceCollection();
            servicios.AddSingleton<Dominio.InterfazAD.IUnidadTrabajoEF>(unidad);
            using var proveedor = servicios.BuildServiceProvider();
            var catalogo = new CatalogoPiezasEspeciales(
                proveedor.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<CatalogoPiezasEspeciales>.Instance);
            var tipos = catalogo.ObtenerActivas().Data!;
            Verificar(tipos.Length == 1 && tipos[0].Codigo == "COL" && tipos[0].EliminaColumna);
        });
    }

    // Una pieza I horizontal a punto de fijarse en el fondo, con bloques acumulados en las columnas 3 a 6.
    private static TableroJuego TableroConPiezaEnElFondo(TipoPiezaEspecial tipo)
    {
        var motor = new MotorJuego(new GeneradorPiezas(new Random(99)));
        var tablero = motor.CrearTablero();
        tablero.TipoPieza = 0;
        tablero.Rotacion = 0;
        tablero.Columna = 3;
        tablero.Fila = 18;
        tablero.PiezaEspecial = tipo;
        tablero.Celdas[19][0] = 1;

        for (var columna = 3; columna <= 6; columna++)
        {
            tablero.Celdas[10][columna] = 1;
        }

        return tablero;
    }

    // ---------- Validación de la configuración ----------

    private static void AgregarValidacionDePiezas(Action<string, Action> prueba)
    {
        prueba("Piezas especiales activas sin tipos disponibles se rechazan con un mensaje claro", () =>
        {
            var salas = new SalaLJ(new MotorControlado(), new OpcionesPausa(), new CatalogoFalso());
            var respuesta = salas.Crear(1, "Ana", DatosConPiezas(ModoJuego.Compartido, true, 50));
            Verificar(!respuesta.Success && respuesta.Error!.Contains("No hay tipos de pieza especial"));
        });
        prueba("La probabilidad debe estar entre 1 y 100 cuando el interruptor está activado", () =>
        {
            var salas = new SalaLJ(new MotorControlado(), new OpcionesPausa(), new CatalogoFalso(TipoColumna));

            foreach (var invalida in new[] { 0, -5, 101, 1000 })
            {
                var respuesta = salas.Crear(1, "Ana", DatosConPiezas(ModoJuego.Compartido, true, invalida));
                Verificar(!respuesta.Success, $"La probabilidad {invalida} debía rechazarse.");
            }

            foreach (var valida in new[] { 1, 25, 50, 75, 100 })
            {
                var respuesta = salas.Crear(1, "Ana", DatosConPiezas(ModoJuego.Compartido, true, valida));
                Verificar(respuesta.Success, $"La probabilidad {valida} debía aceptarse.");
            }
        });
        prueba("Con el interruptor apagado la probabilidad se ignora", () =>
        {
            var salas = new SalaLJ(new MotorControlado(), new OpcionesPausa(), new CatalogoFalso());
            var respuesta = salas.Crear(1, "Ana", DatosConPiezas(ModoJuego.Compartido, false, 500));
            Verificar(respuesta.Success && !respuesta.Data!.PiezasEspeciales.Activas);
            Verificar(respuesta.Data!.PiezasEspeciales.Probabilidad == 0);
        });
        prueba("Si el catálogo falla, no se activan piezas especiales", () =>
        {
            var salas = new SalaLJ(new MotorControlado(), new OpcionesPausa(), new CatalogoFalso { Fallar = true });
            Verificar(!salas.Crear(1, "Ana", DatosConPiezas(ModoJuego.Compartido, true, 50)).Success);
        });
        prueba("Solo el creador configura las piezas y solo antes de iniciar", () =>
        {
            var catalogo = new CatalogoFalso(TipoColumna);
            var salas = new SalaLJ(new MotorControlado(), new OpcionesPausa(), catalogo);
            var id = salas.Crear(1, "Ana", new TCrearSala { Modo = ModoJuego.Compartido }).Data!.Id;
            salas.Unirse(id, 2, "Beto");
            var activar = new TConfiguracionPiezas { Activas = true, Probabilidad = 40 };
            Verificar(!salas.ConfigurarPiezas(id, 2, activar).Success, "Un invitado no configura.");
            Verificar(!salas.ConfigurarPiezas(id, 99, activar).Success, "Un extraño no configura.");
            Verificar(salas.ConfigurarPiezas(id, 1, activar).Data!.PiezasEspeciales.Probabilidad == 40);

            for (var jugador = 1; jugador <= 4; jugador++)
            {
                salas.Unirse(id, jugador, $"J{jugador}");
                salas.Conectar(id, jugador, $"c{jugador}");
                salas.Preparar(id, jugador, true);
            }

            Verificar(salas.Iniciar(id, 1).Success);
            Verificar(!salas.ConfigurarPiezas(id, 1, activar).Success, "No se cambia con la partida en curso.");
        });
    }

    private static TCrearSala DatosConPiezas(ModoJuego modo, bool activas, int probabilidad)
    {
        return new TCrearSala
        {
            Modo = modo,
            PiezasEspeciales = new TConfiguracionPiezas { Activas = activas, Probabilidad = probabilidad }
        };
    }

    // ---------- Creación de partidas de prueba ----------

    private static (SalaLJ Salas, Guid Id) CrearPartida(
        IMotorJuego motor,
        ModoJuego modo,
        int jugadores = 4,
        TCrearSala? datos = null,
        ICatalogoPiezasEspeciales? catalogo = null,
        OpcionesPausa? opciones = null)
    {
        var salas = new SalaLJ(motor, opciones ?? new OpcionesPausa(), catalogo);
        var crear = datos ?? new TCrearSala();
        crear.Modo = modo;
        var id = salas.Crear(1, "Jugador 1", crear).Data!.Id;

        for (var i = 1; i <= jugadores; i++)
        {
            if (i > 1)
            {
                salas.Unirse(id, i, $"Jugador {i}");
            }

            salas.Conectar(id, i, $"conexion-{i}");
            salas.Preparar(id, i, true);
        }

        Verificar(salas.Iniciar(id, 1).Success, "La partida de prueba debía iniciar.");
        return (salas, id);
    }

    private static TPausa EstadoPausa(SalaLJ salas, Guid id)
    {
        return salas.Obtener(id, 1).Data!.Pausa;
    }

    // Jugador 1 solicita y los jugadores 2 y 3 aceptan: 3 de 4 es mayoría estricta.
    private static void AprobarPausa(SalaLJ salas, Guid id)
    {
        Verificar(salas.SolicitarPausa(id, 1).Success);
        Verificar(salas.VotarPausa(id, 2, true).Success);
        Verificar(salas.VotarPausa(id, 3, true).Success);
    }

    // ---------- Votación de pausas ----------

    private static void AgregarVotacionDePausa(Action<string, Action> prueba)
    {
        prueba("La mayoría estricta pide 2, 2, 3, 3 y 4 votos para 2, 3, 4, 5 y 6 jugadores", () =>
        {
            Verificar(PausaPartida.VotosNecesarios(2) == 2);
            Verificar(PausaPartida.VotosNecesarios(3) == 2);
            Verificar(PausaPartida.VotosNecesarios(4) == 3);
            Verificar(PausaPartida.VotosNecesarios(5) == 3);
            Verificar(PausaPartida.VotosNecesarios(6) == 4);
        });
        prueba("El solicitante cuenta como voto y la pausa se aprueba con 3 de 4", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.SolicitarPausa(id, 1);
            var votacion = EstadoPausa(salas, id);
            Verificar(votacion.Fase == FasePausa.Votando && votacion.VotosAFavor.SequenceEqual([1]));
            Verificar(votacion.Habilitados == 4 && votacion.VotosNecesarios == 3);
            salas.VotarPausa(id, 2, true);
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Votando, "Con 2 de 4 todavía no se aprueba.");
            salas.VotarPausa(id, 3, true);
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Pausada);
        });
        prueba("Durante la votación la partida continúa normalmente", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.SolicitarPausa(id, 1);
            var movimiento = salas.Mover(id, 2, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
            Verificar(movimiento.Success);
        });
        prueba("No se aceptan votos duplicados ni el cambio de voto", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.SolicitarPausa(id, 1);
            Verificar(!salas.VotarPausa(id, 1, true).Success, "El solicitante ya votó a favor.");
            Verificar(salas.VotarPausa(id, 2, false).Success);
            Verificar(!salas.VotarPausa(id, 2, false).Success, "Votar dos veces no está permitido.");
            Verificar(!salas.VotarPausa(id, 2, true).Success, "Cambiar el voto no está permitido.");
            Verificar(EstadoPausa(salas, id).VotosEnContra.Length == 1);
        });
        prueba("Una persona ajena a la sala no puede solicitar ni votar", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            Verificar(!salas.SolicitarPausa(id, 99).Success);
            salas.SolicitarPausa(id, 1);
            Verificar(!salas.VotarPausa(id, 99, true).Success);
        });
        prueba("Los espectadores no solicitan ni votan y no cuentan para la mayoría", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.Mover(id, 4, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
            Verificar(!salas.SolicitarPausa(id, 4).Success, "Un jugador eliminado no puede pausar.");
            salas.SolicitarPausa(id, 1);
            var votacion = EstadoPausa(salas, id);
            Verificar(votacion.Habilitados == 3 && votacion.VotosNecesarios == 2);
            Verificar(!salas.VotarPausa(id, 4, true).Success, "Un espectador no vota.");
            salas.VotarPausa(id, 2, true);
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Pausada, "Con 2 de 3 se aprueba.");
        });
        prueba("Si es imposible alcanzar la mayoría, la pausa se rechaza de inmediato", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.SolicitarPausa(id, 1);
            salas.VotarPausa(id, 2, false);
            salas.VotarPausa(id, 3, false);
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Ninguna);
            Verificar(salas.TomarEventos().Any(evento => evento.Nombre == "PausaRechazada"));
        });
        prueba("Pasados 15 segundos los votos no emitidos no cuentan a favor", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.SolicitarPausa(id, 1);
            salas.VotarPausa(id, 2, true);
            var vencimiento = EstadoPausa(salas, id).VencimientoVotacionUtc!.Value;
            salas.Avanzar(vencimiento.AddSeconds(-1));
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Votando, "A los 14 segundos sigue abierta.");
            salas.Avanzar(vencimiento.AddMilliseconds(1));
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Ninguna, "A los 15 segundos se rechaza.");
            Verificar(!salas.VotarPausa(id, 3, true).Success, "Ya no se puede votar.");
        });
        prueba("La votación dura 15 segundos de forma provisional y es configurable", () =>
        {
            var opciones = new OpcionesPausa { SegundosVotacion = 5 };
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo, 4, null, null, opciones);
            salas.SolicitarPausa(id, 1);
            var votacion = EstadoPausa(salas, id);
            var duracion = votacion.VencimientoVotacionUtc!.Value - votacion.AhoraServidorUtc;
            Verificar(duracion.TotalSeconds > 4 && duracion.TotalSeconds <= 5);
            Verificar(new OpcionesPausa().SegundosVotacion == 15 && new OpcionesPausa().SegundosPausa == 180);
        });
        prueba("Cada jugador solicita una sola pausa por partida, aunque se la rechacen", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.SolicitarPausa(id, 1);
            salas.VotarPausa(id, 2, false);
            salas.VotarPausa(id, 3, false);
            Verificar(EstadoPausa(salas, id).SolicitudesUsadasPor.SequenceEqual([1]));
            var repetida = salas.SolicitarPausa(id, 1);
            Verificar(!repetida.Success && repetida.Error!.Contains("Ya utilizó"));
            Verificar(salas.SolicitarPausa(id, 2).Success, "Otro jugador sí puede solicitar la suya.");
        });
        prueba("No puede haber dos votaciones simultáneas en la misma partida", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.SolicitarPausa(id, 1);
            var segunda = salas.SolicitarPausa(id, 2);
            Verificar(!segunda.Success && segunda.Error!.Contains("en curso"));
            Verificar(EstadoPausa(salas, id).SolicitanteId == 1);
        });
        prueba("Un jugador desconectado durante la votación no cuenta como voto a favor", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.Desconectar("conexion-4");
            salas.SolicitarPausa(id, 1);
            salas.VotarPausa(id, 2, true);
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Votando);
            salas.VotarPausa(id, 3, true);
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Pausada, "Alcanzan 3 de 4 sin el desconectado.");
        });
        prueba("Los eventos de SignalR de la pausa se generan en orden", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.SolicitarPausa(id, 1);
            salas.VotarPausa(id, 2, true);
            salas.VotarPausa(id, 3, true);
            var nombres = salas.TomarEventos().Select(evento => evento.Nombre).ToArray();
            string[] esperados =
            [
                "PausaSolicitada", "VotoPausaRegistrado", "VotoPausaRegistrado", "PausaIniciada"
            ];
            Verificar(nombres.SequenceEqual(esperados), string.Join(",", nombres));
            Verificar(salas.TomarEventos().Length == 0, "Los eventos se entregan una sola vez.");
        });
        prueba("Si la partida termina durante una votación, la votación se cierra", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            salas.SolicitarPausa(id, 1);

            for (var jugador = 1; jugador <= 4; jugador++)
            {
                salas.Mover(id, jugador, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
            }

            var sala = salas.Obtener(id, 1).Data!;
            Verificar(sala.Estado == EstadoSala.Finalizada && sala.Pausa.Fase == FasePausa.Ninguna);
        });
    }

    // ---------- Duración de la pausa y temporizadores ----------

    private static void AgregarDuracionYCongelamiento(Action<string, Action> prueba)
    {
        prueba("La pausa aprobada dura tres minutos y la partida se reanuda sola", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            AprobarPausa(salas, id);
            var pausa = EstadoPausa(salas, id);
            var duracion = pausa.FinPausaUtc!.Value - pausa.InicioPausaUtc!.Value;
            Verificar(duracion == TimeSpan.FromSeconds(180), "La pausa debe durar 180 segundos.");
            salas.Avanzar(pausa.FinPausaUtc.Value.AddSeconds(-1));
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Pausada, "A los 179 segundos sigue pausada.");
            salas.Avanzar(pausa.FinPausaUtc.Value.AddMilliseconds(1));
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Ninguna, "A los 180 segundos se reanuda.");
            Verificar(salas.TomarEventos().Any(evento => evento.Nombre == "PausaFinalizada"));
        });
        prueba("En pausa no hay movimientos y los jugadores no avanzan hasta reanudar", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            AprobarPausa(salas, id);
            var movimiento = salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
            Verificar(!movimiento.Success && movimiento.Error!.Contains("pausa"));
            var fin = EstadoPausa(salas, id).FinPausaUtc!.Value;
            salas.Avanzar(fin.AddMilliseconds(1));
            Verificar(salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 }).Success);
        });
        prueba("La pausa detiene la caída de las piezas y no la acumula al reanudar", () =>
        {
            var motor = new MotorContador();
            var (salas, id) = CrearPartida(motor, ModoJuego.Competitivo);
            AprobarPausa(salas, id);
            var pausa = EstadoPausa(salas, id);
            salas.Avanzar(pausa.InicioPausaUtc!.Value.AddSeconds(90));
            Verificar(motor.Avances == 0, "Con la pausa activa no cae ninguna pieza.");
            var fin = pausa.FinPausaUtc!.Value;
            salas.Avanzar(fin.AddMilliseconds(50));
            var inicio = salas.Obtener(id, 1).Data!.FechaInicio!.Value;
            var llegoRapido = pausa.InicioPausaUtc.Value - inicio < TimeSpan.FromMilliseconds(600);
            Verificar(!llegoRapido || motor.Avances == 0, "Al reanudar no deben caer piezas por el tiempo pausado.");
            salas.Avanzar(fin.AddSeconds(2));
            Verificar(motor.Avances > 0, "Después de reanudar la gravedad continúa.");
        });
        prueba("El turno compartido no avanza durante la pausa", () =>
        {
            var motor = new MotorContador();
            var (salas, id) = CrearPartida(motor, ModoJuego.Compartido);
            AprobarPausa(salas, id);
            var antes = salas.Obtener(id, 1).Data!.UsuarioTurno;
            var pausa = EstadoPausa(salas, id);
            salas.Avanzar(pausa.InicioPausaUtc!.Value.AddSeconds(100));
            Verificar(salas.Obtener(id, 1).Data!.UsuarioTurno == antes && motor.Avances == 0);
        });
        prueba("La reconexión vence a los 30 segundos reales incluso durante la pausa", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            AprobarPausa(salas, id);
            salas.Desconectar("conexion-4");
            var fin = EstadoPausa(salas, id).FinPausaUtc!.Value;
            salas.Avanzar(fin.AddSeconds(-1));
            Verificar(JugadorPerdido(salas, id, 4), "La pausa no prolonga los 30 segundos de reconexión.");
            salas.Avanzar(fin.AddMilliseconds(50));
            salas.Avanzar(fin.AddSeconds(10));
            Verificar(JugadorPerdido(salas, id, 4), "Al reanudar sigue eliminado.");
            salas.Avanzar(fin.AddSeconds(40));
            Verificar(JugadorPerdido(salas, id, 4), "Si no vuelve, pierde después de 30 segundos reales.");
            Verificar(EstadoPausa(salas, id).Fase == FasePausa.Ninguna, "La partida siguió su curso.");
        });
    }

    private static bool JugadorPerdido(SalaLJ salas, Guid id, int usuarioId)
    {
        return salas.Obtener(id, 1).Data!.Jugadores.Single(j => j.UsuarioId == usuarioId).Perdido;
    }

    // ---------- Pausa individual ----------

    private static void AgregarPausaIndividual(Action<string, Action> prueba)
    {
        prueba("La pausa individual es directa, sin votación, y se puede repetir", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Individual, 1);
            Verificar(salas.SolicitarPausa(id, 1).Data!.Pausa.Fase == FasePausa.Pausada);
            Verificar(salas.Reanudar(id, 1).Data!.Pausa.Fase == FasePausa.Ninguna);
            Verificar(salas.SolicitarPausa(id, 1).Success, "En individual no hay límite de solicitudes.");
        });
        prueba("Pausa individual: no hay movimientos ni caída hasta reanudar", () =>
        {
            var motor = new MotorContador();
            var (salas, id) = CrearPartida(motor, ModoJuego.Individual, 1);
            salas.SolicitarPausa(id, 1);
            Verificar(!salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 }).Success);
            salas.Avanzar(DateTime.UtcNow.AddMinutes(10));
            Verificar(motor.Avances == 0, "Sin límite de tiempo pero sin caída.");
            Verificar(salas.Reanudar(id, 1).Success);
            Verificar(salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 }).Success);
        });
        prueba("Solo la partida individual se reanuda manualmente", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Competitivo);
            AprobarPausa(salas, id);
            Verificar(!salas.Reanudar(id, 1).Success);
            var (otras, idIndividual) = CrearPartida(new MotorControlado(), ModoJuego.Individual, 1);
            Verificar(!otras.Reanudar(idIndividual, 1).Success, "No hay pausa que reanudar.");
        });
        prueba("La desconexión vencida del creador individual cancela incluso en pausa", () =>
        {
            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Individual, 1);
            salas.SolicitarPausa(id, 1);
            salas.Desconectar("conexion-1");
            salas.Avanzar(DateTime.UtcNow.AddSeconds(31));
            Verificar(!salas.Obtener(id, 1).Success);
            Verificar(salas.PendientesPersistencia().Single().Estado == EstadoSala.Cancelada);
            Verificar(salas.TomarEventos().Any(evento => evento.Nombre == "SalaCerrada"));
        });
        prueba("No se puede pausar una partida que no está en curso", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var id = salas.Crear(1, "Ana", new TCrearSala { Modo = ModoJuego.Competitivo }).Data!.Id;
            Verificar(!salas.SolicitarPausa(id, 1).Success);
        });
    }

    // ---------- Salas públicas y privadas ----------

    private static Guid SalaEnEspera(SalaLJ salas, ModoJuego modo, bool privada, int maximo = 4)
    {
        var datos = new TCrearSala { Modo = modo, Privada = privada, MaximoJugadores = maximo };
        return salas.Crear(1, "Ana", datos).Data!.Id;
    }

    private static void AgregarSalasPrivadas(Action<string, Action> prueba)
    {
        prueba("Una sala privada recibe un código aleatorio de seis caracteres del servidor", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var privada = salas.Crear(1, "Ana", new TCrearSala { Modo = ModoJuego.Compartido, Privada = true }).Data!;
            const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            Verificar(privada.Privada && privada.CodigoPrivado!.Length == 6);
            Verificar(privada.CodigoPrivado!.All(letra => alfabeto.Contains(letra)));
            var publica = salas.Crear(1, "Ana", new TCrearSala { Modo = ModoJuego.Compartido }).Data!;
            Verificar(!publica.Privada && publica.CodigoPrivado == null);
        });
        prueba("Los códigos privados son únicos entre las salas activas", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var codigos = new HashSet<string>();

            for (var i = 0; i < 3000; i++)
            {
                var datos = new TCrearSala { Modo = ModoJuego.Competitivo, Privada = true };
                codigos.Add(salas.Crear(i + 1, "Jugador", datos).Data!.CodigoPrivado!);
            }

            Verificar(codigos.Count == 3000, $"Se generaron {codigos.Count} códigos distintos de 3000.");
        });
        prueba("Las salas privadas no aparecen en el listado público", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var privada = SalaEnEspera(salas, ModoJuego.Competitivo, true);
            var publica = SalaEnEspera(salas, ModoJuego.Competitivo, false);
            var listado = salas.ListarPublicas();
            Verificar(listado.Length == 1 && listado[0].Id == publica && listado.All(s => s.Id != privada));
            Verificar(listado[0].CreadorNombre == "Ana" && listado[0].MaximoJugadores == 4);
        });
        prueba("El listado público omite salas llenas, iniciadas e individuales", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var llena = SalaEnEspera(salas, ModoJuego.Competitivo, false);

            for (var jugador = 2; jugador <= 4; jugador++)
            {
                salas.Unirse(llena, jugador, $"J{jugador}");
            }

            SalaEnEspera(salas, ModoJuego.Individual, false);
            Verificar(salas.ListarPublicas().Length == 0);
        });
        prueba("Se entra a una sala privada con su código, sin distinguir mayúsculas", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var creada = salas.Crear(1, "Ana", new TCrearSala { Modo = ModoJuego.Competitivo, Privada = true }).Data!;
            var codigo = creada.CodigoPrivado!;
            var ingreso = salas.UnirsePorCodigo(" " + codigo.ToLowerInvariant() + " ", 2, "Beto");
            Verificar(ingreso.Success && ingreso.Data!.Jugadores.Length == 2);
        });
        prueba("Una sala privada rechaza el ingreso por identificador y por código público", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var creada = salas.Crear(1, "Ana", new TCrearSala { Modo = ModoJuego.Competitivo, Privada = true }).Data!;
            Verificar(!salas.Unirse(creada.Id, 2, "Beto").Success);
            Verificar(!salas.UnirsePorCodigo(creada.Codigo, 2, "Beto").Success);
            Verificar(salas.Unirse(creada.Id, 1, "Ana").Success, "El creador sigue dentro de su sala.");
        });
        prueba("Un código privado inexistente o mal formado se rechaza", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            SalaEnEspera(salas, ModoJuego.Competitivo, true);
            Verificar(!salas.UnirsePorCodigo("ZZZZZZ", 2, "Beto").Success);
            Verificar(!salas.UnirsePorCodigo("ABC", 2, "Beto").Success);
            Verificar(!salas.UnirsePorCodigo("", 2, "Beto").Success);
        });
        prueba("El servidor valida capacidad y estado antes de permitir el ingreso", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var creada = salas.Crear(1, "Ana", new TCrearSala { Modo = ModoJuego.Competitivo, Privada = true }).Data!;
            var codigo = creada.CodigoPrivado!;

            for (var jugador = 2; jugador <= 4; jugador++)
            {
                Verificar(salas.UnirsePorCodigo(codigo, jugador, $"J{jugador}").Success);
            }

            Verificar(!salas.UnirsePorCodigo(codigo, 5, "J5").Success, "La sala llena no admite más jugadores.");

            for (var jugador = 1; jugador <= 4; jugador++)
            {
                salas.Conectar(creada.Id, jugador, $"c{jugador}");
                salas.Preparar(creada.Id, jugador, true);
            }

            salas.Iniciar(creada.Id, 1);
            Verificar(!salas.UnirsePorCodigo(codigo, 6, "J6").Success, "Una sala iniciada no admite ingresos.");
        });
        prueba("Las salas públicas no superan su capacidad máxima", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var id = SalaEnEspera(salas, ModoJuego.Competitivo, false, 5);

            for (var jugador = 2; jugador <= 5; jugador++)
            {
                Verificar(salas.Unirse(id, jugador, $"J{jugador}").Success);
            }

            Verificar(!salas.Unirse(id, 6, "J6").Success);
        });
    }
}

// Catálogo de pruebas: entrega los tipos que se le indiquen o falla a propósito.
sealed class CatalogoFalso : ICatalogoPiezasEspeciales
{
    private readonly TipoPiezaEspecial[] tipos;

    public CatalogoFalso(params TipoPiezaEspecial[] tipos)
    {
        this.tipos = tipos;
    }

    public bool Fallar { get; set; }

    public Respuesta<TipoPiezaEspecial[]> ObtenerActivas()
    {
        if (Fallar)
        {
            return Respuesta<TipoPiezaEspecial[]>.Fail("Error simulado");
        }

        return Respuesta<TipoPiezaEspecial[]>.Ok(tipos);
    }
}

// Motor de pruebas que cuenta cuántas veces cayó una pieza por gravedad.
sealed class MotorContador : IMotorJuego
{
    public int Avances { get; private set; }

    public TableroJuego CrearTablero()
    {
        return new TableroJuego();
    }

    public TableroJuego CrearTablero(ConfiguracionPiezas piezas)
    {
        return new TableroJuego();
    }

    public PasoJuego Mover(TableroJuego tablero, Movimiento movimiento)
    {
        return new PasoJuego(true, false);
    }

    public PasoJuego Avanzar(TableroJuego tablero)
    {
        Avances++;
        return new PasoJuego(true, false);
    }

    public TTablero ObtenerEstado(TableroJuego tablero, int? usuarioId)
    {
        return new TTablero(
            usuarioId,
            tablero.Celdas.Select(fila => fila.ToArray()).ToArray(),
            0,
            [],
            tablero.Puntuacion,
            tablero.Lineas,
            tablero.Nivel,
            tablero.Perdido);
    }
}
