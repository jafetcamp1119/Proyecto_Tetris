// Prepara el SVG del QR de presentación y comprueba su destino.
const fs = require('node:fs');
const path = require('node:path');
const QRCode = require('qrcode');
const jsQR = require('jsqr');

const direccionJuego = 'https://proyecto-tetris-512517.web.app';
const carpeta = path.join(__dirname, '../../presentacion');

async function prepararQr() {
  fs.mkdirSync(carpeta, { recursive: true });
  const opciones = { errorCorrectionLevel: 'M', margin: 4, width: 600 };
  await QRCode.toFile(path.join(carpeta, 'qr-tetris.svg'), direccionJuego, { ...opciones, type: 'svg' });
  const modelo = QRCode.create(direccionJuego, { errorCorrectionLevel: 'M' });
  const escala = 12;
  const ancho = (modelo.modules.size + 8) * escala;
  const pixeles = new Uint8ClampedArray(ancho * ancho * 4);
  for (let fila = 0; fila < ancho; fila++) {
    for (let columna = 0; columna < ancho; columna++) {
      const x = Math.floor(columna / escala) - 4;
      const y = Math.floor(fila / escala) - 4;
      const dentro = x >= 0 && y >= 0 && x < modelo.modules.size && y < modelo.modules.size;
      const negro = dentro && modelo.modules.get(y, x);
      const indice = (fila * ancho + columna) * 4;
      pixeles[indice] = pixeles[indice + 1] = pixeles[indice + 2] = negro ? 0 : 255;
      pixeles[indice + 3] = 255;
    }
  }
  const lectura = jsQR(pixeles, ancho, ancho);
  if (!lectura || lectura.data !== direccionJuego) {
    throw new Error('El QR no pudo leerse con la dirección correcta.');
  }
  console.log('QR generado y decodificado localmente: ' + lectura.data);
}

prepararQr().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});
