import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ISala, ITablero } from '../modelos/tetris.model';
import { ServicioAudio } from './audio.servicio';

class AudioSimulado {
    static instancias: AudioSimulado[] = [];
    src = '';
    loop = false;
    volume = 1;
    currentTime = 0;
    preload = '';
    paused = true;
    play = vi.fn(() => {
        this.paused = false;
        return Promise.resolve();
    });
    pause = vi.fn(() => { this.paused = true; });

    constructor(src = '') {
        this.src = src;
        AudioSimulado.instancias.push(this);
    }
}

function salaInicial(): ISala {
    return {
        id: 'sala-audio', numeroRonda: 1, modo: 'Compartido', estado: 'Esperando',
        jugadores: [{ usuarioId: 1, perdido: false }, { usuarioId: 2, perdido: false }],
        tableros: [{ usuarioId: null, nivel: 1, evento: null }],
        pausa: { fase: 'Ninguna' }, ganadores: [], ultimoEfecto: null
    } as unknown as ISala;
}

describe('Audio del juego', () => {
    let audio: ServicioAudio;
    const efectos = () => AudioSimulado.instancias.filter(instancia => instancia.src.includes('/efectos/'));

    beforeEach(() => {
        localStorage.clear();
        AudioSimulado.instancias = [];
        vi.stubGlobal('Audio', AudioSimulado);
        vi.useFakeTimers();
        audio = new ServicioAudio();
        audio.usarJugador(1);
    });

    afterEach(() => {
        vi.useRealTimers();
        vi.unstubAllGlobals();
    });

    it('espera una interacción y mantiene una sola canción entre menú y espera', () => {
        audio.cambiarMusica('menu');
        expect(AudioSimulado.instancias).toHaveLength(0);
        audio.desbloquear();
        const musica = AudioSimulado.instancias[0];
        musica.currentTime = 32;
        audio.cambiarMusica('menu');
        audio.actualizarSala(salaInicial(), 1, false);
        expect(AudioSimulado.instancias).toHaveLength(1);
        expect(musica.currentTime).toBe(32);
        expect(musica.loop).toBe(true);
        expect(musica.play).toHaveBeenCalledTimes(1);
    });

    it.each(['Individual', 'Compartido', 'Competitivo'] as const)('usa la canción de partida en %s y vuelve al menú al terminar', modo => {
        audio.desbloquear();
        const sala = salaInicial();
        sala.modo = modo;
        sala.estado = 'EnCurso';
        audio.actualizarSala(sala, 1, true);
        const musica = AudioSimulado.instancias[0];
        expect(musica.src).toContain('musica/partida.mp3');
        sala.estado = 'Finalizada';
        audio.actualizarSala(sala, 1, true);
        expect(musica.src).toContain('musica/menu_y_espera.mp3');
        expect(AudioSimulado.instancias).toHaveLength(1);
    });

    it('pausa música y tablero, y reanuda sin duplicar la canción', () => {
        audio.desbloquear();
        const sala = salaInicial();
        sala.estado = 'EnCurso';
        audio.actualizarSala(sala, 1, true);
        const musica = AudioSimulado.instancias[0];
        musica.currentTime = 14;
        sala.pausa.fase = 'Pausada';
        audio.actualizarSala(sala, 1, true);
        audio.reproducirEfecto('caida');
        expect(musica.paused).toBe(true);
        expect(efectos()).toHaveLength(0);
        sala.pausa.fase = 'Ninguna';
        audio.actualizarSala(sala, 1, true);
        audio.actualizarSala(sala, 1, true);
        expect(musica.paused).toBe(false);
        expect(musica.currentTime).toBe(14);
        expect(musica.play).toHaveBeenCalledTimes(3); // Menú, partida y reanudación.
    });

    it('aplica volúmenes independientes y los recupera por jugador al reabrir', () => {
        audio.desbloquear();
        audio.ajustar({ musicaActiva: false, volumenMusica: 17, volumenEfectos: 80 });
        audio.reproducirEfecto('rotacion');
        expect(AudioSimulado.instancias[0].paused).toBe(true);
        expect(efectos()[0].volume).toBe(0.8);
        const reabierto = new ServicioAudio();
        reabierto.usarJugador(1);
        expect(reabierto.preferencias().volumenMusica).toBe(17);
        expect(reabierto.preferencias().musicaActiva).toBe(false);
        reabierto.usarJugador(2);
        expect(reabierto.preferencias().volumenMusica).toBe(35);
        audio.ajustar({ efectosActivos: false, musicaActiva: true });
        expect(efectos()[0].paused).toBe(true);
        expect(AudioSimulado.instancias[0].paused).toBe(false);
    });

    it('suaviza y limita la repetición del movimiento a una sola voz', () => {
        audio.desbloquear();
        for (let vez = 0; vez < 30; vez++) {
            audio.reproducirEfecto('movimiento');
        }
        expect(efectos()).toHaveLength(1);
        expect(efectos()[0].play).toHaveBeenCalledTimes(1);
        expect(efectos()[0].volume).toBeCloseTo(0.65 * 0.22);
        vi.advanceTimersByTime(100);
        audio.reproducirEfecto('movimiento');
        expect(efectos()).toHaveLength(1);
        expect(efectos()[0].play).toHaveBeenCalledTimes(2);
    });

    it('deduplica estados y respuestas de movimientos por secuencia y no reproduce el historial al reconectar', () => {
        audio.desbloquear();
        const sala = salaInicial();
        sala.estado = 'EnCurso';
        audio.actualizarSala(sala, 1, false);
        sala.tableros[0].evento = { secuencia: 1, sonidos: ['rotacion'], lineasEliminadas: 0, piezaEspecial: null };
        audio.actualizarSala(sala, 1, true);
        vi.advanceTimersByTime(200);
        audio.actualizarSala(sala, 1, true);
        expect(efectos()[0].play).toHaveBeenCalledTimes(1);
        sala.tableros[0].evento.secuencia = 2;
        audio.actualizarSala(sala, 1, false);
        audio.actualizarSala(sala, 1, true);
        expect(efectos()[0].play).toHaveBeenCalledTimes(1);
    });

    it('elige caída, colocación, varias líneas y nivel según el evento confirmado', () => {
        audio.desbloquear();
        const sala = salaInicial();
        sala.estado = 'EnCurso';
        audio.actualizarSala(sala, 1, false);
        sala.tableros[0].nivel = 2;
        sala.tableros[0].evento = { secuencia: 1, sonidos: ['caida', 'bloqueo', 'lineas', 'especial'], lineasEliminadas: 3, piezaEspecial: 'BOMBA' };
        audio.actualizarSala(sala, 1, true);
        expect(efectos().map(efecto => efecto.src.split('/').pop()).sort()).toEqual([
            'caida_rapida.mp3', 'colocar_pieza.mp3', 'eliminar_varias_filas.mp3', 'subir_nivel.mp3'
        ]);
    });

    it('no emite notificaciones de sala ni un sonido de activación especial', () => {
        audio.desbloquear();
        const sala = salaInicial();
        audio.actualizarSala(sala, 1, false);
        sala.jugadores[1].listo = true;
        audio.actualizarSala(sala, 1, true);
        sala.estado = 'EnCurso';
        sala.tableros[0].evento = { secuencia: 1, sonidos: ['especial'], lineasEliminadas: 0, piezaEspecial: 'BARREDORA' };
        audio.actualizarSala(sala, 1, true);
        expect(efectos()).toHaveLength(0);
    });

    it('el ataque suena una sola vez y únicamente para quien lo recibe', () => {
        audio.desbloquear();
        const sala = salaInicial();
        sala.estado = 'EnCurso';
        audio.actualizarSala(sala, 1, false);
        sala.ultimoEfecto = { secuencia: 1, afectadoId: 2, atacanteId: 1, tipo: 'Confusion', estado: 'Aplicado' } as ISala['ultimoEfecto'];
        audio.actualizarSala(sala, 1, true);
        expect(efectos()).toHaveLength(0);
        sala.ultimoEfecto!.secuencia = 2;
        sala.ultimoEfecto!.afectadoId = 1;
        sala.ultimoEfecto!.atacanteId = 2;
        audio.actualizarSala(sala, 1, true);
        vi.advanceTimersByTime(200);
        audio.actualizarSala(sala, 1, true);
        expect(efectos()[0].src).toContain('ataque_recibido.mp3');
        expect(efectos()[0].play).toHaveBeenCalledTimes(1);
    });

    it('en competitivo no reproduce movimientos del tablero observado', () => {
        audio.desbloquear();
        const sala = salaInicial();
        sala.modo = 'Competitivo';
        sala.estado = 'EnCurso';
        sala.tableros[0].usuarioId = 1;
        sala.tableros.push({ usuarioId: 2, nivel: 1, evento: null } as ITablero);
        audio.actualizarSala(sala, 1, false);
        sala.tableros[1].evento = { secuencia: 1, sonidos: ['movimiento'], lineasEliminadas: 0, piezaEspecial: null };
        audio.actualizarSala(sala, 1, true);
        expect(efectos()).toHaveLength(0);
    });

    it('registra derrota y victoria sin repetirlas ni sonar al entrar a resultados históricos', () => {
        audio.desbloquear();
        const sala = salaInicial();
        sala.estado = 'EnCurso';
        audio.actualizarSala(sala, 1, false);
        sala.jugadores[0].perdido = true;
        audio.actualizarSala(sala, 1, true);
        sala.estado = 'Finalizada';
        sala.ganadores = [2];
        audio.actualizarSala(sala, 1, true);
        expect(efectos()).toHaveLength(1);
        expect(efectos()[0].src).toContain('derrota.mp3');
        sala.numeroRonda++;
        sala.estado = 'EnCurso';
        sala.jugadores[0].perdido = false;
        audio.actualizarSala(sala, 1, false);
        sala.estado = 'Finalizada';
        sala.ganadores = [1];
        audio.actualizarSala(sala, 1, true);
        vi.advanceTimersByTime(200);
        audio.actualizarSala(sala, 1, true);
        expect(efectos().find(efecto => efecto.src.includes('victoria'))?.play).toHaveBeenCalledTimes(1);
    });

    it('suspende música y efectos al ocultarse la aplicación', () => {
        audio.desbloquear();
        audio.reproducirEfecto('lineas');
        audio.suspenderAplicacion(true);
        expect(AudioSimulado.instancias.every(instancia => instancia.paused)).toBe(true);
        audio.reproducirEfecto('rotacion');
        expect(efectos()).toHaveLength(1);
        audio.suspenderAplicacion(false);
        expect(AudioSimulado.instancias[0].paused).toBe(false);
    });
});
