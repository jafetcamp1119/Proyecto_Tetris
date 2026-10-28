$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Comun.ps1')
. (Join-Path $PSScriptRoot 'RegistrosCloudflare.ps1')
$script:Api = $null
$procesosPrueba = New-Object 'System.Collections.Generic.List[object]'
$carpeta = Join-Path ([System.IO.Path]::GetTempPath()) ('TetrisRegistros-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($carpeta) | Out-Null
$archivos = New-Object 'System.Collections.Generic.List[string]'
$script:Procesos = New-Object 'System.Collections.Generic.List[object]'
$script:CarpetaTemporal = $carpeta
$script:Frontend = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

function CrearRegistroDePrueba([string]$Modo) {
    $ruta = Join-Path $carpeta ($Modo + '.log')
    foreach ($sufijo in @('', '.abierto', '.liberar')) { $archivos.Add($ruta + $sufijo) }
    $scriptEscritor = Join-Path $PSScriptRoot 'pruebas/EscritorRegistro.ps1'
    $argumentos = '-NoProfile -ExecutionPolicy Bypass -File "' + $scriptEscritor + '" -Ruta "' + $ruta + '" -Modo ' + $Modo
    $proceso = Start-Process powershell.exe -ArgumentList $argumentos -WindowStyle Hidden -PassThru
    $procesosPrueba.Add($proceso)
    return @{ Proceso = $proceso; Salida = $ruta; Errores = (Join-Path $carpeta 'ausente.log') }
}

function EsperarEscritor($Registro) {
    $limite = [DateTime]::UtcNow.AddSeconds(5)
    while (-not (Test-Path ($Registro.Salida + '.abierto'))) {
        if ($Registro.Proceso.HasExited -or [DateTime]::UtcNow -gt $limite) { throw 'El escritor de prueba no abrio el archivo.' }
        Start-Sleep -Milliseconds 25
    }
}

try {
    $compartido = CrearRegistroDePrueba 'Compartido'
    EsperarEscritor $compartido
    $fallaba = $false
    try { [System.IO.File]::ReadAllText($compartido.Salida) | Out-Null } catch [System.IO.IOException] { $fallaba = $true }
    if (-not $fallaba) { throw 'No se reprodujo el bloqueo de ReadAllText con otro proceso escribiendo.' }
    $direccion = EsperarDireccionTunel $compartido 5 20
    if ($direccion -ne 'https://prueba-concurrente.trycloudflare.com' -or $compartido.Proceso.HasExited) { throw 'No se leyo la URL completa mientras el escritor permanecia abierto.' }
    [System.IO.File]::WriteAllText(($compartido.Salida + '.liberar'), 'salir')
    Write-Output 'OK: ReadAllText reproduce el fallo; lectura compartida detecta URL escrita por partes con otro proceso todavia abierto.'

    $exclusivo = CrearRegistroDePrueba 'Exclusivo'
    EsperarEscritor $exclusivo
    if ((LeerRegistroCompartido $exclusivo.Salida 2 10) -ne '') { throw 'No se respeto el bloqueo exclusivo.' }
    if ((EsperarDireccionTunel $exclusivo 5 20) -ne 'https://prueba-concurrente.trycloudflare.com') { throw 'No se recupero la lectura al liberar el bloqueo exclusivo.' }
    [System.IO.File]::WriteAllText(($exclusivo.Salida + '.liberar'), 'salir')
    Write-Output 'OK: bloqueo exclusivo reintentado sin error hasta que el escritor cierra el registro.'

    $tardio = CrearRegistroDePrueba 'CrearTarde'
    if ((LeerRegistroCompartido $tardio.Salida 1 0) -ne '') { throw 'El registro ausente no se manejo correctamente.' }
    if ((EsperarDireccionTunel $tardio 5 20) -ne 'https://prueba-concurrente.trycloudflare.com') { throw 'No se detecto la creacion tardia del registro.' }
    [System.IO.File]::WriteAllText(($tardio.Salida + '.liberar'), 'salir')
    Write-Output 'OK: archivo creado despues de empezar la espera.'

    $emisor = Join-Path $PSScriptRoot 'pruebas/EmisorSalida.ps1'
    $argumentos = '-NoProfile -ExecutionPolicy Bypass -File "' + $emisor + '"'
    $redirigido = IniciarProcesoOculto 'powershell.exe' $argumentos 'redireccion'
    $procesosPrueba.Add($redirigido.Proceso)
    $archivos.Add($redirigido.Salida)
    $archivos.Add($redirigido.Errores)
    if ((EsperarDireccionTunel $redirigido 5 20) -ne 'https://salida-redirigida.trycloudflare.com' -or $redirigido.Proceso.HasExited) {
        throw 'No se pudo leer stderr usando la misma redireccion real del iniciador.'
    }
    Write-Output 'OK: captura real Start-Process/RedirectStandardError del iniciador, con el proceso emisor todavia activo.'

    $grande = Join-Path $carpeta 'grande.log'
    $archivos.Add($grande)
    [System.IO.File]::WriteAllText($grande, (('x' * 80000) + "
https://registro-grande.trycloudflare.com"))
    $lectura = LeerRegistroCompartido $grande
    if ($lectura.Length -gt 65536 -or $lectura -notlike '*https://registro-grande.trycloudflare.com') { throw 'La lectura no se limito a la instantanea final del archivo.' }
    $exclusivoDespues = [System.IO.File]::Open($grande, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    $exclusivoDespues.Dispose()
    Write-Output 'OK: lectura limitada a 64 KiB y handle liberado al terminar.'

    $falso = @{ Proceso = @{ HasExited = $false }; Salida = (Join-Path $carpeta 'ausente.log'); Errores = (Join-Path $carpeta 'ausente.log') }
    $reloj = [System.Diagnostics.Stopwatch]::StartNew()
    $vencio = $false
    try { EsperarDireccionTunel $falso 1 10 | Out-Null } catch { $vencio = $_.Exception.Message -like '*plazo previsto*' }
    if (-not $vencio -or $reloj.Elapsed.TotalSeconds -gt 3) { throw 'No se aplico una espera acotada.' }
    $falso.Proceso.HasExited = $true
    $terminado = $false
    try { EsperarDireccionTunel $falso 1 10 | Out-Null } catch { $terminado = $_.Exception.Message -like '*termino sin establecer*' }
    if (-not $terminado) { throw 'No se detecto el cierre anticipado del proceso.' }
    Write-Output 'OK: timeout acotado y proceso terminado, sin declarar disponibilidad.'
} finally {
    foreach ($proceso in $procesosPrueba) {
        if (-not $proceso.HasExited) { Stop-Process -Id $proceso.Id -ErrorAction SilentlyContinue }
        $proceso.WaitForExit(3000) | Out-Null
        $proceso.Dispose()
    }
    foreach ($archivo in $archivos) {
        if (Test-Path -LiteralPath $archivo) { Remove-Item -LiteralPath $archivo }
    }
    if ([System.IO.Path]::GetFullPath($carpeta).StartsWith([System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $carpeta
    }
}
