using AccesoDatos;
using AccesoDatos.Contexto;
using Dominio.Entidades;
using Dominio.EntidadesTipadas;
using Dominio.Juego;
using Dominio.InterfazAD;
using Dominio.InterfazJuego;
using LogicaJuego.Motor;
using LogicaJuego.Servicios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class PruebasSql
{
    public static async Task<int> EjecutarAsync()
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__TetrisDB") ?? "Server=JAFET\\SQLEXPRESS;Database=TetrisDB;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=5";
        await using var db = new TetrisDBContext(new DbContextOptionsBuilder<TetrisDBContext>().UseSqlServer(cadena).Options);
        await using var transaccion = await db.Database.BeginTransactionAsync();
        var prefijo = "PruebaArq_" + Guid.NewGuid().ToString("N")[..10];
        try
        {
            VerificarCatalogoReal(cadena);
            var usuarios = Enumerable.Range(
                1,
                4).Select(i => new Usuario { NombreUsuario = prefijo + i, Correo = prefijo + i + "@example.com", PasswordHash = "SoloParaPruebaTransaccional", Activo = true, FechaRegistro = DateTime.UtcNow }).ToArray();
            db.Usuarios.AddRange(usuarios);
            await db.SaveChangesAsync();
            using var unidad = new UnidadTrabajoEF(db);
            var registro = new RegistroPartidaLJ(unidad);
            foreach (var modo in new[]
            {
                ModoJuego.Individual,
                ModoJuego.Compartido,
                ModoJuego.Competitivo
            }

            )
            {
                var salas = new SalaLJ(new MotorControlado());
                var id = salas.Crear(
                    usuarios[0].UsuarioId,
                    usuarios[0].NombreUsuario,
                    new TCrearSala { Modo = modo }).Data!.Id;
                var cantidad = modo == ModoJuego.Individual ? 1 : 4;
                for (var i = 0; i < cantidad; i++)
                {
                    if (i > 0)
                    {
                        salas.Unirse(id, usuarios[i].UsuarioId, usuarios[i].NombreUsuario);
                    }

                    salas.Conectar(id, usuarios[i].UsuarioId, $"sql-{i}");
                    salas.Preparar(id, usuarios[i].UsuarioId, true);
                }

                var inicio = salas.Iniciar(id, usuarios[0].UsuarioId);
                if (!inicio.Success)
                {
                    throw new Exception(inicio.Error);
                }

                await registro.GuardarAsync(inicio.Data!);
                // En compartido pierde cada turno hasta que queda un superviviente.
                var derrotas = modo == ModoJuego.Compartido ? cantidad - 1 : cantidad;
                for (var i = 0; i < derrotas; i++)
                {
                    var estado = salas.Mover(
                        id,
                        usuarios[i].UsuarioId,
                        new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 }).Data!;
                    await registro.GuardarAsync(estado);
                }

                var final = salas.Obtener(id, usuarios[0].UsuarioId).Data!;
                await registro.GuardarAsync(final);
                if (final.Estado != EstadoSala.Finalizada)
                {
                    throw new Exception("La partida no finalizó.");
                }

                var partida = await db.Partidas.SingleAsync(p => p.CodigoSala == final.Codigo);
                var resultados = await db.Resultados.Where(r => r.PartidaId == partida.PartidaId).ToListAsync();
                if (resultados.Count != cantidad || resultados.Any(r => r.PosicionFinal != (modo == ModoJuego.Compartido && !r.EsGanador ? 2 : 1)))
                {
                    throw new Exception("Resultados inconsistentes en SQL.");
                }

                if (modo == ModoJuego.Competitivo && (partida.GanadorId != null || resultados.Any(r => !r.EsGanador)))
                {
                    throw new Exception("El empate no se conservó.");
                }

                Console.WriteLine($"OK SQL: {modo}, participantes, resultados y empate.");
            }

            var estadistica = await db.EstadisticasJugadors.SingleAsync(e => e.UsuarioId == usuarios[0].UsuarioId);
            if (estadistica.PartidasJugadas != 3)
            {
                throw new Exception("Se duplicaron o faltaron estadísticas.");
            }

            Console.WriteLine("OK SQL: estadísticas idempotentes.");

            var cancelaciones = new SalaLJ(new MotorControlado());
            var cancelacionId = cancelaciones.Crear(usuarios[0].UsuarioId, "Creador",
                new TCrearSala { Modo = ModoJuego.Competitivo }).Data!.Id;

            for (var indice = 0; indice < 4; indice++)
            {
                if (indice > 0)
                {
                    cancelaciones.Unirse(cancelacionId, usuarios[indice].UsuarioId, "Participante");
                }

                cancelaciones.Conectar(cancelacionId, usuarios[indice].UsuarioId, $"cancelar-{indice}");
                cancelaciones.Preparar(cancelacionId, usuarios[indice].UsuarioId, true);
            }

            await registro.GuardarAsync(cancelaciones.Iniciar(cancelacionId, usuarios[0].UsuarioId).Data!);
            var provisional = cancelaciones.Mover(cancelacionId, usuarios[1].UsuarioId,
                new TMovimiento { Movimiento = Movimiento.Caer, Secuencia = 1 }).Data!;
            await registro.GuardarAsync(provisional);
            var cancelada = cancelaciones.Abandonar(cancelacionId, usuarios[0].UsuarioId).Data!;
            await registro.GuardarAsync(cancelada);
            var entidadCancelada = await db.Partidas.SingleAsync(partida => partida.CodigoSala == cancelada.Codigo);
            if (entidadCancelada.Estado != "Cancelada" || entidadCancelada.GanadorId != null
                || await db.Resultados.AnyAsync(resultado => resultado.PartidaId == entidadCancelada.PartidaId))
            {
                throw new Exception("La cancelación conservó resultados provisionales o un ganador.");
            }

            var quedanActivos = await db.ParticipantesPartida.AnyAsync(participante =>
                participante.PartidaId == entidadCancelada.PartidaId && participante.Estado == "Jugando");
            if (quedanActivos)
            {
                throw new Exception("La cancelación dejó participantes jugando.");
            }

            await registro.CancelarInterrumpidasAsync();
            var finales = await db.Partidas.CountAsync(partida => partida.CreadorId == usuarios[0].UsuarioId
                && partida.Estado == "Finalizada");
            if (finales != 3 || estadistica.PartidasJugadas != 3)
            {
                throw new Exception("La limpieza de reinicio alteró finales o estadísticas.");
            }

            Console.WriteLine("OK SQL: cancelación, eliminación de provisionales y conservación de finales.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR SQL: {ex}");
            return 1;
        }
        finally
        {
            await transaccion.RollbackAsync();
            db.ChangeTracker.Clear();
            var quedan = await db.Usuarios.AnyAsync(u => u.NombreUsuario.StartsWith(prefijo));
            Console.WriteLine(quedan ? "ERROR: revisar limpieza de usuarios de prueba." : "ROLLBACK correcto: no quedaron registros de prueba.");
        }
    }

    private static void VerificarCatalogoReal(string cadena)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddDbContext<TetrisDBContext>(opciones =>
        {
            opciones.UseSqlServer(cadena);
        });
        servicios.AddScoped<IUnidadTrabajoEF, UnidadTrabajoEF>();
        servicios.AddSingleton<ICatalogoPiezasEspeciales, CatalogoPiezasEspeciales>();
        using var proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var catalogo = proveedor.GetRequiredService<ICatalogoPiezasEspeciales>();
        var respuesta = catalogo.ObtenerActivas();

        if (!respuesta.Success)
        {
            throw new Exception(respuesta.Error);
        }

        var efectos = respuesta.Data!.Select(EfectosEspeciales.Identificar).ToHashSet();
        var esperados = Enum.GetValues<EfectoEspecial>().Where(efecto => efecto != EfectoEspecial.Ninguno);

        if (esperados.Any(efecto => !efectos.Contains(efecto)))
        {
            throw new Exception("El catálogo SQL activo no coincide con los siete efectos del motor.");
        }

        var salas = new SalaLJ(new MotorJuego(), new OpcionesPausa(), catalogo);
        var sala = salas.Crear(1, "Prueba catálogo", new TCrearSala
        {
            Modo = ModoJuego.Compartido,
            PiezasEspeciales = new TConfiguracionPiezas
            {
                Activas = true,
                Probabilidad = 100,
                TiposPermitidos = ["BARREDORA"]
            }
        });

        if (!sala.Success || !sala.Data!.PiezasEspeciales.TiposPermitidos!.SequenceEqual(new[] { "BARREDORA" }))
        {
            throw new Exception("La sala no pudo seleccionar Barredora desde el catálogo SQL real.");
        }

        Console.WriteLine("OK SQL: catálogo real, entidades EF, siete efectos y selección de Barredora por código.");
    }
}
