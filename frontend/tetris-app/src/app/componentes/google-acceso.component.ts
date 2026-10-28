// Presenta el ingreso o vinculación Google y coordina el botón web o el acceso nativo.
import { mensajeParaJugador } from '../servicios/mensajes-jugador';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  inject,
  Input,
  OnDestroy,
  Output,
  ViewChild,
} from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { environment } from '../../environments/environment';
import { GoogleServicio } from '../servicios/google.servicio';
import { ErrorCargaGoogle, GoogleWebServicio } from '../servicios/google-web.servicio';
import { ErrorApi } from '../servicios/api.servicio';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-google-acceso',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (habilitado) {
      @if (!vincular && google.registroPendiente()) {
        <section class="tarjeta-tetris">
          <h3>¡Bienvenido a Tetris!</h3>
          <p>Es tu primera vez jugando con esta cuenta de Google.</p>
          <label for="nombre-google">Elegí tu nombre de jugador:</label>
          <input
            id="nombre-google"
            [(ngModel)]="nickname"
            maxlength="50"
            autocomplete="nickname"
            [disabled]="ocupado"
          />
          <button type="button" class="accion" [disabled]="ocupado || !nickname.trim()" (click)="registrar()"
            >Crear mi cuenta</button
          >
          <button type="button" class="accion secundaria" [disabled]="ocupado" (click)="cancelarRegistro()"
            >Cancelar</button
          >
        </section>
      }
      @if (nativo) {
        <button
          type="button"
          class="accion google"
          [style.display]="!vincular && google.registroPendiente() ? 'none' : null"
          [disabled]="ocupado"
          (click)="android()"
        >
          {{ vincular ? 'Vincular cuenta de Google' : 'Continuar con Google' }}
        </button>
      } @else {
        <div
          #botonGoogle
          class="google-web"
          [style.display]="!vincular && google.registroPendiente() ? 'none' : null"
          [attr.aria-busy]="ocupado"
        ></div>
      }
      <p role="status">{{ mensaje }}</p>
      @if (!nativo && puedeReintentar) {
        <button type="button" class="accion secundaria" [disabled]="ocupado" (click)="reintentar()"
          >Reintentar Google</button
        >
      }
    }
  `,
})
export class GoogleAccesoComponent implements AfterViewInit, OnDestroy {
  @Input() vincular = false;
  @Output() finalizado = new EventEmitter<void>();
  @ViewChild('botonGoogle') boton?: ElementRef<HTMLDivElement>;
  readonly google = inject(GoogleServicio);
  nickname = '';
  private readonly web = inject(GoogleWebServicio);
  private readonly vista = inject(ChangeDetectorRef);
  readonly nativo = Capacitor.isNativePlatform();
  readonly habilitado = environment.google.habilitado;
  ocupado = false;
  puedeReintentar = false;
  mensaje = '';
  private activo = true;
  private renovacion?: ReturnType<typeof setTimeout>;

  async ngAfterViewInit(): Promise<void> {
    if (!this.nativo && this.habilitado) {
      await this.prepararWeb();
    }
  }

  // Vuelve a preparar el botón solo si la pantalla sigue activa y no hay otra operación.
  async reintentar(): Promise<void> {
    if (this.ocupado || !this.activo) {
      return;
    }

    await this.prepararWeb();
  }

  // Obtiene un desafío y prepara el botón Google; renueva el intento antes de vencer.
  private async prepararWeb(): Promise<void> {
    clearTimeout(this.renovacion);
    this.puedeReintentar = false;
    this.ocupado = true;
    this.mensaje = '';
    let etapa = 'servidor';

    try {
      const desafio = await this.google.preparar(this.vincular);
      etapa = 'biblioteca';
      await this.web.cargar();

      if (!this.activo || !this.boton) {
        return;
      }

      this.web.mostrarBoton(this.boton.nativeElement, environment.google.idClienteWeb, desafio.nonce, (token) => {
        if (this.activo && !this.ocupado) {
          void this.ejecutar(() => this.google.completar(desafio, token, this.vincular));
        }
      });
      this.renovacion = setTimeout(() => {
        void this.prepararWeb();
      }, 8 * 60000);
    } catch (error) {
      this.puedeReintentar = true;

      if (etapa === 'servidor' && (!(error instanceof ErrorApi) || error.estado >= 500)) {
        this.mensaje =
          'No pudimos conectar con el servidor del juego para preparar Google. Reintentá cuando la conexión esté disponible.';
      } else if (error instanceof ErrorCargaGoogle) {
        this.mensaje =
          'No se pudo cargar el botón de Google. Revisá la conexión o los bloqueadores del navegador y reintentá.';
      } else {
        this.mensaje = mensajeParaJugador(error, 'No se pudo preparar el acceso con Google.');
      }
    } finally {
      this.ocupado = false;
      this.vista.markForCheck();
    }
  }

  // Inicia el acceso con el plugin nativo y presenta el resultado de la operación.
  async android(): Promise<void> {
    if (this.ocupado) {
      return;
    }

    await this.ejecutar(() => this.google.ingresarAndroid(this.vincular));
  }

  // Completa el nickname de la identidad Google que ya verificó el servidor.
  async registrar(): Promise<void> {
    if (!this.ocupado) {
      await this.ejecutar(() => this.google.registrar(this.nickname));
    }
  }

  // Descarta el registro pendiente y prepara otro intento web cuando corresponde.
  async cancelarRegistro(): Promise<void> {
    this.google.cancelarRegistro();
    this.nickname = '';
    this.mensaje = '';

    if (!this.nativo) {
      await this.prepararWeb();
    }
  }

  // Mantiene el estado de espera y distingue vinculación, sesión y registro pendiente.
  private async ejecutar(accion: () => Promise<void>): Promise<void> {
    this.ocupado = true;
    this.mensaje = 'Verificando cuenta...';
    this.vista.markForCheck();

    try {
      await accion();

      if (!this.vincular && this.google.registroPendiente()) {
        this.mensaje = '';
      } else {
        this.mensaje = this.vincular ? 'Google vinculado. Tu jugador y su progreso se conservan.' : 'Sesión iniciada.';
        this.finalizado.emit();
      }
    } catch (error) {
      this.mensaje = mensajeParaJugador(error, 'No se completó el acceso con Google.');

      if (!this.nativo) {
        this.puedeReintentar = true;
      }
    } finally {
      this.ocupado = false;
      this.vista.markForCheck();
    }
  }

  // Cancela la renovación del desafío y retira el registro pendiente al salir.
  ngOnDestroy(): void {
    this.activo = false;
    clearTimeout(this.renovacion);

    if (!this.vincular) {
      this.google.cancelarRegistro();
    }
  }
}
