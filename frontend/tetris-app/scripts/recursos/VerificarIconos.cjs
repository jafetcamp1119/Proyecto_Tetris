const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const raiz = path.join(__dirname, '../..');
const dimensiones = archivo => {
    const datos = fs.readFileSync(archivo);
    assert.equal(datos.toString('ascii', 1, 4), 'PNG');
    return [datos.readUInt32BE(16), datos.readUInt32BE(20)];
};
for (const tamano of [16,32,48]) {
    assert.deepEqual(dimensiones(path.join(raiz, `src/assets/icon/tetris-favicon-v2-${tamano}.png`)), [tamano,tamano]);
}
for (const tamano of [180,192,512]) {
    assert.deepEqual(dimensiones(path.join(raiz, `src/assets/icon/tetris-logo-v2-${tamano}.png`)), [tamano,tamano]);
}
const ico = fs.readFileSync(path.join(raiz, 'src/assets/icon/tetris-favicon-v2.ico'));
assert.equal(ico.readUInt16LE(2), 1);
assert.equal(ico.readUInt16LE(4), 3);
for (let indice=0;indice<3;indice++) {
    const inicio=6+indice*16;
    const longitud=ico.readUInt32LE(inicio+8);
    const offset=ico.readUInt32LE(inicio+12);
    assert.ok(longitud>100 && offset+longitud<=ico.length);
    assert.equal(ico.toString('ascii',offset+1,offset+4),'PNG');
}
for (const [densidad,tamano,frente] of [['mdpi',48,108],['hdpi',72,162],['xhdpi',96,216],['xxhdpi',144,324],['xxxhdpi',192,432]]) {
    const carpeta=path.join(raiz,'android/app/src/main/res','mipmap-'+densidad);
    assert.deepEqual(dimensiones(path.join(carpeta,'ic_launcher.png')),[tamano,tamano]);
    assert.deepEqual(dimensiones(path.join(carpeta,'ic_launcher_foreground.png')),[frente,frente]);
}
const manifiesto=JSON.parse(fs.readFileSync(path.join(raiz,'src/manifest.webmanifest'),'utf8'));
for (const icono of manifiesto.icons) {
    assert.ok(fs.existsSync(path.join(raiz,'src',icono.src)));
}
assert.ok(manifiesto.icons.some(icono=>icono.purpose==='maskable'));
console.log('OK: dimensiones PNG, tres frames ICO, cinco densidades Android y manifest PWA. No se genero APK.');
