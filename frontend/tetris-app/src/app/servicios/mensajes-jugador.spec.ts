import { vi } from 'vitest';
import { ErrorApi } from './api.servicio';
import { mensajeParaJugador } from './mensajes-jugador';

describe('Mensajes públicos', () => {
    it('mantiene los detalles técnicos en consola y muestra una explicación natural', () => {
        const consola = vi.spyOn(console, 'error').mockImplementation(() => undefined);
        const error = new ErrorApi('HTTP 401 JWT inválido localhost:5186', 401);
        expect(mensajeParaJugador(error)).toBe('No pudimos recuperar tu sesión. Intentá ingresar nuevamente.');
        expect(consola).toHaveBeenCalledWith(expect.any(String), error);
        expect(mensajeParaJugador(new Error('SignalR websocket error SQL'))).not.toMatch(/SignalR|SQL|websocket/);
        expect(mensajeParaJugador(new ErrorApi('detalle interno', 409, 'GoogleEnOtroPerfil'))).toContain('Tu progreso se conserva');
        consola.mockRestore();
    });
});
