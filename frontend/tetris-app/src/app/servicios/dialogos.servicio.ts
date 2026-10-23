import { Injectable, inject } from '@angular/core';
import { AlertController } from '@ionic/angular';

@Injectable({ providedIn: 'root' })
export class DialogosServicio {
    private readonly alertas = inject(AlertController);

    async confirmar(mensaje: string): Promise<boolean> {
        const alerta = await this.alertas.create({
            header: 'Tetris',
            message: mensaje,
            backdropDismiss: false,
            buttons: [
                { text: 'Cancelar', role: 'cancel' },
                { text: 'Confirmar', role: 'confirmar' }
            ]
        });
        await alerta.present();
        const resultado = await alerta.onDidDismiss();
        return resultado.role === 'confirmar';
    }
}
