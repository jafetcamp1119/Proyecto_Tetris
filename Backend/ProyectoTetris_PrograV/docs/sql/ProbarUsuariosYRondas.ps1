# Prepara comprobaciones de usuarios y rondas que utilizan SQL. No forma parte de las pruebas en memoria.
param(
    [string]$Servidor = 'JAFET\SQLEXPRESS'
)

$ErrorActionPreference = 'Stop'
$raizBackend = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$salidaPruebas = Join-Path $raizBackend 'Tetris.Pruebas\bin\Verificacion\net10.0'
$ejecutablePruebas = Join-Path $salidaPruebas 'Tetris.Pruebas.dll'
if (-not (Test-Path -LiteralPath $ejecutablePruebas)) {
    throw 'Compile primero con -p:OutputPath=bin/Verificacion/net10.0/.'
}
$estructura = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'TetrisEstructuraVerificada.json') -Raw | ConvertFrom-Json
$scriptCompleto = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'TetrisDB_Completa_Actualizada.txt') -Raw
$scriptActualizacion = Get-Content -LiteralPath (Join-Path $PSScriptRoot '04_usuarios_dispositivos_rondas.sql') -Raw
$conexionAnterior = $env:ConnectionStrings__TetrisDB
$antes = & sqlcmd -S $Servidor -E -C -b -h -1 -Q 'SET NOCOUNT ON; USE TetrisDB; SELECT (SELECT COUNT(*) FROM dbo.Usuarios),(SELECT COUNT(*) FROM dbo.Resultados),(SELECT COUNT(*) FROM dbo.Partidas),(SELECT COUNT(*) FROM dbo.EstadisticasJugador),(SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N''dbo.Usuarios''));'
if ($LASTEXITCODE -ne 0) {
    throw 'No se pudo leer el resumen inicial de TetrisDB.'
}

function EjecutarSql([string]$Texto, [string]$NombreArchivo) {
    $ruta = Join-Path $salidaPruebas $NombreArchivo
    Set-Content -LiteralPath $ruta -Value $Texto -Encoding utf8
    & sqlcmd -S $Servidor -E -C -b -f 65001 -i $ruta
    if ($LASTEXITCODE -ne 0) {
        throw "Falló el script $NombreArchivo."
    }
}

