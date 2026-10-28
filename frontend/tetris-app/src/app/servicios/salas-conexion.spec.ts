import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SalasServicio } from './salas.servicio';
import { AutenticacionServicio } from './autenticacion.servicio';
import { ApiServicio } from './api.servicio';
import { ServicioAudio } from './audio.servicio';
import { ISala } from '../modelos/tetris.model';

describe('Una conexion por intento vigente', () => {
    afterEach(() => vi.restoreAllMocks());

    it('un intento lento anterior no crea otro hub ni reemplaza la sala nueva', async () => {
        const respuestas = new Map<string, () => void>();
        const solicitar = vi.fn((ruta: string) => new Promise<ISala>(resolve => {
            respuestas.set(ruta, () => resolve({ ...sala, id: ruta.split('/').at(-1)! }));
        }));
        const sala = { id: 'nueva', version: 1, numeroRonda: 1, estado: 'Esperando', jugadores: [], pausa: {} } as unknown as ISala;
        const conexion = {
            on: vi.fn(), onreconnecting: vi.fn(), onreconnected: vi.fn(), onclose: vi.fn(),
            start: vi.fn(async () => undefined), stop: vi.fn(async () => undefined),
            invoke: vi.fn(async () => ({ success: true, data: sala }))
        };
        const crear = vi.spyOn(HubConnectionBuilder.prototype, 'build').mockReturnValue(conexion as unknown as HubConnection);
        TestBed.configureTestingModule({ providers: [
            { provide: AutenticacionServicio, useValue: { sesion: signal({ usuarioId: 1 }), solicitar, token: async () => 'simulado' } },
            { provide: ApiServicio, useValue: { url: signal('https://prueba.trycloudflare.com') } },
            { provide: ServicioAudio, useValue: { suspenderPartida: vi.fn(), actualizarSala: vi.fn() } }
        ] });
        const servicio = TestBed.inject(SalasServicio);
        const anterior = servicio.conectar('vieja');
        await vi.waitFor(() => expect(respuestas.has('/api/salas/vieja')).toBe(true));
        const actual = servicio.conectar('nueva');
        await vi.waitFor(() => expect(respuestas.has('/api/salas/nueva')).toBe(true));
        respuestas.get('/api/salas/nueva')!();
        await actual;
        respuestas.get('/api/salas/vieja')!();
        await anterior;
        expect(crear).toHaveBeenCalledTimes(1);
        expect(conexion.start).toHaveBeenCalledTimes(1);
        expect(servicio.sala()?.id).toBe('nueva');
    });
});

describe('Código inicial disponible sin depender de SignalR', () => {
    afterEach(() => vi.restoreAllMocks());

    it('conserva el código de HTTP al entrar, recibir estados y reconectar', async () => {
        const sala = { id: 'publica', codigo: 'CP8CQQ', creadorId: 1, privada: false, codigoPrivado: null,
            version: 1, numeroRonda: 1, estado: 'Esperando', jugadores: [], pausa: {} } as unknown as ISala;
        let permitirConexion!: () => void;
        let reconectar!: () => Promise<void>;
        const eventos = new Map<string, (estado: ISala) => void>();
        const conexion = {
            on: vi.fn((nombre, accion) => eventos.set(nombre, accion)),
            onreconnecting: vi.fn(), onreconnected: vi.fn(accion => { reconectar = accion; }), onclose: vi.fn(),
            start: vi.fn(() => new Promise<void>(resolver => { permitirConexion = resolver; })), stop: vi.fn(async () => undefined),
            invoke: vi.fn(async () => ({ success: true, data: sala }))
        };
        vi.spyOn(HubConnectionBuilder.prototype, 'build').mockReturnValue(conexion as unknown as HubConnection);
        TestBed.configureTestingModule({ providers: [
            { provide: AutenticacionServicio, useValue: { sesion: signal({ usuarioId: 1 }), solicitar: vi.fn(async () => sala), token: async () => 'prueba' } },
            { provide: ApiServicio, useValue: { url: signal('https://prueba.trycloudflare.com') } },
            { provide: ServicioAudio, useValue: { suspenderPartida: vi.fn(), actualizarSala: vi.fn() } }
        ] });
        const servicio = TestBed.inject(SalasServicio);
        const entrada = servicio.conectar(sala.id);
        await vi.waitFor(() => expect(conexion.start).toHaveBeenCalled());
        expect(servicio.conectado()).toBe(false);
        expect(servicio.sala()?.codigo).toBe('CP8CQQ');
        permitirConexion(); await entrada;
        eventos.get('EstadoActualizado')!({ ...sala, version: 2, numeroRonda: 2 });
        expect(servicio.sala()?.codigo).toBe('CP8CQQ');
        sala.version = 3; sala.numeroRonda = 2;
        await reconectar();
        await vi.waitFor(() => expect(conexion.invoke).toHaveBeenCalledTimes(2));
        expect(servicio.sala()?.codigo).toBe('CP8CQQ');
        expect(servicio.sala()?.numeroRonda).toBe(2);
    });
});
