// Comprueba efectos, objetivos, duraciones y limpieza de los especiales.
using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.Juego;

using LogicaJuego.Motor;
using LogicaJuego.Servicios;

internal static class PruebasEfectosEspeciales
{
    public static void Agregar(Action<string, Action> prueba)
    {
        AgregarTablero(prueba);
        AgregarTemporales(prueba);
        AgregarConfiguracion(prueba);
        AgregarBarredora(prueba);
    }

    private static void AgregarTablero(Action<string, Action> prueba)
    {
        prueba("Bomba recorta el área 3x3 en los cuatro bordes sin puntos", () =>
        {
            foreach (var fila in new[] { 0, 18 })
            {
                foreach (var columna in new[] { -1, 7 })
                {
                    var motor = new MotorCapturado();
                    var tablero = motor.CrearTablero();
                    tablero.TipoPieza = 1;
                    tablero.Rotacion = 0;
                    tablero.Fila = fila;
                    tablero.Columna = columna;
                    tablero.PiezaEspecial = Tipo("BOMBA");
                    var centroFila = fila + 1;
                    var centroColumna = columna + 1;
                    // Relleno disperso: ninguna fila completa ni superposición con la pieza.
                    for (var f = 0; f < 20; f++)
                    {
                        for (var c = 0; c < 10; c++)
                        {
                            if ((c < columna + 1 || c > columna + 2 || f < fila || f > fila + 1) && (f + c) % 2 == 0)
                            {
                                tablero.Celdas[f][c] = 7;
                            }
                        }
                    }

                    if (fila == 0)
                    {
                        tablero.Celdas[2][columna + 1] = 7;
                    }

                    var anteriores = tablero.Celdas.Select(f => f.ToArray()).ToArray();
                    Verificar(motor.Avanzar(tablero).PiezaFijada, "La bomba debía fijarse.");

                    for (var f = 0; f < 20; f++)
                    {
                        for (var c = 0; c < 10; c++)
                        {
                            var dentro = Math.Abs(f - centroFila) <= 1 && Math.Abs(c - centroColumna) <= 1;
                            Verificar(tablero.Celdas[f][c] == (dentro ? 0 : anteriores[f][c]), "Área incorrecta de explosión.");
                        }
                    }

                    Verificar(tablero.Puntuacion == 0 && tablero.Lineas == 0, "La explosión no puntúa.");
                    Verificar(tablero.CeldasEliminadasEspecial.All(celda => celda.Fila >= 0 && celda.Fila < 20
                        && celda.Columna >= 0 && celda.Columna < 10), "Coordenadas fuera del tablero.");
                }
            }
        });

        prueba("Doble puntuación multiplica solo las líneas personalizadas y el nivel de esa jugada", () =>
        {
            var motor = new MotorCapturado();
            var configuracion = new ConfiguracionPiezas(false, 0, [], [0], VelocidadInicial.Normal, [137, 400, 700, 999]);
            var tablero = motor.CrearTablero(configuracion);
            PrepararLinea(tablero, "DOBLE_PUNTUACION");
            tablero.Puntuacion = 50;
            tablero.Lineas = 10;
            motor.Avanzar(tablero);
            Verificar(tablero.Puntuacion == 50 + 137 * 2 * 2 && tablero.Lineas == 11);
            Verificar(tablero.PiezaEspecial == null);
            PrepararLinea(tablero, null);
            motor.Avanzar(tablero);
            Verificar(tablero.Puntuacion == 50 + 137 * 2 * 3, "El multiplicador no debe persistir.");
        });

        prueba("Doble puntuación respeta las cuatro recompensas personalizadas", () =>
        {
            for (var cantidad = 1; cantidad <= 4; cantidad++)
            {
                var motor = new MotorCapturado();
                var tablero = motor.CrearTablero(new ConfiguracionPiezas(false, 0, [],
                    [0], VelocidadInicial.Normal, [11, 22, 33, 44]));
                tablero.TipoPieza = 0;
                tablero.Rotacion = 1;
                tablero.Fila = 16;
                tablero.Columna = 3;
                tablero.PiezaEspecial = Tipo("DOBLE");
                tablero.Lineas = 10;

                for (var fila = 20 - cantidad; fila < 20; fila++)
                {
                    for (var columna = 0; columna < 10; columna++)
                    {
                        tablero.Celdas[fila][columna] = columna == 5 ? 0 : 7;
                    }
                }

                motor.Avanzar(tablero);
                Verificar(tablero.Puntuacion == cantidad * 11 * 2 * 2 && tablero.Lineas == 10 + cantidad);
            }
        });

        prueba("Doble puntuación sin líneas conserva únicamente los puntos normales de caída", () =>
        {
            var motor = new MotorCapturado();
            var tablero = motor.CrearTablero();
            tablero.PiezaEspecial = Tipo("DOBLE_PUNTUACION");
            var normal = motor.CrearTablero();
            normal.TipoPieza = tablero.TipoPieza;
            normal.PiezaEspecial = null;
            motor.Mover(tablero, Movimiento.Caer);
            motor.Mover(normal, Movimiento.Caer);
            Verificar(tablero.Lineas == 0 && tablero.Puntuacion == normal.Puntuacion);
        });

        prueba("Barredora limpia la mitad superior y comunica su activación compartida", () =>
        {
            var (salas, motor, _, id) = Partida("BARREDORA");
            motor.Tablero!.Celdas[12][0] = 7;
            motor.Tablero.Celdas[9][0] = 7;
            motor.Tablero.Celdas[0][0] = 7;
            var estado = Caer(salas, id, 1);
            Verificar(estado.UltimoEfecto!.Estado == "Aplicado" && estado.UltimoEfecto.Tipo == EfectoEspecial.Barredora);
            Verificar(motor.Tablero.Celdas[12][0] == 7 && motor.Tablero.Celdas.Sum(fila => fila.Count(c => c != 0)) == 5);
            Verificar(estado.Efectos!.Length == 0 && estado.UltimoEfecto.CeldasEliminadas.Length == 2);
            Verificar(motor.Tablero.Celdas.Take(10).All(fila => fila.All(celda => celda == 0)));
            Verificar(estado.UsuarioTurno == 2 && estado.Tableros.Length == 1);
        });

        prueba("La activación de bomba expone celdas, puntos y una copia segura del estado", () =>
        {
            var (salas, _, _, id) = Partida("BOMBA");
            var estado = Caer(salas, id, 1);
            Verificar(estado.Tableros.Length == 1 && estado.UltimoEfecto!.AfectadoId == 1);
            Verificar(estado.UltimoEfecto!.PuntosJugada == estado.Jugadores[0].Puntos);
            var celda = estado.UltimoEfecto.CeldasEliminadas[0];
            estado.UltimoEfecto.CeldasEliminadas[0] = new TCelda(-1, -1);
            Verificar(salas.Obtener(id, 1).Data!.UltimoEfecto!.CeldasEliminadas[0] == celda);
        });
    }

