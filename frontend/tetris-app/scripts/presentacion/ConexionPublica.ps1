# Espera la salud local y pública, inicia el túnel y comprueba disponibilidad para el panel.
. (Join-Path $PSScriptRoot 'ComprobacionesHttp.ps1')
. (Join-Path $PSScriptRoot 'DisponibilidadTunel.ps1')

# Solicita JSON usando la comprobación común de HTTP.
function ConsultarJson([string]$Url, [string]$Metodo = 'GET') {
    return (SolicitarJsonComprobado $Url $Metodo)
}

# Reintenta la salud dentro del plazo y se detiene si el fallo no admite otro intento.
function EsperarApi([string]$Direccion, [int]$Intentos, [switch]$Publica) {
    $reloj = [System.Diagnostics.Stopwatch]::StartNew()
    $etapa = 'API local'
    if ($Publica) {
        $etapa = 'API publica HTTPS'
    }
    $ultimoError = $null
    $numero = 0
    while ($reloj.Elapsed.TotalSeconds -lt $Intentos) {
        ComprobarProcesosConexion -Publica:$Publica
        $numero++
        $restante = [Math]::Max(1, [Math]::Floor($Intentos - $reloj.Elapsed.TotalSeconds))
        try {
            ComprobarSaludApi $Direccion -TiempoSegundos ([Math]::Min(10, $restante)) -Publica:$Publica
            ComprobarProcesosConexion -Publica:$Publica
            EscribirDiagnosticoConexion ($etapa + ' verificada: ' + $Direccion + '/salud; HTTP=200; intento=' + $numero)
            return
        } catch {
            $ultimoError = $_.Exception
            EscribirDiagnosticoConexion ($etapa + '; intento=' + $numero + '; transcurrido=' + [int]$reloj.Elapsed.TotalSeconds + 's; ' + $ultimoError.Message)
            ComprobarProcesosConexion -Publica:$Publica
            if (-not $ultimoError.Data['Reintentable']) {
                throw
            }
        }
        $espera = [Math]::Min(1000, ($Intentos - $reloj.Elapsed.TotalSeconds) * 1000)
        if ($espera -gt 0) {
            Start-Sleep -Milliseconds ([int]$espera)
        }
    }
    $mensaje = $etapa + ' no disponible dentro del plazo de ' + $Intentos + 's.'
    if ($ultimoError) {
        $mensaje += ' Ultimo fallo: ' + $ultimoError.Message
    }
    throw (CrearErrorConexion $mensaje $false ([bool]($ultimoError -and $ultimoError.Data['FalloDns'])))
}

# Inicia Cloudflare y comprueba registro, DNS, HTTPS, salud y desafío Google. Reintenta fallos DNS.
function IniciarTunel {
    for ($intento = 1; $intento -le 3; $intento++) {
        $script:Tunel = IniciarProcesoOculto $script:Cloudflared 'tunnel --url http://127.0.0.1:5186 --no-autoupdate' ('cloudflare-' + $intento)
        try {
            $direccion = EsperarDireccionTunel $script:Tunel
            # El banner y Registered no garantizan que exista el registro DNS publico.
            EscribirDiagnosticoConexion ('Tunel temporal ' + $intento + '/3: ' + $direccion + '; comprobando DNS, HTTPS, salud y CORS.')
            $segundosRestantes = EsperarPreparacionTunel $script:Tunel $direccion 30
            EsperarApi $direccion $segundosRestantes -Publica
            $desafio = ConsultarJson ($direccion + '/api/google/desafio') 'POST'
            if (-not $desafio.success -or -not $desafio.data.nonce) {
                throw 'El desafio Google no esta disponible en la API HTTPS publica.'
            }
            return $direccion
        } catch {
            if (-not $script:Tunel.Proceso.HasExited) {
                $script:Tunel.Proceso.Kill()
                if (-not $script:Tunel.Proceso.WaitForExit(3000)) {
                    throw 'No se pudo cerrar el tunel anterior. No se abrira otro.'
                }
            }
            if (-not $_.Exception.Data['FalloDns'] -or $intento -eq 3) {
                throw
            }
            EscribirDiagnosticoConexion 'El nombre del tunel no resolvio por DNS. Se cerro ese proceso; se solicitara otra URL temporal.'
        }
    }
}

# Exige procesos activos, API pública y configuración publicada correcta para declarar listo.
function ObtenerEstadoSistema([string]$Direccion) {
    if ($script:Api.Proceso.HasExited -or $script:Tunel.Proceso.HasExited) {
        return @{
            Estado = 'error';
            Mensaje = 'SERVIDOR NO DISPONIBLE. Se detuvo la API o Cloudflare.'
        }
    }
    try {
        VerificarApiPublica $Direccion
        $publicada = ConsultarJson ('https://proyecto-tetris-512517.web.app/assets/configuracion-publica.json?presentacion=' + [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())
        if ($publicada.urlBackend.TrimEnd('/') -ne $Direccion) {
            return @{
                Estado = 'preparando';
                Mensaje = 'PREPARANDO CONEXION. Firebase todavia no apunta a esta sesion.'
            }
        }
        return @{
            Estado = 'listo';
            Mensaje = 'SERVIDOR EN LINEA. Ya pueden escanear y entrar al juego.'
        }
    } catch {
        return @{
            Estado = 'error';
            Mensaje = ('SERVIDOR NO DISPONIBLE. ' + $_.Exception.Message)
        }
    }
}

# Comprueba la salud del origen público con las reglas de HTTPS y CORS.
function VerificarApiPublica([string]$Direccion) {
    ComprobarSaludApi $Direccion -Publica
}
