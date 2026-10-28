import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { EfectosPreparados } from './efectos-preparados';

class ContextoSimulado {
  static instancias: ContextoSimulado[] = [];
  state = 'suspended';
  destination = {};
  fuentes: ReturnType<ContextoSimulado['crearFuente']>[] = [];
  resume = vi.fn(() => {
    this.state = 'running';
    return Promise.resolve();
  });
  close = vi.fn(() => {
    this.state = 'closed';
    return Promise.resolve();
  });
  decodeAudioData = vi.fn(async () => ({ duration: 1 }));
  createGain = vi.fn(() => ({ gain: { value: 1 }, connect: vi.fn(), disconnect: vi.fn() }));
  createBufferSource = vi.fn(() => {
    const fuente = this.crearFuente();
    this.fuentes.push(fuente);
    return fuente;
  });

  constructor() {
    ContextoSimulado.instancias.push(this);
  }

  crearFuente() {
    return {
      buffer: null,
      connect: vi.fn(),
      disconnect: vi.fn(),
      start: vi.fn(),
      stop: vi.fn(),
      onended: null as (() => void) | null,
    };
  }
}

describe('Efectos preparados sin espera de descarga al reproducir', () => {
  let efectos: EfectosPreparados<'mover' | 'girar'>;
  const rutas = { mover: '/mover.mp3', girar: '/girar.mp3' };

  beforeEach(() => {
    ContextoSimulado.instancias = [];
    vi.stubGlobal('AudioContext', ContextoSimulado);
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({ ok: true, arrayBuffer: async () => new ArrayBuffer(4) })),
    );
    efectos = new EfectosPreparados();
  });

  afterEach(() => {
    efectos.cerrar();
    vi.unstubAllGlobals();
  });

  async function preparar(): Promise<ContextoSimulado> {
    efectos.preparar(rutas);
    await vi.waitFor(() => expect(ContextoSimulado.instancias[0].decodeAudioData).toHaveBeenCalledTimes(2));
    await Promise.resolve();
    return ContextoSimulado.instancias[0];
  }

  it('crea y reanuda durante el gesto; descarga y decodifica una sola vez', async () => {
    expect(ContextoSimulado.instancias).toHaveLength(0);
    const contexto = await preparar();
    efectos.preparar(rutas);
    expect(ContextoSimulado.instancias).toHaveLength(1);
    expect(contexto.resume).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledTimes(2);
    expect(efectos.reproducir('mover', 0.5)).toBe(true);
    expect(contexto.fuentes[0].start).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('usa una sola voz por efecto y libera los nodos sin retirar la voz nueva', async () => {
    const contexto = await preparar();
    efectos.reproducir('mover', 0.5);
    efectos.reproducir('mover', 0.5);
    expect(contexto.fuentes[0].stop).toHaveBeenCalledTimes(1);
    contexto.fuentes[0].onended?.();
    expect(contexto.fuentes[0].disconnect).toHaveBeenCalledTimes(1);
    efectos.detener();
    expect(contexto.fuentes[1].stop).toHaveBeenCalledTimes(1);
  });

  it('no encola sonidos cuando Safari suspende el contexto', async () => {
    const contexto = await preparar();
    contexto.state = 'suspended';
    expect(efectos.reproducir('girar', 1)).toBe(false);
    efectos.preparar(rutas);
    expect(contexto.resume).toHaveBeenCalledTimes(2);
    expect(contexto.createBufferSource).not.toHaveBeenCalled();
  });

  it('recorta solo silencio inicial y comienza sin encolar ni duplicar el impacto', async () => {
    const muestras = new Float32Array(44100);
    muestras.fill(0.2, 4410);
    const buffer = {
      duration: 1,
      length: 44100,
      sampleRate: 44100,
      numberOfChannels: 1,
      getChannelData: () => muestras,
    };
    const medir = vi.fn();
    efectos = new EfectosPreparados(medir);
    efectos.preparar(rutas);
    const contexto = ContextoSimulado.instancias[0];
    contexto.decodeAudioData.mockResolvedValue(buffer);
    await vi.waitFor(() => expect(medir).toHaveBeenCalledTimes(2));
    efectos.reproducir('mover', 0.5);
    expect(contexto.fuentes[0].start).toHaveBeenCalledWith(0, 0.098);
    console.info('MEDICION_AUDIO_SINTETICO silencio_antes_ms=100 silencio_despues_ms=2');
  });

  it('permite reintentar una descarga fallida sin duplicar el contexto ni los buffers válidos', async () => {
    vi.mocked(fetch).mockRejectedValue(new Error('Sin red'));
    await efectos.preparar(rutas);
    expect(efectos.reproducir('mover', 1)).toBe(false);
    vi.mocked(fetch).mockResolvedValue({ ok: true, arrayBuffer: async () => new ArrayBuffer(4) } as Response);
    await efectos.preparar(rutas);
    expect(efectos.reproducir('mover', 0.25)).toBe(true);
    await efectos.preparar(rutas);
    expect(fetch).toHaveBeenCalledTimes(4);
    expect(ContextoSimulado.instancias).toHaveLength(1);
    efectos.cerrar();
    expect(ContextoSimulado.instancias[0].close).toHaveBeenCalledTimes(1);
  });
});