    private static void AgregarTemporales(Action<string, Action> prueba)
    {
        prueba("Confusión invierte ambos horizontales y conserva rotación y bajada", () =>
        {
            var (salas, motor, _, id) = Partida("CONFUSION");
            var estado = Caer(salas, id, 1);
            Verificar(estado.UsuarioTurno == 2 && estado.Efectos!.Single().UsuarioId == 2);
            var columna = motor.Tablero!.Columna;
            Mover(salas, id, 2, Movimiento.Izquierda, 1);
            Verificar(motor.Tablero.Columna == columna + 1);
            Mover(salas, id, 2, Movimiento.Derecha, 2);
            Verificar(motor.Tablero.Columna == columna);
            Mover(salas, id, 2, Movimiento.Rotar, 3);
            Verificar(motor.Tablero.Rotacion == 1);
            var fila = motor.Tablero.Fila;
            Mover(salas, id, 2, Movimiento.Bajar, 4);
            Verificar(motor.Tablero.Fila == fila + 1 && motor.Tablero.Puntuacion > 0);
        });

        prueba("Confusión vence sin tick previo y no se aplica al siguiente turno ajeno", () =>
        {
            var (salas, motor, reloj, id) = Partida("CONFUSION");
            Caer(salas, id, 1);
            reloj.Ahora = reloj.Ahora.AddSeconds(10);
            var columna = motor.Tablero!.Columna;
            var estado = Mover(salas, id, 2, Movimiento.Izquierda, 1);
            Verificar(motor.Tablero.Columna == columna - 1);
            Verificar(!estado.Efectos!.Single().Activo && estado.Efectos!.Single().MotivoFin == "TiempoAgotado");
        });

        prueba("Acelerador duplica caída y al vencer restaura velocidad configurada y nivel", () =>
        {
            var (salas, motor, reloj, id) = Partida("ACELERADOR");
            Caer(salas, id, 1);
            motor.Tablero!.Lineas = 10;
            var fila = motor.Tablero.Fila;
            reloj.Ahora = reloj.Ahora.AddMilliseconds(369);
            salas.Avanzar(reloj.Ahora.UtcDateTime);
            Verificar(motor.Tablero.Fila == fila);
            reloj.Ahora = reloj.Ahora.AddMilliseconds(1);
            salas.Avanzar(reloj.Ahora.UtcDateTime);
            Verificar(motor.Tablero.Fila == fila + 1);
            reloj.Ahora = reloj.Ahora.AddSeconds(10);
            salas.Avanzar(reloj.Ahora.UtcDateTime);
            fila = motor.Tablero.Fila;
            reloj.Ahora = reloj.Ahora.AddMilliseconds(739);
            salas.Avanzar(reloj.Ahora.UtcDateTime);
            Verificar(motor.Tablero.Fila == fila);
            reloj.Ahora = reloj.Ahora.AddMilliseconds(1);
            salas.Avanzar(reloj.Ahora.UtcDateTime);
            Verificar(motor.Tablero.Fila == fila + 1);
            Verificar(!salas.Obtener(id, 1).Data!.Efectos!.Single().Activo);
        });

        prueba("Cada temporal conserva ocho segundos durante pausa y vence después de reanudar", () =>
        {
            foreach (var codigo in new[] { "CONFUSION", "ACELERADOR" })
            {
                var (salas, motor, reloj, id) = Partida(codigo);
                Caer(salas, id, 1);
                reloj.Ahora = reloj.Ahora.AddSeconds(2);
                Verificar(salas.SolicitarPausa(id, 1).Success);
                Verificar(salas.VotarPausa(id, 2, true).Success);
                var pausada = salas.VotarPausa(id, 3, true).Data!;
                var fila = motor.Tablero!.Fila;
                Verificar(pausada.Efectos!.Single().MilisegundosRestantes == 8000);
                reloj.Ahora = reloj.Ahora.AddSeconds(179);
                salas.Avanzar(reloj.Ahora.UtcDateTime);
                var durante = salas.Obtener(id, 1).Data!;
                Verificar(durante.Pausa.Fase == FasePausa.Pausada && durante.Efectos!.Single().MilisegundosRestantes == 8000);
                Verificar(motor.Tablero.Fila == fila && durante.UsuarioTurno == 2);
                Verificar(!salas.Mover(id, 2, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 }).Success);
                reloj.Ahora = reloj.Ahora.AddSeconds(1);
                salas.Avanzar(reloj.Ahora.UtcDateTime);
                Verificar(salas.Obtener(id, 1).Data!.Efectos!.Single().MilisegundosRestantes == 8000);
                reloj.Ahora = reloj.Ahora.AddSeconds(8);
                salas.Avanzar(reloj.Ahora.UtcDateTime);
                Verificar(!salas.Obtener(id, 1).Data!.Efectos!.Single().Activo);
            }
        });

