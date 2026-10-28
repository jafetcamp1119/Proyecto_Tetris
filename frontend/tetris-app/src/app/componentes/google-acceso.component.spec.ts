import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { GoogleAccesoComponent } from './google-acceso.component';
import { GoogleServicio } from '../servicios/google.servicio';
import { ErrorCargaGoogle, GoogleWebServicio } from '../servicios/google-web.servicio';
import { signal } from '@angular/core';

describe('Acceso Google web', () => {
  const preparar = vi.fn();
  const cargar = vi.fn();
  const mostrarBoton = vi.fn();

  beforeEach(() => {
    preparar.mockReset().mockResolvedValue({ id: 'desafio', nonce: 'nonce' });
    cargar.mockReset().mockResolvedValue(undefined);
    mostrarBoton.mockReset();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: GoogleServicio,
          useValue: { preparar, completar: vi.fn(), cancelarRegistro: vi.fn(), registroPendiente: signal(null) },
        },
        { provide: GoogleWebServicio, useValue: { cargar, mostrarBoton } },
      ],
    });
  });

  it('muestra el boton con nonce del servidor e ID web de produccion', async () => {
    const fixture = TestBed.createComponent(GoogleAccesoComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    await vi.waitFor(() => {
      expect(mostrarBoton).toHaveBeenCalledWith(
        expect.any(HTMLElement),
        '652877629973-kt2nkjl56mvgv6udli3m92mf64jcfcrs.apps.googleusercontent.com',
        'nonce',
        expect.any(Function),
      );
    });
    fixture.destroy();
  });

  it('distingue backend inaccesible y permite reintentar sin borrar el jugador', async () => {
    preparar.mockRejectedValueOnce(new Error('Sin conexion'));
    const fixture = TestBed.createComponent(GoogleAccesoComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('servidor del juego');
    expect(fixture.nativeElement.textContent).toContain('Reintentar Google');
    expect(cargar).not.toHaveBeenCalled();
    await fixture.componentInstance.reintentar();
    expect(preparar).toHaveBeenCalledTimes(2);
    expect(mostrarBoton).toHaveBeenCalledTimes(1);
    fixture.destroy();
  });

  it('distingue fallo de carga de GIS de fallo del backend', async () => {
    cargar.mockRejectedValue(new ErrorCargaGoogle('Bloqueado'));
    const fixture = TestBed.createComponent(GoogleAccesoComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    await vi.waitFor(() => {
      expect(fixture.componentInstance.mensaje).toContain('bloqueadores');
    });
    expect(mostrarBoton).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it('presenta el nickname de primera vez sin emitir un ingreso antes de completar el registro', async () => {
    const fixture = TestBed.createComponent(GoogleAccesoComponent);
    const finalizado = vi.spyOn(fixture.componentInstance.finalizado, 'emit');
    fixture.detectChanges();
    await fixture.whenStable();
    TestBed.inject(GoogleServicio).registroPendiente.set({
      requiereRegistro: true,
      comprobanteRegistro: 'C'.repeat(64),
      expiraEnUtc: new Date(Date.now() + 300000).toISOString(),
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('¡Bienvenido a Tetris!');
    expect(fixture.nativeElement.querySelector('#nombre-google')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Crear mi cuenta');
    expect(fixture.nativeElement.querySelector('.google-web').style.display).toBe('none');
    expect(finalizado).not.toHaveBeenCalled();
    fixture.destroy();
  });
});
