import { afterEach, describe, expect, it, vi } from 'vitest';
import { cargarConexionPublicada, urlConfiguracionAndroid, validarConfiguracionPublica } from './conexion-publicada';

describe('Conexion dinamica Android y web', () => {
    afterEach(() => vi.unstubAllGlobals());

    it('rechaza otros destinos, secretos o estructuras alteradas', () => {
        for (const datos of [null, [], {}, { urlBackend: 'https://atacante.example' },
            { urlBackend: 'http://prueba.trycloudflare.com' }, { urlBackend: 'https://prueba.trycloudflare.com.evil.example' },
            { urlBackend: 'https://prueba.trycloudflare.com', token: 'privado' }]) {
            expect(() => validarConfiguracionPublica(datos)).toThrow();
        }
    });

    it('Android usa la configuracion estable y recibe un tunel nuevo en cada intento', async () => {
        const solicitar = vi.fn()
            .mockResolvedValueOnce({ ok: true, json: async () => ({ urlBackend: 'https://primero.trycloudflare.com' }) })
            .mockResolvedValueOnce({ ok: true, json: async () => ({ estado: 'Servidor Tetris activo' }) })
            .mockResolvedValueOnce({ ok: true, json: async () => ({ urlBackend: 'https://segundo.trycloudflare.com' }) })
            .mockResolvedValueOnce({ ok: true, json: async () => ({ estado: 'Servidor Tetris activo' }) });
        vi.stubGlobal('fetch', solicitar);
        expect(await cargarConexionPublicada(true)).toBe('https://primero.trycloudflare.com');
        expect(await cargarConexionPublicada(true)).toBe('https://segundo.trycloudflare.com');
        expect(solicitar.mock.calls[0][0]).toBe(urlConfiguracionAndroid);
        expect(solicitar.mock.calls[2][0]).toBe(urlConfiguracionAndroid);
        expect(solicitar.mock.calls[0][1]).toMatchObject({ cache: 'no-store', credentials: 'omit', redirect: 'error' });
    });

    it('no reutiliza el tunel compilado si falla DNS o el backend esta apagado', async () => {
        vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('DNS')));
        await expect(cargarConexionPublicada(true)).rejects.toThrow();
        vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce({ ok: true, json: async () => ({ urlBackend: 'https://prueba.trycloudflare.com' }) })
            .mockResolvedValueOnce({ ok: false }));
        await expect(cargarConexionPublicada(false)).rejects.toThrow('servidor');
    });
});
