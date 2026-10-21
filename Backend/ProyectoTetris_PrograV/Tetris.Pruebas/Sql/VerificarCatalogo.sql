-- Ejecutar con sqlcmd desde la carpeta de la solución. Todos los cambios se revierten.
USE [TetrisDB];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

SELECT * INTO #CatalogoAntes FROM dbo.TiposPieza;
SELECT name, definition, is_disabled, is_not_trusted INTO #RestriccionesAntes
FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Partidas');
SELECT name, definition INTO #DefaultsAntes
FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Partidas');
GO
:r "docs/sql/01_modalidades.sql"
:r "docs/sql/02_pieza_elimina_fila.sql"
:r "docs/sql/03_efectos_especiales.sql"

SELECT * INTO #CatalogoPrimeraEjecucion FROM dbo.TiposPieza;
GO
:r "docs/sql/01_modalidades.sql"
:r "docs/sql/02_pieza_elimina_fila.sql"
:r "docs/sql/03_efectos_especiales.sql"

IF EXISTS (SELECT * FROM #CatalogoPrimeraEjecucion EXCEPT SELECT * FROM dbo.TiposPieza)
   OR EXISTS (SELECT * FROM dbo.TiposPieza EXCEPT SELECT * FROM #CatalogoPrimeraEjecucion)
    THROW 50101, 'La segunda ejecucion cambio el catalogo o agrego duplicados.', 1;

IF EXISTS (SELECT * FROM #CatalogoAntes EXCEPT SELECT * FROM dbo.TiposPieza)
    THROW 50102, 'Se sobrescribieron registros existentes.', 1;

-- En esta base 01 ya está aplicado: tampoco debe alterar sus restricciones y defaults.
IF EXISTS (SELECT * FROM #RestriccionesAntes EXCEPT
           SELECT name, definition, is_disabled, is_not_trusted FROM sys.check_constraints
           WHERE parent_object_id = OBJECT_ID(N'dbo.Partidas'))
   OR EXISTS (SELECT * FROM #DefaultsAntes EXCEPT
              SELECT name, definition FROM sys.default_constraints
              WHERE parent_object_id = OBJECT_ID(N'dbo.Partidas'))
    THROW 50103, 'Se altero una configuracion de modalidades previamente aplicada.', 1;

SELECT CASE
    WHEN EliminaColumna = 1 THEN N'COLUMNA'
    WHEN Codigo LIKE N'%FILA%' THEN N'FILA'
    WHEN REPLACE(REPLACE(UPPER(Codigo), N'_', N''), N' ', N'') IN (N'DOBLE', N'DOBLEPUNTUACION') THEN N'DOBLE'
    ELSE UPPER(Codigo)
END AS Efecto
INTO #Efectos FROM dbo.TiposPieza WHERE EsEspecial = 1;

IF EXISTS (SELECT requerido.Codigo
           FROM (VALUES (N'COLUMNA'), (N'FILA'), (N'CONFUSION'), (N'ACELERADOR'),
                        (N'BOMBA'), (N'DOBLE'), (N'BARREDORA')) requerido(Codigo)
           WHERE NOT EXISTS (SELECT 1 FROM #Efectos WHERE Efecto = requerido.Codigo))
    THROW 50104, 'Falta alguno de los siete efectos.', 1;

IF EXISTS (SELECT Efecto FROM #Efectos GROUP BY Efecto HAVING COUNT(*) > 1)
    THROW 50105, 'Existen efectos duplicados.', 1;

IF @@TRANCOUNT <> 1
    THROW 50106, 'Un script no conservo la transaccion de verificacion.', 1;

SELECT Codigo, Nombre, EsEspecial, EliminaColumna, Activa FROM dbo.TiposPieza ORDER BY TipoPiezaId;
PRINT 'OK SQL CATALOGO: siete efectos, scripts idempotentes y configuraciones anteriores conservadas.';
ROLLBACK TRANSACTION;
PRINT 'ROLLBACK CATALOGO correcto: no se conservaron las inserciones de prueba.';
GO
