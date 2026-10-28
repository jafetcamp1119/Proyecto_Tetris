// Simula respuestas de salud HTTP para probar el iniciador sin usar la API real.
const http = require('node:http');
const origen = 'https://proyecto-tetris-512517.web.app';

function responderJson(respuesta, estado, datos) {
  respuesta.writeHead(estado, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  respuesta.end(JSON.stringify(datos));
}

const servidor = http.createServer((solicitud, respuesta) => {
  const ruta = new URL(solicitud.url, 'http://localhost').pathname;
  if (solicitud.headers.origin === origen && ruta !== '/sin-cors') {
    respuesta.setHeader('Access-Control-Allow-Origin', origen);
  }
  if (ruta === '/salud' || ruta === '/sin-cors') {
    responderJson(respuesta, 200, { estado: 'Servidor Tetris activo' });
    return;
  }
  if (ruta === '/diagnostico') {
    responderJson(respuesta, 200, {
      origenRecibido: solicitud.headers.origin === origen,
      protocoloReenviado: solicitud.headers['x-forwarded-proto'] || null,
      servidorSimulado: true,
    });
    return;
  }
  if (ruta === '/no-json') {
    respuesta.writeHead(200, { 'Content-Type': 'text/html' });
    respuesta.end('<h1>Respuesta de prueba sin JSON</h1>');
    return;
  }
  if (ruta === '/redireccion') {
    respuesta.writeHead(302, { Location: '/salud' });
    respuesta.end();
    return;
  }
  responderJson(respuesta, ruta === '/no-disponible' ? 503 : 404, { estado: 'Prueba de error' });
});
servidor.listen(0, '127.0.0.1', () => {
  console.log('http://127.0.0.1:' + servidor.address().port);
});
