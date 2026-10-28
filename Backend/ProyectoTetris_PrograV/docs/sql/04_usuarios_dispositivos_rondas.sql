-- Prepara nicknames únicos normalizados, credenciales de dispositivo y códigos históricos de ronda.
-- Incluye un procedimiento que consulta ganadores ya escritos por el servidor.
USE [TetrisDB];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

-- Ejecutar durante mantenimiento, con el backend detenido y DESPUÉS de BACKUP + RESTORE VERIFYONLY.
-- Cambiar a 1 solamente después de verificar el respaldo. No se ejecuta contra la BD real automáticamente.
DECLARE @RespaldoVerificado bit = 0;
IF @RespaldoVerificado <> 1
    THROW 50040, 'Primero respalde y verifique TetrisDB; luego confirme @RespaldoVerificado.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
        THROW 50041, 'No existe dbo.Usuarios.', 1;

    -- Detiene la migración si la nueva normalización encuentra duplicados. No renombra ni borra usuarios.
    IF EXISTS (
        SELECT UPPER(LTRIM(RTRIM(NombreUsuario)) COLLATE Latin1_General_100_CI_AS_SC)
        FROM dbo.Usuarios WITH (TABLOCKX, HOLDLOCK)
        GROUP BY UPPER(LTRIM(RTRIM(NombreUsuario)) COLLATE Latin1_General_100_CI_AS_SC)
        HAVING COUNT(*) > 1
    )
        THROW 50042, 'Hay nicknames duplicados normalizados. Resolver manualmente preservando identidades.', 1;

    -- El índice único anterior del correo admite un solo NULL: se sustituye por uno filtrado.
    DECLARE @nombreIndice sysname;
    DECLARE @esRestriccion bit;
    DECLARE @sentencia nvarchar(max);
    DECLARE indicesCorreo CURSOR LOCAL FAST_FORWARD FOR
        SELECT i.name, i.is_unique_constraint
        FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID(N'dbo.Usuarios') AND i.is_unique = 1
          AND i.name <> N'UX_Usuarios_Correo'
          AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id = i.object_id
               AND ic.index_id = i.index_id AND ic.key_ordinal > 0) = 1
          AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c
                      ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                        AND ic.key_ordinal = 1 AND c.name = N'Correo');
    OPEN indicesCorreo;
    FETCH NEXT FROM indicesCorreo INTO @nombreIndice, @esRestriccion;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @esRestriccion = 1
            SET @sentencia = N'ALTER TABLE dbo.Usuarios DROP CONSTRAINT ' + QUOTENAME(@nombreIndice);
        ELSE
            SET @sentencia = N'DROP INDEX ' + QUOTENAME(@nombreIndice) + N' ON dbo.Usuarios';
        EXEC sys.sp_executesql @sentencia;
        FETCH NEXT FROM indicesCorreo INTO @nombreIndice, @esRestriccion;
    END;
    CLOSE indicesCorreo;
    DEALLOCATE indicesCorreo;

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Usuarios') AND name = N'Correo' AND is_nullable = 0)
        ALTER TABLE dbo.Usuarios ALTER COLUMN Correo nvarchar(100) NULL;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Usuarios') AND name = N'PasswordHash' AND is_nullable = 0)
        ALTER TABLE dbo.Usuarios ALTER COLUMN PasswordHash nvarchar(255) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Usuarios') AND name = N'UX_Usuarios_Correo')
        CREATE UNIQUE INDEX UX_Usuarios_Correo ON dbo.Usuarios(Correo) WHERE Correo IS NOT NULL;

    IF COL_LENGTH(N'dbo.Usuarios', N'NombreUsuarioNormalizado') IS NULL
        EXEC(N'ALTER TABLE dbo.Usuarios ADD NombreUsuarioNormalizado AS
            UPPER(LTRIM(RTRIM(NombreUsuario)) COLLATE Latin1_General_100_CI_AS_SC) PERSISTED');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Usuarios') AND name = N'UX_Usuarios_NombreNormalizado')
        EXEC(N'CREATE UNIQUE INDEX UX_Usuarios_NombreNormalizado ON dbo.Usuarios(NombreUsuarioNormalizado)');

    IF OBJECT_ID(N'dbo.CredencialesDispositivo', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CredencialesDispositivo (
            CredencialDispositivoId uniqueidentifier NOT NULL CONSTRAINT PK_CredencialesDispositivo PRIMARY KEY,
            UsuarioId int NOT NULL,
            HashCredencial varchar(64) NOT NULL,
            FechaCreacionUtc datetime2 NOT NULL,
            ExpiraEnUtc datetime2 NOT NULL,
            RevocadaEnUtc datetime2 NULL,
            CONSTRAINT FK_CredencialesDispositivo_Usuarios FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios(UsuarioId),
            CONSTRAINT CK_CredencialesDispositivo_Fechas CHECK (ExpiraEnUtc > FechaCreacionUtc)
        );
        CREATE UNIQUE INDEX IX_CredencialesDispositivo_HashCredencial ON dbo.CredencialesDispositivo(HashCredencial);
        CREATE INDEX IX_CredencialesDispositivo_UsuarioId ON dbo.CredencialesDispositivo(UsuarioId);
    END;

    -- Ampliación sin truncamiento: admite el código interno de rondas posteriores (codigo-2, codigo-3...).
    IF COL_LENGTH(N'dbo.Partidas', N'CodigoSala') < 48
        ALTER TABLE dbo.Partidas ALTER COLUMN CodigoSala nvarchar(24) NULL;

    EXEC(N'CREATE OR ALTER PROCEDURE dbo.DeterminarGanador
    @PartidaId int
AS
BEGIN
    SET NOCOUNT ON;
    -- Solo consulta finales autoritativos escritos por RegistroPartidaLJ.
    -- Devuelve todos los ganadores, incluidos los empates. No finaliza partidas activas.
    SELECT r.UsuarioId AS GanadorId
    FROM dbo.Resultados r
    INNER JOIN dbo.Partidas p ON p.PartidaId = r.PartidaId
    WHERE r.PartidaId = @PartidaId AND p.Estado = N''Finalizada'' AND r.EsGanador = 1
    ORDER BY r.UsuarioId;
END;');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
