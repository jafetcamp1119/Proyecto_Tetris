using System;
using System.Collections.Generic;
using Dominio.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AccesoDatos.Contexto;

public partial class TetrisDBContext : DbContext
{
    public TetrisDBContext()
    {
    }

    public TetrisDBContext(DbContextOptions<TetrisDBContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BloquesTablero> BloquesTableros { get; set; }

    public virtual DbSet<ConexionesJugador> ConexionesJugadors { get; set; }

    public virtual DbSet<ConfiguracionJugador> ConfiguracionJugadors { get; set; }

    public virtual DbSet<EliminacionesBloque> EliminacionesBloques { get; set; }

    public virtual DbSet<EstadisticasJugador> EstadisticasJugadors { get; set; }

    public virtual DbSet<EventosPartidum> EventosPartida { get; set; }

    public virtual DbSet<ParticipantesPartidum> ParticipantesPartida { get; set; }

    public virtual DbSet<Partida> Partidas { get; set; }

    public virtual DbSet<PausasPartidum> PausasPartida { get; set; }

    public virtual DbSet<Perfile> Perfiles { get; set; }

    public virtual DbSet<PiezasJugador> PiezasJugadors { get; set; }

    public virtual DbSet<RecompensasPunto> RecompensasPuntos { get; set; }

    public virtual DbSet<Resultado> Resultados { get; set; }

    public virtual DbSet<SeleccionesRecompensa> SeleccionesRecompensas { get; set; }

    public virtual DbSet<TablerosPartidum> TablerosPartida { get; set; }

    public virtual DbSet<TiposPieza> TiposPiezas { get; set; }

    public virtual DbSet<TurnosPartidum> TurnosPartida { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    public virtual DbSet<VistaRanking> VistaRankings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BloquesTablero>(entity =>
        {
            entity.HasKey(e => e.BloqueId).HasName("PK__BloquesT__1E811A834693E84D");

            entity.ToTable("BloquesTablero");

            entity.HasIndex(e => new { e.TableroId, e.Fila, e.Columna }, "IX_Bloques_Posicion");

            entity.HasIndex(e => e.TableroId, "IX_Bloques_Tablero");

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.FechaColocacion).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.PiezaJugador).WithMany(p => p.BloquesTableros)
                .HasForeignKey(d => d.PiezaJugadorId)
                .HasConstraintName("FK_Bloque_Pieza");

            entity.HasOne(d => d.Tablero).WithMany(p => p.BloquesTableros)
                .HasForeignKey(d => d.TableroId)
                .HasConstraintName("FK_Bloque_Tablero");

            entity.HasOne(d => d.Usuario).WithMany(p => p.BloquesTableros)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bloque_Usuario");
        });

