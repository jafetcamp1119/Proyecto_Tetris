using System.Collections.Concurrent;
using Dominio.EntidadesTipadas;
using Dominio.InterfazJuego;
using Dominio.Juego;
using Utilitarios;

namespace LogicaJuego.Servicios;

// Un único servidor conserva las salas. Cada sala tiene su propio bloqueo para serializar sus cambios.
// Las reglas de la pausa están en PausaPartida/ControlPausas y la conversión a DTO en ProyectorSala.
public class SalaLJ : ISalaLJ
{
    private const int MinimoMultijugador = 2;
    private const int MaximoJugadores = 4;
    private const int SegundosReconexion = 30;

    private readonly ConcurrentQueue<TEventoSala> eventos = new();
    private readonly TimeProvider reloj;
    private DateTime Ahora
    {
        get
        {
            return reloj.GetUtcNow().UtcDateTime;
        }
    }

    private readonly IMotorJuego motor;
    private readonly OpcionesPausa opcionesPausa;
    private readonly ICatalogoPiezasEspeciales? catalogoPiezas;
    private readonly ProyectorSala proyector;
    private readonly ControlPausas pausas;
    private readonly ControlEfectos efectos;
    private readonly CodigosSala codigosPrivados = new();
    private readonly ConcurrentDictionary<Guid, SalaMemoria> _salas = new();

    public SalaLJ(IMotorJuego motor) : this(motor, new OpcionesPausa(), null)
    {
    }

    public SalaLJ(IMotorJuego motor, OpcionesPausa opcionesPausa, ICatalogoPiezasEspeciales? catalogoPiezas,
        TimeProvider? reloj = null, OpcionesEfectos? opcionesEfectos = null)
    {
        this.reloj = reloj ?? TimeProvider.System;
        this.motor = motor;
        this.opcionesPausa = opcionesPausa;
        this.catalogoPiezas = catalogoPiezas;
        this.proyector = new ProyectorSala(motor);
        this.pausas = new ControlPausas(proyector);
        this.efectos = new ControlEfectos(opcionesEfectos ?? new OpcionesEfectos());
    }

    // ---------- Creación y configuración ----------

    public Respuesta<TSala> Crear(int usuarioId, string nombre, TCrearSala datos)
    {
        if (usuarioId <= 0 || string.IsNullOrWhiteSpace(nombre) || !Enum.IsDefined(datos.Modo))
        {
            return Respuesta<TSala>.Fail("Jugador o modo inválido.");
        }

        var esIndividual = datos.Modo == ModoJuego.Individual;
        var maximo = esIndividual ? 1 : datos.MaximoJugadores;
        var minimoPermitido = esIndividual ? 1 : MinimoMultijugador;

        if (maximo < minimoPermitido || maximo > MaximoJugadores)
        {
            return Respuesta<TSala>.Fail("Elegí una sala de dos, tres o cuatro jugadores.");
        }

        var piezas = ConstruirConfiguracionCompleta(
            datos.Modo == ModoJuego.Competitivo ? new TConfiguracionPiezas() : datos.PiezasEspeciales,
            datos.PiezasNormales,
            datos.Velocidad,
            datos.Puntos);

        if (!piezas.Success)
        {
            return Respuesta<TSala>.Fail(piezas.Error!);
        }

        if (datos.MetaPuntos <= 0 || datos.MinimoVictoriasCorona <= 0)
        {
            return Respuesta<TSala>.Fail("La meta y el mínimo de victorias deben ser positivos.");
        }

        var sala = new SalaMemoria(usuarioId, datos.Modo, maximo, opcionesPausa);
        sala.Piezas = piezas.Data!;
        sala.MetaPuntos = datos.MetaPuntos;
        sala.MinimoVictoriasCorona = datos.MinimoVictoriasCorona;
        sala.Jugadores.Add(new JugadorSala(usuarioId, nombre));

        // Una partida individual nunca recibe invitados, por lo que no tiene sentido hacerla privada.
        if (datos.Privada && !esIndividual)
        {
            var codigo = codigosPrivados.Reservar(sala.Id);

            if (codigo == null)
            {
                return Respuesta<TSala>.Fail("No se pudo generar el código de la sala. Intente de nuevo.");
            }

            sala.Privada = true;
            sala.CodigoPrivado = codigo;
        }

        _salas[sala.Id] = sala;
        return Respuesta<TSala>.Ok(proyector.Sala(sala));
    }

