import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { SalasServicio } from './salas.servicio';
import { AutenticacionServicio } from './autenticacion.servicio';
import { ApiServicio } from './api.servicio';
import { ISala } from '../modelos/tetris.model';

describe('Permisos y sincronización del tablero', () => {
    let salas: SalasServicio;
    const estado = (): ISala => ({
        id: 'sala', version: 4, estado: 'EnCurso', modo: 'Compartido', usuarioTurno: 1,
        pausa: { fase: 'Ninguna' }, jugadores: [{ usuarioId: 1, perdido: false, abandono: false, ultimaSecuencia: 2 }]
    } as ISala);

    beforeEach(() => {
        TestBed.configureTestingModule({ providers: [
            { provide: AutenticacionServicio, useValue: { sesion: signal({ usuarioId: 1 }) } },
            { provide: ApiServicio, useValue: {} }
        ] });
        salas = TestBed.inject(SalasServicio);
        salas.conectado.set(true);
    });

    it('solo permite mover al jugador activo con turno y conexión', () => {
        salas.actualizar(estado());
        expect(salas.puedeMover()).toBe(true);
        salas.actualizar({ ...estado(), version: 5, usuarioTurno: 2 });
        expect(salas.puedeMover()).toBe(false);
        salas.actualizar({ ...estado(), version: 6, modo: 'Competitivo', usuarioTurno: null });
        expect(salas.puedeMover()).toBe(true);
        salas.conectado.set(false);
        expect(salas.puedeMover()).toBe(false);
    });

    it('bloquea espectadores y pausas e ignora estados de red atrasados', () => {
        const sala = estado();
        salas.actualizar({ ...sala, version: 10, pausa: { ...sala.pausa, fase: 'Pausada' } });
        salas.actualizar({ ...sala, version: 9 });
        expect(salas.sala()?.version).toBe(10);
        expect(salas.puedeMover()).toBe(false);
        salas.actualizar({ ...sala, version: 11, jugadores: [{ ...sala.jugadores[0], perdido: true }] });
        expect(salas.puedeMover()).toBe(false);
    });
});
