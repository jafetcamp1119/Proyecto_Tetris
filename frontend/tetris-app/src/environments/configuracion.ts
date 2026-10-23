import { configuracionRed } from './configuracion-red';

// Configuración pública compartida por desarrollo y producción. No contiene secretos.
export const configuracion = {
    red: configuracionRed,
    google: {
        habilitado: true,
        idClienteWeb: '652877629973-kt2nkjl56mvgv6udli3m92mf64jcfcrs.apps.googleusercontent.com'
    }
};
