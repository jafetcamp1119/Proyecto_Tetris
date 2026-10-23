import { mensajeParaJugador } from '../../servicios/mensajes-jugador';
import { ChangeDetectorRef, Component, HostListener, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { IonContent } from '@ionic/angular';
import { ICrearSala, ISala, ITablero, ITipoPiezaEspecial, Movimiento } from '../../modelos/tetris.model';
import { SalasServicio } from '../../servicios/salas.servicio';
import { AutenticacionServicio } from '../../servicios/autenticacion.servicio';
import { TableroComponent } from '../../componentes/tablero.component';
import { ControlesComponent } from '../../componentes/controles.component';
import { ConfiguracionSalaComponent, validarConfiguracion } from '../../componentes/configuracion-sala.component';
import { PiezasEspecialesComponent } from '../../componentes/piezas-especiales.component';
import { efectoDePieza, motivoFinal, nombreModo, nombresEfectos } from '../../modelos/presentacion-juego';
import { DialogosServicio } from '../../servicios/dialogos.servicio';

@Component({
    standalone: true,
    imports: [FormsModule, IonContent, TableroComponent, ControlesComponent, ConfiguracionSalaComponent, PiezasEspecialesComponent],
    templateUrl: './partida.page.html'
})
export class PartidaPage {
    readonly salas = inject(SalasServicio);
    readonly acceso = inject(AutenticacionServicio);
    private readonly ruta = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly vista = inject(ChangeDetectorRef);
    private readonly dialogos = inject(DialogosServicio);
    private entradaIniciada = false;
    private reloj?: ReturnType<typeof setInterval>;
    private diferenciaServidor = 0;
    private readonly ahora = signal(Date.now());
    mensaje = '';
    ocupado = false;
    visible = false;
    observando: number | null = null;
    configuracion?: ICrearSala;
    catalogo: ITipoPiezaEspecial[] = [];
    readonly nombreModo = nombreModo;
    readonly nombresEfectos = nombresEfectos;
    readonly motivoFinal = motivoFinal;

    nombreEspecial(codigo: string | null): string {
        const tipo = this.catalogo.find(pieza => pieza.codigo === codigo);
        return tipo ? nombresEfectos[efectoDePieza(tipo)] : 'Pieza especial';
    }

    nombreJugador(id: number): string {
        return this.salas.sala()?.jugadores.find(jugador => jugador.usuarioId === id)?.nombre || 'Otro jugador';
    }

    constructor() {
        effect(() => {
            const pausa = this.salas.sala()?.pausa;
            if (pausa) {
                this.diferenciaServidor = Date.parse(pausa.ahoraServidorUtc) - Date.now();
            }
        });
    }

    get segundosPausa(): number {
        const pausa = this.salas.sala()?.pausa;
        const fin = pausa?.fase === 'Votando' ? pausa.vencimientoVotacionUtc : pausa?.finPausaUtc;
        // Solo presentación: alcanzar cero no reanuda ni resuelve una votación.
        return fin ? Math.max(0, Math.ceil((Date.parse(fin) - this.ahora() - this.diferenciaServidor) / 1000)) : 0;
    }

    ngOnInit(): void {
        void this.ionViewWillEnter();
    }

    get usuarioId(): number {
        return this.acceso.sesion()?.usuarioId || 0;
    }

    get esCreador(): boolean {
        return this.salas.sala()?.creadorId === this.usuarioId;
    }

    get tablero(): ITablero | undefined {
        const sala = this.salas.sala();
        if (!sala || sala.modo === 'Compartido') {
            return sala?.tableros[0];
        }
        const propio = sala.tableros.find(t => t.usuarioId === this.usuarioId);
        if (propio && !propio.perdido) {
            return propio;
        }
        return sala.tableros.find(t => t.usuarioId === this.observando) || propio;
    }

    get estadoJugador(): string {
        const sala = this.salas.sala();
        const jugador = sala?.jugadores.find(j => j.usuarioId === this.usuarioId);
        if (jugador?.perdido) {
            return 'Estás como espectador.';
        }
        if (sala?.modo === 'Compartido') {
            return sala.usuarioTurno === this.usuarioId ? '¡Es tu turno!' : `Turno de ${sala.jugadores.find(j => j.usuarioId === sala.usuarioTurno)?.nombre || 'otro jugador'}`;
        }
        return 'Tu tablero · Usá los controles o las flechas del teclado.';
    }

    async ionViewWillEnter(): Promise<void> {
        if (this.entradaIniciada) {
            return;
        }
        this.entradaIniciada = true;
        this.ahora.set(Date.now());
        this.reloj = setInterval(() => this.ahora.set(Date.now()), 1000);
        this.visible = true;
        this.configuracion = undefined;
        await this.reconectar();
    }

    async ionViewDidLeave(): Promise<void> {
        this.visible = false;
        this.entradaIniciada = false;
        clearInterval(this.reloj);
        await this.salas.desconectar(this.ruta.snapshot.paramMap.get('id')!);
    }

    ngOnDestroy(): void {
        this.visible = false;
        clearInterval(this.reloj);
        void this.salas.desconectar(this.ruta.snapshot.paramMap.get('id')!);
    }

    async reconectar(): Promise<void> {
        await this.ejecutar(async () => {
            await this.salas.conectar(this.ruta.snapshot.paramMap.get('id')!);
            if (this.salas.sala()?.modo === 'Compartido') {
                this.catalogo = await this.acceso.solicitar<ITipoPiezaEspecial[]>('/api/salas/tipos-especiales');
            }
        });
    }

    async mover(movimiento: Movimiento): Promise<void> {
        try {
            await this.salas.mover(movimiento);
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'No se pudo enviar el movimiento.');
            this.vista.markForCheck();
        }
    }

    @HostListener('window:keydown', ['$event'])
    teclado(evento: KeyboardEvent): void {
        if (!this.visible || !this.salas.puedeMover() || (evento.target as HTMLElement).matches('input,select,textarea,button')) {
            return;
        }
        const movimientos: Record<string, Movimiento> = { ArrowLeft: 'Izquierda', ArrowRight: 'Derecha', ArrowUp: 'Rotar', ArrowDown: 'Bajar', ' ': 'Caer' };
        if (movimientos[evento.key]) {
            evento.preventDefault();
            void this.mover(movimientos[evento.key]);
        }
    }

    async accion(nombre: string, cuerpo?: unknown, metodo = 'POST'): Promise<void> {
        await this.ejecutar(() => this.salas.accion(nombre, cuerpo, metodo));
    }

    async editar(sala: ISala): Promise<void> {
        this.configuracion = structuredClone({
            modo: sala.modo, maximoJugadores: sala.maximoJugadores, piezasEspeciales: sala.piezasEspeciales,
            piezasNormales: sala.piezasNormales, velocidad: sala.velocidad, puntos: sala.puntos,
            metaPuntos: sala.metaPuntos, minimoVictoriasCorona: sala.minimoVictoriasCorona
        });
        try {
            this.catalogo = sala.modo === 'Compartido' ? await this.acceso.solicitar<ITipoPiezaEspecial[]>('/api/salas/tipos-especiales') : [];
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'No se pudo cargar el catálogo.');
        } finally {
            this.vista.markForCheck();
        }
    }

    async guardarConfiguracion(): Promise<void> {
        if (!this.configuracion) {
            return;
        }
        const error = validarConfiguracion(this.configuracion);
        if (error) {
            this.mensaje = error;
            return;
        }
        await this.ejecutar(async () => {
            await this.salas.accion('configuracion', this.configuracion, 'PUT');
            this.configuracion = undefined;
        });
    }

    async puedeSalir(): Promise<boolean> {
        const sala = this.salas.sala();
        if (!sala) {
            if (sessionStorage.getItem('tetrisSala')) {
                if (!await this.dialogos.confirmar('No pudimos reconectar. ¿Salir? Si no volvés a tiempo, tu jugador dejará la sala.')) {
                    return false;
                }
                sessionStorage.removeItem('tetrisSala');
                await this.salas.desconectar();
            }
            return true;
        }
        if (sala.estado !== 'Cancelada') {
            const aviso = this.esCreador ? 'Si salís, se cerrará la sala para todos. ¿Salir?' : '¿Abandonar la sala? En una partida activa pasarás a eliminado.';
            if (!await this.dialogos.confirmar(aviso)) {
                return false;
            }
        }
        try {
            await this.salas.abandonar();
            return true;
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'No pudimos confirmar tu salida.');
            this.vista.markForCheck();
            if (await this.dialogos.confirmar('Se perdió la conexión. ¿Salir? Si no volvés a tiempo, tu jugador dejará la sala.')) {
                await this.salas.desconectar();
                sessionStorage.removeItem('tetrisSala');
                return true;
            }
            return false;
        }
    }

    salir(): void {
        void this.router.navigateByUrl('/home');
    }

    private async ejecutar(accion: () => Promise<void>): Promise<void> {
        if (this.ocupado) {
            return;
        }
        this.ocupado = true;
        this.mensaje = '';
        try {
            await accion();
        } catch (error) {
            this.mensaje = mensajeParaJugador(error, 'No se pudo completar la acción.');
        } finally {
            this.ocupado = false;
            this.vista.markForCheck();
        }
    }
}
