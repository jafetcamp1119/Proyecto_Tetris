// Comprueba filas, nivel y velocidad comunes sin mezclar los puntos de cada jugador.
using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.Juego;

using LogicaJuego.Motor;
using LogicaJuego.Servicios;

internal static class PruebasNivelGlobal
{
    public static void Agregar(Action<string, Action> prueba)
    {
        prueba("Compartido suma 5+5 filas, sincroniza niveles, velocidad y conserva puntos individuales", () =>
        {
            var motor = new MotorCapturado();
            var salas = new SalaLJ(motor);
            var id = salas.Crear(1, "Uno", new TCrearSala { Modo = ModoJuego.Compartido, MaximoJugadores = 2 }).Data!.Id;
            salas.Unirse(id, 2, "Dos");

            for (var jugador = 1; jugador <= 2; jugador++)
            {
                salas.Conectar(id, jugador, "c" + jugador);
                salas.Preparar(id, jugador, true);
            }

            var sala = salas.Iniciar(id, 1).Data!;
            Exigir(sala.ProgresoCompartido is { Nivel: 1, FilasNormales: 0, ProgresoSiguienteNivel: 0 });
            var secuencias = new long[3];

            for (var indice = 0; indice < 10; indice++)
            {
                PrepararLinea(motor.Tablero!);
                var activo = sala.UsuarioTurno!.Value;
                sala = salas.Mover(id, activo, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = ++secuencias[activo] }).Data!;
                Exigir(sala.ProgresoCompartido!.FilasNormales == indice + 1);
            }

            Exigir(sala.Jugadores.All(j => j.Nivel == 2 && j.Lineas == 5 && j.Puntos == 500));
            Exigir(sala.Tableros.Single().Nivel == 2 && sala.ProgresoCompartido!.IntervaloCaidaMs == 740);
            salas.Desconectar("c1");
            var retomada = salas.Conectar(id, 1, "telefono").Data!;
            Exigir(retomada.ProgresoCompartido == sala.ProgresoCompartido);
            var activoFinal = retomada.UsuarioTurno!.Value;
            PrepararLinea(motor.Tablero!);
            var once = salas.Mover(
                id,
                activoFinal,
                new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = ++secuencias[activoFinal] }
            ).Data!;
            Exigir(once.Jugadores.Single(j => j.UsuarioId == activoFinal).Puntos == 700);
            Exigir(once.Jugadores.Single(j => j.UsuarioId != activoFinal).Puntos == 500);
            Exigir(once.ProgresoCompartido!.ProgresoSiguienteNivel == 1);
            Exigir(
                !salas.Mover(
                id,
                activoFinal,
                new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = secuencias[activoFinal] }
            ).Success
            );
            Exigir(salas.Obtener(id, 1).Data!.ProgresoCompartido!.FilasNormales == 11);
        });
        prueba("Las filas artificiales de Barredora no hacen subir el nivel compartido", () =>
        {
            var motor = new MotorJuego();
            var tablero = motor.CrearTablero();
            Fijar(tablero, nameof(TableroJuego.EsCompartido), true);
            Fijar(tablero, nameof(TableroJuego.Lineas), 9);
            Fijar(
                tablero,
                nameof(TableroJuego.PiezaEspecial),
                new TipoPiezaEspecial(7, "BARREDORA", "Barredora", false)
            );

            for (var fila = 3; fila < 10; fila++)
            {
                Array.Fill(tablero.Celdas[fila], 1);
            }

            motor.Mover(tablero, Movimiento.Caer);
            Exigir(tablero.Lineas == 9 && tablero.Nivel == 1);
        });
        prueba("Cuatro filas normales cruzan el umbral compartido sin multiplicar filas ni puntos", () =>
        {
            var motor = new MotorJuego();
            var tablero = motor.CrearTablero();

            foreach (var fila in tablero.Celdas)
            {
                Array.Clear(fila);
            }

            for (var fila = 16; fila < 20; fila++)
            {
                Array.Fill(tablero.Celdas[fila], 1);
                tablero.Celdas[fila][5] = 0;
            }

            Fijar(tablero, nameof(TableroJuego.EsCompartido), true);
            Fijar(tablero, nameof(TableroJuego.Lineas), 9);
            Fijar(tablero, nameof(TableroJuego.TipoPieza), 0);
            Fijar(tablero, nameof(TableroJuego.Rotacion), 1);
            Fijar(tablero, nameof(TableroJuego.Fila), 16);
            Fijar(tablero, nameof(TableroJuego.Columna), 3);
            motor.Mover(tablero, Movimiento.Caer);
            Exigir(tablero.Lineas == 13 && tablero.Nivel == 2 && tablero.Puntuacion == 800);
            Exigir(tablero.IntervaloCaidaMs == 740);
        });
    }

    private static void PrepararLinea(TableroJuego tablero)
    {
        foreach (var fila in tablero.Celdas)
        {
            Array.Clear(fila);
        }

        Array.Fill(tablero.Celdas[19], 1);

        for (var columna = 3; columna <= 6; columna++)
        {
            tablero.Celdas[19][columna] = 0;
        }

        Fijar(tablero, nameof(TableroJuego.TipoPieza), 0);
        Fijar(tablero, nameof(TableroJuego.Rotacion), 0);
        Fijar(tablero, nameof(TableroJuego.Fila), 18);
        Fijar(tablero, nameof(TableroJuego.Columna), 3);
        Fijar(tablero, nameof(TableroJuego.PiezaEspecial), null);
    }

    private static void Fijar(TableroJuego tablero, string propiedad, object? valor)
    {
        typeof(TableroJuego).GetProperty(propiedad)!.SetValue(tablero, valor);
    }

    private static void Exigir(bool condicion)
    {
        if (!condicion)
        {
            throw new Exception("El progreso global o los puntos individuales cambiaron.");
        }
    }

    private sealed class MotorCapturado : IMotorJuego
    {
        private readonly MotorJuego motor = new();
        public TableroJuego? Tablero;

        public TableroJuego CrearTablero()
        {
            return CrearTablero(ConfiguracionPiezas.SoloNormales);
        }

        public TableroJuego CrearTablero(ConfiguracionPiezas piezas)
        {
            Tablero = motor.CrearTablero(piezas);
            return Tablero;
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
}
