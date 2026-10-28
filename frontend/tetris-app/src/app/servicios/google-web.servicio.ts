// Carga Google Identity Services y presenta el botón de acceso del navegador.
import { Injectable } from '@angular/core';

interface IdentidadGoogle {
  accounts: {
    id: {
      initialize(opciones: {
        client_id: string;
        nonce: string;
        auto_select: boolean;
        callback: (respuesta: { credential: string }) => void;
      }): void;
      renderButton(elemento: HTMLElement, opciones: { theme: string; size: string; text: string; width: number }): void;
    };
  };
}

export class ErrorCargaGoogle extends Error {}

@Injectable({ providedIn: 'root' })
export class GoogleWebServicio {
  private carga?: Promise<void>;

  // Consulta el objeto de Google Identity Services si su script ya está disponible.
  private identidad(): IdentidadGoogle | undefined {
    return (window as unknown as { google?: IdentidadGoogle }).google;
  }

  // Comparte la carga del script de Google entre los componentes que lo necesitan.
  cargar(): Promise<void> {
    if (this.identidad()?.accounts?.id) {
      return Promise.resolve();
    }

    if (!this.carga) {
      this.carga = this.cargarScript().catch((error) => {
        this.carga = undefined;
        throw error;
      });
    }

    return this.carga;
  }

  // Agrega el script de Google al documento y espera su carga o informa el fallo.
  private cargarScript(): Promise<void> {
    return new Promise<void>((resolver, rechazar) => {
      const script = document.createElement('script');
      script.src = 'https://accounts.google.com/gsi/client';
      script.async = true;
      const fallar = (): void => {
        clearTimeout(tiempo);
        script.remove();
        rechazar(new ErrorCargaGoogle('No se pudo cargar el botón de Google.'));
      };
      const tiempo = setTimeout(fallar, 15000);
      script.onerror = fallar;
      script.onload = () => {
        clearTimeout(tiempo);

        if (!this.identidad()?.accounts?.id) {
          fallar();
          return;
        }

        resolver();
      };
      document.head.append(script);
    });
  }

  // Configura el botón con el cliente y nonce de este intento y devuelve el token obtenido.
  mostrarBoton(elemento: HTMLElement, idCliente: string, nonce: string, completar: (token: string) => void): void {
    const identidad = this.identidad();

    if (!identidad) {
      throw new ErrorCargaGoogle('Google no terminó de cargar.');
    }

    identidad.accounts.id.initialize({
      client_id: idCliente,
      nonce,
      auto_select: false,
      callback: (respuesta) => {
        completar(respuesta.credential);
      },
    });
    elemento.replaceChildren();
    identidad.accounts.id.renderButton(elemento, {
      theme: 'outline',
      size: 'large',
      text: 'continue_with',
      width: 260,
    });
  }
}
