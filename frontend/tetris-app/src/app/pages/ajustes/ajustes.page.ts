// Presenta los ajustes locales de música y efectos que administra ServicioAudio.
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { IonContent } from '@ionic/angular';
import { ServicioAudio } from '../../servicios/audio.servicio';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink, IonContent],
  template: `
    <ion-content>
      <main class="pantalla-tetris">
        <a class="accion secundaria" routerLink="/home">← Volver al menú</a>
        <h1>Ajustes de sonido</h1>
        <section class="tarjeta-tetris formulario-tetris">
          <label
            ><input
              type="checkbox"
              role="switch"
              [ngModel]="audio.preferencias().musicaActiva"
              (ngModelChange)="audio.ajustar({ musicaActiva: $event })"
            />
            Música</label
          >
          <label
            >Volumen de música: {{ audio.preferencias().volumenMusica }} %
            <input
              type="range"
              min="0"
              max="100"
              [ngModel]="audio.preferencias().volumenMusica"
              (ngModelChange)="audio.ajustar({ volumenMusica: +$event })"
            />
          </label>
          <label
            ><input
              type="checkbox"
              role="switch"
              [ngModel]="audio.preferencias().efectosActivos"
              (ngModelChange)="audio.ajustar({ efectosActivos: $event })"
            />
            Efectos de sonido</label
          >
          <label
            >Volumen de efectos: {{ audio.preferencias().volumenEfectos }} %
            <input
              type="range"
              min="0"
              max="100"
              [ngModel]="audio.preferencias().volumenEfectos"
              (ngModelChange)="audio.ajustar({ volumenEfectos: +$event })"
            />
          </label>
          <button class="accion" data-sin-clic (click)="probar()">Probar sonido</button>
          <p>El porcentaje controla la ganancia del juego; no cambia el volumen físico del dispositivo.</p>
          <p>Los ajustes se guardan automáticamente para tu jugador en este dispositivo.</p>
        </section>
      </main>
    </ion-content>
  `,
})
export class AjustesPage {
  readonly audio = inject(ServicioAudio);

  probar(): void {
    void this.audio.probarSonido();
  }
}
