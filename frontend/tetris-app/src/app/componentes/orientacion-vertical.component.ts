// Muestra el aviso de orientación cuando el servicio detecta un móvil horizontal.
import { Component, inject } from '@angular/core';
import { OrientacionServicio } from '../servicios/orientacion.servicio';

@Component({
  selector: 'app-orientacion-vertical',
  standalone: true,
  template: `
    @if (orientacion.requiereVertical()) {
      <section class="aviso-orientacion" role="alert" aria-live="assertive">
        <div>
          <h1>📱 Girá tu celular para continuar jugando</h1>
          <p>Volvé a la posición vertical. Girar el celular no pausa la partida.</p>
        </div>
      </section>
    }
  `,
  styles: [
    `
      .aviso-orientacion {
        position: fixed;
        inset: 0;
        z-index: 100000;
        display: grid;
        place-items: center;
        text-align: center;
        padding: max(24px, env(safe-area-inset-top)) max(24px, env(safe-area-inset-right))
          max(24px, env(safe-area-inset-bottom)) max(24px, env(safe-area-inset-left));
        background: #090e1c;
        color: #f4f6ff;
        touch-action: none;
        overscroll-behavior: contain;
      }
      h1 {
        max-width: 640px;
        font-size: clamp(22px, 4vw, 32px);
        line-height: 1.4;
        color: #bda1ff;
      }
      p {
        max-width: 640px;
        font-size: 16px;
        line-height: 1.6;
      }
    `,
  ],
})
export class OrientacionVerticalComponent {
  readonly orientacion = inject(OrientacionServicio);
}
