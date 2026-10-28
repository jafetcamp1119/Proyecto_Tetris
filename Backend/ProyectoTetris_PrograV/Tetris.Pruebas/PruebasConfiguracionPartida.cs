// Comprueba las opciones y validaciones de piezas, velocidades y puntajes.
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.Juego;

using LogicaJuego.Motor;
using LogicaJuego.Servicios;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

// Pruebas de la configuración de partida: pieza que elimina fila, piezas normales permitidas,
// velocidad inicial, puntos por líneas y eventos para sonido. No usan HTTP ni SQL.
internal static class PruebasConfiguracionPartida
{
    private static readonly TipoPiezaEspecial TipoFila =
        new TipoPiezaEspecial(10, "ELIMINA_FILA", "Elimina fila", false, true);
    private static readonly TipoPiezaEspecial TipoColumna =
        new TipoPiezaEspecial(1, "COL", "Elimina columna", true);

    public static void Agregar(Action<string, Action> prueba)
    {
        AgregarEliminaFila(prueba);
        AgregarPiezasNormales(prueba);
        AgregarVelocidad(prueba);
        AgregarPuntos(prueba);
        AgregarConfiguracionDeSala(prueba);
        AgregarEventosDeSonido(prueba);
    }

    private static void Verificar(bool condicion, string mensaje = "La condición esperada no se cumplió.")
    {
        if (!condicion)
        {
            throw new Exception(mensaje);
        }
    }

    private static MotorJuego NuevoMotor(int semilla)
    {
        return new MotorJuego(new GeneradorPiezas(new Random(semilla)));
    }

    // Pieza I a punto de fijarse. Horizontal: ocupa la fila 19 (columnas 3 a 6).
    // Vertical: ocupa las filas 16 a 19 de la columna 5.
    private static TableroJuego TableroListo(
        ConfiguracionPiezas piezas,
        TipoPiezaEspecial? especial,
        bool vertical)
    {
        var tablero = NuevoMotor(5).CrearTablero(piezas);
        tablero.TipoPieza = 0;
        tablero.Rotacion = vertical ? 1 : 0;
        tablero.Columna = 3;
        tablero.Fila = vertical ? 16 : 18;
        tablero.PiezaEspecial = especial;
        return tablero;
    }

    private static ConfiguracionPiezas Configuracion(params int[] puntos)
    {
        return new ConfiguracionPiezas(
            false,
            0,
            [],
            ConfiguracionPiezas.TodasLasFormas,
            VelocidadInicial.Normal,
            puntos);
    }

    // ---------- Pieza que elimina fila ----------

