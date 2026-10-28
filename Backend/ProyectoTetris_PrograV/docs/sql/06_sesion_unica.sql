-- Agrega el identificador de sesión activa y la huella pendiente de recuperación.
-- El índice filtrado evita repetir una huella pendiente sin impedir varios valores null.
-- Aplicar manualmente SOLO con autorizacion y copia de seguridad. No modifica cuentas existentes.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF DB_NAME() <> 'TetrisDB' THROW 50001, 'Selecciona TetrisDB explicitamente antes de aplicar este script.', 1;
IF COL_LENGTH('dbo.Usuarios', 'SesionActivaId') IS NULL
    ALTER TABLE dbo.Usuarios ADD SesionActivaId uniqueidentifier NULL;
IF COL_LENGTH('dbo.Usuarios', 'HashRecuperacionPendiente') IS NULL
    ALTER TABLE dbo.Usuarios ADD HashRecuperacionPendiente varchar(64) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Usuarios_RecuperacionPendiente' AND object_id = OBJECT_ID('dbo.Usuarios'))
    -- Compilar el indice despues del ALTER, cuando la columna ya existe. Texto constante, sin entradas externas.
    EXEC(N'CREATE UNIQUE INDEX UX_Usuarios_RecuperacionPendiente ON dbo.Usuarios(HashRecuperacionPendiente) WHERE HashRecuperacionPendiente IS NOT NULL;');
COMMIT;
