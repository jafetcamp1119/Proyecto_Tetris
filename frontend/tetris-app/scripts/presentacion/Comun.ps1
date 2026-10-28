# Comparte utilidades de estado, comandos y procesos para el iniciador de presentación.
$ErrorActionPreference = 'Stop'

# Escribe al cambiar estado o mensaje y renueva la vigencia cuando el sistema está listo.
function EscribirEstadoPresentacion([string]$Estado, [string]$Mensaje, [DateTime]$AhoraUtc = [DateTime]::UtcNow) {
    $anterior = $script:UltimoEstadoPresentacion
    $cambio = -not $anterior -or $anterior.Archivo -ne $script:ArchivoEstado -or $anterior.Estado -ne $Estado -or $anterior.Mensaje -cne $Mensaje
    # Solo listo necesita renovar la vigencia del panel (vence a los 45 segundos).
    $renovar = $Estado -eq 'listo' -and $anterior -and ($AhoraUtc - $anterior.EscrituraUtc).TotalSeconds -ge 20
    if (-not $cambio -and -not $renovar) {
        return
    }
    GuardarEstadoPresentacion $Estado $Mensaje $AhoraUtc
    $script:UltimoEstadoPresentacion = @{
        Archivo = $script:ArchivoEstado;
        Estado = $Estado;
        Mensaje = $Mensaje;
        EscrituraUtc = $AhoraUtc
    }
    if ($cambio) {
        Write-Output $Mensaje
    }
}

# Escribe primero un archivo temporal y después actualiza el estado que lee el panel.
function GuardarEstadoPresentacion([string]$Estado, [string]$Mensaje, [DateTime]$AhoraUtc) {
    $datos = @{
        estado = $Estado;
        mensaje = $Mensaje;
        juego = 'https://proyecto-tetris-512517.web.app';
        actualizadoUtc = $AhoraUtc.ToString('o')
    }
    $temporal = $script:ArchivoEstado + '.tmp'
    [System.IO.File]::WriteAllText($temporal, ($datos | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))
    [System.IO.File]::Copy($temporal, $script:ArchivoEstado, $true)
    [System.IO.File]::Delete($temporal)
}

# Ejecuta el programa en la carpeta indicada y detiene el flujo si devuelve un error.
function EjecutarComando([string]$Programa, [string[]]$Argumentos, [string]$Directorio) {
    Push-Location $Directorio
    try {
        & $Programa @Argumentos
        if ($LASTEXITCODE -ne 0) {
            throw "Fallo el comando $Programa. No se continuara con la publicacion."
        }
    } finally {
        Pop-Location
    }
}

# Inicia un proceso sin ventana, captura su salida y lo registra para poder cerrarlo.
function IniciarProcesoOculto([string]$Programa, [string]$Argumentos, [string]$Nombre) {
    $salida = Join-Path $script:CarpetaTemporal ($Nombre + '.salida.log')
    $errores = Join-Path $script:CarpetaTemporal ($Nombre + '.errores.log')
    $proceso = Start-Process -FilePath $Programa -ArgumentList $Argumentos -WorkingDirectory $script:Frontend -WindowStyle Hidden -RedirectStandardOutput $salida -RedirectStandardError $errores -PassThru
    $registro = @{
        Proceso = $proceso;
        Salida = $salida;
        Errores = $errores
    }
    $script:Procesos.Add($registro)
    return $registro
}

# Detiene los procesos registrados por esta ejecución de la presentación.
function DetenerProcesosPresentacion {
    foreach ($registro in $script:Procesos) {
        if (-not $registro.Proceso.HasExited) {
            # El Job Object del iniciador termina tambien todos los descendientes.
            Stop-Process -Id $registro.Proceso.Id -ErrorAction SilentlyContinue
        }
    }
}

# Busca el ejecutable en la ruta indicada, PATH o ubicaciones conocidas de Windows.
function BuscarCloudflared {
    if ($env:TETRIS_CLOUDFLARED_RUTA -and (Test-Path -LiteralPath $env:TETRIS_CLOUDFLARED_RUTA)) {
        return $env:TETRIS_CLOUDFLARED_RUTA
    }
    $comando = Get-Command cloudflared.exe -ErrorAction SilentlyContinue
    if ($comando) {
        return $comando.Source
    }
    $candidatos = @("${env:ProgramFiles}\cloudflared\cloudflared.exe", "${env:ProgramFiles(x86)}\cloudflared\cloudflared.exe", 'C:\cloudflared\bin\cloudflared.exe')
    foreach ($candidato in $candidatos) {
        if (Test-Path -LiteralPath $candidato) {
            return $candidato
        }
    }
    $paquetes = Join-Path $env:LOCALAPPDATA 'Microsoft/WinGet/Packages'
    if (Test-Path $paquetes) {
        $archivo = Get-ChildItem $paquetes -Directory -Filter 'Cloudflare.cloudflared*' | Get-ChildItem -Recurse -File -Filter cloudflared.exe | Select-Object -First 1
        if ($archivo) {
            return $archivo.FullName
        }
    }
    throw 'No se encontro cloudflared.exe. Defini TETRIS_CLOUDFLARED_RUTA con la ruta de su ejecutable y reinicia Visual Studio.'
}

# Comprueba herramientas y recursos locales antes de iniciar conexiones o publicar.
function VerificarRequisitos {
    foreach ($nombre in @('dotnet.exe','npm.cmd','firebase.cmd')) {
        if (-not (Get-Command $nombre -ErrorAction SilentlyContinue)) {
            throw "Falta $nombre en PATH. Reinicia Visual Studio despues de instalarlo."
        }
    }
    $script:Cloudflared = BuscarCloudflared
    foreach ($archivo in @('scripts/ConfigurarPublicacion.ps1','scripts/IniciarBackendPublicado.ps1','firebase.json','presentacion/qr-tetris.svg')) {
        if (-not (Test-Path (Join-Path $script:Frontend $archivo))) {
            throw "Falta $archivo. Ejecuta npm run recursos:presentacion."
        }
    }
}
