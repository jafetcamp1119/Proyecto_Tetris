// Presenta las opciones de piezas especiales a partir del catálogo disponible.
import { Component, Input } from '@angular/core';
import { IConfiguracionPiezas, ITipoPiezaEspecial } from '../modelos/tetris.model';
import { descripcionPieza, efectoDePieza, nombresEfectos } from '../modelos/presentacion-juego';

@Component({
  selector: 'app-piezas-especiales',
  standalone: true,
  template: `
    <div class="tarjetas-especiales">
      @for (tipo of catalogo; track tipo.codigo) {
        @if (editable || seleccionada(tipo)) {
          <label class="tarjeta-especial" [class.seleccionada]="seleccionada(tipo)">
            @if (editable) {
              <input
                type="checkbox"
                [checked]="seleccionada(tipo)"
                (change)="seleccionar(tipo, $event)"
                [attr.aria-label]="nombre(tipo)"
              />
            }
            <span
              ><strong>{{ nombre(tipo) }}</strong
              ><span class="descripcion-especial">{{ descripcion(tipo) }}</span></span
            >
            <small>{{ seleccionada(tipo) ? 'Activada' : 'Desactivada' }}</small>
          </label>
        }
      } @empty {
        <p>No pudimos cargar las piezas especiales. Intentá de nuevo.</p>
      }
    </div>
    @if (editable && configuracion.tiposPermitidos?.length === 0) {
      <p class="mensaje-error" role="status">Elegí al menos una pieza especial o desactivá el interruptor.</p>
    }
  `,
})
export class PiezasEspecialesComponent {
  @Input({ required: true }) configuracion!: IConfiguracionPiezas;
  @Input() catalogo: ITipoPiezaEspecial[] = [];
  @Input() editable = true;
  readonly descripcion = descripcionPieza;

  nombre(tipo: ITipoPiezaEspecial): string {
    const efecto = efectoDePieza(tipo);
    return efecto === 'Ninguno' ? tipo.nombre : nombresEfectos[efecto];
  }

  seleccionada(tipo: ITipoPiezaEspecial): boolean {
    return this.configuracion.tiposPermitidos == null || this.configuracion.tiposPermitidos.includes(tipo.codigo);
  }

  seleccionar(tipo: ITipoPiezaEspecial, evento: Event): void {
    if (!this.editable) {
      return;
    }

    const seleccion = this.configuracion.tiposPermitidos ?? this.catalogo.map((pieza) => pieza.codigo);
    const marcada = (evento.target as HTMLInputElement).checked;
    this.configuracion.tiposPermitidos = marcada
      ? [...new Set([...seleccion, tipo.codigo])]
      : seleccion.filter((codigo) => codigo !== tipo.codigo);
  }
}
