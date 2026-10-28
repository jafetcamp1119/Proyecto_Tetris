. (Join-Path $PSScriptRoot 'Comun.ps1')
. (Join-Path $PSScriptRoot 'ConfiguracionPrivada.ps1')
. (Join-Path $PSScriptRoot 'RegistrosCloudflare.ps1')
. (Join-Path $PSScriptRoot 'ConexionPublica.ps1')
. (Join-Path $PSScriptRoot 'PublicacionFirebase.ps1')
$ErrorActionPreference = 'Stop'
foreach ($archivo in (Get-ChildItem $PSScriptRoot -Filter '*.ps1')) {
    $errores = $null; $tokens = $null
    [System.Management.Automation.Language.Parser]::ParseFile($archivo.FullName, [ref]$tokens, [ref]$errores) | Out-Null
    if ($errores.Count) { throw "Error de sintaxis en $($archivo.Name)" }
}
Write-Output 'OK: scripts PowerShell analizados sin iniciar servicios.'
$script:Api = @{ Proceso = @{ HasExited = $false } }
$script:Tunel = @{ Proceso = @{ HasExited = $false } }
$script:Publicado = 'https://otro-tunel.trycloudflare.com'
$script:CorsValido = $true
function Invoke-WebRequest {
    $cabeceras = @{}
    if ($script:CorsValido) { $cabeceras['Access-Control-Allow-Origin'] = 'https://proyecto-tetris-512517.web.app' }
    return @{ StatusCode = 200; Content = '{"estado":"Servidor Tetris activo"}'; Headers = $cabeceras }
}
function ConsultarJson { return @{ urlBackend = $script:Publicado } }
if ((ObtenerEstadoSistema 'https://actual.trycloudflare.com').Estado -ne 'preparando') { throw 'No se detecto configuracion publicada antigua.' }
$script:Publicado = 'https://actual.trycloudflare.com'
if ((ObtenerEstadoSistema $script:Publicado).Estado -ne 'listo') { throw 'No se reconocio la disponibilidad comprobada.' }
$script:CorsValido = $false
if ((ObtenerEstadoSistema $script:Publicado).Estado -ne 'error') { throw 'Se anuncio disponibilidad sin CORS valido.' }
$script:CorsValido = $true
$httpRechazado = $false
try { VerificarApiPublica 'http://api-insegura.example' } catch { $httpRechazado = $_.Exception.Message -like '*requiere HTTPS*' }
if (-not $httpRechazado) { throw 'Se acepto una API publica HTTP.' }
$script:Tunel.Proceso.HasExited = $true
if ((ObtenerEstadoSistema $script:Publicado).Estado -ne 'error') { throw 'Se anuncio disponibilidad despues de cerrar Cloudflare.' }
Write-Output 'OK: estados por API, CORS, Firebase y proceso de tunel (simulados).'
function LeerLineaConfirmacion { return '' }
function EjecutarComando { throw 'Nunca se debe publicar sin autorizacion.' }
$script:Publicado = 'https://anterior.trycloudflare.com'
$rechazo = $false
try { ConfirmarPublicacion 'https://nuevo.trycloudflare.com' } catch { $rechazo = $_.Exception.Message -like 'Publicacion cancelada*' }
if (-not $rechazo) { throw 'La publicacion no exigio confirmacion.' }
Write-Output 'OK: cancelar confirmacion no ejecuta Firebase deploy.'
$rechazo = $false
try { VerificarConexionSql 'Server=equipo-remoto;Database=TetrisDB;Integrated Security=True' } catch { $rechazo = $_.Exception.Message -like '*SQL Server local*' }
if (-not $rechazo) { throw 'No se rechazo SQL remoto antes de abrir conexion.' }
Write-Output 'OK: SQL remoto rechazado sin abrir conexion ni modificar datos.'
$script:LongitudColumnas = [DBNull]::Value
$comandoSimulado = [PSCustomObject]@{ CommandText = '' }
$comandoSimulado | Add-Member ScriptMethod ExecuteScalar { return $script:LongitudColumnas }
$comandoSimulado | Add-Member ScriptMethod Dispose { }
$conexionSimulada = [PSCustomObject]@{ Comando = $comandoSimulado }
$conexionSimulada | Add-Member ScriptMethod CreateCommand { return $this.Comando }
$rechazo = $false
try { VerificarEsquemaSesion $conexionSimulada } catch { $rechazo = $_.Exception.Data['CodigoTetris'] -eq 'EsquemaSesionPendiente' }
if (-not $rechazo) { throw 'No se detecto el esquema pendiente antes de iniciar la API.' }
$script:LongitudColumnas = 80
VerificarEsquemaSesion $conexionSimulada
if ($comandoSimulado.CommandText -notlike 'SELECT COL_LENGTH*') { throw 'La comprobacion de esquema debe ser de solo lectura.' }
Write-Output 'OK: esquema pendiente detectado con consulta de solo lectura simulada, sin SQL real.'
$pruebaSegura = ConvertTo-SecureString 'verificacion-en-memoria' -AsPlainText -Force
$cifrada = ConvertFrom-SecureString $pruebaSegura
$recuperada = [System.Net.NetworkCredential]::new('', (ConvertTo-SecureString $cifrada)).Password
if ($recuperada -ne 'verificacion-en-memoria') { throw 'Fallo DPAPI del usuario actual.' }
Write-Output 'OK: DPAPI cifra/descifra con el usuario Windows, sin guardar configuracion personal.'
