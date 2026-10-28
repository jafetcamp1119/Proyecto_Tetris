using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Dominio.EntidadesTipadas;
using Dominio.Juego;
using Utilitarios;

internal static partial class PruebasHttp
{
    private static async Task<T> DatosAsync<T>(HttpResponseMessage respuesta)
    {
        using (respuesta)
        {
            var datos = await respuesta.Content.ReadFromJsonAsync<Respuesta<T>>(Json);
            if (!respuesta.IsSuccessStatusCode || datos?.Success != true) { throw new Exception("Fallo el escenario aislado: " + respuesta.RequestMessage?.RequestUri?.AbsolutePath + " " + respuesta.StatusCode + " " + datos?.Error); }
            return datos.Data!;
        }
    }

    private static void UsarToken(HttpClient cliente, TAutenticacion sesion)
    {
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion.Token);
    }

    private static async Task VerificarTransferenciasAsync(Uri direccion, Action<bool, string> verificar)
    {
        foreach (var variante in new[] { (ModoJuego.Individual, false, false), (ModoJuego.Compartido, false, false),
            (ModoJuego.Competitivo, false, false), (ModoJuego.Individual, true, false),
            (ModoJuego.Compartido, false, true), (ModoJuego.Competitivo, false, true) })
        {
            var (modo, pausar, privada) = variante;
            using var cliente = new HttpClient { BaseAddress = direccion };
            cliente.DefaultRequestHeaders.Add("X-IP-Prueba", "127.0.1." + (10 + (int)modo + (pausar ? 20 : 0) + (privada ? 40 : 0)));
            var primero = await DatosAsync<TAutenticacion>(await cliente.PostAsJsonAsync("/api/usuarios/registrar", new TRegistroUsuario { NombreUsuario = "Transferencia" + modo + (pausar ? "Pausa" : "") + (privada ? "Privada" : "") }));
            UsarToken(cliente, primero);
            var sala = await DatosAsync<TSala>(await cliente.PostAsJsonAsync("/api/salas", new TCrearSala { Modo = modo, Privada = privada }, Json));
            await using var anterior = await HubPrueba.ConectarAsync(cliente, direccion, primero.Token);
            await anterior.InvocarAsync("Entrar", sala.Id);
            await using var observador = await HubPrueba.ConectarAsync(cliente, direccion, primero.Token);
            await observador.InvocarAsync("Entrar", Guid.NewGuid());
            await using var rival = modo != ModoJuego.Individual ? await PrepararSegundoAsync(cliente, sala, modo) : null;
            UsarToken(cliente, primero);
            await DatosAsync<TSala>(await cliente.PutAsJsonAsync($"/api/salas/{sala.Id}/listo", true));
            await DatosAsync<TSala>(await cliente.PostAsync($"/api/salas/{sala.Id}/iniciar", null));
            var accion = modo == ModoJuego.Individual ? Movimiento.Caer : Movimiento.Bajar;
            await anterior.InvocarAsync("Mover", sala.Id, new TMovimiento { Movimiento = accion, Secuencia = 1 });
            if (pausar) { await anterior.InvocarAsync("SolicitarPausa", sala.Id); }
            var antes = await DatosAsync<TSala>(await cliente.GetAsync($"/api/salas/{sala.Id}"));
            var vistaAntes = await DatosAsync<TVistaPreviaPrivada>(await cliente.GetAsync($"/api/salas/{sala.Id}/siguiente-pieza"));
            await TransferirYComprobarAsync(cliente, direccion, anterior, primero, antes, vistaAntes, verificar);
            var notificacionGlobal = await observador.EsperarCierreSesionAsync();
            verificar(notificacionGlobal.GetProperty("codigo").GetString() == "SesionReemplazada", modo + ": avisa tambien a la conexion sin sala");
        }
        using var ataques = new HttpClient { BaseAddress = direccion };
        ataques.DefaultRequestHeaders.Add("X-IP-Prueba", "127.0.1.99");
        for (var intento = 1; intento <= 6; intento++)
        {
            using var respuesta = await ataques.PostAsJsonAsync("/api/usuarios/recuperar-codigo", new TRecuperarCodigo { Credencial = "AAAA-AAAA-AAAA" });
            verificar(respuesta.StatusCode == (intento <= 5 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests), "recuperacion limita el intento " + intento);
        }
        await VerificarIngresosConcurrentesAsync(direccion, verificar);
    }

    private static async Task VerificarIngresosConcurrentesAsync(Uri direccion, Action<bool, string> verificar)
    {
        using var cliente = new HttpClient { BaseAddress = direccion };
        cliente.DefaultRequestHeaders.Add("X-IP-Prueba", "127.0.1.100");
        var original = await DatosAsync<TAutenticacion>(await cliente.PostAsJsonAsync("/api/usuarios/registrar", new TRegistroUsuario { NombreUsuario = "IngresoSimultaneo" }));
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => cliente.PostAsJsonAsync("/api/usuarios/recuperar", new TRecuperarSesion { Credencial = original.Credencial! })));
        var vigentes = 0;
        foreach (var respuesta in respuestas)
        {
            if (respuesta.StatusCode == HttpStatusCode.Conflict)
            {
                respuesta.Dispose();
                continue;
            }
            var sesion = await DatosAsync<TAutenticacion>(respuesta);
            UsarToken(cliente, sesion);
            using var comprobacion = await cliente.GetAsync("/api/usuarios/yo");
            if (comprobacion.IsSuccessStatusCode) { vigentes++; }
            else
            {
                var error = await comprobacion.Content.ReadFromJsonAsync<Respuesta<object>>(Json);
                verificar(error?.CodigoError == "SesionReemplazada", "el ingreso concurrente desplazado recibe el motivo correcto");
            }
        }
        verificar(vigentes == 1, "dos ingresos HTTP simultaneos dejan exactamente una sesion autorizada");
    }

    private static async Task<HubPrueba> PrepararSegundoAsync(HttpClient cliente, TSala sala, ModoJuego modo)
    {
        var salaId = sala.Id;
        var segundo = await DatosAsync<TAutenticacion>(await cliente.PostAsJsonAsync("/api/usuarios/registrar", new TRegistroUsuario { NombreUsuario = "Rival" + modo + (sala.Privada ? "Privada" : "") }));
        UsarToken(cliente, segundo);
        var destino = sala.Privada ? $"/api/salas/codigo/{sala.Codigo}/unirse" : $"/api/salas/{salaId}/unirse";
        await DatosAsync<TSala>(await cliente.PostAsync(destino, null));
        var conexion = await HubPrueba.ConectarAsync(cliente, cliente.BaseAddress!, segundo.Token);
        await conexion.InvocarAsync("Entrar", salaId);
        await DatosAsync<TSala>(await cliente.PutAsJsonAsync($"/api/salas/{salaId}/listo", true));
        return conexion;
    }

    private static async Task TransferirYComprobarAsync(HttpClient cliente, Uri direccion, HubPrueba anterior,
        TAutenticacion primero, TSala antes, TVistaPreviaPrivada vistaAntes, Action<bool, string> verificar)
    {
        var nueva = antes.Modo == ModoJuego.Individual
            ? await DatosAsync<TAutenticacion>(await cliente.PostAsJsonAsync("/api/usuarios/recuperar-codigo", new TRecuperarCodigo { Credencial = primero.CodigoRecuperacionNuevo! }))
            : await DatosAsync<TAutenticacion>(await cliente.PostAsJsonAsync("/api/usuarios/recuperar", new TRecuperarSesion { Credencial = primero.Credencial! }));
        verificar(nueva.SalaActivaId == antes.Id && nueva.UsuarioId == primero.UsuarioId, antes.Modo + ": nueva sesion descubre la misma partida");
        var notificacion = await anterior.EsperarCierreSesionAsync();
        verificar(notificacion.GetProperty("codigo").GetString() == "SesionReemplazada" && notificacion.EnumerateObject().Count() == 1,
            antes.Modo + ": notifica inmediatamente a la sesion anterior sin secretos");
        using var invalida = await cliente.GetAsync("/api/usuarios/yo");
        verificar(invalida.StatusCode == HttpStatusCode.Unauthorized, antes.Modo + ": JWT anterior revocado inmediatamente");
        var errorSesion = await invalida.Content.ReadFromJsonAsync<Respuesta<object>>(Json);
        verificar(errorSesion?.CodigoError == "SesionReemplazada", antes.Modo + ": HTTP identifica reemplazo sin interpretar textos");
        using var renovacionAnterior = await cliente.PostAsJsonAsync("/api/usuarios/renovar", new TRecuperarSesion { Credencial = primero.Credencial! });
        verificar(renovacionAnterior.StatusCode == HttpStatusCode.Unauthorized, antes.Modo + ": el dispositivo desplazado no retoma control al renovar");
        var errorRenovacion = await renovacionAnterior.Content.ReadFromJsonAsync<Respuesta<object>>(Json);
        verificar(errorRenovacion?.CodigoError == "SesionReemplazada", antes.Modo + ": renovar identifica el reemplazo al reabrir la app");
        var cerrado = false;
        try { await anterior.InvocarAsync("Mover", antes.Id, new TMovimiento { Movimiento = Movimiento.Derecha, Secuencia = 2 }); }
        catch (Exception error) when (error is WebSocketException or InvalidOperationException) { cerrado = true; }
        verificar(cerrado, antes.Modo + ": WebSocket anterior pierde control");
        UsarToken(cliente, nueva);
        if (nueva.CodigoRecuperacionPorConfirmar) {
            var confirmada = await DatosAsync<bool>(await cliente.PostAsJsonAsync("/api/usuarios/confirmar-codigo-recuperacion", new TRecuperarCodigo { Credencial = nueva.CodigoRecuperacionNuevo! }));
            verificar(confirmada, antes.Modo + ": codigo nuevo se activa despues de guardar");
        }
        await using var actual = await HubPrueba.ConectarAsync(cliente, direccion, nueva.Token);
        var retomada = (await actual.InvocarAsync("Entrar", antes.Id)).Data!;
        var longitudCodigo = antes.Modo == ModoJuego.Individual ? 0 : 6;
        verificar(retomada.Codigo == antes.Codigo && retomada.Codigo.Length == longitudCodigo,
            antes.Modo + ": transferencia conserva el código multijugador o su ausencia en Individual");
        verificar(actual.EventosSesion.Count == 0, antes.Modo + ": la notificacion nunca expulsa la nueva sesion");
        verificar(JsonSerializer.Serialize(antes.Tableros, Json) == JsonSerializer.Serialize(retomada.Tableros, Json), antes.Modo + ": tablero completo, pieza y puntuacion conservados");
        verificar(retomada.UsuarioTurno == antes.UsuarioTurno && retomada.Estado == antes.Estado && retomada.Jugadores.Length == antes.Jugadores.Length,
            antes.Modo + ": sala, turno y participantes sin reiniciar ni duplicar");
        verificar(retomada.ProgresoCompartido == antes.ProgresoCompartido, antes.Modo + ": transferencia conserva progreso global autorizado");
        verificar(retomada.Pausa.Fase == antes.Pausa.Fase && retomada.Pausa.FinPausaUtc == antes.Pausa.FinPausaUtc,
            antes.Modo + ": transferencia conserva fase y vencimiento de pausa");
        var vista = await DatosAsync<TVistaPreviaPrivada>(await cliente.GetAsync($"/api/salas/{antes.Id}/siguiente-pieza"));
        verificar(JsonSerializer.Serialize(vista.Pieza, Json) == JsonSerializer.Serialize(vistaAntes.Pieza, Json), antes.Modo + ": reserva privada conservada");
        var movimiento = await actual.InvocarAsync("Mover", antes.Id, new TMovimiento { Movimiento = Movimiento.Bajar, Secuencia = 2 });
        verificar(movimiento.Success == (antes.Pausa.Fase != FasePausa.Pausada), antes.Modo + ": nueva sesion respeta secuencia y pausa original");
    }
}
