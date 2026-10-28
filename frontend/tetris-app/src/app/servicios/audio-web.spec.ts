import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { archivosAudio, EfectoAudio, ServicioAudio } from './audio.servicio';
import { ISala } from '../modelos/tetris.model';
import { AjustesPage } from '../pages/ajustes/ajustes.page';

class ContextoPrueba {
  static instancias: ContextoPrueba[] = [];
  static automatico = true;
  state = 'suspended';
  onstatechange?: (() => void) | null;
  destination = {};
  fuentes: ReturnType<ContextoPrueba['crearFuente']>[] = [];
  ganancias: { gain: { value: number }; connect: ReturnType<typeof vi.fn>; disconnect: ReturnType<typeof vi.fn> }[] =
    [];
  completarResume?: () => void;
  constructor() {
    ContextoPrueba.instancias.push(this);
  }
  resume = vi.fn(
    () =>
      new Promise<void>((resolver) => {
        this.completarResume = () => {
          this.state = 'running';
          this.onstatechange?.();
          resolver();
        };

        if (ContextoPrueba.automatico) {
          this.completarResume();
        }
      }),
  );
  close = vi.fn(async () => {
    this.state = 'closed';
  });
  decodeAudioData = vi.fn(async (datos: ArrayBuffer) => ({ duration: 1, nombre: new TextDecoder().decode(datos) }));
  createGain = vi.fn(() => {
    const nodo = { gain: { value: 1 }, connect: vi.fn(), disconnect: vi.fn() };
    this.ganancias.push(nodo);
    return nodo;
  });
  createBufferSource = vi.fn(() => {
    const fuente = this.crearFuente();
    this.fuentes.push(fuente);
    return fuente;
  });
  crearFuente() {
    return {
      buffer: null as unknown,
      activa: false,
      connect: vi.fn(),
      disconnect: vi.fn(),
      start: vi.fn(function (this: { activa: boolean }) {
        this.activa = true;
      }),
      stop: vi.fn(function (this: { activa: boolean }) {
        this.activa = false;
      }),
      onended: null,
    };
  }
}

function sala(): ISala {
  return {
    id: 'audio-web',
    numeroRonda: 1,
    modo: 'Compartido',
    estado: 'EnCurso',
    jugadores: [{ usuarioId: 1, perdido: false }],
    tableros: [],
    pausa: { fase: 'Ninguna' },
    ganadores: [],
    ultimoEfecto: null,
  } as unknown as ISala;
}

