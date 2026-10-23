import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
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
  cargando = false;

  constructor(private router: Router) {}

  async ingresar() {
    if (this.cargando) return;

    if (!this.usuario.trim() || !this.contrasena) {
      this.mensaje = 'Escriba su usuario y contraseña.';
      return;
    }

    this.cargando = true;
    this.mensaje = 'Iniciando sesión...';

    try {
      // Envía las credenciales a la API.
      const respuesta = await fetch(
        'http://localhost:5186/api/usuarios/login',
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            nombreUsuario: this.usuario.trim(),
            contrasena: this.contrasena,
          }),
        }
      );

      const resultado = await respuesta.json();

      if (!respuesta.ok || !resultado.success) {
        this.mensaje =
          resultado.error || 'No se pudo iniciar sesión.';
        return;
      }

      const sesion = resultado.data;

      if (!sesion?.token) {
        this.mensaje = 'La API no devolvió el token de acceso.';
        return;
      }

      // Conserva el token durante esta sesión del navegador.
      sessionStorage.setItem('token', sesion.token);
      sessionStorage.setItem('expiraEnUtc', sesion.expiraEnUtc);

      this.contrasena = '';
      this.mensaje = 'Inicio de sesión correcto.';

      await this.router.navigateByUrl('/home');
    } catch {
      this.mensaje =
        'No se pudo completar la conexión. Revise que la API siga abierta y que permita el origen de Ionic en CORS.';
    } finally {
      this.cargando = false;
    }
  }
}