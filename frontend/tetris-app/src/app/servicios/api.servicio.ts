import { Injectable, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { environment } from '../../environments/environment';
import { IRespuesta } from '../modelos/tetris.model';

export class ErrorApi extends Error {
    constructor(mensaje: string, public estado: number, public codigo?: string | null) {
        super(mensaje);
    }
}

@Injectable({ providedIn: 'root' })
export class ApiServicio {
    readonly url = signal(Capacitor.isNativePlatform() ? environment.red.urlAndroid : environment.red.urlNavegador);

    async solicitar<T>(ruta: string, metodo = 'GET', cuerpo?: unknown, token?: string): Promise<T> {
        if (Capacitor.isNativePlatform() && !environment.red.permitirHttpLocal && !this.url().startsWith('https://')) {
            console.error('La versión definitiva requiere HTTPS para conectarse al backend.');
            throw new Error('No pudimos conectar con el juego. Intentá nuevamente más tarde.');
        }

        let respuesta: Response;
        try {
            respuesta = await fetch(`${this.url()}${ruta}`, {
                method: metodo,
                headers: {
                    'Content-Type': 'application/json',
                    ...(token ? { Authorization: `Bearer ${token}` } : {})
                },
                body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
                signal: AbortSignal.timeout(20000),
                cache: 'no-store'
            });
        } catch {
            throw new Error('No pudimos conectar con el juego. Revisá tu conexión Wi-Fi e intentá de nuevo.');
        }

        if (respuesta.status === 204) {
            return undefined as T;
        }
        const datos: IRespuesta<T> | null = await respuesta.json().catch(() => null);
        if (!respuesta.ok || !datos?.success) {
            throw new ErrorApi(datos?.error || (respuesta.status === 429 ? 'Demasiados intentos. Esperá un minuto.' : 'No se pudo completar la solicitud.'), respuesta.status, datos?.codigoError);
        }
        return datos.data as T;
    }
}
