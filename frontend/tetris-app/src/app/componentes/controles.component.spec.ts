import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ControlesComponent } from './controles.component';

describe('Controles táctiles', () => {
    afterEach(() => vi.useRealTimers());

    it('no duplica un toque con el clic sintetizado y detiene la repetición al soltar', () => {
        vi.useFakeTimers();
        const componente = TestBed.createComponent(ControlesComponent);
        componente.componentRef.setInput('habilitado', true);
        componente.detectChanges();
        const movimientos: string[] = [];
        componente.componentInstance.movimiento.subscribe(movimiento => movimientos.push(movimiento));
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
        componente.componentInstance.movimiento.subscribe(movimiento => movimientos.push(movimiento));
        const boton: HTMLButtonElement = componente.nativeElement.querySelector('button');
        boton.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true }));
        componente.componentRef.setInput('habilitado', false);
        componente.detectChanges();
        vi.advanceTimersByTime(800);
        expect(boton.disabled).toBe(true);
        expect(movimientos).toEqual(['Izquierda']);
        componente.destroy();
    });
});
