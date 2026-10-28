// Permite crear, listar y entrar a salas; entrega la sala elegida a la pantalla de partida.
import { mensajeParaJugador } from '../../servicios/mensajes-jugador';
import { ChangeDetectorRef, Component, inject, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { IonContent } from '@ionic/angular';
import { ISala, ISalaPublica, ITipoPiezaEspecial, ModoJuego } from '../../modelos/tetris.model';
import { SalasServicio } from '../../servicios/salas.servicio';
import { AutenticacionServicio } from '../../servicios/autenticacion.servicio';
import {
  configuracionInicial,
  ConfiguracionSalaComponent,
  validarConfiguracion,
} from '../../componentes/configuracion-sala.component';
import { nombreModo } from '../../modelos/presentacion-juego';
import { CodigoSalaComponent } from '../../componentes/codigo-sala.component';
import { combineLatest, Subscription } from 'rxjs';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink, IonContent, ConfiguracionSalaComponent, CodigoSalaComponent],
  templateUrl: './salas.page.html',
})
export class SalasPage implements OnInit, OnDestroy {
  private readonly salas = inject(SalasServicio);
  private readonly acceso = inject(AutenticacionServicio);
  private readonly ruta = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly vista = inject(ChangeDetectorRef);
  private entradaIniciada = false;
  datos = configuracionInicial();
  publicas: ISalaPublica[] = [];
  catalogo: ITipoPiezaEspecial[] = [];
  codigo = '';
  mensaje = '';
  cargando = false;
  accion: 'crear' | 'publicas' | 'privada' = 'crear';
  readonly nombreModo = nombreModo;
  private cambiosRuta?: Subscription;

  // Escucha cambios de ruta para cargar el modo y la acción seleccionados.
  ngOnInit(): void {
    this.cambiosRuta = combineLatest([this.ruta.paramMap, this.ruta.queryParamMap]).subscribe(() => {
      this.entradaIniciada = false;
      void this.ionViewWillEnter();
    });
  }

  // Retira la suscripción de ruta al destruir la pantalla.
  ngOnDestroy(): void {
    this.cambiosRuta?.unsubscribe();
  }

  // Permite preparar de nuevo los datos cuando se vuelva a entrar.
  ionViewDidLeave(): void {
    this.entradaIniciada = false;
  }

  // Retoma una sala pendiente o prepara las opciones del modo solicitado.
  async ionViewWillEnter(): Promise<void> {
    if (this.entradaIniciada) {
      return;
    }

    this.entradaIniciada = true;
    const pendiente = sessionStorage.getItem('tetrisSala');

    if (pendiente) {
      await this.router.navigate(['/partida', pendiente], { replaceUrl: true });
      return;
    }

    const modo = this.ruta.snapshot.queryParamMap.get('modo');
    this.datos = configuracionInicial();
    this.datos.modo = ['Individual', 'Compartido', 'Competitivo'].includes(modo || '')
      ? (modo as ModoJuego)
      : 'Compartido';
    const accion = this.ruta.snapshot.paramMap.get('accion');
    this.accion = accion === 'publicas' || accion === 'privada' ? accion : 'crear';

    if (this.datos.modo === 'Individual') {
      this.accion = 'crear';
      this.datos.maximoJugadores = 1;
    }

    this.publicas = [];
    await this.actualizar();
  }

  // Carga el listado o el catálogo que necesita la pantalla actual.
  async actualizar(): Promise<void> {
    this.cargando = true;
    this.mensaje = '';

    try {
      if (this.accion === 'publicas' && this.datos.modo !== 'Individual') {
        this.publicas = (await this.salas.listar()).filter((s) => s.modo === this.datos.modo);
      }

      if (this.accion === 'crear' && this.datos.modo === 'Compartido') {
        this.catalogo = await this.acceso.solicitar<ITipoPiezaEspecial[]>('/api/salas/tipos-especiales');
      }
    } catch (error) {
      this.mensaje = mensajeParaJugador(error, 'No se pudieron cargar las salas.');
    } finally {
      this.cargando = false;
      this.vista.markForCheck();
    }
  }

  // Valida las opciones y solicita crear la sala.
  async crear(): Promise<void> {
    const error = validarConfiguracion(this.datos);

    if (error) {
      this.mensaje = error;
      return;
    }

    await this.entrar(() => this.salas.crear(this.datos));
  }

  // Solicita entrar a la sala pública seleccionada.
  async unirse(id: string): Promise<void> {
    await this.entrar(() => this.salas.unirse(id));
  }

  // Solicita entrar con el código escrito por el jugador.
  async porCodigo(): Promise<void> {
    await this.entrar(() => this.salas.unirseCodigo(this.codigo));
  }

  // Evita solicitudes duplicadas, guarda la sala recibida y navega a la partida.
  private async entrar(accion: () => Promise<ISala>): Promise<void> {
    if (this.cargando) {
      return;
    }

    this.cargando = true;
    this.mensaje = '';

    try {
      const sala = await accion();
      sessionStorage.setItem('tetrisSala', sala.id);
      await this.router.navigate(['/partida', sala.id]);
    } catch (error) {
      this.mensaje = mensajeParaJugador(error, 'No se pudo entrar a la sala.');
    } finally {
      this.cargando = false;
      this.vista.markForCheck();
    }
  }
}
