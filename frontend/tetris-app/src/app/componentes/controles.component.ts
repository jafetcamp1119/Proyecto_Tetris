import { Component, EventEmitter, HostListener, effect, inject, Input, OnChanges, OnDestroy, Output } from '@angular/core';
import { OrientacionServicio } from '../servicios/orientacion.servicio';
import { Movimiento } from '../modelos/tetris.model';
import { RepetidorEntrada } from '../servicios/repetidor-entrada';
import { RendimientoServicio } from '../servicios/rendimiento.servicio';

@Component({
    selector: 'app-controles',
    standalone: true,
    template: `
        <div class="controles-tetris" data-sin-clic aria-label="Controles del tablero">
            @for (boton of botones; track boton.movimiento) {
                <button type="button" class="control-tetris" [attr.aria-label]="boton.nombre" [disabled]="!puedeInteractuar()"
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
    private readonly orientacion = inject(OrientacionServicio);
    private readonly rendimiento = inject(RendimientoServicio);
    private readonly entrada = new RepetidorEntrada(movimiento => this.movimiento.emit(movimiento), () => this.puedeInteractuar());

    constructor() {
        effect(() => {
            if (this.orientacion.requiereVertical()) {
                this.soltar();
            }
        });
    }

    puedeInteractuar(): boolean {
        return this.habilitado && !this.orientacion.requiereVertical();
    }

    presionar(evento: PointerEvent, movimiento: Movimiento): void {
        this.rendimiento.registrarEvento(evento.timeStamp);
        evento.preventDefault();
        this.soltar();
        if (!this.puedeInteractuar()) {
            return;
        }
        (evento.currentTarget as HTMLElement).setPointerCapture?.(evento.pointerId);
        this.entrada.presionar(movimiento);
    }

    clic(evento: MouseEvent, movimiento: Movimiento): void {
        if (evento.detail === 0 && this.puedeInteractuar()) {
            this.movimiento.emit(movimiento);
        }
    }

    @HostListener('window:blur')
    @HostListener('window:pointerup')
    @HostListener('window:pointercancel')
    @HostListener('document:visibilitychange')
    soltar(): void {
        this.entrada.soltar();
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
