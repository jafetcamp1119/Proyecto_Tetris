import { Movimiento } from '../modelos/tetris.model';

export class RepetidorEntrada {
    private espera?: ReturnType<typeof setTimeout>;
    private repeticion?: ReturnType<typeof setInterval>;
    private movimiento?: Movimiento;

    constructor(private emitir: (movimiento: Movimiento) => void, private habilitado: () => boolean) { }

    presionar(movimiento: Movimiento): void {
        this.soltar();
        if (!this.habilitado()) { return; }
        this.movimiento = movimiento;
        this.emitir(movimiento);
        if (!['Izquierda', 'Derecha', 'Bajar'].includes(movimiento)) { return; }
        this.espera = setTimeout(() => {
            this.repetir();
            if (this.movimiento) { this.repeticion = setInterval(() => this.repetir(), 100); }
        }, 230);
    }

    soltar(movimiento?: Movimiento): void {
        if (movimiento && movimiento !== this.movimiento) { return; }
        clearTimeout(this.espera);
        clearInterval(this.repeticion);
        this.movimiento = undefined;
    }

    private repetir(): void {
        if (!this.habilitado()) { this.soltar(); return; }
        if (this.movimiento) { this.emitir(this.movimiento); }
    }
}
