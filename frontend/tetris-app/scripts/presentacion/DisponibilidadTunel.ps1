# Comprueba que el nombre del túnel resuelva por DNS dentro del plazo disponible.
# Consulta la resolución del nombre del túnel con un plazo limitado.
function ConsultarDnsTunel([string]$Nombre, [int]$TiempoSegundos) {
    $nombreSeguro = [Uri]::EscapeDataString($Nombre)
    foreach ($base in @('https://cloudflare-dns.com/dns-query?name=', 'https://dns.google/resolve?name=')) {
        try {
            $datos = Invoke-RestMethod ($base + $nombreSeguro + '&type=A') -Headers @{
                Accept = 'application/dns-json'
            } -TimeoutSec $TiempoSegundos -ErrorAction Stop
            if ($datos.Status -eq 0 -and @($datos.Answer | Where-Object {
                        $_.type -eq 1
                    }).Count -gt 0) {
                return $true
            }
            EscribirDiagnosticoConexion ('DNS publico; nombre=' + $Nombre + '; resolutor=' + ([Uri]$base).Host + '; codigo DNS=' + $datos.Status)
        } catch {
            $detalle = ObtenerDetalleHttp $_.Exception
            EscribirDiagnosticoConexion ('Consulta DNS por HTTPS fallo; resolutor=' + ([Uri]$base).Host + '; HTTP=' + $detalle.Codigo + '; excepcion=' + $detalle.Estado)
        }
    }
    return $false
}

# Espera registro y resolución DNS antes de consumir el plazo de salud pública.
function EsperarPreparacionTunel($Registro, [string]$Direccion, [int]$TiempoSegundos = 30) {
    $reloj = [System.Diagnostics.Stopwatch]::StartNew()
    $registrado = $false
    while ($reloj.Elapsed.TotalSeconds -lt $TiempoSegundos) {
        ComprobarProcesosConexion -Publica
        if (-not $registrado) {
            $texto = (LeerRegistroCompartido $Registro.Salida) + "`n" + (LeerRegistroCompartido $Registro.Errores)
            $registrado = $texto -match 'Registered tunnel connection'
            if ($registrado) {
                EscribirDiagnosticoConexion 'Cloudflare confirmo Registered tunnel connection; verificando propagacion DNS.'
            }
        }
        if ($registrado) {
            $restante = [Math]::Max(1, [Math]::Floor(($TiempoSegundos - $reloj.Elapsed.TotalSeconds) / 2))
            if (ConsultarDnsTunel ([Uri]$Direccion).DnsSafeHost ([Math]::Min(3, $restante))) {
                EscribirDiagnosticoConexion ('DNS publico disponible: ' + $Direccion + '; comenzando comprobacion HTTPS del sistema.')
                return [int][Math]::Max(1, [Math]::Floor($TiempoSegundos - $reloj.Elapsed.TotalSeconds))
            }
        }
        $espera = [Math]::Min(1000, ($TiempoSegundos - $reloj.Elapsed.TotalSeconds) * 1000)
        if ($espera -gt 0) {
            Start-Sleep -Milliseconds ([int]$espera)
        }
    }
    if (-not $registrado) {
        throw (CrearErrorConexion 'Cloudflare anuncio URL pero no registro una conexion dentro del plazo.' $false)
    }
    throw (CrearErrorConexion ($Direccion + ': el nombre del tunel no se propago en DNS publico dentro del plazo; no se consulto /salud para evitar cache negativa.') $false $true)
}
