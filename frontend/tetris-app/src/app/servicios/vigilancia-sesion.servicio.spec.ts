import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { NavController } from '@ionic/angular';
import { Capacitor } from '@capacitor/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { VigilanciaSesionServicio } from './vigilancia-sesion.servicio';
import { AutenticacionServicio } from './autenticacion.servicio';
import { ApiServicio, ErrorApi } from './api.servicio';
import { SalasServicio } from './salas.servicio';
import { DialogosServicio } from './dialogos.servicio';
import { IAutenticacion } from '../modelos/tetris.model';
import { sesionGuard } from './sesion.guard';
import { PartidaPage } from '../pages/partida/partida.page';

vi.mock('@aparajita/capacitor-secure-storage', () => ({ SecureStorage: { getItem: vi.fn(), setItem: vi.fn() } }));

@Component({ selector: 'app-login-prueba', template: 'Pantalla pública' })
class LoginPruebaComponent {}
@Component({ selector: 'app-privada-prueba', template: 'Tablero privado' })
class PrivadaPruebaComponent {}

function sesion(token = 'pc'): IAutenticacion {
  return {
    usuarioId: 1,
    nombreUsuario: 'Cuenta',
    token,
    tipoToken: 'Bearer',
    credencial: 'C'.repeat(64),
    expiraEnUtc: new Date(Date.now() + 3600000).toISOString(),
    fechaRegistro: '',
    activo: true,
    googleVinculado: false,
    avatarGoogle: null,
    credencialExpiraEnUtc: '',
  };
}

