import { Component, EventEmitter, HostListener, Input, OnChanges, OnDestroy, Output } from '@angular/core';
import { Movimiento } from '../modelos/tetris.model';

@Component({
    selector: 'app-controles',
    standalone: true,
    template: `
        <div class="controles-tetris" data-sin-clic aria-label="Controles del tablero">
            @for (boton of botones; track boton.movimiento) {
                <button type="button" class="control-tetris" [attr.aria-label]="boton.nombre" [disabled]="!habilitado"
                    (pointerdown)="presionar($event, boton.movimiento)" (pointerup)="soltar()" (pointercancel)="soltar()"
                    (lostpointercapture)="soltar()" (click)="clic($event, boton.movimiento)" (contextmenu)="$event.preventDefault()">
                    <span aria-hidden="true">{{ boton.simbolo }}</span><small>{{ boton.nombre }}</small>
                </button>
            }
        </div>
    `
})
export class ControlesComponent implements OnDestroy, OnChanges {
    @Input() habilitado = false;
    @Output() movimiento = new EventEmitter<Movimiento>();
    readonly botones: { movimiento: Movimiento; nombre: string; simbolo: string }[] = [
        { movimiento: 'Izquierda', nombre: 'Izquierda', simbolo: '◀' },
        { movimiento: 'Rotar', nombre: 'Rotar', simbolo: '↻' },
        { movimiento: 'Derecha', nombre: 'Derecha', simbolo: '▶' },
        { movimiento: 'Bajar', nombre: 'Bajar', simbolo: '▼' },
        { movimiento: 'Caer', nombre: 'Caída rápida', simbolo: '⤓' }
    ];
    private espera?: ReturnType<typeof setTimeout>;
    private repeticion?: ReturnType<typeof setInterval>;

    presionar(evento: PointerEvent, movimiento: Movimiento): void {
        evento.preventDefault();
        this.soltar();
        if (!this.habilitado) {
            return;
        }
        (evento.currentTarget as HTMLElement).setPointerCapture?.(evento.pointerId);
        this.movimiento.emit(movimiento);
        if (['Izquierda', 'Derecha', 'Bajar'].includes(movimiento)) {
            this.espera = setTimeout(() => {
                this.repeticion = setInterval(() => {
                    if (this.habilitado) {
                        this.movimiento.emit(movimiento);
                    }
                }, 100);
            }, 230);
        }
    }

    clic(evento: MouseEvent, movimiento: Movimiento): void {
        if (evento.detail === 0 && this.habilitado) {
            this.movimiento.emit(movimiento);
        }
    }

    @HostListener('window:blur')
    @HostListener('window:pointerup')
    @HostListener('window:pointercancel')
    @HostListener('document:visibilitychange')
    soltar(): void {
        clearTimeout(this.espera);
        clearInterval(this.repeticion);
    }

    ngOnChanges(): void {
        if (!this.habilitado) {
            this.soltar();
        }
    }

    ngOnDestroy(): void {
        this.soltar();
    }
}
