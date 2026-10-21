using Dominio.EntidadesTipadas;
using Dominio.Juego;

namespace LogicaJuego.Servicios;

// SalaLJ llama estos métodos bajo el cerrojo de la sala. Los ataques se resuelven al fijar
// la pieza, después de las eliminaciones, sin colas que puedan conservar un objetivo inválido.
internal sealed class ControlEfectos
{
    private readonly OpcionesEfectos opciones;

    public ControlEfectos(OpcionesEfectos opciones)
    {
        opciones.Validar();
        this.opciones = opciones;
    }

    public void Activar(SalaMemoria sala, JugadorSala atacante, TableroJuego tablero, int puntos, DateTime ahora)
    {
        var codigo = tablero.EspecialUltimoEvento;

        if (codigo == null || sala.Modo == ModoJuego.Competitivo)
        {
            return;
        }

        var tipo = tablero.EfectoUltimoEvento;
        var temporal = tipo == EfectoEspecial.Confusion || tipo == EfectoEspecial.Acelerador;
        int? afectadoId = atacante.Id;
        var estado = "Aplicado";

        if (temporal)
        {
            var objetivo = BuscarObjetivo(sala, atacante);
            afectadoId = objetivo?.Id;

            if (objetivo == null)
            {
                estado = "SinObjetivo";
            }
            else
            {
                var segundos = tipo == EfectoEspecial.Confusion
                    ? opciones.SegundosConfusion
                    : opciones.SegundosAcelerador;
                var factor = tipo == EfectoEspecial.Acelerador ? opciones.FactorAcelerador : 1;
                // Repetir el mismo ataque renueva su plazo; nunca acumula multiplicadores.
                objetivo.Efectos.RemoveAll(efecto => efecto.Tipo == tipo);
                objetivo.Efectos.Add(new TEfectoTemporal(objetivo.Id, atacante.Id, tipo, ahora,
                    ahora.AddSeconds(segundos), segundos * 1000, true, null, factor));
            }
        }

        sala.SecuenciaEspecial++;
        sala.UltimoEfecto = new TActivacionEspecial(sala.SecuenciaEspecial, codigo, atacante.Id,
            afectadoId, ahora, puntos,
            tablero.CeldasEliminadasEspecial.Select(celda => new TCelda(celda.Fila, celda.Columna)).ToArray(), estado, tipo);
    }

    private static JugadorSala? BuscarObjetivo(SalaMemoria sala, JugadorSala atacante)
    {
        if (sala.Modo != ModoJuego.Compartido)
        {
            return null;
        }

        var indice = sala.Jugadores.IndexOf(atacante);

        for (var distancia = 1; distancia < sala.Jugadores.Count; distancia++)
        {
            var candidato = sala.Jugadores[(indice + distancia) % sala.Jugadores.Count];

            if (!candidato.Perdido && !candidato.Abandono)
            {
                return candidato;
            }
        }

        return null;
    }

    public static void Actualizar(SalaMemoria sala, DateTime ahora)
    {
        var instante = ahora;

        if (sala.Pausa.Fase == FasePausa.Pausada && sala.Pausa.InicioPausa.HasValue)
        {
            instante = sala.Pausa.InicioPausa.Value;
        }

        foreach (var jugador in sala.Jugadores)
        {
            for (var indice = 0; indice < jugador.Efectos.Count; indice++)
            {
                var efecto = jugador.Efectos[indice];

                if (!efecto.Activo)
                {
                    continue;
                }

                string? motivo = null;

                if (jugador.Perdido || jugador.Abandono)
                {
                    motivo = "JugadorEliminado";
                }
                else if (sala.Estado != EstadoSala.EnCurso)
                {
                    motivo = "PartidaTerminada";
                }
                else if (instante >= efecto.FinUtc)
                {
                    motivo = "TiempoAgotado";
                }

                var restante = Math.Max(0, (int)Math.Ceiling((efecto.FinUtc - instante).TotalMilliseconds));

                if (motivo != null)
                {
                    restante = 0;
                }

                var actualizado = efecto with
                {
                    MilisegundosRestantes = restante,
                    Activo = motivo == null,
                    MotivoFin = motivo
                };

                if (actualizado != efecto)
                {
                    jugador.Efectos[indice] = actualizado;
                    sala.Version++;
                }
            }
        }
    }

    public static Movimiento TraducirMovimiento(JugadorSala jugador, Movimiento movimiento)
    {
        if (!jugador.Efectos.Any(efecto => efecto.Activo && efecto.Tipo == EfectoEspecial.Confusion))
        {
            return movimiento;
        }

        switch (movimiento)
        {
            case Movimiento.Izquierda:
                return Movimiento.Derecha;
            case Movimiento.Derecha:
                return Movimiento.Izquierda;
            default:
                return movimiento;
        }
    }

    public static int FactorVelocidad(JugadorSala jugador)
    {
        return jugador.Efectos.Where(efecto => efecto.Activo && efecto.Tipo == EfectoEspecial.Acelerador)
            .Select(efecto => efecto.FactorVelocidad).DefaultIfEmpty(1).Max();
    }
}
