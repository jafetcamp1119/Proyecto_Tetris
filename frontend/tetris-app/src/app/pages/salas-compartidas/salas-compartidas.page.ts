import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { IonContent } from '@ionic/angular';

@Component({
    selector: 'app-salas-compartidas',
    templateUrl: './salas-compartidas.page.html',
    styleUrls: ['./salas-compartidas.page.scss'],
    standalone: true,
    imports: [IonContent, RouterLink]
})
export class SalasCompartidasPage {
    private readonly router = inject(Router);
    private readonly ruta = inject(ActivatedRoute);
    mensajeOpcion = '';

    get modo(): string {
        return this.ruta.snapshot.queryParamMap.get('modo') === 'Competitivo' ? 'Competitivo' : 'Compartido';
    }

    seleccionarOpcion(opcion: string): void {
        const accion = opcion === 'publica' ? 'publicas' : opcion;
        void this.router.navigate(['/salas', accion], { queryParams: { modo: this.modo } });
    }
}
