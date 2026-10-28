import { Component, Input } from '@angular/core';
import { IPiezaSiguiente } from '../modelos/tetris.model';
import { coloresPiezas } from '../modelos/colores-piezas';

@Component({
    selector: 'app-siguiente-pieza',
    standalone: true,
    template: `
        <section class="siguiente-pieza" aria-label="Siguiente pieza">
            <h2>Siguiente pieza</h2>
            @if (pieza && !espectador) {
                <div class="cuadricula-siguiente" role="img" [attr.aria-label]="descripcion">
                    @for (posicion of posiciones; track posicion) {
                        <span class="celda-siguiente" [class.ocupada]="ocupada(posicion)"
                            [style.background-color]="ocupada(posicion) ? colores[pieza.tipoPieza + 1] : colores[0]">
                            @if (ocupada(posicion) && pieza.piezaEspecial) { <span aria-hidden="true">★</span> }
                        </span>
                    }
                </div>
                @if (pieza.piezaEspecial) { <p class="especial-activa">{{ nombreEspecial }}</p> }
            } @else {
                <p>{{ espectador ? 'Estás como espectador.' : 'Esperando tu próxima pieza…' }}</p>
            }
        </section>
    `,
    styles: [`
        :host { display: block; }
        .siguiente-pieza { border-bottom: 1px solid #ffffff22; padding-bottom: 10px; margin-bottom: 12px; }
        .cuadricula-siguiente { display: grid; grid-template-columns: repeat(4, 1fr); gap: 2px; width: 104px; height: 104px; padding: 4px; border: 1px solid #a782ff; border-radius: 8px; background: #090f20; }
        .celda-siguiente { border-radius: 2px; display: grid; place-items: center; color: #14101f; font-size: 16px; }
        .celda-siguiente.ocupada { box-shadow: inset 0 0 0 2px #ffffff44; }
        .siguiente-pieza p { margin: 6px 0 0; }
        @media (max-width: 650px) {
            .siguiente-pieza { display: grid; grid-template-columns: 1fr 72px; align-items: center; gap: 6px; padding-bottom: 6px; margin-bottom: 8px; }
            .cuadricula-siguiente { width: 72px; height: 72px; grid-column: 2; grid-row: 1 / span 2; }
            .siguiente-pieza h2 { grid-column: 1; }
            .siguiente-pieza p { grid-column: 1; }
        }
    `]
})
export class SiguientePiezaComponent {
    @Input() pieza: IPiezaSiguiente | null = null;
    @Input() espectador = false;
    @Input() nombreEspecial = 'Pieza especial';
    readonly posiciones = Array.from({ length: 16 }, (_, posicion) => posicion);
    readonly colores = coloresPiezas;

    get descripcion(): string {
        const formas = ['I', 'O', 'T', 'S', 'Z', 'J', 'L'];
        return `Siguiente pieza ${formas[this.pieza?.tipoPieza ?? 0]}${this.pieza?.piezaEspecial ? ', ' + this.nombreEspecial : ''}`;
    }

    ocupada(posicion: number): boolean {
        return !!this.pieza?.celdas.some(celda => celda.fila === Math.floor(posicion / 4) && celda.columna === posicion % 4);
    }
}
