import { DOCUMENT } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';

// Respaldo para navegación interna o servidores que entreguen el index de Angular.
// Firebase sirve directamente el documento estático, incluso sin JavaScript.
@Component({
    selector: 'app-privacidad',
    standalone: true,
    template: `
        <main class="pantalla-tetris">
            <section class="tarjeta-tetris">
                <h1>Política de privacidad</h1>
                <p>Abriendo la política de Proyecto Tetris...</p>
                <a href="privacidad/index.html">Leer la política de privacidad</a>
            </section>
        </main>
    `
})
export class PrivacidadPage implements OnInit {
    private readonly documento = inject(DOCUMENT);

    ngOnInit(): void {
        const direccion = new URL('privacidad/index.html', this.documento.baseURI);
        this.documento.defaultView?.location.replace(direccion.href);
    }
}
