import { TestBed } from '@angular/core/testing';
import { configuracionInicial, ConfiguracionSalaComponent, validarConfiguracion } from './configuracion-sala.component';
import { PiezasEspecialesComponent } from './piezas-especiales.component';
import { ITipoPiezaEspecial } from '../modelos/tetris.model';

const catalogo: ITipoPiezaEspecial[] = ['COL', 'FILA', 'CONFUSION', 'ACELERADOR', 'BOMBA', 'BARREDORA', 'DOBLE'].map(
  (codigo, indice) => ({
    codigo,
    tipoPiezaId: indice + 1,
    nombre: codigo,
    eliminaColumna: codigo === 'COL',
    eliminaFila: codigo === 'FILA',
  }),
);

describe('Configuración táctil de sala', () => {
  it('ofrece solo 2, 3 y 4 participantes y oculta especiales hasta activarlas', async () => {
    const fixture = TestBed.createComponent(ConfiguracionSalaComponent);
    const datos = configuracionInicial();
    fixture.componentRef.setInput('datos', datos);
    fixture.componentRef.setInput('catalogo', catalogo);
    fixture.detectChanges();
    const elemento: HTMLElement = fixture.nativeElement;
    expect([...elemento.querySelectorAll('select')[0].options].map((opcion) => opcion.text)).toEqual([
      '2 jugadores',
      '3 jugadores',
      '4 jugadores',
    ]);
    expect(elemento.querySelectorAll('.tarjeta-especial')).toHaveLength(0);
    (elemento.querySelector('[role=switch]') as HTMLInputElement).click();
    fixture.detectChanges();
    expect(elemento.querySelectorAll('.tarjeta-especial')).toHaveLength(7);
    const casillas = elemento.querySelectorAll<HTMLInputElement>('.tarjeta-especial input');
    casillas[5].click();
    fixture.detectChanges();
    expect(datos.piezasEspeciales?.tiposPermitidos).not.toContain('BARREDORA');
    expect(datos.piezasEspeciales?.tiposPermitidos).toHaveLength(6);
    casillas[5].click();
    fixture.detectChanges();
    expect(datos.piezasEspeciales?.tiposPermitidos).toHaveLength(7);
    expect(elemento.textContent).toContain('Elimina la mitad superior del tablero');
    expect(elemento.querySelector('select[multiple]')).toBeNull();
  });

  it('los invitados ven la selección sincronizada sin controles editables', () => {
    const fixture = TestBed.createComponent(PiezasEspecialesComponent);
    fixture.componentRef.setInput('catalogo', catalogo);
    fixture.componentRef.setInput('editable', false);
    fixture.componentRef.setInput('configuracion', { activas: true, probabilidad: 50, tiposPermitidos: ['BOMBA'] });
    fixture.detectChanges();
    const elemento: HTMLElement = fixture.nativeElement;
    expect(elemento.querySelectorAll('.tarjeta-especial')).toHaveLength(1);
    expect(elemento.querySelector('input')).toBeNull();
    expect(elemento.textContent).toContain('Bomba');
    fixture.componentRef.setInput('configuracion', {
      activas: true,
      probabilidad: 100,
      tiposPermitidos: ['FILA', 'CONFUSION'],
    });
    fixture.detectChanges();
    expect(elemento.querySelectorAll('.tarjeta-especial')).toHaveLength(2);
    expect(elemento.textContent).toContain('Invierte izquierda y derecha');
    expect(elemento.textContent).not.toContain('Bomba');
  });

  it('en competitivo no ofrece especiales y valida los mismos límites del backend', () => {
    const fixture = TestBed.createComponent(ConfiguracionSalaComponent);
    const datos = configuracionInicial();
    datos.modo = 'Competitivo';
    datos.piezasEspeciales!.activas = true;
    fixture.componentRef.setInput('datos', datos);
    fixture.componentRef.setInput('catalogo', catalogo);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=switch]')).toBeNull();

    for (const cantidad of [2, 3, 4]) {
      datos.maximoJugadores = cantidad;
      expect(validarConfiguracion(datos)).toBeNull();
    }

    for (const cantidad of [0, 1, 5, 16]) {
      datos.maximoJugadores = cantidad;
      expect(validarConfiguracion(datos)).not.toBeNull();
    }

    datos.maximoJugadores = 2;
    datos.modo = 'Compartido';
    datos.piezasEspeciales!.tiposPermitidos = [];
    expect(validarConfiguracion(datos)).toContain('al menos una pieza especial');
    datos.piezasEspeciales!.tiposPermitidos = ['COL'];

    for (const probabilidad of [1, 100]) {
      datos.piezasEspeciales!.probabilidad = probabilidad;
      expect(validarConfiguracion(datos)).toBeNull();
    }

    for (const probabilidad of [0, 101, 1.5]) {
      datos.piezasEspeciales!.probabilidad = probabilidad;
      expect(validarConfiguracion(datos)).toContain('probabilidad');
    }
  });
});
