$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'PublicacionFirebase.ps1')
$carpeta = Join-Path ([IO.Path]::GetTempPath()) ('TetrisConfirmacion-' + [Guid]::NewGuid().ToString('N'))
$anteriorLocal = $env:LOCALAPPDATA
$script:EntradaPrueba = ''
$script:Lecturas = 0
$script:Comandos = 0
function LeerLineaConfirmacion { $script:Lecturas++; return $script:EntradaPrueba }
function Write-Host { }
function ConsultarJson { return @{ urlBackend = 'https://anterior.trycloudflare.com' } }
function EjecutarComando([string]$Programa, [string[]]$Argumentos) {
    if ($Programa -ne 'firebase.cmd' -or ($Argumentos -join ' ') -ne 'deploy --only hosting --project proyecto-tetris-512517') { throw 'Comando inesperado.' }
    $script:Comandos++
}
try {
    $env:LOCALAPPDATA = $carpeta
    [IO.Directory]::CreateDirectory((Join-Path $carpeta 'ProyectoTetris/Presentacion')) | Out-Null
    $script:Frontend = $carpeta
    $script:ArchivoEstado = $null
    $script:HuellaActual = 'solo-prueba'
    $validas = @('PUBLICAR', 'publicar', 'PuBlIcAr', ' PUBLICAR ', "`tpublicar`t", ([char]0xA0 + 'PUBLICAR' + [char]0xA0))
    foreach ($entrada in $validas) {
        $script:EntradaPrueba = $entrada
        $script:Lecturas = 0
        $antes = $script:Comandos
        ConfirmarPublicacion 'https://nuevo.trycloudflare.com' | Out-Null
        if ($script:Lecturas -ne 1 -or $script:Comandos -ne $antes + 1) { throw 'La confirmacion valida no autoriza exactamente una vez.' }
    }
    $invalidas = @('', ' ', 'NO', 'PUBLICAR NO', 'PUB LICAR', ('PUBLICAR' + [char]0x200B), ([char]0xFEFF + 'PUBLICAR'))
    foreach ($entrada in $invalidas) {
        $script:EntradaPrueba = $entrada
        $script:Lecturas = 0
        $antes = $script:Comandos
        $rechazada = $false
        try { ConfirmarPublicacion 'https://nuevo.trycloudflare.com' | Out-Null }
        catch { $rechazada = $_.Exception.Message -like 'Publicacion cancelada*' }
        if (-not $rechazada -or $script:Lecturas -ne 1 -or $script:Comandos -ne $antes) { throw 'Una respuesta invalida autorizo o repitio la pregunta.' }
    }
    if (EsConfirmacionPublicacion $null) { throw 'Se autorizo un valor nulo.' }
    $script:EntradaPrueba = $null
    $antes = $script:Comandos
    $sinEntrada = $false
    try { ConfirmarPublicacion 'https://nuevo.trycloudflare.com' | Out-Null }
    catch { $sinEntrada = $_.Exception.Message -like 'Entrada de confirmacion no disponible*' }
    if (-not $sinEntrada -or $script:Comandos -ne $antes) { throw 'EOF no informado o autorizado.' }
    function LeerLineaConfirmacion { throw ([IO.IOException]::new('Prueba de lectura fallida')) }
    $falloEntrada = $false
    try { ConfirmarPublicacion 'https://nuevo.trycloudflare.com' | Out-Null }
    catch { $falloEntrada = $_.Exception.Message -like 'No se pudo leer la confirmacion*' }
    if (-not $falloEntrada -or $script:Comandos -ne $antes) { throw 'Error de entrada no informado o autorizado.' }
    Write-Output 'OK: mayusculas, minusculas, espacios y tabulaciones autorizan; otras respuestas, caracteres invisibles, nulo y errores nunca ejecutan deploy; una sola lectura.'
    Write-Output 'Todos los comandos Firebase fueron simulados. No se publico nada.'
} finally {
    $env:LOCALAPPDATA = $anteriorLocal
    foreach ($archivo in @('publicacion.hash')) {
        $ruta = Join-Path $carpeta ('ProyectoTetris/Presentacion/' + $archivo)
        if (Test-Path -LiteralPath $ruta) { Remove-Item -LiteralPath $ruta }
    }
    foreach ($ruta in @((Join-Path $carpeta 'ProyectoTetris/Presentacion'), (Join-Path $carpeta 'ProyectoTetris'), $carpeta)) {
        if (Test-Path -LiteralPath $ruta) { Remove-Item -LiteralPath $ruta }
    }
}
