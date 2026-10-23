import { mensajeParaJugador } from '../../servicios/mensajes-jugador';
import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { IonContent } from '@ionic/angular';
import { IResultado } from '../../modelos/tetris.model';
import { AutenticacionServicio } from '../../servicios/autenticacion.servicio';
import { GoogleAccesoComponent } from '../../componentes/google-acceso.component';
import { DialogosServicio } from '../../servicios/dialogos.servicio';

@Component({
    standalone: true,
    imports: [CommonModule, RouterLink, IonContent, GoogleAccesoComponent],
    templateUrl: './perfil.page.html'
})
export class PerfilPage {
    readonly acceso = inject(AutenticacionServicio);
    private readonly router = inject(Router);
    private readonly vista = inject(ChangeDetectorRef);
    private readonly dialogos = inject(DialogosServicio);
    private entradaIniciada = false;
    resultados: IResultado[] = [];
    mensaje = '';
    codigo = '';
    visible = false;
    cargando = false;

    ngOnInit(): void {
        void this.ionViewWillEnter();
    }

    get mejorPuntuacion(): number {
        return Math.max(0, ...this.resultados.filter(r => r.modalidad === 'Individual').map(r => r.puntuacion));
    }

    get victorias(): number {
        return this.resultados.filter(r => r.esGanador && r.modalidad !== 'Individual').length;
    }

    get multijugador(): IResultado[] {
        return this.resultados.filter(r => r.modalidad !== 'Individual');
    }

    async ionViewWillEnter(): Promise<void> {
        if (this.entradaIniciada) {
            return;
        }
        this.entradaIniciada = true;
        const pendiente = sessionStorage.getItem('tetrisSala');
        if (pendiente) {
            await this.router.navigate(['/partida', pendiente], { replaceUrl: true });
            return;
        }
        this.visible = true;
        this.cargando = true;
        this.mensaje = '';
        try {
            await this.acceso.actualizarPerfil();
            this.resultados = await this.acceso.solicitar<IResultado[]>('/api/resultados/mios');
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'No se pudo cargar el perfil.');
        } finally {
            this.cargando = false;
            this.vista.markForCheck();
        }
    }

    ionViewDidLeave(): void {
        this.visible = false;
        this.entradaIniciada = false;
        this.codigo = '';
    }

    async guardarCuenta(): Promise<void> {
        if (!await this.dialogos.confirmar('Generar un código nuevo reemplaza el anterior. Guardalo fuera del dispositivo.')) {
            return;
        }
        try {
            this.codigo = await this.acceso.solicitar<string>('/api/usuarios/codigo-recuperacion', 'POST');
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'No se pudo generar el código.');
        } finally {
            this.vista.markForCheck();
        }
    }

    async cambiarJugador(): Promise<void> {
        await this.acceso.cambiarJugador();
        await this.router.navigateByUrl('/login', { replaceUrl: true });
    }

    async cerrarSesion(): Promise<void> {
        if (!await this.dialogos.confirmar('Vas a cerrar tu sesión. Para volver a este jugador necesitarás Google o tu código privado. Tu progreso se conserva.')) {
            return;
        }
        try {
            await this.acceso.cerrarSesion();
            await this.router.navigateByUrl('/login', { replaceUrl: true });
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'No pudimos cerrar tu sesión. Intentá de nuevo.');
        } finally {
            this.vista.markForCheck();
        }
    }
}
