import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { Capacitor } from '@capacitor/core';
import { vi } from 'vitest';
import { OrientacionServicio } from './orientacion.servicio';

function crearEntorno(agente = 'iPhone', contactos = 5) {
  const pantalla = Object.assign(new EventTarget(), { type: 'portrait-primary' });
  const consulta = Object.assign(new EventTarget(), { matches: false });
  const ventana = Object.assign(new EventTarget(), {
    navigator: { userAgent: agente, maxTouchPoints: contactos },
    screen: { orientation: pantalla as typeof pantalla | undefined },
    matchMedia: () => consulta,
  });
  TestBed.configureTestingModule({ providers: [{ provide: DOCUMENT, useValue: { defaultView: ventana } }] });
  return { ventana, pantalla, consulta };
}

describe('Orientación móvil compartida', () => {
  beforeEach(() => {
    vi.spyOn(Capacitor, 'isNativePlatform').mockReturnValue(false);
  });
  afterEach(() => vi.restoreAllMocks());

  it('activa y retira el aviso al girar un iPhone sin recrear el servicio', () => {
    const entorno = crearEntorno();
    const servicio = TestBed.inject(OrientacionServicio);
    expect(servicio.requiereVertical()).toBe(false);
    entorno.pantalla.type = 'landscape-primary';
    entorno.pantalla.dispatchEvent(new Event('change'));
    expect(servicio.requiereVertical()).toBe(true);
    entorno.pantalla.type = 'portrait-secondary';
    entorno.pantalla.dispatchEvent(new Event('change'));
    expect(servicio.requiereVertical()).toBe(false);
  });

  it('no restringe un navegador de computadora, incluso con pantalla táctil', () => {
    const entorno = crearEntorno('Windows NT 10.0', 5);
    entorno.pantalla.type = 'landscape-primary';
    expect(TestBed.inject(OrientacionServicio).requiereVertical()).toBe(false);
  });

  it('reconoce iPadOS con agente de escritorio', () => {
    const entorno = crearEntorno('Macintosh; Intel Mac OS X', 5);
    entorno.pantalla.type = 'landscape-primary';
    expect(TestBed.inject(OrientacionServicio).requiereVertical()).toBe(true);
  });

  it('usa el ángulo de Safari y no confunde el teclado abierto con una rotación', () => {
    const entorno = crearEntorno();
    entorno.ventana.screen.orientation = undefined;
    Object.assign(entorno.ventana, { orientation: 0 });
    entorno.consulta.matches = true;
    const servicio = TestBed.inject(OrientacionServicio);
    expect(servicio.requiereVertical()).toBe(false);
    Object.assign(entorno.ventana, { orientation: -90 });
    entorno.ventana.dispatchEvent(new Event('orientationchange'));
    expect(servicio.requiereVertical()).toBe(true);
  });

  it('usa media query como último respaldo y elimina sus escuchas al destruirse', () => {
    const entorno = crearEntorno('Android');
    entorno.ventana.screen.orientation = undefined;
    const servicio = TestBed.inject(OrientacionServicio);
    entorno.consulta.matches = true;
    entorno.consulta.dispatchEvent(new Event('change'));
    expect(servicio.requiereVertical()).toBe(true);
    entorno.consulta.matches = false;
    entorno.consulta.dispatchEvent(new Event('change'));
    expect(servicio.requiereVertical()).toBe(false);
    servicio.ngOnDestroy();
    entorno.consulta.matches = true;
    entorno.consulta.dispatchEvent(new Event('change'));
    expect(servicio.requiereVertical()).toBe(false);
  });

  it('aplica el respaldo en Capacitor aunque el agente no identifique un teléfono', () => {
    vi.mocked(Capacitor.isNativePlatform).mockReturnValue(true);
    const entorno = crearEntorno('WebView');
    entorno.pantalla.type = 'landscape-primary';
    expect(TestBed.inject(OrientacionServicio).requiereVertical()).toBe(true);
  });
});
