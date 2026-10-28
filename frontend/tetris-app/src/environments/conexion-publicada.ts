import { validarUrlPublica } from './configuracion-publicada';

export const urlConfiguracionAndroid = 'https://proyecto-tetris-512517.web.app/assets/configuracion-publica.json';

export function validarConfiguracionPublica(datos: unknown): string {
    if (!datos || typeof datos !== 'object' || Array.isArray(datos)) { throw new Error('Configuracion invalida.'); }
    const objeto = datos as Record<string, unknown>;
    if (Object.keys(objeto).some(clave => clave !== 'urlBackend')) { throw new Error('Configuracion con campos no permitidos.'); }
    const url = validarUrlPublica(objeto['urlBackend']);
    // Solo Quick Tunnels de esta infraestructura; agregar un dominio estable exige una decision del desarrollador.
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*\.trycloudflare\.com$/.test(new URL(url).hostname)) {
        throw new Error('El destino del servidor no pertenece a la infraestructura permitida.');
    }
    return url;
}

export async function cargarConexionPublicada(nativo: boolean): Promise<string> {
    const ruta = nativo ? urlConfiguracionAndroid : '/assets/configuracion-publica.json';
    const respuesta = await fetch(ruta, {
        cache: 'no-store', credentials: 'omit', redirect: 'error', signal: AbortSignal.timeout(10000)
    });
    if (!respuesta.ok) { throw new Error('No se pudo obtener la configuracion vigente.'); }
    const url = validarConfiguracionPublica(await respuesta.json());
    const salud = await fetch(url + '/salud', {
        cache: 'no-store', credentials: 'omit', redirect: 'error', signal: AbortSignal.timeout(8000)
    });
    if (!salud.ok || (await salud.json()).estado !== 'Servidor Tetris activo') { throw new Error('El servidor no esta disponible.'); }
    return url;
}
