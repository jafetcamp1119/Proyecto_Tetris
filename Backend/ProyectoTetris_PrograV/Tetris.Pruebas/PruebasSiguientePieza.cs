// Comprueba la reserva privada y su conservación durante el avance de la partida.
using System.Text.Json;

using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.Juego;

using LogicaJuego.Motor;
using LogicaJuego.Servicios;

internal static class PruebasSiguientePieza
{
    public static void Agregar(Action<string, Action> prueba)
    {
        prueba("Motor consume exactamente la pieza reservada y conserva puntuación y nivel al consultar", () =>
        {
            var motor = new MotorJuego(new GeneradorPiezas(new Random(9)));
            var tablero = motor.CrearTablero();
            var pendiente = tablero.SiguientePieza!;
            var puntos = tablero.Puntuacion;
            var nivel = tablero.Nivel;

            for (var vez = 0; vez < 20; vez++)
            {
                motor.ObtenerEstado(tablero, 1);
                Exigir(ReferenceEquals(pendiente, tablero.SiguientePieza));
            }

            Exigir(tablero.Puntuacion == puntos && tablero.Nivel == nivel);
            motor.Mover(tablero, Movimiento.Caer);
            Exigir(tablero.TipoPieza == pendiente.TipoPieza && tablero.PiezaEspecial == pendiente.Especial);
            Exigir(!ReferenceEquals(pendiente, tablero.SiguientePieza));
        });

        foreach (var cantidad in new[] { 2, 3, 4 })
        {
            prueba($"Compartido {cantidad}: cada turno usa la pieza privada anunciada y conserva las ajenas", () =>
            {
                var (salas, id, _) = Crear(ModoJuego.Compartido, cantidad);
                var secuencias = new long[cantidad + 1];

                for (var vez = 0; vez < cantidad * 2; vez++)
                {
                    var antes = salas.Obtener(id, 1).Data!;
                    Exigir(antes.Estado == EstadoSala.EnCurso);
                    var pendientes = Enumerable.Range(1, cantidad).ToDictionary(jugador => jugador,
                        jugador => salas.ObtenerSiguientePieza(id, jugador).Data!.Pieza!);
                    var activo = antes.UsuarioTurno!.Value;
                    var movida = salas.Mover(id, activo, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = ++secuencias[activo] });
                    Exigir(movida.Success);
                    var siguiente = movida.Data!.UsuarioTurno!.Value;
                    Exigir(movida.Data.Tableros[0].TipoPieza == pendientes[siguiente].TipoPieza);
                    Exigir(movida.Data.Tableros[0].PiezaEspecial == pendientes[siguiente].PiezaEspecial);

                    foreach (var jugador in pendientes.Keys.Where(jugador => jugador != siguiente))
                    {
                        Exigir(Iguales(pendientes[jugador], salas.ObtenerSiguientePieza(id, jugador).Data!.Pieza!));
                    }
                }
            });
        }

        prueba("Consultas privadas no cambian el sorteo, devuelven copias y rechazan a ajenos", () =>
        {
            var (salas, id, _) = Crear(ModoJuego.Compartido, 3);
            var primera = salas.ObtenerSiguientePieza(id, 2).Data!;
            var esperada = primera.Pieza! with
            {
                Celdas = primera.Pieza!.Celdas.ToArray()
            };
            primera.Pieza.Celdas[0] = new TCelda(99, 99);

            for (var vez = 0; vez < 20; vez++)
            {
                Exigir(Iguales(esperada, salas.ObtenerSiguientePieza(id, 2).Data!.Pieza!));
            }

            Exigir(!salas.ObtenerSiguientePieza(id, 999).Success);
            Exigir(!salas.ObtenerSiguientePieza(Guid.NewGuid(), 1).Success);
            var publico = JsonSerializer.Serialize(salas.Obtener(id, 1).Data);
            Exigir(
                !publico.Contains("SiguientePieza") && !publico.Contains("VistaPrevia")
                && !publico.Contains("PiezaPreparada")
            );
        });

