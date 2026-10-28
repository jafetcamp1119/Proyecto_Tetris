param([string]$ArchivoLogo)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$raiz = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $ArchivoLogo) { $ArchivoLogo = Join-Path $raiz 'logo-tetris.png' }
$destino = Join-Path $raiz 'src/assets/icon'
$original = [System.Drawing.Bitmap]::FromFile($ArchivoLogo)

function ObtenerLimitesVisibles([System.Drawing.Bitmap]$Imagen) {
    $izquierda = $Imagen.Width; $arriba = $Imagen.Height; $derecha = 0; $abajo = 0
    for ($fila = 0; $fila -lt $Imagen.Height; $fila++) {
        for ($columna = 0; $columna -lt $Imagen.Width; $columna++) {
            if ($Imagen.GetPixel($columna, $fila).A -gt 20) {
                $izquierda = [Math]::Min($izquierda, $columna)
                $arriba = [Math]::Min($arriba, $fila)
                $derecha = [Math]::Max($derecha, $columna + 1)
                $abajo = [Math]::Max($abajo, $fila + 1)
            }
        }
    }
    if ($derecha -le $izquierda) { throw 'El logo no contiene pixeles visibles.' }
    return [System.Drawing.Rectangle]::FromLTRB($izquierda, $arriba, $derecha, $abajo)
}

function GuardarIcono([System.Drawing.Image]$Imagen, [int]$Tamano, [string]$Ruta, [double]$Proporcion = 0.88, [bool]$Transparente = $false) {
    $salida = New-Object System.Drawing.Bitmap($Tamano, $Tamano)
    $grafico = [System.Drawing.Graphics]::FromImage($salida)
    try {
        $fondo = [System.Drawing.ColorTranslator]::FromHtml('#090e1c')
        if ($Transparente) { $fondo = [System.Drawing.Color]::Transparent }
        $grafico.Clear($fondo)
        $grafico.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $escala = [Math]::Min($Tamano * $Proporcion / $Imagen.Width, $Tamano * $Proporcion / $Imagen.Height)
        $ancho = [int]($Imagen.Width * $escala)
        $alto = [int]($Imagen.Height * $escala)
        $grafico.DrawImage($Imagen, [int](($Tamano - $ancho)/2), [int](($Tamano - $alto)/2), $ancho, $alto)
        [System.IO.Directory]::CreateDirectory((Split-Path $Ruta -Parent)) | Out-Null
        $salida.Save($Ruta, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $grafico.Dispose(); $salida.Dispose() }
}

function GuardarFaviconIco([string]$Carpeta) {
    $archivos = @(foreach ($tamano in @(16,32,48)) { ,([System.IO.File]::ReadAllBytes((Join-Path $Carpeta "tetris-favicon-v2-$tamano.png"))) })
    $flujo = [System.IO.File]::Create((Join-Path $Carpeta 'tetris-favicon-v2.ico'))
    $escritor = New-Object System.IO.BinaryWriter($flujo)
    try {
        $escritor.Write([uint16]0); $escritor.Write([uint16]1); $escritor.Write([uint16]3)
        $desplazamiento = 6 + 16 * 3
        for ($indice = 0; $indice -lt 3; $indice++) {
            $tamano = @(16,32,48)[$indice]
            $escritor.Write([byte]$tamano); $escritor.Write([byte]$tamano)
            $escritor.Write([byte]0); $escritor.Write([byte]0)
            $escritor.Write([uint16]1); $escritor.Write([uint16]32)
            $escritor.Write([uint32]$archivos[$indice].Length); $escritor.Write([uint32]$desplazamiento)
            $desplazamiento += $archivos[$indice].Length
        }
        foreach ($archivo in $archivos) { $escritor.Write([byte[]]$archivo) }
    } finally { $escritor.Dispose(); $flujo.Dispose() }
}

try {
    $limites = ObtenerLimitesVisibles $original
    $logo = $original.Clone($limites, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    # La primera T de la referencia ocupa esta zona; se conserva su dibujo original.
    $letra = $original.Clone([System.Drawing.Rectangle]::FromLTRB(0,105,54,197), [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $logo.Save((Join-Path $destino 'tetris-logo-v2.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        foreach ($tamano in @(16,32,48)) { GuardarIcono $letra $tamano (Join-Path $destino "tetris-favicon-v2-$tamano.png") 0.95 }
        GuardarFaviconIco $destino
        foreach ($tamano in @(180,192,512)) { GuardarIcono $logo $tamano (Join-Path $destino "tetris-logo-v2-$tamano.png") }
        GuardarIcono $logo 512 (Join-Path $destino 'tetris-maskable-v2-512.png') 0.60
        foreach ($densidad in @(@('mdpi',48,108),@('hdpi',72,162),@('xhdpi',96,216),@('xxhdpi',144,324),@('xxxhdpi',192,432))) {
            $carpeta = Join-Path $raiz "android/app/src/main/res/mipmap-$($densidad[0])"
            GuardarIcono $logo $densidad[1] (Join-Path $carpeta 'ic_launcher.png') 0.80
            GuardarIcono $logo $densidad[1] (Join-Path $carpeta 'ic_launcher_round.png') 0.65
            GuardarIcono $logo $densidad[2] (Join-Path $carpeta 'ic_launcher_foreground.png') 0.57 $true
        }
    } finally { $logo.Dispose(); $letra.Dispose() }
} finally { $original.Dispose() }
Write-Output 'Recursos del logo preparados. No se compilo Android ni se genero APK.'
