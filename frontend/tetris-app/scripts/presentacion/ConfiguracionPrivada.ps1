# Carga o prepara la configuración privada protegida por DPAPI del usuario Windows.
# Comprueba SQL mediante lecturas y prepara el inicio seguro de la API.
# Evita cargar por PSModulePath una version de PowerShell distinta a la del iniciador.
Import-Module (Join-Path $PSHOME 'Modules/Microsoft.PowerShell.Security/Microsoft.PowerShell.Security.psd1') -ErrorAction Stop

# Lee la configuración cifrada existente o solicita los datos para prepararla con DPAPI.
function ObtenerConfiguracionPrivada {
    $carpeta = Join-Path $env:LOCALAPPDATA 'ProyectoTetris/Presentacion'
    [System.IO.Directory]::CreateDirectory($carpeta) | Out-Null
    $archivo = Join-Path $carpeta 'configuracion.dpapi'
    if (Test-Path $archivo) {
        try {
            $seguro = ConvertTo-SecureString ([System.IO.File]::ReadAllText($archivo))
            $texto = [System.Net.NetworkCredential]::new('', $seguro).Password
            return ($texto | ConvertFrom-Json)
        } catch {
            throw 'No se pudo descifrar la configuracion privada de este usuario Windows. Recuperala con el mismo usuario o renombra configuracion.dpapi para configurarla otra vez.'
        }
    }
    $conexion = $env:ConnectionStrings__TetrisDB
    if (-not $conexion) {
        $desarrollo = Get-Content (Join-Path $script:RaizRepo 'Backend/ProyectoTetris_PrograV/ProyectoTetris_PrograV/appsettings.Development.json') -Raw | ConvertFrom-Json
        $conexion = $desarrollo.ConnectionStrings.TetrisDB
    }
    if (-not $conexion) {
        $entrada = Read-Host 'Cadena de conexion a SQL Server local' -AsSecureString
        $conexion = [System.Net.NetworkCredential]::new('', $entrada).Password
    }
    $bytes = New-Object byte[] 64
    $generador = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generador.GetBytes($bytes)
    } finally {
        $generador.Dispose()
    }
    $datos = @{
        Conexion = $conexion;
        Clave = [Convert]::ToBase64String($bytes)
    }
    VerificarConexionSql $datos.Conexion
    $seguro = ConvertTo-SecureString ($datos | ConvertTo-Json -Compress) -AsPlainText -Force
    [System.IO.File]::WriteAllText($archivo, (ConvertFrom-SecureString $seguro))
    Write-Output 'Configuracion privada guardada con DPAPI para tu usuario Windows.' | Out-Host
    return [pscustomobject]$datos
}

# Exige un servidor local y comprueba conexión y esquema mediante consultas de lectura.
function VerificarConexionSql([string]$Cadena) {
    Add-Type -AssemblyName System.Data
    $opciones = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($Cadena)
    $servidor = (($opciones.DataSource -replace '^tcp:', '') -split '[\\,]')[0]
    if ($servidor -notin @('.', '(local)', 'localhost', '127.0.0.1', '::1', [Environment]::MachineName)) {
        throw 'La presentacion requiere SQL Server local. No se permite una cadena dirigida a otro equipo.'
    }
    $opciones['Connect Timeout'] = 5
    $conexion = $null
    try {
        $conexion = New-Object System.Data.SqlClient.SqlConnection($opciones.ConnectionString)
        $conexion.Open()
        $comando = $conexion.CreateCommand()
        $comando.CommandText = 'SELECT 1'
        if ($comando.ExecuteScalar() -ne 1) {
            throw 'SQL no respondio correctamente.'
        }
        VerificarEsquemaSesion $conexion
    } catch {
        if ($_.Exception.Data['CodigoTetris'] -eq 'EsquemaSesionPendiente') {
            throw
        }
        throw 'No se pudo consultar SQL Server local. Revisa el servicio y los permisos de tu usuario; no se modifico la base.'
    }
    finally {
        if ($conexion) {
            $conexion.Dispose()
        }
    }
}

# Comprueba las columnas de sesión única sin ejecutar la actualización del esquema.
function VerificarEsquemaSesion($Conexion) {
    $comando = $Conexion.CreateCommand()
    try {
        $comando.CommandText = "SELECT COL_LENGTH('dbo.Usuarios', 'SesionActivaId') + COL_LENGTH('dbo.Usuarios', 'HashRecuperacionPendiente')"
        if ($comando.ExecuteScalar() -is [DBNull]) {
            $errorEsquema = [InvalidOperationException]::new('Falta preparar la sesion unica en SQL. Revisa docs/sql/06_sesion_unica.sql y autoriza su aplicacion manual; el iniciador no modifica la base.')
            $errorEsquema.Data['CodigoTetris'] = 'EsquemaSesionPendiente'
            throw $errorEsquema
        }
    } finally {
        $comando.Dispose()
    }
}

# Comprueba el puerto, compila la API y le entrega las variables privadas solo al iniciarla.
function IniciarBackendSeguro($Privada) {
    $cliente = New-Object System.Net.Sockets.TcpClient
    try {
        $intento = $cliente.ConnectAsync('127.0.0.1',5186)
        if ($intento.Wait(500) -and $cliente.Connected) {
            throw 'El puerto 5186 esta ocupado. Detene Tetris.API antes de iniciar Tetris.Presentacion.'
        }
    } catch [System.AggregateException] { }
    finally {
        $cliente.Dispose()
    }
    $api = Join-Path $script:RaizRepo 'Backend/ProyectoTetris_PrograV/ProyectoTetris_PrograV/ProyectoTetris_PrograV.csproj'
    EjecutarComando 'dotnet.exe' @('build',$api,'-m:1','--nologo') $script:RaizRepo
    $nombres = @('ConnectionStrings__TetrisDB','Jwt__Clave','ASPNETCORE_ENVIRONMENT','DOTNET_ENVIRONMENT','ASPNETCORE_URLS')
    $anteriores = @{}
    foreach ($nombre in $nombres) {
        $anteriores[$nombre] = [Environment]::GetEnvironmentVariable($nombre)
    }
    try {
        $env:ConnectionStrings__TetrisDB = $Privada.Conexion
        $env:Jwt__Clave = $Privada.Clave
        $env:ASPNETCORE_ENVIRONMENT = 'Production'
        $env:DOTNET_ENVIRONMENT = 'Production'
        $env:ASPNETCORE_URLS = 'http://127.0.0.1:5186'
        $script:Api = IniciarProcesoOculto 'cmd.exe' '/d /s /c "npm.cmd run backend:publicado -- -SinCompilar"' 'api'
    } finally {
        foreach ($nombre in $nombres) {
            [Environment]::SetEnvironmentVariable($nombre, $anteriores[$nombre])
        }
    }
    EsperarApi 'http://127.0.0.1:5186' 40
    try {
        Invoke-WebRequest 'http://127.0.0.1:5186/api/google/desafio' -Method Post -UseBasicParsing -TimeoutSec 5 | Out-Null
        throw 'La API acepto HTTP sin proxy en un endpoint protegido por HTTPS. No se abrira el tunel.'
    } catch [System.Net.WebException] {
        if ([int]$_.Exception.Response.StatusCode -ne 400) {
            throw 'No se pudo verificar el modo seguro de la API.'
        }
    }
}