    private static void AgregarEliminaFila(Action<string, Action> prueba)
    {
        prueba("EliminaFila quita la fila de la celda más baja y baja las filas superiores", () =>
        {
            var tablero = TableroListo(ConfiguracionPiezas.SoloNormales, TipoFila, false);
            tablero.Celdas[10][3] = 1;
            tablero.Celdas[19][0] = 1;
            NuevoMotor(1).Avanzar(tablero);
            Verificar(tablero.Celdas.Length == 20 && tablero.Celdas.All(fila => fila.Length == 10));
            Verificar(tablero.Celdas[11][3] == 1 && tablero.Celdas[10][3] == 0, "La fila superior debía bajar.");
            Verificar(tablero.Celdas[19].All(celda => celda == 0), "La fila 19 se eliminó y quedó la de arriba.");
            Verificar(tablero.Celdas[0].All(celda => celda == 0), "Arriba entra una fila vacía.");
        });
        prueba("EliminaFila usa la celda más baja de una pieza vertical", () =>
        {
            var tablero = TableroListo(ConfiguracionPiezas.SoloNormales, TipoFila, true);
            tablero.Celdas[19][0] = 1;
            tablero.Celdas[18][9] = 1;
            NuevoMotor(1).Avanzar(tablero);
            Verificar(tablero.Celdas[19][0] == 0, "La fila 19 debía desaparecer.");
            Verificar(tablero.Celdas[19][9] == 1, "La celda de la fila 18 bajó a la 19.");
            Verificar(tablero.Celdas[19][5] != 0 && tablero.Celdas[17][5] != 0, "El resto de la pieza baja una fila.");
        });
        prueba("Una fila eliminada por el efecto no se puntúa como línea", () =>
        {
            var tablero = TableroListo(ConfiguracionPiezas.SoloNormales, TipoFila, false);

            for (var columna = 0; columna < 10; columna++)
            {
                if (columna < 3 || columna > 6)
                {
                    tablero.Celdas[19][columna] = 1;
                }
            }

            NuevoMotor(1).Avanzar(tablero);
            Verificar(tablero.Lineas == 0 && tablero.Puntuacion == 0, "No debe haber doble eliminación ni puntos.");
            Verificar(tablero.Celdas[19].All(celda => celda == 0));
        });
        prueba("EliminaColumna y EliminaFila son efectos separados", () =>
        {
            var conColumna = TableroListo(ConfiguracionPiezas.SoloNormales, TipoColumna, false);
            conColumna.Celdas[10][3] = 1;
            conColumna.Celdas[10][0] = 1;
            NuevoMotor(1).Avanzar(conColumna);
            Verificar(conColumna.Celdas[10][3] == 0 && conColumna.Celdas[10][0] == 1, "COL solo vacía columnas.");

            var conFila = TableroListo(ConfiguracionPiezas.SoloNormales, TipoFila, false);
            conFila.Celdas[10][3] = 1;
            NuevoMotor(1).Avanzar(conFila);
            Verificar(conFila.Celdas[11][3] == 1, "La fila especial no vacía columnas.");
        });
        prueba("La pieza que elimina fila llega solo en los modos permitidos", () =>
        {
            var catalogo = new CatalogoFalso(TipoFila);

            foreach (var modo in new[] { ModoJuego.Individual, ModoJuego.Competitivo, ModoJuego.Compartido })
            {
                var datos = new TCrearSala
                {
                    PiezasEspeciales = new TConfiguracionPiezas { Activas = true, Probabilidad = 100 }
                };
                var jugadores = modo == ModoJuego.Individual ? 1 : 4;
                var (salas, id) = CrearPartida(NuevoMotor(3), modo, jugadores, datos, catalogo);
                var sala = salas.Obtener(id, 1).Data!;
                Verificar(sala.Tableros.All(tablero => tablero.PiezaEspecial ==
                    (modo == ModoJuego.Competitivo ? null : "ELIMINA_FILA")), modo.ToString());
            }
        });
        prueba("El catálogo reconoce el tipo que elimina fila por su código", () =>
        {
            var unidad = new UnidadMemoria();
            unidad.TiposPiezas.Datos.AddRange(
            [
                new TiposPieza
            {
                TipoPiezaId = 1,
                Codigo = "COL",
                Nombre = "C",
                EsEspecial = true,
                Activa = true,
                EliminaColumna = true
            },
                new TiposPieza { TipoPiezaId = 2, Codigo = "ELIMINA_FILA", Nombre = "F", EsEspecial = true, Activa = true }
            ]);
            var servicios = new ServiceCollection();
            servicios.AddSingleton<Dominio.InterfazAD.IUnidadTrabajoEF>(unidad);
            using var proveedor = servicios.BuildServiceProvider();
            var catalogo = new CatalogoPiezasEspeciales(
                proveedor.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<CatalogoPiezasEspeciales>.Instance);
            var tipos = catalogo.ObtenerActivas().Data!;
            Verificar(tipos.Single(tipo => tipo.Codigo == "ELIMINA_FILA").EliminaFila);
            Verificar(!tipos.Single(tipo => tipo.Codigo == "COL").EliminaFila);
        });
    }

    // ---------- Piezas normales ----------

