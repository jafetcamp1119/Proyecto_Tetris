using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.Juego;
using LogicaJuego.Motor;
using LogicaJuego.Servicios;

internal static class PruebasDefinitivas
{
    public static void Agregar(Action<string, Action> prueba)
    {
        prueba("Competitivo ignora especiales incluso si el catálogo falla", () =>
        {
            var salas = new SalaLJ(new MotorJuego(), new OpcionesPausa(), new CatalogoFalso { Fallar = true });
            var datos = new TCrearSala
            {
                Modo = ModoJuego.Competitivo,
                PiezasEspeciales = new TConfiguracionPiezas { Activas = true, Probabilidad = 100 }
            };
            var id = Preparar(salas, datos);
            var sala = salas.Iniciar(id, 1).Data!;
            Verificar(!sala.PiezasEspeciales.Activas);
            Verificar(sala.Tableros.All(tablero => tablero.PiezaEspecial == null));
            Verificar(!salas.ConfigurarPiezas(id, 1, datos.PiezasEspeciales).Success);
        });

        prueba("Compartido elimina solo el turno, limpia diez filas y conserva las inferiores", () =>
        {
            var motor = new MotorDerrotaCompartida();
            var salas = new SalaLJ(motor);
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Compartido });
            salas.Iniciar(id, 1);
            var tablero = motor.Tablero!;

            for (var fila = 0; fila < 20; fila++)
            {
                tablero.Celdas[fila][0] = 7;
            }

            var sala = Perder(salas, id, 1);
            Verificar(sala.Estado == EstadoSala.EnCurso && sala.UsuarioTurno == 2);
            Verificar(sala.Jugadores.Count(jugador => jugador.Perdido) == 1);
            Verificar(sala.Tableros[0].Celdas.Take(10).All(fila => fila.All(celda => celda == 0)));
            Verificar(sala.Tableros[0].Celdas.Skip(10).All(fila => fila[0] == 7));
            Verificar(sala.Jugadores.All(jugador => jugador.Puntos == 0 && jugador.Lineas == 0));
            Verificar(!salas.Mover(id, 1, new TMovimiento { Secuencia = 2 }).Success);
            Verificar(!salas.SolicitarPausa(id, 1).Success);
        });

        prueba("Compartido gana el superviviente aunque otro haya sumado más puntos", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Compartido });
            salas.Iniciar(id, 1);
            Perder(salas, id, 1);
            salas.Mover(id, 2, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
            Perder(salas, id, 3);
            var sala = salas.Abandonar(id, 2).Data!;
            Verificar(sala.Estado == EstadoSala.Finalizada && sala.Ganadores.SequenceEqual(new[] { 4 }));
            Verificar(sala.Jugadores[1].Puntos > sala.Jugadores[3].Puntos);
            Verificar(sala.MotivoFin == "Supervivencia" && sala.Jugadores[3].Posicion == 1);
        });

        prueba("Compartido finaliza por meta y persiste también al ganador activo", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var id = Preparar(salas, new TCrearSala
            {
                Modo = ModoJuego.Compartido,
                MetaPuntos = 10,
                MinimoVictoriasCorona = 1
            });
            salas.Iniciar(id, 1);
            var sala = salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 }).Data!;
            Verificar(sala.Estado == EstadoSala.Finalizada && sala.MotivoFin == "MetaPuntos");
            Verificar(sala.Ganadores.SequenceEqual(new[] { 1 }));
            Verificar(sala.Jugadores[0].VictoriasSala == 1 && sala.Jugadores[0].Corona);
            var unidad = new UnidadMemoria();
            var registro = new RegistroPartidaLJ(unidad);
            registro.GuardarAsync(sala).GetAwaiter().GetResult();
            registro.GuardarAsync(sala).GetAwaiter().GetResult();
            Verificar(unidad.Resultados.Datos.Count == 4);
            Verificar(unidad.Resultados.Datos.Single(resultado => resultado.UsuarioId == 1).EsGanador);
            Verificar(unidad.Estadisticas.Datos.All(estadistica => estadistica.PartidasJugadas == 1));
        });

        prueba("Creador que abandona cancela sin ganadores y libera el código privado", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Compartido, Privada = true });
            salas.Iniciar(id, 1);
            var cancelada = salas.Abandonar(id, 1).Data!;
            Verificar(cancelada.Estado == EstadoSala.Cancelada && cancelada.Ganadores.Length == 0);
            Verificar(cancelada.Jugadores.All(jugador => jugador.Abandono && !jugador.Conectado));
            Verificar(!salas.Obtener(id, 2).Success);
            Verificar(!salas.UnirsePorCodigo(cancelada.CodigoPrivado!, 5, "Cinco").Success);
            var evento = salas.TomarEventos().Single(evento => evento.Nombre == "SalaCerrada");
            Verificar(evento.ConexionesRetirar!.Length == 4);
            salas.ConfirmarPersistencia(id, cancelada.RevisionPersistencia);
            salas.Avanzar(DateTime.UtcNow);
            Verificar(salas.PendientesPersistencia().Length == 0);
        });

        prueba("Abandono en espera libera el espacio y durante competitivo conserva puntos", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Competitivo });
            Verificar(salas.Abandonar(id, 4).Data!.Jugadores.Length == 3);
            Verificar(salas.Unirse(id, 5, "Cinco").Success);
            salas.Conectar(id, 5, "c5");
            salas.Preparar(id, 5, true);
            salas.Iniciar(id, 1);
            salas.Mover(id, 2, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
            var sala = salas.Abandonar(id, 2).Data!;
            var jugador = sala.Jugadores.Single(jugador => jugador.UsuarioId == 2);
            Verificar(jugador.Perdido && jugador.Abandono && jugador.Puntos == 10);
            Verificar(sala.Estado == EstadoSala.EnCurso && sala.Ganadores.Length == 0);
            Verificar(!salas.Conectar(id, 2, "otra").Success);
            Verificar(!salas.Mover(id, 2, new TMovimiento { Secuencia = 2 }).Success);
        });

        prueba("Abandono compartido pasa el turno y puede producir victoria por supervivencia", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Compartido });
            salas.Iniciar(id, 1);
            salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
            Verificar(salas.Abandonar(id, 2).Data!.UsuarioTurno == 3);
            salas.Abandonar(id, 3);
            var sala = salas.Abandonar(id, 4).Data!;
            Verificar(sala.Estado == EstadoSala.Finalizada && sala.Ganadores.SequenceEqual(new[] { 1 }));
        });

        prueba("Reconexión conserva identidad y secuencia y rechaza movimientos desconectados", () =>
        {
            var reloj = new RelojPrueba();
            var salas = new SalaLJ(new MotorControlado(), new OpcionesPausa(), null, reloj);
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Competitivo });
            salas.Iniciar(id, 1);
            salas.Mover(id, 2, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
            salas.Desconectar("c2");
            Verificar(!salas.Mover(id, 2, new TMovimiento { Secuencia = 2 }).Success);
            reloj.Ahora = reloj.Ahora.AddSeconds(29);
            var sala = salas.Conectar(id, 2, "nueva").Data!;
            Verificar(sala.Jugadores.Length == 4 && sala.Jugadores[1].UltimaSecuencia == 1);
            Verificar(sala.Jugadores[1].Puntos == 10 && !sala.Jugadores[1].Perdido);
            Verificar(!salas.ConexionAutorizada(id, 2, "c2"));
            Verificar(salas.ConexionAutorizada(id, 2, "nueva"));
        });

        prueba("Reconexión tardía se rechaza incluso antes del próximo tick", () =>
        {
            var reloj = new RelojPrueba();
            var salas = new SalaLJ(new MotorControlado(), new OpcionesPausa(), null, reloj);
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Compartido });
            salas.Iniciar(id, 1);
            salas.Mover(id, 1, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
            salas.Desconectar("c2");
            reloj.Ahora = reloj.Ahora.AddSeconds(30);
            Verificar(!salas.Conectar(id, 2, "tarde").Success);
            var sala = salas.Obtener(id, 1).Data!;
            Verificar(sala.Jugadores[1].Perdido && sala.UsuarioTurno == 3);
        });

        prueba("Desconexión vencida del creador tiene prioridad sobre supervivencia", () =>
        {
            var reloj = new RelojPrueba();
            var salas = new SalaLJ(new MotorControlado(), new OpcionesPausa(), null, reloj);
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Compartido });
            salas.Iniciar(id, 1);
            salas.Desconectar("c1");
            salas.Desconectar("c2");
            salas.Desconectar("c3");
            salas.Avanzar(reloj.Ahora.AddSeconds(30).UtcDateTime);
            var sala = salas.PendientesPersistencia().Single();
            Verificar(sala.Estado == EstadoSala.Cancelada && sala.Ganadores.Length == 0);
        });

        prueba("Reinicio cancela solo partidas incompletas y conserva finales guardados", () =>
        {
            var unidad = new UnidadMemoria();
            unidad.Partidas.Datos.Add(new Partida { PartidaId = 1, Estado = "EnCurso" });
            unidad.Partidas.Datos.Add(new Partida { PartidaId = 2, Estado = "Finalizada", FechaFin = DateTime.UtcNow, GanadorId = 4 });
            unidad.Resultados.Datos.Add(new Resultado { PartidaId = 1, Partida = unidad.Partidas.Datos[0] });
            unidad.Resultados.Datos.Add(new Resultado { PartidaId = 2, Partida = unidad.Partidas.Datos[1], UsuarioId = 4, EsGanador = true });
            var registro = new RegistroPartidaLJ(unidad);
            registro.CancelarInterrumpidasAsync().GetAwaiter().GetResult();
            registro.CancelarInterrumpidasAsync().GetAwaiter().GetResult();
            Verificar(unidad.Partidas.Datos[0].Estado == "Cancelada");
            Verificar(unidad.Partidas.Datos[1].GanadorId == 4);
            Verificar(unidad.Resultados.Datos.Count == 1 && unidad.Resultados.Datos[0].EsGanador);
        });

        prueba("Creador desconectado en espera cierra la sala al vencer el plazo", () =>
        {
            var reloj = new RelojPrueba();
            var salas = new SalaLJ(new MotorControlado(), new OpcionesPausa(), null, reloj);
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Competitivo });
            salas.Desconectar("c1");
            reloj.Ahora = reloj.Ahora.AddSeconds(30);
            Verificar(!salas.Conectar(id, 1, "tarde").Success);
            Verificar(salas.ListarPublicas().Length == 0);
            Verificar(salas.TomarEventos().Any(evento => evento.Nombre == "SalaCerrada"));
        });

        prueba("Competitivo elimina al participante desconectado y los demás continúan", () =>
        {
            var reloj = new RelojPrueba();
            var salas = new SalaLJ(new MotorControlado(), new OpcionesPausa(), null, reloj);
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Competitivo });
            salas.Iniciar(id, 1);
            salas.Mover(id, 2, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 });
            salas.Desconectar("c2");
            salas.Avanzar(reloj.Ahora.AddSeconds(30).UtcDateTime);
            var sala = salas.Obtener(id, 1).Data!;
            Verificar(sala.Estado == EstadoSala.EnCurso && sala.Jugadores[1].Perdido);
            Verificar(sala.Jugadores[1].Puntos == 10 && sala.Ganadores.Length == 0);
            Verificar(salas.Mover(id, 3, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 1 }).Success);
        });

        prueba("Empatados suman una victoria y la corona permanece desactivada sin criterio", () =>
        {
            var salas = new SalaLJ(new MotorControlado());
            var id = Preparar(salas, new TCrearSala { Modo = ModoJuego.Competitivo });
            salas.Iniciar(id, 1);

            for (var jugador = 1; jugador <= 4; jugador++)
            {
                Perder(salas, id, jugador);
            }

            var sala = salas.Obtener(id, 1).Data!;
            Verificar(sala.Ganadores.Length == 4);
            Verificar(sala.Jugadores.All(jugador => jugador.Posicion == 1
                && jugador.VictoriasSala == 1 && !jugador.Corona));
            var unidad = new UnidadMemoria();
            var registro = new RegistroPartidaLJ(unidad);
            registro.GuardarAsync(sala).GetAwaiter().GetResult();
            var cierre = salas.Abandonar(id, 1).Data!;
            registro.GuardarAsync(cierre).GetAwaiter().GetResult();
            Verificar(unidad.Resultados.Datos.Count == 4);
            Verificar(unidad.Estadisticas.Datos.All(estadistica => estadistica.PartidasGanadas == 1));
        });
    }

    private static Guid Preparar(SalaLJ salas, TCrearSala datos)
    {
        var sala = salas.Crear(1, "Uno", datos).Data!;

        for (var jugador = 1; jugador <= 4; jugador++)
        {
            if (jugador > 1)
            {
                var ingreso = sala.Privada
                    ? salas.UnirsePorCodigo(sala.CodigoPrivado!, jugador, $"J{jugador}")
                    : salas.Unirse(sala.Id, jugador, $"J{jugador}");
                Verificar(ingreso.Success);
            }

            salas.Conectar(sala.Id, jugador, $"c{jugador}");
            salas.Preparar(sala.Id, jugador, true);
        }

        return sala.Id;
    }

    private static TSala Perder(SalaLJ salas, Guid id, int usuario)
    {
        return salas.Mover(id, usuario, new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 10 }).Data!;
    }

    private static void Verificar(bool condicion)
    {
        if (!condicion)
        {
            throw new Exception("No se cumplió la regla definitiva.");
        }
    }

    private sealed class RelojPrueba : TimeProvider
    {
        public DateTimeOffset Ahora = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return Ahora;
        }
    }

    private sealed class MotorDerrotaCompartida : IMotorJuego
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
            tablero.Perdido = true;
            return new PasoJuego(true, true);
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
