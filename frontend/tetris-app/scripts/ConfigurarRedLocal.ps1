# Detecta la IPv4 privada de desarrollo y actualiza la conexión y la excepción HTTP debug.
# No abre el firewall ni genera una APK.
﻿param([string]$Interfaz)

$ErrorActionPreference = 'Stop'
$redes = @(Get-NetIPConfiguration | Where-Object {
        $_.NetAdapter.Status -eq 'Up' -and $_.IPv4DefaultGateway -and $_.IPv4Address
    })
if ($Interfaz) {
    $redes = @($redes | Where-Object InterfaceAlias -eq $Interfaz)
}
if ($redes.Count -ne 1) {
    throw 'No se pudo elegir una única conexión. Usá -Interfaz con el nombre de tu conexión Wi-Fi.'
}
$direccion = $redes[0].IPv4Address.IPAddress | Select-Object -First 1
$octetos = ([System.Net.IPAddress]::Parse($direccion)).GetAddressBytes()
$privada = $octetos[0] -eq 10 -or ($octetos[0] -eq 192 -and $octetos[1] -eq 168) -or
($octetos[0] -eq 172 -and $octetos[1] -ge 16 -and $octetos[1] -le 31)
if (-not $privada) {
    throw 'La conexión seleccionada no tiene una IPv4 privada de red local.'
}
$raiz = Split-Path $PSScriptRoot -Parent
$configuracion = @"
// Configuración interna de desarrollo. Actualizar con npm run red:local al cambiar de red.
export const configuracionRed = {
    urlNavegador: 'http://' + window.location.hostname + ':5186',
    urlAndroid: 'http://${direccion}:5186',
    permitirHttpLocal: true
};
"@
$seguridad = @"
<?xml version="1.0" encoding="utf-8"?>
<!-- Excepción de HTTP solo para el backend de esta red local y solo en Android debug. -->
<network-security-config>
    <base-config cleartextTrafficPermitted="false">
        <trust-anchors>
            <certificates src="system" />
            <certificates src="user" />
        </trust-anchors>
    </base-config>
    <domain-config cleartextTrafficPermitted="true">
        <domain includeSubdomains="false">${direccion}</domain>
    </domain-config>
</network-security-config>
"@
[System.IO.File]::WriteAllText((Join-Path $raiz 'src/environments/configuracion-red.ts'), $configuracion)
[System.IO.File]::WriteAllText((Join-Path $raiz 'android/app/src/debug/res/xml/seguridad_red_desarrollo.xml'), $seguridad)
Write-Output "Red de desarrollo: http://${direccion}:5186 ($($redes[0].InterfaceAlias))."
Write-Output 'Configuración actualizada. No se compiló ni generó ningún APK.'
