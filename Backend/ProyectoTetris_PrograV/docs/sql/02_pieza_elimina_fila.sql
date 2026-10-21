USE [TetrisDB];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Agrega el tipo de pieza especial que elimina una fila horizontal.
-- No cambia la estructura de TiposPieza ni borra datos. Es seguro ejecutarlo más de una vez.
-- El servidor reconoce el efecto porque el código contiene "FILA".
IF OBJECT_ID(N'dbo.TiposPieza', N'U') IS NULL
    THROW 50002, 'No existe dbo.TiposPieza en TetrisDB.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.TiposPieza WHERE Codigo = N'ELIMINA_FILA')
    INSERT INTO dbo.TiposPieza (Nombre, Codigo, EsEspecial, EliminaColumna, Descripcion, Activa)
    VALUES (N'Elimina fila', N'ELIMINA_FILA', 1, 0,
            N'Al fijarse elimina la fila de su celda más baja; las filas superiores bajan.', 1);

COMMIT TRANSACTION;
GO
