# Lee los registros de Cloudflare aunque el proceso siga escribiendo y obtiene su URL.
# Abre el registro con acceso compartido y tolera bloqueos breves del escritor.
function LeerRegistroCompartido([string]$Ruta, [ValidateRange(1,10)][int]$Intentos = 3, [ValidateRange(0,1000)][int]$EsperaMilisegundos = 75) {
    for ($intento = 0; $intento -lt $Intentos; $intento++) {
        $flujo = $null
        try {
            $compartir = [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
            $flujo = [System.IO.File]::Open($Ruta, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, $compartir)
            # Leer solo una instantanea acotada: un escritor continuo no puede prolongar la lectura.
            $longitud = [int][Math]::Min($flujo.Length, 65536)
            $flujo.Seek(-$longitud, [System.IO.SeekOrigin]::End) | Out-Null
            $datos = New-Object byte[] $longitud
            $leidos = 0
            while ($leidos -lt $longitud) {
                $cantidad = $flujo.Read($datos, $leidos, $longitud - $leidos)
                if ($cantidad -eq 0) {
                    break
                }
                $leidos += $cantidad
            }
            return [System.Text.Encoding]::UTF8.GetString($datos, 0, $leidos)
        } catch [System.IO.IOException] {
            # Puede faltar el archivo o existir un bloqueo exclusivo transitorio.
            if ($intento + 1 -lt $Intentos) {
                Start-Sleep -Milliseconds $EsperaMilisegundos
            }
        } finally {
            if ($flujo) {
                $flujo.Dispose()
            }
        }
    }
    return ''
}

# Extrae una dirección de Quick Tunnel de los registros de salida y error.
function ObtenerDireccionDelRegistro($Registro) {
    $partes = foreach ($archivo in @($Registro.Salida, $Registro.Errores)) {
        LeerRegistroCompartido $archivo
    }
    $texto = $partes -join "`n"
    $coincidencia = [regex]::Match($texto, 'https://[a-z0-9-]+\.trycloudflare\.com(?![a-z0-9.-])')
    if ($coincidencia.Success) {
        return $coincidencia.Value
    }
    return $null
}

# Espera una URL dentro del plazo y detecta si Cloudflare termina antes.
function EsperarDireccionTunel($Registro, [ValidateRange(1,120)][int]$TiempoMaximoSegundos = 60, [ValidateRange(1,1000)][int]$IntervaloMilisegundos = 250) {
    $reloj = [System.Diagnostics.Stopwatch]::StartNew()
    while ($reloj.Elapsed.TotalSeconds -lt $TiempoMaximoSegundos) {
        if ($Registro.Proceso.HasExited) {
            throw 'Cloudflare termino sin establecer el tunel. Revisa su instalacion o conexion.'
        }
        if ($script:Api -and $script:Api.Proceso.HasExited) {
            throw 'La API se detuvo mientras se preparaba Cloudflare.'
        }
        $direccion = ObtenerDireccionDelRegistro $Registro
        if ($direccion) {
            return $direccion
        }
        Start-Sleep -Milliseconds $IntervaloMilisegundos
    }
    throw 'Cloudflare no anuncio una direccion HTTPS dentro del plazo previsto; los registros pueden seguir vacios o bloqueados.'
}
