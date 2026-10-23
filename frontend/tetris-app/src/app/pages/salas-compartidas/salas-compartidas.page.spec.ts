import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SalasCompartidasPage } from './salas-compartidas.page';
import { provideRouter } from '@angular/router';

describe('SalasCompartidasPage', () => {
  let component: SalasCompartidasPage;
  let fixture: ComponentFixture<SalasCompartidasPage>;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    fixture = TestBed.createComponent(SalasCompartidasPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
