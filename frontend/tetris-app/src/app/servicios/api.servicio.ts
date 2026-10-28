// Centraliza las solicitudes HTTP y convierte las respuestas de la API en datos o errores.
// La dirección validada también se utiliza para conectar SignalR.
import { Injectable, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { environment } from '../../environments/environment';
import { IRespuesta } from '../modelos/tetris.model';
import { cargarConexionPublicada } from '../../environments/conexion-publicada';

export class ErrorApi extends Error {
  constructor(
    mensaje: string,
    public estado: number,
    public codigo?: string | null,
  ) {
    super(mensaje);
  }
}

@Injectable({ providedIn: 'root' })
export class ApiServicio {
  private readonly solicitudes = new Map<string, Set<AbortController>>();
  readonly url = signal(Capacitor.isNativePlatform() ? environment.red.urlAndroid : environment.red.urlNavegador);

  // Consulta de nuevo la configuración pública en producción y actualiza el origen del backend.
  async actualizarConexion(): Promise<void> {
    if (!environment.production) {
      return;
    }

    const url = await cargarConexionPublicada(Capacitor.isNativePlatform());
    this.url.set(url);
  }

  // Prepara una solicitud con plazo de 20 segundos y registra su cancelación por token.
  async solicitar<T>(ruta: string, metodo = 'GET', cuerpo?: unknown, token?: string): Promise<T> {
    if (!environment.red.permitirHttpLocal && !this.url().startsWith('https://')) {
      console.error('La versión definitiva requiere HTTPS para conectarse al backend.');
      throw new Error('No pudimos conectar con el juego. Intentá nuevamente más tarde.');
    }

    const control = new AbortController();
    const plazo = setTimeout(() => control.abort(), 20000);

    if (token) {
      if (!this.solicitudes.has(token)) {
        this.solicitudes.set(token, new Set());
      }

      this.solicitudes.get(token)!.add(control);
    }

    try {
      return await this.enviar<T>(ruta, metodo, cuerpo, token, control.signal);
    } finally {
      clearTimeout(plazo);

      if (token) {
        const pendientes = this.solicitudes.get(token);
        pendientes?.delete(control);

        if (!pendientes?.size) {
          this.solicitudes.delete(token);
        }
      }
    }
  }

  // Cancela las solicitudes que todavía pertenecen a una sesión cerrada.
  cancelarSolicitudes(token: string): void {
    for (const control of this.solicitudes.get(token) ?? []) {
      control.abort();
    }

    this.solicitudes.delete(token);
  }

  // Envía JSON y el Bearer cuando corresponde.
  // Devuelve data si la API tuvo éxito o un ErrorApi con estado HTTP y código de error.
  private async enviar<T>(
    ruta: string,
    metodo: string,
    cuerpo: unknown,
    token: string | undefined,
    signal: AbortSignal,
  ): Promise<T> {
    let respuesta: Response;

    try {
      respuesta = await fetch(`${this.url()}${ruta}`, {
        method: metodo,
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
        signal,
        cache: 'no-store',
        redirect: 'error',
      });
    } catch {
      throw new Error('No pudimos conectar con el juego. Revisá tu conexión a Internet e intentá de nuevo.');
    }

    if (respuesta.status === 204) {
      return undefined as T;
    }

    const datos: IRespuesta<T> | null = await respuesta.json().catch(() => null);

    if (!respuesta.ok || !datos?.success) {
      throw new ErrorApi(
        datos?.error ||
          (respuesta.status === 429 ? 'Demasiados intentos. Esperá un minuto.' : 'No se pudo completar la solicitud.'),
        respuesta.status,
        datos?.codigoError,
      );
    }

    return datos.data as T;
  }
}
