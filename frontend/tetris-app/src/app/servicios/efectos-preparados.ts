// Carga y decodifica efectos para reutilizarlos con Web Audio.
// Controla una voz por efecto y libera sus recursos cuando termina el servicio.
import { buscarComienzoAudio } from './comienzo-audio';

export class EfectosPreparados<T extends string> {
  private contexto?: AudioContext;
  private readonly buffers = new Map<T, AudioBuffer>();
  private readonly cargas = new Map<T, Promise<void>>();
  private reanudacion?: Promise<void>;
  private readonly voces = new Map<T, { fuente: AudioBufferSourceNode; volumen: GainNode }>();
  private cerrado = false;
  private readonly comienzos = new Map<T, number>();

  constructor(
    private preparado?: (efecto: T, silencioMs: number) => void,
    private estado?: (disponible: boolean) => void,
    private diagnostico?: (evento: string) => void,
  ) {}

  get usaWebAudio(): boolean {
    return !!this.contexto;
  }

  get disponible(): boolean {
    return this.contexto?.state === 'running';
  }

  // Crea o reactiva el contexto de audio y comparte la carga de los efectos.
  preparar(archivos: Record<T, string>): Promise<boolean> {
    if (this.cerrado) {
      return Promise.resolve(false);
    }

    const constructor =
      window.AudioContext ?? (window as Window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;

    if (!constructor) {
      return Promise.resolve(false);
    }

    try {
      if (!this.contexto || this.contexto.state === 'closed') {
        this.contexto = new constructor({ latencyHint: 'interactive' });
        this.contexto.onstatechange = () => this.notificarEstado();
      }
      // Se invoca durante el gesto, antes de esperar fetch o decodeAudioData (Safari).
      if (this.contexto.state !== 'running') {
        this.reanudacion = this.contexto
          .resume()
          .catch(() => {
            this.diagnostico?.('resume_fallido');
          })
          .finally(() => {
            this.reanudacion = undefined;
            this.notificarEstado();
          });
      }

      for (const efecto of Object.keys(archivos) as T[]) {
        if (!this.cargas.has(efecto)) {
          this.cargas.set(efecto, this.cargar(efecto, archivos[efecto]));
        }
      }

      return Promise.all([...this.cargas.values(), this.reanudacion]).then(() => {
        this.notificarEstado();
        return this.disponible;
      });
    } catch {
      this.diagnostico?.('preparacion_fallida');
      this.estado?.(false);
      return Promise.resolve(false);
    }
  }

  // Informa si Web Audio está disponible después de un cambio de estado.
  private notificarEstado(): void {
    if (this.cerrado) {
      return;
    }

    this.diagnostico?.('contexto_' + this.contexto?.state);
    this.estado?.(this.disponible);
  }

  // Reutiliza el buffer decodificado y reemplaza la voz anterior del mismo efecto.
  reproducir(efecto: T, volumen: number): boolean {
    const buffer = this.buffers.get(efecto);
    const contexto = this.contexto;

    if (!contexto || contexto.state !== 'running' || !buffer) {
      this.voces.get(efecto)?.fuente.stop();
      this.voces.delete(efecto);
      return false;
    }

    this.voces.get(efecto)?.fuente.stop();

    try {
      const fuente = contexto.createBufferSource();
      const ganancia = contexto.createGain();
      fuente.buffer = buffer;
      ganancia.gain.value = volumen;
      fuente.connect(ganancia);
      ganancia.connect(contexto.destination);
      this.voces.set(efecto, { fuente, volumen: ganancia });
      fuente.onended = () => {
        fuente.disconnect();
        ganancia.disconnect();

        if (this.voces.get(efecto)?.fuente === fuente) {
          this.voces.delete(efecto);
        }
      };
      fuente.start(0, this.comienzos.get(efecto) ?? 0);
      return true;
    } catch {
      this.diagnostico?.('reproduccion_fallida');
      this.estado?.(false);
      return false;
    }
  }

  // Actualiza las voces activas y reduce el volumen relativo del movimiento.
  ajustarVolumen(volumen: number, movimiento: T): void {
    for (const [efecto, voz] of this.voces) {
      voz.volumen.gain.value = volumen * (efecto === movimiento ? 0.22 : 1);
    }
  }

  // Detiene las fuentes activas sin borrar los archivos ya preparados.
  detener(): void {
    for (const voz of this.voces.values()) {
      voz.fuente.stop();
    }

    this.voces.clear();
  }

  // Detiene las voces y cierra el contexto de audio.
  cerrar(): void {
    if (this.cerrado) {
      return;
    }

    this.cerrado = true;
    this.detener();
    this.buffers.clear();
    this.comienzos.clear();
    this.cargas.clear();

    if (this.contexto) {
      this.contexto.onstatechange = null;
    }

    void this.contexto?.close().catch(() => undefined);
  }

  // Descarga y decodifica un efecto una sola vez y calcula su comienzo audible.
  private async cargar(efecto: T, ruta: string): Promise<void> {
    try {
      const respuesta = await fetch(ruta, { signal: AbortSignal.timeout(10000) });

      if (!respuesta.ok) {
        throw new Error('Audio no disponible');
      }

      const buffer = await this.contexto!.decodeAudioData(await respuesta.arrayBuffer());

      if (!this.cerrado) {
        const comienzo = buscarComienzoAudio(buffer);
        this.comienzos.set(efecto, comienzo);
        this.buffers.set(efecto, buffer);
        this.preparado?.(efecto, comienzo * 1000);
      }
    } catch {
      this.cargas.delete(efecto);
      this.diagnostico?.('carga_fallida');
    }
  }
}
