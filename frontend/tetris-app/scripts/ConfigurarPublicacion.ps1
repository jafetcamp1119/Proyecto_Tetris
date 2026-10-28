param([Parameter(Mandatory = $true)][string]$UrlBackend)
$ErrorActionPreference = 'Stop'
$direccion = $null
if (-not [Uri]::TryCreate($UrlBackend, [UriKind]::Absolute, [ref]$direccion)) { throw 'La dirección no es válida.' }
$ip = $null
if ($direccion.Scheme -ne 'https' -or $direccion.UserInfo -or -not $direccion.IsDefaultPort -or
    $direccion.AbsolutePath -ne '/' -or $direccion.Query -or $direccion.Fragment -or
    $direccion.IsLoopback -or $direccion.Host -notmatch '\.' -or
    $direccion.Host -match '\.(localhost|local|internal)$' -or
    [System.Net.IPAddress]::TryParse($direccion.Host, [ref]$ip)) {
    throw 'Usá únicamente un origen HTTPS público, sin rutas, puertos, IPs ni credenciales.'
}
$raiz = Split-Path $PSScriptRoot -Parent
if ($direccion.DnsSafeHost -notmatch '^[a-z0-9]+(?:-[a-z0-9]+)*\.trycloudflare\.com$') {
    throw 'El backend debe pertenecer a un Quick Tunnel de Cloudflare. Un dominio diferente requiere actualizar la lista permitida del proyecto.'
}
$texto = @{ urlBackend = $direccion.GetLeftPart([UriPartial]::Authority) } | ConvertTo-Json
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $raiz 'src/assets/configuracion-publica.json'), $texto, $utf8)
if (Test-Path (Join-Path $raiz 'www/index.html')) {
    [System.IO.File]::WriteAllText((Join-Path $raiz 'www/assets/configuracion-publica.json'), $texto, $utf8)
}
Write-Output 'Dirección pública actualizada en un único archivo de configuración y en www si ya existía.'
Write-Output 'No se publico ni compilo Android. La app de produccion obtiene el cambio al publicarlo en Firebase y reintentar la conexion; no requiere otra APK.'
