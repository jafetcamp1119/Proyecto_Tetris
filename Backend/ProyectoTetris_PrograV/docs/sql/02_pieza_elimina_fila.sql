USE [TetrisDB];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Agrega el tipo de pieza especial que elimina una fila horizontal.
-- No cambia la estructura de TiposPieza ni borra datos. Es seguro ejecutarlo más de una vez.
-- Codigo tiene longitud 10: FILA es compatible; ELIMINA_FILA (12) no lo es.
-- También conserva cualquier alias existente reconocido por el catálogo.
IF OBJECT_ID(N'dbo.TiposPieza', N'U') IS NULL
    THROW 50002, 'No existe dbo.TiposPieza en TetrisDB.', 1;

IF EXISTS (SELECT 1 FROM dbo.TiposPieza WHERE Codigo = N'FILA' AND (EsEspecial = 0 OR EliminaColumna = 1))
    THROW 50012, 'El codigo FILA existe con otra configuracion. Revisar sin sobrescribir.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.TiposPieza WITH (UPDLOCK, HOLDLOCK)
               WHERE EsEspecial = 1 AND EliminaColumna = 0 AND Codigo LIKE N'%FILA%')
    INSERT INTO dbo.TiposPieza (Nombre, Codigo, EsEspecial, EliminaColumna, Descripcion, Activa)
    VALUES (N'Elimina fila', N'FILA', 1, 0,
            N'Al fijarse elimina la fila de su celda más baja; las filas superiores bajan.', 1);

COMMIT TRANSACTION;
GO
