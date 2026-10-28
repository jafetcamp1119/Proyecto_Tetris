import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { GoogleWebServicio } from './google-web.servicio';

describe('Carga de Google Identity Services', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    document
      .querySelectorAll('script[src="https://accounts.google.com/gsi/client"]')
      .forEach((script) => script.remove());
  });

  it('comparte la carga y permite un nuevo intento si el script falla', async () => {
    const servicio = TestBed.inject(GoogleWebServicio);
    const primera = servicio.cargar();
    expect(servicio.cargar()).toBe(primera);
    const rechazada = expect(primera).rejects.toThrow('Google');
    document.querySelector('script[src="https://accounts.google.com/gsi/client"]')!.dispatchEvent(new Event('error'));
    await rechazada;
    const segunda = servicio.cargar();
    const nuevoRechazo = expect(segunda).rejects.toThrow();
    expect(segunda).not.toBe(primera);
    document.querySelector('script[src="https://accounts.google.com/gsi/client"]')!.dispatchEvent(new Event('error'));
    await nuevoRechazo;
  });

  it('no espera indefinidamente cuando un bloqueador impide cargar Google', async () => {
    vi.useFakeTimers();
    const servicio = TestBed.inject(GoogleWebServicio);
    const carga = servicio.cargar();
    const rechazada = expect(carga).rejects.toThrow();
    await vi.advanceTimersByTimeAsync(15000);
    await rechazada;
  });
});