    // Solo el creador puede cambiar las piezas especiales, y únicamente antes de iniciar.
    // Las piezas normales, la velocidad y los puntos de la sala se conservan.
    public Respuesta<TSala> ConfigurarPiezas(Guid salaId, int usuarioId, TConfiguracionPiezas datos)
    {
        if (_salas.TryGetValue(salaId, out var existente) && existente.Modo == ModoJuego.Competitivo)
        {
            datos = new TConfiguracionPiezas();
        }

        var especiales = ConstruirConfiguracionPiezas(datos);

        if (!especiales.Success)
        {
            return Respuesta<TSala>.Fail(especiales.Error!);
        }

        return Ejecutar(salaId, usuarioId, (sala, _) =>
        {
            var error = ValidarCreadorEnEspera(sala, usuarioId);

            if (error != null)
            {
                return error;
            }

            sala.Piezas = Combinar(
                especiales.Data!,
                sala.Piezas.FormasPermitidas,
                sala.Piezas.Velocidad,
                sala.Piezas.PuntosPorLineas);
            sala.Version++;
            return null;
        });
    }

    // Cambia de una vez piezas especiales, piezas normales, velocidad y puntos.
    public Respuesta<TSala> ConfigurarSala(Guid salaId, int usuarioId, TConfiguracionSala datos)
    {
        if (_salas.TryGetValue(salaId, out var existente) && existente.Modo == ModoJuego.Competitivo)
        {
            datos.PiezasEspeciales = new TConfiguracionPiezas();
        }

        if (datos.MetaPuntos <= 0 || datos.MinimoVictoriasCorona <= 0)
        {
            return Respuesta<TSala>.Fail("La meta y el mínimo de victorias deben ser positivos.");
        }

        var piezas = ConstruirConfiguracionCompleta(
            datos.PiezasEspeciales,
            datos.PiezasNormales,
            datos.Velocidad,
            datos.Puntos);

        if (!piezas.Success)
        {
            return Respuesta<TSala>.Fail(piezas.Error!);
        }

        return Ejecutar(salaId, usuarioId, (sala, _) =>
        {
            var error = ValidarCreadorEnEspera(sala, usuarioId);

            if (error != null)
            {
                return error;
            }

            sala.Piezas = piezas.Data!;
            sala.MetaPuntos = datos.MetaPuntos;
            sala.MinimoVictoriasCorona = datos.MinimoVictoriasCorona;
            sala.Version++;
            return null;
        });
    }

    private static string? ValidarCreadorEnEspera(SalaMemoria sala, int usuarioId)
    {
        if (sala.Creador != usuarioId)
        {
            return "Solamente el creador puede configurar la sala.";
        }

        if (sala.Estado != EstadoSala.Esperando)
        {
            return "La configuración no se puede cambiar después de iniciar.";
        }

        return null;
    }

    private Respuesta<ConfiguracionPiezas> ConstruirConfiguracionCompleta(
        TConfiguracionPiezas especiales,
        string[]? piezasNormales,
        VelocidadInicial velocidad,
        TPuntosLineas puntos)
    {
        var configuracionEspeciales = ConstruirConfiguracionPiezas(especiales);

        if (!configuracionEspeciales.Success)
        {
            return configuracionEspeciales;
        }

        var formas = ConvertirPiezasNormales(piezasNormales);

        if (!formas.Success)
        {
            return Respuesta<ConfiguracionPiezas>.Fail(formas.Error!);
        }

        if (!Enum.IsDefined(velocidad))
        {
            return Respuesta<ConfiguracionPiezas>.Fail("La velocidad inicial debe ser Lenta, Normal o Rapida.");
        }

        var valoresPuntos = ConvertirPuntos(puntos);

        if (!valoresPuntos.Success)
        {
            return Respuesta<ConfiguracionPiezas>.Fail(valoresPuntos.Error!);
        }

        return Respuesta<ConfiguracionPiezas>.Ok(
            Combinar(configuracionEspeciales.Data!, formas.Data!, velocidad, valoresPuntos.Data!));
    }

    private static ConfiguracionPiezas Combinar(
        ConfiguracionPiezas especiales,
        IReadOnlyList<int> formas,
        VelocidadInicial velocidad,
        IReadOnlyList<int> puntos)
    {
        return new ConfiguracionPiezas(
            especiales.EspecialesActivas,
            especiales.ProbabilidadEspecial,
            especiales.TiposDisponibles,
            formas,
            velocidad,
            puntos);
    }

    // Sin lista: todas las piezas. Con lista: al menos una, solo letras conocidas, sin repetir.
    private static Respuesta<IReadOnlyList<int>> ConvertirPiezasNormales(string[]? piezasNormales)
    {
        if (piezasNormales == null)
        {
            return Respuesta<IReadOnlyList<int>>.Ok(ConfiguracionPiezas.TodasLasFormas);
        }

        var indices = new SortedSet<int>();

        foreach (var letra in piezasNormales)
        {
            var indice = ConfiguracionPiezas.CodigosFormas.ToList()
                .IndexOf((letra ?? string.Empty).Trim().ToUpperInvariant());

            if (indice < 0)
            {
                return Respuesta<IReadOnlyList<int>>.Fail("Pieza normal desconocida. Use I, O, T, S, Z, J o L.");
            }

            indices.Add(indice);
        }

        if (indices.Count == 0)
        {
            return Respuesta<IReadOnlyList<int>>.Fail("Debe haber al menos una pieza normal habilitada.");
        }

        return Respuesta<IReadOnlyList<int>>.Ok(indices.ToArray());
    }

