// Convierte modos, especiales y motivos finales en textos que entiende el jugador.
import { EfectoEspecial, ITipoPiezaEspecial, ModoJuego } from './tetris.model';

export const nombresEfectos: Record<EfectoEspecial, string> = {
  Ninguno: 'Pieza especial',
  EliminarFila: 'Elimina fila',
  EliminarColumna: 'Elimina columnas',
  Confusion: 'Confusión',
  Acelerador: 'Acelerador',
  Bomba: 'Bomba',
  Barredora: 'Barredora',
  DoblePuntuacion: 'Doble puntuación',
};

export function nombreModo(modo: ModoJuego): string {
  return modo === 'Individual' ? 'Solitario' : modo;
}

export function efectoDePieza(tipo: ITipoPiezaEspecial): EfectoEspecial {
  if (tipo.eliminaColumna) {
    return 'EliminarColumna';
  }

  if (tipo.eliminaFila || tipo.codigo.toUpperCase().includes('FILA')) {
    return 'EliminarFila';
  }

  const codigo = tipo.codigo
    .normalize('NFD')
    .replace(/[\u0300-\u036f_\s]/g, '')
    .toUpperCase();
  const efectos: Record<string, EfectoEspecial> = {
    CONFUSION: 'Confusion',
    ACELERADOR: 'Acelerador',
    BOMBA: 'Bomba',
    BARREDORA: 'Barredora',
    DOBLE: 'DoblePuntuacion',
    DOBLEPUNTUACION: 'DoblePuntuacion',
  };
  return efectos[codigo] || 'Ninguno';
}

export function descripcionPieza(tipo: ITipoPiezaEspecial): string {
  const descripciones: Record<EfectoEspecial, string> = {
    Ninguno: 'Pieza especial disponible para esta partida.',
    EliminarFila:
      'Elimina la fila de la celda más baja de la pieza y baja las filas superiores. Esa limpieza no otorga puntos.',
    EliminarColumna: 'Vacía las columnas que ocupa la pieza. Esa limpieza no otorga puntos.',
    Confusion: 'Invierte izquierda y derecha del próximo jugador que sigue en juego durante unos segundos.',
    Acelerador: 'Hace que las piezas del próximo jugador que sigue en juego caigan más rápido temporalmente.',
    Bomba:
      'Elimina bloques en un área de 3 × 3 centrada en la celda inferior de la pieza, recortada en los bordes. No otorga puntos por esa limpieza.',
    Barredora: 'Elimina la mitad superior del tablero para liberar espacio. No otorga puntos.',
    DoblePuntuacion: 'Duplica los puntos de las líneas completas eliminadas en esa jugada.',
  };
  return descripciones[efectoDePieza(tipo)];
}

export function motivoFinal(motivo: string | null): string {
  const motivos: Record<string, string> = {
    MetaPuntos: '¡Se alcanzó la meta de puntos!',
    UltimoActivo: '¡Ya tenemos un ganador!',
    Supervivencia: '¡Quedó un solo jugador en pie!',
    TodosEliminados: '¡Partida finalizada!',
    CreadorAbandono: 'La persona que creó la sala terminó la partida.',
  };
  return motivos[motivo || ''] || '¡Partida finalizada!';
}
