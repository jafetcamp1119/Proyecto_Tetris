import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonContent } from '@ionic/angular';

@Component({
  selector: 'app-login',
  templateUrl: './login.page.html',
  styleUrls: ['./login.page.scss'],
  standalone: true,
  imports: [FormsModule, IonContent],
})
export class LoginPage {
  usuario = '';
  contrasena = '';
  mostrarContrasena = false;
  mensaje = '';

  // Después conectamos el login con la API.
  ingresar() {
    this.mensaje =
      'El diseño está listo. Falta conectar el inicio de sesión con la API.';
  }
}  
