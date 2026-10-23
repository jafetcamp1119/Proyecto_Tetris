import { Component, HostListener, OnDestroy, effect, inject } from '@angular/core';
import { IonApp, IonRouterOutlet, Platform } from '@ionic/angular';
import { NavigationEnd, Router } from '@angular/router';
import { Capacitor } from '@capacitor/core';
import { App } from '@capacitor/app';
import { Subscription } from 'rxjs';
import { DialogosServicio } from './servicios/dialogos.servicio';
import { ServicioAudio } from './servicios/audio.servicio';
import { AutenticacionServicio } from './servicios/autenticacion.servicio';

@Component({
  selector: 'app-root',
  templateUrl: 'app.component.html',
  imports: [IonApp, IonRouterOutlet],
})
export class AppComponent implements OnDestroy {
    private readonly router = inject(Router);
    private readonly plataforma = inject(Platform);
    private readonly dialogos = inject(DialogosServicio);
    readonly audio = inject(ServicioAudio);
    private readonly acceso = inject(AutenticacionServicio);
    private retroceso?: Subscription;
    private navegacion: Subscription;
    private estadoAplicacion?: Awaited<ReturnType<typeof App.addListener>>;

    constructor() {
        effect(() => this.audio.usarJugador(this.acceso.sesion()?.usuarioId ?? null));
        this.navegacion = this.router.events.subscribe(evento => {
            if (evento instanceof NavigationEnd && !evento.urlAfterRedirects.startsWith('/partida/')) {
                this.audio.cambiarMusica('menu');
            }
        });
        if (Capacitor.isNativePlatform()) {
            void App.addListener('appStateChange', estado => this.audio.suspenderAplicacion(!estado.isActive))
                .then(listener => { this.estadoAplicacion = listener; });
            this.retroceso = this.plataforma.backButton.subscribeWithPriority(10, () => {
                if (['/home', '/login', '/'].includes(this.router.url)) {
                    void this.dialogos.confirmar('¿Cerrar Tetris? Tu progreso guardado se conserva.').then(confirmado => {
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

    ngOnDestroy(): void {
        this.retroceso?.unsubscribe();
        this.navegacion.unsubscribe();
        void this.estadoAplicacion?.remove();
        this.audio.suspenderAplicacion(true);
    }

    @HostListener('document:pointerdown')
    @HostListener('document:keydown')
    activarAudio(): void {
        this.audio.desbloquear();
    }

    @HostListener('document:click', ['$event'])
    sonidoBoton(evento: MouseEvent): void {
        const elemento = evento.target instanceof Element ? evento.target : null;
        if (elemento?.closest('button,a,ion-button,summary') && !elemento.closest('[data-sin-clic]')) {
            this.audio.desbloquear();
            this.audio.reproducirEfecto('clic');
        }
    }

    @HostListener('document:visibilitychange')
    cambiarVisibilidad(): void {
        this.audio.suspenderAplicacion(document.hidden);
    }
}
