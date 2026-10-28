param([string]$Ruta, [ValidateSet('Compartido','Exclusivo','CrearTarde')][string]$Modo)
$ErrorActionPreference = 'Stop'
if ($Modo -eq 'CrearTarde') { Start-Sleep -Milliseconds 350 }
$compartir = [System.IO.FileShare]::Read
if ($Modo -eq 'Exclusivo') { $compartir = [System.IO.FileShare]::None }
$flujo = [System.IO.File]::Open($Ruta, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, $compartir)
try {
    [System.IO.File]::WriteAllText(($Ruta + '.abierto'), 'listo')
    foreach ($parte in @('Iniciando conexion ', 'https://prueba-', 'concurrente.trycloudflare.com')) {
        $datos = [System.Text.Encoding]::UTF8.GetBytes($parte)
        $flujo.Write($datos, 0, $datos.Length)
        $flujo.Flush()
        Start-Sleep -Milliseconds 150
    }
    if ($Modo -eq 'Exclusivo') { Start-Sleep -Milliseconds 500 }
    else {
        $reloj = [System.Diagnostics.Stopwatch]::StartNew()
        while (-not (Test-Path ($Ruta + '.liberar')) -and $reloj.Elapsed.TotalSeconds -lt 8) { Start-Sleep -Milliseconds 30 }
    }
} finally { $flujo.Dispose() }
if ($Modo -eq 'Exclusivo') {
    # Liberar el archivo no equivale a terminar el tunel simulado.
    $reloj = [System.Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path ($Ruta + '.liberar')) -and $reloj.Elapsed.TotalSeconds -lt 8) {
        Start-Sleep -Milliseconds 30
    }
}
