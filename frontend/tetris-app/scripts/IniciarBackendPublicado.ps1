# Comprueba las variables privadas requeridas e inicia la API en Production por loopback.
﻿param([switch]$SinCompilar)
$ErrorActionPreference = 'Stop'
if (-not $env:Jwt__Clave -or [System.Text.Encoding]::UTF8.GetByteCount($env:Jwt__Clave) -lt 32) {
    throw 'Definí Jwt__Clave (mínimo 32 bytes) en el entorno de esta terminal.'
}
if (-not $env:ConnectionStrings__TetrisDB) {
    throw 'Definí ConnectionStrings__TetrisDB en el entorno de esta terminal.'
}
$proyecto = Join-Path $PSScriptRoot '../../../Backend/ProyectoTetris_PrograV/ProyectoTetris_PrograV/ProyectoTetris_PrograV.csproj'
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:DOTNET_ENVIRONMENT = 'Production'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5186'
$argumentos = @('run', '--project', $proyecto, '--no-launch-profile')
if ($SinCompilar) {
    $argumentos += '--no-build'
}
& dotnet @argumentos
exit $LASTEXITCODE
