import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { vi } from 'vitest';
import { SalasPage } from './salas.page';
import { SalasCompartidasPage } from '../salas-compartidas/salas-compartidas.page';
import { AutenticacionServicio } from '../../servicios/autenticacion.servicio';
import { SalasServicio } from '../../servicios/salas.servicio';

describe('Entradas independientes a salas', () => {
    beforeEach(() => sessionStorage.clear());

    for (const accion of ['crear', 'publicas', 'privada']) {
        it(`muestra solamente la operación ${accion}`, async () => {
            const parametros = new BehaviorSubject(convertToParamMap({ accion }));
            const consultas = new BehaviorSubject(convertToParamMap({ modo: 'Compartido' }));
            const ruta = {
                paramMap: parametros, queryParamMap: consultas,
                snapshot: { paramMap: parametros.value, queryParamMap: consultas.value }
            };
            const listar = vi.fn().mockResolvedValue([]);
            const solicitar = vi.fn().mockResolvedValue([]);
            TestBed.configureTestingModule({ providers: [
                provideRouter([]),
                { provide: ActivatedRoute, useValue: ruta },
                { provide: SalasServicio, useValue: { listar } },
                { provide: AutenticacionServicio, useValue: { solicitar } }
            ] });
            const fixture = TestBed.createComponent(SalasPage);
            fixture.detectChanges();
            await fixture.whenStable();
            fixture.detectChanges();
            const elemento: HTMLElement = fixture.nativeElement;
            expect(!!elemento.querySelector('app-configuracion-sala')).toBe(accion === 'crear');
            expect(!!elemento.querySelector('#codigo-sala')).toBe(accion === 'privada');
            expect(elemento.textContent?.includes('No hay partidas disponibles. ¡Podés crear una!')).toBe(accion === 'publicas');
            expect(listar).toHaveBeenCalledTimes(accion === 'publicas' ? 1 : 0);
            expect(solicitar).toHaveBeenCalledTimes(accion === 'crear' ? 1 : 0);
            expect(elemento.textContent).not.toMatch(/localhost|5186|SQL Server|SignalR|Configurar servidor|JWT/);
            expect(elemento.querySelector('a[routerLink="/home"]')).not.toBeNull();

            // Angular reutiliza el componente al cambiar el parámetro de la ruta.
            ruta.snapshot.paramMap = convertToParamMap({ accion: 'privada' });
            parametros.next(ruta.snapshot.paramMap);
            await fixture.whenStable();
            fixture.detectChanges();
            expect(elemento.querySelector('#codigo-sala')).not.toBeNull();
            expect(elemento.querySelector('app-configuracion-sala')).toBeNull();
        });
    }

    it('cada botón navega a su operación conservando el modo competitivo', () => {
        TestBed.configureTestingModule({ providers: [
            provideRouter([]),
            { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({ modo: 'Competitivo' }) } } }
        ] });
        const pagina = TestBed.createComponent(SalasCompartidasPage).componentInstance;
        const navegar = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
        for (const [boton, accion] of [['crear', 'crear'], ['publica', 'publicas'], ['privada', 'privada']]) {
            pagina.seleccionarOpcion(boton);
            expect(navegar).toHaveBeenLastCalledWith(['/salas', accion], { queryParams: { modo: 'Competitivo' } });
        }
    });
});
