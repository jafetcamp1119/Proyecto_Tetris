import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { IonContent, IonButton } from '@ionic/angular';
import { AutenticacionServicio } from '../servicios/autenticacion.servicio';

@Component({
    selector: 'app-home',
    templateUrl: 'home.page.html',
    styleUrls: ['home.page.scss'],
    standalone: true,
    imports: [IonContent, IonButton]
})
export class HomePage {
    readonly acceso = inject(AutenticacionServicio);
    private readonly router = inject(Router);
    get salaPendiente(): string | null {
        return sessionStorage.getItem('tetrisSala');
    }

    abrirSalas(): void {
        if (this.salaPendiente) {
            this.reabrirSala();
            return;
        }
        void this.router.navigateByUrl('/salas-compartidas');
    }

    abrirModo(modo: string): void {
        if (this.salaPendiente) {
            this.reabrirSala();
            return;
        }
        const destino = modo === 'Individual' ? ['/salas', 'crear'] : ['/salas-compartidas'];
        void this.router.navigate(destino, { queryParams: { modo } });
    }

    abrirPerfil(): void {
        if (this.salaPendiente) {
            this.reabrirSala();
            return;
        }
        void this.router.navigateByUrl('/perfil');
    }

    abrirAjustes(): void {
        void this.router.navigateByUrl('/ajustes');
    }

    reabrirSala(): void {
        if (this.salaPendiente) {
            void this.router.navigate(['/partida', this.salaPendiente]);
        }
    }
}
