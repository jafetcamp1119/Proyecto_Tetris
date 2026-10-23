import { mensajeParaJugador } from './mensajes-jugador';
import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { App } from '@capacitor/app';
import { Capacitor, PluginListenerHandle } from '@capacitor/core';
import { ICrearSala, IMovimiento, IRespuesta, ISala, ISalaPublica, Movimiento } from '../modelos/tetris.model';
import { AutenticacionServicio } from './autenticacion.servicio';
import { ApiServicio, ErrorApi } from './api.servicio';
import { ServicioAudio } from './audio.servicio';

@Injectable({ providedIn: 'root' })
export class SalasServicio {
    private readonly acceso = inject(AutenticacionServicio);
    private readonly api = inject(ApiServicio);
    private readonly audio = inject(ServicioAudio);
    private conexion?: HubConnection;
    private salaId?: string;
    private escuchaApp?: PluginListenerHandle;
    private secuencia = 0;
    private movimientoPendiente = false;
    readonly sala = signal<ISala | null>(null);
    readonly conectado = signal(false);
    readonly mensajeConexion = signal('');

    listar(): Promise<ISalaPublica[]> {
        return this.acceso.solicitar('/api/salas');
    }

    crear(datos: ICrearSala): Promise<ISala> {
        return this.acceso.solicitar('/api/salas', 'POST', datos);
    }

    unirse(id: string): Promise<ISala> {
        return this.acceso.solicitar(`/api/salas/${id}/unirse`, 'POST');
    }

    unirseCodigo(codigo: string): Promise<ISala> {
        return this.acceso.solicitar(`/api/salas/codigo/${encodeURIComponent(codigo.trim().toUpperCase())}/unirse`, 'POST');
    }

    async conectar(id: string): Promise<void> {
        await this.desconectar();
        this.salaId = id;
        try {
            await this.acceso.solicitar<ISala>(`/api/salas/${id}`);
        } catch (error) {
            if (error instanceof ErrorApi && [400, 404].includes(error.estado)) {
                sessionStorage.removeItem('tetrisSala');
            }
            throw error;
        }
        this.mensajeConexion.set('Conectando con la sala...');
        const conexion = new HubConnectionBuilder()
            .withUrl(`${this.api.url()}/hubs/juego`, { accessTokenFactory: () => this.acceso.token() })
            .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
            .configureLogging(LogLevel.None)
            .build();
        this.conexion = conexion;
        conexion.on('EstadoActualizado', (sala: ISala) => {
            if (this.conexion === conexion && sala.id === id) {
                this.actualizar(sala);
            }
        });
        conexion.on('SalaCerrada', (datos: { salaId: string; motivo: string }) => {
            if (datos.salaId === id && this.conexion === conexion) {
                this.audio.cambiarMusica('menu');
                this.mensajeConexion.set('La persona que creó la sala la cerró. Podés buscar otra partida.');
                this.sala.update(sala => sala ? { ...sala, estado: 'Cancelada' } : null);
            }
        });
        const entrar = async (): Promise<void> => {
            const respuesta = await conexion.invoke<IRespuesta<ISala>>('Entrar', id);
            if (!respuesta.success || !respuesta.data) {
                throw new Error(respuesta.error || 'No se pudo entrar a la sala.');
            }
            if (this.conexion !== conexion) {
                return;
            }
            this.actualizar(respuesta.data);
            this.conectado.set(true);
            this.mensajeConexion.set('Conectado');
            sessionStorage.setItem('tetrisSala', id);
        };
        conexion.onreconnecting(() => {
            this.conectado.set(false);
            this.audio.suspenderPartida();
            this.mensajeConexion.set('Reconectando. Los controles están bloqueados.');
        });
        conexion.onreconnected(() => {
            void entrar().catch(error => this.fallo(error));
        });
        conexion.onclose(() => {
            if (this.conexion === conexion) {
                this.conectado.set(false);
                this.audio.suspenderPartida();
                this.mensajeConexion.set('Se perdió la conexión. Intentá volver a la partida.');
            }
        });
        try {
            await conexion.start();
            await entrar();
        } catch (error) {
            this.fallo(error);
            throw error;
        }
        if (Capacitor.isNativePlatform()) {
            this.escuchaApp = await App.addListener('appStateChange', ({ isActive }) => {
                if (isActive && conexion.state === HubConnectionState.Disconnected && this.conexion === conexion) {
                    void conexion.start().then(entrar).catch(error => this.fallo(error));
                }
            });
        }
    }

    actualizar(sala: ISala): void {
        if (this.salaId && this.salaId !== sala.id) {
            return;
        }
        const actual = this.sala();
        if (actual?.id === sala.id && actual.version > sala.version) {
            return;
        }
        this.secuencia = Math.max(this.secuencia, sala.jugadores.find(j => j.usuarioId === this.acceso.sesion()?.usuarioId)?.ultimaSecuencia || 0);
        this.audio.actualizarSala(sala, this.acceso.sesion()?.usuarioId || 0, this.conectado());
        this.sala.set(sala);
    }

    puedeMover(): boolean {
        const sala = this.sala();
        const usuarioId = this.acceso.sesion()?.usuarioId;
        const jugador = sala?.jugadores.find(j => j.usuarioId === usuarioId);
        return !!sala && !!jugador && this.conectado() && sala.estado === 'EnCurso'
            && sala.pausa.fase !== 'Pausada' && !jugador.perdido && !jugador.abandono
            && (sala.modo !== 'Compartido' || sala.usuarioTurno === usuarioId);
    }

    async mover(movimiento: Movimiento): Promise<void> {
        if (this.movimientoPendiente || !this.puedeMover() || !this.conexion) {
            return;
        }
        this.movimientoPendiente = true;
        const datos: IMovimiento = { movimiento, secuencia: ++this.secuencia };
        try {
            const respuesta = await this.conexion.invoke<IRespuesta<ISala>>('Mover', this.sala()!.id, datos);
            if (!respuesta.success || !respuesta.data) {
                throw new Error(respuesta.error || 'Movimiento rechazado.');
            }
            this.actualizar(respuesta.data);
        } finally {
            this.movimientoPendiente = false;
        }
    }

    async accion(accion: string, cuerpo?: unknown, metodo = 'POST'): Promise<void> {
        const sala = this.sala();
        if (!sala || !this.conectado()) {
            throw new Error('La sala no está conectada.');
        }
        this.actualizar(await this.acceso.solicitar<ISala>(`/api/salas/${sala.id}/${accion}`, metodo, cuerpo));
    }

    async abandonar(): Promise<void> {
        const sala = this.sala();
        if (sala && sala.estado !== 'Cancelada') {
            await this.acceso.solicitar(`/api/salas/${sala.id}/abandonar`, 'POST');
        }
        await this.desconectar();
        sessionStorage.removeItem('tetrisSala');
    }

    async desconectar(id?: string): Promise<void> {
        if (id && id !== this.salaId) {
            return;
        }
        this.salaId = undefined;
        const anterior = this.conexion;
        this.conexion = undefined;
        this.conectado.set(false);
        this.audio.suspenderPartida();
        await this.escuchaApp?.remove();
        this.escuchaApp = undefined;
        if (anterior) {
            anterior.off('EstadoActualizado');
            anterior.off('SalaCerrada');
            await anterior.stop();
        }
        this.sala.set(null);
        this.secuencia = 0;
    }

    private fallo(error: unknown): void {
        this.conectado.set(false);
        this.audio.suspenderPartida();
        this.mensajeConexion.set(mensajeParaJugador(error, 'Falló la conexión a la sala.'));
    }
}
