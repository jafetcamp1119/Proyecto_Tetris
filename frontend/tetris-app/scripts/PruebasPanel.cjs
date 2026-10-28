const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { JSDOM } = require('jsdom');

async function probar(estado, mensaje, fecha = new Date().toISOString()) {
    const carpeta = path.join(__dirname, '../presentacion');
    const pagina = new JSDOM(fs.readFileSync(path.join(carpeta, 'index.html'), 'utf8'), { runScripts: 'outside-only', url: 'http://127.0.0.1:5009/' });
    pagina.window.fetch = async () => ({ ok: true, json: async () => ({ estado, mensaje, actualizadoUtc: fecha }) });
    pagina.window.AbortSignal.timeout = () => new pagina.window.AbortController().signal;
    pagina.window.setTimeout = () => 0;
    pagina.window.eval(fs.readFileSync(path.join(carpeta, 'panel.js'), 'utf8'));
    await new Promise(resolve => setImmediate(resolve));
    return pagina;
}

(async () => {
    const listo = await probar('listo', 'Comprobado');
    assert.equal(listo.window.document.querySelector('#qr').hidden, false);
    assert.equal(listo.window.document.querySelector('#abrir').href, 'https://proyecto-tetris-512517.web.app/');
    assert.equal(listo.window.document.querySelector('#abrir').getAttribute('aria-disabled'), 'false');
    listo.window.close();
    for (const estado of ['preparando','error']) {
        const pagina = await probar(estado, 'Sin disponibilidad confirmada');
        assert.equal(pagina.window.document.querySelector('#qr').hidden, true);
        const evento = new pagina.window.MouseEvent('click', { bubbles: true, cancelable: true });
        pagina.window.document.querySelector('#abrir').dispatchEvent(evento);
        assert.equal(evento.defaultPrevented, true);
        assert.equal(pagina.window.document.querySelectorAll('audio,video,iframe').length, 0);
        pagina.window.close();
    }
    const vencido = await probar('listo','Anterior',new Date(Date.now()-60000).toISOString());
    assert.equal(vencido.window.document.querySelector('#qr').hidden,true);
    assert.match(vencido.window.document.querySelector('#estado-texto').textContent,/NO DISPONIBLE/);
    vencido.window.close();
    console.log('OK panel: QR/boton solo con disponibilidad reciente; bloqueo al preparar, fallar o vencer estado; sin reproduccion automatica.');
})().catch(error => { console.error(error); process.exitCode=1; });
