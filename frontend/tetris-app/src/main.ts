import { Capacitor } from '@capacitor/core';
import { environment } from './environments/environment';
import { cargarConexionPublicada } from './environments/conexion-publicada';
import { bootstrapApplication } from '@angular/platform-browser';
import { RouteReuseStrategy, provideRouter, withComponentInputBinding, withPreloading, PreloadAllModules } from '@angular/router';
import { IonicRouteStrategy, provideIonicAngular } from '@ionic/angular';

import { routes } from './app/app.routes';
import { AppComponent } from './app/app.component';

async function iniciar(): Promise<void> {
  const privacidad = /^\/privacidad\/?$/.test(location.pathname);
  if (environment.production && !privacidad) {
    const url = await cargarConexionPublicada(Capacitor.isNativePlatform());
    environment.red.urlNavegador = url;
    environment.red.urlAndroid = url;
  }
  await bootstrapApplication(AppComponent, {
    providers: [
      { provide: RouteReuseStrategy, useClass: IonicRouteStrategy },
      provideIonicAngular(),
      provideRouter(routes, withPreloading(PreloadAllModules), withComponentInputBinding()),
    ],
  });
}

function mostrarErrorInicio(): void {
  const titulo = document.createElement('h1');
  titulo.textContent = 'Proyecto Tetris';
  const descripcion = document.createElement('p');
  descripcion.textContent = 'Videojuego de piezas descendentes en modos Individual, Compartido y Competitivo.';
  const mensaje = document.createElement('p');
  mensaje.textContent = 'Servidor no disponible o configuración no válida. Revisá tu conexión. La computadora que ejecuta el juego debe estar encendida.';
  mensaje.className = 'mensaje-error';
  const reintentar = document.createElement('button');
  reintentar.textContent = 'Reintentar conexión';
  reintentar.className = 'accion';
  reintentar.onclick = () => {
    reintentar.disabled = true;
    void comenzar();
  };
  const privacidad = document.createElement('a');
  privacidad.href = '/privacidad';
  privacidad.textContent = 'Política de privacidad';
  document.querySelector('app-root')?.replaceChildren(titulo, descripcion, mensaje, reintentar, privacidad);
}

async function comenzar(): Promise<void> {
  try {
    await iniciar();
    if (environment.production && !Capacitor.isNativePlatform() && 'serviceWorker' in navigator) {
      void navigator.serviceWorker.register('/sw.js').catch(() => undefined);
    }
  } catch { mostrarErrorInicio(); }
}

void comenzar();
