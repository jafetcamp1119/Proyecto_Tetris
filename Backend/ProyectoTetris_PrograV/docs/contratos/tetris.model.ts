// Referencia de contratos. La implementación Ionic vigente está en frontend/tetris-app/src/app/modelos.

export interface IRespuesta<T> {
  success: boolean;
  data: T | null;
  error: string | null;
  codigoError?: string | null;
}

export interface IUsuario {
  usuarioId: number;
  nombreUsuario: string;
  fechaRegistro: string;
  activo: boolean;
}

export interface IRegistroUsuario {
  nombreUsuario: string;
}

export interface IRecuperarSesion {
  credencial: string;
}

export interface IAutenticacion extends IUsuario {
  token: string;
  tipoToken: 'Bearer';
  // Puede rotarse al ingresar; guardar el valor recibido en almacenamiento seguro nativo.
  credencial?: string;
  salaActivaId?: string | null;
  codigoRecuperacionNuevo?: string | null;
  codigoRecuperacionPorConfirmar?: boolean;
  credencialExpiraEnUtc: string;
  expiraEnUtc: string;
}

export type ModoJuego = 'Individual' | 'Compartido' | 'Competitivo';
export type EstadoSala = 'Esperando' | 'EnCurso' | 'Finalizada' | 'Cancelada';
export type Movimiento = 'Izquierda' | 'Derecha' | 'Rotar' | 'Bajar' | 'Caer';

// El servidor valida los códigos contra el catálogo activo y sortea cada pieza.
export interface IConfiguracionPiezas {
  activas: boolean;
  // De 1 a 100 cuando activas es true; se ignora cuando es false.
  probabilidad: number;
  // Omitido o null: todos los activos. Vacío se rechaza al activar especiales.
  tiposPermitidos?: string[] | null;
}

// GET /api/salas/tipos-especiales -> IRespuesta<ITipoPiezaEspecial[]> (requiere sesión).
export interface ITipoPiezaEspecial {
  tipoPiezaId: number;
  codigo: string;
  nombre: string;
  eliminaColumna: boolean;
  eliminaFila: boolean;
}

export type EfectoEspecial =
  | 'Ninguno'
  | 'EliminarFila'
  | 'EliminarColumna'
  | 'Confusion'
  | 'Acelerador'
  | 'Bomba'
  | 'Barredora'
  | 'DoblePuntuacion';

export interface IEfectoTemporal {
  usuarioId: number;
  atacanteId: number;
  tipo: EfectoEspecial;
  inicioUtc: string;
  finUtc: string;
  milisegundosRestantes: number;
  activo: boolean;
  motivoFin: 'JugadorEliminado' | 'PartidaTerminada' | 'TiempoAgotado' | null;
  factorVelocidad: number;
}

export interface IActivacionEspecial {
  // Barredora usa codigo BARREDORA, tipo Barredora y estado Aplicado.
  // celdasEliminadas contiene las coordenadas de la mitad superior que estaban ocupadas.
  secuencia: number;
  codigo: string;
  atacanteId: number;
  afectadoId: number | null;
  ahoraServidorUtc: string;
  puntosJugada: number;
  celdasEliminadas: ICelda[];
  // PendienteDefinicion se conserva para compatibilidad; Barredora ya está implementada.
  estado: 'Aplicado' | 'SinObjetivo' | 'PendienteDefinicion';
  tipo: EfectoEspecial;
}

export type PiezaNormal = 'I' | 'O' | 'T' | 'S' | 'Z' | 'J' | 'L';
export type VelocidadInicial = 'Lenta' | 'Normal' | 'Rapida';
export type SonidoJuego = 'movimiento' | 'rotacion' | 'caida' | 'bloqueo' | 'lineas' | 'especial' | 'derrota';

// Puntos base por 1, 2, 3 y 4 líneas (enteros de 0 a 10000); se multiplican por el nivel.
export interface IPuntosLineas {
  unaLinea: number;
  dosLineas: number;
  tresLineas: number;
  cuatroLineas: number;
}

export interface ICrearSala {
  modo: ModoJuego;
  maximoJugadores: number;
  // true: la sala no aparece en el listado y se entra con codigoPrivado.
  privada?: boolean;
  piezasEspeciales?: IConfiguracionPiezas;
  // Sin enviar: las siete. Si se envía, al menos una.
  piezasNormales?: PiezaNormal[];
  // Sin enviar: 'Normal'.
  velocidad?: VelocidadInicial;
  // Sin enviar: 100, 300, 500 y 800.
  puntos?: IPuntosLineas;
  metaPuntos?: number | null;
  minimoVictoriasCorona?: number | null;
}

// Cuerpo de PUT /api/salas/{id}/configuracion (solo el creador, sala en espera).
export interface IConfiguracionSala {
  piezasEspeciales?: IConfiguracionPiezas;
  piezasNormales?: PiezaNormal[];
  velocidad?: VelocidadInicial;
  puntos?: IPuntosLineas;
  metaPuntos?: number | null;
  minimoVictoriasCorona?: number | null;
}

// Qué ocurrió en la última acción de un tablero. Reproduce el sonido solo si secuencia es mayor a la última vista.
export interface IEventoTablero {
  secuencia: number;
  sonidos: SonidoJuego[];
  lineasEliminadas: number;
  piezaEspecial: string | null;
}

