# Calcula la huella del frontend y decide si debe compilar o solicitar otra publicación.
# El deploy requiere la confirmación de consola cuando el sitio necesita actualizarse.
. (Join-Path $PSScriptRoot 'ConfirmacionPublicacion.ps1')

# Calcula una huella de fuentes y dependencias para saber si cambió el contenido del frontend.
function ObtenerHuellaFrontend {
    $archivos = @(Get-ChildItem (Join-Path $script:Frontend 'src') -Recurse -File | Where-Object {
            $_.Name -ne 'configuracion-publica.json'
        })
    foreach ($ruta in @('angular.json','package.json','package-lock.json')) {
        $archivos += Get-Item (Join-Path $script:Frontend $ruta)
    }
    $lineas = foreach ($archivo in ($archivos | Sort-Object FullName)) {
        $hash = (Get-FileHash $archivo.FullName -Algorithm SHA256).Hash
        $archivo.FullName.Substring($script:Frontend.Length) + ':' + $hash
    }
    $algoritmo = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [Convert]::ToBase64String($algoritmo.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lineas -join "`n"))))
    }
    finally {
        $algoritmo.Dispose()
    }
}

# Actualiza la URL local, compila cuando hace falta y verifica los archivos de www.
function PrepararFrontend([string]$Direccion) {
    EjecutarComando 'npm.cmd' @('run','red:publica','--','-UrlBackend',$Direccion) $script:Frontend
    $archivoHuella = Join-Path $env:LOCALAPPDATA 'ProyectoTetris/Presentacion/compilacion.hash'
    $huella = ObtenerHuellaFrontend
    $script:HuellaActual = $huella
    $compilar = -not (Test-Path (Join-Path $script:Frontend 'www/index.html'))
    if (-not (Test-Path $archivoHuella)) {
        $compilar = $true
    }
    elseif ([System.IO.File]::ReadAllText($archivoHuella) -ne $huella) {
        $compilar = $true
    }
    if ($compilar) {
        EjecutarComando 'npm.cmd' @('run','build') $script:Frontend
    }
    try {
        EjecutarComando 'npm.cmd' @('run','verificar:publicacion') $script:Frontend
    } catch {
        if ($compilar) {
            throw
        }
        EjecutarComando 'npm.cmd' @('run','build') $script:Frontend
        EjecutarComando 'npm.cmd' @('run','verificar:publicacion') $script:Frontend
    }
    [System.IO.File]::WriteAllText($archivoHuella, $huella)
}

# Compara el destino y la huella publicados; si cambiaron, solicita PUBLICAR antes del deploy.
function ConfirmarPublicacion([string]$Direccion) {
    $archivoPublicacion = Join-Path $env:LOCALAPPDATA 'ProyectoTetris/Presentacion/publicacion.hash'
    try {
        $actual = ConsultarJson 'https://proyecto-tetris-512517.web.app/assets/configuracion-publica.json'
        if ($actual.urlBackend.TrimEnd('/') -eq $Direccion -and (Test-Path $archivoPublicacion) -and [System.IO.File]::ReadAllText($archivoPublicacion) -eq $script:HuellaActual) {
            return
        }
    } catch { }
    Write-Output 'Firebase necesita una actualizacion para usar el tunel de esta sesion.'
    Write-Output 'Se publicara el contenido validado de www en proyecto-tetris-512517 usando tu sesion de Firebase CLI.'
    if ($script:ArchivoEstado) {
        EscribirEstadoPresentacion 'preparando' 'PREPARANDO CONEXION. Confirma la publicacion en la consola de Visual Studio.'
    }
    SolicitarConfirmacionPublicacion
    EjecutarComando 'firebase.cmd' @('deploy','--only','hosting','--project','proyecto-tetris-512517') $script:Frontend
    [System.IO.File]::WriteAllText($archivoPublicacion, $script:HuellaActual)
}
