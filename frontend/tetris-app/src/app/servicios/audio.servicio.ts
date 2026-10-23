import { Injectable, signal } from '@angular/core';
import { ISala, ITablero } from '../modelos/tetris.model';

export type MusicaJuego = 'menu' | 'partida';
export type EfectoAudio = 'movimiento' | 'rotacion' | 'caida' | 'bloqueo' | 'lineas'
    | 'variasLineas' | 'ataque' | 'nivel' | 'clic' | 'victoria' | 'derrota';

export const archivosAudio = {
    musica: {
        menu: 'assets/audio/musica/menu_y_espera.mp3',
        partida: 'assets/audio/musica/partida.mp3'
    },
    efectos: {
        movimiento: 'mover_pieza.mp3', rotacion: 'girar_pieza.mp3', caida: 'caida_rapida.mp3',
        bloqueo: 'colocar_pieza.mp3', lineas: 'eliminar_filas.mp3', variasLineas: 'eliminar_varias_filas.mp3',
        ataque: 'ataque_recibido.mp3', nivel: 'subir_nivel.mp3', clic: 'clic_boton.mp3',
        victoria: 'victoria.mp3', derrota: 'derrota.mp3'
    }
};

export interface PreferenciasAudio {
    musicaActiva: boolean;
    volumenMusica: number;
    efectosActivos: boolean;
    volumenEfectos: number;
}

const preferenciasIniciales: PreferenciasAudio = {
    musicaActiva: true, volumenMusica: 35, efectosActivos: true, volumenEfectos: 65
};

@Injectable({ providedIn: 'root' })
export class ServicioAudio {
    readonly preferencias = signal<PreferenciasAudio>({ ...preferenciasIniciales });
    readonly requiereInteraccion = signal(true);
    private jugadorId: number | null | undefined;
    private musica?: HTMLAudioElement;
    private pista: MusicaJuego = 'menu';
    private fuente?: MusicaJuego;
    private reproduciendo = false;
    private habilitado = false;
    private aplicacionOculta = false;
    private partidaPausada = false;
    private reproducciones = new Map<EfectoAudio, HTMLAudioElement>();
    private ultimosSonidos = new Map<EfectoAudio, number>();
    private ronda = '';
    private eventos = new Map<string, number>();
    private niveles = new Map<string, number>();
    private ataque = 0;
    private derrotaEscuchada = false;
    private resultadoEscuchado = false;

    usarJugador(usuarioId: number | null): void {
        if (this.jugadorId === usuarioId) {
            return;
        }
        this.jugadorId = usuarioId;
        let guardadas: Partial<PreferenciasAudio> = {};
        try {
            guardadas = JSON.parse(localStorage.getItem(this.clavePreferencias()) || '{}');
        } catch (error) {
            console.warn('No se pudieron leer las preferencias de audio.', error);
        }
        this.preferencias.set({
            musicaActiva: typeof guardadas?.musicaActiva === 'boolean' ? guardadas.musicaActiva : true,
            efectosActivos: typeof guardadas?.efectosActivos === 'boolean' ? guardadas.efectosActivos : true,
            volumenMusica: this.volumenValido(guardadas?.volumenMusica, 35),
            volumenEfectos: this.volumenValido(guardadas?.volumenEfectos, 65)
        });
        this.aplicarVolumen();
    }

    ajustar(cambios: Partial<PreferenciasAudio>): void {
        const nuevas = { ...this.preferencias(), ...cambios };
        nuevas.volumenMusica = this.volumenValido(nuevas.volumenMusica, 35);
        nuevas.volumenEfectos = this.volumenValido(nuevas.volumenEfectos, 65);
        this.preferencias.set(nuevas);
        try {
            localStorage.setItem(this.clavePreferencias(), JSON.stringify(nuevas));
        } catch (error) {
            console.warn('No se pudieron guardar las preferencias de audio.', error);
        }
        this.aplicarVolumen();
    }

    desbloquear(): void {
        if (this.habilitado && !this.requiereInteraccion()) {
            return;
        }
        this.habilitado = true;
        this.requiereInteraccion.set(false);
        this.aplicarVolumen();
    }

    cambiarMusica(pista: MusicaJuego): void {
        this.pista = pista;
        this.partidaPausada = false;
        this.aplicarVolumen();
    }

    suspenderAplicacion(oculta: boolean): void {
        this.aplicacionOculta = oculta;
        if (oculta) {
            this.detenerEfectos();
        }
        this.aplicarVolumen();
    }

    suspenderPartida(): void {
        if (this.pista === 'partida') {
            this.partidaPausada = true;
            this.detenerEfectos();
            this.aplicarVolumen();
        }
    }

    reproducirEfecto(efecto: EfectoAudio): void {
        const opciones = this.preferencias();
        if (!this.habilitado || !opciones.efectosActivos || opciones.volumenEfectos === 0 || this.aplicacionOculta) {
            return;
        }
        if (this.partidaPausada && efecto !== 'clic') {
            return;
        }
        const ahora = Date.now();
        const intervalo = efecto === 'movimiento' ? 95 : 45;
        if (ahora - (this.ultimosSonidos.get(efecto) ?? -Infinity) < intervalo) {
            return;
        }
        this.ultimosSonidos.set(efecto, ahora);
        let audio = this.reproducciones.get(efecto);
        if (!audio) {
            audio = new Audio(`assets/audio/efectos/${archivosAudio.efectos[efecto]}`);
            audio.preload = 'auto';
            this.reproducciones.set(efecto, audio);
        }
        // Cada efecto tiene una sola voz. No se crean copias al mantener un botón pulsado.
        audio.pause();
        audio.currentTime = 0;
        audio.volume = opciones.volumenEfectos / 100 * (efecto === 'movimiento' ? 0.22 : 1);
        void audio.play().catch(error => {
            if (error?.name === 'NotAllowedError') {
                this.requiereInteraccion.set(true);
            }
            console.debug('No se pudo reproducir un efecto de audio.', error);
        });
    }