    private static void AgregarPiezasNormales(Action<string, Action> prueba)
    {
        prueba("Solo salen las piezas normales habilitadas", () =>
        {
            var piezas = new ConfiguracionPiezas(
                false, 0, [], [0, 2], VelocidadInicial.Normal, ConfiguracionPiezas.PuntosPredeterminados);
            var motor = NuevoMotor(21);

            for (var i = 0; i < 200; i++)
            {
                var tablero = motor.CrearTablero(piezas);
                Verificar(tablero.TipoPieza == 0 || tablero.TipoPieza == 2, $"Salió la pieza {tablero.TipoPieza}.");
            }
        });
        prueba("Con una sola pieza habilitada siempre sale esa pieza, incluso después de fijarla", () =>
        {
            var piezas = new ConfiguracionPiezas(
                false, 0, [], [3], VelocidadInicial.Normal, ConfiguracionPiezas.PuntosPredeterminados);
            var motor = NuevoMotor(22);
            var tablero = motor.CrearTablero(piezas);

            for (var i = 0; i < 6; i++)
            {
                motor.Mover(tablero, Movimiento.Caer);
                Verificar(tablero.Perdido || tablero.TipoPieza == 3);
            }
        });
        prueba("Por omisión salen las siete piezas normales", () =>
        {
            var motor = NuevoMotor(23);
            var vistas = new HashSet<int>();

            for (var i = 0; i < 300; i++)
            {
                vistas.Add(motor.CrearTablero().TipoPieza);
            }

            Verificar(vistas.Count == 7);
        });
        prueba("El servidor valida las piezas normales enviadas", () =>
        {
            var salas = new SalaLJ(NuevoMotor(24), new OpcionesPausa(), null);
            Verificar(!salas.Crear(1, "Ana", DatosNormales()).Success, "Una lista vacía debe rechazarse.");
            Verificar(!salas.Crear(1, "Ana", DatosNormales("X")).Success, "Una letra desconocida debe rechazarse.");
            Verificar(!salas.Crear(1, "Ana", DatosNormales("I", "")).Success);

            var sala = salas.Crear(1, "Ana", DatosNormales("t", "I", "T")).Data!;
            Verificar(string.Join(",", sala.PiezasNormales) == "I,T", "Se aceptan minúsculas y se quitan repetidas.");
            Verificar(salas.Crear(1, "Ana", new TCrearSala()).Data!.PiezasNormales.Length == 7);
        });
        prueba("Una sala con piezas normales limitadas solo reparte esas piezas", () =>
        {
            var (salas, id) = CrearPartida(NuevoMotor(25), ModoJuego.Competitivo, 4, DatosNormales("O"));
            Verificar(salas.Obtener(id, 1).Data!.Tableros.All(tablero => tablero.TipoPieza == 1));
        });
        prueba("Las piezas especiales siguen separadas de las normales", () =>
        {
            var datos = DatosNormales("L");
            datos.PiezasEspeciales = new TConfiguracionPiezas { Activas = true, Probabilidad = 100 };
            var (salas, id) = CrearPartida(NuevoMotor(26), ModoJuego.Compartido, 4, datos, new CatalogoFalso(TipoFila));
            var sala = salas.Obtener(id, 1).Data!;
            Verificar(sala.Tableros.All(tablero => tablero.TipoPieza == 6 && tablero.PiezaEspecial == "ELIMINA_FILA"));
        });
    }

    private static TCrearSala DatosNormales(params string[] letras)
    {
        return new TCrearSala { Modo = ModoJuego.Competitivo, PiezasNormales = letras };
    }

    // ---------- Velocidad ----------

    private static void AgregarVelocidad(Action<string, Action> prueba)
    {
        prueba("Lenta, Normal y Rápida tienen intervalos distintos y la Normal conserva los 800 ms", () =>
        {
            var lenta = IntervaloBase(VelocidadInicial.Lenta);
            var normal = IntervaloBase(VelocidadInicial.Normal);
            var rapida = IntervaloBase(VelocidadInicial.Rapida);
            Verificar(lenta > normal && normal > rapida && normal == 800);
        });
        prueba("La velocidad inicial cambia cuándo cae la pieza en la sala", () =>
        {
            Verificar(FilaTrasMilisegundos(VelocidadInicial.Lenta, 600) == 0, "Lenta aún no debía caer a los 600 ms.");
            Verificar(FilaTrasMilisegundos(VelocidadInicial.Rapida, 600) == 1, "Rápida debía caer a los 600 ms.");
            Verificar(FilaTrasMilisegundos(VelocidadInicial.Normal, 600) == 0);
            Verificar(FilaTrasMilisegundos(VelocidadInicial.Normal, 900) == 1);
        });
        prueba("Todos los jugadores de la sala usan la misma velocidad", () =>
        {
            var datos = new TCrearSala { Velocidad = VelocidadInicial.Rapida };
            var (salas, id) = CrearPartida(NuevoMotor(31), ModoJuego.Competitivo, 4, datos);
            var inicio = salas.Obtener(id, 1).Data!.FechaInicio!.Value;
            salas.Avanzar(inicio.AddMilliseconds(600));
            var sala = salas.Obtener(id, 1).Data!;
            Verificar(sala.Velocidad == VelocidadInicial.Rapida);
            Verificar(sala.Tableros.All(tablero => tablero.Pieza.Min(celda => celda.Fila) == 1));
        });
        prueba("Una velocidad inexistente se rechaza", () =>
        {
            var salas = new SalaLJ(NuevoMotor(32), new OpcionesPausa(), null);
            var datos = new TCrearSala { Velocidad = (VelocidadInicial)7 };
            Verificar(!salas.Crear(1, "Ana", datos).Success);
        });
    }

