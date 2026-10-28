# Ejercita la confirmación heredada por el proceso PowerShell de prueba.
param([string]$RaizRepo, [string]$ArchivoEstado)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../ConfirmacionPublicacion.ps1')
Write-Output 'DIAGNOSTICO DE CONFIRMACION. No inicia API, SQL, Cloudflare ni Firebase Deploy.'
Write-Output ('Entrada redirigida: ' + [Console]::IsInputRedirected)
try {
    SolicitarConfirmacionPublicacion
    Write-Output 'CONFIRMACION RECONOCIDA. Prueba terminada SIN PUBLICAR.'
    exit 0
} catch {
    Write-Output $_.Exception.Message
    exit 1
}
