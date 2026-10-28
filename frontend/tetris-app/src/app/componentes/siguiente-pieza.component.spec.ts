import { TestBed } from '@angular/core/testing';
import { SiguientePiezaComponent } from './siguiente-pieza.component';

describe('Siguiente pieza', () => {
    it('dibuja cuatro bloques en una cuadrícula de 16 celdas y actualiza la forma', () => {
        const fixture = TestBed.createComponent(SiguientePiezaComponent);
        fixture.componentRef.setInput('pieza', {
            tipoPieza: 0, piezaEspecial: null,
            celdas: [0, 1, 2, 3].map(columna => ({ fila: 1, columna }))
        });
        fixture.detectChanges();
        const elemento: HTMLElement = fixture.nativeElement;
        expect(elemento.querySelectorAll('.celda-siguiente')).toHaveLength(16);
        expect(elemento.querySelectorAll('.ocupada')).toHaveLength(4);
        expect([...elemento.querySelectorAll('.celda-siguiente')].map((celda, posicion) => celda.classList.contains('ocupada') ? posicion : -1).filter(posicion => posicion >= 0)).toEqual([4, 5, 6, 7]);
        expect(elemento.querySelector('[role=img]')?.getAttribute('aria-label')).toBe('Siguiente pieza I');
        fixture.componentRef.setInput('pieza', {
            tipoPieza: 1, piezaEspecial: 'BOMBA',
            celdas: [{ fila: 0, columna: 1 }, { fila: 0, columna: 2 }, { fila: 1, columna: 1 }, { fila: 1, columna: 2 }]
        });
        fixture.componentRef.setInput('nombreEspecial', 'Bomba');
        fixture.detectChanges();
        expect(elemento.querySelectorAll('.ocupada')).toHaveLength(4);
        expect(elemento.textContent).toContain('Bomba');
        expect(elemento.querySelector('[role=img]')?.getAttribute('aria-label')).toBe('Siguiente pieza O, Bomba');
        expect(elemento.querySelectorAll('.ocupada span')).toHaveLength(4);
    });

    it('no muestra piezas para espectadores ni inventa una pieza mientras espera', () => {
        const fixture = TestBed.createComponent(SiguientePiezaComponent);
        fixture.detectChanges();
        expect(fixture.nativeElement.textContent).toContain('Esperando tu próxima pieza');
        expect(fixture.nativeElement.querySelector('.cuadricula-siguiente')).toBeNull();
        fixture.componentRef.setInput('pieza', { tipoPieza: 0, piezaEspecial: null, celdas: [] });
        fixture.componentRef.setInput('espectador', true);
        fixture.detectChanges();
        expect(fixture.nativeElement.textContent).toContain('Estás como espectador');
        expect(fixture.nativeElement.querySelector('.cuadricula-siguiente')).toBeNull();
    });
});
