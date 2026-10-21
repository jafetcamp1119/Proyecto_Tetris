USE [TetrisDB];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Se amplía la restricción existente para admitir una partida individual.
-- La LJ sigue exigiendo al menos cuatro participantes en los modos multijugador.
-- No se eliminan tablas, filas ni relaciones.
IF OBJECT_ID(N'dbo.Partidas', N'U') IS NULL
    THROW 50001, 'No existe dbo.Partidas en TetrisDB.', 1;

IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = N'CK_Partidas_Jugadores' AND parent_object_id = OBJECT_ID(N'dbo.Partidas'))
    ALTER TABLE dbo.Partidas DROP CONSTRAINT CK_Partidas_Jugadores;

ALTER TABLE dbo.Partidas WITH CHECK ADD CONSTRAINT CK_Partidas_Jugadores
    CHECK (CantidadMinimaJugadores >= 1 AND CantidadMaximaJugadores >= CantidadMinimaJugadores);

DECLARE @restriccion sysname;
SELECT @restriccion = d.name
FROM sys.default_constraints d
JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
WHERE d.parent_object_id = OBJECT_ID(N'dbo.Partidas') AND c.name = N'CantidadMinimaJugadores';

IF @restriccion IS NOT NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'ALTER TABLE dbo.Partidas DROP CONSTRAINT ' + QUOTENAME(@restriccion);
    EXEC sys.sp_executesql @sql;
END;

ALTER TABLE dbo.Partidas ADD CONSTRAINT DF_Partidas_MinimoJugadores
    DEFAULT (4) FOR CantidadMinimaJugadores;

COMMIT TRANSACTION;
GO


