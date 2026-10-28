// Presenta y valida las opciones de la sala antes de enviarlas al servidor.
import { Component, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ICrearSala, ITipoPiezaEspecial, PiezaNormal } from '../modelos/tetris.model';
import { PiezasEspecialesComponent } from './piezas-especiales.component';

// Prepara las opciones iniciales del formulario sin crear todavía una sala en el servidor.
export function configuracionInicial(): ICrearSala {
  return {
    modo: 'Compartido',
    maximoJugadores: 4,
    privada: false,
    piezasNormales: ['I', 'O', 'T', 'S', 'Z', 'J', 'L'],
    velocidad: 'Normal',
    puntos: { unaLinea: 100, dosLineas: 300, tresLineas: 500, cuatroLineas: 800 },
    piezasEspeciales: { activas: false, probabilidad: 10 },
    metaPuntos: null,
    minimoVictoriasCorona: null,
  };
}

@Component({
  selector: 'app-configuracion-sala',
  standalone: true,
  imports: [FormsModule, PiezasEspecialesComponent],
  template: `
    <div class="formulario-tetris">
      @if (creacion && datos.modo !== 'Individual') {
        <label
          >Cantidad de jugadores
          <select [(ngModel)]="datos.maximoJugadores">
            <option [ngValue]="2">2 jugadores</option>
            <option [ngValue]="3">3 jugadores</option>
            <option [ngValue]="4">4 jugadores</option>
          </select>
        </label>
        <label><input type="checkbox" [(ngModel)]="datos.privada" /> Sala privada</label>
      }
      <label
        >Velocidad inicial
        <select [(ngModel)]="datos.velocidad"
          ><option value="Lenta">Lenta</option
          ><option value="Normal">Normal</option
          ><option value="Rapida">Rápida</option></select
        >
      </label>
      <fieldset>
        <legend>Piezas normales</legend>
        @for (pieza of piezas; track pieza) {
          <label class="opcion-pieza"
            ><input
              type="checkbox"
              [checked]="datos.piezasNormales?.includes(pieza)"
              (change)="seleccionar(pieza, $event)"
            />{{ pieza }}</label
          >
        }
      </fieldset>
      @if (datos.puntos; as puntos) {
        <fieldset
          ><legend>Puntos por líneas (antes del multiplicador de nivel)</legend>
          <label>Una<input type="number" min="0" max="10000" [(ngModel)]="puntos.unaLinea" /></label>
          <label>Dos<input type="number" min="0" max="10000" [(ngModel)]="puntos.dosLineas" /></label>
          <label>Tres<input type="number" min="0" max="10000" [(ngModel)]="puntos.tresLineas" /></label>
          <label>Cuatro<input type="number" min="0" max="10000" [(ngModel)]="puntos.cuatroLineas" /></label>
        </fieldset>
      }
      @if (datos.modo === 'Compartido') {
        <label>Meta de puntos (opcional)<input type="number" min="1" [(ngModel)]="datos.metaPuntos" /></label>
        @if (datos.piezasEspeciales; as especiales) {
          <h3>Piezas especiales</h3>
          <label class="interruptor"
            ><input type="checkbox" role="switch" [(ngModel)]="especiales.activas" /> Activar piezas especiales</label
          >
          @if (especiales.activas) {
            <label
              >Probabilidad de aparición: {{ especiales.probabilidad }} %
              <input type="range" min="1" max="100" step="1" [(ngModel)]="especiales.probabilidad" />
            </label>
            <app-piezas-especiales [configuracion]="especiales" [catalogo]="catalogo" />
          }
        }
      }
      @if (datos.modo !== 'Individual') {
        <label
          >Mínimo de victorias para corona (opcional)<input
            type="number"
            min="1"
            [(ngModel)]="datos.minimoVictoriasCorona"
        /></label>
      }
    </div>
  `,
})
export class ConfiguracionSalaComponent {
  @Input({ required: true }) datos!: ICrearSala;
  @Input() creacion = true;
  @Input() catalogo: ITipoPiezaEspecial[] = [];
  readonly piezas: PiezaNormal[] = ['I', 'O', 'T', 'S', 'Z', 'J', 'L'];

  // Agrega o retira una forma normal de las opciones del formulario.
  seleccionar(pieza: PiezaNormal, evento: Event): void {
    const marcar = (evento.target as HTMLInputElement).checked;
    this.datos.piezasNormales = marcar
      ? [...(this.datos.piezasNormales || []), pieza]
      : this.datos.piezasNormales?.filter((p) => p !== pieza);
  }
}

// Revisa las opciones visibles antes de enviarlas; el servidor vuelve a comprobar sus reglas.
export function validarConfiguracion(datos: ICrearSala): string | null {
  if (datos.modo !== 'Individual' && ![2, 3, 4].includes(datos.maximoJugadores)) {
    return 'Elegí una sala de dos, tres o cuatro jugadores.';
  }

  if (!datos.piezasNormales?.length) {
    return 'Elegí al menos una pieza normal.';
  }

  if (datos.modo === 'Compartido' && datos.piezasEspeciales?.activas) {
    if (datos.piezasEspeciales.tiposPermitidos?.length === 0) {
      return 'Elegí al menos una pieza especial.';
    }

    if (
      !Number.isInteger(datos.piezasEspeciales.probabilidad) ||
      datos.piezasEspeciales.probabilidad < 1 ||
      datos.piezasEspeciales.probabilidad > 100
    ) {
      return 'Elegí una probabilidad entre 1 % y 100 %.';
    }
  }

  if (
    datos.puntos &&
    Object.values(datos.puntos).some((puntos) => !Number.isInteger(puntos) || puntos < 0 || puntos > 10000)
  ) {
    return 'Elegí puntos enteros entre 0 y 10 000 para cada recompensa.';
  }

  if (
    [datos.metaPuntos, datos.minimoVictoriasCorona].some(
      (valor) => valor != null && (!Number.isInteger(valor) || valor < 1),
    )
  ) {
    return 'La meta de puntos y las victorias deben ser números positivos.';
  }

  return null;
}
