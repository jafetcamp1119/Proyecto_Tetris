import { TestBed } from '@angular/core/testing';
import { ElementRef, signal } from '@angular/core';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SalasServicio } from './salas.servicio';
import { ServicioAudio } from './audio.servicio';
import { AutenticacionServicio } from './autenticacion.servicio';
import { ApiServicio } from './api.servicio';
import { IMovimiento, ISala, ITablero } from '../modelos/tetris.model';
import { TableroComponent } from '../componentes/tablero.component';

function estado(): ISala {
    return {
        id: 'medicion', version: 1, numeroRonda: 1, estado: 'EnCurso', modo: 'Individual',
        pausa: { fase: 'Ninguna' }, tableros: [],
        jugadores: [{ usuarioId: 1, perdido: false, abandono: false, ultimaSecuencia: 0 }]
    } as unknown as ISala;
}

function prepararSala(): { salas: SalasServicio; actualizarAudio: ReturnType<typeof vi.fn> } {
    const actualizarAudio = vi.fn();
    TestBed.configureTestingModule({ providers: [
        { provide: AutenticacionServicio, useValue: { sesion: signal({ usuarioId: 1 }) } },
        { provide: ApiServicio, useValue: {} },
        { provide: ServicioAudio, useValue: { actualizarSala: actualizarAudio } }
    ] });
    const salas = TestBed.inject(SalasServicio);
    salas.conectado.set(true);
    salas.actualizar(estado());
    return { salas, actualizarAudio };
}

