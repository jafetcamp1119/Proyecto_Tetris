// Comprueba en el navegador la presentación del código de invitación de las salas.
// Comprueba el layout del componente real con CSS de producción, sin cuentas ni backend.
// Complementa las pruebas Angular de roles; no sustituye Safari en un iPhone físico.
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const { JSDOM } = require('jsdom');

const raiz = path.resolve(__dirname, '..');
const navegador = [
  process.env.TETRIS_NAVEGADOR_PRUEBA,
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
].find((ruta) => ruta && fs.existsSync(ruta));
assert.ok(navegador, 'Indicá TETRIS_NAVEGADOR_PRUEBA con la ruta de Edge o Chrome.');
const indice = fs.readFileSync(path.join(raiz, 'www/index.html'), 'utf8');
const archivoCss = indice.match(/href="(styles-[^"]+\.css)"/)[1];
const css = fs.readFileSync(path.join(raiz, 'www', archivoCss), 'utf8');
const componente = fs.readFileSync(path.join(raiz, 'src/app/componentes/codigo-sala.component.ts'), 'utf8');
const estilos = componente
  .match(/styles:\s*\['([^']+)'\]/)[1]
  .replace(':host', 'app-codigo-sala')
  .replace('section {', 'app-codigo-sala section {')
  .replace('code {', 'app-codigo-sala code {')
  .replace('p {', 'app-codigo-sala p {');
const contenido = componente
  .match(/template:\s*`([\s\S]*?)`/)[1]
  .replace('{{ codigo }}', 'CP8CQQ')
  .replace('{{ mensaje() }}', 'Código copiado.');
const escenarios = [];
for (const [ancho, alto] of [
  [320, 568],
  [390, 844],
  [430, 932],
  [1280, 800],
]) {
  for (const rol of ['Anfitrión', 'Invitado']) {
    for (const privada of [false, true]) {
      escenarios.push({ ancho, alto, rol, privada });
    }
  }
}
const documento = `<!doctype html><meta charset="utf-8"><pre id="resultado"></pre><script>
const escenarios = ${JSON.stringify(escenarios)};
const estilo = ${JSON.stringify(css + estilos)};
const componente = ${JSON.stringify(contenido)};
for (const caso of escenarios) {
    const marco = document.createElement('iframe');
    marco.width = caso.ancho; marco.height = caso.alto; marco.style.border = '0';
    marco.srcdoc = '<!doctype html><html><head><style>' + estilo + '</style></head><body>' +
        '<main class="pantalla-tetris pantalla-partida"><nav class="acciones"><button class="accion">Salir al menú</button></nav>' +
        '<h1>Compartido · Ronda 1</h1><app-codigo-sala>' + componente + '</app-codigo-sala>' +
        '<section class="tarjeta-tetris"><h2>Sala de espera</h2><p>' + caso.rol + ' · ' + (caso.privada ? 'Privada' : 'Pública') +
        '</p><button class="accion">Estoy listo</button></section></main></body></html>';
    document.body.append(marco);
}
window.addEventListener('load', () => {
    const resultados = [...document.querySelectorAll('iframe')].map((marco, i) => {
        const doc = marco.contentDocument;
        const codigo = doc.querySelector('app-codigo-sala code');
        const boton = doc.querySelector('app-codigo-sala button');
        const visible = elemento => {
            const caja = elemento.getBoundingClientRect();
            const estilo = marco.contentWindow.getComputedStyle(elemento);
            return caja.width > 0 && caja.height > 0 && caja.left >= 0 && caja.top >= 0 &&
                caja.right <= doc.documentElement.clientWidth && caja.bottom <= marco.height &&
                estilo.visibility !== 'hidden' && estilo.display !== 'none';
        };
        return { ...escenarios[i], codigoVisible: visible(codigo), copiarVisible: visible(boton),
            sinDesbordamiento: doc.documentElement.scrollWidth <= doc.documentElement.clientWidth };
    });
    document.getElementById('resultado').textContent = JSON.stringify(resultados);
});
</script>`;

const temporal = fs.mkdtempSync(path.join(os.tmpdir(), 'tetris-codigo-sala-'));
try {
  const pagina = path.join(temporal, 'prueba.html');
  fs.writeFileSync(pagina, documento);
  const proceso = spawnSync(
    navegador,
    [
      '--headless=new',
      '--disable-gpu',
      '--no-first-run',
      '--disable-background-networking',
      '--disable-extensions',
      '--disable-sync',
      '--user-data-dir=' + path.join(temporal, 'perfil'),
      '--dump-dom',
      pagina,
    ],
    { windowsHide: true, encoding: 'utf8', timeout: 20000, maxBuffer: 16 * 1024 * 1024 },
  );
  assert.ifError(proceso.error);
  assert.equal(proceso.status, 0, 'El navegador debe completar la prueba.');
  const resultados = JSON.parse(new JSDOM(proceso.stdout).window.document.getElementById('resultado').textContent);
  assert.equal(resultados.length, escenarios.length);
  for (const caso of resultados) {
    assert.ok(caso.codigoVisible && caso.copiarVisible && caso.sinDesbordamiento, JSON.stringify(caso));
  }
  console.log(
    'OK layout Edge/Chrome: 16 casos anfitrión/invitado, público/privado, 320/390/430/1280 px; código y copiar visibles sin desbordamiento. Sin backend ni audio.',
  );
} finally {
  const destino = path.resolve(temporal);
  assert.ok(
    destino.startsWith(path.resolve(os.tmpdir()) + path.sep) &&
      path.basename(destino).startsWith('tetris-codigo-sala-'),
  );
  fs.rmSync(destino, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
}
