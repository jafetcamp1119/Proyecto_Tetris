// Detecta cuándo un dispositivo móvil necesita mostrar el aviso de orientación vertical.
import { DOCUMENT } from '@angular/common';
import { inject, Injectable, OnDestroy, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';

@Injectable({ providedIn: 'root' })
export class OrientacionServicio implements OnDestroy {
  private readonly ventana = inject(DOCUMENT).defaultView;
  private readonly aviso = signal(false);
  readonly requiereVertical = this.aviso.asReadonly();
  private readonly consulta = this.ventana?.matchMedia?.('(orientation: landscape)');
  private readonly cambio = (): void => {
    this.actualizar();
  };

  constructor() {
    this.ventana?.addEventListener('resize', this.cambio);
    this.ventana?.addEventListener('orientationchange', this.cambio);
    this.ventana?.screen.orientation?.addEventListener('change', this.cambio);

    if (this.consulta?.addEventListener) {
      this.consulta.addEventListener('change', this.cambio);
    } else {
      this.consulta?.addListener(this.cambio);
    }

    this.actualizar();
  }

  // Reconoce dispositivos móviles, incluido iPadOS que se identifica como Macintosh.
  private esMovil(): boolean {
    if (Capacitor.isNativePlatform()) {
      return true;
    }

    const navegador = this.ventana?.navigator;

    if (!navegador) {
      return false;
    }

    if (/Android|iPhone|iPad|iPod/i.test(navegador.userAgent)) {
      return true;
    }
    // iPadOS puede identificarse como Macintosh al solicitar un sitio de escritorio.
    return /Macintosh/i.test(navegador.userAgent) && navegador.maxTouchPoints > 1;
  }

  // Consulta si la ventana está actualmente en orientación horizontal.
  private estaHorizontal(): boolean {
    const tipo = this.ventana?.screen.orientation?.type;

    if (tipo?.startsWith('landscape')) {
      return true;
    }

    if (tipo?.startsWith('portrait')) {
      return false;
    }

    const angulo = (this.ventana as (Window & { orientation?: number }) | null)?.orientation;

    if (typeof angulo === 'number') {
      return Math.abs(angulo) % 180 === 90;
    }

    return this.consulta?.matches ?? false;
  }

  // Actualiza el aviso de orientación para el dispositivo actual.
  private actualizar(): void {
    this.aviso.set(this.esMovil() && this.estaHorizontal());
  }

  // Retira las escuchas de orientación y tamaño al liberar el servicio.
  ngOnDestroy(): void {
    this.ventana?.removeEventListener('resize', this.cambio);
    this.ventana?.removeEventListener('orientationchange', this.cambio);
    this.ventana?.screen.orientation?.removeEventListener('change', this.cambio);

    if (this.consulta?.removeEventListener) {
      this.consulta.removeEventListener('change', this.cambio);
    } else {
      this.consulta?.removeListener(this.cambio);
    }
  }
}
