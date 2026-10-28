# Coordina requisitos, SQL local, API, túnel, frontend y confirmación de publicación.
# Mantiene la vigilancia del sistema y cierra los procesos al terminar.
param([Parameter(Mandatory=$true)][string]$RaizRepo, [Parameter(Mandatory=$true)][string]$ArchivoEstado, [switch]$VerificarSolamente)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$script:RaizRepo = [System.IO.Path]::GetFullPath($RaizRepo)
$script:Frontend = Join-Path $script:RaizRepo 'frontend/tetris-app'
$script:ArchivoEstado = $ArchivoEstado
$script:CarpetaTemporal = Split-Path $ArchivoEstado -Parent
$script:Procesos = New-Object 'System.Collections.Generic.List[object]'
foreach ($modulo in @('Comun','ConfiguracionPrivada','RegistrosCloudflare','ConexionPublica','PublicacionFirebase')) {
    . (Join-Path $PSScriptRoot ($modulo + '.ps1'))
}
$fallo = $false
$configuraciones = @{}
try {
    EscribirEstadoPresentacion 'preparando' 'PREPARANDO CONEXION. Comprobando herramientas y recursos.'
    VerificarRequisitos
    if ($VerificarSolamente) {
        Write-Output 'Verificacion estatica terminada. No se consulto SQL, no se iniciaron tuneles y no se publico Firebase.'
        exit 0
    }
    $privada = ObtenerConfiguracionPrivada
    VerificarConexionSql $privada.Conexion
    EscribirEstadoPresentacion 'preparando' 'PREPARANDO CONEXION. Iniciando la API en Production.'
    IniciarBackendSeguro $privada
    EscribirEstadoPresentacion 'preparando' 'PREPARANDO CONEXION. Esperando HTTPS de Cloudflare.'
    $direccion = IniciarTunel
    foreach ($ruta in @('src/assets/configuracion-publica.json','www/assets/configuracion-publica.json')) {
        $archivo = Join-Path $script:Frontend $ruta
        if (Test-Path $archivo) {
            $configuraciones[$archivo] = [System.IO.File]::ReadAllText($archivo)
        }
    }
    EscribirEstadoPresentacion 'preparando' 'PREPARANDO CONEXION. Validando la compilacion y Firebase.'
    PrepararFrontend $direccion
    ConfirmarPublicacion $direccion
    while ($true) {
        $estado = ObtenerEstadoSistema $direccion
        EscribirEstadoPresentacion $estado.Estado $estado.Mensaje
        if ($script:Api.Proceso.HasExited -or $script:Tunel.Proceso.HasExited) {
            throw 'Se detuvo un proceso de la presentacion.'
        }
        Start-Sleep -Seconds 10
    }
} catch {
    $fallo = $true
    EscribirEstadoPresentacion 'error' ('SERVIDOR NO DISPONIBLE. ' + $_.Exception.Message)
    foreach ($archivo in $configuraciones.Keys) {
        [System.IO.File]::WriteAllText($archivo, $configuraciones[$archivo], (New-Object System.Text.UTF8Encoding($false)))
    }
} finally {
    DetenerProcesosPresentacion
}
if ($fallo) {
    exit 1
}
