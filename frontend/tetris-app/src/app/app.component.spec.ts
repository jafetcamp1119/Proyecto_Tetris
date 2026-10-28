import { signal } from '@angular/core';
import { OrientacionServicio } from './servicios/orientacion.servicio';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app.component';
import { vi } from 'vitest';
import { Capacitor } from '@capacitor/core';
import { App } from '@capacitor/app';
import { Platform } from '@ionic/angular';
import { VigilanciaSesionServicio } from './servicios/vigilancia-sesion.servicio';

vi.mock('@capacitor/app', () => ({ App: { addListener: vi.fn(), exitApp: vi.fn() } }));

describe('AppComponent', () => {
  it('Capacitor verifica al reactivar la app usando el listener compartido existente', async () => {
    vi.spyOn(Capacitor, 'isNativePlatform').mockReturnValue(true);
    const listener = vi
      .mocked(App.addListener)
      .mockReset()
      .mockResolvedValue({ remove: vi.fn().mockResolvedValue(undefined) });
    const comprobar = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: VigilanciaSesionServicio, useValue: { verificar: comprobar } },
        {
          provide: Platform,
          useValue: { backButton: { subscribeWithPriority: () => ({ unsubscribe: () => undefined }) } },
        },
      ],
    });
    const fixture = TestBed.createComponent(AppComponent);
    await Promise.resolve();
    const activar = listener.mock.calls[0][1] as unknown as (estado: { isActive: boolean }) => void;
    activar({ isActive: false });
    expect(comprobar).not.toHaveBeenCalled();
    activar({ isActive: true });
    expect(comprobar).toHaveBeenCalledTimes(1);
    expect(comprobar).toHaveBeenCalledWith(true);
    fixture.destroy();
    vi.restoreAllMocks();
  });
  it('comprueba al recuperar foco, Internet o visibilidad sin duplicar lógica por página', () => {
    TestBed.configureTestingModule({ imports: [AppComponent], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const comprobar = vi.spyOn(fixture.componentInstance.vigilancia, 'verificar').mockResolvedValue(undefined);
    window.dispatchEvent(new Event('focus'));
    window.dispatchEvent(new Event('online'));
    document.dispatchEvent(new Event('visibilitychange'));
    expect(comprobar).toHaveBeenCalledTimes(3);
    fixture.destroy();
  });
  it('should create the app', async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('superpone el aviso sin desmontar el outlet que contiene la partida', () => {
    const horizontal = signal(false);
    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: OrientacionServicio, useValue: { requiereVertical: horizontal.asReadonly() } },
      ],
    });
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const outlet = fixture.nativeElement.querySelector('ion-router-outlet');
    horizontal.set(true);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Girá tu celular para continuar jugando');
    expect(outlet.hasAttribute('inert')).toBe(true);
    horizontal.set(false);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('ion-router-outlet')).toBe(outlet);
    expect(outlet.hasAttribute('inert')).toBe(false);
    expect(fixture.nativeElement.querySelector('.aviso-orientacion')).toBeNull();
    fixture.destroy();
  });
});
