import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { vi } from 'vitest';
import { routes } from '../../app.routes';
import { AutenticacionServicio } from '../../servicios/autenticacion.servicio';
import { PrivacidadPage } from './privacidad.page';

describe('Ruta pública de privacidad', () => {
    it('abre el documento estático sin consultar sesión, API ni Google', () => {
        const reemplazar = vi.fn();
        TestBed.configureTestingModule({
            providers: [{ provide: DOCUMENT, useValue: {
                baseURI: 'https://proyecto-tetris-512517.web.app/',
                defaultView: { location: { replace: reemplazar } }
            } }]
        });
        const pagina = TestBed.runInInjectionContext(() => new PrivacidadPage());
        pagina.ngOnInit();
        expect(reemplazar).toHaveBeenCalledWith('https://proyecto-tetris-512517.web.app/privacidad/index.html');
    });

    it('reconoce /privacidad antes del comodín sin ejecutar el guard de sesión', async () => {
        const inicializar = vi.fn(() => { throw new Error('No se debe consultar la sesión'); });
        const redireccion = vi.spyOn(PrivacidadPage.prototype, 'ngOnInit').mockImplementation(() => undefined);
        try {
            TestBed.configureTestingModule({ providers: [
                provideRouter(routes),
                { provide: AutenticacionServicio, useValue: { inicializar } }
            ] });
            const navegador = await RouterTestingHarness.create();
            await navegador.navigateByUrl('/privacidad', PrivacidadPage);
            expect(navegador.routeNativeElement?.textContent).toContain('Política de privacidad');
            expect(inicializar).not.toHaveBeenCalled();
            expect(redireccion).toHaveBeenCalled();
        } finally {
            redireccion.mockRestore();
        }
    });
});
