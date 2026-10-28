import { Injectable, inject, signal } from '@angular/core';
import { SocialLogin } from '@capgo/capacitor-social-login';
import { environment } from '../../environments/environment';
import { IAutenticacion } from '../modelos/tetris.model';
import { ApiServicio } from './api.servicio';
import { AutenticacionServicio } from './autenticacion.servicio';

export interface DesafioGoogle {
    id: string;
    nonce: string;
}

interface RegistroGooglePendiente {
    requiereRegistro: true;
    comprobanteRegistro: string;
    expiraEnUtc: string;
}

@Injectable({ providedIn: 'root' })
export class GoogleServicio {
    private readonly api = inject(ApiServicio);
    private readonly acceso = inject(AutenticacionServicio);
    private inicializacion?: Promise<void>;
    readonly registroPendiente = signal<RegistroGooglePendiente | null>(null);

    cancelarRegistro(): void { this.registroPendiente.set(null); }

    async registrar(nombreUsuario: string): Promise<void> {
        const pendiente = this.registroPendiente();
        if (!pendiente || Date.parse(pendiente.expiraEnUtc) <= Date.now()) {
            this.cancelarRegistro();
            throw new Error('La verificación venció. Continuá nuevamente con Google.');
        }
        if (this.acceso.sesion()) { throw new Error('Ya ingresaste con un jugador. Usá Vincular Google desde su perfil.'); }
        const sesion = await this.api.solicitar<IAutenticacion>('/api/google/registrar', 'POST', {
            nombreUsuario: nombreUsuario.trim(), comprobanteRegistro: pendiente.comprobanteRegistro
        });
        if (this.acceso.sesion()) { throw new Error('La sesión cambió durante el registro. El jugador actual se conserva.'); }
        await this.acceso.guardarSesion(sesion);
        this.cancelarRegistro();
    }

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
            const sesion = await this.api.solicitar<IAutenticacion | RegistroGooglePendiente>('/api/google/ingresar', 'POST', datos);
            if (this.acceso.sesion()) {
                throw new Error('La sesión cambió mientras Google se verificaba. El jugador actual se conserva.');
            }
            if ('requiereRegistro' in sesion && sesion.requiereRegistro) { this.registroPendiente.set(sesion); }
            else { await this.acceso.guardarSesion(sesion as IAutenticacion); this.cancelarRegistro(); }
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
