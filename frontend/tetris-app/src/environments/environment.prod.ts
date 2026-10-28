// Construye el entorno de producción con HTTPS y configuración pública validada.
import { google } from './configuracion-google';
import { redPublicada } from './configuracion-publicada';
export const environment = { production: true, google, red: redPublicada };
