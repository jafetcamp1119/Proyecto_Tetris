$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Comun.ps1')
$script:ArchivoEstado = [IO.Path]::GetTempFileName()
$script:UltimoEstadoPresentacion = $null
$hora = [DateTime]::UtcNow
try {
    $primero = @(EscribirEstadoPresentacion 'listo' 'SERVIDOR EN LINEA' $hora)
    $contenido = [IO.File]::ReadAllText($script:ArchivoEstado)
    $repetido = @(EscribirEstadoPresentacion 'listo' 'SERVIDOR EN LINEA' $hora.AddSeconds(10))
    if ($primero.Count -ne 1 -or $repetido.Count -ne 0) { throw 'La consola repite un estado sin cambios.' }
    if ([IO.File]::ReadAllText($script:ArchivoEstado) -ne $contenido) { throw 'Se reescribio el estado antes de necesitar renovar su vigencia.' }
    $renovado = @(EscribirEstadoPresentacion 'listo' 'SERVIDOR EN LINEA' $hora.AddSeconds(20))
    $datos = [IO.File]::ReadAllText($script:ArchivoEstado) | ConvertFrom-Json
    $fechaRenovada = ([DateTime]$datos.actualizadoUtc).ToUniversalTime()
    if ($renovado.Count -ne 0 -or [Math]::Abs(($fechaRenovada - $hora.AddSeconds(20)).TotalMilliseconds) -gt 1) { throw 'No se renovo silenciosamente la vigencia del QR.' }
    $caida = @(EscribirEstadoPresentacion 'error' 'SIN CONEXION' $hora.AddSeconds(21))
    $contenido = [IO.File]::ReadAllText($script:ArchivoEstado)
    $errorRepetido = @(EscribirEstadoPresentacion 'error' 'SIN CONEXION' $hora.AddSeconds(80))
    if ($caida.Count -ne 1 -or $errorRepetido.Count -ne 0 -or [IO.File]::ReadAllText($script:ArchivoEstado) -ne $contenido) { throw 'Se repitio un error sin cambios.' }
    $recuperado = @(EscribirEstadoPresentacion 'listo' 'SERVIDOR EN LINEA' $hora.AddSeconds(81))
    if ($recuperado.Count -ne 1) { throw 'Se oculto la recuperacion del servidor.' }
    $detalle = @(EscribirEstadoPresentacion 'listo' 'SERVIDOR EN LINEA. Nueva comprobacion' $hora.AddSeconds(82))
    if ($detalle.Count -ne 1) { throw 'Se oculto un cambio real del mensaje.' }
    . (Join-Path $PSScriptRoot 'ConexionPublica.ps1')
    $script:Api = @{ Proceso = @{ HasExited = $false } }
    $script:Tunel = @{ Proceso = @{ HasExited = $false } }
    $script:Comprobaciones = 0
    $script:ConsultasFirebase = 0
    $script:UltimoEstadoPresentacion = $null
    function VerificarApiPublica { $script:Comprobaciones++ }
    function ConsultarJson {
        $script:ConsultasFirebase++
        return @{ urlBackend = 'https://prueba.trycloudflare.com' }
    }
    $mensajes = @(
        for ($indice = 0; $indice -lt 6; $indice++) {
            $estado = ObtenerEstadoSistema 'https://prueba.trycloudflare.com'
            EscribirEstadoPresentacion $estado.Estado $estado.Mensaje $hora.AddSeconds(100 + 10 * $indice)
        }
    )
    if ($script:Comprobaciones -ne 6 -or $script:ConsultasFirebase -ne 6 -or $mensajes.Count -ne 1) { throw 'Se omitieron comprobaciones o se repitio el estado estable.' }
    Write-Output 'OK: estados identicos sin mensajes ni escrituras redundantes; vigencia silenciosa a 20s; caida y recuperacion inmediatas.'
    Write-Output 'OK: seis comprobaciones periodicas conservadas; un unico mensaje de disponibilidad.'
} finally {
    Remove-Item -LiteralPath $script:ArchivoEstado -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath ($script:ArchivoEstado + '.tmp') -ErrorAction SilentlyContinue
}
