import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AutenticacionServicio } from './autenticacion.servicio';

export const sesionGuard: CanActivateFn = async () => {
    const acceso = inject(AutenticacionServicio);
    const router = inject(Router);
    await acceso.inicializar();
    return acceso.sesion() ? true : router.createUrlTree(['/login']);
};
