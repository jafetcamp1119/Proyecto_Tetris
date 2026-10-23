-- Actualización aditiva e idempotente. Ejecutar después de verificar un respaldo.
-- No borra, fusiona ni reemplaza usuarios, partidas ni estadísticas.
USE TetrisDB;
GO
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.Usuarios', 'GoogleSub') IS NULL
    ALTER TABLE dbo.Usuarios ADD GoogleSub varchar(255) NULL;
IF COL_LENGTH('dbo.Usuarios', 'AvatarGoogle') IS NULL
    ALTER TABLE dbo.Usuarios ADD AvatarGoogle nvarchar(2048) NULL;
IF COL_LENGTH('dbo.Usuarios', 'HashRecuperacion') IS NULL
    ALTER TABLE dbo.Usuarios ADD HashRecuperacion varchar(64) NULL;
IF COL_LENGTH('dbo.Usuarios', 'RecuperacionExpiraEnUtc') IS NULL
    ALTER TABLE dbo.Usuarios ADD RecuperacionExpiraEnUtc datetime2 NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Usuarios') AND name='IX_Usuarios_GoogleSub')
    EXEC('CREATE UNIQUE INDEX IX_Usuarios_GoogleSub ON dbo.Usuarios(GoogleSub) WHERE GoogleSub IS NOT NULL');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Usuarios') AND name='IX_Usuarios_HashRecuperacion')
    EXEC('CREATE UNIQUE INDEX IX_Usuarios_HashRecuperacion ON dbo.Usuarios(HashRecuperacion) WHERE HashRecuperacion IS NOT NULL');
COMMIT;
GO
