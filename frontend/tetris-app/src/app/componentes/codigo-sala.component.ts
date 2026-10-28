import { Component, Input, signal } from '@angular/core';

@Component({
    selector: 'app-codigo-sala',
    standalone: true,
    styles: [':host { display: block; flex-shrink: 0; } section { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; margin: 8px 0; } code { font-size: 1.3rem; letter-spacing: .15em; } p { margin: 0; }'],
    template: `
        <section aria-label="Código de sala">
            <strong>CÓDIGO DE SALA</strong><code>{{ codigo }}</code>
            <button type="button" class="accion secundaria" (click)="copiar()">Copiar código</button>
            <p role="status">{{ mensaje() }}</p>
        </section>
    `
})
export class CodigoSalaComponent {
    @Input({ required: true }) codigo = '';
    readonly mensaje = signal('');

    async copiar(): Promise<void> {
        try {
            await navigator.clipboard.writeText(this.codigo);
            this.mensaje.set('Código copiado.');
        } catch {
            this.mensaje.set('No se pudo copiar. Podés escribir el código que aparece aquí.');
        }
    }
}
