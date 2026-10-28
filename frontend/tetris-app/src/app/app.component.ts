// Mantiene la estructura principal de Ionic y coordina audio y vigilancia de sesión.
import { Component, effect, HostListener, inject, OnDestroy } from '@angular/core';
import { IonApp, IonRouterOutlet, Platform } from '@ionic/angular';
import { NavigationEnd, Router } from '@angular/router';
import { Capacitor } from '@capacitor/core';
import { App } from '@capacitor/app';
import { Subscription } from 'rxjs';
import { DialogosServicio } from './servicios/dialogos.servicio';
import { ServicioAudio } from './servicios/audio.servicio';
import { AutenticacionServicio } from './servicios/autenticacion.servicio';
import { OrientacionServicio } from './servicios/orientacion.servicio';
import { OrientacionVerticalComponent } from './componentes/orientacion-vertical.component';
import { VigilanciaSesionServicio } from './servicios/vigilancia-sesion.servicio';

@Component({
  selector: 'app-root',
  templateUrl: 'app.component.html',
  styleUrls: ['app.component.scss'],
  imports: [IonApp, IonRouterOutlet, OrientacionVerticalComponent],
})
export class AppComponent implements OnDestroy {
  private readonly router = inject(Router);
  private readonly plataforma = inject(Platform);
  private readonly dialogos = inject(DialogosServicio);
  readonly audio = inject(ServicioAudio);
  readonly orientacion = inject(OrientacionServicio);
  readonly vigilancia = inject(VigilanciaSesionServicio);
  private readonly acceso = inject(AutenticacionServicio);
  private retroceso?: Subscription;
  private navegacion: Subscription;
  private estadoAplicacion?: Awaited<ReturnType<typeof App.addListener>>;

  constructor() {
    effect(() => this.audio.usarJugador(this.acceso.sesion()?.usuarioId ?? null));
    this.navegacion = this.router.events.subscribe((evento) => {
      if (evento instanceof NavigationEnd && !evento.urlAfterRedirects.startsWith('/partida/')) {
        this.audio.cambiarMusica('menu');
      }
    });

    if (Capacitor.isNativePlatform()) {
      void App.addListener('appStateChange', (estado) => {
        this.audio.suspenderAplicacion(!estado.isActive);
        // WebView no siempre emite blur al minimizar: liberar los mismos controles de la web.
        if (!estado.isActive) {
          window.dispatchEvent(new Event('blur'));
        }

        if (estado.isActive) {
          void this.vigilancia.verificar(true);
        }
      }).then((listener) => {
        this.estadoAplicacion = listener;
      });
      this.retroceso = this.plataforma.backButton.subscribeWithPriority(10, () => {
        if (['/home', '/login', '/'].includes(this.router.url)) {
          void this.dialogos.confirmar('¿Cerrar Tetris? Tu progreso guardado se conserva.').then((confirmado) => {
            if (confirmado) {
              void App.exitApp();
            }
          });
        } else {
          // canDeactivate confirma el abandono antes de salir de la partida.
          void this.router.navigateByUrl('/home');
        }
      });
    }
  }

  // Retira suscripciones y escucha nativa y suspende el audio al liberar la aplicación.
  ngOnDestroy(): void {
    this.retroceso?.unsubscribe();
    this.navegacion.unsubscribe();
    void this.estadoAplicacion?.remove();
    this.audio.suspenderAplicacion(true);
  }

  @HostListener('document:pointerdown')
  @HostListener('document:keydown')
  // Desbloquea audio desde un gesto permitido por el navegador.
  activarAudio(): void {
    this.audio.desbloquear();
  }

  @HostListener('document:click', ['$event'])
  // Reproduce el clic de elementos de navegación salvo los controles de juego.
  sonidoBoton(evento: MouseEvent): void {
    const elemento = evento.target instanceof Element ? evento.target : null;

    if (elemento?.closest('button,a,ion-button,summary') && !elemento.closest('[data-sin-clic]')) {
      this.audio.desbloquear();
      this.audio.reproducirEfecto('clic');
    }
  }

  @HostListener('document:visibilitychange')
  // Suspende audio al ocultarse y comprueba la sesión al regresar.
  cambiarVisibilidad(): void {
    this.audio.suspenderAplicacion(document.hidden);

    if (!document.hidden) {
      void this.vigilancia.verificar();
    }
  }

  @HostListener('window:online')
  @HostListener('window:focus')
  // Solicita revisar la sesión cuando vuelve Internet o el foco.
  verificarSesion(): void {
    void this.vigilancia.verificar();
  }
}
