import { mensajeParaJugador } from '../servicios/mensajes-jugador';
import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { IonContent } from '@ionic/angular';
import { AutenticacionServicio } from '../servicios/autenticacion.servicio';
import { GoogleAccesoComponent } from '../componentes/google-acceso.component';

@Component({
    selector: 'app-login',
    templateUrl: './login.page.html',
    styleUrls: ['./login.page.scss'],
    standalone: true,
    imports: [FormsModule, IonContent, GoogleAccesoComponent]
})
export class LoginPage {
    readonly acceso = inject(AutenticacionServicio);
    private readonly router = inject(Router);
    private readonly vista = inject(ChangeDetectorRef);
    private entradaIniciada = false;
    usuario = '';
    mensaje = '';
    cargando = false;
    codigoRecuperacion = '';
    visible = false;

    ngOnInit(): void {
        void this.ionViewWillEnter();
    }

    async ionViewWillEnter(): Promise<void> {
        if (this.entradaIniciada) {
            return;
        }
        this.entradaIniciada = true;
        try {
            await this.acceso.inicializar();
            if (this.acceso.sesion()) {
                await this.continuar();
                return;
            }
            this.visible = true;
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'No se pudo recuperar la sesión.');
            this.visible = true;
        } finally {
            this.vista.markForCheck();
        }
    }

    ionViewDidLeave(): void {
        this.visible = false;
        this.entradaIniciada = false;
    }

    async continuar(): Promise<void> {
        await this.router.navigateByUrl('/home', { replaceUrl: true });
    }

    async ingresar(): Promise<void> {
        await this.ejecutar(() => this.acceso.registrar(this.usuario.trim()));
    }

    async seleccionar(usuarioId: number): Promise<void> {
        await this.ejecutar(() => this.acceso.seleccionar(usuarioId));
    }

    async recuperar(): Promise<void> {
        await this.ejecutar(() => this.acceso.recuperarCodigo(this.codigoRecuperacion));
        this.codigoRecuperacion = '';
    }

    private async ejecutar(accion: () => Promise<void>): Promise<void> {
        if (this.cargando) {
            return;
        }
        this.cargando = true;
        try {
            await accion();
            await this.continuar();
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'No se pudo ingresar.');
        } finally {
            this.cargando = false;
            this.vista.markForCheck();
        }
    }
}