    actualizarSala(sala: ISala, usuarioId: number, enVivo: boolean): void {
        const clave = `${sala.id}:${sala.numeroRonda}`;
        const nuevaRonda = this.ronda !== clave;
        if (nuevaRonda) {
            this.ronda = clave;
            this.eventos.clear();
            this.niveles.clear();
            this.ataque = 0;
            this.derrotaEscuchada = false;
            this.resultadoEscuchado = false;
        }
        this.pista = sala.estado === 'EnCurso' ? 'partida' : 'menu';
        this.partidaPausada = sala.estado === 'EnCurso' && sala.pausa.fase === 'Pausada';
        if (this.partidaPausada) {
            this.detenerEfectos();
        }
        this.aplicarVolumen();
        const jugador = sala.jugadores.find(j => j.usuarioId === usuarioId);
        const reproducir = enVivo && !nuevaRonda && !this.partidaPausada;
        const tableros = (sala.tableros || []).filter(t => sala.modo === 'Compartido' || t.usuarioId === usuarioId);
        for (const tablero of tableros) {
            this.procesarTablero(tablero, reproducir && !jugador?.perdido && sala.estado === 'EnCurso');
        }
        const activacion = sala.ultimoEfecto;
        if (activacion && activacion.secuencia > this.ataque) {
            if (reproducir && activacion.afectadoId === usuarioId && activacion.atacanteId !== usuarioId
                && activacion.estado === 'Aplicado' && ['Confusion', 'Acelerador'].includes(activacion.tipo)) {
                this.reproducirEfecto('ataque');
            }
            this.ataque = activacion.secuencia;
        }
        if (jugador?.perdido && !this.derrotaEscuchada) {
            if (reproducir) {
                this.reproducirEfecto('derrota');
            }
            this.derrotaEscuchada = true;
        }
        if (sala.estado === 'Finalizada' && !this.resultadoEscuchado) {

            if (reproducir && sala.modo !== 'Individual' && sala.ganadores.includes(usuarioId)) {                this.reproducirEfecto('victoria');
            }
            this.resultadoEscuchado = true;
        }
    }

    private procesarTablero(tablero: ITablero, reproducir: boolean): void {
        const clave = String(tablero.usuarioId ?? 'compartido');
        const evento = tablero.evento;
        const anterior = this.eventos.get(clave) ?? 0;
        if (evento && evento.secuencia > anterior) {
            if (reproducir) {
                if (evento.sonidos.includes('movimiento')) {
                    this.reproducirEfecto('movimiento');
                }
                if (evento.sonidos.includes('rotacion')) {
                    this.reproducirEfecto('rotacion');
                }
                if (evento.sonidos.includes('caida')) {
                    this.reproducirEfecto('caida');
                }
                if (evento.sonidos.includes('bloqueo')) {
                    this.reproducirEfecto('bloqueo');
                }
                if (evento.sonidos.includes('lineas')) {
                    this.reproducirEfecto(evento.lineasEliminadas > 1 ? 'variasLineas' : 'lineas');
                }
                // 'especial' se ignora: activar una pieza nunca genera un sonido propio.
            }
            this.eventos.set(clave, evento.secuencia);
        }
        const nivelAnterior = this.niveles.get(clave) ?? tablero.nivel;
        if (reproducir && tablero.nivel > nivelAnterior) {
            this.reproducirEfecto('nivel');
        }
        this.niveles.set(clave, tablero.nivel);
    }

    private aplicarVolumen(): void {
        const opciones = this.preferencias();
        if (!opciones.efectosActivos) {
            this.detenerEfectos();
        }
        for (const [efecto, audio] of this.reproducciones) {
            audio.volume = opciones.volumenEfectos / 100 * (efecto === 'movimiento' ? 0.22 : 1);
        }
        const debeSonar = this.habilitado && opciones.musicaActiva && opciones.volumenMusica > 0
            && !this.aplicacionOculta && !this.partidaPausada;
        if (!debeSonar) {
            this.musica?.pause();
            this.reproduciendo = false;
            return;
        }
        this.musica ??= new Audio();
        this.musica.loop = true;
        this.musica.volume = opciones.volumenMusica / 100;
        if (this.fuente !== this.pista) {
            this.musica.pause();
            this.musica.src = archivosAudio.musica[this.pista];
            this.musica.currentTime = 0;
            this.fuente = this.pista;
            this.reproduciendo = false;
        }
        if (!this.reproduciendo) {
            this.reproduciendo = true;
            const fuente = this.fuente;
            void this.musica.play().catch(error => {
                if (fuente === this.fuente) {
                    this.reproduciendo = false;
                    if (error?.name === 'NotAllowedError') {
                        this.requiereInteraccion.set(true);
                    }
                }
                console.debug('No se pudo iniciar la música.', error);
            });
        }
    }

    private detenerEfectos(): void {
        for (const audio of this.reproducciones.values()) {
            audio.pause();
            audio.currentTime = 0;
        }
    }

    private clavePreferencias(): string {
        return `tetris.audio.${this.jugadorId ?? 'invitado'}`;
    }

    private volumenValido(valor: unknown, predeterminado: number): number {
        return typeof valor === 'number' && Number.isFinite(valor) ? Math.min(100, Math.max(0, valor)) : predeterminado;
    }
}
