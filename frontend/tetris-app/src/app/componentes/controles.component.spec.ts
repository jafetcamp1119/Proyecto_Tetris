import { signal } from '@angular/core';
import { OrientacionServicio } from '../servicios/orientacion.servicio';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ControlesComponent } from './controles.component';

describe('Controles táctiles', () => {
  const avisoVertical = signal(false);
  beforeEach(() => {
    avisoVertical.set(false);
    TestBed.configureTestingModule({
      providers: [{ provide: OrientacionServicio, useValue: { requiereVertical: avisoVertical.asReadonly() } }],
    });
  });
  afterEach(() => vi.useRealTimers());

  it('no duplica un toque con el clic sintetizado y detiene la repetición al soltar', () => {
    vi.useFakeTimers();
    const componente = TestBed.createComponent(ControlesComponent);
    componente.componentRef.setInput('habilitado', true);
    componente.detectChanges();
    const movimientos: string[] = [];
    componente.componentInstance.movimiento.subscribe((movimiento) => movimientos.push(movimiento));
    const boton: HTMLButtonElement = componente.nativeElement.querySelector('button');
    boton.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true }));
    boton.dispatchEvent(new MouseEvent('click', { detail: 1, bubbles: true }));
    expect(movimientos).toEqual(['Izquierda']);
    vi.advanceTimersByTime(450);
    expect(movimientos.length).toBeGreaterThan(1);
    boton.dispatchEvent(new MouseEvent('pointerup', { bubbles: true }));
    const cantidad = movimientos.length;
    vi.advanceTimersByTime(500);
    expect(movimientos.length).toBe(cantidad);
    componente.destroy();
  });

  it('un cambio de turno deshabilita los botones y cancela una pulsación mantenida', () => {
    vi.useFakeTimers();
    const componente = TestBed.createComponent(ControlesComponent);
    componente.componentRef.setInput('habilitado', true);
    componente.detectChanges();
    const movimientos: string[] = [];
    componente.componentInstance.movimiento.subscribe((movimiento) => movimientos.push(movimiento));
    const boton: HTMLButtonElement = componente.nativeElement.querySelector('button');
    boton.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true }));
    componente.componentRef.setInput('habilitado', false);
    componente.detectChanges();
    vi.advanceTimersByTime(800);
    expect(boton.disabled).toBe(true);
    expect(movimientos).toEqual(['Izquierda']);
    componente.destroy();
  });

  it('cancela la pulsación al girar y exige un nuevo toque al volver a vertical', () => {
    vi.useFakeTimers();
    const fixture = TestBed.createComponent(ControlesComponent);
    fixture.componentRef.setInput('habilitado', true);
    fixture.detectChanges();
    const mover = vi.fn();
    fixture.componentInstance.movimiento.subscribe(mover);
    const boton: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    boton.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true }));
    vi.advanceTimersByTime(450);
    const cantidad = mover.mock.calls.length;
    avisoVertical.set(true);
    fixture.detectChanges();
    vi.advanceTimersByTime(600);
    expect(mover).toHaveBeenCalledTimes(cantidad);
    expect(boton.disabled).toBe(true);
    avisoVertical.set(false);
    fixture.detectChanges();
    vi.advanceTimersByTime(600);
    expect(mover).toHaveBeenCalledTimes(cantidad);
    expect(boton.disabled).toBe(false);
    boton.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true }));
    expect(mover).toHaveBeenCalledTimes(cantidad + 1);
    fixture.destroy();
  });
});
