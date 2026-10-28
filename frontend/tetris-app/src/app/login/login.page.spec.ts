import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LoginPage } from './login.page';
import { provideRouter } from '@angular/router';

describe('LoginPage', () => {
  let component: LoginPage;
  let fixture: ComponentFixture<LoginPage>;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    fixture = TestBed.createComponent(LoginPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('presenta el juego y permite abrir la política sin ingresar', () => {
    const elemento = fixture.nativeElement as HTMLElement;
    const enlace = elemento.querySelector<HTMLAnchorElement>('a[href="privacidad/index.html"]');
    expect(enlace?.textContent).toContain('Política de privacidad');
    expect(enlace?.closest('form')).toBeNull();
    expect(elemento.querySelector('.presentacion')?.textContent).toContain('completar líneas');
    expect(elemento.querySelector('.presentacion')?.textContent).toContain('Google');
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('muestra una sola explicación exacta y no atribuye un vencimiento a otro dispositivo', () => {
    component.acceso.motivoCierre.set('SesionReemplazada');
    component.acceso.mensajeSesion.set('Alguien más inició sesión con tu cuenta en otro dispositivo. Por seguridad, cerramos esta sesión.');
    fixture.detectChanges();
    const elemento = fixture.nativeElement as HTMLElement;
    expect(elemento.querySelectorAll('.aviso-sesion')).toHaveLength(1);
    expect(elemento.querySelector('.aviso-sesion')?.textContent).toContain('Sesión cerrada');
    expect(elemento.querySelector('.aviso-sesion')?.textContent).toContain('Si no fuiste vos, te recomendamos recuperar tu cuenta.');
    component.acceso.motivoCierre.set('SesionVencida');
    component.acceso.mensajeSesion.set('Tu sesión venció. Iniciá sesión nuevamente para continuar.');
    fixture.detectChanges();
    expect(elemento.querySelector('.aviso-sesion')?.textContent).not.toContain('otro dispositivo');
    expect(elemento.querySelector('.aviso-sesion')?.textContent).not.toContain('Si no fuiste vos');
  });
});
