import { TestBed } from '@angular/core/testing';
import { afterEach, expect, it, vi } from 'vitest';
import { ApiServicio } from './api.servicio';

afterEach(() => vi.unstubAllGlobals());

it('cancelar solicitudes de la sesión revocada no cancela la del nuevo dispositivo', async () => {
    const pendientes = new Map<string, { signal: AbortSignal; resolver: (respuesta: unknown) => void }>();
    vi.stubGlobal('fetch', vi.fn((_ruta, opciones) => new Promise((resolver, rechazar) => {
        pendientes.set(opciones.headers.Authorization, { signal: opciones.signal, resolver });
        opciones.signal.addEventListener('abort', () => rechazar(new DOMException('Cancelada', 'AbortError')));
    })));
    const api = TestBed.inject(ApiServicio);
    api.url.set('https://prueba.trycloudflare.com');
    const anterior = api.solicitar('/api/usuarios/yo', 'GET', undefined, 'anterior');
    const nueva = api.solicitar('/api/usuarios/yo', 'GET', undefined, 'nueva');
    const rechazada = expect(anterior).rejects.toThrow('conectar');
    api.cancelarSolicitudes('anterior');
    await rechazada;
    expect(pendientes.get('Bearer anterior')?.signal.aborted).toBe(true);
    expect(pendientes.get('Bearer nueva')?.signal.aborted).toBe(false);
    pendientes.get('Bearer nueva')!.resolver({ ok: true, status: 200, json: async () => ({ success: true, data: 'perfil nuevo' }) });
    await expect(nueva).resolves.toBe('perfil nuevo');
});
