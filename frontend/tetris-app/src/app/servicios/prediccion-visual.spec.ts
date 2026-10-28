import { describe, expect, it } from 'vitest';
import { PrediccionVisual } from './prediccion-visual';
import { ISala, ITablero } from '../modelos/tetris.model';

function estado(): ISala {
    const tablero: ITablero = { usuarioId: 1, celdas: Array.from({ length: 20 }, () => Array(10).fill(0)),
        pieza: [{ fila: 0, columna: 4 }, { fila: 1, columna: 3 }, { fila: 1, columna: 4 }, { fila: 1, columna: 5 }],
        tipoPieza: 2, fila: 0, columna: 3, rotacion: 0, piezaSecuencia: 1,
        puntuacion: 25, lineas: 0, nivel: 1, perdido: false, piezaEspecial: null, evento: null };
    return { id: 'prueba', numeroRonda: 1, modo: 'Individual', estado: 'EnCurso', usuarioTurno: 1, tableros: [tablero],
        jugadores: [{ usuarioId: 1, ultimaSecuencia: 0, perdido: false }], pausa: { fase: 'Ninguna' }, efectos: [] } as unknown as ISala;
}

describe('Predicción exclusivamente visual reconciliable', () => {
    it('mueve inmediatamente sin modificar puntos, bloques ni el estado autorizado', () => {
        const sala = estado();
        const original = structuredClone(sala);
        const prediccion = new PrediccionVisual();
        const vista = prediccion.actualizar(sala, 1, { secuencia: 1, movimiento: 'Izquierda' })!;
        expect(vista.columna).toBe(2);
        expect(vista.puntuacion).toBe(25);
        expect(sala).toEqual(original);
        expect(vista.celdas).toBe(sala.tableros[0].celdas);
    });
    it('reconcilia una confirmación parcial sin aplicar dos veces la primera orden', () => {
        const sala = estado();
        const prediccion = new PrediccionVisual();
        const primera = prediccion.actualizar(sala, 1, { secuencia: 1, movimiento: 'Izquierda' })!;
        prediccion.actualizar(sala, 1, { secuencia: 2, movimiento: 'Bajar' });
        sala.jugadores[0].ultimaSecuencia = 1;
        sala.tableros[0] = primera;
        const vista = prediccion.actualizar(sala, 1)!;
        expect(vista.columna).toBe(2);
        expect(vista.fila).toBe(1);
    });
    it('rotación respeta origen 4x4 y colisiones; los rechazos vuelven al servidor', () => {
        const sala = estado();
        const prediccion = new PrediccionVisual();
        const rotada = prediccion.actualizar(sala, 1, { secuencia: 1, movimiento: 'Rotar' })!;
        expect(rotada.pieza).toContainEqual({ fila: 1, columna: 6 });
        expect(rotada.rotacion).toBe(1);
        prediccion.rechazar(1);
        expect(prediccion.actualizar(sala, 1)?.pieza).toEqual(sala.tableros[0].pieza);
        sala.tableros[0].celdas[1][2] = 3;
        expect(prediccion.actualizar(sala, 1, { secuencia: 2, movimiento: 'Izquierda' })?.columna).toBe(3);
    });
    it('no fija ni emite eventos de aterrizaje provisional al caer', () => {
        const sala = estado();
        const prediccion = new PrediccionVisual();
        const vista = prediccion.actualizar(sala, 1, { secuencia: 1, movimiento: 'Caer' });
        expect(vista?.pieza).toEqual(sala.tableros[0].pieza);
        expect(vista?.evento).toBeNull();
        expect(prediccion.esperandoFijacion).toBe(true);
        sala.tableros[0].piezaSecuencia = 2;
        expect(prediccion.actualizar(sala, 1)?.pieza).toEqual(sala.tableros[0].pieza);
        expect(prediccion.esperandoFijacion).toBe(false);
    });
    it('descarta todo al cambiar de turno, pieza, ronda, pausa o control', () => {
        const sala = estado();
        sala.modo = 'Compartido';
        const prediccion = new PrediccionVisual();
        prediccion.actualizar(sala, 1, { secuencia: 1, movimiento: 'Izquierda' });
        sala.usuarioTurno = 2;
        expect(prediccion.actualizar(sala, 1)).toBeNull();
        sala.usuarioTurno = 1;
        expect(prediccion.actualizar(sala, 1)?.columna).toBe(3);
        sala.pausa.fase = 'Pausada';
        expect(prediccion.actualizar(sala, 1)).toBeNull();
    });
});
