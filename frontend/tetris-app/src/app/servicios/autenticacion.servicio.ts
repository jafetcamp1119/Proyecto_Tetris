// Administra la sesión en memoria y los perfiles recordados del dispositivo.
// Distingue ingresar para tomar el control de renovar la sesión que ya está activa.
import { inject, Injectable, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import { IAutenticacion, IUsuario } from '../modelos/tetris.model';
import { ApiServicio, ErrorApi } from './api.servicio';

export interface PerfilRecordado {
  usuarioId: number;
  nombreUsuario: string;
  credencial?: string;
}

export type MotivoCierre = 'SesionReemplazada' | 'SesionVencida' | 'SesionInvalida';

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
  readonly codigoRecuperacionNuevo = signal('');
  readonly codigoRecuperacionPorConfirmar = signal(false);
  readonly mensajeSesion = signal('');
  readonly motivoCierre = signal<MotivoCierre | null>(null);

  // Comparte una sola carga inicial entre quienes solicitan la sesión.
  inicializar(): Promise<void> {
    this.inicializacion ??= this.cargar().catch((error) => {
      this.inicializacion = undefined;
      throw error;
    });
    return this.inicializacion;
  }

  // Lee perfiles guardados e intenta renovar el activo sin tomar el control de otra sesión.
  private async cargar(): Promise<void> {
    const texto = Capacitor.isNativePlatform()
      ? await SecureStorage.getItem(this.clave)
      : localStorage.getItem(this.clave);

    if (texto) {
      this.guardados = JSON.parse(texto) as PerfilesGuardados;
    }
    // Migrar la credencial del login anterior una sola vez, sin recuperar por nickname.
    const anterior = localStorage.getItem('credencialTetrisPrueba');

    if (anterior && !this.guardados.perfiles.length) {
      const sesion = await this.api.solicitar<IAutenticacion>('/api/usuarios/recuperar', 'POST', {
        credencial: anterior,
      });
      await this.guardarSesion({ ...sesion, credencial: sesion.credencial ?? anterior });
      localStorage.removeItem('credencialTetrisPrueba');
    }

    this.perfiles.set(this.guardados.perfiles);

    if (this.guardados.activo != null && !this.sesion()) {
      try {
        await this.recuperarPerfil(this.guardados.activo, false);
      } catch (error) {
        if (error instanceof ErrorApi && error.estado === 401) {
          this.informarCierre(error.codigo);
          this.guardados.activo = null;
          await this.persistir();
        }
        // Un fallo de red no elimina las credenciales recordadas.
      }
    }

    sessionStorage.removeItem('token');
    sessionStorage.removeItem('expiraEnUtc');
  }

  // Solicita una cuenta con nickname y conserva la credencial que devuelve el servidor.
  async registrar(nombreUsuario: string): Promise<void> {
    await this.guardarSesion(
      await this.api.solicitar<IAutenticacion>('/api/usuarios/registrar', 'POST', { nombreUsuario }),
    );
  }

  // Ingresa deliberadamente al perfil recordado y toma el control de su cuenta.
  async seleccionar(usuarioId: number): Promise<void> {
    await this.recuperarPerfil(usuarioId, true);
  }

  // Usa la credencial guardada para ingresar o renovar según tomarControl.
  private async recuperarPerfil(usuarioId: number, tomarControl: boolean): Promise<void> {
    const perfil = this.guardados.perfiles.find((p) => p.usuarioId === usuarioId);

    if (!perfil?.credencial) {
      throw new Error('Recuperá este jugador con Google o con su código privado.');
    }

    const ruta = tomarControl ? '/api/usuarios/recuperar' : '/api/usuarios/renovar';
    const sesion = await this.api.solicitar<IAutenticacion>(ruta, 'POST', { credencial: perfil.credencial });
    await this.guardarSesion({ ...sesion, credencial: sesion.credencial ?? perfil.credencial });
  }

  // Actualiza el perfil recordado y publica el JWT en memoria.
  // El código de recuperación queda disponible solo para mostrarlo en esta operación.
  async guardarSesion(sesion: IAutenticacion): Promise<void> {
    const cambiaJugador = this.sesion()?.usuarioId !== sesion.usuarioId;
    const existente = this.guardados.perfiles.find((p) => p.usuarioId === sesion.usuarioId);
    const perfil = {
      usuarioId: sesion.usuarioId,
      nombreUsuario: sesion.nombreUsuario,
      credencial: sesion.credencial || existente?.credencial,
    };
    this.guardados.perfiles = [...this.guardados.perfiles.filter((p) => p.usuarioId !== sesion.usuarioId), perfil];
    this.guardados.activo = sesion.usuarioId;
    await this.persistir();
    this.sesion.set(sesion);
    this.mensajeSesion.set('');
    this.motivoCierre.set(null);

    if (cambiaJugador || sesion.codigoRecuperacionNuevo) {
      this.codigoRecuperacionNuevo.set(sesion.codigoRecuperacionNuevo ?? '');
      this.codigoRecuperacionPorConfirmar.set(sesion.codigoRecuperacionPorConfirmar === true);
    }

    if (sesion.salaActivaId) {
      sessionStorage.setItem('tetrisSala', sesion.salaActivaId);
    } else if ('salaActivaId' in sesion) {
      sessionStorage.removeItem('tetrisSala');
    }
  }

  // Guarda perfiles y credenciales en el almacén seguro de Android o en localStorage web.
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

  // Devuelve el JWT actual o comparte una renovación cuando queda menos de un minuto.
  async token(): Promise<string> {
    const sesion = this.sesion();

    if (!sesion) {
      throw new Error('Ingresá o recuperá tu jugador para continuar.');
    }

    if (Date.parse(sesion.expiraEnUtc) - Date.now() > 60000) {
      return sesion.token;
    }

    this.renovacion ??= this.renovar().finally(() => {
      this.renovacion = undefined;
    });
    return this.renovacion;
  }

  // Pide un JWT para la misma sesión y descarta la respuesta si ya cambió el ingreso.
  private async renovar(): Promise<string> {
    const tokenAnterior = this.sesion()?.token;
    const perfil = this.guardados.perfiles.find((p) => p.usuarioId === this.sesion()?.usuarioId);

    if (!perfil?.credencial) {
      throw new Error('No hay una credencial válida para renovar la sesión.');
    }

    let sesion: IAutenticacion;

    try {
      sesion = await this.api.solicitar<IAutenticacion>('/api/usuarios/renovar', 'POST', {
        credencial: perfil.credencial,
      });
    } catch (error) {
      if (error instanceof ErrorApi && error.estado === 401 && this.sesion()?.token === tokenAnterior) {
        this.perderControl(error.codigo, tokenAnterior);
      }

      throw error;
    }

    if (this.sesion()?.token !== tokenAnterior) {
      throw new Error('La sesion cambio durante la renovacion.');
    }

    await this.guardarSesion({ ...sesion, credencial: sesion.credencial ?? perfil.credencial });
    return sesion.token;
  }

  // Adjunta un token vigente y rechaza respuestas que lleguen después de cambiar la sesión.
  async solicitar<T>(ruta: string, metodo = 'GET', cuerpo?: unknown): Promise<T> {
    const token = await this.token();

    try {
      const respuesta = await this.api.solicitar<T>(ruta, metodo, cuerpo, token);

      if (this.sesion()?.token !== token) {
        throw new Error('La sesión cambió durante la solicitud.');
      }

      return respuesta;
    } catch (error) {
      if (
        error instanceof ErrorApi &&
        error.estado === 401 &&
        this.sesion()?.token === token &&
        ['SesionReemplazada', 'SesionInvalida', 'SesionVencida', 'CredencialInvalida'].includes(
          error.codigo ?? 'SesionInvalida',
        )
      ) {
        this.perderControl(error.codigo, token);
      }

      throw error;
    }
  }

  // Cancela solicitudes y limpia la sesión privada sin borrar los perfiles recordados.
  perderControl(codigo?: string | null, tokenEsperado?: string): void {
    const token = this.sesion()?.token;

    if (!token || (tokenEsperado && tokenEsperado !== token)) {
      return;
    }

    this.api.cancelarSolicitudes?.(token);
    this.sesion.set(null);
    this.codigoRecuperacionNuevo.set('');
    this.codigoRecuperacionPorConfirmar.set(false);
    sessionStorage.removeItem('tetrisSala');
    this.informarCierre(codigo);
    // Conservar perfiles y credenciales: nunca recuperar automaticamente el control perdido.
  }

  // Convierte el código del servidor en el motivo y mensaje que muestra el ingreso.
  private informarCierre(codigo?: string | null): void {
    const motivo = codigo === 'SesionReemplazada' || codigo === 'SesionVencida' ? codigo : 'SesionInvalida';
    this.motivoCierre.set(motivo);
    this.mensajeSesion.set(
      motivo === 'SesionReemplazada'
        ? 'Alguien más inició sesión con tu cuenta en otro dispositivo. Por seguridad, cerramos esta sesión.'
        : motivo === 'SesionVencida'
          ? 'Tu sesión venció. Iniciá sesión nuevamente para continuar.'
          : 'Esta sesión ya no está disponible. Iniciá sesión nuevamente para continuar.',
    );
  }

  // Consulta los datos públicos propios sin reemplazar una sesión de otro usuario.
  async actualizarPerfil(): Promise<IUsuario> {
    const perfil = await this.solicitar<IUsuario>('/api/usuarios/yo');
    this.sesion.update((sesion) => (sesion?.usuarioId === perfil.usuarioId ? { ...sesion, ...perfil } : sesion));
    return perfil;
  }

  // Deja de usar el perfil activo localmente sin borrar la cuenta del servidor.
  async cambiarJugador(): Promise<void> {
    this.codigoRecuperacionNuevo.set('');
    this.codigoRecuperacionPorConfirmar.set(false);
    this.guardados.activo = null;
    await this.persistir();
    this.sesion.set(null);
    sessionStorage.removeItem('tetrisSala');
  }

  // Revoca la sesión en el backend y retira su credencial de acceso local.
  async cerrarSesion(): Promise<void> {
    await this.solicitar<void>('/api/usuarios/logout', 'POST');
    const usuarioId = this.sesion()!.usuarioId;
    this.guardados.perfiles = this.guardados.perfiles.map((p) =>
      p.usuarioId === usuarioId ? { ...p, credencial: undefined } : p,
    );
    await this.cambiarJugador();
  }

  // Elimina un perfil recordado de este dispositivo; no elimina la cuenta de SQL.
  async quitarPerfil(usuarioId: number): Promise<void> {
    this.guardados.perfiles = this.guardados.perfiles.filter((p) => p.usuarioId !== usuarioId);
    await this.persistir();
  }

  // Recupera la cuenta con su código privado y recibe el nuevo código pendiente.
  async recuperarCodigo(credencial: string): Promise<void> {
    await this.guardarSesion(
      await this.api.solicitar<IAutenticacion>('/api/usuarios/recuperar-codigo', 'POST', {
        credencial: credencial.trim(),
      }),
    );
  }

  // Confirma el código pendiente en el servidor y lo quita de la pantalla al terminar.
  async confirmarCodigoGuardado(): Promise<void> {
    const codigo = this.codigoRecuperacionNuevo();
    const token = this.sesion()?.token;

    if (this.codigoRecuperacionPorConfirmar()) {
      await this.solicitar('/api/usuarios/confirmar-codigo-recuperacion', 'POST', { credencial: codigo });
    }

    if (this.sesion()?.token === token && this.codigoRecuperacionNuevo() === codigo) {
      this.codigoRecuperacionNuevo.set('');
      this.codigoRecuperacionPorConfirmar.set(false);
    }
  }
}