    private static int IntervaloBase(VelocidadInicial velocidad)
    {
        return new ConfiguracionPiezas(
            false, 0, [], ConfiguracionPiezas.TodasLasFormas, velocidad, ConfiguracionPiezas.PuntosPredeterminados)
            .IntervaloBaseMs;
    }

    private static int FilaTrasMilisegundos(VelocidadInicial velocidad, int milisegundos)
    {
        var datos = new TCrearSala { Velocidad = velocidad };
        var (salas, id) = CrearPartida(NuevoMotor(30), ModoJuego.Individual, 1, datos);
        var inicio = salas.Obtener(id, 1).Data!.FechaInicio!.Value;
        salas.Avanzar(inicio.AddMilliseconds(milisegundos));
        return salas.Obtener(id, 1).Data!.Tableros[0].Pieza.Min(celda => celda.Fila);
    }

    // ---------- Puntos por líneas ----------

    private static void AgregarPuntos(Action<string, Action> prueba)
    {
        prueba("Los puntos por 1, 2, 3 y 4 líneas son configurables", () =>
        {
            for (var lineas = 1; lineas <= 4; lineas++)
            {
                var puntos = new[] { 11, 22, 33, 44 };
                var tablero = TableroConLineas(Configuracion(puntos), lineas);
                NuevoMotor(1).Avanzar(tablero);
                Verificar(tablero.Lineas == lineas, $"Se esperaban {lineas} líneas y hubo {tablero.Lineas}.");
                Verificar(tablero.Puntuacion == puntos[lineas - 1], $"{lineas} líneas dieron {tablero.Puntuacion}.");
            }
        });
        prueba("Los puntos por omisión siguen siendo 100, 300, 500 y 800", () =>
        {
            var esperado = new[] { 100, 300, 500, 800 };

            for (var lineas = 1; lineas <= 4; lineas++)
            {
                var tablero = TableroConLineas(ConfiguracionPiezas.SoloNormales, lineas);
                NuevoMotor(1).Avanzar(tablero);
                Verificar(tablero.Puntuacion == esperado[lineas - 1]);
            }
        });
        prueba("El multiplicador por nivel se conserva con puntos personalizados", () =>
        {
            var tablero = TableroConLineas(Configuracion(50, 0, 0, 0), 1);
            tablero.Lineas = 10;
            NuevoMotor(1).Avanzar(tablero);
            Verificar(tablero.Puntuacion == 100, "Nivel 2 con 50 puntos base debía dar 100.");
        });
        prueba("Los puntos negativos o demasiado altos se rechazan", () =>
        {
            var salas = new SalaLJ(NuevoMotor(40), new OpcionesPausa(), null);
            var negativo = new TCrearSala { Puntos = new TPuntosLineas { DosLineas = -1 } };
            var enorme = new TCrearSala
            {
                Puntos = new TPuntosLineas { CuatroLineas = ConfiguracionPiezas.PuntosMaximosPorJugada + 1 }
            };
            var ceros = new TCrearSala { Puntos = new TPuntosLineas { UnaLinea = 0 } };
            Verificar(!salas.Crear(1, "Ana", negativo).Success);
            Verificar(!salas.Crear(1, "Ana", enorme).Success);
            Verificar(salas.Crear(1, "Ana", ceros).Success, "Cero es un valor permitido.");
        });
        prueba("Los puntos de la sala se publican y valen para todos los tableros", () =>
        {
            var datos = new TCrearSala { Puntos = new TPuntosLineas { UnaLinea = 7, CuatroLineas = 70 } };
            var (salas, id) = CrearPartida(NuevoMotor(41), ModoJuego.Competitivo, 4, datos);
            var sala = salas.Obtener(id, 1).Data!;
            Verificar(sala.Puntos.UnaLinea == 7 && sala.Puntos.DosLineas == 300 && sala.Puntos.CuatroLineas == 70);
        });
    }

