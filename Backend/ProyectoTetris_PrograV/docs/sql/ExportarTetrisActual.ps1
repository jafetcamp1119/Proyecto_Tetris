# Exporta la definición SQL existente para preparar una referencia del esquema. Ejecutarlo requiere acceso a SQL.
$ErrorActionPreference = 'Stop'
trap {
    $errorActual = $_.Exception
    while ($null -ne $errorActual) {
        Write-Output ($errorActual.GetType().FullName + ': ' + $errorActual.Message)
        $errorActual = $errorActual.InnerException
    }
    exit 1
}
$directorioSmo = 'C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE'
foreach ($dependencia in @('System.Runtime.CompilerServices.Unsafe.dll', 'System.Buffers.dll', 'System.Memory.dll', 'System.Threading.Tasks.Extensions.dll')) {
    [void][System.Reflection.Assembly]::LoadFrom([System.IO.Path]::Combine($directorioSmo, 'PublicAssemblies', $dependencia))
}
$resolver = [System.ResolveEventHandler] {
    param($origen, $evento)
    $nombreArchivo = $evento.Name.Split(',')[0] + '.dll'
    foreach ($directorio in @($directorioSmo, [System.IO.Path]::Combine($directorioSmo, 'PublicAssemblies'))) {
        $ruta = [System.IO.Path]::Combine($directorio, $nombreArchivo)
        if ([System.IO.File]::Exists($ruta)) {
            return [System.Reflection.Assembly]::LoadFrom($ruta)
        }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
foreach ($nombre in @('Microsoft.SqlServer.Management.Sdk.Sfc.dll', 'Microsoft.SqlServer.ConnectionInfo.dll', 'Microsoft.SqlServer.SqlEnum.dll', 'Microsoft.SqlServer.Smo.dll', 'Microsoft.SqlServer.SmoExtended.dll')) {
    [void][System.Reflection.Assembly]::LoadFrom((Join-Path $directorioSmo $nombre))
}
$conexion = New-Object Microsoft.SqlServer.Management.Common.ServerConnection
$conexion.ServerInstance = 'JAFET\SQLEXPRESS'
$conexion.LoginSecure = $true
$conexion.TrustServerCertificate = $true
$conexion.ConnectTimeout = 5
$conexion.StatementTimeout = 30
$servidor = New-Object Microsoft.SqlServer.Management.Smo.Server($conexion)
$conexion.Connect()
$servidor.Databases.Refresh()
Write-Output ('Servidor conectado: ' + $conexion.ServerInstance + '; bases: ' + (($servidor.Databases | ForEach-Object {
                $_.Name
            }) -join ', '))
$base = $servidor.Databases['TetrisDB']
if ($null -eq $base) {
    throw 'No se encontro TetrisDB.'
}
$base.Refresh()
$tablas = @($base.Tables | Where-Object {
        -not $_.IsSystemObject
    } | Sort-Object Name)
Write-Output ('Tablas leidas: ' + $tablas.Count)
$vistas = @($base.Views | Where-Object {
        -not $_.IsSystemObject
    } | Sort-Object Name)
Write-Output ('Vistas leidas: ' + $vistas.Count)
$filasProcedimientos = $conexion.ExecuteWithResults('USE [TetrisDB]; SELECT name AS Nombre, SCHEMA_NAME(schema_id) AS Esquema FROM sys.procedures WHERE is_ms_shipped = 0 ORDER BY name;').Tables[0].Rows
$procedimientos = @($filasProcedimientos | ForEach-Object {
        $base.StoredProcedures.Item($_.Nombre, $_.Esquema)
    })
Write-Output ('Procedimientos leidos: ' + $procedimientos.Count)
$opciones = New-Object Microsoft.SqlServer.Management.Smo.ScriptingOptions
$opciones.ScriptSchema = $true
$opciones.ScriptData = $false
$opciones.ScriptDrops = $false
$opciones.WithDependencies = $false
$opciones.DriPrimaryKey = $true
$opciones.DriUniqueKeys = $true
$opciones.DriDefaults = $true
$opciones.DriChecks = $true
$opciones.DriForeignKeys = $false
$opciones.DriIncludeSystemNames = $true
$opciones.Indexes = $true
$opciones.Triggers = $true
$opciones.IncludeHeaders = $false
$opciones.IncludeDatabaseContext = $false
$opciones.SchemaQualify = $true
$opciones.ScriptOwner = $false
$opciones.Permissions = $false
$opciones.ExtendedProperties = $false
$opciones.TargetServerVersion = [Microsoft.SqlServer.Management.Smo.SqlServerVersion]::Version150
$scriptor = New-Object Microsoft.SqlServer.Management.Smo.Scripter($servidor)
$scriptor.Options = $opciones
$objetos = @($tablas) + @($vistas) + @($procedimientos)
$urns = [Microsoft.SqlServer.Management.Sdk.Sfc.Urn[]]@($objetos | ForEach-Object {
        $_.Urn
    })
$lotes = @($scriptor.Script($urns))
Write-Output ('Lotes iniciales: ' + $lotes.Count)
foreach ($tabla in $tablas) {
    foreach ($relacion in $tabla.ForeignKeys) {
        $lotes += @($relacion.Script())
    }
}
Write-Output ('Lotes con relaciones: ' + $lotes.Count)
$catalogos = $conexion.ExecuteWithResults('USE [TetrisDB]; SET NOCOUNT ON; SELECT TipoPiezaId,Nombre,Codigo,EsEspecial,EliminaColumna,Descripcion,Activa FROM dbo.TiposPieza ORDER BY TipoPiezaId; SELECT RecompensaId,Nombre,PuntosRequeridos,TipoPiezaId,Descripcion,Activa FROM dbo.RecompensasPuntos ORDER BY RecompensaId;')
$salida = [ordered]@{
    Tablas = @($tablas | ForEach-Object {
            $_.Name
        })
    Vistas = @($vistas | ForEach-Object {
            $_.Name
        })
    Procedimientos = @($procedimientos | ForEach-Object {
            $_.Name
        })
    Collation = $base.Collation
    Lotes = $lotes
    Tipos = @($catalogos.Tables[0].Rows | ForEach-Object {
            [ordered]@{
                TipoPiezaId=$_.TipoPiezaId;
                Nombre=$_.Nombre;
                Codigo=$_.Codigo;
                EsEspecial=$_.EsEspecial;
                EliminaColumna=$_.EliminaColumna;
                Descripcion=$_.Descripcion;
                Activa=$_.Activa
            }
        })
    Recompensas = @($catalogos.Tables[1].Rows | ForEach-Object {
            [ordered]@{
                RecompensaId=$_.RecompensaId;
                Nombre=$_.Nombre;
                PuntosRequeridos=$_.PuntosRequeridos;
                TipoPiezaId=$_.TipoPiezaId;
                Descripcion=$_.Descripcion;
                Activa=$_.Activa
            }
        })
}
$rutaTemporal = Join-Path $PSScriptRoot 'TetrisEstructuraVerificada.json'
$salida | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $rutaTemporal -Encoding UTF8
$conexion.Disconnect()
Write-Output ('Tablas=' + $tablas.Count + '; Vistas=' + $vistas.Count + '; Procedimientos=' + $procedimientos.Count + '; Lotes=' + $lotes.Count)
