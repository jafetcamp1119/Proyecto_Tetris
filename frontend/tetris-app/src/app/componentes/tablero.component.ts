// Dibuja bloques fijos y pieza activa en canvas a partir del tablero recibido.
// Agrupa cambios en el siguiente cuadro de animación y evita dibujar una vista idéntica.
import { AfterViewInit, Component, ElementRef, inject, Input, OnChanges, OnDestroy, ViewChild } from '@angular/core';
import { ITablero } from '../modelos/tetris.model';
import { coloresPiezas } from '../modelos/colores-piezas';
import { RendimientoServicio } from '../servicios/rendimiento.servicio';

@Component({
  selector: 'app-tablero',
  standalone: true,
  template: `
    <div class="lienzo-contenedor">
      <canvas
        #lienzo
        class="tablero-tetris"
        [class.destello-lineas]="tablero.evento?.lineasEliminadas"
        role="img"
        [attr.aria-label]="'Tablero de Tetris. Puntuación: ' + tablero.puntuacion + '. Nivel: ' + tablero.nivel"
      ></canvas>
    </div>
    @if (tablero.piezaEspecial) {
      <p class="especial-activa">{{ nombreEspecial }}</p>
    }
  `,
})
export class TableroComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) tablero!: ITablero;
  @Input() nombreEspecial = 'Pieza especial';
  @ViewChild('lienzo') lienzo?: ElementRef<HTMLCanvasElement>;
  private readonly colores = coloresPiezas;
  private readonly rendimiento = inject(RendimientoServicio);
  private cuadro?: number;
  private ultimaRepresentacion = '';

  // Solicita el primer dibujo una vez que el canvas ya existe.
  ngAfterViewInit(): void {
    this.programarDibujo();
  }

  // Solicita un dibujo cuando Angular entrega un tablero distinto.
  ngOnChanges(): void {
    this.programarDibujo();
  }

  // Cancela el cuadro de animación pendiente al retirar el componente.
  ngOnDestroy(): void {
    if (this.cuadro !== undefined) {
      cancelAnimationFrame(this.cuadro);
    }
  }

  // Agrupa las actualizaciones que ocurren antes del próximo cuadro de animación.
  private programarDibujo(): void {
    if (this.cuadro !== undefined || !this.lienzo) {
      return;
    }

    this.cuadro = requestAnimationFrame(() => {
      this.cuadro = undefined;
      this.dibujar();
    });
  }

  // Compara la representación anterior y dibuja celdas y pieza activa solo cuando cambian.
  private dibujar(): void {
    const lienzo = this.lienzo?.nativeElement;
    const contexto = lienzo?.getContext('2d');

    if (!lienzo || !contexto || !this.tablero) {
      return;
    }

    const representacion = JSON.stringify([
      this.tablero.celdas,
      this.tablero.pieza,
      this.tablero.tipoPieza,
      this.tablero.perdido,
      this.tablero.piezaEspecial,
    ]);

    if (representacion === this.ultimaRepresentacion) {
      return;
    }

    this.ultimaRepresentacion = representacion;
    const inicio = this.rendimiento.iniciar();
    const medida = 30;
    const ancho = (this.tablero.celdas[0]?.length || 10) * medida;
    const alto = this.tablero.celdas.length * medida;

    if (lienzo.width !== ancho) {
      lienzo.width = ancho;
    }

    if (lienzo.height !== alto) {
      lienzo.height = alto;
    }

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
    this.tablero.celdas.forEach((fila, indiceFila) =>
      fila.forEach((tipo, columna) => celda(indiceFila, columna, tipo)),
    );

    if (!this.tablero.perdido) {
      this.tablero.pieza.forEach((pieza) => celda(pieza.fila, pieza.columna, this.tablero.tipoPieza + 1, true));
    }

    this.rendimiento.finalizar('tablero_dibujo_ms', inicio);
    this.rendimiento.marcarDibujo();
  }
}