describe('Cierre central de la sesión anterior', () => {
  let acceso: AutenticacionServicio;
  let vigilancia: VigilanciaSesionServicio;
  let solicitar: ReturnType<typeof vi.fn>;
  let navegar: ReturnType<typeof vi.fn>;
  let desconectar: ReturnType<typeof vi.fn>;
  let cancelar: ReturnType<typeof vi.fn>;
  let conexiones: { eventos: Map<string, (datos?: unknown) => void>; stop: ReturnType<typeof vi.fn> }[];

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    solicitar = vi.fn().mockResolvedValue({});
    navegar = vi.fn().mockResolvedValue(true);
    desconectar = vi.fn().mockResolvedValue(undefined);
    cancelar = vi.fn();
    conexiones = [];
    vi.spyOn(HubConnectionBuilder.prototype, 'build').mockImplementation(() => {
      const eventos = new Map<string, (datos?: unknown) => void>();
      const conexion = {
        eventos,
        state: HubConnectionState.Connected,
        start: vi.fn().mockResolvedValue(undefined),
        stop: vi.fn().mockResolvedValue(undefined),
        on: vi.fn((nombre, accion) => eventos.set(nombre, accion)),
        onclose: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
      };
      conexiones.push(conexion);
      return conexion as unknown as HubConnection;
    });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: ApiServicio,
          useValue: { solicitar, cancelarSolicitudes: cancelar, url: () => 'https://prueba.trycloudflare.com' },
        },
        { provide: SalasServicio, useValue: { desconectar } },
        { provide: DialogosServicio, useValue: { cerrarPendientes: vi.fn().mockResolvedValue(undefined) } },
        { provide: NavController, useValue: { navigateRoot: navegar } },
      ],
    });
    acceso = TestBed.inject(AutenticacionServicio);
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
  });

  async function iniciar(): Promise<void> {
    vigilancia = TestBed.inject(VigilanciaSesionServicio);
    await acceso.inicializar();
    await acceso.guardarSesion(sesion());
    TestBed.tick();
  }

  for (const nativo of [false, true]) {
    it(`el evento expulsa ${nativo ? 'Android hacia PC' : 'PC hacia Android'} sin abandonar ni borrar credenciales`, async () => {
      vi.spyOn(Capacitor, 'isNativePlatform').mockReturnValue(nativo);
      await iniciar();
      sessionStorage.setItem('tetrisSala', 'partida');
      acceso.codigoRecuperacionNuevo.set('PRIVADO');
      conexiones[0].eventos.get('SesionReemplazada')!();
      conexiones[0].eventos.get('SesionReemplazada')!();
      TestBed.tick();
      expect(acceso.sesion()).toBeNull();
      expect(acceso.mensajeSesion()).toBe(
        'Alguien más inició sesión con tu cuenta en otro dispositivo. Por seguridad, cerramos esta sesión.',
      );
      expect(acceso.codigoRecuperacionNuevo()).toBe('');
      expect(sessionStorage.getItem('tetrisSala')).toBeNull();
      expect(acceso.perfiles()[0].credencial).toBe('C'.repeat(64));
      expect(cancelar).toHaveBeenCalledWith('pc');
      expect(desconectar).toHaveBeenCalledTimes(1);
      expect(navegar).toHaveBeenCalledExactlyOnceWith('/login', { replaceUrl: true, animated: false });
      expect(solicitar.mock.calls.some((c) => /abandonar|logout/.test(c[0]))).toBe(false);
      expect(conexiones[0].stop).toHaveBeenCalledTimes(1);
    });
  }

  it('al volver sin SignalR, un 401 identificado detecta el reemplazo y agrupa comprobaciones', async () => {
    await iniciar();
    solicitar.mockRejectedValueOnce(new ErrorApi('Texto genérico', 401, 'SesionReemplazada'));
    await Promise.all([vigilancia.verificar(), vigilancia.verificar(), vigilancia.verificar()]);
    TestBed.tick();
    expect(solicitar).toHaveBeenCalledTimes(1);
    expect(acceso.motivoCierre()).toBe('SesionReemplazada');
    expect(navegar).toHaveBeenCalledTimes(1);
  });

  it('backend apagado o Internet desconectado conservan la sesión sin acusar otro ingreso', async () => {
    await iniciar();
    solicitar.mockRejectedValueOnce(new Error('No se pudo conectar'));
    await vigilancia.verificar();
    expect(acceso.sesion()?.token).toBe('pc');
    expect(acceso.motivoCierre()).toBeNull();
    expect(vigilancia.mensajeConexion()).toContain('conexión');
    expect(navegar).not.toHaveBeenCalled();
  });

  it('un vencimiento no se presenta como reemplazo', async () => {
    await iniciar();
    solicitar.mockRejectedValueOnce(new ErrorApi('Vencido', 401, 'SesionVencida'));
    await vigilancia.verificar();
    TestBed.tick();
    expect(acceso.motivoCierre()).toBe('SesionVencida');
    expect(acceso.mensajeSesion()).not.toContain('otro dispositivo');
    expect(navegar).toHaveBeenCalledTimes(1);
  });

  it('el evento retrasado de una conexión vieja no expulsa el ingreso nuevo', async () => {
    await iniciar();
    const eventoViejo = conexiones[0].eventos.get('SesionReemplazada')!;
    await acceso.guardarSesion(sesion('telefono'));
    TestBed.tick();
    eventoViejo();
    TestBed.tick();
    expect(acceso.sesion()?.token).toBe('telefono');
    expect(navegar).not.toHaveBeenCalled();
    expect(conexiones).toHaveLength(2);
  });

  it('la navegación raíz y el guard impiden volver al tablero tras la expulsión', async () => {
    navegar.mockImplementation((ruta: string, opciones: object) =>
      TestBed.inject(Router).navigateByUrl(ruta, opciones),
    );
    TestBed.inject(Router).resetConfig([
      { path: 'login', component: LoginPruebaComponent },
      {
        path: 'partida/prueba',
        component: PrivadaPruebaComponent,
        canActivate: [sesionGuard],
        canDeactivate: [() => PartidaPage.prototype.puedeSalir.call({ acceso } as unknown as PartidaPage)],
      },
    ]);
    await iniciar();
    const harness = await RouterTestingHarness.create('/partida/prueba');
    expect(harness.routeNativeElement?.textContent).toContain('Tablero privado');
    conexiones[0].eventos.get('SesionReemplazada')!();
    TestBed.tick();
    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/login'));
    await harness.navigateByUrl('/partida/prueba');
    expect(TestBed.inject(Router).url).toBe('/login');
    expect(harness.routeNativeElement?.textContent).not.toContain('Tablero privado');
  });
});
