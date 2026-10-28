const fs = require('node:fs');
const path = require('node:path');
const raiz = path.join(__dirname, '..');
const carpeta = path.join(raiz, 'www');
const exigir = (condicion, mensaje) => { if (!condicion) { throw new Error(mensaje); } };
try {
    const firebase = JSON.parse(fs.readFileSync(path.join(raiz, 'firebase.json'), 'utf8'));
    exigir(firebase.hosting.public === 'www', 'Hosting debe publicar www.');
    exigir(firebase.hosting.headers.some(regla => regla.regex === '^[/\\\\][^.]*$' &&
        regla.headers.some(cabecera => cabecera.key === 'Cache-Control' && cabecera.value === 'no-store')),
        'La raíz y las rutas Angular deben enviar HTML sin caché.');
    exigir(firebase.hosting.rewrites.some(r => r.source === '**' && r.destination === '/index.html'), 'Falta la ruta de Angular.');
    const reglaPrivacidad = firebase.hosting.rewrites.findIndex(r =>
        r.regex === "^[/\\\\]privacidad([/\\\\].*)?$" && r.destination === '/privacidad/index.html');
    const reglaAngular = firebase.hosting.rewrites.findIndex(r => r.source === '**');
    exigir(reglaPrivacidad >= 0 && reglaPrivacidad < reglaAngular, 'Privacidad debe resolverse antes del fallback de Angular.');
    exigir(firebase.hosting.headers.some(r => r.regex === "^[/\\\\]privacidad([/\\\\].*)?$" &&
        r.headers.some(h => h.key === 'Cache-Control' && h.value === 'no-store')),
        'Privacidad debe enviarse sin caché.');
    const documentoPrivacidad = fs.readFileSync(path.join(carpeta, 'privacidad/index.html'), 'utf8');
    exigir(documentoPrivacidad.includes('id="titulo-privacidad"') && !documentoPrivacidad.includes('<app-root>'),
        'El archivo público debe contener la política, no el index del juego.');
    const datos = JSON.parse(fs.readFileSync(path.join(carpeta, 'assets/configuracion-publica.json'), 'utf8'));
    exigir(firebase.hosting.headers.some(regla => regla.regex === "^[/\\\\]assets[/\\\\]configuracion-publica\\.json$" &&
        regla.headers.some(cabecera => cabecera.key === 'Access-Control-Allow-Origin' && cabecera.value === '*')),
        'La configuracion publica debe poder consultarse desde Capacitor sin credenciales.');
    exigir(Object.keys(datos).length === 1 && typeof datos.urlBackend === 'string', 'Configuración pública inesperada.');
    const url = new URL(datos.urlBackend);
    exigir(/^[a-z0-9]+(?:-[a-z0-9]+)*\.trycloudflare\.com$/.test(url.hostname), 'El backend no pertenece a la lista permitida.');
    exigir(url.protocol === 'https:' && !url.username && !url.password && !url.port &&
        url.pathname === '/' && !url.search && !url.hash && url.hostname.includes('.') &&
        !/^[\d.]+$/.test(url.hostname) && !url.hostname.includes(':') &&
        !/(^|\.)(localhost|local|internal)$/.test(url.hostname), 'El backend debe usar un origen HTTPS público.');
    const indice = fs.readFileSync(path.join(carpeta, 'index.html'), 'utf8');
    exigir(/main-[\w-]+\.js/.test(indice) && !fs.readdirSync(carpeta).some(n => n.endsWith('.map')), 'Generá www con npm run build (producción).');
    for (const nombre of ['manifest.webmanifest', 'sw.js', 'assets/icon/tetris-192.png', 'assets/icon/tetris-512.png', 'privacidad/index.html', 'privacidad/privacidad.css', 'assets/icon/tetris-logo-v2-192.png', 'assets/icon/tetris-logo-v2-512.png', 'assets/icon/tetris-logo-v2-180.png', 'assets/icon/tetris-maskable-v2-512.png', 'assets/icon/tetris-favicon-v2.ico']) {
        exigir(fs.existsSync(path.join(carpeta, nombre)), 'Falta recurso: ' + nombre);
    }
    const audios = path.join(raiz, 'src/assets/audio');
    const revisar = (actual, relativa = '') => {
        for (const archivo of fs.readdirSync(actual, { withFileTypes: true })) {
            const ruta = path.join(relativa, archivo.name);
            if (archivo.isDirectory()) { revisar(path.join(actual, archivo.name), ruta); }
            else { exigir(fs.existsSync(path.join(carpeta, 'assets/audio', ruta)), 'Falta audio: ' + ruta); }
        }
    };
    revisar(audios);
    console.log('Publicación validada: producción, configuración HTTPS, rutas, iconos y audio. No se accedió a cuentas externas.');
} catch (error) {
    console.error('No se puede publicar: ' + error.message);
    process.exitCode = 1;
}
