import { validarUrlPublica } from './configuracion-publicada';

describe('Configuración publicada', () => {
    it('normaliza un origen HTTPS público', () => {
        expect(validarUrlPublica('https://api.example.com/')).toBe('https://api.example.com');
    });
    it('rechaza red local, credenciales, rutas y valores ausentes', () => {
        for (const valor of ['', undefined, 'http://api.example.com', 'https://localhost',
            'https://192.168.1.29', 'https://127.0.0.1', 'https://[::1]', 'https://equipo.local',
            'https://usuario:clave@api.example.com', 'https://api.example.com/api',
            'https://api.example.com?token=x', 'https://api.example.com#x']) {
            expect(() => validarUrlPublica(valor)).toThrow();
        }
    });
});
