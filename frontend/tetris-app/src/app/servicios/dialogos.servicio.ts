import { Injectable, inject } from '@angular/core';
import { AlertController } from '@ionic/angular';

@Injectable({ providedIn: 'root' })
export class DialogosServicio {
    private readonly alertas = inject(AlertController);
    private generacion = 0;

    async cerrarPendientes(): Promise<void> {
        this.generacion++;
        const alerta = await this.alertas.getTop();
        if (alerta) { await alerta.dismiss(false, 'sesion-cerrada'); }
    }

    async confirmar(mensaje: string): Promise<boolean> {
        const generacion = this.generacion;
        const alerta = await this.alertas.create({
            header: 'Tetris',
            message: mensaje,
            backdropDismiss: false,
            buttons: [
                { text: 'Cancelar', role: 'cancel' },
                { text: 'Confirmar', role: 'confirmar' }
            ]
        });
        if (generacion !== this.generacion) { return false; }
        await alerta.present();
        if (generacion !== this.generacion) { await alerta.dismiss(false, 'sesion-cerrada'); }
        const resultado = await alerta.onDidDismiss();
        return resultado.role === 'confirmar';
    }
}