        prueba("Un tick tardío conserva solo el tiempo real posterior al fin de pausa", () =>
        {
            var (salas, _, reloj, id) = Partida("ACELERADOR");
            Caer(salas, id, 1);
            reloj.Ahora = reloj.Ahora.AddSeconds(2);
            salas.SolicitarPausa(id, 1);
            salas.VotarPausa(id, 2, true);
            salas.VotarPausa(id, 3, true);
            reloj.Ahora = reloj.Ahora.AddSeconds(185);
            salas.Avanzar(reloj.Ahora.UtcDateTime);
            var estado = salas.Obtener(id, 1).Data!;
            Verificar(estado.Pausa.Fase == FasePausa.Ninguna);
            Verificar(estado.Efectos!.Single().MilisegundosRestantes == 3000);
        });

        prueba("Confusión y acelerador coexisten y repetir el ataque renueva sin acumular velocidad", () =>
        {
            var (salas, motor, reloj, id) = Partida("CONFUSION");
            Caer(salas, id, 1);
            var finConfusion = salas.Obtener(id, 1).Data!.Efectos!.Single().FinUtc;

            for (var jugador = 2; jugador <= 4; jugador++)
            {
                motor.Tablero!.PiezaEspecial = null;
                Caer(salas, id, jugador);
            }

            reloj.Ahora = reloj.Ahora.AddSeconds(2);
            motor.Tablero!.PiezaEspecial = Tipo("ACELERADOR");
            Mover(salas, id, 1, Movimiento.Caer, 2);
            var estado = salas.Obtener(id, 1).Data!;
            Verificar(estado.Efectos!.Count(efecto => efecto.Activo && efecto.UsuarioId == 2) == 2);
            Verificar(estado.Efectos!.Single(efecto => efecto.Tipo == EfectoEspecial.Confusion).FinUtc == finConfusion);

            for (var jugador = 2; jugador <= 4; jugador++)
            {
                motor.Tablero.PiezaEspecial = null;
                Mover(salas, id, jugador, Movimiento.Caer, 2);
            }

            reloj.Ahora = reloj.Ahora.AddSeconds(2);
            motor.Tablero.PiezaEspecial = Tipo("ACELERADOR");
            estado = Mover(salas, id, 1, Movimiento.Caer, 3);
            var acelerador = estado.Efectos!.Single(efecto => efecto.Tipo == EfectoEspecial.Acelerador);
            Verificar(estado.Efectos!.Length == 2 && acelerador.FactorVelocidad == 2);
            Verificar(acelerador.MilisegundosRestantes == 10000);
        });

