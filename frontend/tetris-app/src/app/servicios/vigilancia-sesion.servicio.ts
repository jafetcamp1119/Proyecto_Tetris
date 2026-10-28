// Comprueba la sesión incluso fuera de una partida y atiende su reemplazo.
// Cierra la interfaz privada cuando el servidor confirma que se perdió el acceso.
import { DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { NavController } from '@ionic/angular';
import {
  HttpTransportType,
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { AutenticacionServicio } from './autenticacion.servicio';
import { ApiServicio } from './api.servicio';
import { SalasServicio } from './salas.servicio';
import { DialogosServicio } from './dialogos.servicio';

@Injectable({ providedIn: 'root' })
export class VigilanciaSesionServicio {
  private readonly acceso = inject(AutenticacionServicio);
  private readonly api = inject(ApiServicio);
  private readonly salas = inject(SalasServicio);
  private readonly navegacion = inject(NavController);
  private readonly dialogos = inject(DialogosServicio);
  private conexion?: HubConnection;
  private tokenConexion?: string;
  private comprobacion?: Promise<void>;
  private ultimaComprobacion = 0;
  private ultimoIntentoConexion = 0;
  private comprobacionDiferida?: ReturnType<typeof setTimeout>;
  private cierreAtendido = false;
  private destruido = false;
  readonly cerrando = signal(false);
  readonly mensajeConexion = signal('');

  constructor() {
    effect(() => {
      const token = this.acceso.sesion()?.token;

      if (token) {
        this.cierreAtendido = false;
        this.cerrando.set(false);
        this.asegurarConexion(token);
      } else {
        this.detenerConexion();

        if (this.acceso.motivoCierre() && !this.cierreAtendido) {
          this.cerrarInterfaz();
        }
      }
    });
    const reloj = setInterval(() => {
      if (!document.hidden) {
        void this.verificar();
      }
    }, 60000);
    inject(DestroyRef).onDestroy(() => {
      this.destruido = true;
      clearInterval(reloj);
      clearTimeout(this.comprobacionDiferida);
      this.detenerConexion();
    });
  }

  // Agrupa comprobaciones simultáneas y deja al menos cinco segundos entre consultas.
  verificar(alActivar = false): Promise<void> {
    if (this.destruido || !this.acceso.sesion() || (!alActivar && document.hidden)) {
      return Promise.resolve();
    }

    if (this.comprobacion) {
      return this.comprobacion;
    }

    const restante = 5000 - (Date.now() - this.ultimaComprobacion);

    if (restante > 0) {
      this.comprobacionDiferida ??= setTimeout(() => {
        this.comprobacionDiferida = undefined;
        void this.verificar(alActivar);
      }, restante);
      return Promise.resolve();
    }

    this.ultimaComprobacion = Date.now();
    this.comprobacion = this.comprobarSesion().finally(() => {
      this.comprobacion = undefined;
    });
    return this.comprobacion;
  }

  // Consulta el usuario propio y conserva la sesión si el fallo solo es de conectividad.
  private async comprobarSesion(): Promise<void> {
    const token = this.acceso.sesion()?.token;

    try {
      await this.acceso.solicitar('/api/usuarios/yo');
      this.mensajeConexion.set('');
      const actual = this.acceso.sesion()?.token;

      if (actual) {
        this.asegurarConexion(actual);
      }
    } catch {
      // Un fallo de red o del servidor no acredita un ingreso desde otro dispositivo.
      if (this.acceso.sesion()?.token === token) {
        this.mensajeConexion.set(
          navigator.onLine === false
            ? 'No tenés conexión a Internet. Revisá la conexión y reintentá.'
            : 'Servidor no disponible. Revisá tu conexión e intentá nuevamente.',
        );
      }
    }
  }

  // Abre un observador de cuenta que recibe cierres aunque no se haya entrado a una sala.
  private asegurarConexion(token: string): void {
    if (
      this.destruido ||
      (this.tokenConexion === token &&
        this.conexion &&
        (this.conexion.state !== HubConnectionState.Disconnected || Date.now() - this.ultimoIntentoConexion < 30000))
    ) {
      return;
    }

    this.detenerConexion();
    this.tokenConexion = token;
    this.ultimoIntentoConexion = Date.now();
    this.mensajeConexion.set('');
    // Observador de cuenta sin entrar a una sala: funciona también en Inicio, Perfil y Ajustes.
    const conexion = new HubConnectionBuilder()
      .withUrl(`${this.api.url()}/hubs/juego`, {
        accessTokenFactory: () => token,
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
      .configureLogging(LogLevel.None)
      .build();
    this.conexion = conexion;
    conexion.on('SesionReemplazada', () => this.recibirCierre(conexion, token, 'SesionReemplazada'));
    conexion.on('SesionTerminada', (datos: { codigo?: string }) => this.recibirCierre(conexion, token, datos.codigo));
    conexion.onclose(() => {
      if (this.conexion === conexion) {
        void this.verificar();
      }
    });
    conexion.onreconnecting(() => {
      if (this.conexion === conexion) {
        void this.verificar();
      }
    });
    conexion.onreconnected(() => {
      if (this.conexion === conexion) {
        void this.verificar();
      }
    });
    void conexion
      .start()
      .then(() => {
        if (this.conexion !== conexion) {
          return conexion.stop();
        }

        return undefined;
      })
      .catch(() => {
        if (this.conexion === conexion) {
          void this.verificar();
        }
      });
  }

  // Atiende el cierre solo si pertenece a la conexión y al token actuales.
  private recibirCierre(conexion: HubConnection, token: string, codigo?: string): void {
    if (this.conexion === conexion) {
      this.acceso.perderControl(codigo, token);
    }
  }

  // Retira la referencia activa antes de detener el observador anterior.
  private detenerConexion(): void {
    const anterior = this.conexion;
    this.conexion = undefined;
    this.tokenConexion = undefined;

    if (anterior) {
      void anterior.stop().catch(() => undefined);
    }
  }

  // Cierra conexiones y diálogos privados y vuelve al ingreso con navegación raíz.
  private cerrarInterfaz(): void {
    this.cierreAtendido = true;
    this.cerrando.set(true);
    this.mensajeConexion.set('');
    // Limpiar ocurre sincrónicamente antes del primer await; nunca se envía Abandonar ni logout.
    void this.salas.desconectar().catch(() => undefined);
    void this.dialogos.cerrarPendientes();
    void this.navegacion
      .navigateRoot('/login', { replaceUrl: true, animated: false })
      .finally(() => this.cerrando.set(false));
  }
}
