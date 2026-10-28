# Comprueba respuestas HTTP y CORS y conserva diagnósticos de conectividad.
# Prepara un mensaje de diagnóstico de la conexión para el registro del iniciador.
function EscribirDiagnosticoConexion([string]$Mensaje) {
    # Host evita mezclar los registros con los valores devueltos por las funciones.
    $linea = '[conexion ' + [DateTime]::UtcNow.ToString('o') + '] ' + $Mensaje
    Write-Host $linea
    if ($script:CarpetaTemporal -and [IO.Directory]::Exists($script:CarpetaTemporal)) {
        GuardarDiagnosticoConexion $linea (Join-Path $script:CarpetaTemporal 'conexion.log')
    }
}

# Agrega la línea de diagnóstico al archivo indicado.
function GuardarDiagnosticoConexion([string]$Linea, [string]$Archivo) {
    $flujo = $null
    try {
        $flujo = [IO.File]::Open($Archivo, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
        $flujo.Seek(0, [IO.SeekOrigin]::End) | Out-Null
        $datos = [Text.Encoding]::UTF8.GetBytes($Linea + [Environment]::NewLine)
        $flujo.Write($datos, 0, $datos.Length)
    } catch [IO.IOException] {
        Write-Host '[conexion] No se pudo guardar conexion.log; el diagnostico permanece en consola.'
    } finally {
        if ($flujo) {
            $flujo.Dispose()
        }
    }
}

# Prepara un error con indicadores para decidir si conviene reintentar o cambiar de túnel.
function CrearErrorConexion([string]$Mensaje, [bool]$Reintentable, [bool]$FalloDns = $false) {
    $errorConexion = New-Object System.InvalidOperationException($Mensaje)
    $errorConexion.Data['Reintentable'] = $Reintentable
    $errorConexion.Data['FalloDns'] = $FalloDns
    return $errorConexion
}

# Extrae los datos de la respuesta HTTP que ayudan a explicar el fallo.
function ObtenerDetalleHttp($Excepcion) {
    $actual = $Excepcion
    while ($actual.InnerException -and -not ($actual -is [System.Net.WebException])) {
        $actual = $actual.InnerException
    }
    $codigo = 0
    $estado = $actual.GetType().Name
    if ($actual -is [System.Net.WebException]) {
        $estado = $actual.Status.ToString()
        if ($actual.Response) {
            $codigo = [int]$actual.Response.StatusCode
        }
    }
    $dns = $estado -eq 'NameResolutionFailure'
    $transitorio = $dns -or $estado -in @('ProxyNameResolutionFailure', 'ConnectFailure', 'Timeout', 'ConnectionClosed', 'ReceiveFailure', 'SendFailure', 'KeepAliveFailure')
    if ($codigo) {
        $transitorio = $codigo -ge 500 -or $codigo -in @(408, 429)
    }
    return @{
        Codigo = $codigo;
        Estado = $estado;
        Dns = $dns;
        Transitorio = $transitorio
    }
}

# Comprueba estado HTTP, tipo y contenido JSON y, cuando se pide, las cabeceras CORS.
function SolicitarJsonComprobado([string]$Url, [string]$Metodo = 'GET', [int]$TiempoSegundos = 10, [switch]$ExigirCors) {
    $uri = [Uri]$Url
    $destino = $uri.GetLeftPart([UriPartial]::Path)
    $origen = 'https://proyecto-tetris-512517.web.app'
    try {
        $erroresHttp = @()
        # PowerShell 5 entrega el 3xx y ademas un error no terminante al impedir redirecciones.
        $respuesta = Invoke-WebRequest -Uri $Url -Method $Metodo -Headers @{
            Origin = $origen
        } -UseBasicParsing -TimeoutSec $TiempoSegundos -MaximumRedirection 0 -ErrorAction SilentlyContinue -ErrorVariable erroresHttp
        if (-not $respuesta -and $erroresHttp.Count) {
            throw $erroresHttp[0].Exception
        }
    } catch {
        $detalle = ObtenerDetalleHttp $_.Exception
        $motivo = 'HTTP=' + $detalle.Codigo + '; excepcion=' + $detalle.Estado
        if ($detalle.Dns) {
            $motivo += '; DNS no resolvio el nombre; no se alcanzo la API'
        }
        throw (CrearErrorConexion ($destino + ': ' + $motivo) $detalle.Transitorio $detalle.Dns)
    }
    if ([int]$respuesta.StatusCode -ne 200) {
        $codigo = [int]$respuesta.StatusCode
        throw (CrearErrorConexion ($destino + ': HTTP=' + $codigo) ($codigo -ge 500 -or $codigo -in @(408,429)))
    }
    if ($ExigirCors -and $respuesta.Headers['Access-Control-Allow-Origin'] -ne $origen) {
        throw (CrearErrorConexion ($destino + ': HTTP=200; el origen Firebase no fue autorizado por CORS.') $false)
    }
    try {
        $datos = $respuesta.Content | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw (CrearErrorConexion ($destino + ': HTTP=200; respuesta JSON invalida.') $false)
    }
    return $datos
}

# Exige una respuesta de salud válida de la API y aplica las comprobaciones públicas cuando corresponda.
function ComprobarSaludApi([string]$Direccion, [int]$TiempoSegundos = 10, [switch]$Publica) {
    if ($Publica -and ([Uri]$Direccion).Scheme -ne 'https') {
        throw (CrearErrorConexion 'La API publica requiere HTTPS.' $false)
    }
    $url = $Direccion.TrimEnd('/') + '/salud'
    $salud = SolicitarJsonComprobado $url -TiempoSegundos $TiempoSegundos -ExigirCors:$Publica
    if ($salud.estado -ne 'Servidor Tetris activo') {
        throw (CrearErrorConexion ($url + ': HTTP=200; no se recibio el estado de salud esperado.') $false)
    }
}

# Detiene la espera si ya terminó la API o el túnel que se está comprobando.
function ComprobarProcesosConexion([switch]$Publica) {
    if ($script:Api -and $script:Api.Proceso.HasExited) {
        throw (CrearErrorConexion ('La API termino antes de responder; codigo de salida=' + $script:Api.Proceso.ExitCode) $false)
    }
    if ($Publica -and $script:Tunel -and $script:Tunel.Proceso.HasExited) {
        throw (CrearErrorConexion ('Cloudflare se detuvo; codigo de salida=' + $script:Tunel.Proceso.ExitCode) $false)
    }
}