        prueba("Compartido: perder limpia la reserva del espectador y conserva las del resto", () =>
        {
            var (salas, id, motor) = Crear(ModoJuego.Compartido, 4);
            var anunciada = salas.ObtenerSiguientePieza(id, 2).Data!.Pieza!;

            for (var fila = 0; fila < 4; fila++)
            {
                Array.Fill(motor.Tableros[0].Celdas[fila], 1);
                motor.Tableros[0].Celdas[fila][9] = 0;
            }

            var estado = salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 }).Data!;
            Exigir(estado.Jugadores[0].Perdido && estado.UsuarioTurno == 2);
            Exigir(salas.ObtenerSiguientePieza(id, 1).Data!.Pieza == null);
            Exigir(
                estado.Tableros[0].TipoPieza == anunciada.TipoPieza
                && estado.Tableros[0].PiezaEspecial == anunciada.PiezaEspecial
            );
            Exigir(!salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 2 }).Success);
            Exigir(salas.ObtenerSiguientePieza(id, 1).Data!.Pieza == null);
        });

        prueba("Compartido: abandono del turno consume la pieza anunciada al sucesor", () =>
        {
            var (salas, id, _) = Crear(ModoJuego.Compartido, 4);
            salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
            var anunciada = salas.ObtenerSiguientePieza(id, 3).Data!.Pieza!;
            var estado = salas.Abandonar(id, 2).Data!;
            Exigir(estado.UsuarioTurno == 3 && estado.Tableros[0].TipoPieza == anunciada.TipoPieza);
            Exigir(!salas.ObtenerSiguientePieza(id, 2).Success);
        });

        prueba("Compartido: la gravedad entrega la reserva del siguiente turno", () =>
        {
            var (salas, id, motor) = Crear(ModoJuego.Compartido, 3);
            var anunciada = salas.ObtenerSiguientePieza(id, 2).Data!.Pieza!;
            motor.Tableros[0].Fila = 18;
            salas.Avanzar(DateTime.UtcNow.AddSeconds(1));
            var estado = salas.Obtener(id, 1).Data!;
            Exigir(estado.UsuarioTurno == 2 && estado.Tableros[0].TipoPieza == anunciada.TipoPieza);
        });

        prueba("Compartido: reconexión, pausa y secuencia duplicada no vuelven a sortear reservas", () =>
        {
            var (salas, id, _) = Crear(ModoJuego.Compartido, 3);
            var pendiente = salas.ObtenerSiguientePieza(id, 2).Data!.Pieza!;
            salas.Desconectar("conexion-2");
            Exigir(salas.Conectar(id, 2, "recuperada").Success);
            Exigir(Iguales(pendiente, salas.ObtenerSiguientePieza(id, 2).Data!.Pieza!));
            salas.SolicitarPausa(id, 1);
            salas.VotarPausa(id, 2, true);
            Exigir(!salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 }).Success);
            Exigir(Iguales(pendiente, salas.ObtenerSiguientePieza(id, 2).Data!.Pieza!));
            var fin = salas.Obtener(id, 1).Data!.Pausa.FinPausaUtc!.Value;
            salas.Avanzar(fin.AddMilliseconds(1));
            salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
            Exigir(!salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 }).Success);
            Exigir(Iguales(pendiente, salas.ObtenerSiguientePieza(id, 2).Data!.Pieza!));
        });

        prueba("Nueva ronda retira las vistas anteriores y prepara reservas con la configuración conservada", () =>
        {
            var (salas, id, _) = Crear(ModoJuego.Compartido, 2, new TCrearSala { MetaPuntos = 1 });
            salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 });
            var final = salas.Obtener(id, 1).Data!;
            Exigir(final.Estado == EstadoSala.Finalizada && salas.ObtenerSiguientePieza(id, 2).Data!.Pieza == null);
            salas.ConfirmarPersistencia(id, final.RevisionPersistencia);
            Exigir(salas.NuevaRonda(id, 1).Success);
            Exigir(
                salas.ObtenerSiguientePieza(id, 2).Data!.NumeroRonda == 2
                && salas.ObtenerSiguientePieza(id, 2).Data!.Pieza == null
            );
            salas.Preparar(id, 1, true);
            salas.Preparar(id, 2, true);
            Exigir(salas.Iniciar(id, 1).Success);
            Exigir(
                salas.ObtenerSiguientePieza(id, 2).Data!.NumeroRonda == 2
                && salas.ObtenerSiguientePieza(id, 2).Data!.Pieza != null
            );
        });

        prueba("Reservas compartidas respetan formas, siete especiales y probabilidad configuradas", () =>
        {
            var tipos = new[] {
                new TipoPiezaEspecial(1, "X", "Columnas", true),
                new TipoPiezaEspecial(2, "FILA", "Fila", false, true),
                new TipoPiezaEspecial(3, "CONFUSION", "Confusión", false),
                new TipoPiezaEspecial(4, "ACELERADOR", "Acelerador", false),
                new TipoPiezaEspecial(5, "BOMBA", "Bomba", false),
                new TipoPiezaEspecial(6, "DOBLE", "Doble", false),
                new TipoPiezaEspecial(7, "BARREDORA", "Barredora", false)
            };

            foreach (var tipo in tipos)
            {
                var (salas, id, _) = Crear(ModoJuego.Compartido, 2, new TCrearSala
                {
                    PiezasNormales = ["L"],
                    PiezasEspeciales = new TConfiguracionPiezas { Activas = true, Probabilidad = 100, TiposPermitidos = [tipo.Codigo] }
                }, new CatalogoFalso(tipos));

                foreach (var jugador in new[] { 1, 2 })
                {
                    var pieza = salas.ObtenerSiguientePieza(id, jugador).Data!.Pieza!;
                    Exigir(pieza.TipoPieza == 6 && pieza.PiezaEspecial == tipo.Codigo);
                    Exigir(
                        pieza.Celdas.Length == 4
                        && pieza.Celdas.All(celda => celda.Fila is >= 0 and < 4 && celda.Columna is >= 0 and < 4)
                    );
                }

                var estado = salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 }).Data!;
                Exigir(estado.Tableros[0].PiezaEspecial == tipo.Codigo);
            }
        });

        foreach (var modo in new[] { ModoJuego.Individual, ModoJuego.Competitivo })
        {
            prueba($"{modo}: reserva propia, actualización al fijar, pausa y reconexión estables", () =>
            {
                var (salas, id, _) = Crear(modo, modo == ModoJuego.Individual ? 1 : 2);
                var pendiente = salas.ObtenerSiguientePieza(id, 1).Data!.Pieza!;
                var estado = salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 }).Data!;
                Exigir(estado.Tableros.First(tablero => tablero.UsuarioId == 1).TipoPieza == pendiente.TipoPieza);
                var nueva = salas.ObtenerSiguientePieza(id, 1).Data!.Pieza!;
                Exigir(nueva.PiezaEspecial == null);
                salas.SolicitarPausa(id, 1);

                if (modo == ModoJuego.Competitivo)
                {
                    salas.VotarPausa(id, 2, true);
                }

                Exigir(Iguales(nueva, salas.ObtenerSiguientePieza(id, 1).Data!.Pieza!));
                salas.Desconectar("conexion-1");
                Exigir(salas.Conectar(id, 1, "reconectada").Success);
                Exigir(Iguales(nueva, salas.ObtenerSiguientePieza(id, 1).Data!.Pieza!));
            });
        }
    }

    private static (SalaLJ Salas, Guid Id, MotorCapturado Motor) Crear(ModoJuego modo, int cantidad,
        TCrearSala? datos = null, CatalogoFalso? catalogo = null)
    {
        var generador = new GeneradorPiezas(new Random(31));
        var motor = new MotorCapturado(new MotorJuego(generador));
        var salas = new SalaLJ(motor, new OpcionesPausa(), catalogo, generador: generador);
        datos ??= new TCrearSala();
        datos.Modo = modo;
        datos.MaximoJugadores = cantidad;
        var id = salas.Crear(1, "Jugador 1", datos).Data!.Id;

        for (var jugador = 1; jugador <= cantidad; jugador++)
        {
            if (jugador > 1)
            {
                Exigir(salas.Unirse(id, jugador, $"Jugador {jugador}").Success);
            }

            salas.Conectar(id, jugador, $"conexion-{jugador}");
            salas.Preparar(id, jugador, true);
        }

        Exigir(salas.Iniciar(id, 1).Success);
        return (salas, id, motor);
    }

    private static bool Iguales(TPiezaSiguiente primera, TPiezaSiguiente segunda)
    {
        return primera.TipoPieza == segunda.TipoPieza && primera.PiezaEspecial == segunda.PiezaEspecial
            && primera.Celdas.SequenceEqual(segunda.Celdas);
    }

    private static void Exigir(bool condicion)
    {
        if (!condicion)
        {
            throw new Exception("No se conservó la vista previa o su privacidad.");
        }
    }

    private sealed class MotorCapturado(IMotorJuego motor) : IMotorJuego
    {
        public List<TableroJuego> Tableros
        {
            get;
        } = [];

        public TableroJuego CrearTablero()
        {
            return CrearTablero(ConfiguracionPiezas.SoloNormales);
        }

        public TableroJuego CrearTablero(ConfiguracionPiezas piezas)
        {
            var tablero = motor.CrearTablero(piezas);
            Tableros.Add(tablero);
            return tablero;
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
