# Comprueba la conexión SQL local mediante el código de verificación del proyecto.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ConfiguracionPrivada.ps1')
$frontend = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$repo = Split-Path (Split-Path $frontend -Parent) -Parent
$cadena = $env:ConnectionStrings__TetrisDB
if (-not $cadena) {
    $datos = Get-Content (Join-Path $repo 'Backend/ProyectoTetris_PrograV/ProyectoTetris_PrograV/appsettings.Development.json') -Raw | ConvertFrom-Json
    $cadena = $datos.ConnectionStrings.TetrisDB
}
VerificarConexionSql $cadena
Write-Output 'OK: SQL Server local responde SELECT 1 en la base configurada. No se modificaron datos.'
