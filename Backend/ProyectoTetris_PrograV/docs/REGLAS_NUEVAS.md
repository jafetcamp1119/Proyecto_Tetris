# Reglas vigentes del Tetris

La actualización definitiva del 21 de octubre de 2026 está en
[ACTUALIZACION_DEFINITIVA.md](ACTUALIZACION_DEFINITIVA.md).
Ese informe incluye contratos, archivos, pruebas y pendientes.

## Modalidades

- Individual: tablero propio; las piezas especiales son opcionales.
- Compartido: un tablero, turnos por pieza, eliminación individual y espectadores.
  Al perder el turno se vacían las diez filas superiores sin puntos; continúa el siguiente activo.
  El último activo gana automáticamente. La meta configurada es personal, como confirmó la usuaria.
- Competitivo: tableros independientes simultáneos, únicamente las siete formas tradicionales.
  Se conserva la selección de formas normales configurada. Nunca aparecen especiales, ataques ni poderes.
  Todos siguen hasta perder, incluido el último. Gana la puntuación máxima y se comparten los empates.

La API conserva los nombres de modos. Si se usan números, siguen siendo
Individual=0, Compartido=1, Competitivo=2; los nombres modo 1/2/3 de las reglas no cambian este contrato.
Es preferible enviar los nombres JSON.

## Pausas

Se conservan la pausa individual y la votación multijugador: mayoría estricta,
15 segundos para votar, una solicitud por jugador y 180 segundos de pausa aprobada.
Los valores se configuran en la sección Pausas de appsettings.json.
Solo votan jugadores no eliminados; un desconectado cuenta pero no emite un voto automático.
Durante la votación el juego sigue. La pausa aprobada congela la caída y los turnos normales.

La nueva regla de reconexión prevalece: son 30 segundos reales también en pausa.
Una eliminación por vencimiento puede pasar el turno, y la desconexión vencida del creador cierra la sala.
Las piezas continúan detenidas mientras dure la pausa.

Eventos conservados: PausaSolicitada, VotoPausaRegistrado, PausaRechazada,
PausaIniciada y PausaFinalizada. Sus cuerpos son IPausa; EstadoActualizado también contiene pausa.

## Salas privadas

Se conserva el código aleatorio de seis caracteres. La sala privada no aparece en el listado,
exige el código para el primer ingreso y permite recuperar la conexión de un miembro existente.
Una sala pública admite identificador o código público de doce caracteres.
Solo el creador configura o inicia. Multijugador exige al menos cuatro participantes listos y conectados.

## Especiales

El servidor lee los tipos activos de TiposPieza al configurar la sala.
Se conservan EliminaColumna y EliminaFila, documentados en CONFIGURACION_PARTIDA.md.
El competitivo omite el catálogo y fuerza especiales desactivadas en creación, configuración e inicio.
Los demás efectos carecen de reglas suficientes en el proyecto: no se han inventado.

## Reinicio y abandonos

Abandonar es una acción explícita en REST y SignalR. Desconectar involuntariamente inicia el plazo.
El creador cancela sin ganadores; los participantes se eliminan conservando puntos.
Al reiniciar se cancelan las partidas incompletas en SQL y se eliminan resultados provisionales.
Los resultados finalizados ya guardados se conservan.
