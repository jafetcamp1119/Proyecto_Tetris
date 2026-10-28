// Se analiza una sola vez al decodificar. No recorta música ni modifica los archivos originales.
export function buscarComienzoAudio(buffer: AudioBuffer): number {
  if (typeof buffer.getChannelData !== 'function') {
    return 0;
  }

  const limite = Math.min(buffer.length, Math.floor(buffer.sampleRate));
  const canales = Array.from({ length: buffer.numberOfChannels }, (_, canal) => buffer.getChannelData(canal));

  for (let muestra = 0; muestra < limite; muestra++) {
    if (canales.some((canal) => Math.abs(canal[muestra]) > 0.001)) {
      return Math.max(0, muestra / buffer.sampleRate - 0.002);
    }
  }

  return 0;
}