export interface IVotoPausa {
  aceptar: boolean;
}

export interface IMovimiento {
  movimiento: Movimiento;
  secuencia: number;
}

export interface ICelda {
  fila: number;
  columna: number;
}

export interface IPiezaSiguiente {
  tipoPieza: number;
  celdas: ICelda[];
  piezaEspecial: string | null;
}

// Evento privado VistaPreviaActualizada; nunca forma parte de ISala o ITablero.
export interface IVistaPreviaPrivada {
  salaId: string;
  usuarioId: number;
  numeroRonda: number;
  version: number;
  pieza: IPiezaSiguiente | null;
}

export interface ITablero {
  fila?: number;
  columna?: number;
  rotacion?: number;
  piezaSecuencia?: number;
  usuarioId: number | null;
  celdas: number[][];
  tipoPieza: number;
  pieza: ICelda[];
  puntuacion: number;
  lineas: number;
  nivel: number;
  perdido: boolean;
  // Código del tipo especial de la pieza activa (por ejemplo "COL"), o null si es normal.
  piezaEspecial: string | null;
  // null mientras el tablero no haya tenido acciones.
  evento: IEventoTablero | null;
}

export interface IPuntuacion {
  usuarioId: number;
  nombre: string;
  puntos: number;
  lineas: number;
  nivel: number;
  perdido: boolean;
  listo: boolean;
  conectado: boolean;
  ultimaSecuencia: number;
  posicion: number | null;
  abandono: boolean;
  reconectarHastaUtc: string | null;
  victoriasSala: number;
  // Lidera las victorias de la sala; no implica haber ganado la última ronda.
  corona: boolean;
  ganoRonda: boolean;
}

export type FasePausa = 'Ninguna' | 'Votando' | 'Pausada';

// Las horas son del servidor. Calcula la cuenta regresiva con vencimientoVotacionUtc o finPausaUtc
// y la diferencia entre ahoraServidorUtc y la hora de tu dispositivo; no decidas tú cuándo termina.
export interface IPausa {
  fase: FasePausa;
  solicitanteId: number | null;
  solicitanteNombre: string | null;
  habilitados: number;
  votosNecesarios: number;
  votosAFavor: number[];
  votosEnContra: number[];
  // Jugadores que ya consumieron su única solicitud de pausa.
  solicitudesUsadasPor: number[];
  vencimientoVotacionUtc: string | null;
  inicioPausaUtc: string | null;
  // null en la pausa individual, que no tiene límite de tiempo.
  finPausaUtc: string | null;
  segundosRestantes: number;
  ahoraServidorUtc: string;
  detalle: string | null;
}

// Nombres de los eventos de SignalR relacionados con la pausa. Todos reciben un IPausa.
export type EventoPausa =
  'PausaSolicitada' | 'VotoPausaRegistrado' | 'PausaRechazada' | 'PausaIniciada' | 'PausaFinalizada';

export interface ISalaPublica {
  id: string;
  codigo: string;
  modo: ModoJuego;
  creadorNombre: string;
  jugadores: number;
  maximoJugadores: number;
  piezasEspecialesActivas: boolean;
}

export interface ISala {
  progresoCompartido?: {
    filasNormales: number;
    nivel: number;
    progresoSiguienteNivel: number;
    intervaloCaidaMs: number;
  } | null;
  numeroRonda: number;
  // Llegan también en EstadoActualizado. Los controles se envían sin invertir desde Ionic.
  efectos: IEfectoTemporal[];
  ultimoEfecto: IActivacionEspecial | null;
  id: string;
  codigo: string;
  creadorId: number;
  modo: ModoJuego;
  estado: EstadoSala;
  maximoJugadores: number;
  version: number;
  revisionPersistencia: number;
  usuarioTurno: number | null;
  jugadores: IPuntuacion[];
  tableros: ITablero[];
  ganadores: number[];
  fechaInicio: string | null;
  fechaFin: string | null;
  privada: boolean;
  // Código de 6 caracteres para invitar a una sala privada; null en las salas públicas.
  codigoPrivado: string | null;
  piezasEspeciales: IConfiguracionPiezas;
  pausa: IPausa;
  piezasNormales: PiezaNormal[];
  velocidad: VelocidadInicial;
  puntos: IPuntosLineas;
  metaPuntos: number | null;
  minimoVictoriasCorona: number | null;
  motivoFin: string | null;
}

export interface IConfiguracion {
  configuracionId: number;
  usuarioId: number;
  mostrarControlesPantalla: boolean;
  permitirPausa: boolean;
  sonidoActivo: boolean;
}

export interface IResultado {
  resultadoId: number;
  partidaId: number;
  usuarioId: number;
  puntuacion: number;
  lineasEliminadas: number;
  nivelMaximo: number;
  posicionFinal: number;
  estadoFinal: string;
  esGanador: boolean;
  fechaResultado: string;
}

export interface IRanking {
  usuarioId: number;
  puntuacionMaxima: number;
  partidasJugadas: number;
  partidasGanadas: number;
}

// El estado completo y estos eventos usan el mismo salaId.
export interface ISalaCerrada {
  salaId: string;
  motivo: string;
}

export interface IJugadorAbandono {
  salaId: string;
  usuarioId: number;
}