describe('Mediciones controladas de rendimiento', () => {
    it('retira la vista provisional sin confirmación aunque el socket no haya detectado la caída', async () => {
        vi.useFakeTimers();
        const { salas } = prepararSala();
        const tablero = { usuarioId: 1, celdas: Array.from({ length: 20 }, () => Array(10).fill(0)),
            pieza: [{ fila: 1, columna: 3 }], fila: 0, columna: 3, piezaSecuencia: 1, tipoPieza: 0 } as ITablero;
        salas.actualizar({ ...estado(), version: 2, tableros: [tablero] });
        Object.assign(salas, { conexion: { invoke: vi.fn(() => new Promise(resolve => setTimeout(() => resolve({ success: true, data: { ...estado(), version: 3 } }), 3000))) } });
        const movimiento = salas.mover('Izquierda');
        expect(salas.tableroVisual()?.columna).toBe(2);
        await vi.advanceTimersByTimeAsync(2100);
        expect(salas.tableroVisual()).toBeNull();
        expect(salas.sala()?.tableros[0].columna).toBe(3);
        await vi.advanceTimersByTimeAsync(900);
        await movimiento;
    });
    it('compara la vista inmediata con esperar la respuesta en un RTT simulado de 1000 ms', async () => {
        vi.useFakeTimers();
        const { salas } = prepararSala();
        const tablero = { usuarioId: 1, celdas: Array.from({ length: 20 }, () => Array(10).fill(0)),
            pieza: [{ fila: 1, columna: 3 }], fila: 0, columna: 3, piezaSecuencia: 1, tipoPieza: 0, puntuacion: 0 } as ITablero;
        salas.actualizar({ ...estado(), version: 2, tableros: [tablero] });
        const respuesta = { ...estado(), version: 3, tableros: [{ ...tablero, columna: 2, pieza: [{ fila: 1, columna: 2 }] }] };
        Object.assign(salas, { conexion: { invoke: vi.fn(() => new Promise(resolve => setTimeout(() => resolve({ success: true, data: respuesta }), 1000))) } });
        const movimiento = salas.mover('Izquierda');
        await vi.advanceTimersByTimeAsync(16);
        expect(salas.tableroVisual()?.columna).toBe(2);
        expect(salas.sala()?.tableros[0].columna).toBe(3);
        await vi.advanceTimersByTimeAsync(984);
        await movimiento;
        expect(salas.sala()?.tableros[0].columna).toBe(2);
        console.info('MEDICION_CONTROLADA RTT_ms=1000 vista_anterior_disponible_ms=1000 vista_provisional_antes_del_primer_frame_ms=16');
    });
    afterEach(() => {
        vi.useRealTimers();
        vi.unstubAllGlobals();
        vi.restoreAllMocks();
    });

    it('mide diez entradas a 100 ms con respuestas simuladas de 1000 ms', async () => {
        vi.useFakeTimers();
        const { salas } = prepararSala();
        const invocar = vi.fn((_metodo: string, _id: string, datos: IMovimiento) => {
            return new Promise(resolve => {
                setTimeout(() => resolve({ success: true, data: { ...estado(), version: datos.secuencia + 1 } }), 1000);
            });
        });
        Object.assign(salas, { conexion: { invoke: invocar } });
        const pendientes: Promise<void>[] = [];
        for (let indice = 0; indice < 10; indice++) {
            pendientes.push(salas.mover('Derecha'));
            await vi.advanceTimersByTimeAsync(100);
        }
        await vi.advanceTimersByTimeAsync(1000);
        await Promise.all(pendientes);
        console.info('MEDICION entradas=10 RTT_simulado_ms=1000 enviadas=' + invocar.mock.calls.length);
        expect(invocar.mock.calls.length).toBe(8);
    });

    it('mide procesamiento de veinte copias de la misma versión', () => {
        const { salas, actualizarAudio } = prepararSala();
        actualizarAudio.mockClear();
        for (let indice = 0; indice < 20; indice++) {
            salas.actualizar(structuredClone(estado()));
        }
        console.info('MEDICION estados_duplicados=20 procesados=' + actualizarAudio.mock.calls.length);
        expect(actualizarAudio).not.toHaveBeenCalled();
    });

    it('mide trabajo de canvas para diez notificaciones en un cuadro', () => {
        const cuadros: FrameRequestCallback[] = [];
        vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => { cuadros.push(callback); return cuadros.length; });
        vi.stubGlobal('cancelAnimationFrame', vi.fn());
        const contexto = { fillRect: vi.fn(), strokeRect: vi.fn(), fillText: vi.fn(), clearRect: vi.fn() };
        const lienzo = document.createElement('canvas');
        vi.spyOn(lienzo, 'getContext').mockReturnValue(contexto as unknown as CanvasRenderingContext2D);
        const componente = TestBed.runInInjectionContext(() => new TableroComponent());
        componente.lienzo = new ElementRef(lienzo);
        const tablero = { celdas: Array.from({ length: 20 }, () => Array(10).fill(0)), pieza: [], tipoPieza: 0, nivel: 1, puntuacion: 0 } as unknown as ITablero;
        for (let indice = 0; indice < 10; indice++) {
            componente.tablero = structuredClone(tablero);
            componente.ngOnChanges();
        }
        cuadros.forEach(callback => callback(0));
        console.info('MEDICION notificaciones_canvas=10 celdas_pintadas=' + contexto.fillRect.mock.calls.length);
        expect(contexto.fillRect.mock.calls.length).toBe(200);
        componente.ngOnChanges();
        cuadros[1](16);
        expect(contexto.fillRect.mock.calls.length).toBe(200);
        componente.ngOnDestroy();
    });

    it('no modifica el estado autorizado y descarta respuestas atrasadas o de una conexión anterior', async () => {
        const { salas } = prepararSala();
        const respuestas: ((valor: unknown) => void)[] = [];
        const conexion = { invoke: vi.fn(() => new Promise(resolve => respuestas.push(resolve))) };
        Object.assign(salas, { conexion });
        const primero = salas.mover('Derecha');
        const segundo = salas.mover('Rotar');
        expect(salas.sala()?.version).toBe(1);
        expect(conexion.invoke.mock.calls).toHaveLength(2);
        respuestas[1]({ success: true, data: { ...estado(), version: 3 } });
        await segundo;
        respuestas[0]({ success: true, data: { ...estado(), version: 2 } });
        await primero;
        expect(salas.sala()?.version).toBe(3);
        const anterior = salas.mover('Izquierda');
        Object.assign(salas, { conexion: {} });
        respuestas[2]({ success: true, data: { ...estado(), version: 4 } });
        await anterior;
        expect(salas.sala()?.version).toBe(3);
    });
});