    // Deja listas las líneas pedidas para que la pieza I las complete al fijarse.
    private static TableroJuego TableroConLineas(ConfiguracionPiezas piezas, int lineas)
    {
        var vertical = lineas > 1;
        var tablero = TableroListo(piezas, null, vertical);

        if (!vertical)
        {
            for (var columna = 0; columna < 10; columna++)
            {
                tablero.Celdas[19][columna] = (columna >= 3 && columna <= 6) ? 0 : 1;
            }

            return tablero;
        }

        for (var fila = 20 - lineas; fila < 20; fila++)
        {
            for (var columna = 0; columna < 10; columna++)
            {
                tablero.Celdas[fila][columna] = columna == 5 ? 0 : 1;
            }
        }

        // La pieza vertical ocupa cuatro filas; con menos líneas se acomoda más arriba para no tapar huecos.
        tablero.Fila = 16;
        return tablero;
    }

    // ---------- Configuración de la sala ----------

    private static void AgregarConfiguracionDeSala(Action<string, Action> prueba)
    {
        prueba("El creador cambia toda la configuración antes de iniciar", () =>
        {
            var salas = new SalaLJ(NuevoMotor(50), new OpcionesPausa(), new CatalogoFalso(TipoFila));
            var id = salas.Crear(1, "Ana", new TCrearSala()).Data!.Id;
            var nueva = new TConfiguracionSala
            {
                PiezasEspeciales = new TConfiguracionPiezas { Activas = true, Probabilidad = 30 },
                PiezasNormales = ["I", "J"],
                Velocidad = VelocidadInicial.Lenta,
                Puntos = new TPuntosLineas { UnaLinea = 1, DosLineas = 2, TresLineas = 3, CuatroLineas = 4 }
            };
            var sala = salas.ConfigurarSala(id, 1, nueva).Data!;
            Verificar(sala.PiezasEspeciales.Activas && sala.PiezasEspeciales.Probabilidad == 30);
            Verificar(string.Join(",", sala.PiezasNormales) == "I,J");
            Verificar(sala.Velocidad == VelocidadInicial.Lenta && sala.Puntos.CuatroLineas == 4);
        });
        prueba("Una configuración inválida no cambia la sala", () =>
        {
            var salas = new SalaLJ(NuevoMotor(51), new OpcionesPausa(), null);
            var id = salas.Crear(1, "Ana", DatosNormales("T")).Data!.Id;
            var mala = new TConfiguracionSala { PiezasNormales = [] };
            Verificar(!salas.ConfigurarSala(id, 1, mala).Success);
            Verificar(string.Join(",", salas.Obtener(id, 1).Data!.PiezasNormales) == "T");
        });
        prueba("Solo el creador configura y no se puede cambiar después de iniciar", () =>
        {
            var (salas, id) = CrearPartida(NuevoMotor(52), ModoJuego.Competitivo, 4);
            Verificar(!salas.ConfigurarSala(id, 1, new TConfiguracionSala()).Success, "Ya inició.");

            var esperando = new SalaLJ(NuevoMotor(53), new OpcionesPausa(), null);
            var otra = esperando.Crear(1, "Ana", new TCrearSala()).Data!.Id;
            esperando.Unirse(otra, 2, "Beto");
            Verificar(!esperando.ConfigurarSala(otra, 2, new TConfiguracionSala()).Success, "Beto no es creador.");
        });
        prueba("Cambiar las piezas especiales conserva las piezas normales, la velocidad y los puntos", () =>
        {
            var datos = DatosNormales("S", "Z");
            datos.Modo = ModoJuego.Compartido;
            datos.Velocidad = VelocidadInicial.Rapida;
            datos.Puntos = new TPuntosLineas { UnaLinea = 9 };
            var salas = new SalaLJ(NuevoMotor(54), new OpcionesPausa(), new CatalogoFalso(TipoFila));
            var id = salas.Crear(1, "Ana", datos).Data!.Id;
            var activar = new TConfiguracionPiezas { Activas = true, Probabilidad = 10 };
            var sala = salas.ConfigurarPiezas(id, 1, activar).Data!;
            Verificar(string.Join(",", sala.PiezasNormales) == "S,Z");
            Verificar(sala.Velocidad == VelocidadInicial.Rapida && sala.Puntos.UnaLinea == 9);
            Verificar(sala.PiezasEspeciales.Activas);
        });
    }

