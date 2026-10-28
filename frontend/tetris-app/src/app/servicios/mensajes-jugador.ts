import { ErrorApi } from './api.servicio';

export function mensajeParaJugador(
  error: unknown,
  alternativa = 'No pudimos completar la acción. Intentá de nuevo.',
): string {
  console.error('Tetris: no se pudo completar una operación.', error);

  if (error instanceof ErrorApi) {
    const mensajes: Record<string, string> = {
      NombreOcupado: 'Ese nickname ya está ocupado. Elegí otro.',
      NombreInvalido: 'Elegí un nickname de hasta 50 caracteres.',
      CredencialInvalida: 'No pudimos recuperar tu jugador. Ingresá con Google o con tu código privado.',
      SesionVencida: 'Tu sesión venció. Intentá ingresar nuevamente.',
      SesionInvalida: 'No pudimos recuperar tu sesión. Intentá ingresar nuevamente.',
      GoogleEnOtroPerfil:
        'Esa cuenta Google pertenece a otro jugador. Usá Cambiar de jugador para abrir ese perfil. Tu progreso se conserva.',
      GoogleYaVinculado: 'Este jugador ya tiene otra cuenta Google vinculada.',
      GoogleSinVincular: 'Continuá con Google para registrar tu nombre de jugador.',
      RegistroGoogleVencido: 'La verificación de Google venció o ya se utilizó. Continuá nuevamente con Google.',
      GoogleInvalido: 'No pudimos verificar tu cuenta Google. Intentá de nuevo.',
    };

    if (error.codigo && mensajes[error.codigo]) {
      return mensajes[error.codigo];
    }

    if (error.estado === 401) {
      return 'No pudimos recuperar tu sesión. Intentá ingresar nuevamente.';
    }

    if (error.estado === 429) {
      return 'Hiciste varios intentos seguidos. Esperá un minuto e intentá de nuevo.';
    }

    if (error.estado === 400) {
      const detalle = error.message.toLocaleLowerCase('es');

      if (detalle.includes('llena') || detalle.includes('capacidad')) {
        return 'La sala está llena. Elegí otra partida.';
      }

      if (detalle.includes('listo') || detalle.includes('mínimo') || detalle.includes('al menos')) {
        return 'Para empezar necesitan al menos dos jugadores conectados y listos.';
      }

      if (detalle.includes('código') || detalle.includes('no existe') || detalle.includes('no se encontró')) {
        return 'No encontramos esa sala. Revisá el código o buscá otra partida.';
      }

      if (detalle.includes('creador')) {
        return 'Solo quien creó la sala puede cambiar esta opción.';
      }
    }
  }

  return alternativa;
}
