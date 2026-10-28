function EsConfirmacionPublicacion($Respuesta) {
    if ($null -eq $Respuesta -or $Respuesta -isnot [string]) { return $false }
    return [string]::Equals($Respuesta.Trim(), 'PUBLICAR', [StringComparison]::OrdinalIgnoreCase)
}

function LeerLineaConfirmacion {
    return [Console]::ReadLine()
}

function SolicitarConfirmacionPublicacion {
    Write-Host 'Para autorizar este deploy escribi PUBLICAR; cualquier otra respuesta cancela: ' -NoNewline
    try {
        # Una sola lectura de la entrada heredada; no depende de la interfaz Read-Host de PowerShell.
        $respuesta = LeerLineaConfirmacion
    } catch {
        throw 'No se pudo leer la confirmacion: error de entrada de consola. No se autorizo Firebase. Usa una consola interactiva de Visual Studio.'
    }
    if ($null -eq $respuesta) {
        throw 'Entrada de confirmacion no disponible (fin de entrada). No se autorizo Firebase. Usa la consola interactiva de Visual Studio.'
    }
    if (-not (EsConfirmacionPublicacion $respuesta)) {
        foreach ($caracter in $respuesta.ToCharArray()) {
            if ([char]::GetUnicodeCategory($caracter) -eq [Globalization.UnicodeCategory]::Format) {
                throw 'Publicacion cancelada. La respuesta contiene caracteres invisibles; escribi PUBLICAR manualmente.'
            }
        }
        throw 'Publicacion cancelada. La respuesta no fue PUBLICAR; no se ejecuto Firebase Deploy.'
    }
}