    private static Respuesta<IReadOnlyList<int>> ConvertirPuntos(TPuntosLineas puntos)
    {
        var valores = new[] { puntos.UnaLinea, puntos.DosLineas, puntos.TresLineas, puntos.CuatroLineas };
        var invalido = valores.Any(valor => valor < 0 || valor > ConfiguracionPiezas.PuntosMaximosPorJugada);

        if (invalido)
        {
            return Respuesta<IReadOnlyList<int>>.Fail(
                $"Los puntos por líneas deben estar entre 0 y {ConfiguracionPiezas.PuntosMaximosPorJugada}.");
        }

        return Respuesta<IReadOnlyList<int>>.Ok(valores);
    }

    // Valida lo que envía el cliente y fija, con datos del servidor, qué tipos pueden aparecer.
    private Respuesta<ConfiguracionPiezas> ConstruirConfiguracionPiezas(TConfiguracionPiezas datos)
    {
        if (!datos.Activas)
        {
            return Respuesta<ConfiguracionPiezas>.Ok(ConfiguracionPiezas.SoloNormales);
        }

        if (datos.Probabilidad < 1 || datos.Probabilidad > 100)
        {
            return Respuesta<ConfiguracionPiezas>.Fail(
                "La probabilidad de piezas especiales debe estar entre 1 y 100.");
        }

        var tipos = catalogoPiezas?.ObtenerActivas();

        if (tipos == null || !tipos.Success)
        {
            return Respuesta<ConfiguracionPiezas>.Fail("No se pudieron consultar los tipos de pieza especial.");
        }

        if (tipos.Data!.Length == 0)
        {
            return Respuesta<ConfiguracionPiezas>.Fail(
                "No hay tipos de pieza especial habilitados; desactive las piezas especiales.");
        }

        var permitidos = tipos.Data;

        if (datos.TiposPermitidos != null)
        {
            var codigos = datos.TiposPermitidos.Select(codigo => (codigo ?? string.Empty).Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            permitidos = tipos.Data.Where(tipo => codigos.Contains(tipo.Codigo)).ToArray();

            if (codigos.Count == 0 || permitidos.Length != codigos.Count)
            {
                return Respuesta<ConfiguracionPiezas>.Fail("Seleccione al menos un tipo especial activo del catálogo.");
            }
        }

        return Respuesta<ConfiguracionPiezas>.Ok(new ConfiguracionPiezas(true, datos.Probabilidad, permitidos));
    }

    // ---------- Consulta e ingreso ----------

    public TSalaPublica[] ListarPublicas()
    {
        var publicas = new List<TSalaPublica>();

        foreach (var sala in _salas.Values)
        {
            lock (sala.Cerrojo)
            {
                if (EsPublicaYDisponible(sala))
                {
                    publicas.Add(proyector.SalaPublica(sala));
                }
            }
        }

        return publicas.ToArray();
    }

    private static bool EsPublicaYDisponible(SalaMemoria sala)
    {
        var esperando = sala.Estado == EstadoSala.Esperando;
        var hayEspacio = sala.Jugadores.Count < sala.Maximo;
        var admiteInvitados = sala.Modo != ModoJuego.Individual;

        return !sala.Privada && esperando && hayEspacio && admiteInvitados;
    }

    public Respuesta<TSala> Obtener(Guid salaId, int usuarioId)
    {
        return Ejecutar(salaId, usuarioId, (_, _) => null);
    }

    // Entrada por identificador: solo para salas públicas. Una sala privada exige su código.
    public Respuesta<TSala> Unirse(Guid salaId, int usuarioId, string nombre)
    {
        if (!_salas.TryGetValue(salaId, out var sala))
        {
            return Respuesta<TSala>.Fail("Sala no encontrada.");
        }

        lock (sala.Cerrojo)
        {
            if (sala.Privada && sala.BuscarJugador(usuarioId) == null)
            {
                return Respuesta<TSala>.Fail("Esta sala es privada. Ingrese con su código.");
            }

            return Ingresar(sala, usuarioId, nombre);
        }
    }

    // Acepta el código privado de 6 caracteres o el código público de 12 de una sala pública.
    public Respuesta<TSala> UnirsePorCodigo(string codigo, int usuarioId, string nombre)
    {
        var texto = codigo.Trim();

        if (texto.Length == CodigosSala.Longitud)
        {
            return UnirsePorCodigoPrivado(texto, usuarioId, nombre);
        }

        if (texto.Length == 12)
        {
            return UnirsePorCodigoPublico(texto, usuarioId, nombre);
        }

        return Respuesta<TSala>.Fail("Código inválido.");
    }

    private Respuesta<TSala> UnirsePorCodigoPrivado(string codigo, int usuarioId, string nombre)
    {
        var encontrada = codigosPrivados.TryObtenerSala(codigo, out var salaId);

        if (!encontrada || !_salas.TryGetValue(salaId, out var sala))
        {
            return Respuesta<TSala>.Fail("Código inválido o sala no disponible.");
        }

        lock (sala.Cerrojo)
        {
            return Ingresar(sala, usuarioId, nombre);
        }
    }

    private Respuesta<TSala> UnirsePorCodigoPublico(string codigo, int usuarioId, string nombre)
    {
        var sala = _salas.Values.FirstOrDefault(s => s.Id.ToString("N").StartsWith(
            codigo,
            StringComparison.OrdinalIgnoreCase));

        if (sala == null)
        {
            return Respuesta<TSala>.Fail("Sala no encontrada.");
        }

        return Unirse(sala.Id, usuarioId, nombre);
    }

    // Comprobaciones comunes de ingreso: estado de la sala y capacidad. Se llama con el bloqueo tomado.
    private Respuesta<TSala> Ingresar(SalaMemoria sala, int usuarioId, string nombre)
    {
        ActualizarDesconexiones(sala, Ahora);

        if (sala.BuscarJugador(usuarioId)?.Abandono == true || sala.Estado == EstadoSala.Cancelada)
        {
            return Respuesta<TSala>.Fail("El jugador abandonó o la sala se cerró.");
        }

        if (sala.BuscarJugador(usuarioId) != null)
        {
            return Respuesta<TSala>.Ok(proyector.Sala(sala));
        }

        if (sala.Estado != EstadoSala.Esperando || sala.Jugadores.Count >= sala.Maximo)
        {
            return Respuesta<TSala>.Fail("La sala está llena o ya comenzó.");
        }

        sala.Jugadores.Add(new JugadorSala(usuarioId, nombre));
        sala.Version++;
        return Respuesta<TSala>.Ok(proyector.Sala(sala));
    }

    private Respuesta<TSala> Ejecutar(Guid id, int usuarioId, Func<SalaMemoria, JugadorSala, string?> accion)
    {
        if (!_salas.TryGetValue(id, out var sala))
        {
            return Respuesta<TSala>.Fail("Sala no encontrada.");
        }

        lock (sala.Cerrojo)
        {
            ActualizarDesconexiones(sala, Ahora);
            FinalizarSiCorresponde(sala, Ahora);
            pausas.Actualizar(sala, Ahora);
            ControlEfectos.Actualizar(sala, Ahora);
            var jugador = sala.BuscarJugador(usuarioId);

            if (jugador == null || jugador.Abandono)
            {
                return Respuesta<TSala>.Fail("No pertenece a esta sala.");
            }

            var error = accion(sala, jugador);
            ControlEfectos.Actualizar(sala, Ahora);

            if (error == null)
            {
                return Respuesta<TSala>.Ok(proyector.Sala(sala));
            }

            return Respuesta<TSala>.Fail(error);
        }
    }

    // ---------- Preparación e inicio ----------

    public Respuesta<TSala> Preparar(Guid salaId, int usuarioId, bool listo)
    {
        return Ejecutar(salaId, usuarioId, (sala, jugador) =>
        {
            if (sala.Estado != EstadoSala.Esperando)
            {
                return "La partida ya comenzó.";
            }

            jugador.Listo = listo;
            sala.Version++;
            return null;
        });
    }

    public Respuesta<TSala> Iniciar(Guid salaId, int usuarioId)
    {
        return Ejecutar(salaId, usuarioId, (sala, _) =>
        {
            var error = ValidarInicio(sala, usuarioId);

            if (error != null)
            {
                return error;
            }

            CrearTableros(sala);
            sala.Estado = EstadoSala.EnCurso;
            sala.Version++;
            sala.Revision++;
            return null;
        });
    }

    // Se espera la confirmación del guardado final antes de descartar el estado de la ronda.
    public Respuesta<TSala> NuevaRonda(Guid salaId, int usuarioId)
    {
        return Ejecutar(salaId, usuarioId, (sala, _) =>
        {
            if (sala.Creador != usuarioId)
            {
                return "Solamente el creador puede preparar otra ronda.";
            }

            if (sala.Estado != EstadoSala.Finalizada)
            {
                return "La ronda debe haber finalizado.";
            }

            if (sala.RevisionGuardada != sala.Revision)
            {
                return "Los resultados todavía se están guardando. Intentá nuevamente en unos segundos.";
            }

            sala.Jugadores.RemoveAll(jugador => jugador.Abandono);
            foreach (var jugador in sala.Jugadores)
            {
                jugador.Listo = false;
                jugador.Perdido = false;
                jugador.Puntos = 0;
                jugador.Lineas = 0;
                jugador.Nivel = 1;
                jugador.Secuencia = 0;
                jugador.Tablero = null;
                jugador.Efectos.Clear();
                jugador.Desconexion = null;
                if (jugador.Conexiones.Count == 0)
                {
                    jugador.Desconexion = Ahora;
                }
            }

            sala.NumeroRonda++;
            sala.Estado = EstadoSala.Esperando;
            sala.Compartido = null;
            sala.Turno = 0;
            sala.Inicio = null;
            sala.Fin = null;
            sala.Ganadores = [];
            sala.MotivoFin = null;
            sala.UltimoEfecto = null;
            sala.Pausa = new PausaPartida(opcionesPausa);
            sala.Creada = Ahora;
            sala.Version++;
            return null;
        });
    }

    private static string? ValidarInicio(SalaMemoria sala, int usuarioId)
    {
        if (sala.Creador != usuarioId)
        {
            return "Solamente el creador puede iniciar.";
        }

        if (sala.Estado != EstadoSala.Esperando)
        {
            return "La partida ya comenzó.";
        }

        var minimo = sala.Modo == ModoJuego.Individual ? 1 : MinimoMultijugador;

        if (sala.Jugadores.Count < minimo || sala.Jugadores.Any(j => !j.Listo))
        {
            return "Faltan jugadores o participantes listos.";
        }

        if (sala.Modo != ModoJuego.Individual && sala.Jugadores.Any(j => j.Conexiones.Count == 0))
        {
            return "Todos deben estar conectados antes de iniciar.";
        }

        return ValidarPiezasAntesDeIniciar(sala.Piezas);
    }

    // La configuración ya se validó al crearla; se repite aquí porque es una regla del servidor.
    private static string? ValidarPiezasAntesDeIniciar(ConfiguracionPiezas piezas)
    {
        var puntosValidos = piezas.PuntosPorLineas.Count == 4
            && piezas.PuntosPorLineas.All(valor => valor >= 0 && valor <= ConfiguracionPiezas.PuntosMaximosPorJugada);

        if (piezas.FormasPermitidas.Count == 0 || !puntosValidos)
        {
            return "La configuración de piezas normales o de puntos no es válida.";
        }

        if (!piezas.EspecialesActivas)
        {
            return null;
        }

        var probabilidadValida = piezas.ProbabilidadEspecial >= 1 && piezas.ProbabilidadEspecial <= 100;

        if (!probabilidadValida || piezas.TiposDisponibles.Count == 0)
        {
            return "La configuración de piezas especiales no es válida.";
        }

        return null;
    }

    private void CrearTableros(SalaMemoria sala)
    {
        if (sala.Modo == ModoJuego.Competitivo)
        {
            sala.Piezas = Combinar(ConfiguracionPiezas.SoloNormales,
                sala.Piezas.FormasPermitidas, sala.Piezas.Velocidad, sala.Piezas.PuntosPorLineas);
        }

        sala.Inicio = Ahora;
        sala.UltimaCaida = sala.Inicio.Value;

        if (sala.Modo == ModoJuego.Compartido)
        {
            sala.Compartido = motor.CrearTablero(sala.Piezas);
            sala.Compartido.EsCompartido = true;
            return;
        }

        foreach (var jugador in sala.Jugadores)
        {
            jugador.Tablero = motor.CrearTablero(sala.Piezas);
            jugador.UltimaCaida = sala.Inicio.Value;
        }
    }

    // ---------- Movimientos ----------

    public Respuesta<TSala> Mover(Guid salaId, int usuarioId, TMovimiento datos)
    {
        return Ejecutar(salaId, usuarioId, (sala, jugador) =>
        {
            if (sala.Estado != EstadoSala.EnCurso || jugador.Perdido || jugador.Desconexion.HasValue)
            {
                return "El jugador no está activo.";
            }

            if (pausas.EstaPausada(sala))
            {
                return "La partida está en pausa.";
            }

            if (!Enum.IsDefined(datos.Movimiento) || datos.Secuencia <= jugador.Secuencia)
            {
                return "Acción inválida o repetida.";
            }

            if (sala.Modo == ModoJuego.Compartido && sala.Jugadores[sala.Turno].Id != usuarioId)
            {
                return "No es su turno.";
            }

            jugador.Secuencia = datos.Secuencia;
            var tablero = sala.Compartido ?? jugador.Tablero!;
            var movimiento = ControlEfectos.TraducirMovimiento(jugador, datos.Movimiento);
            AplicarPaso(sala, jugador, tablero, () => motor.Mover(tablero, movimiento), Ahora);
            return null;
        });
    }

    private void AplicarPaso(
        SalaMemoria sala,
        JugadorSala jugador,
        TableroJuego tablero,
        Func<PasoJuego> accion,
        DateTime ahora)
    {
        var puntos = tablero.Puntuacion;
        var lineas = tablero.Lineas;
        var secuenciaEvento = tablero.EventoSecuencia;
        var paso = accion();

        jugador.Puntos += tablero.Puntuacion - puntos;
        jugador.Lineas += tablero.Lineas - lineas;
        jugador.Nivel = Math.Max(jugador.Nivel, tablero.Nivel);

        if (paso.Cambio)
        {
            sala.Version++;
        }

        if (tablero.Perdido)
        {
            EliminarJugador(sala, jugador);
        }

        if (paso.PiezaFijada)
        {
            if (tablero.EventoSecuencia > secuenciaEvento)
            {
                efectos.Activar(sala, jugador, tablero, tablero.Puntuacion - puntos, ahora);
            }

            jugador.UltimaCaida = ahora;
            sala.UltimaCaida = ahora;

            if (sala.Modo == ModoJuego.Compartido && !jugador.Perdido)
            {
                SiguienteTurno(sala);
            }
        }

        FinalizarSiCorresponde(sala, ahora);
        ControlEfectos.Actualizar(sala, ahora);
    }

    // La limpieza de emergencia no cambia los puntos ni las líneas.
    private static void EliminarJugador(SalaMemoria sala, JugadorSala jugador)
    {
        jugador.Perdido = true;

        if (jugador.Tablero != null)
        {
            jugador.Tablero.Perdido = true;
        }

        if (sala.Compartido != null && sala.Jugadores[sala.Turno] == jugador)
        {
            for (var fila = 0; fila < 10; fila++)
            {
                Array.Clear(sala.Compartido.Celdas[fila]);
            }

            // Se conserva la siguiente pieza que el motor ya sorteó.
            sala.Compartido.Perdido = false;
            sala.Compartido.Fila = 0;
            sala.Compartido.Columna = 3;
            sala.Compartido.Rotacion = 0;
            SiguienteTurno(sala);
        }

        sala.Revision++;
        sala.Version++;
    }

    private static void SiguienteTurno(SalaMemoria sala)
    {
        for (var i = 0; i < sala.Jugadores.Count; i++)
        {
            sala.Turno = (sala.Turno + 1) % sala.Jugadores.Count;

            if (!sala.Jugadores[sala.Turno].Perdido)
            {
                return;
            }
        }
    }

    private void FinalizarSiCorresponde(SalaMemoria sala, DateTime ahora)
    {
        if (sala.Estado != EstadoSala.EnCurso)
        {
            return;
        }

        var activos = sala.Jugadores.Where(jugador => !jugador.Perdido).ToArray();
        var supervivencia = sala.Modo == ModoJuego.Compartido && activos.Length == 1;
        var meta = sala.Modo == ModoJuego.Compartido && sala.MetaPuntos.HasValue
            && sala.Jugadores.Any(jugador => jugador.Puntos >= sala.MetaPuntos.Value);

        if (activos.Length > 0 && !supervivencia && !meta)
        {
            return;
        }

        sala.Estado = EstadoSala.Finalizada;
        sala.Fin = ahora;
        sala.MotivoFin = "TodosEliminados";

        if (supervivencia)
        {
            sala.MotivoFin = "Supervivencia";
        }
        else if (meta)
        {
            sala.MotivoFin = "MetaPuntos";
        }

        if (sala.Modo == ModoJuego.Compartido && activos.Length == 0)
        {
            sala.Ganadores = [];
        }
        else if (supervivencia)
        {
            sala.Ganadores = [activos[0].Id];
        }
        else
        {
            var maximo = sala.Jugadores.Max(jugador => jugador.Puntos);
            sala.Ganadores = sala.Jugadores.Where(jugador => jugador.Puntos == maximo)
                .Select(jugador => jugador.Id).ToArray();
        }

        foreach (var ganador in sala.Jugadores.Where(jugador => sala.Ganadores.Contains(jugador.Id)))
        {
            ganador.Victorias++;
        }

        sala.Revision++;
        sala.Version++;
        pausas.CancelarSiTermino(sala, ahora);
    }

    public Respuesta<TSala> Abandonar(Guid salaId, int usuarioId)
    {
        return Ejecutar(salaId, usuarioId, (sala, jugador) =>
        {
            if (usuarioId == sala.Creador)
            {
                CerrarSala(sala, "El creador abandonó la sala.", Ahora);
                return null;
            }

            var conexionesRetirar = jugador.Conexiones.ToArray();
            jugador.Abandono = true;
            jugador.Conexiones.Clear();
            jugador.Desconexion = null;

            if (sala.Estado == EstadoSala.Esperando)
            {
                sala.Jugadores.Remove(jugador);
            }
            else if (sala.Estado == EstadoSala.EnCurso && !jugador.Perdido)
            {
                EliminarJugador(sala, jugador);
                FinalizarSiCorresponde(sala, Ahora);
            }

            sala.Version++;
            eventos.Enqueue(new TEventoSala("JugadorAbandono", sala.Id,
                new { SalaId = sala.Id, UsuarioId = usuarioId }, conexionesRetirar));
            return null;
        });
    }

    private void CerrarSala(SalaMemoria sala, string motivo, DateTime ahora)
    {
        if (sala.Estado != EstadoSala.Finalizada)
        {
            sala.Estado = EstadoSala.Cancelada;
            sala.Ganadores = [];
            sala.Fin = ahora;
            sala.MotivoFin = motivo;
        }

        sala.Pausa.Cancelar();
        sala.Compartido = null;
        sala.Version++;
        sala.Revision++;
        eventos.Enqueue(new TEventoSala("SalaCerrada", sala.Id,
            new { SalaId = sala.Id, Motivo = motivo },
            sala.Jugadores.SelectMany(jugador => jugador.Conexiones).ToArray()));

        foreach (var jugador in sala.Jugadores)
        {
            jugador.Conexiones.Clear();
            jugador.Tablero = null;
            jugador.Abandono = true;
        }

        codigosPrivados.Liberar(sala.CodigoPrivado, sala.Id);
    }

    // ---------- Pausa ----------

    public Respuesta<TSala> SolicitarPausa(Guid salaId, int usuarioId)
    {
        return Ejecutar(salaId, usuarioId, (sala, jugador) =>
        {
            return pausas.Solicitar(sala, jugador, Ahora);
        });
    }

    public Respuesta<TSala> VotarPausa(Guid salaId, int usuarioId, bool aceptar)
    {
        return Ejecutar(salaId, usuarioId, (sala, jugador) =>
        {
            return pausas.Votar(sala, jugador, aceptar, Ahora);
        });
    }

    public Respuesta<TSala> Reanudar(Guid salaId, int usuarioId)
    {
        return Ejecutar(salaId, usuarioId, (sala, _) =>
        {
            return pausas.ReanudarIndividual(sala, Ahora);
        });
    }

    public TEventoSala[] TomarEventos()
    {
        var pendientes = new List<TEventoSala>(pausas.TomarEventos());

        while (eventos.TryDequeue(out var evento))
        {
            pendientes.Add(evento);
        }

        return pendientes.ToArray();
    }

    // ---------- Conexiones ----------

    public Respuesta<TSala> Conectar(Guid salaId, int usuarioId, string conexionId)
    {
        return Ejecutar(salaId, usuarioId, (sala, jugador) =>
        {
            if (jugador.Abandono || sala.Estado == EstadoSala.Cancelada)
            {
                return "La sala está cerrada o el plazo de reconexión terminó.";
            }

            jugador.Conexiones.Add(conexionId);
            jugador.Desconexion = null;
            sala.Version++;
            return null;
        });
    }

    public bool ConexionAutorizada(Guid salaId, int usuarioId, string conexionId)
    {
        if (!_salas.TryGetValue(salaId, out var sala))
        {
            return false;
        }

        lock (sala.Cerrojo)
        {
            var jugador = sala.BuscarJugador(usuarioId);
            return jugador != null && !jugador.Abandono && jugador.Conexiones.Contains(conexionId);
        }
    }

    public TSala[] Desconectar(string conexionId)
    {
        var cambios = new List<TSala>();

        foreach (var sala in _salas.Values)
        {
            lock (sala.Cerrojo)
            {
                if (QuitarConexion(sala, conexionId))
                {
                    cambios.Add(proyector.Sala(sala));
                }
            }
        }

        return cambios.ToArray();
    }

    private bool QuitarConexion(SalaMemoria sala, string conexionId)
    {
        var cambio = false;

        foreach (var jugador in sala.Jugadores)
        {
            if (!jugador.Conexiones.Remove(conexionId))
            {
                continue;
            }

            if (jugador.Conexiones.Count == 0)
            {
                jugador.Desconexion = Ahora;

                if (sala.Estado == EstadoSala.Esperando)
                {
                    jugador.Listo = false;
                }
            }

            sala.Version++;
            cambio = true;
        }

        return cambio;
    }

    // ---------- Reloj del servidor ----------

    public TSala[] Avanzar(DateTime ahora)
    {
        var actualizadas = new List<TSala>();

        foreach (var sala in _salas.Values)
        {
            lock (sala.Cerrojo)
            {
                if (PuedeRetirarse(sala, ahora))
                {
                    RetirarSala(sala);
                    continue;
                }

                var versionAnterior = sala.Version;

                if (sala.Estado == EstadoSala.Esperando)
                {
                    ActualizarDesconexiones(sala, ahora);
                }

                if (sala.Estado == EstadoSala.EnCurso)
                {
                    AvanzarSalaEnCurso(sala, ahora);
                }

                if (sala.Version != versionAnterior)
                {
                    actualizadas.Add(proyector.Sala(sala));
                }
            }
        }

        return actualizadas.ToArray();
    }

    private void AvanzarSalaEnCurso(SalaMemoria sala, DateTime ahora)
    {
        pausas.Actualizar(sala, ahora);

        ActualizarDesconexiones(sala, ahora);

        FinalizarSiCorresponde(sala, ahora);
        ControlEfectos.Actualizar(sala, ahora);

        // Durante una pausa las piezas no descienden y el turno no avanza.
        if (sala.Estado == EstadoSala.EnCurso && !pausas.EstaPausada(sala))
        {
            AvanzarTableros(sala, ahora);
        }
    }

    private void RetirarSala(SalaMemoria sala)
    {
        _salas.TryRemove(sala.Id, out _);
        codigosPrivados.Liberar(sala.CodigoPrivado, sala.Id);
    }

    private static bool PuedeRetirarse(SalaMemoria sala, DateTime ahora)
    {
        var esperaVencida = sala.Estado == EstadoSala.Esperando &&
            ahora - sala.Creada > TimeSpan.FromHours(2);

        // Un final pendiente debe permanecer disponible hasta que SQL confirme el guardado.
        var finalGuardado = (sala.Estado == EstadoSala.Finalizada || sala.Estado == EstadoSala.Cancelada) &&
            sala.RevisionGuardada == sala.Revision &&
            ahora - sala.Fin > TimeSpan.FromHours(2);

        var cerradaGuardada = (sala.Estado == EstadoSala.Cancelada
            || sala.Jugadores.All(jugador => jugador.Abandono))
            && sala.RevisionGuardada == sala.Revision;

        return esperaVencida || finalGuardado || cerradaGuardada;
    }

    private void ActualizarDesconexiones(SalaMemoria sala, DateTime ahora)
    {
        if (sala.Estado != EstadoSala.EnCurso && sala.Estado != EstadoSala.Esperando)
        {
            return;
        }

        var vencidos = sala.Jugadores.Where(jugador => jugador.Desconexion.HasValue
            && ahora - jugador.Desconexion.Value >= TimeSpan.FromSeconds(SegundosReconexion)).ToArray();

        // La cancelación del creador tiene prioridad sobre cualquier victoria.
        if (vencidos.Any(jugador => jugador.Id == sala.Creador))
        {
            CerrarSala(sala, "El creador no se reconectó dentro del plazo.", ahora);
            return;
        }

        foreach (var jugador in vencidos)
        {
            jugador.Desconexion = null;
            jugador.Abandono = true;

            if (sala.Estado == EstadoSala.Esperando)
            {
                sala.Jugadores.Remove(jugador);
            }
            else if (!jugador.Perdido)
            {
                EliminarJugador(sala, jugador);
            }

            sala.Version++;
            eventos.Enqueue(new TEventoSala("JugadorAbandono", sala.Id,
                new { SalaId = sala.Id, UsuarioId = jugador.Id }));
        }
    }

    private void AvanzarTableros(SalaMemoria sala, DateTime ahora)
    {
        if (sala.Compartido != null)
        {
            AvanzarTableroCompartido(sala, ahora);
            return;
        }

        foreach (var jugador in sala.Jugadores.Where(jugador => !jugador.Perdido && !jugador.Desconexion.HasValue))
        {
            if (ahora - jugador.UltimaCaida < Intervalo(jugador.Tablero!))
            {
                continue;
            }

            jugador.UltimaCaida = ahora;
            AplicarPaso(sala, jugador, jugador.Tablero!, () => motor.Avanzar(jugador.Tablero!), ahora);
        }
    }

    private void AvanzarTableroCompartido(SalaMemoria sala, DateTime ahora)
    {
        if (sala.Jugadores[sala.Turno].Desconexion.HasValue
            || ahora - sala.UltimaCaida < Intervalo(sala.Compartido!, ControlEfectos.FactorVelocidad(sala.Jugadores[sala.Turno])))
        {
            return;
        }

        sala.UltimaCaida = ahora;
        AplicarPaso(
            sala,
            sala.Jugadores[sala.Turno],
            sala.Compartido!,
            () => motor.Avanzar(sala.Compartido!),
            ahora);
    }

    private static TimeSpan Intervalo(TableroJuego tablero, int factor = 1)
    {
        var milisegundos = tablero.Piezas.IntervaloBaseMs - (tablero.Nivel - 1) * 60;
        return TimeSpan.FromMilliseconds(Math.Max(1, Math.Max(100, milisegundos) / factor));
    }

    // ---------- Persistencia ----------

    public TSala[] PendientesPersistencia()
    {
        var pendientes = new List<TSala>();

        foreach (var sala in _salas.Values)
        {
            lock (sala.Cerrojo)
            {
                if (sala.Revision > sala.RevisionGuardada)
                {
                    pendientes.Add(proyector.Sala(sala));
                }
            }
        }

        return pendientes.ToArray();
    }

    public void ConfirmarPersistencia(Guid salaId, long revision)
    {
        if (_salas.TryGetValue(salaId, out var sala))
        {
            lock (sala.Cerrojo)
            {
                sala.RevisionGuardada = Math.Max(sala.RevisionGuardada, Math.Min(revision, sala.Revision));
            }
        }
    }
}