    // ---------- Eventos para sonido ----------

    private static void AgregarEventosDeSonido(Action<string, Action> prueba)
    {
        prueba("El estado identifica movimiento, rotación y bloqueo con una secuencia creciente", () =>
        {
            var motor = NuevoMotor(60);
            var tablero = motor.CrearTablero();
            Verificar(motor.ObtenerEstado(tablero, 1).Evento == null, "Sin acciones todavía no hay evento.");

            motor.Mover(tablero, Movimiento.Izquierda);
            var movimiento = motor.ObtenerEstado(tablero, 1).Evento!;
            Verificar(movimiento.Sonidos.SequenceEqual(["movimiento"]));

            motor.Mover(tablero, Movimiento.Rotar);
            var rotacion = motor.ObtenerEstado(tablero, 1).Evento!;
            Verificar(rotacion.Sonidos.Contains("rotacion") && rotacion.Secuencia > movimiento.Secuencia);

            motor.Mover(tablero, Movimiento.Caer);
            var bloqueo = motor.ObtenerEstado(tablero, 1).Evento!;
            Verificar(bloqueo.Sonidos.Contains("bloqueo") && bloqueo.Secuencia > rotacion.Secuencia);
        });
        prueba("Un movimiento imposible no genera evento nuevo", () =>
        {
            var motor = NuevoMotor(61);
            var tablero = motor.CrearTablero();
            tablero.Columna = 0;
            motor.Mover(tablero, Movimiento.Izquierda);
            Verificar(motor.ObtenerEstado(tablero, 1).Evento == null);
        });
        prueba("El estado identifica líneas eliminadas y su cantidad", () =>
        {
            var tablero = TableroConLineas(ConfiguracionPiezas.SoloNormales, 2);
            var motor = NuevoMotor(1);
            motor.Avanzar(tablero);
            var evento = motor.ObtenerEstado(tablero, 1).Evento!;
            Verificar(evento.Sonidos.Contains("lineas") && evento.LineasEliminadas == 2);
        });
        prueba("El estado identifica la activación de una pieza especial", () =>
        {
            var tablero = TableroListo(ConfiguracionPiezas.SoloNormales, TipoFila, false);
            var motor = NuevoMotor(1);
            motor.Avanzar(tablero);
            var evento = motor.ObtenerEstado(tablero, 1).Evento!;
            Verificar(evento.Sonidos.Contains("especial") && evento.PiezaEspecial == "ELIMINA_FILA");
        });
        prueba("El estado identifica la derrota y la sala finalizada", () =>
        {
            var motor = NuevoMotor(62);
            var tablero = motor.CrearTablero();

            for (var i = 0; i < 40 && !tablero.Perdido; i++)
            {
                motor.Mover(tablero, Movimiento.Caer);
            }

            Verificar(tablero.Perdido);
            Verificar(motor.ObtenerEstado(tablero, 1).Evento!.Sonidos.Contains("derrota"));

            var (salas, id) = CrearPartida(new MotorControlado(), ModoJuego.Individual, 1);
            salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
            Verificar(salas.Obtener(id, 1).Data!.Estado == EstadoSala.Finalizada);
        });
    }

    private static (SalaLJ Salas, Guid Id) CrearPartida(
        IMotorJuego motor,
        ModoJuego modo,
        int jugadores,
        TCrearSala? datos = null,
        ICatalogoPiezasEspeciales? catalogo = null)
    {
        var salas = new SalaLJ(motor, new OpcionesPausa(), catalogo);
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
}
