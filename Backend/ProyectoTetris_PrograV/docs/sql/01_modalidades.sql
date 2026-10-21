USE [TetrisDB];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- No se toca una restricción que ya admite individual ni un valor predeterminado existente.
-- Antes de un cambio de estructura, realizar respaldo y revisar las dependencias en SSMS.
IF OBJECT_ID(N'dbo.Partidas', N'U') IS NULL
    THROW 50001, 'No existe dbo.Partidas en TetrisDB.', 1;

IF EXISTS (SELECT 1 FROM dbo.Partidas WHERE CantidadMinimaJugadores < 1
           OR CantidadMaximaJugadores < CantidadMinimaJugadores)
    THROW 50011, 'Hay cantidades de jugadores incompatibles. No se modificaron los datos.', 1;

DECLARE @definicion nvarchar(max);
SELECT @definicion = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(definition,
    N' ', N''), N'[', N''), N']', N''), N'(', N''), N')', N'')
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.Partidas') AND name = N'CK_Partidas_Jugadores';

IF @definicion IS NOT NULL
   AND @definicion NOT IN (
       N'CantidadMinimaJugadores>=1ANDCantidadMaximaJugadores>=CantidadMinimaJugadores',
       N'CantidadMinimaJugadores>=4ANDCantidadMaximaJugadores>=CantidadMinimaJugadores')
    THROW 50021, 'Restriccion personalizada: revisar dependencias antes de modificarla.', 1;

IF @definicion = N'CantidadMinimaJugadores>=4ANDCantidadMaximaJugadores>=CantidadMinimaJugadores'
    ALTER TABLE dbo.Partidas DROP CONSTRAINT CK_Partidas_Jugadores;

IF @definicion IS NULL
   OR @definicion = N'CantidadMinimaJugadores>=4ANDCantidadMaximaJugadores>=CantidadMinimaJugadores'
    ALTER TABLE dbo.Partidas WITH CHECK ADD CONSTRAINT CK_Partidas_Jugadores
        CHECK (CantidadMinimaJugadores >= 1 AND CantidadMaximaJugadores >= CantidadMinimaJugadores);

-- Si existe, conserva su nombre, valor y configuración de confianza.
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints d
               JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
               WHERE d.parent_object_id = OBJECT_ID(N'dbo.Partidas') AND c.name = N'CantidadMinimaJugadores')
    ALTER TABLE dbo.Partidas ADD CONSTRAINT DF_Partidas_MinimoJugadores
        DEFAULT (4) FOR CantidadMinimaJugadores;

COMMIT TRANSACTION;
GO


