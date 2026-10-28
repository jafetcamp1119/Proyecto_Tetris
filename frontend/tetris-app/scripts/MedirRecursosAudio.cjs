// Decodifica los MP3 reales en un navegador aislado; no reproduce sonido ni usa cuentas.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const http = require('node:http');
const ts = require('typescript');
const { spawn, spawnSync } = require('node:child_process');
const esperar = ms => new Promise(resolve => setTimeout(resolve, ms));

async function medirNavegador(direccion, temporal) {
    const proceso = spawn(navegador, ['--headless=new', '--disable-gpu', '--no-first-run', '--disable-background-networking',
        '--disable-extensions', '--disable-sync', '--user-data-dir=' + temporal, '--remote-debugging-port=0', direccion],
        { windowsHide: true, stdio: 'ignore' });
    let fallo;
    proceso.on('error', error => { fallo = error; });
    let socket;
    try {
        const archivoPuerto = path.join(temporal, 'DevToolsActivePort');
        for (let intento = 0; intento < 100 && !fs.existsSync(archivoPuerto); intento++) {
            if (fallo || proceso.exitCode !== null) { throw fallo || new Error('El navegador se cerró antes del diagnóstico.'); }
            await esperar(100);
        }
        const puerto = Number(fs.readFileSync(archivoPuerto, 'utf8').split('\n')[0]);
        const paginas = await (await fetch('http://127.0.0.1:' + puerto + '/json/list')).json();
        const pagina = paginas.find(p => p.type === 'page' && p.url.startsWith(direccion));
        if (!pagina) { throw new Error('No se encontró la página local de diagnóstico.'); }
        socket = new WebSocket(pagina.webSocketDebuggerUrl);
        await Promise.race([
            new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', reject, { once: true }); }),
            esperar(3000).then(() => { throw new Error('No se pudo conectar al navegador de diagnóstico.'); })
        ]);
        const respuestas = new Map();
        socket.addEventListener('message', evento => {
            const mensaje = JSON.parse(evento.data);
            if (respuestas.has(mensaje.id)) { respuestas.get(mensaje.id)(mensaje); respuestas.delete(mensaje.id); }
        });
        for (let intento = 1; intento <= 150; intento++) {
            const respuesta = new Promise(resolve => { respuestas.set(intento, resolve); });
            socket.send(JSON.stringify({ id: intento, method: 'Runtime.evaluate', params: {
                expression: "document.getElementById('resultado')?.textContent", returnByValue: true
            } }));
            const mensaje = await Promise.race([respuesta, esperar(3000).then(() => { throw new Error('El navegador no respondió.'); })]);
            const texto = mensaje.result?.result?.value;
            if (texto?.startsWith('[') || texto?.startsWith('{')) { return JSON.parse(texto); }
            await esperar(100);
        }
        throw new Error('La decodificación no terminó dentro del plazo.');
    } finally {
        if (socket?.readyState === WebSocket.OPEN) { socket.send(JSON.stringify({ id: 1000, method: 'Browser.close' })); socket.close(); }
        for (let intento = 0; intento < 20 && proceso.exitCode === null; intento++) { await esperar(100); }
        if (proceso.exitCode === null && proceso.pid) { spawnSync('taskkill.exe', ['/PID', String(proceso.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' }); }
    }
}

const carpeta = path.resolve(__dirname, '../src/assets/audio/efectos');
const archivos = fs.readdirSync(carpeta).filter(n => n.endsWith('.mp3'));
const prepararAudio = ts.transpileModule(fs.readFileSync(path.resolve(__dirname, '../src/app/servicios/comienzo-audio.ts'), 'utf8'), {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext }
}).outputText;
const navegadores = [process.env.TETRIS_NAVEGADOR_PRUEBA,
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Google/Chrome/Application/chrome.exe'];
const navegador = navegadores.find(n => n && fs.existsSync(n));
if (!navegador) { throw new Error('Indicá TETRIS_NAVEGADOR_PRUEBA con la ruta de Edge o Chrome.'); }

const pagina = `<!doctype html><html lang="es"><body><pre id="resultado">Esperando decodificación</pre><script type="module">
${prepararAudio}
(async () => {
    const contexto = new AudioContext();
    const resultados = [];
    for (const archivo of ${JSON.stringify(archivos)}) {
        const respuesta = await fetch('/efectos/' + encodeURIComponent(archivo));
        const buffer = await contexto.decodeAudioData(await respuesta.arrayBuffer());
        const canales = Array.from({length: buffer.numberOfChannels}, (_, c) => buffer.getChannelData(c));
        let inicio = 0;
        while (inicio < buffer.length && !canales.some(c => Math.abs(c[inicio]) > 0.001)) { inicio++; }
        resultados.push({archivo, duracionMs: Math.round(buffer.duration * 1000), silencioInicialMs: +(inicio * 1000 / buffer.sampleRate).toFixed(2),
            silencioTrasPrepararMs: +(inicio * 1000 / buffer.sampleRate - buscarComienzoAudio(buffer) * 1000).toFixed(2)});
    }
    await contexto.close();
    document.getElementById('resultado').textContent = JSON.stringify(resultados);
})().catch(error => { document.getElementById('resultado').textContent = JSON.stringify({error: error.name}); });
</script></body></html>`;

async function ejecutar() {
    const temporal = fs.mkdtempSync(path.join(os.tmpdir(), 'tetris-audio-'));
    const servidor = http.createServer((solicitud, respuesta) => {
        const ruta = new URL(solicitud.url, 'http://localhost').pathname;
        if (ruta === '/') { respuesta.setHeader('Content-Type', 'text/html'); respuesta.end(pagina); return; }
        const nombre = decodeURIComponent(ruta.slice('/efectos/'.length));
        if (!ruta.startsWith('/efectos/') || !archivos.includes(nombre)) { respuesta.writeHead(404); respuesta.end(); return; }
        respuesta.setHeader('Content-Type', 'audio/mpeg');
        fs.createReadStream(path.join(carpeta, nombre)).pipe(respuesta);
    });
    await new Promise(resolve => servidor.listen(0, '127.0.0.1', resolve));
    try {
        const direccion = 'http://127.0.0.1:' + servidor.address().port;
        const datos = await medirNavegador(direccion, temporal);
        if (!Array.isArray(datos) || datos.length !== archivos.length) { throw new Error('No se completó la decodificación.'); }
        console.log(JSON.stringify({ tipo: 'decodificacion_archivos_reales_sin_medicion_acustica', resultados: datos }, null, 2));
    } finally {
        servidor.closeAllConnections();
        await new Promise(resolve => servidor.close(resolve));
        const destino = fs.realpathSync(temporal);
        const raizTemporal = fs.realpathSync(os.tmpdir()) + path.sep;
        if (destino.startsWith(raizTemporal) && path.basename(destino).startsWith('tetris-audio-')) {
            try { fs.rmSync(destino, { recursive: true, force: true }); } catch { /* El navegador puede liberar su perfil con demora. */ }
        }
    }
}
ejecutar().catch(error => { console.error(error.message); process.exitCode = 1; });
