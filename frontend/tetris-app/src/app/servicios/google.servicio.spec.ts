import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SocialLogin } from '@capgo/capacitor-social-login';
import { GoogleServicio } from './google.servicio';
import { ApiServicio, ErrorApi } from './api.servicio';
import { AutenticacionServicio } from './autenticacion.servicio';

vi.mock('@capgo/capacitor-social-login', () => ({
  SocialLogin: { initialize: vi.fn(), login: vi.fn(), logout: vi.fn() },
}));
describe('Un botón Google para ingresar o completar el registro', () => {
  const solicitar = vi.fn();
  const guardarSesion = vi.fn();
  let google: GoogleServicio;
  const pendiente = {
    requiereRegistro: true,
    comprobanteRegistro: 'C'.repeat(64),
    expiraEnUtc: new Date(Date.now() + 300000).toISOString(),
  };
  beforeEach(() => {
    solicitar.mockReset();
    guardarSesion.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: ApiServicio, useValue: { solicitar } },
        { provide: AutenticacionServicio, useValue: { sesion: signal(null), guardarSesion } },
      ],
    });
    google = TestBed.inject(GoogleServicio);
  });
  it('un Google desconocido pide nickname y permite corregir un nombre ocupado sin repetir OAuth', async () => {
    solicitar.mockResolvedValueOnce(pendiente);
    await google.completar({ id: 'nonce-id', nonce: 'nonce' }, 'token-verificado-en-servidor', false);
    expect(google.registroPendiente()).toEqual(pendiente);
    expect(guardarSesion).not.toHaveBeenCalled();
    solicitar.mockRejectedValueOnce(new ErrorApi('Nombre ocupado', 409, 'NombreOcupado'));
    await expect(google.registrar('Existente')).rejects.toThrow('Nombre ocupado');
    expect(google.registroPendiente()).toEqual(pendiente);
    const sesion = { usuarioId: 8, token: 'jwt' };
    solicitar.mockResolvedValueOnce(sesion);
    await google.registrar(' Nuevo ');
    expect(solicitar).toHaveBeenLastCalledWith('/api/google/registrar', 'POST', {
      nombreUsuario: 'Nuevo',
      comprobanteRegistro: pendiente.comprobanteRegistro,
    });
    expect(guardarSesion).toHaveBeenCalledWith(sesion);
    expect(google.registroPendiente()).toBeNull();
  });
  it('Google ya vinculado ingresa sin pedir otro nickname', async () => {
    const sesion = { usuarioId: 8, nombreUsuario: 'Anterior', token: 'jwt' };
    solicitar.mockResolvedValueOnce(sesion);
    await google.completar({ id: 'id', nonce: 'nonce' }, 'id-token', false);
    expect(guardarSesion).toHaveBeenCalledWith(sesion);
    expect(google.registroPendiente()).toBeNull();
    expect(solicitar).toHaveBeenCalledTimes(1);
  });
  it('Android conserva el plugin nativo, nonce y el mismo registro en dos etapas', async () => {
    vi.mocked(SocialLogin.initialize).mockResolvedValue(undefined);
    vi.mocked(SocialLogin.logout).mockResolvedValue(undefined);
    vi.mocked(SocialLogin.login).mockResolvedValue({
      result: { responseType: 'online', idToken: 'id-token' },
    } as Awaited<ReturnType<typeof SocialLogin.login>>);
    solicitar.mockResolvedValueOnce({ id: 'id', nonce: 'nonce-nativo' }).mockResolvedValueOnce(pendiente);
    await google.ingresarAndroid(false);
    expect(SocialLogin.login).toHaveBeenCalledWith({
      provider: 'google',
      options: { nonce: 'nonce-nativo', style: 'standard', autoSelectEnabled: false },
    });
    expect(solicitar).toHaveBeenLastCalledWith('/api/google/ingresar', 'POST', {
      desafioId: 'id',
      idToken: 'id-token',
    });
    expect(google.registroPendiente()).toEqual(pendiente);
  });
});
