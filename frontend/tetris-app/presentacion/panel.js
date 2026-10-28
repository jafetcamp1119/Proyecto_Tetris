// Consulta el estado del iniciador y habilita el QR solo cuando el sistema informa disponibilidad reciente.
const indicador = document.querySelector('#estado');
const textoEstado = document.querySelector('#estado-texto');
const mensaje = document.querySelector('#mensaje');
const qr = document.querySelector('#qr');
const espera = document.querySelector('#espera');
const abrir = document.querySelector('#abrir');
let disponible = false;

function mostrarEstado(estado, detalle) {
  disponible = estado === 'listo';
  indicador.className = 'estado ' + estado;
  const etiquetas = { listo: 'SERVIDOR EN LÍNEA', preparando: 'PREPARANDO CONEXIÓN', error: 'SERVIDOR NO DISPONIBLE' };
  textoEstado.textContent = etiquetas[estado] || etiquetas.error;
  mensaje.textContent = detalle;
  qr.hidden = !disponible;
  espera.hidden = disponible;
  abrir.setAttribute('aria-disabled', String(!disponible));
}

async function actualizarEstado() {
  try {
    const respuesta = await fetch('/estado', { cache: 'no-store', signal: AbortSignal.timeout(6000) });
    if (!respuesta.ok) {
      throw new Error('Sin estado');
    }
    const datos = await respuesta.json();
    const reciente = Date.now() - Date.parse(datos.actualizadoUtc) < 45000;
    if (datos.estado === 'listo' && !reciente) {
      throw new Error('Estado vencido');
    }
    mostrarEstado(datos.estado, datos.mensaje);
  } catch {
    mostrarEstado('error', 'No se pudo comprobar el sistema. No invites a los jugadores hasta recuperar la conexión.');
  } finally {
    setTimeout(actualizarEstado, 3000);
  }
}

abrir.addEventListener('click', (evento) => {
  if (!disponible) {
    evento.preventDefault();
  }
});
document.querySelector('#pantalla').addEventListener('click', async () => {
  if (document.fullscreenElement) {
    await document.exitFullscreen();
  } else {
    await document.documentElement.requestFullscreen();
  }
});
void actualizarEstado();
