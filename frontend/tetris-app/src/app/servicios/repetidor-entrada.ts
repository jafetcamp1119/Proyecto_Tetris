// Repite izquierda, derecha y bajada mientras se mantiene una entrada.
// La rotación y la caída rápida se emiten una sola vez por pulsación.
import { Movimiento } from '../modelos/tetris.model';

export class RepetidorEntrada {
  private espera?: ReturnType<typeof setTimeout>;
  private repeticion?: ReturnType<typeof setInterval>;
  private movimiento?: Movimiento;

  constructor(
    private emitir: (movimiento: Movimiento) => void,
    private habilitado: () => boolean,
  ) {}

  // Emite la entrada inicial y, si admite repetición, espera 230 ms antes de repetir cada 100 ms.
  presionar(movimiento: Movimiento): void {
    this.soltar();

    if (!this.habilitado()) {
      return;
    }

    this.movimiento = movimiento;
    this.emitir(movimiento);

    if (!['Izquierda', 'Derecha', 'Bajar'].includes(movimiento)) {
      return;
    }

    this.espera = setTimeout(() => {
      this.repetir();

      if (this.movimiento) {
        this.repeticion = setInterval(() => this.repetir(), 100);
      }
    }, 230);
  }

  // Cancela espera y repetición; una liberación de otra tecla no corta la entrada actual.
  soltar(movimiento?: Movimiento): void {
    if (movimiento && movimiento !== this.movimiento) {
      return;
    }

    clearTimeout(this.espera);
    clearInterval(this.repeticion);
    this.movimiento = undefined;
  }

  // Vuelve a comprobar el permiso de mover antes de emitir otra entrada.
  private repetir(): void {
    if (!this.habilitado()) {
      this.soltar();
      return;
    }

    if (this.movimiento) {
      this.emitir(this.movimiento);
    }
  }
}