        prueba("Un ataque omite al eliminado antes del bloqueo y nunca elige al atacante", () =>
        {
            var (salas, _, _, id) = Partida("CONFUSION");
            Verificar(salas.Abandonar(id, 2).Success);
            var estado = Caer(salas, id, 1);
            Verificar(estado.UsuarioTurno == 3 && estado.Efectos!.Single().UsuarioId == 3);
            Verificar(estado.UltimoEfecto!.AtacanteId == 1 && estado.UltimoEfecto.AfectadoId == 3);
        });

        prueba("Eliminar al receptor cancela su efecto; finalizar cancela los restantes", () =>
        {
            var (salas, _, _, id) = Partida("ACELERADOR");
            Caer(salas, id, 1);
            var estado = salas.Abandonar(id, 2).Data!;
            Verificar(!estado.Efectos!.Single().Activo && estado.Efectos!.Single().MotivoFin == "JugadorEliminado");
            Caer(salas, id, 3);
            estado = salas.Abandonar(id, 3).Data!;
            estado = salas.Abandonar(id, 4).Data!;
            Verificar(estado.Estado == EstadoSala.Finalizada && estado.Efectos!.All(efecto => !efecto.Activo));
        });

        prueba("Repetir una secuencia no reactiva el ataque ni duplica puntos", () =>
        {
            var (salas, _, _, id) = Partida("CONFUSION");
            var estado = Caer(salas, id, 1);
            Verificar(!salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 }).Success);
            var actual = salas.Obtener(id, 1).Data!;
            Verificar(actual.UltimoEfecto!.Secuencia == 1 && actual.Jugadores[0].Puntos == estado.Jugadores[0].Puntos);
        });

        prueba("Un ataque no afecta la velocidad ni los controles de otro participante", () =>
        {
            var (salas, motor, _, id) = Partida("CONFUSION");
            Caer(salas, id, 1);
            // La segunda pieza se convierte en normal para evitar otro ataque.
            motor.Tablero!.PiezaEspecial = null;
            Caer(salas, id, 2);
            var estado = salas.Obtener(id, 1).Data!;
            Verificar(estado.Efectos!.Any(efecto => efecto.UsuarioId == 2));
            Verificar(estado.Efectos!.All(efecto => efecto.UsuarioId != 1));
            var columna = motor.Tablero.Columna;
            Mover(salas, id, 3, Movimiento.Izquierda, 1);
            Verificar(motor.Tablero.Columna == columna - 1);
        });
    }

    private static void AgregarConfiguracion(Action<string, Action> prueba)
    {
        prueba("Selección por códigos rechaza vacío y tipos desconocidos y conserva solo los elegidos", () =>
        {
            var catalogo = new CatalogoFalso(Tipo("CONFUSION"), Tipo("BOMBA"));
            var salas = new SalaLJ(new MotorJuego(), new OpcionesPausa(), catalogo);
            var datos = new TCrearSala
            {
                Modo = ModoJuego.Compartido,
                PiezasEspeciales = new TConfiguracionPiezas { Activas = true, Probabilidad = 100, TiposPermitidos = [] }
            };
            Verificar(!salas.Crear(1, "Uno", datos).Success);
            datos.PiezasEspeciales.TiposPermitidos = ["NO_EXISTE"];
            Verificar(!salas.Crear(1, "Uno", datos).Success);
            datos.PiezasEspeciales.TiposPermitidos = ["bomba"];
            var creada = salas.Crear(1, "Uno", datos);
            Verificar(creada.Success && creada.Data!.PiezasEspeciales.TiposPermitidos!.SequenceEqual(new[] { "BOMBA" }));
        });

        prueba("Sorteo del 1, 37 y 100 por ciento y distribución uniforme entre siete tipos", () =>
        {
            var tipos = new[] { "CONFUSION", "ACELERADOR", "BOMBA", "BARREDORA", "DOBLE_PUNTUACION", "FILA", "COL" }
                .Select(Tipo).ToArray();

            foreach (var porcentaje in new[] { 1, 37, 100 })
            {
                var generador = new GeneradorPiezas(new Random(932));
                var configuracion = new ConfiguracionPiezas(true, porcentaje, tipos);
                var cantidades = new int[tipos.Length];
                const int muestras = 100000;

                for (var indice = 0; indice < muestras; indice++)
                {
                    var especial = generador.SortearEspecial(configuracion);

                    if (especial != null)
                    {
                        cantidades[Array.IndexOf(tipos, especial)]++;
                    }
                }

                var esperado = muestras * porcentaje / 100.0;
                Verificar(Math.Abs(cantidades.Sum() - esperado) < 600);
                Verificar(
                    cantidades.All(cantidad => Math.Abs(cantidad - esperado / tipos.Length) < Math.Max(70, esperado * 0.02))
                );

                if (porcentaje == 100)
                {
                    Verificar(cantidades.Sum() == muestras);
                }
            }
        });

        prueba("Competitivo ignora los siete tipos y no activa poderes; individual conserva piezas normales por defecto", () =>
        {
            var (salas, motor, _, id) = Partida("BOMBA", ModoJuego.Competitivo);
            var estado = salas.Obtener(id, 1).Data!;
            Verificar(estado.Tableros.Length == 4 && estado.Tableros.All(tablero => tablero.PiezaEspecial == null));
            Verificar(!motor.Tablero!.Piezas.EspecialesActivas);
            estado = Caer(salas, id, 1);
            Verificar(estado.Efectos!.Length == 0 && estado.UltimoEfecto == null);
            var individual = new SalaLJ(new MotorJuego());
            var creada = individual.Crear(1, "Uno", new TCrearSala { Modo = ModoJuego.Individual }).Data!;
            individual.Preparar(creada.Id, 1, true);
            Verificar(individual.Iniciar(creada.Id, 1).Data!.Tableros.Single().PiezaEspecial == null);
        });

        prueba("Opciones rechazan duraciones inválidas y permiten valores configurados", () =>
        {
            var rechazo = false;

            try
            {
                new OpcionesEfectos { SegundosAcelerador = 0 }.Validar();
            }
            catch (ArgumentException)
            {
                rechazo = true;
            }

            Verificar(rechazo);
            var (salas, _, _, id) = Partida("CONFUSION", opciones: new OpcionesEfectos { SegundosConfusion = 7 });
            Verificar(Caer(salas, id, 1).Efectos!.Single().MilisegundosRestantes == 7000);
        });
    }

    private static void AgregarBarredora(Action<string, Action> prueba)
    {
        prueba("Barredora calcula la mitad con alturas pares e impares y anchos reales", () =>
        {
            foreach (var altura in new[] { 0, 1, 2, 3, 19, 20, 21 })
            {
                var celdas = Enumerable.Range(0, altura).Select(_ => Enumerable.Repeat(7, 6).ToArray()).ToArray();
                var eliminadas = new List<(int Fila, int Columna)>();
                MotorJuego.BarrerMitadSuperior(celdas, eliminadas);
                Verificar(celdas.Length == altura && celdas.All(fila => fila.Length == 6));
                Verificar(celdas.Take(altura / 2).All(fila => fila.All(celda => celda == 0)));
                Verificar(celdas.Skip(altura / 2).All(fila => fila.All(celda => celda == 7)));
                Verificar(eliminadas.Count == altura / 2 * 6);
                MotorJuego.BarrerMitadSuperior(celdas, eliminadas);
                Verificar(eliminadas.Count == altura / 2 * 6, "Barrer de nuevo una zona vacía no elimina otras celdas.");
            }
        });

        prueba("Barredora no compacta ni puntúa líneas inferiores y conserva dimensiones", () =>
        {
            var motor = new MotorJuego();
            var tablero = motor.CrearTablero();
            tablero.EsCompartido = true;
            tablero.TipoPieza = 0;
            tablero.Rotacion = 0;
            tablero.Fila = 8;
            tablero.Columna = 3;
            tablero.PiezaEspecial = Tipo("BARREDORA");
            tablero.Puntuacion = 123;
            tablero.Lineas = 8;
            tablero.Celdas[0][0] = 7;
            tablero.Celdas[9][0] = 7;
            tablero.Celdas[10][3] = 7;
            Array.Fill(tablero.Celdas[19], 7);
            var inferiores = tablero.Celdas.Skip(10).Select(fila => fila.ToArray()).ToArray();
            Verificar(motor.Avanzar(tablero).PiezaFijada);
            Verificar(tablero.Puntuacion == 123 && tablero.Lineas == 8 && tablero.LineasUltimoEvento == 0);
            Verificar(tablero.Celdas.Length == 20 && tablero.Celdas.All(fila => fila.Length == 10));
            Verificar(tablero.Celdas.Take(10).All(fila => fila.All(celda => celda == 0)));

            for (var fila = 10; fila < 20; fila++)
            {
                Verificar(tablero.Celdas[fila].SequenceEqual(inferiores[fila - 10]));
            }

            Verificar(tablero.CeldasEliminadasEspecial.Length == 6);
            Verificar(tablero.CeldasEliminadasEspecial.All(celda => celda.Fila < 10));
        });

        prueba("Barredora se aplica una sola vez y consultas o secuencias repetidas no vuelven a barrer", () =>
        {
            var (salas, motor, _, id) = Partida("BARREDORA");
            motor.Tablero!.Celdas[9][0] = 7;
            Caer(salas, id, 1);
            motor.Tablero.Celdas[0][0] = 7;
            var estado = salas.Obtener(id, 1).Data!;
            Verificar(!salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 }).Success);
            var siguiente = salas.Obtener(id, 2).Data!;
            Verificar(siguiente.Tableros[0].Celdas[0][0] == 7);
            Verificar(siguiente.UltimoEfecto!.Secuencia == estado.UltimoEfecto!.Secuencia);
        });

        prueba("Barredora habilitada se sortea; desactivada o excluida no aparece", () =>
        {
            var (salas, _, _, id) = Partida("BARREDORA");
            Verificar(salas.Obtener(id, 1).Data!.Tableros.Single().PiezaEspecial == "BARREDORA");
            var generador = new GeneradorPiezas(new Random(18));
            var desactivada = new ConfiguracionPiezas(false, 100, [Tipo("BARREDORA")]);
            var excluida = new ConfiguracionPiezas(true, 100, [Tipo("BOMBA")]);

            for (var indice = 0; indice < 1000; indice++)
            {
                Verificar(generador.SortearEspecial(desactivada) == null);
                Verificar(generador.SortearEspecial(excluida)!.Codigo == "BOMBA");
            }
        });

        prueba("Barredora respeta individual y competitivo sin limpiar sus tableros", () =>
        {
            var motor = new MotorJuego();
            var tablero = motor.CrearTablero();
            tablero.PiezaEspecial = Tipo("BARREDORA");
            tablero.Celdas[0][0] = 7;
            motor.Mover(tablero, Movimiento.Caer);
            Verificar(tablero.Celdas[0][0] == 7 && tablero.EspecialUltimoEvento == null);
            var (salas, _, _, id) = Partida("BARREDORA", ModoJuego.Competitivo);
            var estado = Caer(salas, id, 1);
            Verificar(estado.Tableros.All(celda => celda.PiezaEspecial == null) && estado.UltimoEfecto == null);
        });
    }

    private static TipoPiezaEspecial Tipo(string codigo)
    {
        return new TipoPiezaEspecial(1, codigo, codigo, codigo == "COL", codigo == "FILA");
    }

    private static void PrepararLinea(TableroJuego tablero, string? codigo)
    {
        foreach (var fila in tablero.Celdas)
        {
            Array.Clear(fila);
        }

        tablero.TipoPieza = 0;
        tablero.Rotacion = 0;
        tablero.Fila = 18;
        tablero.Columna = 3;
        tablero.PiezaEspecial = codigo == null ? null : Tipo(codigo);

        for (var columna = 0; columna < 10; columna++)
        {
            tablero.Celdas[19][columna] = columna >= 3 && columna <= 6 ? 0 : 7;
        }
    }

    private static (SalaLJ Salas, MotorCapturado Motor, RelojEfectos Reloj, Guid Id) Partida(
        string codigo, ModoJuego modo = ModoJuego.Compartido, OpcionesEfectos? opciones = null)
    {
        var reloj = new RelojEfectos();
        var motor = new MotorCapturado();
        var salas = new SalaLJ(motor, new OpcionesPausa(), new CatalogoFalso(Tipo(codigo)), reloj, opciones);
        var creada = salas.Crear(1, "Uno", new TCrearSala
        {
            Modo = modo,
            PiezasNormales = ["I"],
            PiezasEspeciales = new TConfiguracionPiezas { Activas = true, Probabilidad = 100 }
        });
        Verificar(creada.Success, creada.Error ?? "No se creó la sala.");
        var id = creada.Data!.Id;

        for (var jugador = 1; jugador <= 4; jugador++)
        {
            if (jugador > 1)
            {
                Verificar(salas.Unirse(id, jugador, $"J{jugador}").Success);
            }

            salas.Conectar(id, jugador, $"c{jugador}");
            salas.Preparar(id, jugador, true);
        }

        Verificar(salas.Iniciar(id, 1).Success);
        return (salas, motor, reloj, id);
    }

    private static TSala Caer(SalaLJ salas, Guid id, int jugador)
    {
        return Mover(salas, id, jugador, Movimiento.Caer, 1);
    }

    private static TSala Mover(SalaLJ salas, Guid id, int jugador, Movimiento movimiento, long secuencia)
    {
        var respuesta = salas.Mover(id, jugador, new TMovimiento { Movimiento = movimiento, Secuencia = secuencia });
        Verificar(respuesta.Success, respuesta.Error ?? "Movimiento rechazado.");
        return respuesta.Data!;
    }

    private static void Verificar(bool condicion, string mensaje = "No se cumplió la regla del efecto.")
    {
        if (!condicion)
        {
            throw new Exception(mensaje);
        }
    }

    private sealed class RelojEfectos : TimeProvider
    {
        public DateTimeOffset Ahora = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return Ahora;
        }
    }

    private sealed class MotorCapturado : IMotorJuego
    {
        private readonly MotorJuego motor = new(new GeneradorPiezas(new Random(52)));
        public TableroJuego? Tablero;

        public TableroJuego CrearTablero()
        {
            return CrearTablero(ConfiguracionPiezas.SoloNormales);
        }

        public TableroJuego CrearTablero(ConfiguracionPiezas configuracion)
        {
            Tablero = motor.CrearTablero(configuracion);
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