try {
    foreach ($tipo in @('Nueva', 'Migrada')) {
        $nombreBase = 'TetrisValidacion_' + $tipo + '_' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
        $creada = $false
        try {
            if ($tipo -eq 'Nueva') {
                EjecutarSql ($scriptCompleto.Replace('TetrisDB', $nombreBase)) 'crear_nueva.sql'
                $creada = $true
            }
            else {
                $separadorLotes = [Environment]::NewLine + 'GO' + [Environment]::NewLine
                EjecutarSql ("CREATE DATABASE [$nombreBase] COLLATE Modern_Spanish_CI_AS;" + $separadorLotes + "USE [$nombreBase];" + $separadorLotes + ($estructura.Lotes -join $separadorLotes)) 'crear_anterior.sql'
                $creada = $true
                EjecutarSql @"
USE [$nombreBase];
INSERT INTO dbo.Usuarios(NombreUsuario,Correo,PasswordHash) VALUES (N'LegadoSintetico',N'fixture@example.invalid',N'HASH_LEGADO_SINTETICO');
DECLARE @usuario int = SCOPE_IDENTITY();
INSERT INTO dbo.EstadisticasJugador(UsuarioId,TotalPuntos,LineasEliminadas,NivelMaximoAlcanzado) VALUES (@usuario,1234,10,2);
INSERT INTO dbo.Partidas(CreadorId,CodigoSala,TipoPartida,EsPrivada,CantidadMinimaJugadores,CantidadMaximaJugadores,Estado,FechaCreacion,FechaFin)
VALUES (@usuario,N'LEGADO',N'Individual',0,1,1,N'Finalizada',SYSUTCDATETIME(),SYSUTCDATETIME());
DECLARE @partida int = SCOPE_IDENTITY();
INSERT INTO dbo.Resultados(PartidaId,UsuarioId,Puntuacion,LineasEliminadas,NivelMaximo,PosicionFinal,EstadoFinal,EsGanador)
VALUES (@partida,@usuario,1234,10,2,1,N'Ganador',1);
INSERT INTO dbo.Usuarios(NombreUsuario,Correo,PasswordHash) VALUES (N' Duplicado',N'duplicado1@example.invalid',N'HASH_LEGADO_SINTETICO'),(N'Duplicado',N'duplicado2@example.invalid',N'HASH_LEGADO_SINTETICO');
"@ 'datos_legados.sql'
                # Base nueva con datos sintéticos: no hay datos personales que respaldar.
                $actualizacionTemporal = $scriptActualizacion.Replace('[TetrisDB]', "[$nombreBase]").Replace('@RespaldoVerificado bit = 0', '@RespaldoVerificado bit = 1')
                $rutaDuplicados = Join-Path $salidaPruebas 'duplicados.sql'
                Set-Content -LiteralPath $rutaDuplicados -Value $actualizacionTemporal -Encoding utf8
                $rechazo = & sqlcmd -S $Servidor -E -C -b -f 65001 -i $rutaDuplicados 2>&1
                if ($LASTEXITCODE -eq 0 -or ($rechazo -join '') -notmatch '50042') {
                    Write-Output ($rechazo -join [Environment]::NewLine)
                    throw 'La migración no detuvo los duplicados legados como se esperaba.'
                }
                Write-Output 'OK MIGRACION: duplicados detienen cambios sin borrar usuarios.'
                EjecutarSql @"
USE [$nombreBase];
IF COL_LENGTH(N'dbo.Usuarios',N'NombreUsuarioNormalizado') IS NOT NULL
    THROW 50100, 'El rechazo modificó el esquema.', 1;
DELETE FROM dbo.Usuarios WHERE Correo IN (N'duplicado1@example.invalid',N'duplicado2@example.invalid');
"@ 'limpiar_duplicados_sinteticos.sql'
                EjecutarSql $actualizacionTemporal 'migrar.sql'
                EjecutarSql $actualizacionTemporal 'repetir_migracion.sql'
                EjecutarSql @"
USE [$nombreBase];
IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios u JOIN dbo.EstadisticasJugador e ON e.UsuarioId=u.UsuarioId
    JOIN dbo.Resultados r ON r.UsuarioId=u.UsuarioId JOIN dbo.Partidas p ON p.PartidaId=r.PartidaId
    WHERE u.NombreUsuario=N'LegadoSintetico' AND u.Correo=N'fixture@example.invalid' AND u.PasswordHash=N'HASH_LEGADO_SINTETICO'
      AND e.TotalPuntos=1234 AND r.Puntuacion=1234 AND p.CodigoSala=N'LEGADO')
    THROW 50101, 'Se alteró el histórico sintético.', 1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=0 AND is_not_trusted=0) < 30
    THROW 50102, 'Revisar relaciones.', 1;
PRINT 'OK MIGRACION: usuarios, datos legados, estadísticas, partidas, resultados y relaciones conservados; script repetible.';
"@ 'verificar_historico.sql'
                # Catálogos públicos de la misma estructura verificada, sin exportar usuarios.
                $soloCatalogos = $scriptCompleto.Substring($scriptCompleto.IndexOf('SET IDENTITY_INSERT dbo.TiposPieza ON;'))
                EjecutarSql ("USE [$nombreBase];`r`nGO`r`n" + $soloCatalogos) 'catalogos.sql'
            }

            $env:ConnectionStrings__TetrisDB = "Server=$Servidor;Database=$nombreBase;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=5"
            & dotnet $ejecutablePruebas '--identidad-sql'
            if ($LASTEXITCODE -ne 0) {
                throw "Fallaron las pruebas de identidad en $tipo."
            }
            & dotnet $ejecutablePruebas '--integracion-sql'
            if ($LASTEXITCODE -ne 0) {
                throw "Fallaron las pruebas de juego en $tipo."
            }
            Write-Output "OK: base $tipo verificada."
        }
        finally {
            # Solo se elimina la base temporal creada por esta ejecución.
            if ($creada -and $nombreBase -match '^TetrisValidacion_(Nueva|Migrada)_[0-9a-f]{12}$') {
                & sqlcmd -S $Servidor -E -C -b -Q "USE master; ALTER DATABASE [$nombreBase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$nombreBase];"
                if ($LASTEXITCODE -ne 0) {
                    Write-Warning "Revisar limpieza de $nombreBase."
                }
            }
        }
    }
}
finally {
    $env:ConnectionStrings__TetrisDB = $conexionAnterior
    $despues = & sqlcmd -S $Servidor -E -C -b -h -1 -Q 'SET NOCOUNT ON; USE TetrisDB; SELECT (SELECT COUNT(*) FROM dbo.Usuarios),(SELECT COUNT(*) FROM dbo.Resultados),(SELECT COUNT(*) FROM dbo.Partidas),(SELECT COUNT(*) FROM dbo.EstadisticasJugador),(SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N''dbo.Usuarios''));'
    if (($antes -join '') -ne ($despues -join '')) {
        throw 'El resumen de TetrisDB cambió durante las pruebas; revisar actividad externa.'
    }
    Write-Output 'OK: TetrisDB conserva sus cantidades y estructura de usuarios; no recibió migraciones ni datos de prueba.'
}
