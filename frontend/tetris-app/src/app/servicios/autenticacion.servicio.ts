import { Injectable, inject, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import { IAutenticacion, IUsuario } from '../modelos/tetris.model';
import { ApiServicio, ErrorApi } from './api.servicio';

export interface PerfilRecordado {
    usuarioId: number;
    nombreUsuario: string;
    credencial?: string;
}

interface PerfilesGuardados {
    activo: number | null;
    perfiles: PerfilRecordado[];
}

@Injectable({ providedIn: 'root' })
export class AutenticacionServicio {
    private readonly api = inject(ApiServicio);
    private readonly clave = 'tetris.perfiles.v1';
    private inicializacion?: Promise<void>;
    private renovacion?: Promise<string>;
    private guardados: PerfilesGuardados = { activo: null, perfiles: [] };
    readonly sesion = signal<IAutenticacion | null>(null);
    readonly perfiles = signal<PerfilRecordado[]>([]);

    inicializar(): Promise<void> {
        this.inicializacion ??= this.cargar().catch(error => {
            this.inicializacion = undefined;
            throw error;
        });
        return this.inicializacion;
    }

    private async cargar(): Promise<void> {
        const texto = Capacitor.isNativePlatform()
            ? await SecureStorage.getItem(this.clave) : localStorage.getItem(this.clave);
        if (texto) {
            this.guardados = JSON.parse(texto) as PerfilesGuardados;
        }
        // Migrar la credencial del login anterior una sola vez, sin recuperar por nickname.
        const anterior = localStorage.getItem('credencialTetrisPrueba');
        if (anterior && !this.guardados.perfiles.length) {
            const sesion = await this.api.solicitar<IAutenticacion>('/api/usuarios/recuperar', 'POST', { credencial: anterior });
            await this.guardarSesion({ ...sesion, credencial: anterior });
            localStorage.removeItem('credencialTetrisPrueba');
        }
        this.perfiles.set(this.guardados.perfiles);
        if (this.guardados.activo != null && !this.sesion()) {
            try {
                await this.seleccionar(this.guardados.activo);
            } catch (error) {
                if (error instanceof ErrorApi && error.estado === 401) {
                    this.guardados.activo = null;
                    await this.persistir();
                }
                // Un fallo de red no elimina las credenciales recordadas.
            }
        }
        sessionStorage.removeItem('token');
        sessionStorage.removeItem('expiraEnUtc');
    }

    async registrar(nombreUsuario: string): Promise<void> {
        await this.guardarSesion(await this.api.solicitar<IAutenticacion>('/api/usuarios/registrar', 'POST', { nombreUsuario }));
    }

    async seleccionar(usuarioId: number): Promise<void> {
        const perfil = this.guardados.perfiles.find(p => p.usuarioId === usuarioId);
        if (!perfil?.credencial) {
            throw new Error('Recuperá este jugador con Google o con su código privado.');
        }
        const sesion = await this.api.solicitar<IAutenticacion>('/api/usuarios/recuperar', 'POST', { credencial: perfil.credencial });
        await this.guardarSesion({ ...sesion, credencial: perfil.credencial });
    }

    async guardarSesion(sesion: IAutenticacion): Promise<void> {
        const existente = this.guardados.perfiles.find(p => p.usuarioId === sesion.usuarioId);
        const perfil = { usuarioId: sesion.usuarioId, nombreUsuario: sesion.nombreUsuario, credencial: sesion.credencial || existente?.credencial };
        this.guardados.perfiles = [...this.guardados.perfiles.filter(p => p.usuarioId !== sesion.usuarioId), perfil];
        this.guardados.activo = sesion.usuarioId;
        await this.persistir();
        this.sesion.set(sesion);
    }

    private async persistir(): Promise<void> {
        const texto = JSON.stringify(this.guardados);
        if (Capacitor.isNativePlatform()) {
            // Android Keystore: nunca usar un fallback sin cifrar si falla el plugin.
            await SecureStorage.setItem(this.clave, texto);
        } else {
            // Navegador de desarrollo: mismo riesgo de acceso local/XSS que cualquier web.
            localStorage.setItem(this.clave, texto);
        }
        this.perfiles.set([...this.guardados.perfiles]);
    }

    async token(): Promise<string> {
        const sesion = this.sesion();
        if (!sesion) {
            throw new Error('Ingresá o recuperá tu jugador para continuar.');
        }
        if (Date.parse(sesion.expiraEnUtc) - Date.now() > 60000) {
            return sesion.token;
        }
        this.renovacion ??= this.renovar().finally(() => { this.renovacion = undefined; });
        return this.renovacion;
    }

    private async renovar(): Promise<string> {
        const perfil = this.guardados.perfiles.find(p => p.usuarioId === this.sesion()?.usuarioId);
        if (!perfil?.credencial) {
            throw new Error('No hay una credencial válida para renovar la sesión.');
        }
        const sesion = await this.api.solicitar<IAutenticacion>('/api/usuarios/renovar', 'POST', { credencial: perfil.credencial });
        await this.guardarSesion({ ...sesion, credencial: perfil.credencial });
        return sesion.token;
    }

    async solicitar<T>(ruta: string, metodo = 'GET', cuerpo?: unknown): Promise<T> {
        return this.api.solicitar<T>(ruta, metodo, cuerpo, await this.token());
    }

    async actualizarPerfil(): Promise<IUsuario> {
        const perfil = await this.solicitar<IUsuario>('/api/usuarios/yo');
        this.sesion.update(sesion => sesion ? { ...sesion, ...perfil } : null);
        return perfil;
    }

    async cambiarJugador(): Promise<void> {
        this.guardados.activo = null;
        await this.persistir();
        this.sesion.set(null);
        sessionStorage.removeItem('tetrisSala');
    }

    async cerrarSesion(): Promise<void> {
        await this.solicitar<void>('/api/usuarios/logout', 'POST');
        const usuarioId = this.sesion()!.usuarioId;
        this.guardados.perfiles = this.guardados.perfiles.map(p => p.usuarioId === usuarioId ? { ...p, credencial: undefined } : p);
        await this.cambiarJugador();
    }

    async quitarPerfil(usuarioId: number): Promise<void> {
        this.guardados.perfiles = this.guardados.perfiles.filter(p => p.usuarioId !== usuarioId);
        await this.persistir();
    }

    async recuperarCodigo(credencial: string): Promise<void> {
        await this.guardarSesion(await this.api.solicitar<IAutenticacion>('/api/usuarios/recuperar-codigo', 'POST', { credencial: credencial.trim() }));
    }
}
