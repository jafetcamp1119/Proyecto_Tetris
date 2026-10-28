using System.Text.Json;

namespace Tetris.Presentacion;

// Lee el estado que genera PowerShell para mostrar la disponibilidad en el panel local.
public static class EstadoPanel
{
    // Lee el archivo compartido y deja de mostrar listo si no se actualiza por 45 segundos.
    public static object Leer(string archivo)
    {
        try
        {
            using var flujo = File.Open(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var datos = JsonDocument.Parse(flujo);
            var raiz = datos.RootElement;
            var actualizado = raiz.GetProperty("actualizadoUtc").GetDateTime();

            if (raiz.GetProperty("estado").GetString() == "listo"
                && DateTime.UtcNow - actualizado > TimeSpan.FromSeconds(45))
            {
                return NoDisponible("La comprobacion del sistema dejo de actualizarse.");
            }

            return raiz.Clone();
        }
        catch
        {
            return new
            {
                estado = "preparando",
                mensaje = "PREPARANDO CONEXION",
                actualizadoUtc = DateTime.UtcNow
            };
        }
    }

    // Prepara un estado de error con la hora actual.
    public static object NoDisponible(string mensaje)
    {
        return new
        {
            estado = "error",
            mensaje,
            actualizadoUtc = DateTime.UtcNow
        };
    }

    // Escribe el error que consultará el panel local.
    public static void GuardarError(string archivo, string mensaje)
    {
        File.WriteAllText(archivo, JsonSerializer.Serialize(NoDisponible(mensaje)));
    }

    // Conserva el error del script o registra que el iniciador terminó sin disponibilidad confirmada.
    public static void RegistrarFinIniciador(string archivo)
    {
        var estado = Leer(archivo);

        if (estado is JsonElement datos && datos.GetProperty("estado").GetString() == "error")
        {
            // El script ya dejo el destino y la causa; conservar ese diagnostico.
            return;
        }

        GuardarError(
            archivo,
            "SERVIDOR NO DISPONIBLE. El iniciador termino sin confirmar disponibilidad; revisa la consola."
        );
    }
}
