import { Component, inject, signal } from '@angular/core';
import { AutenticacionServicio } from '../servicios/autenticacion.servicio';

@Component({
    selector: 'app-codigo-recuperacion',
    standalone: true,
    styles: [':host { display: block; flex-shrink: 0; max-height: 25dvh; overflow: auto; }'],
    template: `
        @if (acceso.codigoRecuperacionNuevo(); as codigo) {
            <section class="tarjeta-tetris" role="status">
                <h2>Guardá tu código de recuperación</h2>
                <p>Se muestra únicamente ahora. Guardalo fuera de la aplicación: vence en un año.</p>
                @if (acceso.codigoRecuperacionPorConfirmar()) {
                    <p>El código anterior sigue siendo válido hasta que confirmés que guardaste este nuevo código.
                        Al confirmar, se activa el nuevo y se invalida el anterior.</p>
                }
                <code class="codigo-privado">{{ codigo }}</code>
                <button class="accion" (click)="copiar(codigo)">Copiar código</button>
                <button class="accion secundaria" [disabled]="confirmando()" (click)="confirmar()">Ya lo guardé</button>
                <p>{{ mensaje() }}</p>
            </section>
        }
    `
})
export class CodigoRecuperacionComponent {
    readonly acceso = inject(AutenticacionServicio);
    readonly mensaje = signal('');
    readonly confirmando = signal(false);

    async confirmar(): Promise<void> {
        if (this.confirmando()) { return; }
        this.confirmando.set(true);
        try {
            await this.acceso.confirmarCodigoGuardado();
            this.mensaje.set('');
        } catch {
            this.mensaje.set('No se pudo confirmar. Conservá este código y reintentá antes de cerrar el aviso.');
        } finally {
            this.confirmando.set(false);
        }
    }

    async copiar(codigo: string): Promise<void> {
        try { await navigator.clipboard.writeText(codigo); this.mensaje.set('Código copiado.'); }
        catch { this.mensaje.set('No se pudo copiar. Anotá el código antes de cerrar este aviso.'); }
    }
}
