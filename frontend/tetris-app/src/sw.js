// El juego requiere Internet. No almacenar API, credenciales ni respuestas de Google.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
