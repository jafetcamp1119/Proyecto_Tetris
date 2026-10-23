import { Injectable, inject } from '@angular/core';
import { SocialLogin } from '@capgo/capacitor-social-login';
import { environment } from '../../environments/environment';
import { IAutenticacion } from '../modelos/tetris.model';
import { ApiServicio } from './api.servicio';
import { AutenticacionServicio } from './autenticacion.servicio';

export interface DesafioGoogle {
    id: string;
    nonce: string;
}

@Injectable({ providedIn: 'root' })
export class GoogleServicio {
    private readonly api = inject(ApiServicio);
    private readonly acceso = inject(AutenticacionServicio);
    private inicializacion?: Promise<void>;

    preparar(vincular: boolean): Promise<DesafioGoogle> {
        return vincular
            ? this.acceso.solicitar('/api/google/desafio-vincular', 'POST')
            : this.api.solicitar('/api/google/desafio', 'POST');
    }

    async completar(desafio: DesafioGoogle, idToken: string, vincular: boolean): Promise<void> {
        const datos = { desafioId: desafio.id, idToken };
        if (vincular) {
            await this.acceso.solicitar('/api/google/vincular', 'POST', datos);
            await this.acceso.actualizarPerfil();
        } else {
            if (this.acceso.sesion()) {
                throw new Error('Ya ingresaste con un jugador. Vinculá Google desde su perfil o elegí Cambiar de jugador.');
            }
            const sesion = await this.api.solicitar<IAutenticacion>('/api/google/ingresar', 'POST', datos);
            if (this.acceso.sesion()) {
                throw new Error('La sesión cambió mientras Google se verificaba. El jugador actual se conserva.');
            }
            await this.acceso.guardarSesion(sesion);
        }
    }

    async ingresarAndroid(vincular: boolean): Promise<void> {
        this.inicializacion ??= SocialLogin.initialize({
            google: { webClientId: environment.google.idClienteWeb, mode: 'online' }
        }).catch(error => {
            this.inicializacion = undefined;
            throw error;
        });
        await this.inicializacion;
        const desafio = await this.preparar(vincular);
        const respuesta = await SocialLogin.login({
            provider: 'google',
            options: { nonce: desafio.nonce, style: 'standard', autoSelectEnabled: false }
        });
        // Solo necesitamos el ID token de esta operación. El plugin no debe conservar
        // una sesión Google en preferencias; la sesión Tetris usa el almacén cifrado.
        await SocialLogin.logout({ provider: 'google' });
        if (respuesta.result.responseType !== 'online' || !respuesta.result.idToken) {
            throw new Error('Google no devolvió un ID token válido.');
        }
        await this.completar(desafio, respuesta.result.idToken, vincular);
    }
}