        modelBuilder.Entity<ConexionesJugador>(entity =>
        {
            entity.HasKey(e => e.ConexionId).HasName("PK__Conexion__ECBFBD71080A476C");

            entity.ToTable("ConexionesJugador");

            entity.Property(e => e.EstadoConexion)
                .HasMaxLength(20)
                .HasDefaultValue("Conectado");
            entity.Property(e => e.FechaConexion).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Partida).WithMany(p => p.ConexionesJugadors)
                .HasForeignKey(d => d.PartidaId)
                .HasConstraintName("FK_Conexion_Partida");

            entity.HasOne(d => d.Usuario).WithMany(p => p.ConexionesJugadors)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Conexion_Usuario");
        });

        modelBuilder.Entity<ConfiguracionJugador>(entity =>
        {
            entity.HasKey(e => e.ConfiguracionId).HasName("PK__Configur__9B95E03607AAC406");

            entity.ToTable("ConfiguracionJugador");

            entity.HasIndex(e => e.UsuarioId, "UQ__Configur__2B3DE7B965F732F4").IsUnique();

            entity.Property(e => e.MostrarControlesPantalla).HasDefaultValue(true);
            entity.Property(e => e.PermitirPausa).HasDefaultValue(true);
            entity.Property(e => e.SonidoActivo).HasDefaultValue(true);

            entity.HasOne(d => d.Usuario).WithOne(p => p.ConfiguracionJugador)
                .HasForeignKey<ConfiguracionJugador>(d => d.UsuarioId)
                .HasConstraintName("FK_Configuracion_Usuario");
        });

        modelBuilder.Entity<EliminacionesBloque>(entity =>
        {
            entity.HasKey(e => e.EliminacionId).HasName("PK__Eliminac__BBE8B559E2E78462");

            entity.Property(e => e.FechaEliminacion).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Partida).WithMany(p => p.EliminacionesBloques)
                .HasForeignKey(d => d.PartidaId)
                .HasConstraintName("FK_Eliminacion_Partida");

            entity.HasOne(d => d.PiezaJugador).WithMany(p => p.EliminacionesBloques)
                .HasForeignKey(d => d.PiezaJugadorId)
                .HasConstraintName("FK_Eliminacion_Pieza");

            entity.HasOne(d => d.UsuarioAfectado).WithMany(p => p.EliminacionesBloqueUsuarioAfectados)
                .HasForeignKey(d => d.UsuarioAfectadoId)
                .HasConstraintName("FK_Eliminacion_Afectado");

            entity.HasOne(d => d.UsuarioAtacante).WithMany(p => p.EliminacionesBloqueUsuarioAtacantes)
                .HasForeignKey(d => d.UsuarioAtacanteId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Eliminacion_Atacante");
        });

        modelBuilder.Entity<EstadisticasJugador>(entity =>
        {
            entity.HasKey(e => e.EstadisticaId).HasName("PK__Estadist__5E78B40CE3AC4888");

            entity.ToTable("EstadisticasJugador");

            entity.HasIndex(e => e.UsuarioId, "UQ__Estadist__2B3DE7B958C27DEC").IsUnique();

            entity.Property(e => e.FechaActualizacion).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.NivelMaximoAlcanzado).HasDefaultValue(1);

            entity.HasOne(d => d.Usuario).WithOne(p => p.EstadisticasJugador)
                .HasForeignKey<EstadisticasJugador>(d => d.UsuarioId)
                .HasConstraintName("FK_Estadistica_Usuario");
        });

        modelBuilder.Entity<EventosPartidum>(entity =>
        {
            entity.HasKey(e => e.EventoId).HasName("PK__EventosP__1EEB592193C6A0A6");

            entity.HasIndex(e => e.PartidaId, "IX_Eventos_Partida");

            entity.Property(e => e.FechaEvento).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TipoEvento).HasMaxLength(50);

            entity.HasOne(d => d.Partida).WithMany(p => p.EventosPartida)
                .HasForeignKey(d => d.PartidaId)
                .HasConstraintName("FK_Evento_Partida");

            entity.HasOne(d => d.Usuario).WithMany(p => p.EventosPartida)
                .HasForeignKey(d => d.UsuarioId)
                .HasConstraintName("FK_Evento_Usuario");
        });

        modelBuilder.Entity<ParticipantesPartidum>(entity =>
        {
            entity.HasKey(e => e.ParticipanteId).HasName("PK__Particip__E6DEAC5F28DF13C4");

            entity.HasIndex(e => e.PartidaId, "IX_Participantes_Partida");

            entity.HasIndex(e => new { e.PartidaId, e.UsuarioId }, "UQ_Participante").IsUnique();

            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .HasDefaultValue("Esperando");
            entity.Property(e => e.FechaIngreso).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.NivelActual).HasDefaultValue(1);

            entity.HasOne(d => d.Partida).WithMany(p => p.ParticipantesPartida)
                .HasForeignKey(d => d.PartidaId)
                .HasConstraintName("FK_Participante_Partida");

            entity.HasOne(d => d.Usuario).WithMany(p => p.ParticipantesPartida)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Participante_Usuario");
        });

        modelBuilder.Entity<Partida>(entity =>
        {
            entity.HasKey(e => e.PartidaId).HasName("PK__Partidas__E63E1FE37F3403D0");

            entity.HasIndex(e => e.Estado, "IX_Partidas_Estado");

            entity.HasIndex(e => e.CodigoSala, "UQ__Partidas__44BC6388F5EC572E").IsUnique();

            entity.Property(e => e.CantidadMaximaJugadores).HasDefaultValue(4);
            entity.Property(e => e.CantidadMinimaJugadores).HasDefaultValue(4);
            entity.Property(e => e.CodigoSala).HasMaxLength(20);
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .HasDefaultValue("Esperando");
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.TipoPartida)
                .HasMaxLength(20)
                .HasDefaultValue("Multijugador");

            entity.HasOne(d => d.Creador).WithMany(p => p.PartidaCreadors)
                .HasForeignKey(d => d.CreadorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Partidas_Creador");

            entity.HasOne(d => d.Ganador).WithMany(p => p.PartidaGanadors)
                .HasForeignKey(d => d.GanadorId)
                .HasConstraintName("FK_Partidas_Ganador");
        });

        modelBuilder.Entity<PausasPartidum>(entity =>
        {
            entity.HasKey(e => e.PausaId).HasName("PK__PausasPa__7F0BF15C11508E55");

            entity.Property(e => e.Activa).HasDefaultValue(true);
            entity.Property(e => e.FechaInicio).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Partida).WithMany(p => p.PausasPartida)
                .HasForeignKey(d => d.PartidaId)
                .HasConstraintName("FK_Pausa_Partida");

            entity.HasOne(d => d.Turno).WithMany(p => p.PausasPartida)
                .HasForeignKey(d => d.TurnoId)
                .HasConstraintName("FK_Pausa_Turno");

            entity.HasOne(d => d.Usuario).WithMany(p => p.PausasPartida)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Pausa_Usuario");
        });

        modelBuilder.Entity<Perfile>(entity =>
        {
            entity.HasKey(e => e.PerfilId).HasName("PK__Perfiles__0C005B062FF78748");

            entity.HasIndex(e => e.UsuarioId, "UQ__Perfiles__2B3DE7B9593FAAD5").IsUnique();

            entity.Property(e => e.FechaActualizacion).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.NombreVisible).HasMaxLength(50);

            entity.HasOne(d => d.Usuario).WithOne(p => p.Perfile)
                .HasForeignKey<Perfile>(d => d.UsuarioId)
                .HasConstraintName("FK_Perfiles_Usuarios");
        });

        modelBuilder.Entity<PiezasJugador>(entity =>
        {
            entity.HasKey(e => e.PiezaJugadorId).HasName("PK__PiezasJu__1D5E5C9D046970FD");

            entity.ToTable("PiezasJugador");

            entity.Property(e => e.FechaAsignacion).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Partida).WithMany(p => p.PiezasJugadors)
                .HasForeignKey(d => d.PartidaId)
                .HasConstraintName("FK_PiezaJugador_Partida");

            entity.HasOne(d => d.TipoPieza).WithMany(p => p.PiezasJugadors)
                .HasForeignKey(d => d.TipoPiezaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PiezaJugador_Tipo");

            entity.HasOne(d => d.Usuario).WithMany(p => p.PiezasJugadors)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PiezaJugador_Usuario");
        });

        modelBuilder.Entity<RecompensasPunto>(entity =>
        {
            entity.HasKey(e => e.RecompensaId).HasName("PK__Recompen__351687DD3C56EF91");

            entity.Property(e => e.Activa).HasDefaultValue(true);
            entity.Property(e => e.Descripcion).HasMaxLength(255);
            entity.Property(e => e.Nombre).HasMaxLength(50);

            entity.HasOne(d => d.TipoPieza).WithMany(p => p.RecompensasPuntos)
                .HasForeignKey(d => d.TipoPiezaId)
                .HasConstraintName("FK_Recompensa_Pieza");
        });

        modelBuilder.Entity<Resultado>(entity =>
        {
            entity.HasKey(e => e.ResultadoId).HasName("PK__Resultad__7904DD616E21C864");

            entity.HasIndex(e => e.Puntuacion, "IX_Resultados_Puntuacion").IsDescending();

            entity.HasIndex(e => new { e.PartidaId, e.UsuarioId }, "UQ_Resultado").IsUnique();

            entity.Property(e => e.EstadoFinal).HasMaxLength(20);
            entity.Property(e => e.FechaResultado).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.NivelMaximo).HasDefaultValue(1);

            entity.HasOne(d => d.Partida).WithMany(p => p.Resultados)
                .HasForeignKey(d => d.PartidaId)
                .HasConstraintName("FK_Resultado_Partida");

            entity.HasOne(d => d.Usuario).WithMany(p => p.Resultados)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Resultado_Usuario");
        });

        modelBuilder.Entity<SeleccionesRecompensa>(entity =>
        {
            entity.HasKey(e => e.SeleccionId).HasName("PK__Seleccio__5D23944B749E5428");

            entity.ToTable("SeleccionesRecompensa");

            entity.Property(e => e.FechaSeleccion).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Partida).WithMany(p => p.SeleccionesRecompensas)
                .HasForeignKey(d => d.PartidaId)
                .HasConstraintName("FK_Seleccion_Partida");

            entity.HasOne(d => d.Recompensa).WithMany(p => p.SeleccionesRecompensas)
                .HasForeignKey(d => d.RecompensaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Seleccion_Recompensa");

            entity.HasOne(d => d.Usuario).WithMany(p => p.SeleccionesRecompensas)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Seleccion_Usuario");
        });

        modelBuilder.Entity<TablerosPartidum>(entity =>
        {
            entity.HasKey(e => e.TableroId).HasName("PK__Tableros__270E2AFCA0B90DD8");

            entity.HasIndex(e => e.PartidaId, "UQ__Tableros__E63E1FE26401C35D").IsUnique();

            entity.Property(e => e.Columnas).HasDefaultValue(10);
            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .HasDefaultValue("Esperando");
            entity.Property(e => e.FechaActualizacion).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Filas).HasDefaultValue(20);

            entity.HasOne(d => d.Partida).WithOne(p => p.TablerosPartidum)
                .HasForeignKey<TablerosPartidum>(d => d.PartidaId)
                .HasConstraintName("FK_Tablero_Partida");

            entity.HasOne(d => d.UsuarioPausa).WithMany(p => p.TablerosPartida)
                .HasForeignKey(d => d.UsuarioPausaId)
                .HasConstraintName("FK_Tablero_Pausa");
        });

        modelBuilder.Entity<TiposPieza>(entity =>
        {
            entity.HasKey(e => e.TipoPiezaId).HasName("PK__TiposPie__DA0ED64E25496D04");

            entity.ToTable("TiposPieza");

            entity.HasIndex(e => e.Codigo, "UQ__TiposPie__06370DAC9A8FAB37").IsUnique();

            entity.Property(e => e.Activa).HasDefaultValue(true);
            entity.Property(e => e.Codigo).HasMaxLength(10);
            entity.Property(e => e.Descripcion).HasMaxLength(255);
            entity.Property(e => e.Nombre).HasMaxLength(50);
        });

        modelBuilder.Entity<TurnosPartidum>(entity =>
        {
            entity.HasKey(e => e.TurnoId).HasName("PK__TurnosPa__AD3E2E94FF149DFA");

            entity.HasIndex(e => e.PartidaId, "IX_Turnos_Partida");

            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .HasDefaultValue("Pendiente");

            entity.HasOne(d => d.Partida).WithMany(p => p.TurnosPartida)
                .HasForeignKey(d => d.PartidaId)
                .HasConstraintName("FK_Turno_Partida");

            entity.HasOne(d => d.Usuario).WithMany(p => p.TurnosPartida)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Turno_Usuario");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.UsuarioId).HasName("PK__Usuarios__2B3DE7B88839D6AF");

            entity.HasIndex(e => e.Correo, "UQ__Usuarios__60695A194A7AC665").IsUnique();

            entity.HasIndex(e => e.NombreUsuario, "UQ__Usuarios__6B0F5AE033B56FF8").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.Correo).HasMaxLength(100);
            entity.Property(e => e.FechaRegistro).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.NombreUsuario).HasMaxLength(50);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
        });

        modelBuilder.Entity<VistaRanking>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("VistaRanking");

            entity.Property(e => e.NombreUsuario).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
