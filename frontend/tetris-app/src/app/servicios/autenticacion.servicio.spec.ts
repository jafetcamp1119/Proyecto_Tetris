import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { Capacitor } from '@capacitor/core';
import { SecureStorage } from '@aparajita/capacitor-secure-storage';
import { ApiServicio, ErrorApi } from './api.servicio';
import { AutenticacionServicio } from './autenticacion.servicio';
import { IAutenticacion } from '../modelos/tetris.model';

vi.mock('@aparajita/capacitor-secure-storage', () => ({
  SecureStorage: { getItem: vi.fn(), setItem: vi.fn() },
}));

describe('Identidades y progreso del dispositivo', () => {
  const sesion = (usuarioId: number): IAutenticacion => ({
    usuarioId,
    nombreUsuario: `Jugador${usuarioId}`,
    googleVinculado: false,
    avatarGoogle: null,
    fechaRegistro: '',
    activo: true,
    token: `jwt${usuarioId}`,
    tipoToken: 'Bearer',
    credencial: 'A'.repeat(63) + usuarioId,
    credencialExpiraEnUtc: '',
    expiraEnUtc: new Date(Date.now() + 3600000).toISOString(),
  });
  let solicitar: ReturnType<typeof vi.fn>;
  let acceso: AutenticacionServicio;

  beforeEach(() => {
    localStorage.clear();
    solicitar = vi.fn();
    TestBed.configureTestingModule({ providers: [{ provide: ApiServicio, useValue: { solicitar } }] });
    acceso = TestBed.inject(AutenticacionServicio);
  });

  afterEach(() => vi.restoreAllMocks());

  it('una respuesta privada tardía no se entrega a la cuenta nueva', async () => {
    await acceso.guardarSesion(sesion(1));
    let completar!: (dato: unknown) => void;
    solicitar.mockReturnValueOnce(
      new Promise((resolve) => {
        completar = resolve;
      }),
    );
    const pendiente = acceso.solicitar('/api/usuarios/yo');
    await vi.waitFor(() => expect(solicitar).toHaveBeenCalled());
    await acceso.guardarSesion(sesion(2));
    completar({ usuarioId: 1 });
    await expect(pendiente).rejects.toThrow('sesión cambió');
    expect(acceso.sesion()?.usuarioId).toBe(2);
  });

  it('un error al confirmar conserva el codigo; el reintento lo activa sin persistirlo', async () => {
    const codigo = 'K7PM-42QX-8R2D';
    await acceso.guardarSesion({ ...sesion(1), codigoRecuperacionNuevo: codigo, codigoRecuperacionPorConfirmar: true });
    expect(localStorage.getItem('tetris.perfiles.v1')).not.toContain(codigo);
    solicitar.mockRejectedValueOnce(new Error('Sin red'));
    await expect(acceso.confirmarCodigoGuardado()).rejects.toThrow('Sin red');
    expect(acceso.codigoRecuperacionNuevo()).toBe(codigo);
    expect(acceso.codigoRecuperacionPorConfirmar()).toBe(true);
    solicitar.mockResolvedValueOnce(true);
    await acceso.confirmarCodigoGuardado();
    expect(solicitar).toHaveBeenLastCalledWith(
      '/api/usuarios/confirmar-codigo-recuperacion',
      'POST',
      { credencial: codigo },
      'jwt1',
    );
    expect(acceso.codigoRecuperacionNuevo()).toBe('');
    expect(acceso.codigoRecuperacionPorConfirmar()).toBe(false);
  });

  it('un ID token de Google rechazado no cierra la sesion valida de Tetris', async () => {
    await acceso.guardarSesion(sesion(1));
    solicitar.mockRejectedValueOnce(new ErrorApi('Google invalido', 401, 'GoogleInvalido'));
    await expect(acceso.solicitar('/api/google/vincular', 'POST')).rejects.toThrow();
    expect(acceso.sesion()?.usuarioId).toBe(1);
  });

  it('restaurar un dispositivo desplazado no inicia sesion automaticamente ni borra su credencial', async () => {
    localStorage.setItem(
      'tetris.perfiles.v1',
      JSON.stringify({
        activo: 1,
        perfiles: [{ usuarioId: 1, nombreUsuario: 'Uno', credencial: sesion(1).credencial }],
      }),
    );
    solicitar.mockRejectedValueOnce(new ErrorApi('Sesion reemplazada', 401));
    await acceso.inicializar();
    expect(solicitar).toHaveBeenCalledWith('/api/usuarios/renovar', 'POST', { credencial: sesion(1).credencial });
    expect(solicitar.mock.calls.some((llamada) => llamada[0] === '/api/usuarios/recuperar')).toBe(false);
    expect(acceso.sesion()).toBeNull();
    expect(acceso.perfiles()[0].credencial).toBe(sesion(1).credencial);
  });

  it('una respuesta vieja de renovacion no reemplaza la cuenta nueva', async () => {
    await acceso.guardarSesion({ ...sesion(1), expiraEnUtc: new Date(0).toISOString() });
    let completar!: (valor: IAutenticacion) => void;
    solicitar.mockReturnValueOnce(
      new Promise<IAutenticacion>((resolve) => {
        completar = resolve;
      }),
    );
    const renovacion = acceso.token();
    await acceso.guardarSesion(sesion(2));
    completar(sesion(1));
    await expect(renovacion).rejects.toThrow('sesion cambio');
    expect(acceso.sesion()?.usuarioId).toBe(2);
  });

  it('cambia de jugador conservando la credencial individual sin revocar cuentas', async () => {
    await acceso.inicializar();
    await acceso.guardarSesion(sesion(1));
    await acceso.guardarSesion(sesion(2));
    await acceso.cambiarJugador();
    expect(acceso.sesion()).toBeNull();
    expect(acceso.perfiles()).toHaveLength(2);
    solicitar.mockResolvedValueOnce({ ...sesion(1), credencial: undefined });
    await acceso.seleccionar(1);
    expect(acceso.sesion()?.usuarioId).toBe(1);
    expect(solicitar).toHaveBeenCalledWith('/api/usuarios/recuperar', 'POST', { credencial: sesion(1).credencial });
    expect(solicitar.mock.calls.some((llamada) => llamada[0].includes('logout'))).toBe(false);
  });

  it('cerrar sesión revoca solo la credencial activa y no borra los otros perfiles', async () => {
    await acceso.inicializar();
    await acceso.guardarSesion(sesion(1));
    await acceso.guardarSesion(sesion(2));
    solicitar.mockResolvedValueOnce(undefined);
    await acceso.cerrarSesion();
    expect(acceso.sesion()).toBeNull();
    expect(acceso.perfiles().find((p) => p.usuarioId === 1)?.credencial).toBe(sesion(1).credencial);
    expect(acceso.perfiles().find((p) => p.usuarioId === 2)?.credencial).toBeUndefined();
    expect(solicitar).toHaveBeenCalledWith('/api/usuarios/logout', 'POST', undefined, 'jwt2');
  });

  it('una falla de recuperación no elimina perfiles ni estadísticas del servidor', async () => {
    await acceso.inicializar();
    await acceso.guardarSesion(sesion(1));
    await acceso.cambiarJugador();
    solicitar.mockRejectedValueOnce(new ErrorApi('Código inválido', 401));
    await expect(acceso.recuperarCodigo('B'.repeat(64))).rejects.toThrow('Código inválido');
    expect(acceso.perfiles()[0].credencial).toBe(sesion(1).credencial);
  });

  it('Android escribe en almacenamiento seguro y no degrada a localStorage', async () => {
    vi.spyOn(Capacitor, 'isNativePlatform').mockReturnValue(true);
    vi.spyOn(SecureStorage, 'getItem').mockResolvedValue(null);
    const guardar = vi.spyOn(SecureStorage, 'setItem').mockResolvedValue(undefined);
    await acceso.inicializar();
    await acceso.guardarSesion(sesion(1));
    expect(guardar).toHaveBeenCalled();
    expect(localStorage.getItem('tetris.perfiles.v1')).toBeNull();
    guardar.mockRejectedValueOnce(new Error('Keystore no disponible'));
    await expect(acceso.guardarSesion(sesion(2))).rejects.toThrow('Keystore no disponible');
    expect(localStorage.getItem('tetris.perfiles.v1')).toBeNull();
  });
});
