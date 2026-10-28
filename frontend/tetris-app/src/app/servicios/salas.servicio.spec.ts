import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { SalasServicio } from './salas.servicio';
import { AutenticacionServicio } from './autenticacion.servicio';
import { ApiServicio } from './api.servicio';
import { ISala, IVistaPreviaPrivada } from '../modelos/tetris.model';

describe('Permisos y sincronización del tablero', () => {
  let salas: SalasServicio;
  const estado = (): ISala =>
    ({
      id: 'sala',
      version: 4,
      numeroRonda: 1,
      estado: 'EnCurso',
      modo: 'Compartido',
      usuarioTurno: 1,
      pausa: { fase: 'Ninguna' },
      jugadores: [{ usuarioId: 1, perdido: false, abandono: false, ultimaSecuencia: 2 }],
    }) as ISala;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AutenticacionServicio, useValue: { sesion: signal({ usuarioId: 1 }) } },
        { provide: ApiServicio, useValue: {} },
      ],
    });
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

  it('acepta solo la vista del jugador, sala y ronda actuales y descarta datos atrasados', () => {
    salas.actualizar(estado());
    const vista: IVistaPreviaPrivada = {
      salaId: 'sala',
      usuarioId: 1,
      numeroRonda: 1,
      version: 4,
      pieza: { tipoPieza: 2, celdas: [], piezaEspecial: null },
    };
    salas.actualizarVistaPrevia(vista);
    expect(salas.vistaPrevia()).toEqual(vista);
    salas.actualizarVistaPrevia({ ...vista, usuarioId: 2, version: 5 });
    salas.actualizarVistaPrevia({ ...vista, salaId: 'otra', version: 5 });
    salas.actualizarVistaPrevia({ ...vista, version: 3, pieza: null });
    expect(salas.vistaPrevia()).toEqual(vista);
    salas.actualizar({ ...estado(), numeroRonda: 2, version: 6 });
    expect(salas.vistaPrevia()).toBeNull();
    salas.actualizarVistaPrevia({ ...vista, version: 7 });
    expect(salas.vistaPrevia()).toBeNull();
    salas.actualizarVistaPrevia({ ...vista, numeroRonda: 2, version: 7 });
    expect(salas.vistaPrevia()?.numeroRonda).toBe(2);
  });

  it('retira la pieza del jugador eliminado y no acepta vistas posteriores de esa partida', () => {
    salas.actualizar(estado());
    const vista: IVistaPreviaPrivada = {
      salaId: 'sala',
      usuarioId: 1,
      numeroRonda: 1,
      version: 4,
      pieza: { tipoPieza: 1, celdas: [], piezaEspecial: null },
    };
    salas.actualizarVistaPrevia(vista);
    expect(salas.vistaPrevia()?.pieza).not.toBeNull();
    salas.actualizar({ ...estado(), version: 5, jugadores: [{ ...estado().jugadores[0], perdido: true }] });
    expect(salas.vistaPrevia()).toBeNull();
    salas.actualizarVistaPrevia({ ...vista, version: 6 });
    expect(salas.vistaPrevia()).toBeNull();
  });
});
