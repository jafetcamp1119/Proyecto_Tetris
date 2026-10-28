// Ejecutar mientras Firebase Hosting Emulator esté activo. No usa cuentas ni el backend.
const assert = require('node:assert/strict');
const { JSDOM } = require('jsdom');
const origen = process.env.TETRIS_HOSTING_PRUEBA || 'http://127.0.0.1:5000';
if (!/^http:\/\/(127\.0\.0\.1|localhost):\d+$/.test(origen)) {
    throw new Error('Esta comprobación solo admite un emulador local.');
}
(async () => {
    for (const ruta of ['/privacidad', '/privacidad/', '/privacidad/index.html', '/privacidad?actualizar=1']) {
        const respuesta = await fetch(origen + ruta);
        assert.equal(respuesta.status, 200, ruta + ' debe ser pública sin Authorization ni cookies');
        assert.match(respuesta.headers.get('content-type'), /text\/html/);
        assert.equal(respuesta.headers.get('cache-control'), 'no-store', 'Privacidad no debe reutilizar una respuesta HTML antigua');
        const pagina = new JSDOM(await respuesta.text()).window.document;
        assert.equal(pagina.documentElement.lang, 'es');
        assert.match(pagina.querySelector('h1')?.textContent || '', /Política de privacidad/);
        assert.equal(pagina.querySelectorAll('script, app-root, form, iframe').length, 0,
            'La política debe poder leerse sin JavaScript, sesión o llamadas al backend');
        assert.equal(pagina.querySelector('a[href^="mailto:"]')?.getAttribute('href'), 'mailto:jafetcamp1119@gmail.com');
        assert.ok(pagina.querySelector('#conservacion'));
        assert.ok(pagina.querySelector('#pendientes'));
        console.log('OK Hosting: ' + ruta + ' sirve la política completa sin iniciar sesión.');
    }
    const css = await fetch(origen + '/privacidad/privacidad.css');
    const configuracion = await fetch(origen + '/assets/configuracion-publica.json', { headers: { Origin: 'https://localhost' } });
    assert.equal(configuracion.status, 200);
    assert.equal(configuracion.headers.get('access-control-allow-origin'), '*', 'La configuracion publica debe poder leerse desde Capacitor');
    assert.equal(configuracion.headers.get('cache-control'), 'no-store');
    assert.deepEqual(Object.keys(await configuracion.json()), ['urlBackend']);
    assert.equal(css.status, 200);
    assert.match(css.headers.get('content-type'), /text\/css/);
    const worker = await fetch(origen + '/sw.js');
    assert.equal(worker.status, 200);
    assert.equal(worker.headers.get('cache-control'), 'no-store');
    const codigoWorker = await worker.text();
    assert.ok(!/respondWith|caches\.open|addEventListener\(['"]fetch/.test(codigoWorker),
        'El service worker no debe sustituir privacidad por el shell del juego');
    const principal = await fetch(origen + '/');
    assert.equal(principal.status, 200);
    assert.equal(principal.headers.get('cache-control'), 'no-store', 'La URL inicial tampoco debe reutilizar HTML antiguo');
    assert.match(await principal.text(), /<app-root>/);
    const indice = await fetch(origen + '/index.html');
    assert.equal(indice.headers.get('cache-control'), 'no-store');
    const angular = await fetch(origen + '/login');
    assert.equal(angular.status, 200);
    assert.match(await angular.text(), /<app-root>/, 'Se conserva el fallback de Angular');
    for (const ruta of ['/login', '/home', '/salas/crear', '/partida/sala-de-prueba']) {
        const respuesta = await fetch(origen + ruta);
        assert.equal(respuesta.status, 200);
        assert.equal(respuesta.headers.get('cache-control'), 'no-store', ruta + ': evitar versiones antiguas por ruta de entrada');
        assert.match(await respuesta.text(), /<app-root>/);
    }
    console.log('OK Hosting: estilos de privacidad, página principal y ruta Angular.');
})().catch(error => { console.error(error); process.exitCode = 1; });
