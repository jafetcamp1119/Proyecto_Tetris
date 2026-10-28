import { mensajeParaJugador } from './mensajes-jugador';
import { Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, HttpTransportType, LogLevel } from '@microsoft/signalr';
import { App } from '@capacitor/app';
import { Capacitor, PluginListenerHandle } from '@capacitor/core';
import { ICrearSala, IMovimiento, IRespuesta, ISala, ISalaPublica, ITablero, IVistaPreviaPrivada, Movimiento } from '../modelos/tetris.model';
import { AutenticacionServicio } from './autenticacion.servicio';
import { ApiServicio, ErrorApi } from './api.servicio';
import { ServicioAudio } from './audio.servicio';
import { RendimientoServicio } from './rendimiento.servicio';
import { PrediccionVisual } from './prediccion-visual';

@Injectable({ providedIn: 'root' })
export class SalasServicio {
    private readonly acceso = inject(AutenticacionServicio);
    private readonly api = inject(ApiServicio);
    private readonly audio = inject(ServicioAudio);
    private readonly rendimiento = inject(RendimientoServicio);
    private conexion?: HubConnection;
    private intentoConexion = 0;
    private salaId?: string;
    private escuchaApp?: PluginListenerHandle;
    private secuencia = 0;
    private readonly prediccion = new PrediccionVisual();
    private plazoPrediccion?: ReturnType<typeof setTimeout>;
    readonly tableroVisual = signal<ITablero | null>(null);
    private readonly movimientosPendientes = new Set<{ secuencia: number; inicio: number; confirmado: boolean }>();
    private readonly maximoMovimientosPendientes = 8;
    readonly sala = signal<ISala | null>(null);
    readonly vistaPrevia = signal<IVistaPreviaPrivada | null>(null);
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
        const intento = ++this.intentoConexion;
        await this.cerrarConexionActual();
        if (intento !== this.intentoConexion) { return; }
        this.salaId = id;
        try {
            const inicial = await this.acceso.solicitar<ISala>(`/api/salas/${id}`);
            if (intento !== this.intentoConexion) { return; }
            // Mostrar el código autorizado sin esperar la negociación de SignalR.
            this.actualizar(inicial);
        } catch (error) {
            if (intento !== this.intentoConexion) { return; }
            if (error instanceof ErrorApi && [400, 404].includes(error.estado)) {
                sessionStorage.removeItem('tetrisSala');
            }
            throw error;
        }
        if (intento !== this.intentoConexion) { return; }
        this.mensajeConexion.set('Conectando con la sala...');
        const tokenConexion = this.acceso.sesion()?.token;
        const conexion = new HubConnectionBuilder()
            .withUrl(`${this.api.url()}/hubs/juego`, {
                accessTokenFactory: () => this.acceso.token(),
                // Quick Tunnels no admite SSE. Mantener negociación y long polling.
                transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling
            })
            .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
            .configureLogging(this.rendimiento.habilitado ? this.rendimiento : LogLevel.None)
            .build();
        this.conexion = conexion;
        conexion.on('SesionReemplazada', () => {
            if (this.conexion !== conexion) { return; }
            if (this.acceso.sesion()?.token !== tokenConexion) { return; }
            this.intentoConexion++;
            this.conectado.set(false);
            this.movimientosPendientes.clear();
            this.audio.suspenderPartida();
            this.acceso.perderControl('SesionReemplazada', tokenConexion);
            this.mensajeConexion.set('Tu cuenta se inicio en otro dispositivo. Esta sesion se cerro.');
            this.conexion = undefined;
            void conexion.stop();
        });
        conexion.on('SesionTerminada', (datos: { codigo?: string }) => {
            if (this.conexion === conexion) { this.acceso.perderControl(datos.codigo, tokenConexion); }
        });
        conexion.on('VistaPreviaActualizada', (vista: IVistaPreviaPrivada) => {
            if (this.conexion === conexion && vista.salaId === id) {
                this.actualizarVistaPrevia(vista);
            }
        });
        conexion.on('EstadoActualizado', (sala: ISala) => {
            if (this.conexion === conexion && sala.id === id) {
                this.actualizar(sala);
            }
        });
        conexion.on('SalaCerrada', (datos: { salaId: string; motivo: string }) => {
            if (datos.salaId === id && this.conexion === conexion) {
                this.audio.cambiarMusica('menu');
                this.vistaPrevia.set(null);
                this.mensajeConexion.set('La persona que creó la sala la cerró. Podés buscar otra partida.');
                this.sala.update(sala => sala ? { ...sala, estado: 'Cancelada' } : null);
            }
        });
        const entrar = async (): Promise<void> => {
            if (this.conexion !== conexion) { return; }
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
            if (this.conexion !== conexion) { return; }
            this.rendimiento.contar('reconexiones');
            this.suspenderPrediccion();
            this.movimientosPendientes.clear();
            this.conectado.set(false);
            this.audio.suspenderPartida();
            this.mensajeConexion.set('Reconectando. Los controles están bloqueados.');
        });
        conexion.onreconnected(() => {
            if (this.conexion !== conexion) { return; }
            void entrar().catch(error => { if (this.conexion === conexion) { this.fallo(error); } });
        });
        conexion.onclose(() => {
            if (this.conexion === conexion) {
                this.suspenderPrediccion();
                this.conectado.set(false);
                this.audio.suspenderPartida();
                this.mensajeConexion.set('Se perdió la conexión. Intentá volver a la partida.');
            }
        });
        try {
            await conexion.start();
            if (intento !== this.intentoConexion) { await conexion.stop(); return; }
            await entrar();
        } catch (error) {
            if (intento !== this.intentoConexion) { return; }
            this.fallo(error);
            throw error;
        }
        if (Capacitor.isNativePlatform()) {
            const escucha = await App.addListener('appStateChange', ({ isActive }) => {
                if (isActive && conexion.state === HubConnectionState.Disconnected && this.conexion === conexion) {
                    void conexion.start().then(entrar).catch(error => { if (this.conexion === conexion) { this.fallo(error); } });
                }
            });
            if (intento === this.intentoConexion) { this.escuchaApp = escucha; }
            else { await escucha.remove(); }
        }
    }

    actualizar(sala: ISala): void {
        if (this.salaId && this.salaId !== sala.id) {
            return;
        }
        const actual = this.sala();
        const usuarioId = this.acceso.sesion()?.usuarioId;
        const secuenciaActual = actual?.jugadores.find(j => j.usuarioId === usuarioId)?.ultimaSecuencia ?? 0;
        const secuenciaRecibida = sala.jugadores.find(j => j.usuarioId === usuarioId)?.ultimaSecuencia ?? 0;
        if (actual?.id === sala.id && (actual.version > sala.version || (actual.version === sala.version && secuenciaRecibida <= secuenciaActual))) {
            this.rendimiento.contar('estados_descartados');
            return;
        }
        this.confirmarMovimientos(sala);
        this.tableroVisual.set(this.prediccion.actualizar(sala, this.acceso.sesion()?.usuarioId ?? 0));
        const inicio = this.rendimiento.iniciar();
        this.secuencia = Math.max(this.secuencia, sala.jugadores.find(j => j.usuarioId === this.acceso.sesion()?.usuarioId)?.ultimaSecuencia || 0);
        this.audio.actualizarSala(sala, this.acceso.sesion()?.usuarioId || 0, this.conectado());
        const vista = this.vistaPrevia();
        if (sala.estado !== 'EnCurso' || sala.jugadores.find(j => j.usuarioId === this.acceso.sesion()?.usuarioId)?.perdido
            || (vista && (vista.salaId !== sala.id || vista.numeroRonda < sala.numeroRonda))) {
            this.vistaPrevia.set(null);
        }
        this.sala.set(sala);
        this.rendimiento.contar('estados_aplicados');
        this.rendimiento.finalizar('estado_procesamiento_ms', inicio);
    }

    actualizarVistaPrevia(vista: IVistaPreviaPrivada): void {
        const sala = this.sala();
        const usuarioId = this.acceso.sesion()?.usuarioId;
        if (vista.usuarioId !== usuarioId || vista.salaId !== (this.salaId ?? sala?.id)) {
            return;
        }
        if (sala && (vista.numeroRonda < sala.numeroRonda || vista.version < sala.version
            || sala.jugadores.find(j => j.usuarioId === usuarioId)?.perdido || sala.estado === 'Cancelada' || sala.estado === 'Finalizada')) {
            return;
        }
        const anterior = this.vistaPrevia();
        if (anterior && vista.numeroRonda === anterior.numeroRonda && vista.version <= anterior.version) {
            return;
        }
        this.vistaPrevia.set(vista);
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
        if (!this.puedeMover() || !this.conexion) {
            return;
        }
        if (this.movimientosPendientes.size >= this.maximoMovimientosPendientes || this.prediccion.esperandoFijacion) {
            this.rendimiento.contar('controles_limitados');
            return;
        }
        const conexion = this.conexion;
        this.rendimiento.marcarEntrada();
        const sala = this.sala()!;
        const datos: IMovimiento = { movimiento, secuencia: ++this.secuencia };
        const pendiente = { secuencia: datos.secuencia, inicio: this.rendimiento.iniciar(), confirmado: false };
        this.movimientosPendientes.add(pendiente);
        this.rendimiento.maximo('maximo_movimientos_pendientes', this.movimientosPendientes.size);
        this.tableroVisual.set(this.prediccion.actualizar(sala, this.acceso.sesion()!.usuarioId, datos));
        clearTimeout(this.plazoPrediccion);
        this.plazoPrediccion = setTimeout(() => this.suspenderPrediccion(), 2000);
        this.rendimiento.contar('controles_enviados');
        try {
            // La vista provisional no modifica el estado autorizado; SignalR conserva el orden.
            const respuesta = await conexion.invoke<IRespuesta<ISala>>('Mover', sala.id, datos);
            if (this.conexion !== conexion || !this.conectado() || this.sala()?.numeroRonda !== sala.numeroRonda) { return; }
            if (!respuesta.success || !respuesta.data) {
                throw new Error(respuesta.error || 'Movimiento rechazado.');
            }
            this.actualizar(respuesta.data);
        } catch (error) {
            if (this.conexion === conexion) {
                this.prediccion.rechazar(datos.secuencia);
                this.tableroVisual.set(null);
            }
            throw error;
        } finally {
            this.movimientosPendientes.delete(pendiente);
            if (!this.movimientosPendientes.size) { clearTimeout(this.plazoPrediccion); }
            this.rendimiento.finalizar('signalr_respuesta_ms', pendiente.inicio);
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
        this.intentoConexion++;
        await this.cerrarConexionActual();
    }

    private async cerrarConexionActual(): Promise<void> {
        this.suspenderPrediccion();
        this.salaId = undefined;
        this.vistaPrevia.set(null);
        const anterior = this.conexion;
        this.conexion = undefined;
        this.movimientosPendientes.clear();
        this.conectado.set(false);
        this.audio.suspenderPartida();
        const escucha = this.escuchaApp;
        this.escuchaApp = undefined;
        this.sala.set(null);
        this.secuencia = 0;
        await escucha?.remove();
        if (anterior) {
            anterior.off('EstadoActualizado');
            anterior.off('SalaCerrada');
            anterior.off('VistaPreviaActualizada');
            anterior.off('SesionReemplazada');
            anterior.off('SesionTerminada');
            await anterior.stop();
        }
    }

    private fallo(error: unknown): void {
        this.suspenderPrediccion();
        this.conectado.set(false);
        this.audio.suspenderPartida();
        this.mensajeConexion.set(mensajeParaJugador(error, 'Falló la conexión a la sala.'));
    }

    suspenderPrediccion(): void {
        clearTimeout(this.plazoPrediccion);
        this.prediccion.limpiar();
        this.tableroVisual.set(null);
    }

    private confirmarMovimientos(sala: ISala): void {
        const secuencia = sala.jugadores.find(jugador => jugador.usuarioId === this.acceso.sesion()?.usuarioId)?.ultimaSecuencia ?? 0;
        for (const pendiente of this.movimientosPendientes) {
            if (!pendiente.confirmado && secuencia >= pendiente.secuencia) {
                pendiente.confirmado = true;
                this.rendimiento.finalizar('control_confirmacion_ms', pendiente.inicio);
            }
        }
    }
}
