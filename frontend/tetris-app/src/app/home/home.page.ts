import { Component, signal } from '@angular/core';
import {
  IonHeader,
  IonToolbar,
  IonTitle,
  IonContent,
  IonButton
} from '@ionic/angular';

@Component({
  selector: 'app-home',
  templateUrl: 'home.page.html',
  styleUrls: ['home.page.scss'],
  imports: [IonHeader, IonToolbar, IonTitle, IonContent, IonButton],
})
export class HomePage {
  mensaje = signal('Todavía no hemos probado la conexión.');
  consultando = signal(false);

  async probarConexion() {
    this.consultando.set(true);
    this.mensaje.set('Conectando...');

    try {
      const respuesta = await fetch('http://localhost:5186/salud');

      if (!respuesta.ok) {
        throw new Error('La API respondió con un error.');
      }

      const datos = await respuesta.json();
      this.mensaje.set(datos.estado);
    } catch (error) {
      this.mensaje.set('No se pudo consultar la API.');
      console.error(error);
    } finally {
      this.consultando.set(false);
    }
  }
}