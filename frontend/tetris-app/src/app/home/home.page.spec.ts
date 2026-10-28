import { ComponentFixture, TestBed } from '@angular/core/testing';

import { HomePage } from './home.page';
import { provideRouter } from '@angular/router';
import { AutenticacionServicio } from '../servicios/autenticacion.servicio';
import { IAutenticacion } from '../modelos/tetris.model';

describe('HomePage', () => {
  let component: HomePage;
  let fixture: ComponentFixture<HomePage>;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    fixture = TestBed.createComponent(HomePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('muestra un nickname con HTML como texto y nunca crea el contenido inyectado', async () => {
    const acceso = TestBed.inject(AutenticacionServicio);
    const nombreUsuario = '<img src=x onerror=alert(1)>';
    acceso.sesion.set({ usuarioId: 1, nombreUsuario } as IAutenticacion);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(nombreUsuario);
    expect(fixture.nativeElement.querySelector('img[onerror]')).toBeNull();
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
  });
});
