import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CodigoSalaComponent } from './codigo-sala.component';

describe('Código de sala visible y compartible', () => {
    const portapapelesOriginal = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
    afterEach(() => {
        if (portapapelesOriginal) { Object.defineProperty(navigator, 'clipboard', portapapelesOriginal); }
        else { Reflect.deleteProperty(navigator, 'clipboard'); }
    });

    it('muestra y copia exactamente el código recibido del servidor', async () => {
        const copiar = vi.fn().mockResolvedValue(undefined);
        Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: copiar } });
        const fixture = TestBed.createComponent(CodigoSalaComponent);
        fixture.componentRef.setInput('codigo', 'K7PM42');
        fixture.detectChanges();
        expect(fixture.nativeElement.querySelector('code').textContent).toBe('K7PM42');
        fixture.nativeElement.querySelector('button').click();
        await fixture.whenStable();
        expect(copiar).toHaveBeenCalledExactlyOnceWith('K7PM42');
        expect(fixture.componentInstance.mensaje()).toBe('Código copiado.');
    });

    it('conserva el código visible y explica la alternativa si el portapapeles falla', async () => {
        Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: vi.fn().mockRejectedValue(new Error('No permitido')) } });
        const fixture = TestBed.createComponent(CodigoSalaComponent);
        fixture.componentRef.setInput('codigo', 'K7PM42');
        await fixture.componentInstance.copiar();
        fixture.detectChanges();
        expect(fixture.nativeElement.querySelector('code').textContent).toBe('K7PM42');
        expect(fixture.componentInstance.mensaje()).toContain('Podés escribir');
    });
});
