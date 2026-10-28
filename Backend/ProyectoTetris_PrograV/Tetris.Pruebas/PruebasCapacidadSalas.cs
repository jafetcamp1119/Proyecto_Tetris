// Comprueba capacidades, códigos de invitación y condiciones de ingreso a salas.
using Dominio.EntidadesTipadas;
using Dominio.Juego;

using LogicaJuego.Motor;
using LogicaJuego.Servicios;

internal static class PruebasCapacidadSalas
{
    public static void Agregar(Action<string, Action> prueba)
    {
        foreach (var modo in new[] { ModoJuego.Compartido, ModoJuego.Competitivo })
        {
            foreach (var capacidad in new[] { 2, 3, 4 })
            {
                prueba($"{modo}: capacidad {capacidad}, mínimo dos, límite de ingreso y pausa", () =>
                {
                    var salas = new SalaLJ(new MotorJuego(new GeneradorPiezas(new Random(2))));
                    var creada = salas.Crear(1, "Ana", new TCrearSala { Modo = modo, MaximoJugadores = capacidad });
                    Exigir(creada.Success);
                    var id = creada.Data!.Id;
                    salas.Conectar(id, 1, "c1");
                    salas.Preparar(id, 1, true);
                    Exigir(!salas.Iniciar(id, 1).Success);

                    for (var jugador = 2; jugador <= capacidad; jugador++)
                    {
                        Exigir(salas.Unirse(id, jugador, $"Jugador {jugador}").Success);
                        salas.Conectar(id, jugador, $"c{jugador}");
                        salas.Preparar(id, jugador, true);
                    }

                    Exigir(!salas.Unirse(id, capacidad + 1, "Sin espacio").Success);
                    Exigir(salas.Iniciar(id, 1).Success);
                    var estado = salas.Obtener(id, 1).Data!;
                    Exigir(estado.Tableros.Length == (modo == ModoJuego.Compartido ? 1 : capacidad));
                    Exigir(salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 }).Success);
                    Exigir(salas.SolicitarPausa(id, 1).Success);

                    for (var jugador = 2; jugador <= capacidad / 2 + 1; jugador++)
                    {
                        Exigir(salas.VotarPausa(id, jugador, true).Success);
                    }

                    Exigir(salas.Obtener(id, 1).Data!.Pausa.Fase == FasePausa.Pausada);
                    Exigir(!salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 2 }).Success);
                });
            }

            prueba($"{modo}: rechaza capacidades fuera de dos a cuatro", () =>
            {
                foreach (var capacidad in new[] { -1, 0, 1, 5, 16 })
                {
                    var salas = new SalaLJ(new MotorJuego());
                    Exigir(!salas.Crear(1, "Ana", new TCrearSala { Modo = modo, MaximoJugadores = capacidad }).Success);
                }
            });
        }

        prueba("Caída rápida informa su sonido una vez y la gravedad no simula movimiento manual", () =>
        {
            var motor = new MotorJuego();
            var tablero = motor.CrearTablero();
            motor.Avanzar(tablero);
            Exigir(motor.ObtenerEstado(tablero, 1).Evento == null);
            motor.Mover(tablero, Movimiento.Caer);
            var primero = motor.ObtenerEstado(tablero, 1).Evento!;
            Exigir(primero.Sonidos.Contains("caida") && primero.Sonidos.Contains("bloqueo"));
            Exigir(primero.Sonidos.Count(sonido => sonido == "caida") == 1);
            Exigir(motor.ObtenerEstado(tablero, 1).Evento!.Secuencia == primero.Secuencia);
        });
    }

    private static void Exigir(bool condicion)
    {
        if (!condicion)
        {
            throw new Exception("No se cumplió la regla de capacidad o audio.");
        }
    }
}
