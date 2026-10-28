// Configuración pública cargada antes de iniciar Angular.
export const redPublicada = { urlNavegador: '', urlAndroid: '', permitirHttpLocal: false };

export function validarUrlPublica(valor: unknown): string {
    if (typeof valor !== 'string') { throw new Error('Falta la dirección del servidor.'); }
    const url = new URL(valor);
    const host = url.hostname.toLowerCase();
    if (url.protocol !== 'https:' || url.username || url.password || url.port ||
        url.pathname !== '/' || url.search || url.hash || !host.includes('.') ||
        host === 'localhost' || host.endsWith('.localhost') || host.endsWith('.local') ||
        host.endsWith('.internal') || /^[0-9.]+$/.test(host) || host.includes(':')) {
        throw new Error('Se requiere un origen HTTPS público sin rutas ni credenciales.');
    }
    return url.origin;
}
