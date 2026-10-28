import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { LogLevel } from '@microsoft/signalr';
import { RendimientoServicio } from './rendimiento.servicio';

describe('Diagnostico de rendimiento voluntario y acotado', () => {
    beforeEach(() => { localStorage.removeItem('tetris.diagnostico.rendimiento'); Reflect.deleteProperty(window, 'tetrisRendimiento'); });
    afterEach(() => { localStorage.removeItem('tetris.diagnostico.rendimiento'); Reflect.deleteProperty(window, 'tetrisRendimiento'); vi.restoreAllMocks(); });

    it('no recopila muestras ni crea un acceso global sin activacion', () => {
        const rendimiento = new RendimientoServicio();
        rendimiento.finalizar('prueba', 0);
        rendimiento.contar('prueba');
        expect(rendimiento.habilitado).toBe(false);
        expect(Reflect.has(window, 'tetrisRendimiento')).toBe(false);
        expect(rendimiento.resumen()).toEqual({ transporte: 'Sin comprobar', contadores: {}, tiempos: {} });
    });

    it('limita a 120 muestras y no conserva mensajes ni tokens de SignalR', () => {
        localStorage.setItem('tetris.diagnostico.rendimiento', '1');
        const rendimiento = new RendimientoServicio();
        vi.spyOn(performance, 'now').mockReturnValue(10);
        for (let indice = 0; indice < 300; indice++) { rendimiento.finalizar('control_ms', 0); }
        rendimiento.log(LogLevel.Information, 'WebSocket connected to https://ejemplo/?access_token=secreto');
        const resumen = JSON.stringify(rendimiento.resumen());
        expect(resumen).toContain('"muestras":120');
        expect(resumen).toContain('WebSockets');
        expect(resumen).not.toContain('secreto');
        expect(resumen).not.toContain('ejemplo');
    });

    it('distingue un fallback exitoso a LongPolling de un transporte solo seleccionado', () => {
        localStorage.setItem('tetris.diagnostico.rendimiento', '1');
        const rendimiento = new RendimientoServicio();
        rendimiento.log(LogLevel.Debug, "Selecting transport 'LongPolling'.");
        expect(JSON.stringify(rendimiento.resumen())).toContain('Sin comprobar');
        rendimiento.log(LogLevel.Debug, 'The HttpConnection connected successfully.');
        expect(JSON.stringify(rendimiento.resumen())).toContain('LongPolling');
    });
});
