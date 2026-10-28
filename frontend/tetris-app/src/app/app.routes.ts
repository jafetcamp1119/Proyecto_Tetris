// Define las pantallas y protege su ingreso con sesionGuard.
// Antes de salir de Partida consulta puedeSalir para confirmar el abandono.
import { Routes } from '@angular/router';
import { sesionGuard } from './servicios/sesion.guard';
import { PartidaPage } from './pages/partida/partida.page';

export const routes: Routes = [
  {
    path: 'privacidad',
    pathMatch: 'full',
    loadComponent: () => import('./pages/privacidad/privacidad.page').then((m) => m.PrivacidadPage),
  },
  {
    path: 'ajustes',
    canActivate: [sesionGuard],
    loadComponent: () => import('./pages/ajustes/ajustes.page').then((m) => m.AjustesPage),
  },
  {
    path: 'login',
    loadComponent: () => import('./login/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'home',
    canActivate: [sesionGuard],
    loadComponent: () => import('./home/home.page').then((m) => m.HomePage),
  },
  {
    path: 'perfil',
    canActivate: [sesionGuard],
    loadComponent: () => import('./pages/perfil/perfil.page').then((m) => m.PerfilPage),
  },
  {
    path: 'salas-compartidas',
    canActivate: [sesionGuard],
    loadComponent: () => import('./pages/salas-compartidas/salas-compartidas.page').then((m) => m.SalasCompartidasPage),
  },
  {
    path: 'salas/:accion',
    canActivate: [sesionGuard],
    loadComponent: () => import('./pages/salas/salas.page').then((m) => m.SalasPage),
  },
  { path: 'salas', redirectTo: 'salas/crear', pathMatch: 'full' },
  {
    path: 'partida/:id',
    canActivate: [sesionGuard],
    canDeactivate: [(pagina: PartidaPage) => pagina.puedeSalir()],
    loadComponent: () => import('./pages/partida/partida.page').then((m) => m.PartidaPage),
  },
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: '**', redirectTo: 'home' },
];
