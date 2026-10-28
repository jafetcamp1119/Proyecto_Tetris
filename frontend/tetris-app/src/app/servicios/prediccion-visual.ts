import { IMovimiento, ISala, ITablero } from '../modelos/tetris.model';

// Solo desplaza la pieza visible; nunca fija bloques, suma puntos ni ejecuta especiales.
export class PrediccionVisual {
  private pendientes: IMovimiento[] = [];
  private contexto = '';
  // Detiene nuevas predicciones cuando una entrada puede haber fijado la pieza y falta confirmación.
  private barrera = 0;

  // Descarta las entradas pendientes y la referencia de la pieza anterior.
  limpiar(): void {
    this.pendientes = [];
    this.contexto = '';
    this.barrera = 0;
  }

  get esperandoFijacion(): boolean {
    return this.barrera > 0;
  }

  // Parte del tablero confirmado y vuelve a aplicar entradas todavía pendientes.
  // Se reinicia al cambiar sala, ronda, turno o pieza; no calcula puntos ni fija bloques.
  actualizar(sala: ISala, usuarioId: number, comando?: IMovimiento): ITablero | null {
    const tablero =
      sala.modo === 'Compartido' ? sala.tableros?.[0] : sala.tableros?.find((t) => t.usuarioId === usuarioId);
    const jugador = sala.jugadores.find((j) => j.usuarioId === usuarioId);
    const puede =
      sala.estado === 'EnCurso' &&
      sala.pausa.fase !== 'Pausada' &&
      jugador &&
      !jugador.perdido &&
      !jugador.abandono &&
      (sala.modo !== 'Compartido' || sala.usuarioTurno === usuarioId);

    if (
      !puede ||
      !tablero?.piezaSecuencia ||
      sala.efectos?.some((e) => e.usuarioId === usuarioId && e.activo && e.tipo === 'Confusion')
    ) {
      this.limpiar();
      return null;
    }

    const contexto = `${sala.id}:${sala.numeroRonda}:${sala.usuarioTurno}:${tablero.piezaSecuencia}`;

    if (contexto !== this.contexto) {
      this.limpiar();
      this.contexto = contexto;
    }

    const confirmada = jugador.ultimaSecuencia;
    this.pendientes = this.pendientes.filter((p) => p.secuencia > confirmada);

    if (confirmada >= this.barrera) {
      this.barrera = 0;
    }

    if (comando && !this.barrera && this.pendientes.length < 8) {
      if (comando.movimiento === 'Caer') {
        this.barrera = comando.secuencia;
      } else {
        this.pendientes.push(comando);
      }
    }

    let vista = { ...tablero, pieza: tablero.pieza.map((c) => ({ ...c })) };

    for (const pendiente of this.pendientes) {
      const propuesta = this.proponer(vista, pendiente.movimiento);

      if (propuesta) {
        vista = propuesta;
      } else if (pendiente.movimiento === 'Bajar') {
        this.barrera = pendiente.secuencia;
        break;
      }
    }

    return vista;
  }

  // Descarta la entrada rechazada y las posteriores para evitar continuar una vista inválida.
  rechazar(secuencia: number): void {
    this.pendientes = this.pendientes.filter((p) => p.secuencia < secuencia);
    this.barrera = 0;
  }

  // Calcula una traslación o rotación visible y devuelve null si no cabe.
  private proponer(tablero: ITablero, movimiento: IMovimiento['movimiento']): ITablero | null {
    const origenFila = tablero.fila ?? 0;
    const origenColumna = tablero.columna ?? 3;
    const desplazamiento = movimiento === 'Izquierda' ? -1 : movimiento === 'Derecha' ? 1 : 0;
    const pieza = tablero.pieza.map((c) => {
      if (movimiento === 'Rotar' && tablero.tipoPieza !== 1) {
        return { fila: origenFila + c.columna - origenColumna, columna: origenColumna + 3 - (c.fila - origenFila) };
      }

      return { fila: c.fila + (movimiento === 'Bajar' ? 1 : 0), columna: c.columna + desplazamiento };
    });

    if (
      pieza.some(
        (c) =>
          c.columna < 0 ||
          c.columna >= tablero.celdas[0].length ||
          c.fila >= tablero.celdas.length ||
          (c.fila >= 0 && tablero.celdas[c.fila][c.columna] !== 0),
      )
    ) {
      return null;
    }

    return {
      ...tablero,
      pieza,
      fila: origenFila + (movimiento === 'Bajar' ? 1 : 0),
      columna: origenColumna + desplazamiento,
      rotacion: movimiento === 'Rotar' ? ((tablero.rotacion ?? 0) + 1) % 4 : tablero.rotacion,
    };
  }
}
