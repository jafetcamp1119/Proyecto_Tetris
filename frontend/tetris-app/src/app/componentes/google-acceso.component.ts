import { mensajeParaJugador } from '../servicios/mensajes-jugador';
import { AfterViewInit, ChangeDetectorRef, Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild, inject } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { environment } from '../../environments/environment';
import { GoogleServicio } from '../servicios/google.servicio';

interface GoogleIdentity {
    accounts: {
        id: {
            initialize(opciones: { client_id: string; nonce: string; auto_select: boolean; callback: (respuesta: { credential: string }) => void }): void;
            renderButton(elemento: HTMLElement, opciones: { theme: string; size: string; text: string; width: number }): void;
            cancel(): void;
        };
    };
}

let cargaGoogle: Promise<void> | undefined;

@Component({
    selector: 'app-google-acceso',
    standalone: true,
    template: `
        @if (habilitado) {
            @if (nativo) {
                <button type="button" class="accion google" [disabled]="ocupado" (click)="android()">
                    {{ vincular ? 'Vincular cuenta de Google' : 'Continuar con Google' }}
                </button>
            } @else {
                <div #botonGoogle class="google-web" [attr.aria-busy]="ocupado"></div>
            }
            <p role="status">{{ mensaje }}</p>
        }
    `
})
export class GoogleAccesoComponent implements AfterViewInit, OnDestroy {
    @Input() vincular = false;
    @Output() finalizado = new EventEmitter<void>();
    @ViewChild('botonGoogle') boton?: ElementRef<HTMLDivElement>;
    private readonly google = inject(GoogleServicio);
    private readonly vista = inject(ChangeDetectorRef);
    readonly nativo = Capacitor.isNativePlatform();
    readonly habilitado = environment.google.habilitado;
    ocupado = false;
    mensaje = '';
    private activo = true;
    private renovacion?: ReturnType<typeof setTimeout>;

    async ngAfterViewInit(): Promise<void> {
        if (!this.nativo && this.habilitado) {
            await this.prepararWeb();
        }
    }

    private identidad(): GoogleIdentity {
        return (window as unknown as { google: GoogleIdentity }).google;
    }

    private async prepararWeb(): Promise<void> {
        try {
            cargaGoogle ??= new Promise<void>((resolver, rechazar) => {
                const script = document.createElement('script');
                script.src = 'https://accounts.google.com/gsi/client';
                script.async = true;
                script.onload = () => resolver();
                script.onerror = () => { cargaGoogle = undefined; rechazar(new Error('No se pudo cargar Google. Podés continuar como invitado.')); };
                document.head.append(script);
            });
            await cargaGoogle;
            const desafio = await this.google.preparar(this.vincular);
            if (!this.activo || !this.boton) {
                return;
            }
            this.identidad().accounts.id.initialize({
                client_id: environment.google.idClienteWeb,
                nonce: desafio.nonce,
                auto_select: false,
                callback: respuesta => {
                    if (this.activo && !this.ocupado) {
                        void this.ejecutar(() => this.google.completar(desafio, respuesta.credential, this.vincular));
                    }
                }
            });
            this.boton.nativeElement.replaceChildren();
            this.identidad().accounts.id.renderButton(this.boton.nativeElement, {
                theme: 'outline', size: 'large', text: 'continue_with', width: 260
            });
            this.renovacion = setTimeout(() => void this.prepararWeb(), 8 * 60000);
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'Google no está disponible.');
            this.vista.markForCheck();
        }
    }

    async android(): Promise<void> {
        await this.ejecutar(() => this.google.ingresarAndroid(this.vincular));
    }

    private async ejecutar(accion: () => Promise<void>): Promise<void> {
        this.ocupado = true;
        this.mensaje = 'Verificando cuenta...';
        this.vista.markForCheck();
        try {
            await accion();
            this.mensaje = this.vincular ? 'Google vinculado. Tu jugador y su progreso se conservan.' : 'Sesión recuperada.';
            this.finalizado.emit();
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'No se completó el acceso con Google.');
        } finally {
            this.ocupado = false;
            this.vista.markForCheck();
        }
    }

    ngOnDestroy(): void {
        this.activo = false;
        clearTimeout(this.renovacion);
    }
}
