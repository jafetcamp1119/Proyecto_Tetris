USE [TetrisDB];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Solo agrega datos faltantes; no altera tablas ni reactiva tipos deshabilitados.
-- Codigo admite diez caracteres en el modelo actual; doble puntuación usa DOBLE.
IF OBJECT_ID(N'dbo.TiposPieza', N'U') IS NULL
    THROW 50003, 'No existe dbo.TiposPieza en TetrisDB.', 1;

IF EXISTS (SELECT 1 FROM dbo.TiposPieza
           WHERE Codigo IN (N'CONFUSION', N'ACELERADOR', N'BOMBA', N'DOBLE', N'BARREDORA')
             AND (EsEspecial = 0 OR EliminaColumna = 1))
    THROW 50013, 'Un codigo especial existente tiene otra configuracion. Revisar sin sobrescribir.', 1;

INSERT INTO dbo.TiposPieza (Nombre, Codigo, EsEspecial, EliminaColumna, Descripcion, Activa)
SELECT nuevo.Nombre, nuevo.Codigo, 1, 0, nuevo.Descripcion, nuevo.Activa
FROM (VALUES
    (N'Confusión', N'CONFUSION', N'Invierte izquierda y derecha del siguiente jugador activo durante el plazo del servidor.', 1),
    (N'Acelerador', N'ACELERADOR', N'Acelera la caída del siguiente jugador activo durante el plazo del servidor.', 1),
    (N'Bomba', N'BOMBA', N'Vacía un área de 3 por 3 centrada en la celda más baja e izquierda de la pieza.', 1),
    (N'Doble puntuación', N'DOBLE', N'Duplica únicamente los puntos por líneas de la jugada, conservando el nivel.', 1),
    (N'Barredora', N'BARREDORA', N'Limpia la mitad superior del tablero compartido sin desplazar filas ni puntuar la limpieza.', 1)
) AS nuevo(Nombre, Codigo, Descripcion, Activa)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.TiposPieza existente WITH (UPDLOCK, HOLDLOCK)
    WHERE existente.Codigo = nuevo.Codigo
       OR (nuevo.Codigo = N'DOBLE' AND existente.EsEspecial = 1 AND existente.EliminaColumna = 0
           AND REPLACE(REPLACE(UPPER(existente.Codigo), N'_', N''), N' ', N'') = N'DOBLEPUNTUACION')
);

COMMIT TRANSACTION;
GO
