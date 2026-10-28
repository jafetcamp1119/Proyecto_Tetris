$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ConexionPublica.ps1')
. (Join-Path $PSScriptRoot 'Comun.ps1')
. (Join-Path $PSScriptRoot 'RegistrosCloudflare.ps1')

function ExigirFallo([scriptblock]$Accion, [string]$Patron) {
    try { & $Accion } catch {
        if ($_.Exception.Message -notlike $Patron) { throw }
        return $_.Exception
    }
    throw ('No se rechazo el escenario: ' + $Patron)
}

# HTTP real contra un servidor aislado: no inicia la API ni consulta SQL.
$script:Frontend = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$script:CarpetaTemporal = Join-Path $env:TEMP ('TetrisPruebasSalud-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($script:CarpetaTemporal) | Out-Null
$script:Procesos = New-Object 'System.Collections.Generic.List[object]'
$script:Api = $null
$script:Tunel = $null
try {
    $servidor = IniciarProcesoOculto (Get-Command node.exe).Source ('"' + (Join-Path $PSScriptRoot 'pruebas/ServidorSalud.cjs') + '"') 'servidor'
    $local = ''
    for ($i = 0; $i -lt 50 -and -not $local; $i++) {
        Start-Sleep -Milliseconds 100
        $local = (LeerRegistroCompartido $servidor.Salida).Trim()
    }
    if (-not $local) { throw 'No inicio el servidor HTTP simulado.' }
    EsperarApi $local 3
    SolicitarJsonComprobado ($local + '/salud') -ExigirCors | Out-Null
    $errorHttp = ExigirFallo { SolicitarJsonComprobado ($local + '/no-disponible') } '*HTTP=503*'
    if (-not $errorHttp.Data['Reintentable']) { throw 'HTTP 503 debe poder reintentarse.' }
    ExigirFallo { SolicitarJsonComprobado ($local + '/no-existe') } '*HTTP=404*' | Out-Null
    ExigirFallo { SolicitarJsonComprobado ($local + '/sin-cors') -ExigirCors } '*HTTP=200*CORS*' | Out-Null
    ExigirFallo { SolicitarJsonComprobado ($local + '/no-json') } '*HTTP=200*JSON invalida*' | Out-Null
    ExigirFallo { SolicitarJsonComprobado ($local + '/redireccion') } '*HTTP=302*' | Out-Null
    Write-Output 'OK: HTTP real local 200, 503, 404, JSON invalido y CORS ausente.'
} finally {
    foreach ($registro in $script:Procesos) {
        if (-not $registro.Proceso.HasExited) { $registro.Proceso.Kill() }
        $registro.Proceso.WaitForExit(3000) | Out-Null
        $registro.Proceso.Dispose()
    }
}

& {
    $script:IntentosHttp = 0
    function Invoke-WebRequest {
        $script:IntentosHttp++
        if ($script:IntentosHttp -eq 1) {
            throw ([System.Net.WebException]::new('Mensaje sensible que no debe registrarse', [System.Net.WebExceptionStatus]::NameResolutionFailure))
        }
        return @{ StatusCode = 200; Headers = @{ 'Access-Control-Allow-Origin' = 'https://proyecto-tetris-512517.web.app' }; Content = '{"estado":"Servidor Tetris activo"}' }
    }
    EsperarApi 'https://ejemplo.trycloudflare.com' 3 -Publica
    if ($script:IntentosHttp -ne 2) { throw 'No se recupero de DNS transitorio.' }
    function Invoke-WebRequest {
        throw ([System.Net.WebException]::new('Mensaje sensible', [System.Net.WebExceptionStatus]::NameResolutionFailure))
    }
    $fallo = ExigirFallo { EsperarApi 'https://ejemplo.trycloudflare.com' 1 -Publica } '*API publica HTTPS*NameResolutionFailure*'
    if (-not $fallo.Data['FalloDns'] -or $fallo.Message -like '*sensible*') { throw 'No se clasifico DNS sin filtrar secretos.' }
    $script:Api = @{ Proceso = @{ HasExited = $true; ExitCode = 17 } }
    ExigirFallo { EsperarApi 'http://127.0.0.1:5186' 2 } '*API termino*17*' | Out-Null
    $script:Api = $null
    Write-Output 'OK: DNS transitorio, DNS persistente con causa conservada y salida prematura de API.'
}

& {
    $script:IntentosHttp = 0
    function Invoke-WebRequest {
        $script:IntentosHttp++
        if ($script:IntentosHttp -eq 1) {
            throw ([System.Net.WebException]::new('Inicio en curso', [System.Net.WebExceptionStatus]::ConnectFailure))
        }
        return @{ StatusCode = 200; Headers = @{}; Content = '{"estado":"Servidor Tetris activo"}' }
    }
    EsperarApi 'http://127.0.0.1:5186' 3
    if ($script:IntentosHttp -ne 2) { throw 'No se espero el arranque local.' }
    $script:IntentosHttp = 0
    ExigirFallo { EsperarApi 'https://ejemplo.trycloudflare.com' 3 -Publica } '*CORS*' | Out-Null
    if ($script:IntentosHttp -ne 2) { throw 'Se reintento un fallo permanente de CORS.' }
    Write-Output 'OK: espera arranque local; falla sin agotar el plazo cuando CORS es incorrecto.'
}

& {
    $script:Api = $null
    $script:Tunel = $null
    function LeerRegistroCompartido { return '' }
    function ConsultarDnsTunel { throw 'No debe consultarse DNS antes de Registered.' }
    ExigirFallo { EsperarPreparacionTunel @{ Salida = ''; Errores = '' } 'https://ejemplo.trycloudflare.com' 1 } '*no registro una conexion*' | Out-Null
    $script:ConsultasDns = 0
    function LeerRegistroCompartido { return 'Registered tunnel connection' }
    function ConsultarDnsTunel {
        $script:ConsultasDns++
        return ($script:ConsultasDns -ge 2)
    }
    function Invoke-WebRequest { throw 'No consultar HTTP durante la propagacion DNS.' }
    $restante = EsperarPreparacionTunel @{ Salida = ''; Errores = '' } 'https://ejemplo.trycloudflare.com' 3
    if ($restante -lt 1 -or $script:ConsultasDns -ne 2) { throw 'No se espero propagacion sin resolver el nombre con Windows.' }
    Write-Output 'OK: Registered antes de DNS; propagacion antes de HTTP; presupuesto restante compartido.'
}

& {
    $script:ConsultasDns = 0
    function Invoke-RestMethod {
        $script:ConsultasDns++
        if ($script:ConsultasDns -eq 1) { return @{ Status = 3 } }
        return @{ Status = 0; Answer = @(@{ type = 1; data = '192.0.2.1' }) }
    }
    if (-not (ConsultarDnsTunel 'ejemplo.trycloudflare.com' 1) -or $script:ConsultasDns -ne 2) {
        throw 'No se comprobo el segundo resolutor tras NXDOMAIN.'
    }
    Write-Output 'OK: NXDOMAIN de un resolutor no oculta un registro positivo del otro.'
}

& {
    $script:Creados = 0
    $script:Cerrados = 0
    $script:FallaSiempre = $false
    function IniciarProcesoOculto {
        if ($script:Creados -ne $script:Cerrados) { throw 'Se abrio un tunel antes de cerrar el anterior.' }
        $script:Creados++
        $proceso = [pscustomobject]@{ HasExited = $false }
        $proceso | Add-Member ScriptMethod Kill { $this.HasExited = $true; $script:Cerrados++ }
        $proceso | Add-Member ScriptMethod WaitForExit { return $this.HasExited }
        return @{ Proceso = $proceso }
    }
    function EsperarDireccionTunel { return ('https://prueba-' + $script:Creados + '.trycloudflare.com') }
    function EsperarPreparacionTunel { return 30 }
    function EsperarApi {
        if ($script:FallaSiempre -or $script:Creados -lt 2) {
            throw (CrearErrorConexion 'API publica: NameResolutionFailure' $false $true)
        }
    }
    function ConsultarJson { return @{ success = $true; data = @{ nonce = 'solo-prueba' } } }
    $direccion = IniciarTunel
    if ($direccion -ne 'https://prueba-2.trycloudflare.com' -or $script:Cerrados -ne 1) { throw 'No se renovo el tunel defectuoso.' }
    $script:Tunel.Proceso.Kill()
    $script:Creados = 0
    $script:Cerrados = 0
    $script:FallaSiempre = $true
    ExigirFallo { IniciarTunel } '*NameResolutionFailure*' | Out-Null
    if ($script:Creados -ne 3 -or $script:Cerrados -ne 3) { throw 'Reintentos ilimitados o procesos de tunel sin cerrar.' }
    Write-Output 'OK: renueva solo ante DNS; maximo tres tuneles, sin procesos simultaneos ni huerfanos.'
}
