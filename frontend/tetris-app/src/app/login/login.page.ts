// Permite crear o recuperar un jugador y seleccionar perfiles recordados.
// Muestra los motivos de cierre que informa el servicio de autenticación.
import { mensajeParaJugador } from '../servicios/mensajes-jugador';
import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { IonContent } from '@ionic/angular';
import { AutenticacionServicio } from '../servicios/autenticacion.servicio';
import { GoogleAccesoComponent } from '../componentes/google-acceso.component';

@Component({
  selector: 'app-login',
  templateUrl: './login.page.html',
  styleUrls: ['./login.page.scss'],
  standalone: true,
  imports: [FormsModule, IonContent, GoogleAccesoComponent],
})
export class LoginPage implements OnInit {
  readonly acceso = inject(AutenticacionServicio);
  private readonly router = inject(Router);
  private readonly vista = inject(ChangeDetectorRef);
  private entradaIniciada = false;
  usuario = '';
  mensaje = '';
  cargando = false;
  codigoRecuperacion = '';
  visible = false;

  // Prepara la pantalla de ingreso y la carga inicial de perfiles.
  ngOnInit(): void {
    void this.ionViewWillEnter();
  }

  // Actualiza el estado de ingreso al mostrar la pantalla.
  async ionViewWillEnter(): Promise<void> {
    if (this.entradaIniciada) {
      return;
    }

    this.entradaIniciada = true;

    try {
      await this.acceso.inicializar();

      if (this.acceso.sesion()) {
        await this.continuar();
        return;
      }

      this.visible = true;
    } catch (error) {
      this.mensaje = mensajeParaJugador(error, 'No se pudo recuperar la sesión.');
      this.visible = true;
    } finally {
      this.vista.markForCheck();
    }
  }

  // Marca la pantalla como oculta cuando se abandona el ingreso.
  ionViewDidLeave(): void {
    this.visible = false;
    this.entradaIniciada = false;
  }

  // Abre el menú principal después de que el ingreso quedó confirmado.
  async continuar(): Promise<void> {
    await this.router.navigateByUrl('/home', { replaceUrl: true });
  }

  // Solicita registrar el nickname y, si se acepta, continúa al menú.
  async ingresar(): Promise<void> {
    await this.ejecutar(() => this.acceso.registrar(this.usuario.trim()));
  }

  // Ingresa deliberadamente con la credencial de un perfil recordado.
  async seleccionar(usuarioId: number): Promise<void> {
    await this.ejecutar(() => this.acceso.seleccionar(usuarioId));
  }

  // Envía el código privado para recuperar la cuenta.
  async recuperar(): Promise<void> {
    await this.ejecutar(() => this.acceso.recuperarCodigo(this.codigoRecuperacion));
    this.codigoRecuperacion = '';
  }

  // Controla espera y errores de las acciones de ingreso para evitar envíos simultáneos.
  private async ejecutar(accion: () => Promise<void>): Promise<void> {
    if (this.cargando) {
      return;
    }

    this.cargando = true;

    try {
      await accion();
      await this.continuar();
    } catch (error) {
      this.mensaje = mensajeParaJugador(error, 'No se pudo ingresar.');
    } finally {
      this.cargando = false;
      this.vista.markForCheck();
    }
  }
}