describe('Audio Web: interrupción, resultados y ganancia real de la muestra', () => {
  let audio: ServicioAudio;
  beforeEach(() => {
    localStorage.clear();
    ContextoPrueba.instancias = [];
    ContextoPrueba.automatico = true;
    vi.stubGlobal('AudioContext', ContextoPrueba);
    vi.stubGlobal(
      'fetch',
      vi.fn(async (ruta: string) => ({ ok: true, arrayBuffer: async () => new TextEncoder().encode(ruta).buffer })),
    );
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    audio = TestBed.inject(ServicioAudio);
    audio.usarJugador(1);
    audio.ajustar({ musicaActiva: false });
  });
  afterEach(() => {
    audio.ngOnDestroy();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  for (const volumen of [0, 25, 50, 75, 100]) {
    it(`Probar sonido utiliza ganancia ${volumen}% sin activar efectos por su cuenta`, async () => {
      audio.ajustar({ volumenEfectos: volumen });
      await audio.probarSonido();
      const contexto = ContextoPrueba.instancias[0];

      if (volumen === 0) {
        expect(contexto?.fuentes.length ?? 0).toBe(0);
      } else {
        expect(contexto.ganancias.at(-1)?.gain.value).toBeCloseTo(volumen / 100);
      }

      expect(audio.preferencias().efectosActivos).toBe(true);
    });
  }

  it('la ganancia se aplica a todos los efectos y actualiza también las voces que ya suenan', async () => {
    await audio.desbloquear();
    audio.ajustar({ volumenEfectos: 50 });
    const contexto = ContextoPrueba.instancias[0];

    for (const efecto of Object.keys(archivosAudio.efectos) as EfectoAudio[]) {
      expect(audio.reproducirEfecto(efecto)).toBe(true);
      expect(contexto.ganancias.at(-1)?.gain.value).toBeCloseTo(efecto === 'movimiento' ? 0.11 : 0.5);
    }

    audio.ajustar({ volumenEfectos: 25 });
    expect(contexto.ganancias[0].gain.value).toBeCloseTo(0.055);
    expect(contexto.ganancias.slice(1).every((nodo) => nodo.gain.value === 0.25)).toBe(true);
  });

  it('el deslizador de Ajustes cambia la ganancia de Probar sonido y no agrega un clic global', async () => {
    const fixture = TestBed.createComponent(AjustesPage);
    fixture.detectChanges();
    const controles = fixture.nativeElement.querySelectorAll('input[type="range"]') as NodeListOf<HTMLInputElement>;
    controles[1].value = '25';
    controles[1].dispatchEvent(new Event('input', { bubbles: true }));
    const boton = fixture.nativeElement.querySelector('button');
    expect(boton.hasAttribute('data-sin-clic')).toBe(true);
    boton.click();
    await fixture.whenStable();
    expect(ContextoPrueba.instancias[0].ganancias.at(-1)?.gain.value).toBe(0.25);
    controles[1].value = '75';
    controles[1].dispatchEvent(new Event('input', { bubbles: true }));
    boton.click();
    await fixture.whenStable();
    expect(ContextoPrueba.instancias[0].ganancias.at(-1)?.gain.value).toBe(0.75);
    expect(ContextoPrueba.instancias[0].fuentes.filter((f) => f.activa)).toHaveLength(1);
    fixture.destroy();
  });
  it('efectos desactivados y volumen cero detienen fuentes, sin reactivarlas al probar', async () => {
    await audio.probarSonido();
    audio.ajustar({ efectosActivos: false });
    await audio.probarSonido();
    expect(ContextoPrueba.instancias[0].fuentes.filter((f) => f.activa)).toHaveLength(0);
    expect(audio.preferencias().efectosActivos).toBe(false);
    audio.ajustar({ efectosActivos: true });
    await audio.probarSonido();
    audio.ajustar({ volumenEfectos: 0 });
    expect(ContextoPrueba.instancias[0].fuentes.filter((f) => f.activa)).toHaveLength(0);
  });
  it('reanuda interrupted con otro gesto, conserva un contexto y emite victoria una sola vez', async () => {
    await audio.desbloquear();
    const contexto = ContextoPrueba.instancias[0];
    const estado = sala();
    audio.actualizarSala(estado, 1, false);
    contexto.state = 'interrupted';
    contexto.onstatechange?.();
    estado.estado = 'Finalizada';
    estado.ganadores = [1];
    audio.actualizarSala(estado, 1, true);
    expect(contexto.fuentes).toHaveLength(0);
    expect(audio.requiereInteraccion()).toBe(true);
    await audio.desbloquear();
    audio.actualizarSala(estado, 1, true);
    expect(contexto.fuentes).toHaveLength(1);
    expect(String((contexto.fuentes[0].buffer as { nombre: string }).nombre)).toContain('victoria.mp3');
    expect(ContextoPrueba.instancias).toHaveLength(1);
  });
  it('un resultado recibido mientras resume todavía espera no se pierde ni cae a HTMLAudio', async () => {
    ContextoPrueba.automatico = false;
    const activacion = audio.desbloquear();
    const contexto = ContextoPrueba.instancias[0];
    await vi.waitFor(() => expect(contexto.decodeAudioData).toHaveBeenCalledTimes(11));
    const estado = sala();
    audio.actualizarSala(estado, 1, false);
    estado.estado = 'Finalizada';
    estado.ganadores = [];
    audio.actualizarSala(estado, 1, true);
    expect(contexto.fuentes).toHaveLength(0);
    contexto.completarResume!();
    await activacion;
    expect(contexto.fuentes).toHaveLength(1);
    expect(String((contexto.fuentes[0].buffer as { nombre: string }).nombre)).toContain('derrota.mp3');
  });
  it('no reproduce resultados históricos ni pendientes que vencieron durante una interrupción', async () => {
    vi.useFakeTimers();
    await audio.desbloquear();
    const contexto = ContextoPrueba.instancias[0];
    const estado = sala();
    audio.actualizarSala(estado, 1, false);
    contexto.state = 'interrupted';
    contexto.onstatechange?.();
    estado.estado = 'Finalizada';
    estado.ganadores = [1];
    audio.actualizarSala(estado, 1, true);
    await vi.advanceTimersByTimeAsync(11000);
    await audio.desbloquear();
    audio.actualizarSala(estado, 1, true);
    expect(contexto.fuentes).toHaveLength(0);
  });
  it('muestras rápidas y cambios de pantalla conservan volumen sin voces duplicadas', async () => {
    audio.ajustar({ volumenEfectos: 25 });
    await Promise.all([audio.probarSonido(), audio.probarSonido(), audio.probarSonido()]);
    const contexto = ContextoPrueba.instancias[0];
    expect(contexto.fuentes.filter((f) => f.activa)).toHaveLength(1);
    audio.cambiarMusica('partida');
    audio.cambiarMusica('menu');
    expect(audio.preferencias().volumenEfectos).toBe(25);
    expect(JSON.parse(localStorage.getItem('tetris.audio.1')!).volumenEfectos).toBe(25);
  });
});
