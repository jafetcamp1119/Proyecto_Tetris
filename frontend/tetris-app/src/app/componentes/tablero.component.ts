import { AfterViewInit, Component, ElementRef, Input, OnChanges, ViewChild } from '@angular/core';
import { ITablero } from '../modelos/tetris.model';

@Component({
    selector: 'app-tablero',
    standalone: true,
    template: `
        <canvas #lienzo class="tablero-tetris" [class.destello-lineas]="tablero.evento?.lineasEliminadas" role="img"
            [attr.aria-label]="'Tablero de Tetris. Puntuación: ' + tablero.puntuacion + '. Nivel: ' + tablero.nivel"></canvas>
        @if (tablero.piezaEspecial) {
            <p class="especial-activa">{{ nombreEspecial }}</p>
        }
    `
})
export class TableroComponent implements AfterViewInit, OnChanges {
    @Input({ required: true }) tablero!: ITablero;
    @Input() nombreEspecial = 'Pieza especial';
    @ViewChild('lienzo') lienzo?: ElementRef<HTMLCanvasElement>;
    private readonly colores = ['#111a2c', '#26d9e8', '#f5d547', '#ab79fb', '#47d68c', '#fa637b', '#618aff', '#ffa44f'];

    ngAfterViewInit(): void {
        this.dibujar();
    }

    ngOnChanges(): void {
        this.dibujar();
    }

    private dibujar(): void {
        const lienzo = this.lienzo?.nativeElement;
        const contexto = lienzo?.getContext('2d');
        if (!lienzo || !contexto || !this.tablero) {
            return;
        }
        const medida = 30;
        lienzo.width = (this.tablero.celdas[0]?.length || 10) * medida;
        lienzo.height = this.tablero.celdas.length * medida;
        const celda = (fila: number, columna: number, tipo: number, activa = false): void => {
            if (fila < 0) {
                return;
            }
            contexto.fillStyle = this.colores[Math.abs(tipo) % this.colores.length];
            contexto.fillRect(columna * medida + 1, fila * medida + 1, medida - 2, medida - 2);
            if (tipo || activa) {
                contexto.strokeStyle = activa ? '#ffffffbb' : '#ffffff33';
                contexto.strokeRect(columna * medida + 3, fila * medida + 3, medida - 6, medida - 6);
            }
            if (activa && this.tablero.piezaEspecial) {
                contexto.fillStyle = '#14101f';
                contexto.font = 'bold 20px Arial';
                contexto.fillText('★', columna * medida + 5, fila * medida + 22);
            }
        };
        this.tablero.celdas.forEach((fila, indiceFila) => fila.forEach((tipo, columna) => celda(indiceFila, columna, tipo)));
        if (!this.tablero.perdido) {
            this.tablero.pieza.forEach(pieza => celda(pieza.fila, pieza.columna, this.tablero.tipoPieza + 1, true));
        }
    }
}
