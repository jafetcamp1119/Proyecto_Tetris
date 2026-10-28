import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PartidaPage } from './partida.page';
import { SalasServicio } from '../../servicios/salas.servicio';
import { AutenticacionServicio } from '../../servicios/autenticacion.servicio';
import { ApiServicio } from '../../servicios/api.servicio';
import { DialogosServicio } from '../../servicios/dialogos.servicio';
import { OrientacionServicio } from '../../servicios/orientacion.servicio';
import { ISala, ModoJuego } from '../../modelos/tetris.model';
import { configuracionInicial } from '../../componentes/configuracion-sala.component';

function crearPantalla(modo: ModoJuego, privada: boolean, usuarioId: number) {
    const estado = signal({ ...configuracionInicial(), modo, privada, id: 'sala', codigo: 'CP8CQQ',
        codigoPrivado: privada ? 'CP8CQQ' : null, creadorId: 1, numeroRonda: 1, estado: 'Esperando',
        jugadores: [1, 2].map(id => ({ usuarioId: id, nombre: 'Jugador ' + id, perdido: false,
            puntos: 0, listo: false, conectado: true })), tableros: [], ganadores: [], efectos: [],
        pausa: { fase: 'Ninguna', ahoraServidorUtc: new Date().toISOString() }
    } as unknown as ISala);
    TestBed.configureTestingModule({ providers: [provideRouter([]),
        { provide: SalasServicio, useValue: { sala: estado, conectado: signal(true), mensajeConexion: signal('Conectado'),
            vistaPrevia: signal(null), tableroVisual: signal(null), puedeMover: () => false,
            conectar: vi.fn(async () => undefined), desconectar: vi.fn(async () => undefined) } },
        { provide: AutenticacionServicio, useValue: { sesion: signal({ usuarioId }), codigoRecuperacionNuevo: signal(null), solicitar: vi.fn(async () => []) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'sala' }) } } },
        { provide: ApiServicio, useValue: {} }, { provide: DialogosServicio, useValue: {} },
        { provide: OrientacionServicio, useValue: { requiereVertical: signal(false) } }
    ] });
    const fixture = TestBed.createComponent(PartidaPage);
    fixture.detectChanges();
    return { fixture, estado };
}

describe('Código en la pantalla real de anfitrión e invitados', () => {
    const original = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
    afterEach(() => {
        if (original) { Object.defineProperty(navigator, 'clipboard', original); }
        else { Reflect.deleteProperty(navigator, 'clipboard'); }
    });

    for (const modo of ['Compartido', 'Competitivo'] as ModoJuego[]) {
        for (const privada of [false, true]) {
            for (const usuarioId of [1, 2]) {
                it(`${modo}, privada=${privada}, jugador=${usuarioId}: muestra y copia el mismo código`, async () => {
                    const copiar = vi.fn().mockResolvedValue(undefined);
                    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: copiar } });
                    const { fixture } = crearPantalla(modo, privada, usuarioId);
                    const bloque = fixture.nativeElement.querySelector('app-codigo-sala');
                    expect(bloque).not.toBeNull();
                    expect(bloque.querySelector('code').textContent).toBe('CP8CQQ');
                    expect(bloque.closest('[hidden]')).toBeNull();
                    bloque.querySelector('button').click(); await fixture.whenStable();
                    expect(copiar).toHaveBeenCalledExactlyOnceWith('CP8CQQ');
                    fixture.destroy();
                });
            }
        }
    }

    for (const codigo of ['', 'CP8CQQ']) {
        it(`Solitario no muestra código ni copiar, incluso con un código legado (${codigo || 'vacío'})`, () => {
            const { fixture, estado } = crearPantalla('Individual', false, 1);
            for (const fase of ['Esperando', 'EnCurso', 'Finalizada'] as const) {
                estado.update(sala => ({ ...sala, codigo, estado: fase }));
                fixture.detectChanges();
                expect(fixture.nativeElement.querySelector('app-codigo-sala')).toBeNull();
                expect(fixture.nativeElement.textContent).not.toContain('CÓDIGO DE SALA');
                expect(fixture.nativeElement.textContent).not.toContain('Copiar código');
            }
            fixture.destroy();
        });
    }

    it('el anfitrión conserva la sección al iniciar, finalizar y preparar otra ronda', () => {
        const { fixture, estado } = crearPantalla('Compartido', false, 1);
        for (const fase of ['EnCurso', 'Finalizada', 'Esperando'] as const) {
            estado.update(sala => ({ ...sala, estado: fase, numeroRonda: fase === 'Esperando' ? 2 : 1 }));
            fixture.detectChanges();
            expect(fixture.nativeElement.querySelector('app-codigo-sala code').textContent).toBe('CP8CQQ');
        }
        fixture.destroy();
    });
});
